using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;


namespace Timelapse
{
    public class DBData
    {
        #region Private Variables and Constants
        private SQLiteWrapper DB;      // A pointer to the Timelapse Data database
        #endregion 

        #region Public Properties

        public DataTable dataTable = new DataTable();        // contains the results of the data query
        public DataTable markerTable = new DataTable();      // contains the markers

        /// <summary>The complete path (excluding file name) of the data db. </summary>
        public string FolderPath {get; set;}

        /// <summary>The folder name (not the path) containing the data db </summary>
        public string Folder { get { return Utilities.GetFolderNameFromFolderPath(this.FolderPath); } }   
   
        /// <summary>The file name of the data db lives</summary>
        public string Filename { get { return Constants.DBIMAGEDATAFILENAME; } }

        /// <summary>The path + file name of the data db </summary>
        public string FilePath { get { return System.IO.Path.Combine(this.FolderPath, this.Filename); }} 

        /// <summary>A pointer to the data table </summary>
        public DataTable templateTable  {get; set;}

        /// <summary>A pointer to the table that has information about the entire image set</summary>
        public DataTable ImageSetTable { get; set; }

        /// <summary>The number of rows in the datatable, each row corresponding to an image </summary>
        public int ImageCount { get { return this.dataTable.Rows.Count; } }

        /// <summary>Returns true of the data database exists</summary>
        public bool Exists { get { return File.Exists(this.FilePath); } }    

        public int CurrentId = -1;                          // the Id of the current record. -1 if its not pointing to anything or if the database is empty
        public int CurrentRow = -1;

        // Access methods for the ImageDataTable key/values
        public string Log
        {
            set { this.UpdateRow(1, Constants.LOG, value, Constants.TABLEIMAGESET); }
            get { return ImageSetGetValue(Constants.LOG); }
        }

        public bool State_Magnifyer
        {
            set { this.UpdateRow(1, Constants.STATE_MAGNIFIER, value.ToString(), Constants.TABLEIMAGESET); }
            get { string result = ImageSetGetValue(Constants.STATE_MAGNIFIER); return Convert.ToBoolean(result); }
        }

        public int State_Row
        {
            set { this.UpdateRow(1, Constants.STATE_ROW, value.ToString(), Constants.TABLEIMAGESET); }
            get { string result = ImageSetGetValue(Constants.STATE_ROW); return Convert.ToInt32(result); }
        }

        public int State_Filter
        {
            set { int ifilter = (int)value; this.UpdateRow(1, Constants.STATE_FILTER, ifilter.ToString(), Constants.TABLEIMAGESET); }
            get { string result = ImageSetGetValue(Constants.STATE_FILTER); return Convert.ToInt32(result); }
        }
        

        public Hashtable DataLabelFromType = new Hashtable();
        public Hashtable TypeFromKey = new Hashtable();

        #endregion 

        #region Constructors, Destructors
        
        /// <summary>Constructor </summary>
        public DBData() { }

        #endregion

