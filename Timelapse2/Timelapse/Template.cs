using System;
using System.Collections.Generic;
using System.Data;
using System.IO;

namespace Timelapse
{
    /// <summary>
    /// Open the Timelapse Template Database. Place the Template Table contents into memory (the templateTable)
    /// </summary>
    public class Template
    {
        #region Private variables
        private SQLiteWrapper DB;            
        private DataTable m_templateTable = new DataTable();     // A table containing all the names of the data fields 
        // public Dictionary<String, String> typeToKey = new Dictionary<String, String>();
        #endregion 

        #region Public Properties 

        
        /// <summary>
        /// The folder path where the database should be located
        /// </summary>
        public string Folder { get; set; }   
   
        /// <summary>
        /// The file name of the template db lives
        /// </summary>
        public string Filename { get; set; } 
 
        /// <summary>
        /// The complete path (including file name) of the template db
        /// </summary>
        public string FilePath { get; set; }   
   
        /// <summary>
        /// // This datatable will contain the template
        /// </summary>
        public DataTable templateTable        
        {
            set { m_templateTable = value; }
            get { return m_templateTable; }
        }


        /// Returns true of the Timelapse Template database exists 
        /// </summary>
        public bool Exists { get { return File.Exists(this.FilePath); } }    

        #endregion

        #region Public methods
        /// <summary>Constructor </summary>
        public Template () {}

        /// <summary>
        /// Create and assign the connection to the Timelapse Data database
        /// </summary>
        public bool Open(string folder, string filename)
        {
            // Initialize some variables so we remember them
            this.Folder = folder;
            this.Filename = filename;
            this.FilePath = System.IO.Path.Combine (this.Folder, this.Filename);
            
            // Check that the database exists and that it can be opened
            if (!this.Exists) return false;
            
            // Open a connection to the template DB
            try 
            { 
                this.DB = new SQLiteWrapper(this.FilePath);    // Create a pointer to the database and assign it
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Load the Template Table into the data structure Data database. 
        /// Assumes that the database has already been opened. 
        /// Also verify that the Labels and Data Labels are non-empty, where it updates the TemplateTable (in the database and in memory) as needed
        /// </summary>
        public void LoadTemplateTable()
        {
            // All the code below goes through the template table to see if there are any non-empty labels / data labels,
            // and if so, updates them to a reasonable value. If both are empty, it keeps track of its type and creates
            // a label called (say) Counter3 for the third counter that has no label. If there is no DataLabel value, it
            // makes it the same as the label. Ultimately, it guarantees that there will always be a (hopefully unique)
            // data label and label name. 
            // As well, the contents of the template table are loaded into memory.

            // The template table will hold all the values of the TableTemplate in the database
            bool result;
            string command_executed;
            this.templateTable = new DataTable();

            this.templateTable = this.DB.GetDataTableFromSelect(Constants.Database.SelectStarFrom + Constants.Database.TemplateTable + " ORDER BTemplateDatabase.Y  " + Constants.Database.SpreadsheetOrder + "  ASC", out result, out command_executed);
            DataRow row;            // The current row
            String label = "";      // The row's label
            String data_label = ""; // The row's data label
            String type = "";       // The row's type
            String temp = "";

            int counter_count = 0;  // The number of counters/ choices/ notes seen so far
            int choice_count = 0;
            int note_count = 0;
            int flag_count = 0;

            Dictionary<String, Object> dataline = new Dictionary<String, Object>();    // Will hold key/value pairs that have change din the current row
            // For each row...
            for (int i = 0; i < templateTable.Rows.Count; i++)
            {
                row = templateTable.Rows[i];

                // Get various values from each row
                type = (string)row[Constants.Database.Type];
                // Not sure why, but if its an empty value it doesn't like it. Therefore we do this in a try/catch.
                try { label = (string)row[Constants.Control.Label]; }
                catch { label = ""; }
                try { data_label = (string)row[Constants.Control.DataLabel]; }
                catch { data_label = ""; }

                // Increment the times we have seen a particular type, and compose a possible unique label identifying it (e.g., Counter3)
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
                        temp = "";
                        break;
                }

                // Check if various values are empty, and if so update the row and fill the dataline with appropriate defaults
                dataline.Clear();
                if ("" == data_label.Trim() && "" == label.Trim()) // No labels / data labels, so use the ones we created
                {
                    dataline.Add(Constants.Control.Label, temp);
                    dataline.Add(Constants.Control.DataLabel, temp);
                    row[Constants.Control.Label] = temp;
                    row[Constants.Control.DataLabel] = temp;
                }
                else if ("" == data_label.Trim())   // No data label but a label, so use the label's value
                {
                    dataline.Add(Constants.Control.DataLabel, label);
                    row[Constants.Control.DataLabel] = row[Constants.Control.Label];
                }

                // Now add the new values to the database
                if (dataline.Count > 0)
                {
                    string id = row[Constants.Database.ID].ToString();
                    string cmd = Constants.Database.ID + " = " + id;
                    this.DB.UpdateWhere(Constants.Database.TemplateTable, dataline, cmd, out result, out command_executed);
                }
            }
        }

        // Gets the schema for the database, i.e., the column definitions.
        // Currently unused
        public void GetSchema()
        {
            bool result = false;
            string command_executed = "";
          
            DataTable temp = this.DB.GetDataTableFromSelect("Pragma table_info('" + Constants.Database.TemplateTable + "')", out result, out  command_executed);
            if (result == false) return;
            for (int i = 0; i < temp.Rows.Count; i++)
            {
                string s = "=Template===";
                for (int j = 0; j < temp.Columns.Count; j++)
                {
                    s += " " + temp.Rows[i][j].ToString();
                }
                System.Diagnostics.Debug.Print(s);
            }
        }
        #endregion
    }
}
