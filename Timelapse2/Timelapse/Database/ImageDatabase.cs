using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using Timelapse.Images;
using Timelapse.Util;

namespace Timelapse.Database
{
    public class ImageDatabase
    {
        // A pointer to the Timelapse Data database
        private SQLiteWrapper database;

        /// <summary>Gets the file name of the image database on disk.</summary>
        public string FileName { get; private set; }

        /// <summary>Gets the complete path to the folder containing the image database.</summary>
        public string FolderPath { get; private set; }

        /// <summary>Gets or sets the database's template table.</summary>
        public DataTable TemplateTable { get; set; }

        /// <summary>Gets the table that has information about the entire image set</summary>
        public DataTable ImageSetTable { get; private set; }

        // the Id of the current record. -1 if its not pointing to anything or if the database is empty
        public int CurrentId { get; set; }
        public int CurrentRow { get; set; }
        public Hashtable DataLabelFromType { get; private set; }

        // contains the results of the data query
        public DataTable DataTable { get; private set; }
        // contains the markers
        public DataTable MarkerTable { get; private set; }
        public Hashtable TypeFromKey { get; private set; }

        public ImageDatabase(string folderPath, string fileName)
        {
            this.CurrentId = -1;
            this.CurrentRow = -1;
            this.DataLabelFromType = new Hashtable();
            this.DataTable = new DataTable();
            this.FolderPath = folderPath;
            this.FileName = fileName;
            this.MarkerTable = new DataTable();
            this.TypeFromKey = new Hashtable();
        }

        /// <summary>Gets the number of images currently in the image table.</summary>
        public int ImageCount
        {
            get { return this.DataTable.Rows.Count; }
        }

        public string Log
        {
            get { return this.ImageSetGetValue(Constants.DatabaseElement.Log); }
            set { this.UpdateRow(1, Constants.DatabaseElement.Log, value, Constants.Database.ImageSetTable); }
        }

        public bool State_Magnifyer
        {
            get
            {
                string result = this.ImageSetGetValue(Constants.DatabaseElement.Magnifier);
                return Convert.ToBoolean(result);
            }
            set
            {
                this.UpdateRow(1, Constants.DatabaseElement.Magnifier, value.ToString(), Constants.Database.ImageSetTable);
            }
        }

        public int State_Row
        {
            get
            {
                string result = this.ImageSetGetValue(Constants.DatabaseElement.Row);
                return Convert.ToInt32(result);
            }
            set
            {
                this.UpdateRow(1, Constants.DatabaseElement.Row, value.ToString(), Constants.Database.ImageSetTable);
            }
        }

        public int State_Filter
        {
            get
            {
                string result = this.ImageSetGetValue(Constants.DatabaseElement.Filter);
                return Convert.ToInt32(result);
            }
            set
            {
                int ifilter = (int)value;
                this.UpdateRow(1, Constants.DatabaseElement.Filter, ifilter.ToString(), Constants.Database.ImageSetTable);
            }
        }

        public bool State_WhiteSpaceTrimmed
        {
            get
            {
                string result = this.ImageSetGetValue(Constants.DatabaseElement.WhiteSpaceTrimmed);
                return Convert.ToBoolean(result);
            }
            set
            {
                this.UpdateRow(1, Constants.DatabaseElement.WhiteSpaceTrimmed, value.ToString(), Constants.Database.ImageSetTable);
            }
        }

        public int FindFile()
        {
            string[] files = System.IO.Directory.GetFiles(this.FolderPath, "*.ddb");
            if (files.Count() == 1)
            {
                this.FileName = System.IO.Path.GetFileName(files[0]); // Get the file name, excluding the path
                return 0;  // 0 means we have a valid .ddb filename
            }
            else if (files.Count() > 1)
            {
                DialogChooseDataBaseFile dlg = new DialogChooseDataBaseFile(files);
                bool? result = dlg.ShowDialog();
                if (result == true)
                {
                    this.FileName = dlg.SelectedFile; // 0 means we have a valid .ddb filename
                    return 0;
                }
                else
                {
                    return 1; // User cancelled the file selection operation
                }
            }
            return 2; // There are no existing .ddb files files
        }