        #region Public methods for creating the database and the lookup tables
        /// <summary>
        /// Create a database file (if needed) and connect to it. Also keeps a local copy of the template
        /// </summary>
        /// <returns></returns>
        public bool CreateDB(Template template)
        {
             // Create the DB
            try
            {
                this.DB = new SQLiteWrapper(this.FilePath);
                this.templateTable = template.templateTable;
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
            string command_executed = "";
            Dictionary<string, string> column_definition = new Dictionary<string, string>();

            column_definition.Add(Constants.ID, Constants.TABLE_CREATIONSTRING_WITHPRIMARYKEY);  // It begins with the ID integer primary key
            foreach (DataRow row in this.templateTable.Rows)
            {
                id = (Int64)row[Constants.ID];
                db_label = (string)row[Constants.DATALABEL];
                default_value = (string)row[Constants.DEFAULT];
               
                column_definition.Add(db_label, " Text '" + default_value + "'");
            }
            this.DB.CreateTable(Constants.TABLEDATA, column_definition, out result, out command_executed);
            // Debug.Print (result.ToString() + " " + command_executed);
            string command = "Select * FROM " + Constants.TABLEDATA;
            this.dataTable = this.DB.GetDataTableFromSelect(command, out result, out command_executed);
            

            //2. Create the ImageSetTable and initialize a single row in it
            column_definition.Clear ();
            column_definition.Add (Constants.ID, Constants.TABLE_CREATIONSTRING_WITHPRIMARYKEY);  // It begins with the ID integer primary key
            column_definition.Add (Constants.LOG, " TEXT DEFAULT 'Add text here.'");

            column_definition.Add (Constants.STATE_MAGNIFIER, " TEXT DEFAULT 'true'");
            column_definition.Add (Constants.STATE_ROW, " TEXT DEFAULT '0'");
            int ifilter = (int)Constants.ImageQualityFilters.All;
            column_definition.Add (Constants.STATE_FILTER, " TEXT DEFAULT '" + ifilter.ToString() + "'");
            this.DB.CreateTable(Constants.TABLEIMAGESET, column_definition, out result, out command_executed);

            Dictionary<String, String> dataline = new Dictionary<String, String>(); // Populate the data for the image set with defaults
            dataline.Add(Constants.LOG, "Add text here");
            dataline.Add(Constants.STATE_MAGNIFIER, "true");
            dataline.Add(Constants.STATE_ROW, "0");
            dataline.Add(Constants.STATE_FILTER, ifilter.ToString());
            List <Dictionary<string,string>> insertion_statements = new List <Dictionary<string,string>> ();
            insertion_statements.Add (dataline);
            this.DB.InsertMultiplesBeginEnd (Constants.TABLEIMAGESET, insertion_statements, out result, out command_executed);

            // 3.Create the MarkersTable and initialize a single row in it
            column_definition.Clear ();
            column_definition.Add (Constants.ID, Constants.TABLE_CREATIONSTRING_WITHPRIMARYKEY);  // t begins with the ID integer primary key
            string type = "";
            foreach (DataRow row in this.templateTable.Rows)
            {
                type = (string)row[Constants.TYPE];
                if (type.Equals (Constants.COUNTER))
                {
                    id = (Int64)row[Constants.ID];
                    db_label = (string)row[Constants.DATALABEL];
                    string key = db_label;
                    column_definition.Add (db_label, " Text Default ''");
                }
            }
            this.DB.CreateTable(Constants.TABLEMARKERS, column_definition, out result, out command_executed);

            // 4. Copy the TemplateTable to this Database
            // First we have to create the table
            Dictionary<string, string> column_definitions = new Dictionary<string, string>(); 
            column_definitions.Add(Constants.ID, "INTEGER primary key");
            column_definitions.Add(Constants.CONTROLORDER, "INTEGER");
            column_definitions.Add(Constants.SPREADSHEETORDER, "INTEGER");
            column_definitions.Add(Constants.TYPE, "text");
            column_definitions.Add(Constants.DEFAULT, "text");
            column_definitions.Add(Constants.LABEL, "text");
            column_definitions.Add(Constants.DATALABEL, "text");
            column_definitions.Add(Constants.TOOLTIP, "text");
            column_definitions.Add(Constants.TXTBOXWIDTH, "text");
            column_definitions.Add(Constants.COPYABLE, "text");
            column_definitions.Add(Constants.VISIBLE, "text");
            column_definitions.Add(Constants.LIST, "text");
            this.DB.CreateTable (Constants.TABLETEMPLATE, column_definitions, out result, out command_executed);

            DataView tempView = this.templateTable.DefaultView;
            tempView.Sort = "ID ASC";
            DataTable tempTable = tempView.ToTable();
            if (tempTable.Rows.Count != 0)
                this.DB.InsertDataTableIntoTable(Constants.TABLETEMPLATE, tempTable, out result, out command_executed);
        }
       

        public void InitializeMarkerTableFromDataTable ()
        {
            bool result;
            string command_executed = "";
            string command = "Select * FROM " + Constants.TABLEMARKERS;
            this.markerTable = this.DB.GetDataTableFromSelect(command, out result, out command_executed);
        }
        /// <summary>
        /// Create lookup tables that allow us to retrive a key from a type and vice versa
        /// TODO Should probably change this so its done internally rather than called externally
        /// </summary>
        public void CreateLookupTables ()
        {
            Int64 id;
            string data_label;
            string rowtype;
            foreach (DataRow row in this.templateTable.Rows)
            {
                id = (Int64)row[Constants.ID];
                data_label = (string) row[Constants.DATALABEL];
                rowtype = (string)row[Constants.TYPE];

                // We don't want to add these types to the hash, as there can be multiple ones, which means the key would not be unique
                if (  ! (rowtype.Equals(Constants.NOTE) || rowtype.Equals(Constants.FIXEDCHOICE) || rowtype.Equals (Constants.COUNTER) || rowtype.Equals (Constants.FLAG)))
                    this.DataLabelFromType.Add(rowtype, data_label);  
                this.TypeFromKey.Add(data_label, rowtype);
            }
        }

        #endregion

        #region public methods for returning a copy of a table as is currently in the database
        public DataTable CreateDataTableFromDatabaseTable (string tablename)
        {
            bool result;
            string command_executed = "";
            string command = "Select * FROM " + tablename;
            return this.DB.GetDataTableFromSelect(command, out result, out command_executed);
        }
        #endregion

        #region Public methods for populating the Tabledata database with some basic filters
        /// <summary> 
        /// Populate the data datatable so that it matches all the entries in its associated database table.
        /// Then set the currentID and currentRow to the the first record in the returned set
        /// </summary>

        // Filter: All images
        public bool GetImagesAll()
        {
            return this.GetImages ("*", "");
        }

        // Filter: Corrupted images only
        public bool GetImagesCorrupted()
        {
            string where = (string)this.DataLabelFromType[Constants.IMAGEQUALITY]; // key
            where += "=\"" + Constants.IMAGEQUALITY_CORRUPTED + "\"";  // = value

            return this.GetImages("*", where);
        }

        // Filter: Dark images only
        public bool GetImagesDark()
        {
            string where = (string)this.DataLabelFromType[Constants.IMAGEQUALITY]; // key
            where += "=\"" + Constants.IMAGEQUALITY_DARK + "\"";  // = value

            return this.GetImages("*", where);
        }

        // Filter: Missing images only
        public bool GetImagesMissing()
        {
            string where = (string)this.DataLabelFromType[Constants.IMAGEQUALITY]; // key
            where += "=\"" + Constants.IMAGEQUALITY_MISSING + "\"";  // = value

            return this.GetImages("*", where);
        }

        // Filter:  images marked for deltion
        public bool GetImagesMarkedForDeletion()
        {
            string where = (string)this.DataLabelFromType[Constants.DELETEFLAG]; // key
            where += "=\"true\""; // = value
            return this.GetImages("*", where);
        }

        public DataTable GetDataTableOfImagesMarkedForDeletion()
        {
            string where = (string)this.DataLabelFromType[Constants.DELETEFLAG]; // key
            where += "=\"true\""; // = value
            return this.GetDataTableOfImages("*", where);
        }

        public bool GetImagesAllButDarkAndCorrupted()
        {
            string where = (string)this.DataLabelFromType[Constants.IMAGEQUALITY]; // key
            where += " IS NOT \"" + Constants.IMAGEQUALITY_DARK + "\"";  // = value
            where += " AND ";
            where += (string)this.DataLabelFromType[Constants.IMAGEQUALITY];
            where += " IS NOT \"" + Constants.IMAGEQUALITY_CORRUPTED + "\"";  // = value

            return this.GetImages("*", where);
        }

        private bool GetImages (string searchstring, string where)
        {
            bool result;
            string command_executed;

            string query = "Select " + searchstring;
            query += " FROM " + Constants.TABLEDATA;
            if (!where.Equals("")) query += " WHERE " + where;

            DataTable tempTable = this.DB.GetDataTableFromSelect(query, out result, out command_executed);
            if (tempTable.Rows.Count == 0) return false ;
            this.dataTable = this.DB.GetDataTableFromSelect(query, out result, out command_executed);
            return true;
        }

        private DataTable GetDataTableOfImages(string searchstring, string where)
        {
            bool result;
            string command_executed;

            string query = "Select " + searchstring;
            query += " FROM " + Constants.TABLEDATA;
            if (!where.Equals("")) query += " WHERE " + where;

            DataTable tempTable = this.DB.GetDataTableFromSelect(query, out result, out command_executed);
            if (tempTable.Rows.Count == 0) return null;
            tempTable = this.DB.GetDataTableFromSelect(query, out result, out command_executed);
            return tempTable;
        }

        public DataTable GetImagesAllForExporting()
        {
            bool result;
            string command_executed;

            string query = "Select * FROM " + Constants.TABLEDATA;
            return this.DB.GetDataTableFromSelect(query, out result, out command_executed);
        }
        #endregion

        #region Public methods for counting various things in the TableData
        public int[] GetImageCounts()
        {
            int [] counts = new int[4]{0,0,0,0};
            counts[(int)Constants.ImageQualityFilters.Dark] = doCountQuery(Constants.IMAGEQUALITY_DARK);
            counts[(int)Constants.ImageQualityFilters.Corrupted] = doCountQuery(Constants.IMAGEQUALITY_CORRUPTED);
            counts[(int)Constants.ImageQualityFilters.Missing] = doCountQuery(Constants.IMAGEQUALITY_MISSING);
            counts[(int)Constants.ImageQualityFilters.Ok] = doCountQuery(Constants.IMAGEQUALITY_OK);
            return counts;
        }

        public int GetDeletedImagesCounts()
        {
            bool result;
            string command_executed = "";
            try
            {
                string query = "Select Count(*) FROM " + Constants.TABLEDATA + " Where " + (string)this.DataLabelFromType[Constants.DELETEFLAG] + " = \"true\"";
                return this.DB.GetCountFromSelect(query, out result, out command_executed);
            }
            catch
            {
                return 0;
            }
        }

        // helper method to the above that actually executes the query
        private int doCountQuery (string to_match)
        {
            bool result;
            string command_executed = "";
            try
            {
                string query = "Select Count(*) FROM " + Constants.TABLEDATA + " Where " + (string)this.DataLabelFromType[Constants.IMAGEQUALITY] + " = \"" + to_match + "\"";
                return this.DB.GetCountFromSelect(query, out result, out command_executed);
            }
            catch
            {
                return 0;
            }
        }
        #endregion

        #region Public methods for Inserting TableData Rows
        // Insert one or more rows into a table
        public void InsertMultipleRows (string table, List <Dictionary<String, String>> insertion_statements)
        {
            bool result;
            string command_executed = "";
            this.DB.InsertMultiplesBeginEnd (table, insertion_statements, out result, out command_executed);
        }
        #endregion 

        #region Public methods for Updating TableData Rows
        /// <summary>
        /// Update a column value (identified by its key) in an existing row (identified by its ID) 
        /// By default, if the table parameter is not included, we use the TABLEDATA table
        /// </summary>
        /// <param name="id"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void UpdateRow(int id, string key, string value)
        {
            UpdateRow(id, key, value, Constants.TABLEDATA);
        }
        public void UpdateRow(int id, string key, string value, string table)
        {
            bool result;
            string command_executed = "";
            String where = Constants.ID + " = " + id.ToString ();
            Dictionary<String, Object> dataline = new Dictionary<String, Object>(); // Populate the data 
            dataline.Add (key, value);
            this.DB.UpdateWhere (table, dataline, where, out result, out command_executed);

            //Updata the datatable if that is the table currenty being considered.
            if (table.Equals(Constants.TABLEDATA))
            {
                //  NoT sure if this is more efficient than just looping through it, but...
                DataRow[] foundRows = this.dataTable.Select(Constants.ID + " = " + id);
                if (foundRows.Length > 0)
                {
                    int index = this.dataTable.Rows.IndexOf(foundRows[0]);
                    this.dataTable.Rows[index][key] = (string)value;
                }
                //Debug.Print("In UpdateRow - Data: " + key + " " + value + " " + table);
            }
            else            //Update the MarkerTable if that is the table currenty being considered.
            {
                if (table.Equals(Constants.TABLEMARKERS))
                {
                    //  Not sure if this is more efficient than just looping through it, but...
                    DataRow[] foundRows = this.markerTable.Select(Constants.ID + " = " + id);
                    if (foundRows.Length > 0)
                    {
                        int index = this.markerTable.Rows.IndexOf(foundRows[0]);
                        this.markerTable.Rows[index][key] = (string)value;
                    }
                    //Debug.Print("In UpdateRow -Marker: " + key + " " + value + " " + table);
                }
            }
        }

        // Update all rows across the entire database with the given key/value pair
        public void RowsUpdateAll(string key, string value)
        {
            bool result;
            string query = "Update " + Constants.TABLEDATA + " SET " + key + "=" + "'" + value + "'";
            this.DB.ExecuteNonQuery(query, out result);

            for (int i = 0; i < this.dataTable.Rows.Count; i++)
            {
                this.dataTable.Rows[i][key] = value;
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

            for (int i = 0; i < this.dataTable.Rows.Count; i++)
            {
                this.dataTable.Rows[i][key] = value;
                columnname_value_list = new Dictionary<String, Object>();
                columnname_value_list.Add(key, value);
                id = (Int64) this.dataTable.Rows[i][Constants.ID];
                where = Constants.ID + " = " + this.dataTable.Rows[i][Constants.ID].ToString();                             // on this paticular row with the given id
                update_query_list.Add(columnname_value_list, where);
            }
            this.DB.UpdateWhereBeginEnd(Constants.TABLEDATA, update_query_list, out result, out command_executed);
        }

 
        public void RowsUpdateFromRowToRow(string key, string value, int from, int to)
        {
            bool result;
            int from_id = from + 1; // rows start at 0, while indexes start at 1
            int to_id = to + 1;
            string query = "Update " + Constants.TABLEDATA + " SET " + key + "=" + "\"" + value + "\" ";
            query += "Where Id >= " + from_id.ToString() + " AND Id <= " + to_id.ToString ();
            this.DB.ExecuteNonQuery(query, out result);

            for (int i = from; i <= to; i++)
            {
                this.dataTable.Rows[i][key] = value;
            }
        }

        // Given a list of column/value pairs (the String,Object) and the FILE name indicating a row, update it
        public void RowsUpdateRowsFromFilenames (Dictionary<Dictionary<String, Object>, String> update_query_list)
        {
            bool result;
            string command_executed;
            this.DB.UpdateWhereBeginEnd(Constants.TABLEDATA, update_query_list, out result, out command_executed);
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
                this.dataTable.Rows[i][key] = value;
                columnname_value_list = new Dictionary<String, Object>();
                columnname_value_list.Add(key, value);
                id = (Int64)this.dataTable.Rows[i][Constants.ID];
                where = Constants.ID + " = " + this.dataTable.Rows[i][Constants.ID].ToString();                             // on this paticular row with the given id
                update_query_list.Add(columnname_value_list, where);
            }
            this.DB.UpdateWhereBeginEnd(Constants.TABLEDATA, update_query_list, out result, out command_executed);
        }

        // Given a time difference in ticks, update all the date / time field in the database
        // Note that it does NOT update the dataTable - this has to be done outside of this routine by regenerating the datatables with whatever filter is being used..
        // TODO: modify this to include argments showing the current filtered view and row number, perhaps, so we could restore the datatable and the view?? 
        // TODO But that would add complications if there are unanticipated filtered views.
        // TODO: Another option is to go through whatever the current datatable is and just update those fields. 
        public void RowsUpdateAllDateTimeFieldsWithCorrectionValue (long ticks_difference, int from, int to)
        {
            bool result;
            string command_executed;
            string original_date = "";
            DateTime dtTemp;

            // We create a temporary table. We do this just in case we are currently on a filtered view
            DataTable tempTable = this.DB.GetDataTableFromSelect("Select * FROM " + Constants.TABLEDATA, out result, out command_executed);
            if (tempTable.Rows.Count == 0) return;

            // We now have an unfiltered temporary data table
            // Get the original value of each, and update each date by the corrected amount if possible
            for (int i = from; i < to; i++)
            {
                original_date = (string) tempTable.Rows[i][Constants.DATE] + " " + (string) tempTable.Rows[i][Constants.TIME];
                result = DateTime.TryParse(original_date, out dtTemp);
                if (!result) continue; // Since we can't get a correct date/time, just leave it unaltered.
                
                // correct the date and modify the temporary datatable rows accordingly
                dtTemp = dtTemp.AddTicks(ticks_difference);
                tempTable.Rows[i][Constants.DATE] = DateTimeHandler.StandardDateString(dtTemp);
                tempTable.Rows[i][Constants.TIME] = DateTimeHandler.StandardTimeString(dtTemp);
            }

            // Now update the actual database with the new date/time values stored in the temporary table
            Dictionary<Dictionary<String, Object>, String> update_query_list = new Dictionary<Dictionary<String, Object>, String> ();
            Dictionary<String, Object> columnname_value_list;
            String where;
            Int64 id;
            for (int i = from; i < to; i++)
            {
                original_date = (string)tempTable.Rows[i][Constants.DATE] + " " + (string)tempTable.Rows[i][Constants.TIME];
                result = DateTime.TryParse(original_date, out dtTemp);
                if (!result) continue; // Since we can't get a correct date/time, don't create an update query for that row.

                columnname_value_list = new Dictionary<String, Object>();                       // UPdate the date and time
                columnname_value_list.Add(Constants.DATE, tempTable.Rows[i][Constants.DATE]);   
                columnname_value_list.Add(Constants.TIME, tempTable.Rows[i][Constants.TIME]);
                id = (Int64)tempTable.Rows[i][Constants.ID];
                where = Constants.ID + " = " + tempTable.Rows[i][Constants.ID].ToString();                             // on this paticular row with the given id
                update_query_list.Add(columnname_value_list, where);
            }
            this.DB.UpdateWhereBeginEnd(Constants.TABLEDATA, update_query_list, out result, out command_executed);
        }

        // Update all the date fields by swapping the days and months.
        // This should ONLY be called if such swapping across all dates (excepting corrupt ones) is possible
        // as otherwise it will only swap those dates it can
        // It also assumes that the data table is showing All images
        public void RowsUpdateSwapDayMonth()
        {
            bool result;
            string command_executed;
            string original_date = "";
            DateTime dtDate;
            DateTime reversedDate;
            Dictionary<Dictionary<String, Object>, String> update_query_list = new Dictionary<Dictionary<String, Object>, String>();
            Dictionary<String, Object> columnname_value_list;
            String where;
            Int64 id;

            if (this.dataTable.Rows.Count == 0) return;

            // Get the original date value of each. If we can swap the date order, do so. 
            for (int i = 0; i < dataTable.Rows.Count; i++)
            {
                if (this.RowIsImageCorrupted(i)) continue;  // skip over corrupted images
                try 
                { 
                    // If we fail on any of these, continue on to the next date
                    original_date = (string)dataTable.Rows[i][Constants.DATE];
                    dtDate = DateTime.Parse(original_date);
                    reversedDate = new DateTime(dtDate.Year, dtDate.Day, dtDate.Month); // we have swapped the day with the month
                }
                catch 
                {
                    continue;
                };

                // Now update the actual database with the new date/time values stored in the temporary table
                columnname_value_list = new Dictionary<String, Object>();                  // Update the date 
                columnname_value_list.Add(Constants.DATE, DateTimeHandler.StandardDateString(reversedDate));
                id = (Int64)this.dataTable.Rows[i][Constants.ID];
                where = Constants.ID + " = " + id.ToString();                             // on this paticular row with the given id
                update_query_list.Add(columnname_value_list, where);
            }
            this.DB.UpdateWhereBeginEnd(Constants.TABLEDATA, update_query_list, out result, out command_executed);
        }
        #endregion

        #region Public Methods for Deleting Rows
        public void DeleteRow(int id)
        {
            bool result=true;
            string command_executed = "";
            this.DB.DeleteFromTable(Constants.TABLEDATA, "ID = " + id.ToString(), out result, out command_executed);
        }
        #endregion

        #region Public methods for Pragmas: Getting the schema
        // Not used
        public void GetTableSchema (string table)
        {
            bool result = false;
            string command_executed = "";
            //DataRow row;
           // DataColumn col;
            DataTable temp = this.DB.GetDataTableFromSelect("Pragma table_info('TemplateTable')", out result, out  command_executed);
            if (result == false) return;
            for (int i = 0; i < temp.Rows.Count; i++ )
            {
                string s = "====";
                for (int j = 0; j < temp.Columns.Count; j++ )
                { 
                    s += " " + temp.Rows[i][j].ToString();
                }
                Debug.Print (s);
            }
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
                if (this.CurrentRow == -1) return -1;
                return Convert.ToInt32(this.dataTable.Rows[this.CurrentRow][Constants.ID]); // The Id of the current row
            } 
            catch  // If for some reason the above fails, we want to at least return something.
            {
                return -1;
            }
        }

