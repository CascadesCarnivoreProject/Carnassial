using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Controls;

namespace Carnassial.Database
{
    /// <summary>
    /// Carnassial Template Database.
    /// </summary>
    public class TemplateDatabase : IDisposable
    {
        private bool disposed;
        private DataGrid editorDataGrid;
        private DateTime mostRecentBackup;
        private DataRowChangeEventHandler onTemplateTableRowChanged;

        public DataTableBackedList<ControlRow> Controls { get; private set; }

        protected SQLiteWrapper Database { get; set; }

        /// <summary>Gets the path of the database on disk.</summary>
        public string FilePath { get; private set; }

        protected TemplateDatabase(string filePath)
        {
            this.disposed = false;
            this.mostRecentBackup = FileBackup.GetMostRecentBackup(filePath);

            // open or create database
            this.Database = new SQLiteWrapper(filePath);
            this.FilePath = filePath;
        }

        public void BindToEditorDataGrid(DataGrid dataGrid, DataRowChangeEventHandler onRowChanged)
        {
            this.editorDataGrid = dataGrid;
            this.onTemplateTableRowChanged = onRowChanged;
            this.GetControlsSortedByControlOrder();
        }

        protected void CreateBackupIfNeeded()
        {
            if (DateTime.UtcNow - this.mostRecentBackup < Constant.File.BackupInterval)
            {
                // not due for a new backup yet
                return;
            }

            FileBackup.TryCreateBackup(this.FilePath);
            this.mostRecentBackup = DateTime.UtcNow;
        }

        public static TemplateDatabase CreateOrOpen(string filePath)
        {
            // check for an existing database before instantiating the databse as SQL wrapper instantiation creates the database file
            bool populateDatabase = !File.Exists(filePath);

            TemplateDatabase templateDatabase = new TemplateDatabase(filePath);
            if (populateDatabase)
            {
                // initialize the database if it's newly created
                templateDatabase.OnDatabaseCreated(null);
            }
            else
            {
                // if it's an existing database check if it needs updating to current structure and load data tables
                templateDatabase.OnExistingDatabaseOpened(null);
            }
            return templateDatabase;
        }

