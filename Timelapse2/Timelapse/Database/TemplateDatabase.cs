using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;

namespace Timelapse.Database
{
    /// <summary>
    /// Open the Timelapse Template Database. Place the Template Table contents into memory (the templateTable)
    /// </summary>
    public class TemplateDatabase
    {
        private SQLiteWrapper database;

        /// <summary>
        /// Gets or sets the complete path (including file name) of the template database
        /// </summary>
        public string FilePath { get; private set; }

        /// <summary>
        /// Gets or sets the template table
        /// </summary>
        public DataTable TemplateTable { get; private set; }

        /// <summary>
        /// Create and assign the connection to the Timelapse Data database
        /// </summary>
        public static bool TryOpen(string filePath, out TemplateDatabase template)
        {
            // Check that the database exists and that it can be opened
            if (!File.Exists(filePath))
            {
                template = null;
                return false;
            }

            // Open a connection to the template DB
            try
            {
                template = new TemplateDatabase();
                template.database = new SQLiteWrapper(filePath);    // Create a pointer to the database and assign it
                template.FilePath = filePath;
                template.LoadTemplateTable();
                return true;
            }
            catch
            {
                template = null;
                return false;
            }
        }

        /// <summary>
        /// Load the Template Table into the data structure Data database. 
        /// Assumes that the database has already been opened. 
        /// Also verify that the Labels and Data Labels are non-empty, where it updates the TemplateTable (in the database and in memory) as needed
        /// </summary>
        private void LoadTemplateTable()
        {
            // All the code below goes through the template table to see if there are any non-empty labels / data labels,
            // and if so, updates them to a reasonable value. If both are empty, it keeps track of its type and creates
            // a label called (say) Counter3 for the third counter that has no label. If there is no DataLabel value, it
            // makes it the same as the label. Ultimately, it guarantees that there will always be a (hopefully unique)
            // data label and label name. 
            // As well, the contents of the template table are loaded into memory.

            // The template table will hold all the values of the TableTemplate in the database
            DataTable templateTable;
            bool result = this.database.TryGetDataTableFromSelect(Constants.Database.SelectStarFrom + Constants.Database.TemplateTable + " ORDER BY  " + Constants.Database.SpreadsheetOrder + "  ASC", out templateTable);
            Debug.Assert(result == true && templateTable != null, String.Format("Loading template table from {0} failed.", this.FilePath));
            this.TemplateTable = templateTable;

            int counter_count = 0;  // The number of counters/ choices/ notes seen so far
            int choice_count = 0;
            int note_count = 0;
            int flag_count = 0;

            Dictionary<String, Object> dataline = new Dictionary<String, Object>();    // Will hold key/value pairs that have change din the current row
            // For each row...
            for (int i = 0; i < this.TemplateTable.Rows.Count; i++)
            {
                DataRow row = this.TemplateTable.Rows[i];

                // Get various values from each row
                string type = (string)row[Constants.Database.Type];
                // Not sure why, but if its an empty value it doesn't like it. Therefore we do this in a try/catch.
                string label;      // The row's label
                try
                {
                    label = (string)row[Constants.Control.Label];
                }
                catch
                {
                    label = String.Empty;
                }

                string data_label; // The row's data label
                try
                {
                    data_label = (string)row[Constants.Control.DataLabel];
                }
                catch
                {
                    data_label = String.Empty;
                }

                // Increment the times we have seen a particular type, and compose a possible unique label identifying it (e.g., Counter3)
                string temp;
                switch (type)
                {
                    case Constants.DatabaseElement.Counter:
                        counter_count++;
                        temp = type + counter_count.ToString();
                        break;
                    case Constants.DatabaseElement.FixedChoice:
                        choice_count++;
                        temp = type + choice_count.ToString();
                        break;
                    case Constants.DatabaseElement.Note:
                        note_count++;
                        temp = type + note_count.ToString();
                        break;
                    case Constants.DatabaseElement.Flag:
                        flag_count++;
                        temp = type + flag_count.ToString();
                        break;
                    default:
                        temp = String.Empty;
                        break;
                }

                // Check if various values are empty, and if so update the row and fill the dataline with appropriate defaults
                dataline.Clear();
                if (String.Empty == data_label.Trim() && String.Empty == label.Trim())
                {
                    // No labels / data labels, so use the ones we created
                    dataline.Add(Constants.Control.Label, temp);
                    dataline.Add(Constants.Control.DataLabel, temp);
                    row[Constants.Control.Label] = temp;
                    row[Constants.Control.DataLabel] = temp;
                }
                else if (String.Empty == data_label.Trim())
                {
                    // No data label but a label, so use the label's value
                    dataline.Add(Constants.Control.DataLabel, label);
                    row[Constants.Control.DataLabel] = row[Constants.Control.Label];
                }

                // Now add the new values to the database
                if (dataline.Count > 0)
                {
                    string id = row[Constants.Database.ID].ToString();
                    string cmd = Constants.Database.ID + " = " + id;
                    string command_executed;
                    this.database.UpdateWhere(Constants.Database.TemplateTable, dataline, cmd, out result, out command_executed);
                }
            }
        }
    }
}