        /// <summary> 
        // Go to the first row, returning true if we can otherwise false
        /// </summary>
        public bool ToDataRowFirst()
        {
            // Check if there are no rows. If none, set Id and Row indicators to reflect that
            if (this.dataTable.Rows.Count <= 0)
            {
                this.CurrentId = -1;
                this.CurrentRow = -1;
                return false;
            }

            // We have some rows. The first row is always 0, and then get the Id in that first row
            this.CurrentRow = 0; // The first row
            this.CurrentId = GetIdOfCurrentRow();
            return true;
        }

        /// <summary> 
        // Go to the next row, returning false if we can;t (e.g., if we are at the end) 
        /// </summary>
        public bool ToDataRowNext()
        {
            int count = this.dataTable.Rows.Count;

            // Check if we are on the last row. If so, do nothing and return false.
            if (this.CurrentRow >= (count - 1)) return false;

            // Go to the next row
            this.CurrentRow += 1;
            this.CurrentId = GetIdOfCurrentRow();
            return true;
        }

        /// <summary>
        /// Go to the previous row, returning true if we can otherwise false (e.g., if we are at the beginning)
        /// </summary>
        /// <returns></returns>
        public bool ToDataRowPrevious()
        {
            // Check if we are on the first row. If so, do nothing and return false.
            if (this.CurrentRow == 0) return false;

            // Go to the previous row
            this.CurrentRow -= 1;
            this.CurrentId = GetIdOfCurrentRow();
            return true;
        }

