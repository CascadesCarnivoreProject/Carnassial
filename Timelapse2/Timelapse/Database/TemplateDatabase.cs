using System;
using System.Data;
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
        /// Gets the complete path (including file name) of the template database
        /// </summary>
        public string FilePath { get; private set; }

        /// <summary>
        /// Gets the template table
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
            this.TemplateTable = this.database.GetDataTableFromSelect(Constants.Database.SelectStarFrom + Constants.Database.TemplateTable + " ORDER BY  " + Constants.Database.SpreadsheetOrder + "  ASC");

            // For each row...
            int counterCount = 0;  // The number of counters/ choices/ notes seen so far
            int choiceCount = 0;
            int flagCount = 0;
            int noteCount = 0;
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

                string dataLabel; // The row's data label
                try
                {
                    dataLabel = (string)row[Constants.Control.DataLabel];
                }
                catch
                {
                    dataLabel = String.Empty;
                }

                // Increment the times we have seen a particular type, and compose a possible unique label identifying it (e.g., Counter3)
                string uniqueFallbackLabel;
                switch (type)
                {
                    case Constants.DatabaseColumn.Counter:
                        counterCount++;
                        uniqueFallbackLabel = type + counterCount.ToString();
                        break;
                    case Constants.DatabaseColumn.FixedChoice:
                        choiceCount++;
                        uniqueFallbackLabel = type + choiceCount.ToString();
                        break;
                    case Constants.DatabaseColumn.Note:
                        noteCount++;
                        uniqueFallbackLabel = type + noteCount.ToString();
                        break;
                    case Constants.DatabaseColumn.Flag:
                        flagCount++;
                        uniqueFallbackLabel = type + flagCount.ToString();
                        break;
                    default:
                        uniqueFallbackLabel = String.Empty;
                        break;
                }

                // Check if various values are empty, and if so update the row and fill the dataline with appropriate defaults
                ColumnTuplesWithWhere columnsToUpdate = new ColumnTuplesWithWhere();    // Will hold key/value pairs that have change din the current row
                if (String.Empty == dataLabel.Trim() && String.Empty == label.Trim())
                {
                    // No labels / data labels, so use the ones we created
                    columnsToUpdate.Columns.Add(new ColumnTuple(Constants.Control.Label, uniqueFallbackLabel));
                    columnsToUpdate.Columns.Add(new ColumnTuple(Constants.Control.DataLabel, uniqueFallbackLabel));
                    row[Constants.Control.Label] = uniqueFallbackLabel;
                    row[Constants.Control.DataLabel] = uniqueFallbackLabel;
                }
                else if (String.Empty == dataLabel.Trim())
                {
                    // No data label but a label, so use the label's value
                    columnsToUpdate.Columns.Add(new ColumnTuple(Constants.Control.DataLabel, label));
                    row[Constants.Control.DataLabel] = row[Constants.Control.Label];
                }

                // Now add the new values to the database
                if (columnsToUpdate.Columns.Count > 0)
                {
                    long id = (long)row[Constants.Database.ID];
                    columnsToUpdate.SetWhere(id);
                    this.database.Update(Constants.Database.TemplateTable, columnsToUpdate);
                }
            }
        }
    }
}
