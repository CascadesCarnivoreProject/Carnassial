using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;

namespace Timelapse.Database
{
    /// <summary>
    /// Timelapse Template Database.
    /// </summary>
    public class TemplateDatabase : IDisposable
    {
        private bool disposed;

        // default constructor
        public TemplateDatabase(string filePath)
            : this(filePath, null)
        {
        }

        // optional clone constructor
        protected TemplateDatabase(string filePath, TemplateDatabase other)
        {
            this.disposed = false;

            // check for an existing database before instantiating the SQL wrapper as its instantiation creates the file
            bool populateDatabase = !File.Exists(filePath);

            // open or create database
            this.Database = new SQLiteWrapper(filePath);
            this.FilePath = filePath;

            if (populateDatabase)
            {
                // initialize the database if it's newly created
                this.OnDatabaseCreated(other);
            }
            else
            {
                // if it's an existing database check if it needs updating to current structure and load data tables
                this.OnExistingDatabaseOpened(other);
            }
        }

        protected SQLiteWrapper Database { get; set; }

        /// <summary>Gets the file name of the image database on disk.</summary>
        public string FilePath { get; private set; }

        /// <summary>
        /// Gets the template table
        /// </summary>
        public DataTable TemplateTable { get; private set; }

        public DataRow AddControl(string controlType)
        {
            // create the row for the new control in the data table
            DataRow newRow = this.TemplateTable.NewRow();
            string dataLabelPrefix;
            switch (controlType)
            {
                case Constants.Control.Counter:
                    dataLabelPrefix = Constants.Control.Counter;
                    newRow[Constants.Control.DefaultValue] = Constants.ControlDefault.CounterValue;
                    newRow[Constants.Control.Type] = Constants.Control.Counter;
                    newRow[Constants.Control.TextBoxWidth] = Constants.ControlDefault.CounterWidth;
                    newRow[Constants.Control.Copyable] = false;
                    newRow[Constants.Control.Visible] = true;
                    newRow[Constants.Control.Tooltip] = Constants.ControlDefault.CounterTooltip;
                    break;
                case Constants.Control.Note:
                    dataLabelPrefix = Constants.Control.Note;
                    newRow[Constants.Control.DefaultValue] = Constants.ControlDefault.NoteValue;
                    newRow[Constants.Control.Type] = Constants.Control.Note;
                    newRow[Constants.Control.TextBoxWidth] = Constants.ControlDefault.NoteWidth;
                    newRow[Constants.Control.Copyable] = true;
                    newRow[Constants.Control.Visible] = true;
                    newRow[Constants.Control.Tooltip] = Constants.ControlDefault.NoteTooltip;
                    break;
                case Constants.Control.FixedChoice:
                    dataLabelPrefix = Constants.Control.Choice;
                    newRow[Constants.Control.DefaultValue] = Constants.ControlDefault.FixedChoiceValue;
                    newRow[Constants.Control.Type] = Constants.Control.FixedChoice;
                    newRow[Constants.Control.TextBoxWidth] = Constants.ControlDefault.FixedChoiceWidth;
                    newRow[Constants.Control.Copyable] = true;
                    newRow[Constants.Control.Visible] = true;
                    newRow[Constants.Control.Tooltip] = Constants.ControlDefault.FixedChoiceTooltip;
                    break;
                case Constants.Control.Flag:
                    dataLabelPrefix = Constants.Control.Flag;
                    newRow[Constants.Control.DefaultValue] = Constants.ControlDefault.FlagValue;
                    newRow[Constants.Control.Type] = Constants.Control.Flag;
                    newRow[Constants.Control.TextBoxWidth] = Constants.ControlDefault.FlagWidth;
                    newRow[Constants.Control.Copyable] = true;
                    newRow[Constants.Control.Visible] = true;
                    newRow[Constants.Control.Tooltip] = Constants.ControlDefault.FlagTooltip;
                    break;
                default:
                    throw new NotSupportedException(String.Format("Unhandled control type {0}."));
            }

            string dataLabel = this.GetNextUniqueDataLabel(dataLabelPrefix);
            newRow[Constants.Control.ControlOrder] = this.TemplateTable.Rows.Count + 1;
            newRow[Constants.Control.DataLabel] = dataLabel;
            newRow[Constants.Control.Label] = dataLabel;
            newRow[Constants.Control.List] = Constants.ControlDefault.ListValue;
            newRow[Constants.Control.SpreadsheetOrder] = this.TemplateTable.Rows.Count + 1;

            // add the new control to the database
            List<ColumnTuple> controlInsert = new List<ColumnTuple>();
            for (int column = 0; column < this.TemplateTable.Columns.Count; column++)
            {
                controlInsert.Add(new ColumnTuple(this.TemplateTable.Columns[column].ColumnName, newRow[column].ToString()));
            }
            List<List<ColumnTuple>> controlInsertWrapper = new List<List<ColumnTuple>>() { controlInsert };
            this.Database.Insert(Constants.Database.TemplateTable, controlInsertWrapper);

            // update the in memory table to reflect current database content
            // could just add the new row to the table but this is done in case a bug results in the insert lacking perfect fidelity
            this.TemplateTable = this.GetControlsSortedByControlOrder();
            return this.TemplateTable.Rows[this.TemplateTable.Rows.Count - 1];
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        public DataTable GetControlsSortedByControlOrder()
        {
            return this.Database.GetDataTableFromSelect(Constants.Sql.SelectStarFrom + Constants.Database.TemplateTable + " ORDER BY  " + Constants.Control.ControlOrder);
        }

        public bool IsControlCopyable(string dataLabel)
        {
            long id = this.GetControlIDFromTemplateTable(dataLabel);
            DataRow foundRow = this.TemplateTable.Rows.Find(id);
            bool is_copyable;
            return bool.TryParse((string)foundRow[Constants.Control.Copyable], out is_copyable) ? is_copyable : false;
        }

        public void RemoveControl(DataRow controlToRemove)
        {
            // capture state
            int removedControlOrder = Convert.ToInt32((Int64)controlToRemove[Constants.Control.ControlOrder]);
            int removedSpreadsheetOrder = Convert.ToInt32((Int64)controlToRemove[Constants.Control.SpreadsheetOrder]);

            // drop the control from the database and data table
            string where = Constants.DatabaseColumn.ID + " = " + controlToRemove[Constants.DatabaseColumn.ID];
            this.Database.DeleteRows(Constants.Database.TemplateTable, where);
            this.TemplateTable.Rows.Remove(controlToRemove);

            // regenerate counter and spreadsheet orders; if they're greater than the one removed, decrement
            List<ColumnTuplesWithWhere> controlUpdates = new List<ColumnTuplesWithWhere>();
            for (int rowIndex = 0; rowIndex < this.TemplateTable.Rows.Count; rowIndex++)
            {
                DataRow row = this.TemplateTable.Rows[rowIndex];
                long controlOrder = (long)row[Constants.Control.ControlOrder];
                long spreadsheetOrder = (long)row[Constants.Control.SpreadsheetOrder];

                if (controlOrder > removedControlOrder)
                {
                    List<ColumnTuple> controlUpdate = new List<ColumnTuple>();
                    controlUpdate.Add(new ColumnTuple(Constants.Control.ControlOrder, controlOrder - 1));
                    row[Constants.Control.ControlOrder] = controlOrder - 1;
                    where = Constants.DatabaseColumn.ID + " = " + row[Constants.DatabaseColumn.ID];
                    controlUpdates.Add(new ColumnTuplesWithWhere(controlUpdate, where));
                }

                if (spreadsheetOrder > removedSpreadsheetOrder)
                {
                    List<ColumnTuple> controlUpdate = new List<ColumnTuple>();
                    controlUpdate.Add(new ColumnTuple(Constants.Control.SpreadsheetOrder, spreadsheetOrder - 1));
                    row[Constants.Control.SpreadsheetOrder] = spreadsheetOrder - 1;
                    where = Constants.DatabaseColumn.ID + " = " + row[Constants.DatabaseColumn.ID];
                    controlUpdates.Add(new ColumnTuplesWithWhere(controlUpdate, where));
                }
            }
            this.Database.Update(Constants.Database.TemplateTable, controlUpdates);

            // update the in memory table to reflect current database content
            // should not be necessary but this is done to mitigate divergence in case a bug results in the delete lacking perfect fidelity
            this.TemplateTable = this.GetControlsSortedByControlOrder();
        }

        public void SyncControlToDatabase(DataRow control)
        {
            List<ColumnTuple> columns = this.GetTuples(this.TemplateTable, control);
            ColumnTuplesWithWhere updateQuery = new ColumnTuplesWithWhere(columns, Constants.DatabaseColumn.ID + " = " + control[Constants.DatabaseColumn.ID]);
            this.Database.Update(Constants.Database.TemplateTable, updateQuery);

            // it's possible the passed data row isn't attached to TemplateTable, so refresh the table just in case
            this.TemplateTable = this.GetControlsSortedByControlOrder();
        }

        public void SyncTemplateTableToDatabase()
        {
            this.SyncTemplateTableToDatabase(this.TemplateTable);
        }

        public void SyncTemplateTableToDatabase(DataTable newTable)
        {
            // clear the existing table in the database and add the new values
            this.Database.DeleteRows(Constants.Database.TemplateTable, null);
            this.Insert(Constants.Database.TemplateTable, newTable);

            // update the in memory table to reflect current database content
            // could just use the new table but this is done in case a bug results in the insert lacking perfect fidelity
            this.TemplateTable = this.GetControlsSortedByControlOrder();
        }

        public static bool TryOpen(string filePath, out TemplateDatabase database)
        {
            try
            {
                database = new TemplateDatabase(filePath);
                return true;
            }
            catch (Exception exception)
            {
                Debug.Assert(false, exception.ToString());

                database = null;
                return false;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                if (this.TemplateTable != null)
                {
                    this.TemplateTable.Dispose();
                }
            }

            this.disposed = true;
        }

        /// <summary>Given a data label, get the id of the corresponding data entry control</summary>
        protected long GetControlIDFromTemplateTable(string dataLabel)
        {
            for (int i = 0; i < this.TemplateTable.Rows.Count; i++)
            {
                if (dataLabel.Equals(this.TemplateTable.Rows[i][Constants.Control.DataLabel]))
                {
                    return (long)this.TemplateTable.Rows[i][Constants.DatabaseColumn.ID];
                }
            }
            return -1;
        }

        protected List<ColumnTuple> GetTuples(DataTable dataTable, DataRow row)
        {
            List<ColumnTuple> tuples = new List<ColumnTuple>();
            for (int column = 0; column < dataTable.Columns.Count; column++)
            {
                tuples.Add(new ColumnTuple(dataTable.Columns[column].ToString(), row[column].ToString()));
            }
            return tuples;
        }

        protected void Insert(string tableName, DataTable dataTable)
        {
            List<List<ColumnTuple>> insertionStatements = new List<List<ColumnTuple>>();
            for (int row = 0; row < dataTable.Rows.Count; row++)
            {
                insertionStatements.Add(this.GetTuples(dataTable, dataTable.Rows[row]));
            }

            this.Database.Insert(tableName, insertionStatements);
        }

        protected virtual void OnDatabaseCreated(TemplateDatabase other)
        {
            // create the template table
            List<ColumnTuple> templateTableColumns = new List<ColumnTuple>();
            templateTableColumns.Add(new ColumnTuple(Constants.DatabaseColumn.ID, "INTEGER primary key autoincrement"));
            templateTableColumns.Add(new ColumnTuple(Constants.Control.ControlOrder, "INTEGER"));
            templateTableColumns.Add(new ColumnTuple(Constants.Control.SpreadsheetOrder, "INTEGER"));
            templateTableColumns.Add(new ColumnTuple(Constants.Control.Type, "text"));
            templateTableColumns.Add(new ColumnTuple(Constants.Control.DefaultValue, "text"));
            templateTableColumns.Add(new ColumnTuple(Constants.Control.Label, "text"));
            templateTableColumns.Add(new ColumnTuple(Constants.Control.DataLabel, "text"));
            templateTableColumns.Add(new ColumnTuple(Constants.Control.Tooltip, "text"));
            templateTableColumns.Add(new ColumnTuple(Constants.Control.TextBoxWidth, "text"));
            templateTableColumns.Add(new ColumnTuple(Constants.Control.Copyable, "text"));
            templateTableColumns.Add(new ColumnTuple(Constants.Control.Visible, "text"));
            templateTableColumns.Add(new ColumnTuple(Constants.Control.List, "text"));
            this.Database.CreateTable(Constants.Database.TemplateTable, templateTableColumns);

            // if an existing table was passed, clone its contents into this database
            if (other != null)
            {
                this.SyncTemplateTableToDatabase(other.TemplateTable);
                return;
            }

            // no existing table to clone, so add standard controls to template table
            List<List<ColumnTuple>> standardControls = new List<List<ColumnTuple>>();
            int controlOrder = 0; // The control order, incremented by 1 for every new entry
            int spreadsheetOrder = 0; // The spreadsheet order, incremented by 1 for every new entry

            // file
            List<ColumnTuple> file = new List<ColumnTuple>();
            file.Add(new ColumnTuple(Constants.Control.ControlOrder, ++controlOrder));
            file.Add(new ColumnTuple(Constants.Control.SpreadsheetOrder, ++spreadsheetOrder));
            file.Add(new ColumnTuple(Constants.Control.Type, Constants.DatabaseColumn.File));
            file.Add(new ColumnTuple(Constants.Control.DefaultValue, Constants.ControlDefault.FileValue));
            file.Add(new ColumnTuple(Constants.Control.Label, Constants.DatabaseColumn.File));
            file.Add(new ColumnTuple(Constants.Control.DataLabel, Constants.DatabaseColumn.File));
            file.Add(new ColumnTuple(Constants.Control.Tooltip, Constants.ControlDefault.FileTooltip));
            file.Add(new ColumnTuple(Constants.Control.TextBoxWidth, Constants.ControlDefault.FileWidth));
            file.Add(new ColumnTuple(Constants.Control.Copyable, "false"));
            file.Add(new ColumnTuple(Constants.Control.Visible, "true"));
            file.Add(new ColumnTuple(Constants.Control.List, String.Empty));
            standardControls.Add(file);

            // folder
            List<ColumnTuple> folder = new List<ColumnTuple>();
            folder.Add(new ColumnTuple(Constants.Control.ControlOrder, ++controlOrder));
            folder.Add(new ColumnTuple(Constants.Control.SpreadsheetOrder, ++spreadsheetOrder));
            folder.Add(new ColumnTuple(Constants.Control.Type, Constants.DatabaseColumn.Folder));
            folder.Add(new ColumnTuple(Constants.Control.DefaultValue, Constants.ControlDefault.FolderValue));
            folder.Add(new ColumnTuple(Constants.Control.Label, Constants.DatabaseColumn.Folder));
            folder.Add(new ColumnTuple(Constants.Control.DataLabel, Constants.DatabaseColumn.Folder));
            folder.Add(new ColumnTuple(Constants.Control.Tooltip, Constants.ControlDefault.FolderTooltip));
            folder.Add(new ColumnTuple(Constants.Control.TextBoxWidth, Constants.ControlDefault.FolderWidth));
            folder.Add(new ColumnTuple(Constants.Control.Copyable, "false"));
            folder.Add(new ColumnTuple(Constants.Control.Visible, "true"));
            folder.Add(new ColumnTuple(Constants.Control.List, String.Empty));
            standardControls.Add(folder);

            // date
            List<ColumnTuple> date = new List<ColumnTuple>();
            date.Add(new ColumnTuple(Constants.Control.ControlOrder, ++controlOrder));
            date.Add(new ColumnTuple(Constants.Control.SpreadsheetOrder, ++spreadsheetOrder));
            date.Add(new ColumnTuple(Constants.Control.Type, Constants.DatabaseColumn.Date));
            date.Add(new ColumnTuple(Constants.Control.DefaultValue, Constants.ControlDefault.DateValue));
            date.Add(new ColumnTuple(Constants.Control.Label, Constants.DatabaseColumn.Date));
            date.Add(new ColumnTuple(Constants.Control.DataLabel, Constants.DatabaseColumn.Date));
            date.Add(new ColumnTuple(Constants.Control.Tooltip, Constants.ControlDefault.DateTooltip));
            date.Add(new ColumnTuple(Constants.Control.TextBoxWidth, Constants.ControlDefault.DateWidth));
            date.Add(new ColumnTuple(Constants.Control.Copyable, "false"));
            date.Add(new ColumnTuple(Constants.Control.Visible, "true"));
            date.Add(new ColumnTuple(Constants.Control.List, String.Empty));
            standardControls.Add(date);

            // time
            List<ColumnTuple> time = new List<ColumnTuple>();
            time.Add(new ColumnTuple(Constants.Control.ControlOrder, ++controlOrder));
            time.Add(new ColumnTuple(Constants.Control.SpreadsheetOrder, ++spreadsheetOrder));
            time.Add(new ColumnTuple(Constants.Control.Type, Constants.DatabaseColumn.Time));
            time.Add(new ColumnTuple(Constants.Control.DefaultValue, Constants.ControlDefault.TimeValue));
            time.Add(new ColumnTuple(Constants.Control.Label, Constants.DatabaseColumn.Time));
            time.Add(new ColumnTuple(Constants.Control.DataLabel, Constants.DatabaseColumn.Time));
            time.Add(new ColumnTuple(Constants.Control.Tooltip, Constants.ControlDefault.TimeTooltip));
            time.Add(new ColumnTuple(Constants.Control.TextBoxWidth, Constants.ControlDefault.TimeWidth));
            time.Add(new ColumnTuple(Constants.Control.Copyable, "false"));
            time.Add(new ColumnTuple(Constants.Control.Visible, "true"));
            time.Add(new ColumnTuple(Constants.Control.List, String.Empty));
            standardControls.Add(time);

            // image quality
            List<ColumnTuple> imageQuality = new List<ColumnTuple>();
            imageQuality.Add(new ColumnTuple(Constants.Control.ControlOrder, ++controlOrder));
            imageQuality.Add(new ColumnTuple(Constants.Control.SpreadsheetOrder, ++spreadsheetOrder));
            imageQuality.Add(new ColumnTuple(Constants.Control.Type, Constants.DatabaseColumn.ImageQuality));
            imageQuality.Add(new ColumnTuple(Constants.Control.DefaultValue, Constants.ControlDefault.ImageQualityValue));
            imageQuality.Add(new ColumnTuple(Constants.Control.Label, Constants.DatabaseColumn.ImageQuality));
            imageQuality.Add(new ColumnTuple(Constants.Control.DataLabel, Constants.DatabaseColumn.ImageQuality));
            imageQuality.Add(new ColumnTuple(Constants.Control.Tooltip, Constants.ControlDefault.ImageQualityTooltip));
            imageQuality.Add(new ColumnTuple(Constants.Control.TextBoxWidth, Constants.ControlDefault.ImageQualityWidth));
            imageQuality.Add(new ColumnTuple(Constants.Control.Copyable, "false"));
            imageQuality.Add(new ColumnTuple(Constants.Control.Visible, "true"));
            imageQuality.Add(new ColumnTuple(Constants.Control.List, Constants.ImageQuality.ListOfValues));
            standardControls.Add(imageQuality);

            // delete flag
            List<ColumnTuple> markForDeletion = new List<ColumnTuple>();
            markForDeletion.Add(new ColumnTuple(Constants.Control.ControlOrder, ++controlOrder));
            markForDeletion.Add(new ColumnTuple(Constants.Control.SpreadsheetOrder, ++spreadsheetOrder));
            markForDeletion.Add(new ColumnTuple(Constants.Control.Type, Constants.Control.DeleteFlag));
            markForDeletion.Add(new ColumnTuple(Constants.Control.DefaultValue, Constants.ControlDefault.FlagValue));
            markForDeletion.Add(new ColumnTuple(Constants.Control.Label, Constants.Control.DeleteFlag));
            markForDeletion.Add(new ColumnTuple(Constants.Control.DataLabel, Constants.Control.DeleteFlag));
            markForDeletion.Add(new ColumnTuple(Constants.Control.Tooltip, Constants.ControlDefault.MarkForDeletionTooltip));
            markForDeletion.Add(new ColumnTuple(Constants.Control.TextBoxWidth, Constants.ControlDefault.FlagWidth));
            markForDeletion.Add(new ColumnTuple(Constants.Control.Copyable, "false"));
            markForDeletion.Add(new ColumnTuple(Constants.Control.Visible, "true"));
            markForDeletion.Add(new ColumnTuple(Constants.Control.List, String.Empty));
            standardControls.Add(markForDeletion);

            // insert standard controls into the template table
            this.Database.Insert(Constants.Database.TemplateTable, standardControls);

            // populate the in memory version of the template table
            this.TemplateTable = this.GetControlsSortedByControlOrder();
        }

        protected virtual void OnExistingDatabaseOpened(TemplateDatabase other)
        {
            this.TemplateTable = this.GetControlsSortedByControlOrder();
            this.MigrateTemplateTableIfNeeded();
        }

        private string GetNextUniqueDataLabel(string dataLabelPrefix)
        {
            // get all existing data labels, as we have to ensure that a new data label doesn't have the same name as an existing one
            List<string> dataLabels = new List<string>();
            for (int row = 0; row < this.TemplateTable.Rows.Count; row++)
            {
                dataLabels.Add((string)this.TemplateTable.Rows[row][Constants.Control.DataLabel]);
            }

            // If the data label name exists, keep incrementing the count that is appended to the end
            // of the field type until it forms a unique data label name
            int dataLabelUniqueIdentifier = 0;
            string nextDataLabel = dataLabelPrefix + dataLabelUniqueIdentifier.ToString();
            while (dataLabels.Contains(nextDataLabel))
            {
                ++dataLabelUniqueIdentifier;
                nextDataLabel = dataLabelPrefix + dataLabelUniqueIdentifier.ToString();
            }

            return nextDataLabel;
        }

        /// <summary>
        /// Load the Template Table into the data structure Data database. 
        /// Assumes that the database has already been opened. 
        /// Also verify that the Labels and Data Labels are non-empty, where it updates the TemplateTable (in the database and in memory) as needed
        /// </summary>
        private void MigrateTemplateTableIfNeeded()
        {
            // All the code below goes through the template table to see if there are any non-empty labels / data labels,
            // and if so, updates them to a reasonable value. If both are empty, it keeps track of its type and creates
            // a label called (say) Counter3 for the third counter that has no label. If there is no DataLabel value, it
            // makes it the same as the label. Ultimately, it guarantees that there will always be a (hopefully unique)
            // data label and label name. 
            // As well, the contents of the template table are loaded into memory.
            for (int rowIndex = 0; rowIndex < this.TemplateTable.Rows.Count; rowIndex++)
            {
                DataRow row = this.TemplateTable.Rows[rowIndex];

                // Get various values from each row
                string controlType = (string)row[Constants.Control.Type];
                // Not sure why, but if its an empty value it doesn't like it. Therefore we do this in a try/catch.
                string label;      // The row's label
                try
                {
                    label = (string)row[Constants.Control.Label];
                }
                catch
                {
                    label = null;
                }

                string dataLabel; // The row's data label
                try
                {
                    dataLabel = (string)row[Constants.Control.DataLabel];
                }
                catch
                {
                    dataLabel = null;
                }

                // Check if various values are empty, and if so update the row and fill the dataline with appropriate defaults
                ColumnTuplesWithWhere columnsToUpdate = new ColumnTuplesWithWhere();    // holds columns which have changed for the current control
                bool noDataLabel = String.IsNullOrWhiteSpace(dataLabel);
                if (noDataLabel && String.IsNullOrWhiteSpace(label))
                {
                    dataLabel = this.GetNextUniqueDataLabel(controlType);
                    columnsToUpdate.Columns.Add(new ColumnTuple(Constants.Control.Label, dataLabel));
                    columnsToUpdate.Columns.Add(new ColumnTuple(Constants.Control.DataLabel, dataLabel));
                    row[Constants.Control.Label] = dataLabel;
                    row[Constants.Control.DataLabel] = dataLabel;
                }
                else if (noDataLabel)
                {
                    // No data label but a label, so use the label's value as the data label
                    columnsToUpdate.Columns.Add(new ColumnTuple(Constants.Control.DataLabel, label));
                    row[Constants.Control.DataLabel] = label;
                }

                // Now add the new values to the database
                if (columnsToUpdate.Columns.Count > 0)
                {
                    long id = (long)row[Constants.DatabaseColumn.ID];
                    columnsToUpdate.SetWhere(id);
                    this.Database.Update(Constants.Database.TemplateTable, columnsToUpdate);
                }
            }
        }
    }
}