        /// <summary>
        /// Go to a particular row, returning true if we can otherwise false (e.g., if the index is out of range)
        /// Remember, that we are zero based, so (for example) and index of 5 will go to the 6th row
        /// </summary>
        /// <returns></returns>
        public bool ToDataRowIndex(int row_index)
        {
            int count = this.dataTable.Rows.Count;

            // Check if that particular row exists. If so, do nothing and return false.

            if (this.RowInBounds(row_index))
            {
                // Go to the previous row
                this.CurrentRow = row_index;
                this.CurrentId = GetIdOfCurrentRow();
                return true;
            }
            else return false;
        }
        #endregion 
        
        #region Public Methods to get values from a TableData given an ID
        // Given a key and a data label, return its string value. Set result to succeeded or failed
        /// <summary>
        /// Example usage:
        ///   bool result;
        ///   string str = this.dbData.KeyGetValueFromDataLabel(3, Constants.FILE, out result);
        ///   MessageBox.Show(result.ToString() + " " + str);
        /// </summary>

        public string IDGetValueFromDataLabel(int key, string data_label, out bool result)
        {
            result = false;
            string found_string = "";
            DataRow foundRow = dataTable.Rows.Find(key);
            if (null != foundRow)
            {
                try
                {
                    found_string = (string)foundRow[data_label];
                    result = true;
                }
                catch { }
            }
            return found_string;
        } 