        public ControlRow AddUserDefinedControl(string controlType)
        {
            this.CreateBackupIfNeeded();

            // create the row for the new control in the data table
            ControlRow newControl = this.Controls.NewRow();
            string dataLabelPrefix;
            switch (controlType)
            {
                case Constant.Control.Counter:
                    dataLabelPrefix = Constant.Control.Counter;
                    newControl.DefaultValue = Constant.ControlDefault.CounterValue;
                    newControl.Type = Constant.Control.Counter;
                    newControl.Width = Constant.ControlDefault.CounterWidth;
                    newControl.Copyable = false;
                    newControl.Visible = true;
                    newControl.Tooltip = Constant.ControlDefault.CounterTooltip;
                    newControl.DataLabel = this.GetNextUniqueDataLabel(dataLabelPrefix);
                    newControl.Label = newControl.DataLabel;
                    break;
                case Constant.Control.Note:
                    dataLabelPrefix = Constant.Control.Note;
                    newControl.DefaultValue = Constant.ControlDefault.Value;
                    newControl.Type = Constant.Control.Note;
                    newControl.Width = Constant.ControlDefault.NoteWidth;
                    newControl.Copyable = true;
                    newControl.Visible = true;
                    newControl.Tooltip = Constant.ControlDefault.NoteTooltip;
                    newControl.DataLabel = this.GetNextUniqueDataLabel(dataLabelPrefix);
                    newControl.Label = newControl.DataLabel;
                    break;
                case Constant.Control.FixedChoice:
                    dataLabelPrefix = Constant.Control.Choice;
                    newControl.DefaultValue = Constant.ControlDefault.Value;
                    newControl.Type = Constant.Control.FixedChoice;
                    newControl.Width = Constant.ControlDefault.FixedChoiceWidth;
                    newControl.Copyable = true;
                    newControl.Visible = true;
                    newControl.Tooltip = Constant.ControlDefault.FixedChoiceTooltip;
                    newControl.DataLabel = this.GetNextUniqueDataLabel(dataLabelPrefix);
                    newControl.Label = newControl.DataLabel;
                    break;
                case Constant.Control.Flag:
                    dataLabelPrefix = Constant.Control.Flag;
                    newControl.DefaultValue = Constant.ControlDefault.FlagValue;
                    newControl.Type = Constant.Control.Flag;
                    newControl.Width = Constant.ControlDefault.FlagWidth;
                    newControl.Copyable = true;
                    newControl.Visible = true;
                    newControl.Tooltip = Constant.ControlDefault.FlagTooltip;
                    newControl.DataLabel = this.GetNextUniqueDataLabel(dataLabelPrefix);
                    newControl.Label = newControl.DataLabel;
                    break;
                default:
                    throw new NotSupportedException(String.Format("Unhandled control type {0}.", controlType));
            }
            newControl.ControlOrder = this.GetOrderForNewControl();
            newControl.List = Constant.ControlDefault.Value;
            newControl.SpreadsheetOrder = newControl.ControlOrder;

            // add the new control to the database
            List<List<ColumnTuple>> controlInsertWrapper = new List<List<ColumnTuple>>() { newControl.GetColumnTuples().Columns };
            this.Database.Insert(Constant.DatabaseTable.Controls, controlInsertWrapper);

            // update the in memory table to reflect current database content
            // could just add the new row to the table but this is done in case a bug results in the insert lacking perfect fidelity
            this.GetControlsSortedByControlOrder();
            return this.Controls[this.Controls.RowCount - 1];
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void GetControlsSortedByControlOrder()
        {
            DataTable templateTable = this.Database.GetDataTableFromSelect("SELECT * FROM " + Constant.DatabaseTable.Controls + " ORDER BY  " + Constant.Control.ControlOrder);
            this.Controls = new DataTableBackedList<ControlRow>(templateTable, (DataRow row) => { return new ControlRow(row); });
            this.Controls.BindDataGrid(this.editorDataGrid, this.onTemplateTableRowChanged);
        }

        public List<string> GetDataLabelsExceptIDInSpreadsheetOrder()
        {
            List<string> dataLabels = new List<string>();
            IEnumerable<ControlRow> controlsInSpreadsheetOrder = this.Controls.OrderBy(control => control.SpreadsheetOrder);
            foreach (ControlRow control in controlsInSpreadsheetOrder)
            {
                string dataLabel = control.DataLabel;
                if (dataLabel == String.Empty)
                {
                    dataLabel = control.Label;
                }
                Debug.Assert(String.IsNullOrWhiteSpace(dataLabel) == false, String.Format("Encountered empty data label and label at ID {0} in template table.", control.ID));

                // get a list of datalabels so we can add columns in the order that matches the current template table order
                if (Constant.DatabaseColumn.ID != dataLabel)
                {
                    dataLabels.Add(dataLabel);
                }
            }
            return dataLabels;
        }

        public bool IsControlCopyable(string dataLabel)
        {
            long id = this.GetControlID(dataLabel);
            ControlRow control = this.Controls.Find(id);
            return control.Copyable;
        }

        public void RemoveUserDefinedControl(ControlRow controlToRemove)
        {
            this.CreateBackupIfNeeded();

            string controlType = controlToRemove.Type;
            if (Constant.Control.StandardTypes.Contains(controlType))
            {
                throw new NotSupportedException(String.Format("Standard control of type {0} cannot be removed.", controlType));
            }

            // capture state
            long removedControlOrder = controlToRemove.ControlOrder;
            long removedSpreadsheetOrder = controlToRemove.SpreadsheetOrder;

            // drop the control from the database and data table
            string where = Constant.DatabaseColumn.ID + " = " + controlToRemove.ID;
            this.Database.DeleteRows(Constant.DatabaseTable.Controls, where);
            this.GetControlsSortedByControlOrder();

            // regenerate counter and spreadsheet orders; if they're greater than the one removed, decrement
            List<ColumnTuplesWithWhere> controlUpdates = new List<ColumnTuplesWithWhere>();
            foreach (ControlRow control in this.Controls)
            {
                long controlOrder = control.ControlOrder;
                long spreadsheetOrder = control.SpreadsheetOrder;

                if (controlOrder > removedControlOrder)
                {
                    List<ColumnTuple> controlUpdate = new List<ColumnTuple>();
                    controlUpdate.Add(new ColumnTuple(Constant.Control.ControlOrder, controlOrder - 1));
                    control.ControlOrder = controlOrder - 1;
                    controlUpdates.Add(new ColumnTuplesWithWhere(controlUpdate, control.ID));
                }

                if (spreadsheetOrder > removedSpreadsheetOrder)
                {
                    List<ColumnTuple> controlUpdate = new List<ColumnTuple>();
                    controlUpdate.Add(new ColumnTuple(Constant.Control.SpreadsheetOrder, spreadsheetOrder - 1));
                    control.SpreadsheetOrder = spreadsheetOrder - 1;
                    controlUpdates.Add(new ColumnTuplesWithWhere(controlUpdate, control.ID));
                }
            }
            this.Database.Update(Constant.DatabaseTable.Controls, controlUpdates);

            // update the in memory table to reflect current database content
            // should not be necessary but this is done to mitigate divergence in case a bug results in the delete lacking perfect fidelity
            this.GetControlsSortedByControlOrder();
        }

        /// <summary>
        /// Update the database row for the control to the current content of the template table.  The caller is responsible for ensuring the data row wrapped
        /// by the ControlRow object is present in both the data table and database versions of the template table.
        /// </summary>
        public void SyncControlToDatabase(ControlRow control)
        {
            this.CreateBackupIfNeeded();
            this.Database.Update(Constant.DatabaseTable.Controls, control.GetColumnTuples());
        }

        private void SyncTemplateTableToDatabase()
        {
            this.SyncTemplateTableToDatabase(this.Controls);
        }

        private void SyncTemplateTableToDatabase(DataTableBackedList<ControlRow> newTable)
        {
            this.CreateBackupIfNeeded();

            // clear the existing table in the database and add the new values
            this.Database.DeleteRows(Constant.DatabaseTable.Controls, null);

            List<List<ColumnTuple>> newTableTuples = new List<List<ColumnTuple>>();
            foreach (ControlRow control in newTable)
            {
                newTableTuples.Add(control.GetColumnTuples().Columns);
            }
            this.Database.Insert(Constant.DatabaseTable.Controls, newTableTuples);

            // update the in memory table to reflect current database content
            // could just use the new table but this is done in case a bug results in the insert lacking perfect fidelity
            this.GetControlsSortedByControlOrder();
        }

        public static bool TryCreateOrOpen(string filePath, out TemplateDatabase database)
        {
            try
            {
                database = TemplateDatabase.CreateOrOpen(filePath);
                return true;
            }
            catch (Exception exception)
            {
                Debug.Fail(exception.ToString());
                database = null;
                return false;
            }
        }

        public void UpdateDisplayOrder(string orderColumnName, Dictionary<string, long> newOrderByDataLabel)
        {
            // argument validation
            if (orderColumnName != Constant.Control.ControlOrder && orderColumnName != Constant.Control.SpreadsheetOrder)
            {
                throw new ArgumentOutOfRangeException("column", String.Format("'{0}' is not a control order column.  Only '{1}' and '{2}' are order columns.", orderColumnName, Constant.Control.ControlOrder, Constant.Control.SpreadsheetOrder));
            }

            if (newOrderByDataLabel.Count != this.Controls.RowCount)
            {
                throw new NotSupportedException(String.Format("Partial order updates are not supported.  New ordering for {0} controls was passed but {1} controls are present for '{2}'.", newOrderByDataLabel.Count, this.Controls.RowCount, orderColumnName));
            }

            List<long> uniqueOrderValues = newOrderByDataLabel.Values.Distinct().ToList();
            if (uniqueOrderValues.Count != newOrderByDataLabel.Count)
            {
                throw new ArgumentException("newOrderByDataLabel", String.Format("Each control must have a unique value for its order.  {0} duplicate values were passed for '{1}'.", newOrderByDataLabel.Count - uniqueOrderValues.Count, orderColumnName));
            }

            uniqueOrderValues.Sort();
            for (int control = 0; control < uniqueOrderValues.Count; ++control)
            {
                int expectedOrder = control + 1;
                if (uniqueOrderValues[control] != expectedOrder)
                {
                    throw new ArgumentOutOfRangeException("newOrderByDataLabel", String.Format("Control order must be a ones based count.  An order of {0} was passed instead of the expected order {1} for '{2}'.", uniqueOrderValues[0], expectedOrder, orderColumnName));
                }
            }

            // update in memory table with new order
            foreach (ControlRow control in this.Controls)
            {
                string dataLabel = control.DataLabel;
                long newOrder = newOrderByDataLabel[dataLabel];
                switch (orderColumnName)
                {
                    case Constant.Control.ControlOrder:
                        control.ControlOrder = newOrder;
                        break;
                    case Constant.Control.SpreadsheetOrder:
                        control.SpreadsheetOrder = newOrder;
                        break;
                    default:
                        throw new NotSupportedException(String.Format("Unhandled column '{0}'.", orderColumnName));
                }
            }

            // sync new order to database
            this.SyncTemplateTableToDatabase();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                if (this.Controls != null)
                {
                    this.Controls.Dispose();
                }
            }

            this.disposed = true;
        }

