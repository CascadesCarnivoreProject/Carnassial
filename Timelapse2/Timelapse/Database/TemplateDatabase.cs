using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Controls;

namespace Timelapse.Database
{
    /// <summary>
    /// Timelapse Template Database.
    /// </summary>
    public class TemplateDatabase : IDisposable
    {
        private bool disposed;
        private DataGrid editorDataGrid;
        private DataRowChangeEventHandler onTemplateTableRowChanged;

        protected TemplateDatabase(string filePath)
        {
            this.disposed = false;

            // open or create database
            this.Database = new SQLiteWrapper(filePath);
            this.FilePath = filePath;
        }

        protected SQLiteWrapper Database { get; set; }

        /// <summary>Gets the file name of the image database on disk.</summary>
        public string FilePath { get; private set; }

        /// <summary>
        /// Gets the template table
        /// </summary>
        public DataTableBackedList<ControlRow> TemplateTable { get; private set; }

        public void BindToEditorDataGrid(DataGrid dataGrid, DataRowChangeEventHandler onRowChanged)
        {
            this.editorDataGrid = dataGrid;
            this.onTemplateTableRowChanged = onRowChanged;
            this.GetControlsSortedByControlOrder();
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
            // create the row for the new control in the data table
            ControlRow newControl = this.TemplateTable.NewRow();
            string dataLabelPrefix;
            switch (controlType)
            {
                case Constants.Control.Counter:
                    dataLabelPrefix = Constants.Control.Counter;
                    newControl.DefaultValue = Constants.ControlDefault.CounterValue;
                    newControl.Type = Constants.Control.Counter;
                    newControl.TextBoxWidth = Constants.ControlDefault.CounterWidth;
                    newControl.Copyable = false;
                    newControl.Visible = true;
                    newControl.Tooltip = Constants.ControlDefault.CounterTooltip;
                    newControl.DataLabel = this.GetNextUniqueDataLabel(dataLabelPrefix);
                    newControl.Label = newControl.DataLabel;
                    break;
                case Constants.Control.Note:
                    dataLabelPrefix = Constants.Control.Note;
                    newControl.DefaultValue = Constants.ControlDefault.Value;
                    newControl.Type = Constants.Control.Note;
                    newControl.TextBoxWidth = Constants.ControlDefault.NoteWidth;
                    newControl.Copyable = true;
                    newControl.Visible = true;
                    newControl.Tooltip = Constants.ControlDefault.NoteTooltip;
                    newControl.DataLabel = this.GetNextUniqueDataLabel(dataLabelPrefix);
                    newControl.Label = newControl.DataLabel;
                    break;
                case Constants.Control.FixedChoice:
                    dataLabelPrefix = Constants.Control.Choice;
                    newControl.DefaultValue = Constants.ControlDefault.Value;
                    newControl.Type = Constants.Control.FixedChoice;
                    newControl.TextBoxWidth = Constants.ControlDefault.FixedChoiceWidth;
                    newControl.Copyable = true;
                    newControl.Visible = true;
                    newControl.Tooltip = Constants.ControlDefault.FixedChoiceTooltip;
                    newControl.DataLabel = this.GetNextUniqueDataLabel(dataLabelPrefix);
                    newControl.Label = newControl.DataLabel;
                    break;
                case Constants.Control.Flag:
                    dataLabelPrefix = Constants.Control.Flag;
                    newControl.DefaultValue = Constants.ControlDefault.FlagValue;
                    newControl.Type = Constants.Control.Flag;
                    newControl.TextBoxWidth = Constants.ControlDefault.FlagWidth;
                    newControl.Copyable = true;
                    newControl.Visible = true;
                    newControl.Tooltip = Constants.ControlDefault.FlagTooltip;
                    newControl.DataLabel = this.GetNextUniqueDataLabel(dataLabelPrefix);
                    newControl.Label = newControl.DataLabel;
                    break;
                default:
                    throw new NotSupportedException(String.Format("Unhandled control type {0}.", controlType));
            }
            newControl.ControlOrder = this.GetOrderForNewControl();
            newControl.List = Constants.ControlDefault.Value;
            newControl.SpreadsheetOrder = newControl.ControlOrder;

            // add the new control to the database
            List<List<ColumnTuple>> controlInsertWrapper = new List<List<ColumnTuple>>() { newControl.GetColumnTuples().Columns };
            this.Database.Insert(Constants.Database.TemplateTable, controlInsertWrapper);

            // update the in memory table to reflect current database content
            // could just add the new row to the table but this is done in case a bug results in the insert lacking perfect fidelity
            this.GetControlsSortedByControlOrder();
            return this.TemplateTable[this.TemplateTable.RowCount - 1];
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void GetControlsSortedByControlOrder()
        {
            DataTable templateTable = this.Database.GetDataTableFromSelect(Constants.Sql.SelectStarFrom + Constants.Database.TemplateTable + " ORDER BY  " + Constants.Control.ControlOrder);
            this.TemplateTable = new DataTableBackedList<ControlRow>(templateTable, (DataRow row) => { return new ControlRow(row); });
            this.TemplateTable.BindDataGrid(this.editorDataGrid, this.onTemplateTableRowChanged);
        }

        public List<string> GetDataLabelsExceptID()
        {
            List<string> dataLabels = new List<string>();
            foreach (ControlRow control in this.TemplateTable)
            {
                string dataLabel = control.DataLabel;
                if (dataLabel == String.Empty)
                {
                    dataLabel = control.Label;
                }
                Debug.Assert(String.IsNullOrWhiteSpace(dataLabel) == false, String.Format("Encountered empty data label and label at ID {0} in template table.", control.ID));

                // get a list of datalabels so we can add columns in the order that matches the current template table order
                if (Constants.DatabaseColumn.ID != dataLabel)
                {
                    dataLabels.Add(dataLabel);
                }
            }
            return dataLabels;
        }

        public bool IsControlCopyable(string dataLabel)
        {
            long id = this.GetControlIDFromTemplateTable(dataLabel);
            ControlRow control = this.TemplateTable.Find(id);
            return control.Copyable;
        }

        public void RemoveUserDefinedControl(ControlRow controlToRemove)
        {
            string controlType;
            // For backwards compatability: MarkForDeletion DataLabel is of the type DeleteFlag,
            // which is a standard control. So we coerce it into thinking its a different type.
            if (controlToRemove.DataLabel == Constants.ControlsDeprecated.MarkForDeletion)
            {
                controlType = Constants.ControlsDeprecated.MarkForDeletion;
            }
            else
            { 
                controlType = controlToRemove.Type;
            }
            if (Constants.Control.StandardTypes.Contains(controlType))
            {
                throw new NotSupportedException(String.Format("Standard control of type {0} cannot be removed.", controlType));
            }

            // capture state
            long removedControlOrder = controlToRemove.ControlOrder;
            long removedSpreadsheetOrder = controlToRemove.SpreadsheetOrder;

            // drop the control from the database and data table
            string where = Constants.DatabaseColumn.ID + " = " + controlToRemove.ID;
            this.Database.DeleteRows(Constants.Database.TemplateTable, where);
            this.GetControlsSortedByControlOrder();

            // regenerate counter and spreadsheet orders; if they're greater than the one removed, decrement
            List<ColumnTuplesWithWhere> controlUpdates = new List<ColumnTuplesWithWhere>();
            foreach (ControlRow control in this.TemplateTable)
            {
                long controlOrder = control.ControlOrder;
                long spreadsheetOrder = control.SpreadsheetOrder;

                if (controlOrder > removedControlOrder)
                {
                    List<ColumnTuple> controlUpdate = new List<ColumnTuple>();
                    controlUpdate.Add(new ColumnTuple(Constants.Control.ControlOrder, controlOrder - 1));
                    control.ControlOrder = controlOrder - 1;
                    controlUpdates.Add(new ColumnTuplesWithWhere(controlUpdate, control.ID));
                }

                if (spreadsheetOrder > removedSpreadsheetOrder)
                {
                    List<ColumnTuple> controlUpdate = new List<ColumnTuple>();
                    controlUpdate.Add(new ColumnTuple(Constants.Control.SpreadsheetOrder, spreadsheetOrder - 1));
                    control.SpreadsheetOrder = spreadsheetOrder - 1;
                    controlUpdates.Add(new ColumnTuplesWithWhere(controlUpdate, control.ID));
                }
            }
            this.Database.Update(Constants.Database.TemplateTable, controlUpdates);

            // update the in memory table to reflect current database content
            // should not be necessary but this is done to mitigate divergence in case a bug results in the delete lacking perfect fidelity
            this.GetControlsSortedByControlOrder();
        }

        public void SyncControlToDatabase(ControlRow control)
        {
            this.Database.Update(Constants.Database.TemplateTable, control.GetColumnTuples());

            // it's possible the passed data row isn't attached to TemplateTable, so refresh the table just in case
            this.GetControlsSortedByControlOrder();
        }

        private void SyncTemplateTableToDatabase()
        {
            this.SyncTemplateTableToDatabase(this.TemplateTable);
        }

        private void SyncTemplateTableToDatabase(DataTableBackedList<ControlRow> newTable)
        {
            // clear the existing table in the database and add the new values
            this.Database.DeleteRows(Constants.Database.TemplateTable, null);

            List<List<ColumnTuple>> newTableTuples = new List<List<ColumnTuple>>();
            foreach (ControlRow control in newTable)
            {
                newTableTuples.Add(control.GetColumnTuples().Columns);
            }
            this.Database.Insert(Constants.Database.TemplateTable, newTableTuples);

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
                Debug.Assert(false, exception.ToString());
                database = null;
                return false;
            }
        }