        // Convenience functions for the standard data types, with the ID supplied
        public string IDGetFile(int key, out bool result) { return (string) IDGetValueFromDataLabel(key, Constants.FILE, out result);}
        public string IDGetFolder(int key, out bool result) { return (string)IDGetValueFromDataLabel(key, Constants.FOLDER, out result); }
        public string IDGetDate(int key, out bool result) { return (string)IDGetValueFromDataLabel(key, Constants.DATE, out result); }
        public string IDGetTime(int key, out bool result) { return (string) IDGetValueFromDataLabel(key, Constants.TIME, out result); }
        public string IDGetImageQuality(int key, out bool result) { return (string) IDGetValueFromDataLabel(key, Constants.IMAGEQUALITY, out result); }

        // Convenience functions for the standard data types, where it assumes the Id is the currentID
        public string IDGetFile(out bool result) { return (string)IDGetValueFromDataLabel(this.CurrentId, Constants.FILE, out result); }
        public string IDGetFolder(out bool result) { return (string)IDGetValueFromDataLabel(this.CurrentId, Constants.FOLDER, out result); }
        public string IDGetDate(out bool result) { return (string)IDGetValueFromDataLabel(this.CurrentId, Constants.DATE, out result); }
        public string IDGetTime(out bool result) { return (string)IDGetValueFromDataLabel(this.CurrentId, Constants.TIME, out result); }
        public string IDGetImageQuality(out bool result) { return (string)IDGetValueFromDataLabel(this.CurrentId, Constants.IMAGEQUALITY, out result); }
        #endregion