        /// <summary>Given a data label, get the corresponding data entry control</summary>
        public ControlRow FindControl(string dataLabel)
        {
            foreach (ControlRow control in this.Controls)
            {
                if (dataLabel.Equals(control.DataLabel))
                {
                    return control;
                }
            }

            Debug.Fail(String.Format("Control for data label '{0}' not found.", dataLabel));
            return null;
        }

        /// <summary>Given a data label, get the id of the corresponding data entry control</summary>
        protected long GetControlID(string dataLabel)
        {
            ControlRow control = this.FindControl(dataLabel);
            if (control == null)
            {
                return -1;
            }
            return control.ID;
        }

        protected virtual void OnDatabaseCreated(TemplateDatabase other)
        {
            // create the template table
            List<ColumnDefinition> templateTableColumns = new List<ColumnDefinition>();
            templateTableColumns.Add(new ColumnDefinition(Constant.DatabaseColumn.ID, Constant.Sql.CreationStringPrimaryKey));
            templateTableColumns.Add(new ColumnDefinition(Constant.Control.ControlOrder, Constant.Sql.Integer));
            templateTableColumns.Add(new ColumnDefinition(Constant.Control.SpreadsheetOrder, Constant.Sql.Integer));
            templateTableColumns.Add(new ColumnDefinition(Constant.Control.Type, Constant.Sql.Text));
            templateTableColumns.Add(new ColumnDefinition(Constant.Control.DefaultValue, Constant.Sql.Text));
            templateTableColumns.Add(new ColumnDefinition(Constant.Control.Label, Constant.Sql.Text));
            templateTableColumns.Add(new ColumnDefinition(Constant.Control.DataLabel, Constant.Sql.Text));
            templateTableColumns.Add(new ColumnDefinition(Constant.Control.Tooltip, Constant.Sql.Text));
            templateTableColumns.Add(new ColumnDefinition(Constant.Control.Width, Constant.Sql.Integer));
            templateTableColumns.Add(new ColumnDefinition(Constant.Control.Copyable, Constant.Sql.Text));
            templateTableColumns.Add(new ColumnDefinition(Constant.Control.Visible, Constant.Sql.Text));
            templateTableColumns.Add(new ColumnDefinition(Constant.Control.List, Constant.Sql.Text));
            this.Database.CreateTable(Constant.DatabaseTable.Controls, templateTableColumns);

            // if an existing table was passed, clone its contents into this database
            if (other != null)
            {
                this.SyncTemplateTableToDatabase(other.Controls);
                return;
            }

            // no existing table to clone, so add standard controls to template table
            List<List<ColumnTuple>> standardControls = new List<List<ColumnTuple>>();
            long controlOrder = 0; // The control order, a one based count incremented for every new entry
            long spreadsheetOrder = 0; // The spreadsheet order, a one based count incremented for every new entry

            // file
            List<ColumnTuple> file = new List<ColumnTuple>();
            file.Add(new ColumnTuple(Constant.Control.ControlOrder, ++controlOrder));
            file.Add(new ColumnTuple(Constant.Control.SpreadsheetOrder, ++spreadsheetOrder));
            file.Add(new ColumnTuple(Constant.Control.Type, Constant.DatabaseColumn.File));
            file.Add(new ColumnTuple(Constant.Control.DefaultValue, Constant.ControlDefault.Value));
            file.Add(new ColumnTuple(Constant.Control.Label, Constant.DatabaseColumn.File));
            file.Add(new ColumnTuple(Constant.Control.DataLabel, Constant.DatabaseColumn.File));
            file.Add(new ColumnTuple(Constant.Control.Tooltip, Constant.ControlDefault.FileTooltip));
            file.Add(new ColumnTuple(Constant.Control.Width, Constant.ControlDefault.FileWidth));
            file.Add(new ColumnTuple(Constant.Control.Copyable, false));
            file.Add(new ColumnTuple(Constant.Control.Visible, true));
            file.Add(new ColumnTuple(Constant.Control.List, Constant.ControlDefault.Value));
            standardControls.Add(file);

            // relative path
            standardControls.Add(this.GetRelativePathTuples(++controlOrder, ++spreadsheetOrder, true));

            // datetime
            standardControls.Add(this.GetDateTimeTuples(++controlOrder, ++spreadsheetOrder, true));

            // utcOffset
            standardControls.Add(this.GetUtcOffsetTuples(++controlOrder, ++spreadsheetOrder, false));

            // image quality
            List<ColumnTuple> imageQuality = new List<ColumnTuple>();
            imageQuality.Add(new ColumnTuple(Constant.Control.ControlOrder, ++controlOrder));
            imageQuality.Add(new ColumnTuple(Constant.Control.SpreadsheetOrder, ++spreadsheetOrder));
            imageQuality.Add(new ColumnTuple(Constant.Control.Type, Constant.DatabaseColumn.ImageQuality));
            imageQuality.Add(new ColumnTuple(Constant.Control.DefaultValue, Constant.ControlDefault.Value));
            imageQuality.Add(new ColumnTuple(Constant.Control.Label, Constant.DatabaseColumn.ImageQuality));
            imageQuality.Add(new ColumnTuple(Constant.Control.DataLabel, Constant.DatabaseColumn.ImageQuality));
            imageQuality.Add(new ColumnTuple(Constant.Control.Tooltip, Constant.ControlDefault.ImageQualityTooltip));
            imageQuality.Add(new ColumnTuple(Constant.Control.Width, Constant.ControlDefault.ImageQualityWidth));
            imageQuality.Add(new ColumnTuple(Constant.Control.Copyable, false));
            imageQuality.Add(new ColumnTuple(Constant.Control.Visible, true));
            imageQuality.Add(new ColumnTuple(Constant.Control.List, Constant.ImageQuality.ListOfValues));
            standardControls.Add(imageQuality);

            // delete flag
            standardControls.Add(this.GetDeleteFlagTuples(++controlOrder, ++spreadsheetOrder, true));

            // insert standard controls into the template table
            this.Database.Insert(Constant.DatabaseTable.Controls, standardControls);

            // populate the in memory version of the template table
            this.GetControlsSortedByControlOrder();
        }

