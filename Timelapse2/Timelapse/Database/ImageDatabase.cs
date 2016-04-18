using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using Timelapse.Images;
using Timelapse.Util;

namespace Timelapse.Database
{
    public class ImageDatabase
    {
        // A pointer to the Timelapse Data database
        private SQLiteWrapper database;

        // the current image, null if its no been set or if the database is empty
        public ImageProperties CurrentImage { get; private set; }
        public int CurrentImageRow { get; private set; }

        /// <summary>Gets the file name of the image database on disk.</summary>
        public string FileName { get; private set; }

        /// <summary>Gets the complete path to the folder containing the image database.</summary>
        public string FolderPath { get; private set; }

        /// <summary>Gets or sets the database's template table.</summary>
        public DataTable TemplateTable { get; set; }

        public Dictionary<string, string> DataLabelFromControlType { get; private set; }

        // contains the results of the data query
        public DataTable ImageDataTable { get; private set; }

        // contains the markers
        public DataTable MarkerTable { get; private set; }

        public Dictionary<string, string> ControlTypeFromDataLabel { get; private set; }

        public ImageDatabase(string folderPath, string fileName)
        {
            this.ControlTypeFromDataLabel = new Dictionary<string, string>();
            this.CurrentImage = null;
            this.CurrentImageRow = -1;
            this.DataLabelFromControlType = new Dictionary<string, string>();
            this.ImageDataTable = new DataTable();
            this.FolderPath = folderPath;
            this.FileName = fileName;
            this.MarkerTable = new DataTable();
        }

        /// <summary>Gets the number of images currently in the image table.</summary>
        public int ImageCount
        {
            get { return this.ImageDataTable.Rows.Count; }
        }

        public void AppendToImageSetLog(StringBuilder logEntry)
        {
            string existingLog = this.GetImageSetLog();
            this.SetImageSetLog(existingLog + logEntry.ToString());
        }