        #region Public Methods to get values from a TableData given row

        public string RowGetValueFromType(string type)
        {
            return RowGetValueFromType (type, this.CurrentRow);
        }
        public string RowGetValueFromType (string type, int row_index)
        {
            if (this.RowInBounds(row_index))
            {
                string key = (string) this.DataLabelFromType[type];
                string result;
                try {result = (string)this.dataTable.Rows[row_index][key]; }
                catch { result = ""; }
                return result;
            }
            else return "";
        }

        // Given a row index, return the ID
        public int RowGetID (int row_index)
        {
            if (! this.RowInBounds(row_index)) return -1;
            try 
            {
                Int64 id = (Int64) this.dataTable.Rows[row_index][Constants.ID];
                return Convert.ToInt32(id);
            }
            catch 
            { 
                return-1; 
            }
        }

        public string RowGetValueFromDataLabel (string data_label)
        {
            return RowGetValueFromDataLabel(data_label, this.CurrentRow);
        }
        public string RowGetValueFromDataLabel(string data_label, int row_index)
        {
            if (this.RowInBounds(row_index))
            {
                return (string)this.dataTable.Rows[row_index][data_label];
            }
            else return "";
        }


        // A convenience routine for checking to see if the image in the current row is displayable (i.e., not corrupted or missing)
        /// <summary> A convenience routine for checking to see if the image in the current row is displayable (i.e., not corrupted or missing) </summary>
        /// <returns></returns>
        public bool RowIsImageDisplayable ()
        {
            return RowIsImageDisplayable(this.CurrentRow);
        }
        /// <summary> A convenience routine for checking to see if the image in the given row is displayable (i.e., not corrupted or missing)</summary>
        /// <param name="row"></param>
        /// <returns></returns>
        public bool RowIsImageDisplayable(int row)
        {
            string result = RowGetValueFromDataLabel((string)this.DataLabelFromType[Constants.IMAGEQUALITY], row);
            if (result.Equals(Constants.IMAGEQUALITY_CORRUPTED) || result.Equals (Constants.IMAGEQUALITY_MISSING)) return false;
            return true;
        }