        #region Public methods for creating the database and the lookup tables
        /// <summary>
        /// Create a database file (if needed) and connect to it. Also keeps a local copy of the template
        /// </summary>
        /// <returns>true if the database could be created, false otherwise</returns>
        public bool TryCreateImageDatabase(TemplateDatabase template)
        {
            // Create the DB
            try
            {
                this.database = new SQLiteWrapper(this.GetFilePath());
                this.TemplateTable = template.TemplateTable;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Make an empty Data Table based on the information in the Template Table.
        /// Assumes that the database has already been opened and that the Template Table is loaded, where the DataLabel always has a valid value.
        /// Then create both the ImageSet table and the Markers table
        /// </summary>
        public void CreateTables()
        {
            // 1. Create the Data Table from the template
            // First, define the creation string based on the contents of the template. 
            Int64 id;
            string db_label;
            string default_value;
            bool result;
            string command_executed = String.Empty;
            Dictionary<string, string> column_definition = new Dictionary<string, string>();

            column_definition.Add(Constants.Database.ID, Constants.Database.CreationStringPrimaryKey);  // It begins with the ID integer primary key
            foreach (DataRow row in this.TemplateTable.Rows)
            {
                id = (Int64)row[Constants.Database.ID];
                db_label = (string)row[Constants.Control.DataLabel];
                default_value = (string)row[Constants.Control.DefaultValue];

                column_definition.Add(db_label, " Text '" + default_value + "'");
            }
            this.database.CreateTable(Constants.Database.DataTable, column_definition, out result, out command_executed);
            // Debug.Print (result.ToString() + " " + command_executed);
            string command = "Select * FROM " + Constants.Database.DataTable;
            DataTable dataTable;
            result = this.database.TryGetDataTableFromSelect(command, out dataTable);
            this.DataTable = dataTable;

            // 2. Create the ImageSetTable and initialize a single row in it
            column_definition.Clear();
            column_definition.Add(Constants.Database.ID, Constants.Database.CreationStringPrimaryKey);  // It begins with the ID integer primary key
            column_definition.Add(Constants.DatabaseElement.Log, " TEXT DEFAULT 'Add text here.'");

            column_definition.Add(Constants.DatabaseElement.Magnifier, " TEXT DEFAULT 'true'");
            column_definition.Add(Constants.DatabaseElement.Row, " TEXT DEFAULT '0'");
            int ifilter = (int)ImageQualityFilter.All;
            column_definition.Add(Constants.DatabaseElement.Filter, " TEXT DEFAULT '" + ifilter.ToString() + "'");
            this.database.CreateTable(Constants.Database.ImageSetTable, column_definition, out result, out command_executed);

            Dictionary<String, String> dataline = new Dictionary<String, String>(); // Populate the data for the image set with defaults
            dataline.Add(Constants.DatabaseElement.Log, "Add text here");
            dataline.Add(Constants.DatabaseElement.Magnifier, "true");
            dataline.Add(Constants.DatabaseElement.Row, "0");
            dataline.Add(Constants.DatabaseElement.Filter, ifilter.ToString());
            List<Dictionary<string, string>> insertion_statements = new List<Dictionary<string, string>>();
            insertion_statements.Add(dataline);
            this.database.InsertMultiplesBeginEnd(Constants.Database.ImageSetTable, insertion_statements, out result, out command_executed);

            // 3.Create the MarkersTable and initialize a single row in it
            column_definition.Clear();
            column_definition.Add(Constants.Database.ID, Constants.Database.CreationStringPrimaryKey);  // t begins with the ID integer primary key
            string type = String.Empty;
            foreach (DataRow row in this.TemplateTable.Rows)
            {
                type = (string)row[Constants.Database.Type];
                if (type.Equals(Constants.DatabaseElement.Counter))
                {
                    id = (Int64)row[Constants.Database.ID];
                    db_label = (string)row[Constants.Control.DataLabel];
                    string key = db_label;
                    column_definition.Add(db_label, " Text Default ''");
                }
            }
            this.database.CreateTable(Constants.Database.MarkersTable, column_definition, out result, out command_executed);

            // 4. Copy the TemplateTable to this Database
            // First we have to create the table
            Dictionary<string, string> column_definitions = new Dictionary<string, string>();
            column_definitions.Add(Constants.Database.ID, "INTEGER primary key");
            column_definitions.Add(Constants.Database.ControlOrder, "INTEGER");
            column_definitions.Add(Constants.Database.SpreadsheetOrder, "INTEGER");
            column_definitions.Add(Constants.Database.Type, "text");
            column_definitions.Add(Constants.Control.DefaultValue, "text");
            column_definitions.Add(Constants.Control.Label, "text");
            column_definitions.Add(Constants.Control.DataLabel, "text");
            column_definitions.Add(Constants.Control.Tooltop, "text");
            column_definitions.Add(Constants.Control.TextBoxWidth, "text");
            column_definitions.Add(Constants.Control.Copyable, "text");
            column_definitions.Add(Constants.Control.Visible, "text");
            column_definitions.Add(Constants.Control.List, "text");
            this.database.CreateTable(Constants.Database.TemplateTable, column_definitions, out result, out command_executed);

            DataView tempView = this.TemplateTable.DefaultView;
            tempView.Sort = "ID ASC";
            DataTable tempTable = tempView.ToTable();
            if (tempTable.Rows.Count != 0)
            {
                this.database.InsertDataTableIntoTable(Constants.Database.TemplateTable, tempTable, out result, out command_executed);
            }
        }

        /// <summary>Returns true if the data database exists</summary>
        public bool Exists()
        {
            return File.Exists(this.GetFilePath());
        }

        public void InitializeMarkerTableFromDataTable()
        {
            string command = "Select * FROM " + Constants.Database.MarkersTable;
            DataTable markerTable;
            bool result = this.database.TryGetDataTableFromSelect(command, out markerTable);
            this.MarkerTable = markerTable;
        }

        /// <summary>
        /// Create lookup tables that allow us to retrieve a key from a type and vice versa
        /// TODO Should probably change this so its done internally rather than called externally
        /// </summary>
        public void CreateLookupTables()
        {
            Int64 id;
            string data_label;
            string rowtype;
            foreach (DataRow row in this.TemplateTable.Rows)
            {
                id = (Int64)row[Constants.Database.ID];
                data_label = (string)row[Constants.Control.DataLabel];
                rowtype = (string)row[Constants.Database.Type];

                // We don't want to add these types to the hash, as there can be multiple ones, which means the key would not be unique
                if (!(rowtype.Equals(Constants.DatabaseElement.Note) ||
                      rowtype.Equals(Constants.DatabaseElement.FixedChoice) ||
                      rowtype.Equals(Constants.DatabaseElement.Counter) ||
                      rowtype.Equals(Constants.DatabaseElement.Flag)))
                {
                    this.DataLabelFromType.Add(rowtype, data_label);
                }
                this.TypeFromKey.Add(data_label, rowtype);
            }
        }

        public void RenameDataFile(string newFileName, TemplateDatabase template)
        {
            if (File.Exists(Path.Combine(this.FolderPath, this.FileName)))
            {
                File.Move(Path.Combine(this.FolderPath, this.FileName),
                          Path.Combine(this.FolderPath, newFileName));  // Change the file name to the new file name
                this.FileName = newFileName; // Store the file name
                this.TryCreateImageDatabase(template);          // Recreate the database connecction
            }
        }
        #endregion

        #region public methods for returning a copy of a table as is currently in the database
        public DataTable CreateDataTableFromDatabaseTable(string tablename)
        {
            string command = "Select * FROM " + tablename;
            DataTable dataTable;
            bool result = this.database.TryGetDataTableFromSelect(command, out dataTable);
            return dataTable;
        }
        #endregion

        #region Public methods for populating the Tabledata database with some basic filters
        /// <summary> 
        /// Populate the image table so that it matches all the entries in its associated database table.
        /// Then set the currentID and currentRow to the the first record in the returned set
        /// </summary>
        public bool GetImagesAll()
        {
            // Filter: All images
            return this.GetImages("*", String.Empty);
        }

        // Filter: Corrupted images only
        public bool GetImagesCorrupted()
        {
            string where = (string)this.DataLabelFromType[Constants.DatabaseElement.ImageQuality]; // key
            where += "=\"" + Constants.ImageQuality.Corrupted + "\"";  // = value

            return this.GetImages("*", where);
        }

        // Filter: Dark images only
        public bool GetImagesDark()
        {
            string where = (string)this.DataLabelFromType[Constants.DatabaseElement.ImageQuality]; // key
            where += "=\"" + Constants.ImageQuality.Dark + "\"";  // = value

            return this.GetImages("*", where);
        }

        // Filter: Missing images only
        public bool GetImagesMissing()
        {
            string where = (string)this.DataLabelFromType[Constants.DatabaseElement.ImageQuality]; // key
            where += "=\"" + Constants.ImageQuality.Missing + "\"";  // = value

            return this.GetImages("*", where);
        }

        // Filter:  images marked for deltion
        public bool GetImagesMarkedForDeletion()
        {
            string where = (string)this.DataLabelFromType[Constants.DatabaseElement.DeleteFlag]; // key
            where += "=\"true\""; // = value
            return this.GetImages("*", where);
        }

        public DataTable GetDataTableOfImagesMarkedForDeletion()
        {
            string where = (string)this.DataLabelFromType[Constants.DatabaseElement.DeleteFlag]; // key
            where += "=\"true\""; // = value
            return this.GetDataTableOfImages("*", where);
        }

        public bool GetImagesAllButDarkAndCorrupted()
        {
            string where = (string)this.DataLabelFromType[Constants.DatabaseElement.ImageQuality]; // key
            where += " IS NOT \"" + Constants.ImageQuality.Dark + "\"";  // = value
            where += " AND ";
            where += (string)this.DataLabelFromType[Constants.DatabaseElement.ImageQuality];
            where += " IS NOT \"" + Constants.ImageQuality.Corrupted + "\"";  // = value

            return this.GetImages("*", where);
        }

        // Custom filter - for a singe where Col=Value
        public bool GetImagesCustom(string datalabel, string comparison, string value)
        {
            string where = datalabel; // key
            where += comparison + "\"";
            where += value + "\"";  // = value

            return this.GetImages("*", where);
        }

        // Custom Filter - for one or more Col=Value anded or or'd together
        public bool GetImagesCustom(string where_part)
        {
            // where should be of the form datalabel1=value1 AND datalabel2<>value2, etc. 
            return this.GetImages("*", where_part);
        }

        private bool GetImages(string searchstring, string where)
        {
            string query = "Select " + searchstring;
            query += " FROM " + Constants.Database.DataTable;
            if (!where.Equals(String.Empty))
            {
                query += " WHERE " + where;
            }

            // Debug.Print(query);
            DataTable tempTable;
            bool result = this.database.TryGetDataTableFromSelect(query, out tempTable);
            if (tempTable.Rows.Count == 0)
            {
                return false;
            }

            DataTable dataTable;
            result = this.database.TryGetDataTableFromSelect(query, out dataTable);
            this.DataTable = dataTable;
            return true;
        }

        private DataTable GetDataTableOfImages(string searchstring, string where)
        {
            string query = "Select " + searchstring;
            query += " FROM " + Constants.Database.DataTable;
            if (!where.Equals(String.Empty))
            {
                query += " WHERE " + where;
            }

            DataTable tempTable;
            bool result = this.database.TryGetDataTableFromSelect(query, out tempTable);
            if (tempTable.Rows.Count == 0)
            {
                return null;
            }

            result = this.database.TryGetDataTableFromSelect(query, out tempTable);
            return tempTable;
        }

        public DataTable GetImagesAllForExporting()
        {
            string query = "Select * FROM " + Constants.Database.DataTable;
            DataTable dataTable;
            bool result = this.database.TryGetDataTableFromSelect(query, out dataTable);
            return dataTable;
        }
        #endregion

        #region Public methods for counting various things in the TableData
        public int[] GetImageCounts()
        {
            int[] counts = new int[4] { 0, 0, 0, 0 };
            counts[(int)ImageQualityFilter.Dark] = this.ExecuteCountQuery(Constants.ImageQuality.Dark);
            counts[(int)ImageQualityFilter.Corrupted] = this.ExecuteCountQuery(Constants.ImageQuality.Corrupted);
            counts[(int)ImageQualityFilter.Missing] = this.ExecuteCountQuery(Constants.ImageQuality.Missing);
            counts[(int)ImageQualityFilter.Ok] = this.ExecuteCountQuery(Constants.ImageQuality.Ok);
            return counts;
        }

        public int GetNoFilterCount()
        {
            return this.ExecuteCountQuery();
        }

        public int GetDeletedImagesCounts()
        {
            bool result;
            string command_executed = String.Empty;
            try
            {
                string query = "Select Count(*) FROM " + Constants.Database.DataTable + " Where " + (string)this.DataLabelFromType[Constants.DatabaseElement.DeleteFlag] + " = \"true\"";
                return this.database.GetCountFromSelect(query, out result, out command_executed);
            }
            catch
            {
                return 0;
            }
        }

        // helper method to the above that actually executes the query
        // This first form just returns the count of all images with no filters applied
        private int ExecuteCountQuery()
        {
            bool result;
            string command_executed = String.Empty;
            try
            {
                string query = "Select Count(*) FROM " + Constants.Database.DataTable;
                return this.database.GetCountFromSelect(query, out result, out command_executed);
            }
            catch
            {
                return 0;
            }
        }

        private int ExecuteCountQuery(string to_match)
        {
            bool result;
            string command_executed = String.Empty;
            try
            {
                string query = "Select Count(*) FROM " + Constants.Database.DataTable + " Where " + (string)this.DataLabelFromType[Constants.DatabaseElement.ImageQuality] + " = \"" + to_match + "\"";
                return this.database.GetCountFromSelect(query, out result, out command_executed);
            }
            catch
            {
                return 0;
            }
        }

        public int GetCustomFilterCount(string where)
        {
            bool result;
            string command_executed = String.Empty;
            try
            {
                string query = "Select Count(*) FROM " + Constants.Database.DataTable + " Where " + where;
                return this.database.GetCountFromSelect(query, out result, out command_executed);
            }
            catch
            {
                return 0;
            }
        }
        #endregion

        #region Public methods for Inserting TableData Rows
        // Insert one or more rows into a table
        public void InsertMultipleRows(string table, List<Dictionary<String, String>> insertion_statements)
        {
            bool result;
            string command_executed = String.Empty;
            this.database.InsertMultiplesBeginEnd(table, insertion_statements, out result, out command_executed);
        }
        #endregion 

        #region Public methods for Updating TableData Rows
        /// <summary>
        /// Update a column value (identified by its key) in an existing row (identified by its ID) 
        /// By default, if the table parameter is not included, we use the TABLEDATA table
        /// </summary>
        public void UpdateRow(int id, string key, string value)
        {
            this.UpdateRow(id, key, value, Constants.Database.DataTable);
        }

        public void UpdateRow(int id, string key, string value, string table)
        {
            bool result;
            string command_executed = String.Empty;
            String where = Constants.Database.ID + " = " + id.ToString();
            Dictionary<String, Object> dataline = new Dictionary<String, Object>(); // Populate the data 
            dataline.Add(key, value);
            this.database.UpdateWhere(table, dataline, where, out result, out command_executed);

            if (table.Equals(Constants.Database.DataTable))
            {
                // Update the datatable if that is the table currenty being considered.
                // NoT sure if this is more efficient than just looping through it, but...
                DataRow[] foundRows = this.DataTable.Select(Constants.Database.ID + " = " + id);
                if (foundRows.Length > 0)
                {
                    int index = this.DataTable.Rows.IndexOf(foundRows[0]);
                    this.DataTable.Rows[index][key] = (string)value;
                }
                // Debug.Print("In UpdateRow - Data: " + key + " " + value + " " + table);
            }
            else
            {
                // Update the MarkerTable if that is the table currenty being considered.
                if (table.Equals(Constants.Database.MarkersTable))
                {
                    // Not sure if this is more efficient than just looping through it, but...
                    DataRow[] foundRows = this.MarkerTable.Select(Constants.Database.ID + " = " + id);
                    if (foundRows.Length > 0)
                    {
                        int index = this.MarkerTable.Rows.IndexOf(foundRows[0]);
                        this.MarkerTable.Rows[index][key] = (string)value;
                    }
                    // Debug.Print("In UpdateRow -Marker: " + key + " " + value + " " + table);
                }
            }
        }

        // Update all rows across the entire database with the given key/value pair
        public void RowsUpdateAll(string key, string value)
        {
            bool result;
            string query = "Update " + Constants.Database.DataTable + " SET " + key + "=" + "'" + value + "'";
            this.database.ExecuteNonQuery(query, out result);

            for (int i = 0; i < this.DataTable.Rows.Count; i++)
            {
                this.DataTable.Rows[i][key] = value;
            }
        }

        // TODO: Saul  NEW TO TEST Update all rows across the entire database with the given key/value pair
        public void RowsUpdateMultipleRecordsByID(string key, string value)
        {
            bool result;
            string query = "Update " + Constants.Database.DataTable + " SET " + key + "=" + "'" + value + "'";
            this.database.ExecuteNonQuery(query, out result);

            for (int i = 0; i < this.DataTable.Rows.Count; i++)
            {
                this.DataTable.Rows[i][key] = value;
            }
        }

        // Update all rows in the filtered view only with the given key/value pair
        public void RowsUpdateAllFilteredView(string key, string value)
        {
            bool result;
            string command_executed;
            Dictionary<Dictionary<String, Object>, String> update_query_list = new Dictionary<Dictionary<String, Object>, String>();
            Dictionary<String, Object> columnname_value_list;
            String where;
            Int64 id;

            for (int i = 0; i < this.DataTable.Rows.Count; i++)
            {
                this.DataTable.Rows[i][key] = value;
                columnname_value_list = new Dictionary<String, Object>();
                columnname_value_list.Add(key, value);
                id = (Int64)this.DataTable.Rows[i][Constants.Database.ID];
                where = Constants.Database.ID + " = " + this.DataTable.Rows[i][Constants.Database.ID].ToString();                             // on this paticular row with the given id
                update_query_list.Add(columnname_value_list, where);
            }
            this.database.UpdateWhereBeginEnd(Constants.Database.DataTable, update_query_list, out result, out command_executed);
        }
        // TODO: Change the date function to use this, as it currently updates the db one at a time.
        // This handy function efficiently updates multiple rows (each row identified by an ID) with different key/value pairs. 
        // For example, consider the tuple:
        // 5, myKey1, value1
        // 5, myKey2, value2
        // 6, myKey1, value3
        // 6, myKey2, value4
        // This will update;
        //     row with id 5 with value1 and value2 assigned to myKey1 and myKey2 
        //     row with id 6 with value3 and value3 assigned to myKey1 and myKey2 
        public void RowsUpdateByRowIdKeyVaue(List<Tuple<int, string, string>> dbupdate_list)
        {
            bool result;
            string command_executed = String.Empty;
            Dictionary<Dictionary<String, Object>, String> update_query_list = new Dictionary<Dictionary<String, Object>, String>();
            Dictionary<String, Object> columnname_value_list;
            String where;
            string id;
            string key;
            string value;

            foreach (Tuple<int, string, string> t in dbupdate_list)
            {
                id = t.Item1.ToString();
                key = t.Item2;
                value = t.Item3;
                // Debug.Print(id.ToString() + " : " + key + " : " + value);

                DataRow datarow = this.DataTable.Rows.Find(id);
                datarow[key] = value;
                // Debug.Print(datarow.ToString());

                columnname_value_list = new Dictionary<String, Object>();
                columnname_value_list.Add(key, value);
                where = Constants.Database.ID + " = " + id;
                update_query_list.Add(columnname_value_list, where);
            }
            this.database.UpdateWhereBeginEnd(Constants.Database.DataTable, update_query_list, out result, out command_executed);
            // Debug.Print(command_executed);
            // Debug.Print(result.ToString());
        }

        public void RowsUpdateFromRowToRow(string key, string value, int from, int to)
        {
            bool result;
            int from_id = from + 1; // rows start at 0, while indexes start at 1
            int to_id = to + 1;
            string query = "Update " + Constants.Database.DataTable + " SET " + key + "=" + "\"" + value + "\" ";
            query += "Where Id >= " + from_id.ToString() + " AND Id <= " + to_id.ToString();
            this.database.ExecuteNonQuery(query, out result);

            for (int i = from; i <= to; i++)
            {
                this.DataTable.Rows[i][key] = value;
            }
        }

        // Given a list of column/value pairs (the String,Object) and the FILE name indicating a row, update it
        public void RowsUpdateRowsFromFilenames(Dictionary<Dictionary<String, Object>, String> update_query_list)
        {
            bool result;
            string command_executed;
            this.database.UpdateWhereBeginEnd(Constants.Database.DataTable, update_query_list, out result, out command_executed);
        }

        public void RowsUpdateFromRowToRowFilteredView(string key, string value, int from, int to)
        {
            bool result;
            string command_executed;
            Dictionary<Dictionary<String, Object>, String> update_query_list = new Dictionary<Dictionary<String, Object>, String>();
            Dictionary<String, Object> columnname_value_list;
            String where;
            Int64 id;

            int from_id = from + 1; // rows start at 0, while indexes start at 1
            int to_id = to + 1;

            for (int i = from; i <= to; i++)
            {
                this.DataTable.Rows[i][key] = value;
                columnname_value_list = new Dictionary<String, Object>();
                columnname_value_list.Add(key, value);
                id = (Int64)this.DataTable.Rows[i][Constants.Database.ID];
                where = Constants.Database.ID + " = " + this.DataTable.Rows[i][Constants.Database.ID].ToString();                             // on this paticular row with the given id
                update_query_list.Add(columnname_value_list, where);
            }
            this.database.UpdateWhereBeginEnd(Constants.Database.DataTable, update_query_list, out result, out command_executed);
        }

        // Given a time difference in ticks, update all the date / time field in the database
        // Note that it does NOT update the dataTable - this has to be done outside of this routine by regenerating the datatables with whatever filter is being used..
        // TODO: modify this to include argments showing the current filtered view and row number, perhaps, so we could restore the datatable and the view?? 
        // TODO But that would add complications if there are unanticipated filtered views.
        // TODO: Another option is to go through whatever the current datatable is and just update those fields. 
        public void RowsUpdateAllDateTimeFieldsWithCorrectionValue(long ticks_difference, int from, int to)
        {
            // We create a temporary table. We do this just in case we are currently on a filtered view
            DataTable tempTable;
            if (this.database.TryGetDataTableFromSelect("Select * FROM " + Constants.Database.DataTable, out tempTable) == false)
            {
                return;
            }

            // We now have an unfiltered temporary data table
            // Get the original value of each, and update each date by the corrected amount if possible
            bool result;
            for (int i = from; i < to; i++)
            {
                string original_date = (string)tempTable.Rows[i][Constants.DatabaseElement.Date] + " " + (string)tempTable.Rows[i][Constants.DatabaseElement.Time];
                DateTime date;
                result = DateTime.TryParse(original_date, out date);
                if (!result)
                {
                    continue; // Since we can't get a correct date/time, just leave it unaltered.
                }

                // correct the date and modify the temporary datatable rows accordingly
                date = date.AddTicks(ticks_difference);
                tempTable.Rows[i][Constants.DatabaseElement.Date] = DateTimeHandler.StandardDateString(date);
                tempTable.Rows[i][Constants.DatabaseElement.Time] = DateTimeHandler.StandardTimeString(date);
            }

            // Now update the actual database with the new date/time values stored in the temporary table
            Dictionary<Dictionary<String, Object>, String> update_query_list = new Dictionary<Dictionary<String, Object>, String>();
            Dictionary<String, Object> columnname_value_list;
            String where;
            Int64 id;
            for (int i = from; i < to; i++)
            {
                string original_date = (string)tempTable.Rows[i][Constants.DatabaseElement.Date] + " " + (string)tempTable.Rows[i][Constants.DatabaseElement.Time];
                DateTime date;
                result = DateTime.TryParse(original_date, out date);
                if (!result)
                {
                    continue; // Since we can't get a correct date/time, don't create an update query for that row.
                }

                columnname_value_list = new Dictionary<String, Object>();                       // Update the date and time
                columnname_value_list.Add(Constants.DatabaseElement.Date, tempTable.Rows[i][Constants.DatabaseElement.Date]);
                columnname_value_list.Add(Constants.DatabaseElement.Time, tempTable.Rows[i][Constants.DatabaseElement.Time]);
                id = (Int64)tempTable.Rows[i][Constants.Database.ID];
                where = Constants.Database.ID + " = " + tempTable.Rows[i][Constants.Database.ID].ToString();                             // on this paticular row with the given id
                update_query_list.Add(columnname_value_list, where);
            }

            string command_executed;
            this.database.UpdateWhereBeginEnd(Constants.Database.DataTable, update_query_list, out result, out command_executed);
        }

        // Update all the date fields by swapping the days and months.
        // This should ONLY be called if such swapping across all dates (excepting corrupt ones) is possible
        // as otherwise it will only swap those dates it can
        // It also assumes that the data table is showing All images
        public void RowsUpdateSwapDayMonth()
        {
            bool result;
            string command_executed;
            string original_date = String.Empty;
            DateTime date;
            DateTime reversedDate;
            Dictionary<Dictionary<String, Object>, String> update_query_list = new Dictionary<Dictionary<String, Object>, String>();
            Dictionary<String, Object> columnname_value_list;
            String where;
            Int64 id;

            if (this.DataTable.Rows.Count == 0)
            {
                return;
            }

            // Get the original date value of each. If we can swap the date order, do so. 
            for (int i = 0; i < DataTable.Rows.Count; i++)
            {
                if (this.RowIsImageCorrupted(i))
                {
                    continue;  // skip over corrupted images
                }

                try
                {
                    // If we fail on any of these, continue on to the next date
                    original_date = (string)DataTable.Rows[i][Constants.DatabaseElement.Date];
                    date = DateTime.Parse(original_date);
                    reversedDate = new DateTime(date.Year, date.Day, date.Month); // we have swapped the day with the month
                }
                catch
                {
                    continue;
                }

                // Now update the actual database with the new date/time values stored in the temporary table
                columnname_value_list = new Dictionary<String, Object>();                  // Update the date 
                columnname_value_list.Add(Constants.DatabaseElement.Date, DateTimeHandler.StandardDateString(reversedDate));
                id = (Int64)this.DataTable.Rows[i][Constants.Database.ID];
                where = Constants.Database.ID + " = " + id.ToString();                             // on this paticular row with the given id
                update_query_list.Add(columnname_value_list, where);
            }
            this.database.UpdateWhereBeginEnd(Constants.Database.DataTable, update_query_list, out result, out command_executed);
        }

        // Update all the date fields between the start and end index by swapping the days and months.
        // It  assumes that the data table is showing All images
        public void RowsUpdateSwapDayMonth(int start, int end)
        {
            bool result;
            string command_executed;
            string original_date = String.Empty;
            DateTime date;
            DateTime reversedDate;
            Dictionary<Dictionary<String, Object>, String> update_query_list = new Dictionary<Dictionary<String, Object>, String>();
            Dictionary<String, Object> columnname_value_list;
            String where;
            Int64 id;

            if (this.DataTable.Rows.Count == 0 || start >= this.DataTable.Rows.Count || end >= this.DataTable.Rows.Count)
            {
                return;
            }

            // Get the original date value of each. If we can swap the date order, do so. 
            for (int i = start; i <= end; i++)
            {
                if (this.RowIsImageCorrupted(i))
                {
                    continue;  // skip over corrupted images
                }
                try
                {
                    // If we fail on any of these, continue on to the next date
                    original_date = (string)DataTable.Rows[i][Constants.DatabaseElement.Date];
                    date = DateTime.Parse(original_date);
                    reversedDate = new DateTime(date.Year, date.Day, date.Month); // we have swapped the day with the month
                }
                catch
                {
                    continue;
                }

                // Now update the actual database with the new date/time values stored in the temporary table
                columnname_value_list = new Dictionary<String, Object>();                  // Update the date 
                columnname_value_list.Add(Constants.DatabaseElement.Date, DateTimeHandler.StandardDateString(reversedDate));
                id = (Int64)this.DataTable.Rows[i][Constants.Database.ID];
                where = Constants.Database.ID + " = " + id.ToString();                             // on this paticular row with the given id
                update_query_list.Add(columnname_value_list, where);
            }
            this.database.UpdateWhereBeginEnd(Constants.Database.DataTable, update_query_list, out result, out command_executed);
        }
        #endregion

        #region Public methods for trimming white space in all table columns
        public void DataTableTrimDataWhiteSpace()
        {
            bool result;
            string command_executed;
            List<string> column_names = new List<string>();
            for (int i = 0; i < this.TemplateTable.Rows.Count; i++)
            {
                DataRow row = this.TemplateTable.Rows[i];
                column_names.Add((string)row[Constants.Control.DataLabel]);
            }
            this.database.UpdateColumnTrimWhiteSpace(Constants.Database.DataTable, column_names, out result, out command_executed);
        }
        #endregion

        #region Public Methods for Deleting Rows
        public void DeleteRow(int id)
        {
            bool result = true;
            string command_executed = String.Empty;
            this.database.DeleteFromTable(Constants.Database.DataTable, "ID = " + id.ToString(), out result, out command_executed);
        }
        #endregion

        #region Public Methods related to Navigating TableData Rows
        /// <summary> 
        /// Return the Id of the current row 
        /// </summary>
        public int GetIdOfCurrentRow()
        {
            try
            {
                if (this.CurrentRow == -1)
                {
                    return -1;
                }
                return Convert.ToInt32(this.DataTable.Rows[this.CurrentRow][Constants.Database.ID]); // The Id of the current row
            }
            catch
            {
                // If for some reason the above fails, we want to at least return something.
                return -1;
            }
        }

        /// <summary> 
        /// Go to the first row, returning true if we can otherwise false
        /// </summary>
        public bool ToDataRowFirst()
        {
            // Check if there are no rows. If none, set Id and Row indicators to reflect that
            if (this.DataTable.Rows.Count <= 0)
            {
                this.CurrentId = -1;
                this.CurrentRow = -1;
                return false;
            }

            // We have some rows. The first row is always 0, and then get the Id in that first row
            this.CurrentRow = 0; // The first row
            this.CurrentId = this.GetIdOfCurrentRow();
            return true;
        }

        /// <summary> 
        /// Go to the next row, returning false if we can;t (e.g., if we are at the end) 
        /// </summary>
        public bool ToDataRowNext()
        {
            int count = this.DataTable.Rows.Count;

            // Check if we are on the last row. If so, do nothing and return false.
            if (this.CurrentRow >= (count - 1))
            {
                return false;
            }

            // Go to the next row
            this.CurrentRow += 1;
            this.CurrentId = this.GetIdOfCurrentRow();
            return true;
        }

        /// <summary>
        /// Go to the previous row, returning true if we can otherwise false (e.g., if we are at the beginning)
        /// </summary>
        public bool ToDataRowPrevious()
        {
            // Check if we are on the first row. If so, do nothing and return false.
            if (this.CurrentRow == 0)
            {
                return false;
            }

            // Go to the previous row
            this.CurrentRow -= 1;
            this.CurrentId = this.GetIdOfCurrentRow();
            return true;
        }

        /// <summary>
        /// Go to a particular row, returning true if we can otherwise false (e.g., if the index is out of range)
        /// Remember, that we are zero based, so (for example) and index of 5 will go to the sixth row
        /// </summary>
        public bool ToDataRowIndex(int row_index)
        {
            int count = this.DataTable.Rows.Count;

            // Check if that particular row exists. If so, do nothing and return false.

            if (this.RowInBounds(row_index))
            {
                // Go to the previous row
                this.CurrentRow = row_index;
                this.CurrentId = this.GetIdOfCurrentRow();
                return true;
            }
            else
            {
                return false;
            }
        }
        #endregion

        /// <summary>Given a key and a data label, return its string value.</summary>
        private string IDGetValueFromDataLabel(int key, string data_label, out bool result)
        {
            result = false;
            string found_string = String.Empty;
            DataRow foundRow = this.DataTable.Rows.Find(key);
            if (null != foundRow)
            {
                try
                {
                    found_string = (string)foundRow[data_label];
                    result = true;
                }
                catch
                {
                }
            }
            return found_string;
        }

        // Convenience functions for the standard data types, with the ID supplied
        public string IDGetFile(int key, out bool result)
        {
            return (string)this.IDGetValueFromDataLabel(key, Constants.DatabaseElement.File, out result);
        }

        public string IDGetFolder(int key, out bool result)
        {
            return (string)this.IDGetValueFromDataLabel(key, Constants.DatabaseElement.Folder, out result);
        }
        public string IDGetDate(int key, out bool result)
        {
            return (string)this.IDGetValueFromDataLabel(key, Constants.DatabaseElement.Date, out result);
        }

        public string IDGetTime(int key, out bool result)
        {
            return (string)this.IDGetValueFromDataLabel(key, Constants.DatabaseElement.Time, out result);
        }

        public string IDGetImageQuality(int key, out bool result)
        {
            return (string)this.IDGetValueFromDataLabel(key, Constants.DatabaseElement.ImageQuality, out result);
        }

        // Convenience functions for the standard data types, where it assumes the Id is the currentID
        public string IDGetFile(out bool result)
        {
            return (string)this.IDGetValueFromDataLabel(this.CurrentId, Constants.DatabaseElement.File, out result);
        }

        public string IDGetFolder(out bool result)
        {
            return (string)this.IDGetValueFromDataLabel(this.CurrentId, Constants.DatabaseElement.Folder, out result);
        }

        public string IDGetDate(out bool result)
        {
            return (string)this.IDGetValueFromDataLabel(this.CurrentId, Constants.DatabaseElement.Date, out result);
        }

        public string IDGetTime(out bool result)
        {
            return (string)this.IDGetValueFromDataLabel(this.CurrentId, Constants.DatabaseElement.Time, out result);
        }

        public string IDGetImageQuality(out bool result)
        {
            return (string)this.IDGetValueFromDataLabel(this.CurrentId, Constants.DatabaseElement.ImageQuality, out result);
        }

        #region Public Methods to get values from a TableData given row
        public string RowGetValueFromType(string type)
        {
            return this.RowGetValueFromType(type, this.CurrentRow);
        }

        public string RowGetValueFromType(string type, int row_index)
        {
            if (this.RowInBounds(row_index))
            {
                string key = (string)this.DataLabelFromType[type];
                string result;
                try
                {
                    result = (string)this.DataTable.Rows[row_index][key];
                }
                catch
                {
                    result = String.Empty;
                }
                return result;
            }
            else
            {
                return String.Empty;
            }
        }

        // Given a row index, return the ID
        public int RowGetID(int row_index)
        {
            if (!this.RowInBounds(row_index))
            {
                return -1;
            }
            try
            {
                Int64 id = (Int64)this.DataTable.Rows[row_index][Constants.Database.ID];
                return Convert.ToInt32(id);
            }
            catch
            {
                return -1;
            }
        }

        public string RowGetValueFromDataLabel(string data_label)
        {
            return this.RowGetValueFromDataLabel(data_label, this.CurrentRow);
        }

        public string RowGetValueFromDataLabel(string data_label, int row_index)
        {
            if (this.RowInBounds(row_index))
            {
                return (string)this.DataTable.Rows[row_index][data_label];
            }
            else
            {
                return String.Empty;
            }
        }

        /// <summary>A convenience routine for checking to see if the image in the current row is displayable (i.e., not corrupted or missing)</summary>
        public bool RowIsImageDisplayable()
        {
            return this.RowIsImageDisplayable(this.CurrentRow);
        }

        /// <summary> A convenience routine for checking to see if the image in the given row is displayable (i.e., not corrupted or missing)</summary>
        public bool RowIsImageDisplayable(int row)
        {
            string result = this.RowGetValueFromDataLabel((string)this.DataLabelFromType[Constants.DatabaseElement.ImageQuality], row);
            if (result.Equals(Constants.ImageQuality.Corrupted) || result.Equals(Constants.ImageQuality.Missing))
            {
                return false;
            }
            return true;
        }

        /// <summary> A convenience routine for checking to see if the image in the given row is corrupted</summary>
        public bool RowIsImageCorrupted(int row)
        {
            string result = this.RowGetValueFromDataLabel((string)this.DataLabelFromType[Constants.DatabaseElement.ImageQuality], row);
            return result.Equals(Constants.ImageQuality.Corrupted) ? true : false;
        }

        // Find the next displayable image after the provided row in the current image set
        public int RowFindNextDisplayableImage(int initial_row)
        {
            for (int row = initial_row; row < this.DataTable.Rows.Count; row++)
            {
                if (this.RowIsImageDisplayable(row))
                {
                    return row;
                }
            }
            return -1;
        }
        #endregion

        #region Public Methods to get values from a TemplateTable 

        public DataTable TemplateGetSortedByControls()
        {
            DataTable tempdt = this.TemplateTable.Copy();
            DataView dv = tempdt.DefaultView;
            dv.Sort = Constants.Database.ControlOrder + " ASC";
            return dv.ToTable();
        }

        public bool TemplateIsCopyable(string data_label)
        {
            int id = this.GetID(data_label);
            DataRow foundRow = this.TemplateTable.Rows.Find(id);
            bool is_copyable;
            return bool.TryParse((string)foundRow[Constants.Control.Copyable], out is_copyable) ? is_copyable : false;
        }

        public string TemplateGetDefault(string data_label)
        {
            int id = this.GetID(data_label);
            DataRow foundRow = this.TemplateTable.Rows.Find(id);
            return (string)foundRow[Constants.Control.DefaultValue];
        }
        #endregion

        #region Public methods to set values in the TableData row
        public void RowSetValueFromDataLabel(string key, string value)
        {
            this.RowSetValueFromKey(key, value, this.CurrentRow);
        }

        private void RowSetValueFromKey(string key, string value, int row_index)
        {
            if (this.RowInBounds(row_index))
            {
                this.DataTable.Rows[this.CurrentRow][key] = value;
                this.UpdateRow(this.CurrentId, key, value);
            }
        }

        public void RowSetValueFromID(string key, string value, int id)
        {
            if (id == Convert.ToInt32(this.DataTable.Rows[this.CurrentRow][Constants.Database.ID].ToString()))
            {
                this.DataTable.Rows[this.CurrentRow][key] = value;
            }
            this.UpdateRow(id, key, value);
        }
        #endregion

        #region Public methods to get / set values in the ImageSetData
        /// <summary>Given a key, return its value</summary>
        private string ImageSetGetValue(string key)
        {
            // Get the single row
            string query = "Select * From " + Constants.Database.ImageSetTable + " WHERE " + Constants.Database.ID + " = 1";
            DataTable imagesetTable;
            if (this.database.TryGetDataTableFromSelect(query, out imagesetTable) == false)
            {
                return String.Empty;
            }
            return (string)imagesetTable.Rows[0][key];
        }

        // Check if the White Space column exists in the ImageSetTable
        public bool DoesWhiteSpaceColumnExist()
        {
            return this.database.IsColumnInTable(Constants.Database.ImageSetTable, Constants.DatabaseElement.WhiteSpaceTrimmed);
        }

        // Create the White Space column exists in the ImageSetTable
        public bool CreateWhiteSpaceColumn()
        {
            if (this.database.CreateColumnInTable(Constants.Database.ImageSetTable, Constants.DatabaseElement.WhiteSpaceTrimmed))
            {
                // Set the value to true 

                return true;
            }
            return false;
        }

        // <summary>Given a key, value pair, update its value</summary>
        private void ImageSetGetValue(string key, string value)
        {
            this.UpdateRow(1, Constants.DatabaseElement.Log, value, Constants.Database.ImageSetTable);
        }
        #endregion

        #region Public Methods to manipulate the MarkersTable

        /// <summary>
        /// Get the metatag counter list associated with all counters representing the current row
        /// It will have a MetaTagCounter for each control, even if there may be no metatags in it
        /// </summary>
        /// <returns>list of counters</returns>
        public List<MetaTagCounter> MarkerTableGetMetaTagCounterList()
        {
            List<MetaTagCounter> metaTagCounters = new List<MetaTagCounter>();

            // Test to see if we actually have a valid result
            if (this.MarkerTable.Rows.Count == 0)
            {
                return metaTagCounters;    // This should not really happen, but just in case
            }
            if (this.MarkerTable.Columns.Count == 0)
            {
                return metaTagCounters; // Should also not happen as this wouldn't be called unless we have at least one counter control
            }

            int id = this.GetIdOfCurrentRow();

            // Get the current row number of the id in the marker table
            int row_num = this.MarkerTableFindRowNumber(id);
            if (row_num < 0)
            {
                return metaTagCounters;
            }

            // Iterate through the columns, where we create a new MetaTagCounter for each control and add it to the MetaTagCounte rList
            MetaTagCounter mtagCounter;
            string datalabel = String.Empty;
            string value = String.Empty;
            List<Point> points;
            for (int i = 0; i < this.MarkerTable.Columns.Count; i++)
            {
                datalabel = this.MarkerTable.Columns[i].ColumnName;
                if (datalabel.Equals(Constants.Database.ID))
                {
                    continue;  // Skip the ID
                }

                // Create a new MetaTagCounter representing this control's meta tag,
                mtagCounter = new MetaTagCounter();
                mtagCounter.DataLabel = datalabel;

                // Now create a new Metatag for each point and add it to the counter
                try
                {
                    value = (string)this.MarkerTable.Rows[row_num][datalabel];
                }
                catch
                {
                    value = String.Empty;
                }

                points = this.ParseCoords(value); // parse the contents into a set of points
                foreach (Point point in points)
                {
                    mtagCounter.CreateMetaTag(point, datalabel);  // add the metatage to the list
                }
                metaTagCounters.Add(mtagCounter);   // and add that metaTag counter to our lists of metaTag counters
            }
            return metaTagCounters;
        }

        /// <summary>
        /// Given a point, add it to the Counter (identified by its data label) within the current row in the Marker table. 
        /// </summary>
        /// <param name="dataLabel">data label</param>
        /// <param name="pointlist">A list of points in the form x,y|x,y|x,y</param>
        public void MarkerTableAddPoint(string dataLabel, string pointlist)
        {
            // Find the current row number
            int id = this.GetIdOfCurrentRow();
            int row_num = this.MarkerTableFindRowNumber(id);
            if (row_num < 0)
            {
                return;
            }

            // Update the database and datatable
            this.MarkerTable.Rows[row_num][dataLabel] = pointlist;
            this.UpdateRow(id, dataLabel, pointlist, Constants.Database.MarkersTable);  // Update the database
        }

        // Given a list of column/value pairs (the String,Object) and the FILE name indicating a row, update it
        public void RowsUpdateMarkerRows(Dictionary<Dictionary<String, Object>, String> update_query_list)
        {
            bool result;
            string command_executed;
            this.database.UpdateWhereBeginEnd(Constants.Database.MarkersTable, update_query_list, out result, out command_executed);
        }
        public void RowsUpdateMarkerRows(List<ColumnTupleListWhere> update_query_list)
        {
            bool result;
            string command_executed;
            this.database.UpdateWhereBeginEnd(Constants.Database.MarkersTable, update_query_list, out result, out command_executed);
        }

        // The id is the row to update, the datalabels are the labels of each control to updata, 
        // and the markers are the respective point lists for each of those labels
        public void UpdateMarkersInRows(List<ColumnTupleListWhere> all_markers)
        {
            string sid;
            int id;
            char[] quote = { '\'' };
            foreach (ColumnTupleListWhere ctlw in all_markers)
            {
                List<ColumnTuple> ctl = ctlw.ListPair;
                // We have to parse the id, as its in the form of Id=5 (for example)
                sid = ctlw.Where.Substring(ctlw.Where.IndexOf("=") + 1);
                sid = sid.Trim(quote);

                if (!Int32.TryParse(sid, out id))
                {
                    Debug.Print("Can't GetThe Id");
                    break;
                }
                foreach (ColumnTuple ct in ctl)
                {
                    if (!ct.ColumnValue.Equals(String.Empty))
                    {
                        this.MarkerTable.Rows[id - 1][ct.ColumnName] = ct.ColumnValue;
                    }
                }
            }
        }

        /// <summary>
        /// Given an id, find the row number that matches it in the Marker Table
        /// </summary>
        /// <returns>-1 on failure</returns>
        private int MarkerTableFindRowNumber(int id)
        {
            for (int row_number = 0; row_number < this.MarkerTable.Rows.Count; row_number++)
            {
                string str = this.MarkerTable.Rows[row_number][Constants.Database.ID].ToString();
                int this_id;
                if (Int32.TryParse(str, out this_id) == false)
                {
                    return -1;
                }

                if (this_id == id)
                {
                    return row_number;
                }
            }
            return -1;
        }

        private List<Point> ParseCoords(string value)
        {
            List<Point> points = new List<Point>();
            if (value.Equals(String.Empty))
            {
                return points;
            }

            char[] delimiterBar = { Constants.Database.MarkerBar };
            string[] pointsAsStrings = value.Split(delimiterBar);

            foreach (string s in pointsAsStrings)
            {
                Point point = Point.Parse(s);
                points.Add(point);
            }
            return points;
        }
        #endregion

        #region Private Methods
        private bool RowInBounds(int row_index)
        {
            return (row_index >= 0 && row_index < this.DataTable.Rows.Count) ? true : false;
        }

        /// <summary>Gets the complete path to the image database.</summary>
        private string GetFilePath()
        {
            return Path.Combine(this.FolderPath, this.FileName);
        }

        private DataRow GetTemplateRow(string data_label)
        {
            int id = this.GetID(data_label);
            return this.TemplateTable.Rows.Find(id);
        }

        /// <summary>Given a data label, get the id of the key's row</summary>
        private int GetID(string data_label)
        {
            for (int i = 0; i < this.TemplateTable.Rows.Count; i++)
            {
                if (data_label.Equals(this.TemplateTable.Rows[i][Constants.Control.DataLabel]))
                {
                    Int64 id = (Int64)this.TemplateTable.Rows[i][Constants.Database.ID];
                    return Convert.ToInt32(id);
                }
            }
            return -1;
        }
        #endregion
    }
}