        protected virtual void OnExistingDatabaseOpened(TemplateDatabase other)
        {
            this.GetControlsSortedByControlOrder();
        }

        /// <summary>
        /// Set the order of the specified control to the specified value, shifting other controls' orders as needed.
        /// </summary>
        private void SetControlOrders(string dataLabel, int order)
        {
            if ((order < 1) || (order > this.Controls.RowCount))
            {
                throw new ArgumentOutOfRangeException("order", "Control and spreadsheet orders must be contiguous ones based values.");
            }

            Dictionary<string, long> newControlOrderByDataLabel = new Dictionary<string, long>();
            Dictionary<string, long> newSpreadsheetOrderByDataLabel = new Dictionary<string, long>();
            foreach (ControlRow control in this.Controls)
            {
                if (control.DataLabel == dataLabel)
                {
                    newControlOrderByDataLabel.Add(dataLabel, order);
                    newSpreadsheetOrderByDataLabel.Add(dataLabel, order);
                }
                else
                {
                    long currentControlOrder = control.ControlOrder;
                    if (currentControlOrder >= order)
                    {
                        ++currentControlOrder;
                    }
                    newControlOrderByDataLabel.Add(control.DataLabel, currentControlOrder);

                    long currentSpreadsheetOrder = control.SpreadsheetOrder;
                    if (currentSpreadsheetOrder >= order)
                    {
                        ++currentSpreadsheetOrder;
                    }
                    newSpreadsheetOrderByDataLabel.Add(control.DataLabel, currentSpreadsheetOrder);
                }
            }

            this.UpdateDisplayOrder(Constant.Control.ControlOrder, newControlOrderByDataLabel);
            this.UpdateDisplayOrder(Constant.Control.SpreadsheetOrder, newSpreadsheetOrderByDataLabel);
            this.GetControlsSortedByControlOrder();
        }

