using System.Collections.Generic;
using System.Windows;
using System.IO;
using System.Data;
using System.Diagnostics;

namespace Timelapse
{
    /// <summary>
    /// Write all the data in the database to the CSV file indicated in the file path
    /// </summary>
    static class SpreadsheetWriter
    {
        #region Public methods
        /// <summary>
        /// Export all the database data associated with the filtered view to the CSV file indicated in the file path so that spreadsheet applications (like Excel) can display it.
        /// </summary>
        /// <param name="db"></param>
        /// <param name="filepath"></param>
        public static void ExportDataAsCSV(DBData db, string filepath)
        {
            TextWriter tw = new StreamWriter(filepath, false);
            try
            {
                // Write the header as defined by the data labels in the template file
                // If the data label is an empty string, we use the label instead.
                string header = "";
                string label;
                string datalabel;
                List<string> datalabels = new List<string>();
                for (int i = 0; i < db.templateTable.Rows.Count; i++)
                {
                    label = (string)db.templateTable.Rows[i][Constants.Control.Label];
                    datalabel = (string)db.templateTable.Rows[i][Constants.Control.DataLabel];
                    header += addColumn(getLabel(label, datalabel));

                    // get a list of datalabels so we can add columns in the order that matches the current template table order
                    if (Constants.Database.ID != datalabel) datalabels.Add(datalabel);
                }
                tw.WriteLine(header);
                // For each row in the data table, write out the columns in the same order as the 
                // data labels in the template file
                for (int i = 0; i < db.dataTable.Rows.Count; i++)
                {
                    string row = "";
                    foreach (string dataLabel in datalabels)
                    {
                        row += addColumn((string)db.dataTable.Rows[i][dataLabel]);
                    }
                    tw.WriteLine(row);
                }   
            }
            catch
            {
                // Can't write the spreadsheet file
                DlgMessageBox dlgMB = new DlgMessageBox();
                dlgMB.IconType = MessageBoxImage.Error;
                dlgMB.ButtonType = MessageBoxButton.OK;

                dlgMB.MessageTitle = "Can't write the spreadsheet file.";
                dlgMB.MessageProblem = "The following file can't be written: " + filepath + ".";
                dlgMB.MessageReason = "You may already have it open in Excel or another  application.";
                dlgMB.MessageSolution = "If the file is open in another application, close it and try again.";
                dlgMB.ShowDialog();
            }
            tw.Close();
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
