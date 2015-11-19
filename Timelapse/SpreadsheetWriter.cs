using System;
using System.Xml;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.IO;
using System.Windows.Shapes;
using System.Data;
using Timelapse;

namespace Timelapse
{
    /// <summary>
    /// Write all the data in the database to the CSV file indicated in the file path
    /// </summary>
    static class SpreadsheetWriter
    {
        #region Public methods
        /// <summary>
        /// Export all the data in the database to the CSV file indicated in the file path so that spreadsheet applications (like Excel) can display it.
        /// </summary>
        /// <param name="db"></param>
        /// <param name="filepath"></param>
        public static void ExportDataAsCSV(DBData db, string filepath)
        {
            DataTable templateTable = db.templateTable;     // We use the templateTable to get the column names
            DataTable dataTable = db.GetImagesAllForExporting(); // THis returns ALL images, regardless of the currently filtered view.
            try
            {
                if (File.Exists(filepath)) File.Delete(filepath);   // Delete the file if it exists.

                TextWriter tw = new StreamWriter(filepath, false);

                // Write the header as defined by the data labels in the template file
                // If the data label is an empty string, we use the label instead.
                string header = "";
                string label;
                string datalabel;
                List<string> datalabels = new List<string>();
                for (int i = 0; i < templateTable.Rows.Count; i++ )
                {
                    label = (string)templateTable.Rows[i][Constants.LABEL];
                    datalabel = (string)templateTable.Rows[i][Constants.DATALABEL];
                    header += addColumn(getLabel(label, datalabel));

                    // get a list of datalabels so we can add columns in the order that matches the current template table order
                    if (Constants.ID != datalabel) datalabels.Add(datalabel);
                }
                tw.WriteLine(header);


                // For each row in the data table, write out the columns in the same order as the 
                // data labels in the template file
                for (int i = 0; i < dataTable.Rows.Count; i++)
                {
                    string row = "";
                     foreach (string dataLabel in datalabels)
                     {
                         row += addColumn((string)db.dataTable.Rows[i][dataLabel]);
                     }
                     tw.WriteLine(row);
                }
                tw.Close();
            }
            catch
            {
                Messages.CantWriteExcelFile(filepath);
            }
        }
        #endregion

        #region Private methods

        // Returms the datalabel if it isn't empty, otherwise the label
        private static string getLabel(string label, string datalabel)
        {
            return (datalabel == "") ? label : datalabel;
        }

        // Check if there is any Quotation Mark '"', a Comma ',', a Line Feed \x0A,  or Carriage Return \x0D
        // and escape it as needed
        private static string addColumn(string value)
        {
            if (value == null) return ",";
            if (value.IndexOfAny("\",\x0A\x0D".ToCharArray()) > -1)
                return("\"" + value.Replace("\"", "\"\"") + "\"" + ",");
            else
                return (value + ",");
        }
        #endregion 
    }
}