        /// <summary>
        /// Set the ID of the specified control to the specified value, shifting other controls' IDs as needed.
        /// </summary>
        private void SetControlID(string dataLabel, int newID)
        {
            // nothing to do
            long currentID = this.GetControlID(dataLabel);
            if (currentID == newID)
            {
                return;
            }

            // move other controls out of the way if the requested ID is in use
            ControlRow conflictingControl = this.Controls.Find(newID);
            List<string> queries = new List<string>();
            if (conflictingControl != null)
            {
                // First update: because any changed IDs have to be unique, first move them beyond the current ID range
                long maximumID = 0;
                foreach (ControlRow control in this.Controls)
                {
                    if (maximumID < control.ID)
                    {
                        maximumID = control.ID;
                    }
                }
                Debug.Assert((maximumID > 0) && (maximumID <= Int64.MaxValue), String.Format("Maximum ID found is {0}, which is out of range.", maximumID));
                string jumpAmount = maximumID.ToString();

                string increaseIDs = "Update " + Constant.DatabaseTable.Controls;
                increaseIDs += " SET " + Constant.DatabaseColumn.ID + " = " + Constant.DatabaseColumn.ID + " + 1 + " + jumpAmount;
                increaseIDs += " WHERE " + Constant.DatabaseColumn.ID + " >= " + newID;
                queries.Add(increaseIDs);

                // Second update: decrease IDs above newID to be one more than their original value
                // This leaves everything in sequence except for an open spot at newID.
                string reduceIDs = "Update " + Constant.DatabaseTable.Controls;
                reduceIDs += " SET " + Constant.DatabaseColumn.ID + " = " + Constant.DatabaseColumn.ID + " - " + jumpAmount;
                reduceIDs += " WHERE " + Constant.DatabaseColumn.ID + " >= " + newID;
                queries.Add(reduceIDs);
            }

            // 3rd update: change the target ID to the desired ID
            this.CreateBackupIfNeeded();
            string setControlID = "Update " + Constant.DatabaseTable.Controls;
            setControlID += " SET " + Constant.DatabaseColumn.ID + " = " + newID;
            setControlID += " WHERE " + Constant.Control.DataLabel + " = '" + dataLabel + "'";
            queries.Add(setControlID);
            this.Database.ExecuteNonQueryWrappedInBeginEnd(queries);

            this.GetControlsSortedByControlOrder();
        }