        public void UpdateDisplayOrder(string orderColumnName, Dictionary<string, long> newOrderByDataLabel)
        {
            // argument validation
            if (orderColumnName != Constants.Control.ControlOrder && orderColumnName != Constants.Control.SpreadsheetOrder)
            {
                throw new ArgumentOutOfRangeException("column", String.Format("'{0}' is not a control order column.  Only '{1}' and '{2}' are order columns.", orderColumnName, Constants.Control.ControlOrder, Constants.Control.SpreadsheetOrder));
            }

            if (newOrderByDataLabel.Count != this.TemplateTable.RowCount)
            {
                throw new NotSupportedException(String.Format("Partial order updates are not supported.  New ordering for {0} controls was passed but {1} controls are present for '{2}'.", newOrderByDataLabel.Count, this.TemplateTable.RowCount, orderColumnName));
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
            foreach (ControlRow control in this.TemplateTable)
            {
                string dataLabel = control.DataLabel;
                long newOrder = newOrderByDataLabel[dataLabel];
                switch (orderColumnName)
                {
                    case Constants.Control.ControlOrder:
                        control.ControlOrder = newOrder;
                        break;
                    case Constants.Control.SpreadsheetOrder:
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
                if (this.TemplateTable != null)
                {
                    this.TemplateTable.Dispose();
                }
            }

            this.disposed = true;
        }

        /// <summary>Given a data label, get the corresponding data entry control</summary>
        public ControlRow GetControlFromTemplateTable(string dataLabel)
        {
            foreach (ControlRow control in this.TemplateTable)
            {
                if (dataLabel.Equals(control.DataLabel))
                {
                    return control;
                }
            }
            return null;
        }

        /// <summary>Given a data label, get the id of the corresponding data entry control</summary>
        protected long GetControlIDFromTemplateTable(string dataLabel)
        {
            ControlRow control = this.GetControlFromTemplateTable(dataLabel);
            if (control == null)
            {
                return -1;
            }
            return control.ID;
        }

        protected virtual void OnDatabaseCreated(TemplateDatabase other)
        {
            // create the template table
            List<ColumnTuple> templateTableColumns = new List<ColumnTuple>();
            templateTableColumns.Add(new ColumnTuple(Constants.DatabaseColumn.ID, "INTEGER primary key autoincrement"));
            templateTableColumns.Add(new ColumnTuple(Constants.Control.ControlOrder, "INTEGER"));
            templateTableColumns.Add(new ColumnTuple(Constants.Control.SpreadsheetOrder, "INTEGER"));
            templateTableColumns.Add(new ColumnTuple(Constants.Control.Type, Constants.Sql.Text));
            templateTableColumns.Add(new ColumnTuple(Constants.Control.DefaultValue, Constants.Sql.Text));
            templateTableColumns.Add(new ColumnTuple(Constants.Control.Label, Constants.Sql.Text));
            templateTableColumns.Add(new ColumnTuple(Constants.Control.DataLabel, Constants.Sql.Text));
            templateTableColumns.Add(new ColumnTuple(Constants.Control.Tooltip, Constants.Sql.Text));
            templateTableColumns.Add(new ColumnTuple(Constants.Control.TextBoxWidth, Constants.Sql.Text));
            templateTableColumns.Add(new ColumnTuple(Constants.Control.Copyable, Constants.Sql.Text));
            templateTableColumns.Add(new ColumnTuple(Constants.Control.Visible, Constants.Sql.Text));
            templateTableColumns.Add(new ColumnTuple(Constants.Control.List, Constants.Sql.Text));
            this.Database.CreateTable(Constants.Database.TemplateTable, templateTableColumns);

            // if an existing table was passed, clone its contents into this database
            if (other != null)
            {
                this.SyncTemplateTableToDatabase(other.TemplateTable);
                return;
            }

            // no existing table to clone, so add standard controls to template table
            List<List<ColumnTuple>> standardControls = new List<List<ColumnTuple>>();
            long controlOrder = 0; // The control order, a one based count incremented for every new entry
            long spreadsheetOrder = 0; // The spreadsheet order, a one based count incremented for every new entry

            // file
            List<ColumnTuple> file = new List<ColumnTuple>();
            file.Add(new ColumnTuple(Constants.Control.ControlOrder, ++controlOrder));
            file.Add(new ColumnTuple(Constants.Control.SpreadsheetOrder, ++spreadsheetOrder));
            file.Add(new ColumnTuple(Constants.Control.Type, Constants.DatabaseColumn.File));
            file.Add(new ColumnTuple(Constants.Control.DefaultValue, Constants.ControlDefault.Value));
            file.Add(new ColumnTuple(Constants.Control.Label, Constants.DatabaseColumn.File));
            file.Add(new ColumnTuple(Constants.Control.DataLabel, Constants.DatabaseColumn.File));
            file.Add(new ColumnTuple(Constants.Control.Tooltip, Constants.ControlDefault.FileTooltip));
            file.Add(new ColumnTuple(Constants.Control.TextBoxWidth, Constants.ControlDefault.FileWidth));
            file.Add(new ColumnTuple(Constants.Control.Copyable, Constants.Boolean.False));
            file.Add(new ColumnTuple(Constants.Control.Visible, Constants.Boolean.True));
            file.Add(new ColumnTuple(Constants.Control.List, Constants.ControlDefault.Value));
            standardControls.Add(file);

            // relative path
            standardControls.Add(this.GetRelativePathTuples(++controlOrder, ++spreadsheetOrder, true));

            // folder
            List<ColumnTuple> folder = new List<ColumnTuple>();
            folder.Add(new ColumnTuple(Constants.Control.ControlOrder, ++controlOrder));
            folder.Add(new ColumnTuple(Constants.Control.SpreadsheetOrder, ++spreadsheetOrder));
            folder.Add(new ColumnTuple(Constants.Control.Type, Constants.DatabaseColumn.Folder));
            folder.Add(new ColumnTuple(Constants.Control.DefaultValue, Constants.ControlDefault.Value));
            folder.Add(new ColumnTuple(Constants.Control.Label, Constants.DatabaseColumn.Folder));
            folder.Add(new ColumnTuple(Constants.Control.DataLabel, Constants.DatabaseColumn.Folder));
            folder.Add(new ColumnTuple(Constants.Control.Tooltip, Constants.ControlDefault.FolderTooltip));
            folder.Add(new ColumnTuple(Constants.Control.TextBoxWidth, Constants.ControlDefault.FolderWidth));
            folder.Add(new ColumnTuple(Constants.Control.Copyable, Constants.Boolean.False));
            folder.Add(new ColumnTuple(Constants.Control.Visible, Constants.Boolean.True));
            folder.Add(new ColumnTuple(Constants.Control.List, Constants.ControlDefault.Value));
            standardControls.Add(folder);

            // date
            List<ColumnTuple> date = new List<ColumnTuple>();
            date.Add(new ColumnTuple(Constants.Control.ControlOrder, ++controlOrder));
            date.Add(new ColumnTuple(Constants.Control.SpreadsheetOrder, ++spreadsheetOrder));
            date.Add(new ColumnTuple(Constants.Control.Type, Constants.DatabaseColumn.Date));
            date.Add(new ColumnTuple(Constants.Control.DefaultValue, Constants.ControlDefault.Value));
            date.Add(new ColumnTuple(Constants.Control.Label, Constants.DatabaseColumn.Date));
            date.Add(new ColumnTuple(Constants.Control.DataLabel, Constants.DatabaseColumn.Date));
            date.Add(new ColumnTuple(Constants.Control.Tooltip, Constants.ControlDefault.DateTooltip));
            date.Add(new ColumnTuple(Constants.Control.TextBoxWidth, Constants.ControlDefault.DateWidth));
            date.Add(new ColumnTuple(Constants.Control.Copyable, Constants.Boolean.False));
            date.Add(new ColumnTuple(Constants.Control.Visible, Constants.Boolean.True));
            date.Add(new ColumnTuple(Constants.Control.List, Constants.ControlDefault.Value));
            standardControls.Add(date);

            // time
            List<ColumnTuple> time = new List<ColumnTuple>();
            time.Add(new ColumnTuple(Constants.Control.ControlOrder, ++controlOrder));
            time.Add(new ColumnTuple(Constants.Control.SpreadsheetOrder, ++spreadsheetOrder));
            time.Add(new ColumnTuple(Constants.Control.Type, Constants.DatabaseColumn.Time));
            time.Add(new ColumnTuple(Constants.Control.DefaultValue, Constants.ControlDefault.Value));
            time.Add(new ColumnTuple(Constants.Control.Label, Constants.DatabaseColumn.Time));
            time.Add(new ColumnTuple(Constants.Control.DataLabel, Constants.DatabaseColumn.Time));
            time.Add(new ColumnTuple(Constants.Control.Tooltip, Constants.ControlDefault.TimeTooltip));
            time.Add(new ColumnTuple(Constants.Control.TextBoxWidth, Constants.ControlDefault.TimeWidth));
            time.Add(new ColumnTuple(Constants.Control.Copyable, Constants.Boolean.False));
            time.Add(new ColumnTuple(Constants.Control.Visible, Constants.Boolean.True));
            time.Add(new ColumnTuple(Constants.Control.List, Constants.ControlDefault.Value));
            standardControls.Add(time);

            // image quality
            List<ColumnTuple> imageQuality = new List<ColumnTuple>();
            imageQuality.Add(new ColumnTuple(Constants.Control.ControlOrder, ++controlOrder));
            imageQuality.Add(new ColumnTuple(Constants.Control.SpreadsheetOrder, ++spreadsheetOrder));
            imageQuality.Add(new ColumnTuple(Constants.Control.Type, Constants.DatabaseColumn.ImageQuality));
            imageQuality.Add(new ColumnTuple(Constants.Control.DefaultValue, Constants.ControlDefault.Value));
            imageQuality.Add(new ColumnTuple(Constants.Control.Label, Constants.DatabaseColumn.ImageQuality));
            imageQuality.Add(new ColumnTuple(Constants.Control.DataLabel, Constants.DatabaseColumn.ImageQuality));
            imageQuality.Add(new ColumnTuple(Constants.Control.Tooltip, Constants.ControlDefault.ImageQualityTooltip));
            imageQuality.Add(new ColumnTuple(Constants.Control.TextBoxWidth, Constants.ControlDefault.ImageQualityWidth));
            imageQuality.Add(new ColumnTuple(Constants.Control.Copyable, Constants.Boolean.False));
            imageQuality.Add(new ColumnTuple(Constants.Control.Visible, Constants.Boolean.True));
            imageQuality.Add(new ColumnTuple(Constants.Control.List, Constants.ImageQuality.ListOfValues));
            standardControls.Add(imageQuality);

            // delete flag
            standardControls.Add(this.GetDeleteFlagTuples(++controlOrder, ++spreadsheetOrder, true));

            // insert standard controls into the template table
            this.Database.Insert(Constants.Database.TemplateTable, standardControls);

            // populate the in memory version of the template table
            this.GetControlsSortedByControlOrder();
        }

        protected virtual void OnExistingDatabaseOpened(TemplateDatabase other)
        {
            this.GetControlsSortedByControlOrder();
            this.EnsureDataLabelsAndLabelsNotEmpty();
            this.EnsureCurrentSchema();
        }

        // Do various checks and corrections to the Template DB to maintain backwards compatability. 
        private void EnsureCurrentSchema()
        {
            // Add a RelativePath control to pre v2.1 databases if one hasn't already been inserted
            long relativePathID = this.GetControlIDFromTemplateTable(Constants.DatabaseColumn.RelativePath);
            if (relativePathID == -1)
            {
                // insert a relative path control, where its ID will be created as the next highest ID
                long order = this.GetOrderForNewControl();
                List<ColumnTuple> relativePathControl = this.GetRelativePathTuples(order, order, true);
                this.Database.Insert(Constants.Database.TemplateTable, new List<List<ColumnTuple>>() { relativePathControl });

                // move the relative path control to ID and order 2 for consistency with newly created templates
                this.SetControlID(Constants.DatabaseColumn.RelativePath, Constants.Database.RelativePathPosition);
                this.SetControlOrders(Constants.DatabaseColumn.RelativePath, Constants.Database.RelativePathPosition);
            }

            // Backwards compatability: ensure a DeleteFlag control exists, replacing the MarkForDeletion data label used in pre 2.1.0.4 templates if necessary
            ControlRow markForDeletion = this.GetControlFromTemplateTable(Constants.ControlsDeprecated.MarkForDeletion);
            if (markForDeletion != null)
            {
                List<ColumnTuple> deleteFlagControl = this.GetDeleteFlagTuples(markForDeletion.ControlOrder, markForDeletion.SpreadsheetOrder, markForDeletion.Visible);
                this.Database.Update(Constants.Database.TemplateTable, new ColumnTuplesWithWhere(deleteFlagControl, markForDeletion.ID));
                this.GetControlsSortedByControlOrder();
            }
            else if (this.GetControlIDFromTemplateTable(Constants.Control.DeleteFlag) < 0)
            {
                // insert a DeleteFlag control, where its ID will be created as the next highest ID
                long order = this.GetOrderForNewControl();
                List<ColumnTuple> deleteFlagControl = this.GetDeleteFlagTuples(order, order, true);
                this.Database.Insert(Constants.Database.TemplateTable, new List<List<ColumnTuple>>() { deleteFlagControl });
                this.GetControlsSortedByControlOrder();
            }
        }

        /// <summary>
        /// Set the order of the specified control to the specified value, shifting other controls' orders as needed.
        /// </summary>
        private void SetControlOrders(string dataLabel, int order)
        {
            if ((order < 1) || (order > this.TemplateTable.RowCount))
            {
                throw new ArgumentOutOfRangeException("order", "Control and spreadsheet orders must be contiguous ones based values.");
            }

            Dictionary<string, long> newControlOrderByDataLabel = new Dictionary<string, long>();
            Dictionary<string, long> newSpreadsheetOrderByDataLabel = new Dictionary<string, long>();
            foreach (ControlRow control in this.TemplateTable)
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

            this.UpdateDisplayOrder(Constants.Control.ControlOrder, newControlOrderByDataLabel);
            this.UpdateDisplayOrder(Constants.Control.SpreadsheetOrder, newSpreadsheetOrderByDataLabel);
            this.GetControlsSortedByControlOrder();
        }

        /// <summary>
        /// Set the ID of the specified control to the specified value, shifting other controls' IDs as needed.
        /// </summary>
        private void SetControlID(string dataLabel, int newID)
        {
            // nothing to do
            long currentID = this.GetControlIDFromTemplateTable(dataLabel);
            if (currentID == newID)
            {
                return;
            }

            // move other controls out of the way if the requested ID is in use
            ControlRow conflictingControl = this.TemplateTable.Find(newID);
            List<string> queries = new List<string>();
            if (conflictingControl != null)
            {
                // First update: because any changed IDs have to be unique, first move them beyond the current ID range
                long maximumID = 0;
                foreach (ControlRow control in this.TemplateTable)
                {
                    if (maximumID < control.ID)
                    {
                        maximumID = control.ID;
                    }
                }
                Debug.Assert((maximumID > 0) && (maximumID <= Int64.MaxValue), String.Format("Maximum ID found is {0}, which is out of range.", maximumID));
                string jumpAmount = maximumID.ToString();

                string increaseIDs = "Update " + Constants.Database.TemplateTable;
                increaseIDs += " SET " + Constants.DatabaseColumn.ID + " = " + Constants.DatabaseColumn.ID + " + 1 + " + jumpAmount;
                increaseIDs += " WHERE " + Constants.DatabaseColumn.ID + " >= " + newID;
                queries.Add(increaseIDs);

                // Second update: decrease IDs above newID to be one more than their original value
                // This leaves everything in sequence except for an open spot at newID.
                string reduceIDs = "Update " + Constants.Database.TemplateTable;
                reduceIDs += " SET " + Constants.DatabaseColumn.ID + " = " + Constants.DatabaseColumn.ID + " - " + jumpAmount;
                reduceIDs += " WHERE " + Constants.DatabaseColumn.ID + " >= " + newID;
                queries.Add(reduceIDs);
            }

            // 3rd update: change the target ID to the desired ID
            string setControlID = "Update " + Constants.Database.TemplateTable;
            setControlID += " SET " + Constants.DatabaseColumn.ID + " = " + newID;
            setControlID += " WHERE " + Constants.Control.DataLabel + " = '" + dataLabel + "'";
            queries.Add(setControlID);
            this.Database.ExecuteNonQueryWrappedInBeginEnd(queries);

            this.GetControlsSortedByControlOrder();
        }

        public string GetNextUniqueDataLabel(string dataLabelPrefix)
        {
            // get all existing data labels, as we have to ensure that a new data label doesn't have the same name as an existing one
            List<string> dataLabels = new List<string>();
            foreach (ControlRow control in this.TemplateTable)
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
            return this.TemplateTable.RowCount + 1;
        }

        /// <summary>
        /// Supply default values for any empty labels or data labels are non-empty, updating both TemplateTable and the database as needed
        /// </summary>
        private void EnsureDataLabelsAndLabelsNotEmpty()
        {
            // All the code below goes through the template table to see if there are any non-empty labels / data labels,
            // and if so, updates them to a reasonable value. If both are empty, it keeps track of its type and creates
            // a label called (say) Counter3 for the third counter that has no label. If there is no DataLabel value, it
            // makes it the same as the label. Ultimately, it guarantees that there will always be a (hopefully unique)
            // data label and label name. 
            // As well, the contents of the template table are loaded into memory.
            foreach (ControlRow control in this.TemplateTable)
            {
                // Check if various values are empty, and if so update the row and fill the dataline with appropriate defaults
                ColumnTuplesWithWhere columnsToUpdate = new ColumnTuplesWithWhere();    // holds columns which have changed for the current control
                bool noDataLabel = String.IsNullOrWhiteSpace(control.DataLabel);
                if (noDataLabel && String.IsNullOrWhiteSpace(control.Label))
                {
                    string dataLabel = this.GetNextUniqueDataLabel(control.Type);
                    columnsToUpdate.Columns.Add(new ColumnTuple(Constants.Control.Label, dataLabel));
                    columnsToUpdate.Columns.Add(new ColumnTuple(Constants.Control.DataLabel, dataLabel));
                    control.Label = dataLabel;
                    control.DataLabel = dataLabel;
                }
                else if (noDataLabel)
                {
                    // No data label but a label, so use the label's value as the data label
                    columnsToUpdate.Columns.Add(new ColumnTuple(Constants.Control.DataLabel, control.Label));
                    control.DataLabel = control.Label;
                }

                // Now add the new values to the database
                if (columnsToUpdate.Columns.Count > 0)
                {
                    columnsToUpdate.SetWhere(control.ID);
                    this.Database.Update(Constants.Database.TemplateTable, columnsToUpdate);
                }
            }
        }

        // Defines a RelativePath column. The definition is used by its caller to insert a RelativePath column into the template for backwards compatability. 
        private List<ColumnTuple> GetRelativePathTuples(long controlOrder, long spreadsheetOrder, bool visible)
        {
            List<ColumnTuple> relativePath = new List<ColumnTuple>();
            relativePath.Add(new ColumnTuple(Constants.Control.ControlOrder, controlOrder));
            relativePath.Add(new ColumnTuple(Constants.Control.SpreadsheetOrder, spreadsheetOrder));
            relativePath.Add(new ColumnTuple(Constants.Control.Type, Constants.DatabaseColumn.RelativePath));
            relativePath.Add(new ColumnTuple(Constants.Control.DefaultValue, Constants.ControlDefault.Value));
            relativePath.Add(new ColumnTuple(Constants.Control.Label, Constants.DatabaseColumn.RelativePath));
            relativePath.Add(new ColumnTuple(Constants.Control.DataLabel, Constants.DatabaseColumn.RelativePath));
            relativePath.Add(new ColumnTuple(Constants.Control.Tooltip, Constants.ControlDefault.RelativePathTooltip));
            relativePath.Add(new ColumnTuple(Constants.Control.TextBoxWidth, Constants.ControlDefault.RelativePathWidth));
            relativePath.Add(new ColumnTuple(Constants.Control.Copyable, Constants.Boolean.False));
            relativePath.Add(new ColumnTuple(Constants.Control.Visible, visible));
            relativePath.Add(new ColumnTuple(Constants.Control.List, Constants.ControlDefault.Value));
            return relativePath;
        }

        // Defines a DeleteFlag column. The definition is used by its caller to insert a DeleteFlag column into the template for backwards compatability. 
        private List<ColumnTuple> GetDeleteFlagTuples(long controlOrder, long spreadsheetOrder, bool visible)
        {
            List<ColumnTuple> deleteFlag = new List<ColumnTuple>();
            deleteFlag.Add(new ColumnTuple(Constants.Control.ControlOrder, controlOrder));
            deleteFlag.Add(new ColumnTuple(Constants.Control.SpreadsheetOrder, spreadsheetOrder));
            deleteFlag.Add(new ColumnTuple(Constants.Control.Type, Constants.Control.DeleteFlag));
            deleteFlag.Add(new ColumnTuple(Constants.Control.DefaultValue, Constants.ControlDefault.FlagValue));
            deleteFlag.Add(new ColumnTuple(Constants.Control.Label, Constants.Control.DeleteFlagLabel));
            deleteFlag.Add(new ColumnTuple(Constants.Control.DataLabel, Constants.Control.DeleteFlag));
            deleteFlag.Add(new ColumnTuple(Constants.Control.Tooltip, Constants.ControlDefault.DeleteFlagTooltip));
            deleteFlag.Add(new ColumnTuple(Constants.Control.TextBoxWidth, Constants.ControlDefault.FlagWidth));
            deleteFlag.Add(new ColumnTuple(Constants.Control.Copyable, Constants.Boolean.False));
            deleteFlag.Add(new ColumnTuple(Constants.Control.Visible, visible));
            deleteFlag.Add(new ColumnTuple(Constants.Control.List, Constants.ControlDefault.Value));
            return deleteFlag;
        }
    }
}