        /// <summary> A convenience routine for checking to see if the image in the given row is corrupted</summary>
        /// <param name="row"></param>
        /// <returns></returns>
        public bool RowIsImageCorrupted(int row)
        {
            string result = RowGetValueFromDataLabel((string)this.DataLabelFromType[Constants.IMAGEQUALITY], row);
            return (result.Equals(Constants.IMAGEQUALITY_CORRUPTED)) ?  true : false;
        }

        // Find the next displayable image after the provided row in the current image set
        public int RowFindNextDisplayableImage (int initial_row)
        {
            for (int row = initial_row; row < this.dataTable.Rows.Count; row++)
            {
                if (RowIsImageDisplayable(row)) return row;
            }
            return -1;
        }
        #endregion

        #region Public Methods to get values from a TemplateTable 

        public DataTable TemplateGetSortedByControls()
        {
            DataTable tempdt = this.templateTable.Copy();
            DataView dv = tempdt.DefaultView;
            dv.Sort = Constants.CONTROLORDER + " ASC";
            return dv.ToTable();
        }

        public bool TemplateIsCopyable(string data_label)
        {
            bool is_copyable;
            int id = GetID(data_label);
            DataRow foundRow = templateTable.Rows.Find(id);
            return bool.TryParse((string)foundRow[Constants.COPYABLE], out is_copyable) ? is_copyable : false;
            //id--;
            //return bool.TryParse((string)templateTable.Rows[id][Constants.COPYABLE], out is_copyable) ? is_copyable : false;
        }

        public string TemplateGetDefault(string data_label)
        {
            int id = GetID(data_label);
            DataRow foundRow = templateTable.Rows.Find(id);
            return (string) foundRow[Constants.DEFAULT];
        }
        
        #endregion

        #region Public methods to set values in the TableData row
        public void RowSetValueFromDataLabel(string key, string value)
        {
            RowSetValueFromKey(key, value, this.CurrentRow);
        }
        private void RowSetValueFromKey(string key, string value, int row_index)
        {
            if (this.RowInBounds(row_index))
            {
               this.dataTable.Rows[this.CurrentRow][key] = value;
               this.UpdateRow(this.CurrentId, key, value);
            }
        }
        #endregion

        #region Public methods to get / set values in the ImageSetData

        /// <summary>Given a key, return its value</summary>
        /// <param name="key"></param>
        /// <returns></returns>
        private string ImageSetGetValue (string key)
        {
            bool result;
            string command_executed = "";
            // Get the single row
            string query = "Select * From " + Constants.TABLEIMAGESET + " WHERE " + Constants.ID + " = 1" ;
            DataTable imagesetTable = this.DB.GetDataTableFromSelect(query, out result, out command_executed);
            if (imagesetTable.Rows.Count == 0) return "" ;
            return (string) imagesetTable.Rows[0][key];
         }

        // <summary>Given a key, value pair, update its value</summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        private void ImageSetGetValue (string key, string value)
        {
            this.UpdateRow(1, Constants.LOG, value, Constants.TABLEIMAGESET);
        }
        #endregion

        #region Public Methods to manipulate the MarkersTable