        public string GetNextUniqueDataLabel(string dataLabelPrefix)
        {
            // get all existing data labels, as we have to ensure that a new data label doesn't have the same name as an existing one
            List<string> dataLabels = new List<string>();
            foreach (ControlRow control in this.Controls)
            {
                dataLabels.Add(control.DataLabel);
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

        private long GetOrderForNewControl()
        {
            return this.Controls.RowCount + 1;
        }

        private List<ColumnTuple> GetDateTimeTuples(long controlOrder, long spreadsheetOrder, bool visible)
        {
            List<ColumnTuple> dateTime = new List<ColumnTuple>();
            dateTime.Add(new ColumnTuple(Constant.Control.ControlOrder, controlOrder));
            dateTime.Add(new ColumnTuple(Constant.Control.SpreadsheetOrder, spreadsheetOrder));
            dateTime.Add(new ColumnTuple(Constant.Control.Type, Constant.DatabaseColumn.DateTime));
            dateTime.Add(new ColumnTuple(Constant.Control.DefaultValue, Constant.ControlDefault.DateTimeValue.UtcDateTime));
            dateTime.Add(new ColumnTuple(Constant.Control.Label, Constant.DatabaseColumn.DateTime));
            dateTime.Add(new ColumnTuple(Constant.Control.DataLabel, Constant.DatabaseColumn.DateTime));
            dateTime.Add(new ColumnTuple(Constant.Control.Tooltip, Constant.ControlDefault.DateTimeTooltip));
            dateTime.Add(new ColumnTuple(Constant.Control.Width, Constant.ControlDefault.DateTimeWidth));
            dateTime.Add(new ColumnTuple(Constant.Control.Copyable, false));
            dateTime.Add(new ColumnTuple(Constant.Control.Visible, visible));
            dateTime.Add(new ColumnTuple(Constant.Control.List, Constant.ControlDefault.Value));
            return dateTime;
        }

        // Defines a RelativePath control. The definition is used by its caller to insert a RelativePath control into the template for backwards compatability. 
        private List<ColumnTuple> GetRelativePathTuples(long controlOrder, long spreadsheetOrder, bool visible)
        {
            List<ColumnTuple> relativePath = new List<ColumnTuple>();
            relativePath.Add(new ColumnTuple(Constant.Control.ControlOrder, controlOrder));
            relativePath.Add(new ColumnTuple(Constant.Control.SpreadsheetOrder, spreadsheetOrder));
            relativePath.Add(new ColumnTuple(Constant.Control.Type, Constant.DatabaseColumn.RelativePath));
            relativePath.Add(new ColumnTuple(Constant.Control.DefaultValue, Constant.ControlDefault.Value));
            relativePath.Add(new ColumnTuple(Constant.Control.Label, Constant.DatabaseColumn.RelativePath));
            relativePath.Add(new ColumnTuple(Constant.Control.DataLabel, Constant.DatabaseColumn.RelativePath));
            relativePath.Add(new ColumnTuple(Constant.Control.Tooltip, Constant.ControlDefault.RelativePathTooltip));
            relativePath.Add(new ColumnTuple(Constant.Control.Width, Constant.ControlDefault.RelativePathWidth));
            relativePath.Add(new ColumnTuple(Constant.Control.Copyable, false));
            relativePath.Add(new ColumnTuple(Constant.Control.Visible, visible));
            relativePath.Add(new ColumnTuple(Constant.Control.List, Constant.ControlDefault.Value));
            return relativePath;
        }

        // Defines a DeleteFlag control. The definition is used by its caller to insert a DeleteFlag control into the template for backwards compatability. 
        private List<ColumnTuple> GetDeleteFlagTuples(long controlOrder, long spreadsheetOrder, bool visible)
        {
            List<ColumnTuple> deleteFlag = new List<ColumnTuple>();
            deleteFlag.Add(new ColumnTuple(Constant.Control.ControlOrder, controlOrder));
            deleteFlag.Add(new ColumnTuple(Constant.Control.SpreadsheetOrder, spreadsheetOrder));
            deleteFlag.Add(new ColumnTuple(Constant.Control.Type, Constant.DatabaseColumn.DeleteFlag));
            deleteFlag.Add(new ColumnTuple(Constant.Control.DefaultValue, Constant.ControlDefault.FlagValue));
            deleteFlag.Add(new ColumnTuple(Constant.Control.Label, Constant.ControlDefault.DeleteFlagLabel));
            deleteFlag.Add(new ColumnTuple(Constant.Control.DataLabel, Constant.DatabaseColumn.DeleteFlag));
            deleteFlag.Add(new ColumnTuple(Constant.Control.Tooltip, Constant.ControlDefault.DeleteFlagTooltip));
            deleteFlag.Add(new ColumnTuple(Constant.Control.Width, Constant.ControlDefault.FlagWidth));
            deleteFlag.Add(new ColumnTuple(Constant.Control.Copyable, false));
            deleteFlag.Add(new ColumnTuple(Constant.Control.Visible, visible));
            deleteFlag.Add(new ColumnTuple(Constant.Control.List, Constant.ControlDefault.Value));
            return deleteFlag;
        }

        private List<ColumnTuple> GetUtcOffsetTuples(long controlOrder, long spreadsheetOrder, bool visible)
        {
            List<ColumnTuple> utcOffset = new List<ColumnTuple>();
            utcOffset.Add(new ColumnTuple(Constant.Control.ControlOrder, controlOrder));
            utcOffset.Add(new ColumnTuple(Constant.Control.SpreadsheetOrder, spreadsheetOrder));
            utcOffset.Add(new ColumnTuple(Constant.Control.Type, Constant.DatabaseColumn.UtcOffset));
            utcOffset.Add(new ColumnTuple(Constant.Control.DefaultValue, Constant.ControlDefault.DateTimeValue.Offset));
            utcOffset.Add(new ColumnTuple(Constant.Control.Label, Constant.DatabaseColumn.UtcOffset));
            utcOffset.Add(new ColumnTuple(Constant.Control.DataLabel, Constant.DatabaseColumn.UtcOffset));
            utcOffset.Add(new ColumnTuple(Constant.Control.Tooltip, Constant.ControlDefault.UtcOffsetTooltip));
            utcOffset.Add(new ColumnTuple(Constant.Control.Width, Constant.ControlDefault.UtcOffsetWidth));
            utcOffset.Add(new ColumnTuple(Constant.Control.Copyable, false));
            utcOffset.Add(new ColumnTuple(Constant.Control.Visible, visible));
            utcOffset.Add(new ColumnTuple(Constant.Control.List, Constant.ControlDefault.Value));
            return utcOffset;
        }
    }
}