        public int TrySelectDatabaseFile()
        {
            string[] files = Directory.GetFiles(this.FolderPath, "*.ddb");
            if (files.Count() == 1)
            {
                this.FileName = Path.GetFileName(files[0]); // Get the file name, excluding the path
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
            return 2; // There are no existing .ddb files
        }

        /// <summary>
        /// Create a database file (if needed) and connect to it. Also keeps a local copy of the template
        /// </summary>
        /// <returns>true if the database could be created, false otherwise</returns>
        public bool TryCreateImageDatabase(TemplateDatabase template)
        {
            // Create the DB
            try
            {
                this.database = new SQLiteWrapper(this.GetDatabaseFilePath());
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
            this.database.CreateTable(Constants.Database.ImageDataTable, column_definition, out result, out command_executed);
            // Debug.Print (result.ToString() + " " + command_executed);
            string command = "Select * FROM " + Constants.Database.ImageDataTable;
            DataTable dataTable;
            result = this.database.TryGetDataTableFromSelect(command, out dataTable);
            this.ImageDataTable = dataTable;

            // 2. Create the ImageSetTable and initialize a single row in it
            column_definition.Clear();
            column_definition.Add(Constants.Database.ID, Constants.Database.CreationStringPrimaryKey);  // It begins with the ID integer primary key
            column_definition.Add(Constants.DatabaseColumn.Log, " TEXT DEFAULT 'Add text here.'");

            column_definition.Add(Constants.DatabaseColumn.Magnifier, " TEXT DEFAULT 'true'");
            column_definition.Add(Constants.DatabaseColumn.Row, " TEXT DEFAULT '0'");
            int ifilter = (int)ImageQualityFilter.All;
            column_definition.Add(Constants.DatabaseColumn.Filter, " TEXT DEFAULT '" + ifilter.ToString() + "'");
            this.database.CreateTable(Constants.Database.ImageSetTable, column_definition, out result, out command_executed);

            Dictionary<String, String> dataline = new Dictionary<String, String>(); // Populate the data for the image set with defaults
            dataline.Add(Constants.DatabaseColumn.Log, "Add text here");
            dataline.Add(Constants.DatabaseColumn.Magnifier, "true");
            dataline.Add(Constants.DatabaseColumn.Row, "0");
            dataline.Add(Constants.DatabaseColumn.Filter, ifilter.ToString());
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
                if (type.Equals(Constants.DatabaseColumn.Counter))
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
            return File.Exists(this.GetDatabaseFilePath());
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
            foreach (DataRow row in this.TemplateTable.Rows)
            {
                long id = (long)row[Constants.Database.ID];
                string dataLabel = (string)row[Constants.Control.DataLabel];
                string controlType = (string)row[Constants.Database.Type];

                // We don't want to add these types to the hash, as there can be multiple ones, which means the key would not be unique
                if (!(controlType.Equals(Constants.DatabaseColumn.Note) ||
                      controlType.Equals(Constants.DatabaseColumn.FixedChoice) ||
                      controlType.Equals(Constants.DatabaseColumn.Counter) ||
                      controlType.Equals(Constants.DatabaseColumn.Flag)))
                {
                    this.DataLabelFromControlType.Add(controlType, dataLabel);
                }
                this.ControlTypeFromDataLabel.Add(dataLabel, controlType);
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

        public DataTable CreateDataTableFromDatabaseTable(string tablename)
        {
            string command = "Select * FROM " + tablename;
            DataTable dataTable;
            bool result = this.database.TryGetDataTableFromSelect(command, out dataTable);
            return dataTable;
        }

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
            string where = this.DataLabelFromControlType[Constants.DatabaseColumn.ImageQuality]; // key
            where += "=\"" + Constants.ImageQuality.Corrupted + "\"";  // = value

            return this.GetImages("*", where);
        }

        // Filter: Dark images only
        public bool GetImagesDark()
        {
            string where = this.DataLabelFromControlType[Constants.DatabaseColumn.ImageQuality]; // key
            where += "=\"" + Constants.ImageQuality.Dark + "\"";  // = value

            return this.GetImages("*", where);
        }

        // Filter: Missing images only
        public bool GetImagesMissing()
        {
            string where = this.DataLabelFromControlType[Constants.DatabaseColumn.ImageQuality]; // key
            where += "=\"" + Constants.ImageQuality.Missing + "\"";  // = value

            return this.GetImages("*", where);
        }

        // Filter:  images marked for deltion
        public bool GetImagesMarkedForDeletion()
        {
            string where = this.DataLabelFromControlType[Constants.DatabaseColumn.DeleteFlag]; // key
            where += "=\"true\""; // = value
            return this.GetImages("*", where);
        }

        public DataTable GetDataTableOfImagesMarkedForDeletion()
        {
            string where = this.DataLabelFromControlType[Constants.DatabaseColumn.DeleteFlag]; // key
            where += "=\"true\""; // = value
            return this.GetDataTableOfImages("*", where);
        }

        public bool GetImagesAllButDarkAndCorrupted()
        {
            string where = this.DataLabelFromControlType[Constants.DatabaseColumn.ImageQuality]; // key
            where += " IS NOT \"" + Constants.ImageQuality.Dark + "\"";  // = value
            where += " AND ";
            where += this.DataLabelFromControlType[Constants.DatabaseColumn.ImageQuality];
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
            query += " FROM " + Constants.Database.ImageDataTable;
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
            this.ImageDataTable = dataTable;
            return true;
        }

        public ImageQualityFilter GetImageSetFilter()
        {
            string result = this.ImageSetGetValue(Constants.DatabaseColumn.Filter);
            return (ImageQualityFilter)Convert.ToInt32(result);
        }

        public bool GetImageSetWhiteSpaceTrimmed()
        {
            string result = this.ImageSetGetValue(Constants.DatabaseColumn.WhiteSpaceTrimmed);
            return Convert.ToBoolean(result);
        }

        public string GetImageSetLog()
        {
            return this.ImageSetGetValue(Constants.DatabaseColumn.Log);
        }

        private DataTable GetDataTableOfImages(string select, string where)
        {
            string query = "Select " + select;
            query += " FROM " + Constants.Database.ImageDataTable;
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
            string query = "Select * FROM " + Constants.Database.ImageDataTable;
            DataTable dataTable;
            bool result = this.database.TryGetDataTableFromSelect(query, out dataTable);
            return dataTable;
        }

        public Dictionary<ImageQualityFilter, int> GetImageCounts()
        {
            Dictionary<ImageQualityFilter, int> counts = new Dictionary<ImageQualityFilter, int>();
            counts[ImageQualityFilter.Dark] = this.GetImageCountByQuality(Constants.ImageQuality.Dark);
            counts[ImageQualityFilter.Corrupted] = this.GetImageCountByQuality(Constants.ImageQuality.Corrupted);
            counts[ImageQualityFilter.Missing] = this.GetImageCountByQuality(Constants.ImageQuality.Missing);
            counts[ImageQualityFilter.Ok] = this.GetImageCountByQuality(Constants.ImageQuality.Ok);
            return counts;
        }

        public int GetDeletedImageCount()
        {
            bool result;
            string command_executed = String.Empty;
            try
            {
                string query = "Select Count(*) FROM " + Constants.Database.ImageDataTable + " Where " + this.DataLabelFromControlType[Constants.DatabaseColumn.DeleteFlag] + " = \"true\"";
                return this.database.GetCountFromSelect(query, out result, out command_executed);
            }
            catch
            {
                return 0;
            }
        }

        // This first form just returns the count of all images with no filters applied
        public int GetImageCount()
        {
            bool result;
            string command_executed = String.Empty;
            try
            {
                string query = "Select Count(*) FROM " + Constants.Database.ImageDataTable;
                return this.database.GetCountFromSelect(query, out result, out command_executed);
            }
            catch
            {
                return 0;
            }
        }

        private int GetImageCountByQuality(string imageQualityFilter)
        {
            try
            {
                string query = "Select Count(*) FROM " + Constants.Database.ImageDataTable + " Where " + this.DataLabelFromControlType[Constants.DatabaseColumn.ImageQuality] + " = \"" + imageQualityFilter + "\"";
                bool result;
                string commandExecuted;
                return this.database.GetCountFromSelect(query, out result, out commandExecuted);
            }
            catch
            {
                return 0;
            }
        }

        public int GetImageCountWithCustomFilter(string where)
        {
            try
            {
                string query = "Select Count(*) FROM " + Constants.Database.ImageDataTable + " Where " + where;
                bool result;
                string commandExecuted = String.Empty;
                return this.database.GetCountFromSelect(query, out result, out commandExecuted);
            }
            catch
            {
                return 0;
            }
        }

        // Insert one or more rows into a table
        public void InsertMultipleRows(string table, List<Dictionary<String, String>> insertionStatements)
        {
            bool result;
            string command_executed = String.Empty;
            this.database.InsertMultiplesBeginEnd(table, insertionStatements, out result, out command_executed);
        }

        public bool IsMagnifierEnabled()
        {
            string result = this.ImageSetGetValue(Constants.DatabaseColumn.Magnifier);
            return Convert.ToBoolean(result);
        }

        /// <summary>
        /// Update a column value (identified by its key) in an existing row (identified by its ID) 
        /// By default, if the table parameter is not included, we use the TABLEDATA table
        /// </summary>
        public void UpdateImage(long id, string dataLabel, string value)
        {
            this.UpdateID(id, dataLabel, value, Constants.Database.ImageDataTable);
        }

        public void UpdateID(long id, string dataLabel, string value, string table)
        {
            // update the row in the database
            string where = Constants.Database.ID + " = " + id.ToString();
            Dictionary<String, object> dataline = new Dictionary<String, object>(); // Populate the data 
            dataline.Add(dataLabel, value);
            bool result;
            string command_executed;
            this.database.UpdateWhere(table, dataline, where, out result, out command_executed);

            // update the copy of the row in the loaded data table
            DataTable dataTable;
            switch (table)
            {
                case Constants.Database.ImageDataTable:
                    dataTable = this.ImageDataTable;
                    break;
                case Constants.Database.ImageSetTable:
                    // image set operations go directly to database; no data table is in use
                    return;
                case Constants.Database.MarkersTable:
                    dataTable = this.MarkerTable;
                    break;
                case Constants.Database.TemplateTable:
                    dataTable = this.TemplateTable;
                    break;
                default:
                    throw new NotSupportedException(String.Format("Unhandled table {0}.", table));
            }

            // nothing to do if the data table hasn't been loaded
            if (dataTable == null)
            {
                return;
            }

            // update the table
            DataRow[] foundRows = dataTable.Select(Constants.Database.ID + " = " + id);
            if (foundRows.Length == 1)
            {
                int index = dataTable.Rows.IndexOf(foundRows[0]);
                dataTable.Rows[index][dataLabel] = (string)value;
            }
            else
            {
                Debug.Assert(false, String.Format("Found {0} rows with ID {1}.", foundRows.Length, id));
            }
        }

        // Update all rows in the filtered view only with the given key/value pair
        public void UpdateAllImagesInFilteredView(string dataLabel, string value)
        {
            Dictionary<Dictionary<String, object>, String> updateQuery = new Dictionary<Dictionary<String, object>, String>();
            for (int i = 0; i < this.ImageCount; i++)
            {
                this.ImageDataTable.Rows[i][dataLabel] = value;
                Dictionary<string, object> columnNameAndValue = new Dictionary<string, object>();
                columnNameAndValue.Add(dataLabel, value);
                long id = (long)this.ImageDataTable.Rows[i][Constants.Database.ID];
                string where = Constants.Database.ID + " = " + this.ImageDataTable.Rows[i][Constants.Database.ID].ToString();                             // on this paticular row with the given id
                updateQuery.Add(columnNameAndValue, where);
            }

            bool result;
            string command_executed;
            this.database.UpdateWhereBeginEnd(Constants.Database.ImageDataTable, updateQuery, out result, out command_executed);
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
        public void UpdateImages(List<Tuple<long, string, string>> imagesToUpdate)
        {
            // update data table
            Dictionary<Dictionary<string, object>, string> updateQuery = new Dictionary<Dictionary<string, object>, string>();
            foreach (Tuple<long, string, string> image in imagesToUpdate)
            {
                string id = image.Item1.ToString();
                string dataLabel = image.Item2;
                string value = image.Item3;
                // Debug.Print(id.ToString() + " : " + key + " : " + value);

                DataRow datarow = this.ImageDataTable.Rows.Find(id);
                datarow[dataLabel] = value;
                // Debug.Print(datarow.ToString());

                Dictionary<string, object> columnNameAndValue = new Dictionary<string, object>();
                columnNameAndValue.Add(dataLabel, value);
                string where = Constants.Database.ID + " = " + id;
                updateQuery.Add(columnNameAndValue, where);
            }

            // update database
            bool result;
            string commandExecuted;
            this.database.UpdateWhereBeginEnd(Constants.Database.ImageDataTable, updateQuery, out result, out commandExecuted);
        }

        // Given a list of column/value pairs (the string,object) and the FILE name indicating a row, update it
        public void UpdateImages(Dictionary<Dictionary<string, object>, string> updateQuery)
        {
            bool result;
            string commandCxecuted;
            this.database.UpdateWhereBeginEnd(Constants.Database.ImageDataTable, updateQuery, out result, out commandCxecuted);
        }

        public void UpdateImages(string dataLabel, string value, int fromRow, int toRow)
        {
            Dictionary<Dictionary<string, object>, string> updateQuery = new Dictionary<Dictionary<string, object>, string>();
            int fromIndex = fromRow + 1; // rows start at 0, while indexes start at 1
            int toIndex = toRow + 1;
            for (int index = fromRow; index <= toRow; index++)
            {
                // update data table
                // TODO: Saul  is there an off by one error here as .Rows is accessed with a one based count?
                this.ImageDataTable.Rows[index][dataLabel] = value;
                Dictionary<string, object> columnname_value_list = new Dictionary<string, object>();
                columnname_value_list.Add(dataLabel, value);
                long id = (long)this.ImageDataTable.Rows[index][Constants.Database.ID];

                // update database
                string where = Constants.Database.ID + " = " + this.ImageDataTable.Rows[index][Constants.Database.ID].ToString();                             // on this paticular row with the given id
                updateQuery.Add(columnname_value_list, where);
            }

            bool result;
            string commandExecuted;
            this.database.UpdateWhereBeginEnd(Constants.Database.ImageDataTable, updateQuery, out result, out commandExecuted);
        }

        // Given a time difference in ticks, update all the date / time field in the database
        // Note that it does NOT update the dataTable - this has to be done outside of this routine by regenerating the datatables with whatever filter is being used..
        // TODO: modify this to include argments showing the current filtered view and row number, perhaps, so we could restore the datatable and the view?? 
        // TODO But that would add complications if there are unanticipated filtered views.
        // TODO: Another option is to go through whatever the current datatable is and just update those fields. 
        public void AdjustAllImageTimes(TimeSpan adjustment, int from, int to)
        {
            // We create a temporary table. We do this just in case we are currently on a filtered view
            DataTable tempTable;
            if (this.database.TryGetDataTableFromSelect("Select * FROM " + Constants.Database.ImageDataTable, out tempTable) == false)
            {
                return;
            }

            // We now have an unfiltered temporary data table
            // Get the original value of each, and update each date by the corrected amount if possible
            bool result;
            for (int i = from; i < to; i++)
            {
                string original_date = (string)tempTable.Rows[i][Constants.DatabaseColumn.Date] + " " + (string)tempTable.Rows[i][Constants.DatabaseColumn.Time];
                DateTime date;
                result = DateTime.TryParse(original_date, out date);
                if (!result)
                {
                    continue; // Since we can't get a correct date/time, just leave it unaltered.
                }

                // correct the date and modify the temporary datatable rows accordingly
                date += adjustment;
                tempTable.Rows[i][Constants.DatabaseColumn.Date] = DateTimeHandler.StandardDateString(date);
                tempTable.Rows[i][Constants.DatabaseColumn.Time] = DateTimeHandler.StandardTimeString(date);
            }

            // Now update the actual database with the new date/time values stored in the temporary table
            Dictionary<Dictionary<string, object>, string> update_query_list = new Dictionary<Dictionary<string, object>, string>();
            Dictionary<string, object> columnname_value_list;
            string where;
            Int64 id;
            for (int i = from; i < to; i++)
            {
                string original_date = (string)tempTable.Rows[i][Constants.DatabaseColumn.Date] + " " + (string)tempTable.Rows[i][Constants.DatabaseColumn.Time];
                DateTime date;
                result = DateTime.TryParse(original_date, out date);
                if (!result)
                {
                    continue; // Since we can't get a correct date/time, don't create an update query for that row.
                }

                columnname_value_list = new Dictionary<string, object>();                       // Update the date and time
                columnname_value_list.Add(Constants.DatabaseColumn.Date, tempTable.Rows[i][Constants.DatabaseColumn.Date]);
                columnname_value_list.Add(Constants.DatabaseColumn.Time, tempTable.Rows[i][Constants.DatabaseColumn.Time]);
                id = (Int64)tempTable.Rows[i][Constants.Database.ID];
                where = Constants.Database.ID + " = " + tempTable.Rows[i][Constants.Database.ID].ToString();                             // on this paticular row with the given id
                update_query_list.Add(columnname_value_list, where);
            }

            string command_executed;
            this.database.UpdateWhereBeginEnd(Constants.Database.ImageDataTable, update_query_list, out result, out command_executed);
        }

        // Update all the date fields by swapping the days and months.
        // This should ONLY be called if such swapping across all dates (excepting corrupt ones) is possible
        // as otherwise it will only swap those dates it can
        // It also assumes that the data table is showing All images
        public void ExchangeDayAndMonthInImageDate()
        {
            this.ExchangeDayAndMonthInImageDate(0, this.ImageCount);
        }

        // Update all the date fields between the start and end index by swapping the days and months.
        // It  assumes that the data table is showing All images
        public void ExchangeDayAndMonthInImageDate(int startRow, int endRow)
        {
            if (this.ImageCount == 0 || startRow >= this.ImageCount || endRow >= this.ImageCount)
            {
                return;
            }

            // Get the original date value of each. If we can swap the date order, do so. 
            Dictionary<Dictionary<string, object>, string> updateQuery = new Dictionary<Dictionary<string, object>, string>();
            for (int i = startRow; i <= endRow; i++)
            {
                if (this.IsImageCorrupt(i))
                {
                    continue;  // skip over corrupted images
                }

                DateTime reversedDate;
                try
                {
                    // If we fail on any of these, continue on to the next date
                    string original_date = (string)this.ImageDataTable.Rows[i][Constants.DatabaseColumn.Date];
                    DateTime date = DateTime.Parse(original_date);
                    reversedDate = new DateTime(date.Year, date.Day, date.Month); // we have swapped the day with the month
                }
                catch
                {
                    continue;
                }

                // Now update the actual database with the new date/time values stored in the temporary table
                Dictionary<string, object> columnNameAndValue = new Dictionary<string, object>();                  // Update the date 
                columnNameAndValue.Add(Constants.DatabaseColumn.Date, DateTimeHandler.StandardDateString(reversedDate));
                long id = (long)this.ImageDataTable.Rows[i][Constants.Database.ID];
                string where = Constants.Database.ID + " = " + id.ToString();                             // on this paticular row with the given id
                updateQuery.Add(columnNameAndValue, where);
            }

            bool result;
            string command_executed;
            this.database.UpdateWhereBeginEnd(Constants.Database.ImageDataTable, updateQuery, out result, out command_executed);
        }

        public void TrimImageAndTemplateTableWhitespace()
        {
            bool result;
            string command_executed;
            List<string> column_names = new List<string>();
            for (int i = 0; i < this.TemplateTable.Rows.Count; i++)
            {
                DataRow row = this.TemplateTable.Rows[i];
                column_names.Add((string)row[Constants.Control.DataLabel]);
            }
            this.database.UpdateColumnTrimWhiteSpace(Constants.Database.ImageDataTable, column_names, out result, out command_executed);

            this.UpdateID(1, Constants.DatabaseColumn.WhiteSpaceTrimmed, true.ToString(), Constants.Database.ImageSetTable);
        }

        public void DeleteImage(long id)
        {
            bool result = true;
            string command_executed = String.Empty;
            this.database.DeleteFromTable(Constants.Database.ImageDataTable, "ID = " + id.ToString(), out result, out command_executed);
        }

        /// <summary> 
        /// Go to the first image, returning true if we can otherwise false
        /// </summary>
        public bool TryMoveToFirstImage()
        {
            return this.TryMoveToImage(0);
        }

        /// <summary> 
        /// Go to the next image, returning false if we can't (e.g., if we are at the end) 
        /// </summary>
        public bool TryMoveToNextImage()
        {
            return this.TryMoveToImage(this.CurrentImageRow + 1);
        }

        /// <summary>
        /// Go to the previous image, returning true if we can otherwise false (e.g., if we are at the beginning)
        /// </summary>
        public bool TryMoveToPreviousImage()
        {
            return this.TryMoveToImage(this.CurrentImageRow - 1);
        }

        /// <summary>
        /// Go to a particular image, returning true if we can otherwise false (e.g., if the index is out of range)
        /// Remember, that we are zero based, so (for example) and index of 5 will go to the sixth image
        /// </summary>
        public bool TryMoveToImage(int imageRowIndex)
        {
            if (this.IsImageRowInRange(imageRowIndex))
            {
                this.CurrentImageRow = imageRowIndex;
                this.CurrentImage = new ImageProperties(this.ImageDataTable.Rows[this.CurrentImageRow]);
                return true;
            }
            else
            {
                return false;
            }
        }

        // Given a row index, return the ID
        public int GetImageID(int rowIndex)
        {
            if (!this.IsImageRowInRange(rowIndex))
            {
                return -1;
            }
            try
            {
                Int64 id = (Int64)this.ImageDataTable.Rows[rowIndex][Constants.Database.ID];
                return Convert.ToInt32(id);
            }
            catch
            {
                return -1;
            }
        }

        public string GetCurrentImageValue(string dataLabel)
        {
            return this.GetImageValue(dataLabel, this.CurrentImageRow);
        }

        public string GetImageValue(string dataLabel, int row_index)
        {
            if (this.IsImageRowInRange(row_index))
            {
                return (string)this.ImageDataTable.Rows[row_index][dataLabel];
            }
            else
            {
                return String.Empty;
            }
        }

        /// <summary>A convenience routine for checking to see if the image in the given row is displayable (i.e., not corrupted or missing)</summary>
        public bool IsImageDisplayable(int row)
        {
            string result = this.GetImageValue(this.DataLabelFromControlType[Constants.DatabaseColumn.ImageQuality], row);
            if (result.Equals(Constants.ImageQuality.Corrupted) || result.Equals(Constants.ImageQuality.Missing))
            {
                return false;
            }
            return true;
        }

        /// <summary>A convenience routine for checking to see if the image in the given row is corrupted</summary>
        public bool IsImageCorrupt(int row)
        {
            string result = this.GetImageValue(this.DataLabelFromControlType[Constants.DatabaseColumn.ImageQuality], row);
            return result.Equals(Constants.ImageQuality.Corrupted) ? true : false;
        }

        // Find the next displayable image after the provided row in the current image set
        public int FindNextDisplayableImage(int initialRow)
        {
            for (int row = initialRow; row < this.ImageCount; row++)
            {
                if (this.IsImageDisplayable(row))
                {
                    return row;
                }
            }
            return -1;
        }

        public DataTable GetControlsSortedByControlOrder()
        {
            DataTable tempdt = this.TemplateTable.Copy();
            DataView dv = tempdt.DefaultView;
            dv.Sort = Constants.Database.ControlOrder + " ASC";
            return dv.ToTable();
        }

        public bool IsControlCopyable(string dataLabel)
        {
            long id = this.GetControlIDFromTemplateTable(dataLabel);
            DataRow foundRow = this.TemplateTable.Rows.Find(id);
            bool is_copyable;
            return bool.TryParse((string)foundRow[Constants.Control.Copyable], out is_copyable) ? is_copyable : false;
        }

        public string GetControlDefaultValue(string dataLabel)
        {
            long id = this.GetControlIDFromTemplateTable(dataLabel);
            DataRow foundRow = this.TemplateTable.Rows.Find(id);
            return (string)foundRow[Constants.Control.DefaultValue];
        }

        public void UpdateCurrentImage(string dataLabel, string value)
        {
            if (this.IsImageRowInRange(this.CurrentImageRow))
            {
                this.ImageDataTable.Rows[this.CurrentImageRow][dataLabel] = value;
                this.UpdateImage(this.CurrentImage.ID, dataLabel, value);
            }
        }

        // Check if the White Space column exists in the ImageSetTable
        public bool DoesWhiteSpaceColumnExist()
        {
            return this.database.IsColumnInTable(Constants.Database.ImageSetTable, Constants.DatabaseColumn.WhiteSpaceTrimmed);
        }

        // Create the White Space column exists in the ImageSetTable
        public bool CreateWhiteSpaceColumn()
        {
            if (this.database.CreateColumnInTable(Constants.Database.ImageSetTable, Constants.DatabaseColumn.WhiteSpaceTrimmed))
            {
                // Set the value to true 

                return true;
            }
            return false;
        }

        public int GetImageSetRowIndex()
        {
            string result = this.ImageSetGetValue(Constants.DatabaseColumn.Row);
            return Convert.ToInt32(result);
        }

        /// <summary>
        /// Get the metatag counter list associated with all counters representing the current row
        /// It will have a MetaTagCounter for each control, even if there may be no metatags in it
        /// </summary>
        /// <returns>list of counters</returns>
        public List<MetaTagCounter> GetMetaTagCounters()
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

            // Get the current row number of the id in the marker table
            int row_num = this.FindMarkerRow(this.CurrentImage.ID);
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

                points = this.ParseCoordinate(value); // parse the contents into a set of points
                foreach (Point point in points)
                {
                    mtagCounter.CreateMetaTag(point, datalabel);  // add the metatage to the list
                }
                metaTagCounters.Add(mtagCounter);   // and add that metaTag counter to our lists of metaTag counters
            }
            return metaTagCounters;
        }

        public void SetImageSetLog(string logEntry)
        {
            this.UpdateID(1, Constants.DatabaseColumn.Log, logEntry, Constants.Database.ImageSetTable);
        }

        /// <summary>
        /// Set the list of marker points on the current row in the marker table. 
        /// </summary>
        /// <param name="dataLabel">data label</param>
        /// <param name="pointList">A list of points in the form x,y|x,y|x,y</param>
        public void SetMarkerPoints(string dataLabel, string pointList)
        {
            // Find the current row number
            int row_num = this.FindMarkerRow(this.CurrentImage.ID);
            if (row_num < 0)
            {
                return;
            }

            // Update the database and datatable
            this.MarkerTable.Rows[row_num][dataLabel] = pointList;
            this.UpdateID(this.CurrentImage.ID, dataLabel, pointList, Constants.Database.MarkersTable);  // Update the database
        }

        public void UpdateImageSetFilter(ImageQualityFilter filter)
        {
            this.UpdateID(1, Constants.DatabaseColumn.Filter, ((int)filter).ToString(), Constants.Database.ImageSetTable);
        }

        public void UpdateImageSetRowIndex(int row)
        {
            this.UpdateID(1, Constants.DatabaseColumn.Row, row.ToString(), Constants.Database.ImageSetTable);
        }

        public void UpdateMagnifierEnabled(bool enabled)
        {
            this.UpdateID(1, Constants.DatabaseColumn.Magnifier, enabled.ToString(), Constants.Database.ImageSetTable);
        }

        // The id is the row to update, the datalabels are the labels of each control to updata, 
        // and the markers are the respective point lists for each of those labels
        public void UpdateMarkers(List<ColumnTupleListWhere> markersToUpdate)
        {
            // update markers in database
            bool result;
            string command_executed;
            this.database.UpdateWhereBeginEnd(Constants.Database.MarkersTable, markersToUpdate, out result, out command_executed);

            // update markers in marker data table
            char[] quote = { '\'' };
            foreach (ColumnTupleListWhere marker in markersToUpdate)
            {
                List<ColumnTuple> ctl = marker.ListPair;
                // We have to parse the id, as its in the form of Id=5 (for example)
                string sid = marker.Where.Substring(marker.Where.IndexOf("=") + 1);
                sid = sid.Trim(quote);

                int id;
                if (!Int32.TryParse(sid, out id))
                {
                    Debug.Print("Can't GetThe Id");
                    break;
                }
                foreach (ColumnTuple ct in ctl)
                {
                    if (!ct.ColumnValue.Equals(String.Empty))
                    {
                        // TODO: Saul  .Rows is being indexed by ID rather than row index; is this correct?
                        this.MarkerTable.Rows[id - 1][ct.ColumnName] = ct.ColumnValue;
                    }
                }
            }
        }

        /// <summary>
        /// Given an id, find the row number that matches it in the Marker Table
        /// </summary>
        /// <returns>-1 on failure</returns>
        private int FindMarkerRow(long imageID)
        {
            for (int row_number = 0; row_number < this.MarkerTable.Rows.Count; row_number++)
            {
                string str = this.MarkerTable.Rows[row_number][Constants.Database.ID].ToString();
                int this_id;
                if (Int32.TryParse(str, out this_id) == false)
                {
                    return -1;
                }

                if (this_id == imageID)
                {
                    return row_number;
                }
            }
            return -1;
        }

        private List<Point> ParseCoordinate(string value)
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

        /// <summary>Gets the complete path to the image database.</summary>
        private string GetDatabaseFilePath()
        {
            return Path.Combine(this.FolderPath, this.FileName);
        }

        /// <summary>Given a data label, get the id of the corresponding data entry control</summary>
        private long GetControlIDFromTemplateTable(string dataLabel)
        {
            for (int i = 0; i < this.TemplateTable.Rows.Count; i++)
            {
                if (dataLabel.Equals(this.TemplateTable.Rows[i][Constants.Control.DataLabel]))
                {
                    return (long)this.TemplateTable.Rows[i][Constants.Database.ID];
                }
            }
            return -1;
        }

        /// <summary>Given a key, return its value</summary>
        private string ImageSetGetValue(string dataLabel)
        {
            // Get the single row
            string query = "Select * From " + Constants.Database.ImageSetTable + " WHERE " + Constants.Database.ID + " = 1";
            DataTable imagesetTable;
            if (this.database.TryGetDataTableFromSelect(query, out imagesetTable) == false)
            {
                return String.Empty;
            }
            return (string)imagesetTable.Rows[0][dataLabel];
        }

        // <summary>Given a key, value pair, update its value</summary>
        private void ImageSetSetValue(string key, string value)
        {
            this.UpdateID(1, Constants.DatabaseColumn.Log, value, Constants.Database.ImageSetTable);
        }

        private bool IsImageRowInRange(int imageRowIndex)
        {
            return (imageRowIndex >= 0) && (imageRowIndex < this.ImageCount) ? true : false;
        }
    }
}