        /// <summary>
        /// Get the metatag counter list associated with all counters representing the current row
        /// It will have a MetaTagCounter for each control, even if there may be no metatags in it
        /// </summary>
        /// <returns>List<MetaTagCounter></returns>
        public List<MetaTagCounter> MarkerTableGetMetaTagCounterList ()
        {
            List<MetaTagCounter> metaTagCounters = new List<MetaTagCounter>();

            // Test to see if we actually have a valid result
            if (this.markerTable.Rows.Count == 0) return metaTagCounters;    // This should not really happen, but just in case
            if (this.markerTable.Columns.Count == 0) return metaTagCounters; // Should also not happen as this wouldn't be called unless we have at least one counter control

            int id = this.GetIdOfCurrentRow();

            // Get the current row number of the id in the marker table
            int row_num = MarkerTableFindRowNumber(id);
            if (row_num < 0) return metaTagCounters;

            // Iterate through the columns, where we create a new MetaTagCounter for each control and add it to the MetaTagCounte rList
            MetaTagCounter mtagCounter;
            string datalabel = "";
            string value = "";
            List<Point> points;
            for (int i = 0; i < markerTable.Columns.Count; i++)
            {
                datalabel = markerTable.Columns[i].ColumnName;
                if (datalabel.Equals(Constants.ID)) continue;  // Skip the ID

                // Create a new MetaTagCounter representing this control's meta tag,
                mtagCounter = new MetaTagCounter();
                mtagCounter.DataLabel = datalabel;

                // Now create a new Metatag for each point and add it to the counter
                try { value = (string)markerTable.Rows[row_num][datalabel]; } catch { value = ""; }
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
        /// Given a point, add it to the Counter (identified by its datalabel) within the current row in the Marker table. 
        /// </summary>
        /// <param name="dataLabel"></param>
        /// <param name="point">A list of points in the form x,y|x,y|x,y</param>
        public void MarkerTableAddPoint(string dataLabel, string pointlist)
        {
            // Find the current row number
            int id = this.GetIdOfCurrentRow();
            int row_num = MarkerTableFindRowNumber(id);
            if (row_num < 0) return;

            // Update the database and datatable
            this.markerTable.Rows[row_num][dataLabel] = pointlist;
            this.UpdateRow(id, dataLabel, pointlist, Constants.TABLEMARKERS);  // Update the database
        }

        // Given a list of column/value pairs (the String,Object) and the FILE name indicating a row, update it
        public void RowsUpdateMarkerRows(Dictionary<Dictionary<String, Object>, String> update_query_list)
        {
            bool result;
            string command_executed;
            this.DB.UpdateWhereBeginEnd(Constants.TABLEMARKERS, update_query_list, out result, out command_executed);
        }
        public void RowsUpdateMarkerRows(List <ColumnTupleListWhere> update_query_list)
        {
            bool result;
            string command_executed;
            this.DB.UpdateWhereBeginEnd(Constants.TABLEMARKERS, update_query_list, out result, out command_executed);
        }

        // The id is the row to update, the datalabels are the labels of each control to updata, 
        // and the markers are the respective point lists for each of those labels
        public void UpdateMarkersInRows(List<ColumnTupleListWhere> all_markers)
        {
            string sid;
            int id;
            char [] quote = {'\''};
            foreach (ColumnTupleListWhere ctlw in all_markers)
            {
                ColumnTupleList ctl = ctlw.Listpair;
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
                    if (!ct.ColumnValue.Equals("")) { 
                        this.markerTable.Rows[id - 1][ct.ColumnName] = ct.ColumnValue;
                    }
                }
            }
        }
        // OLD VERSION OF UPDATEMARKERS IN ROW, CAN PROBABLY DELETE
        // The id is the row to update, the datalabels are the labels of each control to updata, 
        // and the markers are the respective point lists for each of those labels
        //public void UpdateMarkersInRow (int id, List<string> datalabels, List<string> markers)
        //{
        //    bool result = false;
        //    string command_executed = "";
        //    Dictionary<String, Object> columnname_value_list = new Dictionary<String,Object> ();
        //    for (int i = 0; i < datalabels.Count; i++)
        //    {
        //        this.markerTable.Rows[id-1][datalabels[i]] = markers[i];
        //        // If the markers are non-empty, add it to the list of things to update
        //        if (!markers[i].Equals(""))
        //            columnname_value_list.Add (datalabels[i], markers[i]);
        //    }
        //    if (columnname_value_list.Count > 0)
        //        this.DB.UpdateWhere(Constants.TABLEMARKERS, columnname_value_list, Constants.ID + "=" + id, out result, out command_executed);
        //}

        /// <summary>
        /// Given an id, find the row number that matches it in the Marker Table
        /// </summary>
        /// <param name="id"></param>
        /// <returns>-1 on failure</returns>
        private int MarkerTableFindRowNumber(int id)
        {
            for (int row_number=0; row_number< this.markerTable.Rows.Count; row_number++)
            {
                string str = markerTable.Rows[row_number][Constants.ID].ToString();
                int this_id;
                if (Int32.TryParse(str, out this_id) == false) return -1; 
                if (this_id == id) return row_number;
            }
            return -1;
        }

        private List<Point> ParseCoords (string value)
        {
            List<Point> points = new List<Point>();
            if (value.Equals("")) return points;

            char[] delimiterBar = { Constants.MARKERBAR };
            string[] sPoints = value.Split(delimiterBar);

            foreach (string s in sPoints)
            {
                Point point = Point.Parse (s);
                points.Add(point);
            }
            return points;
        }
        #endregion

        #region Private Methods

        private bool RowInBounds (int row_index)
        {
            return (row_index >= 0 && row_index < this.dataTable.Rows.Count ) ? true : false;
        }

        private DataRow GetTemplateRow(string data_label)
        {
            int id = GetID(data_label);
            return templateTable.Rows.Find(id);
            // id--;
            //return this.templateTable.Rows[id];
        }

        /// <summary>Given a datalabel, get the id of the key's row</summary>
        /// <param name="data_label"></param>
        /// <returns></returns>
        private int GetID (string data_label)
        {
            for (int i = 0; i < templateTable.Rows.Count; i++)
            {
                if (data_label.Equals(templateTable.Rows[i][Constants.DATALABEL]))
                { 
                    Int64 id= (Int64) templateTable.Rows[i][Constants.ID];
                    return Convert.ToInt32(id);
                }
            }
            return -1;
        }
        #endregion
    }

}
