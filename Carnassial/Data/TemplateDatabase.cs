using Carnassial.Database;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ColumnDefinition = Carnassial.Database.ColumnDefinition;

namespace Carnassial.Data
{
    /// <summary>
    /// Carnassial template database
    /// </summary>
    public class TemplateDatabase
    {
        private DateTime mostRecentBackup;

        public ControlTable Controls { get; private set; }

        protected SQLiteDatabase Database { get; set; }

        /// <summary>Gets the path of the database on disk.</summary>
        public string FilePath { get; private set; }

        /// <summary>Gets the complete path to the folder containing the database.</summary>
        public string FolderPath { get; private set; }

        public ImageSetRow ImageSet { get; private set; }

        protected TemplateDatabase(string filePath)
        {
            this.mostRecentBackup = FileBackup.GetMostRecentBackup(filePath);

            this.Controls = new ControlTable();
            // open or create database
            this.Database = new SQLiteDatabase(filePath);
            this.FilePath = filePath;
            this.FolderPath = Path.GetDirectoryName(filePath);
        }

        public ControlRow AppendUserDefinedControl(ControlType controlType)
        {
            this.CreateBackupIfNeeded();

            // create the row for the new control in the data table
            ControlRow newControl = new ControlRow(controlType, this.GetNextUniqueDataLabel(controlType.ToString()), this.Controls.RowCount + 1);
            ColumnTuplesForInsert newControlInsert = ControlRow.CreateInsert(newControl);
            using (SQLiteConnection connection = this.Database.CreateConnection())
            {
                newControlInsert.Insert(connection);

                // refresh in memory table in order to get the new control's ID
                this.GetControlsSortedByControlOrder(connection);
            }
            return this.Controls[this.Controls.RowCount - 1];
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

        protected void GetControlsSortedByControlOrder(SQLiteConnection connection)
        {
            Select select = new Select(Constant.DatabaseTable.Controls)
            {
                OrderBy = Constant.ControlColumn.ControlOrder
            };

            this.Database.LoadDataTableFromSelect(this.Controls, connection, select);
        }

        public string GetNextUniqueDataLabel(string dataLabelPrefix)
        {
            // get all existing data labels
            List<string> dataLabels = new List<string>();
            foreach (ControlRow control in this.Controls)
            {
                dataLabels.Add(control.DataLabel);
            }

            // if the candidate data label exists, increment the count until a unique data label is found
            int dataLabelUniqueIdentifier = 0;
            string nextDataLabel = dataLabelPrefix + dataLabelUniqueIdentifier.ToString();
            while (dataLabels.Contains(nextDataLabel, StringComparer.Ordinal))
            {
                ++dataLabelUniqueIdentifier;
                nextDataLabel = dataLabelPrefix + dataLabelUniqueIdentifier.ToString();
            }

            return nextDataLabel;
        }

        private void LoadImageSet(SQLiteConnection connection)
        {
            ImageSetTable imageSetTable = new ImageSetTable();
            this.Database.LoadDataTableFromSelect(imageSetTable, connection, new Select(Constant.DatabaseTable.ImageSet));
            this.ImageSet = imageSetTable[0];
        }

        public void RemoveUserDefinedControl(ControlRow controlToRemove)
        {
            this.CreateBackupIfNeeded();

            if (controlToRemove.IsUserControl() == false)
            {
                throw new NotSupportedException(String.Format("Standard control {0} cannot be removed.", controlToRemove.DataLabel));
            }

            // capture state
            long removedControlOrder = controlToRemove.ControlOrder;
            long removedSpreadsheetOrder = controlToRemove.SpreadsheetOrder;

            // regenerate counter and spreadsheet orders; if they're greater than the one removed, decrement
            ColumnTuplesWithID controlOrderUpdates = new ColumnTuplesWithID(Constant.DatabaseTable.Controls, Constant.ControlColumn.ControlOrder);
            ColumnTuplesWithID spreadsheetOrderUpdates = new ColumnTuplesWithID(Constant.DatabaseTable.Controls, Constant.ControlColumn.SpreadsheetOrder);
            foreach (ControlRow control in this.Controls)
            {
                if (control.ControlOrder > removedControlOrder)
                {
                    --control.ControlOrder;
                    controlOrderUpdates.Add(control.ID, control.ControlOrder);
                }

                if (control.SpreadsheetOrder > removedSpreadsheetOrder)
                {
                    --control.SpreadsheetOrder;
                    spreadsheetOrderUpdates.Add(control.ID, control.SpreadsheetOrder);
                }
            }

            // drop the control from the database and data table
            using (SQLiteConnection connection = this.Database.CreateConnection())
            {
                using (SQLiteTransaction transaction = connection.BeginTransaction())
                {
                    using (SQLiteCommand removeControl = new SQLiteCommand("DELETE FROM " + Constant.DatabaseTable.Controls + Constant.Sql.Where + Constant.DatabaseColumn.ID + " = " + controlToRemove.ID, connection))
                    {
                        removeControl.ExecuteNonQuery();
                    }

                    controlOrderUpdates.Update(connection, transaction);
                    spreadsheetOrderUpdates.Update(connection, transaction);

                    transaction.Commit();
                }

                // refresh in memory table
                // Using Remove() rather than rebuilding the table would be desirable but doing so changes row ordering and requires a resort.
                // This path is seldom used and isn't performance sensitive so simplicity is favored instead.
                this.GetControlsSortedByControlOrder(connection);
            }
        }

        public static bool TryCreateOrOpen(string filePath, out TemplateDatabase templateDatabase)
        {
            // check for an existing database before instantiating the databse as SQL wrapper instantiation creates the database file
            bool populateDatabase = !File.Exists(filePath);

            templateDatabase = new TemplateDatabase(filePath);
            if (populateDatabase)
            {
                // initialize the database if it's newly created
                templateDatabase.OnDatabaseCreated(null);
            }
            else
            {
                // if it's an existing database check if it needs updating to current structure and load data tables
                if (templateDatabase.OnExistingDatabaseOpened(null) == false)
                {
                    return false;
                }
            }

            // check all tables have been loaded from the database
            Debug.Assert(templateDatabase.Controls != null, "Controls wasn't loaded.");
            Debug.Assert(templateDatabase.ImageSet != null, "ImageSet wasn't loaded.");

            return true;
        }

        /// <summary>
        /// Update the database row for the control to the version currently in memory.
        /// </summary>
        public bool TrySyncControlToDatabase(ControlRow control)
        {
            if (control.HasChanges == false)
            {
                return false;
            }

            this.CreateBackupIfNeeded();
            using (SQLiteConnection connection = this.Database.CreateConnection())
            {
                this.SyncControlToDatabase(connection, control);
                return true;
            }
        }

        public bool TrySyncImageSetToDatabase()
        {
            if (this.ImageSet.HasChanges == false)
            {
                return false;
            }

            // don't trigger backups on image set updates as none of the properties in the image set table is particularly important
            // For example, this avoids creating a backup when a custom selection is reverted to all when Carnassial exits.
            ColumnTuplesWithID imageSetUpdate = this.ImageSet.CreateUpdate();
            using (SQLiteConnection connection = this.Database.CreateConnection())
            {
                imageSetUpdate.Update(connection);
            }
            this.ImageSet.AcceptChanges();
            return true;
        }

        public void UpdateDisplayOrder(string orderColumnName, Dictionary<string, int> newOrderByDataLabel)
        {
            using (SQLiteConnection connection = this.Database.CreateConnection())
            {
                this.UpdateDisplayOrder(connection, orderColumnName, newOrderByDataLabel);
            }
        }

        private void UpdateDisplayOrder(SQLiteConnection connection, string orderColumnName, Dictionary<string, int> newOrderByDataLabel)
        {
            // argument validation
            if (orderColumnName != Constant.ControlColumn.ControlOrder && orderColumnName != Constant.ControlColumn.SpreadsheetOrder)
            {
                throw new ArgumentOutOfRangeException(nameof(orderColumnName), String.Format("'{0}' is not a control order column.  Only '{1}' and '{2}' are order columns.", orderColumnName, Constant.ControlColumn.ControlOrder, Constant.ControlColumn.SpreadsheetOrder));
            }

            if (newOrderByDataLabel.Count != this.Controls.RowCount)
            {
                throw new NotSupportedException(String.Format("Partial order updates are not supported.  New ordering for {0} controls was passed but {1} controls are present for '{2}'.", newOrderByDataLabel.Count, this.Controls.RowCount, orderColumnName));
            }

            List<int> uniqueOrderValues = newOrderByDataLabel.Values.Distinct().ToList();
            if (uniqueOrderValues.Count != newOrderByDataLabel.Count)
            {
                throw new ArgumentException(String.Format("Each control must have a unique value for its order.  {0} duplicate values were passed for '{1}'.", newOrderByDataLabel.Count - uniqueOrderValues.Count, orderColumnName), nameof(newOrderByDataLabel));
            }

            uniqueOrderValues.Sort();
            for (int control = 0; control < uniqueOrderValues.Count; ++control)
            {
                int expectedOrder = control + 1;
                if (uniqueOrderValues[control] != expectedOrder)
                {
                    throw new ArgumentOutOfRangeException(nameof(newOrderByDataLabel), String.Format("Control order must be a ones based count.  An order of {0} was passed instead of the expected order {1} for '{2}'.", uniqueOrderValues[0], expectedOrder, orderColumnName), nameof(newOrderByDataLabel));
                }
            }

            // update in memory table with new order
            ColumnTuplesWithID controlsToUpdate = new ColumnTuplesWithID(Constant.DatabaseTable.Controls, orderColumnName);
            foreach (ControlRow control in this.Controls)
            {
                string dataLabel = control.DataLabel;
                int newOrder = newOrderByDataLabel[dataLabel];
                switch (orderColumnName)
                {
                    case Constant.ControlColumn.ControlOrder:
                        control.ControlOrder = newOrder;
                        break;
                    case Constant.ControlColumn.SpreadsheetOrder:
                        control.SpreadsheetOrder = newOrder;
                        break;
                    default:
                        throw new NotSupportedException(String.Format("Unhandled column '{0}'.", orderColumnName));
                }
                controlsToUpdate.Add(control.ID, newOrder);
            }

            // sync new order to database
            using (SQLiteTransaction transaction = connection.BeginTransaction())
            {
                controlsToUpdate.Update(connection, transaction);
                transaction.Commit();
            }

            // if the control order changed rebuild the in memory table to sort it in the new order
            if (orderColumnName == Constant.ControlColumn.ControlOrder)
            {
                this.GetControlsSortedByControlOrder(connection);
            }
        }

        protected virtual void OnDatabaseCreated(TemplateDatabase other)
        {
            SQLiteTableSchema controlTableSchema = ControlTable.CreateSchema();
            SQLiteTableSchema imageSetTableSchema = ImageSetTable.CreateSchema();
            using (SQLiteConnection connection = this.Database.CreateConnection())
            {
                using (SQLiteTransaction transaction = connection.BeginTransaction())
                {
                    controlTableSchema.CreateTableAndIndicies(connection, transaction);

                    imageSetTableSchema.CreateTableAndIndicies(connection, transaction);
                    ColumnTuplesForInsert imageSetRow = ImageSetRow.CreateInsert(Path.GetFileName(this.FolderPath));
                    imageSetRow.Insert(connection, transaction);

                    transaction.Commit();
                }

                // if an existing template database was passed, clone its contents into this database and populate the in memory table
                if (other != null)
                {
                    using (SQLiteTransaction transaction = connection.BeginTransaction())
                    {
                        ColumnTuplesForInsert controlsToAdd = ControlRow.CreateInsert(other.Controls);
                        controlsToAdd.Insert(connection, transaction);

                        ColumnTuplesWithID imageSet = other.ImageSet.CreateUpdate();
                        imageSet.Update(connection, transaction);

                        Version version;
                        using (SQLiteConnection otherConnection = other.Database.CreateConnection())
                        {
                            version = other.Database.GetUserVersion(otherConnection);
                        }
                        this.Database.SetUserVersion(connection, transaction, version);

                        transaction.Commit();
                    }

                    this.GetControlsSortedByControlOrder(connection);
                    this.LoadImageSet(connection);
                    return;
                }

                // no existing table to clone, so add standard controls to template table
                this.GetControlsSortedByControlOrder(connection);
                int controlOrder = 0; // one based count incremented for every new control

                // standard controls
                ControlRow file = new ControlRow(ControlType.Note, Constant.FileColumn.File, ++controlOrder)
                {
                    Copyable = false,
                    Label = Constant.FileColumn.File,
                    Tooltip = Constant.ControlDefault.FileTooltip
                };
                ControlRow relativePath = new ControlRow(ControlType.Note, Constant.FileColumn.RelativePath, ++controlOrder)
                {
                    Copyable = false,
                    Tooltip = Constant.ControlDefault.RelativePathTooltip
                };
                ControlRow dateTime = new ControlRow(ControlType.DateTime, Constant.FileColumn.DateTime, ++controlOrder)
                {
                    IndexInFileTable = true,
                    Tooltip = Constant.ControlDefault.DateTimeTooltip
                };
                ControlRow utcOffset = new ControlRow(ControlType.UtcOffset, Constant.FileColumn.UtcOffset, ++controlOrder)
                {
                    Tooltip = Constant.ControlDefault.UtcOffsetTooltip
                };
                ControlRow classification = new ControlRow(ControlType.FixedChoice, Constant.FileColumn.Classification, ++controlOrder)
                {
                    Copyable = false,
                    WellKnownValues = Constant.ControlDefault.ClassificationWellKnownValues,
                    Tooltip = Constant.ControlDefault.ClassificationTooltip
                };
                ControlRow deleteFlag = new ControlRow(ControlType.Flag, Constant.FileColumn.DeleteFlag, ++controlOrder)
                {
                    Copyable = false,
                    Label = Constant.ControlDefault.DeleteFlagLabel,
                    Tooltip = Constant.ControlDefault.DeleteFlagTooltip
                };

                // insert standard controls into the controls table
                ColumnTuplesForInsert controls = ControlRow.CreateInsert(new List<ControlRow>()
                {
                    file,
                    relativePath,
                    dateTime,
                    utcOffset,
                    classification,
                    deleteFlag
                });

                using (SQLiteTransaction transaction = connection.BeginTransaction())
                {
                    controls.Insert(connection, transaction);
                    this.Database.SetUserVersion(connection, transaction, Constant.Release.V2_2_0_3);

                    transaction.Commit();
                }

                // reload controls table to get updated IDs
                this.GetControlsSortedByControlOrder(connection);
                // ensure image set is available
                this.LoadImageSet(connection);
            }
        }

        protected virtual bool OnExistingDatabaseOpened(TemplateDatabase other)
        {
            using (SQLiteConnection connection = this.Database.CreateConnection())
            {
                List<string> tables = this.Database.GetTableNames(connection);
                if (tables.Contains(Constant.DatabaseTable.Controls, StringComparer.Ordinal) == false)
                {
                    // if no table named Controls, then this is a database from Timelapse rather than Carnassial
                    return false;
                }

                if (this.Database.GetUserVersion(connection) < Constant.Release.V2_2_0_3)
                {
                    this.UpdateControlTableTo2203Schema(connection);
                    using (SQLiteTransaction transaction = connection.BeginTransaction())
                    {
                        this.UpdateImageSetTo2203Schema(connection, transaction, tables);

                        if (other == null)
                        {
                            // if this is a template database being opened go ahead and update its version
                            // If it's a file database, don't update the database as the caller also needs to perform updates to complete
                            // version migration.
                            this.Database.SetUserVersion(connection, transaction, Constant.Release.V2_2_0_3);
                        }

                        transaction.Commit();
                    }
                }
                else
                {
                    this.GetControlsSortedByControlOrder(connection);
                }

                this.LoadImageSet(connection);
            }
            return true;
        }

        /// <summary>
        /// Set the order of the specified control to the specified value, shifting other controls' orders as needed.
        /// </summary>
        private void SetControlOrders(string dataLabel, int order)
        {
            if ((order < 1) || (order > this.Controls.RowCount))
            {
                throw new ArgumentOutOfRangeException(nameof(order), "Control and spreadsheet orders must be contiguous ones based values.");
            }

            Dictionary<string, int> newControlOrderByDataLabel = new Dictionary<string, int>(StringComparer.Ordinal);
            Dictionary<string, int> newSpreadsheetOrderByDataLabel = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (ControlRow control in this.Controls)
            {
                if (String.Equals(control.DataLabel, dataLabel, StringComparison.Ordinal))
                {
                    newControlOrderByDataLabel.Add(dataLabel, order);
                    newSpreadsheetOrderByDataLabel.Add(dataLabel, order);
                }
                else
                {
                    int currentControlOrder = control.ControlOrder;
                    if (currentControlOrder >= order)
                    {
                        ++currentControlOrder;
                    }
                    newControlOrderByDataLabel.Add(control.DataLabel, currentControlOrder);

                    int currentSpreadsheetOrder = control.SpreadsheetOrder;
                    if (currentSpreadsheetOrder >= order)
                    {
                        ++currentSpreadsheetOrder;
                    }
                    newSpreadsheetOrderByDataLabel.Add(control.DataLabel, currentSpreadsheetOrder);
                }
            }

            using (SQLiteConnection connection = this.Database.CreateConnection())
            {
                this.UpdateDisplayOrder(connection, Constant.ControlColumn.ControlOrder, newControlOrderByDataLabel);
                this.UpdateDisplayOrder(connection, Constant.ControlColumn.SpreadsheetOrder, newSpreadsheetOrderByDataLabel);
                this.GetControlsSortedByControlOrder(connection);
            }
        }

        private void SyncControlToDatabase(SQLiteConnection connection, ControlRow control)
        {
            Debug.Assert(control.HasChanges, "Unexpected call to sync control without changes.");

            ColumnTuplesWithID controlUpdate = control.CreateUpdate();
            controlUpdate.Update(connection);
            control.AcceptChanges();
        }

        private void UpdateControlTableTo2203Schema(SQLiteConnection connection)
        {
            // if this is a .tdb from Carnassial 2.2.0.2 or earlier
            // - insert AnalysisLabel column in controls table
            // - convert Copyable and Visible columns from text to boolean integers
            bool addAnalysisLabel = false;
            bool addIndex = false;
            bool convertCopyableAndVisibleToInteger = false;
            bool convertTypeToInteger = false;
            bool renameList = false;
            bool renameWidth = false;
            SQLiteTableSchema currentSchema = this.Database.GetTableSchema(connection, Constant.DatabaseTable.Controls);
            if (currentSchema.ColumnDefinitions.SingleOrDefault(column => column.Name == Constant.ControlColumn.AnalysisLabel) == null)
            {
                addAnalysisLabel = true;
            }
            if (currentSchema.ColumnDefinitions.SingleOrDefault(column => column.Name == Constant.ControlColumn.IndexInFileTable) == null)
            {
                addIndex = true;
            }

            if (String.Equals(currentSchema.ColumnDefinitions.Single(column => String.Equals(column.Name, Constant.ControlColumn.Copyable, StringComparison.Ordinal)).Type, Constant.SQLiteAffninity.Text, StringComparison.OrdinalIgnoreCase))
            {
                convertCopyableAndVisibleToInteger = true;
            }
            if (String.Equals(currentSchema.ColumnDefinitions.Single(column => String.Equals(column.Name, Constant.ControlColumn.Type, StringComparison.Ordinal)).Type, Constant.SQLiteAffninity.Text, StringComparison.OrdinalIgnoreCase))
            {
                convertTypeToInteger = true;
            }

            #pragma warning disable CS0618 // Type or member is obsolete
            if (currentSchema.ColumnDefinitions.SingleOrDefault(column => String.Equals(column.Name, Constant.ControlColumn.List, StringComparison.Ordinal)) != null)
            #pragma warning restore CS0618 // Type or member is obsolete
            {
                renameList = true;
            }
            #pragma warning disable CS0618 // Type or member is obsolete
            if (currentSchema.ColumnDefinitions.SingleOrDefault(column => String.Equals(column.Name, Constant.ControlColumn.Width, StringComparison.Ordinal)) != null)
            #pragma warning restore CS0618 // Type or member is obsolete
            {
                renameWidth = true;
            }

            if (addAnalysisLabel || addIndex || convertCopyableAndVisibleToInteger || convertTypeToInteger || renameList || renameWidth)
            {
                using (SQLiteTransaction transaction = connection.BeginTransaction())
                {
                    if (addAnalysisLabel)
                    {
                        ColumnDefinition analysisLabel = new ColumnDefinition(Constant.ControlColumn.AnalysisLabel, Constant.SQLiteAffninity.Integer)
                        {
                            DefaultValue = 0.ToString(),
                            NotNull = true
                        };
                        this.Database.AddColumnToTable(connection, transaction, Constant.DatabaseTable.Controls, 6, analysisLabel);
                    }
                    if (addIndex)
                    {
                        ColumnDefinition index = new ColumnDefinition(Constant.ControlColumn.IndexInFileTable, Constant.SQLiteAffninity.Integer)
                        {
                            DefaultValue = 0.ToString(),
                            NotNull = true
                        };
                        this.Database.AddColumnToTable(connection, transaction, Constant.DatabaseTable.Controls, 11, index);
                    }

                    if (convertCopyableAndVisibleToInteger)
                    {
                        this.Database.ConvertBooleanStringColumnToInteger(connection, transaction, Constant.DatabaseTable.Controls, Constant.ControlColumn.Copyable);
                        this.Database.ConvertBooleanStringColumnToInteger(connection, transaction, Constant.DatabaseTable.Controls, Constant.ControlColumn.Visible);
                    }
                    if (convertTypeToInteger)
                    {
                        this.Database.ConvertNonFlagEnumStringColumnToInteger<ControlType>(connection, transaction, Constant.DatabaseTable.Controls, Constant.ControlColumn.Type);
                    }

                    if (renameList)
                    {
                        #pragma warning disable CS0618 // Type or member is obsolete
                        this.Database.RenameColumn(connection, transaction, Constant.DatabaseTable.Controls, Constant.ControlColumn.List, Constant.ControlColumn.WellKnownValues, (ColumnDefinition columnWithNameChanged) => { });
                    }
                    if (renameWidth)
                    {
                        #pragma warning disable CS0618 // Type or member is obsolete
                        this.Database.RenameColumn(connection, transaction, Constant.DatabaseTable.Controls, Constant.ControlColumn.Width, Constant.ControlColumn.MaxWidth, (ColumnDefinition columnWithNameChanged) =>
                        #pragma warning restore CS0618 // Type or member is obsolete
                        {
                            columnWithNameChanged.DefaultValue = Constant.ControlDefault.MaxWidth.ToString();
                            columnWithNameChanged.NotNull = true;
                        });
                    }

                    transaction.Commit();
                }
            }

            this.GetControlsSortedByControlOrder(connection);

            // rename image quality control to classification
            #pragma warning disable CS0618 // Type or member is obsolete
            ControlRow imageQuality = this.Controls.SingleOrDefault(control => String.Equals(control.DataLabel, Constant.FileColumn.ImageQuality, StringComparison.Ordinal));
            #pragma warning restore CS0618 // Type or member is obsolete
            if (imageQuality != null)
            {
                imageQuality.DataLabel = Constant.FileColumn.Classification;
                this.SyncControlToDatabase(connection, imageQuality);
            }

            // update choices and tooltip for file classification to Carnassial 2.2.0.3 schema if they're not already set as such
            ControlRow classification = this.Controls[Constant.FileColumn.Classification];
            if (String.Equals(classification.WellKnownValues, Constant.ControlDefault.ClassificationWellKnownValues, StringComparison.Ordinal) == false)
            {
                imageQuality.Label = Constant.FileColumn.Classification;
                classification.Tooltip = Constant.ControlDefault.ClassificationTooltip;
                classification.WellKnownValues = Constant.ControlDefault.ClassificationWellKnownValues;
                this.SyncControlToDatabase(connection, classification);
            }

            // set index flag on date time control if it's not already set
            ControlRow dateTime = this.Controls[Constant.FileColumn.DateTime];
            if (dateTime.IndexInFileTable == false)
            {
                dateTime.IndexInFileTable = true;
                this.SyncControlToDatabase(connection, dateTime);
            }

            // update defaults for flags to Carnassial 2.2.0.3 schema if they're not already set as such
            foreach (ControlRow flag in this.Controls.Where(control => control.Type == ControlType.Flag))
            {
                if (String.Equals(flag.DefaultValue, Boolean.FalseString, StringComparison.OrdinalIgnoreCase))
                {
                    flag.DefaultValue = Constant.Sql.FalseString;
                }
                else if (String.Equals(flag.DefaultValue, Boolean.TrueString, StringComparison.OrdinalIgnoreCase))
                {
                    flag.DefaultValue = Constant.Sql.TrueString;
                }
                if (flag.HasChanges)
                {
                    this.SyncControlToDatabase(connection, flag);
                }
            }
        }

        private void UpdateImageSetTo2203Schema(SQLiteConnection connection, SQLiteTransaction transaction, List<string> tables)
        {
            if (tables.Contains(Constant.DatabaseTable.ImageSet, StringComparer.Ordinal) == false)
            {
                // create default ImageSet table if this is a .tdb from Carnassial 2.2.0.1 or earlier
                SQLiteTableSchema imageSetTableSchema = ImageSetTable.CreateSchema();
                imageSetTableSchema.CreateTableAndIndicies(connection, transaction);
                ColumnTuplesForInsert imageSetRow = ImageSetRow.CreateInsert(Path.GetFileName(this.FolderPath));
                imageSetRow.Insert(connection, transaction);
            }
            else
            {
                // ImageSetOptions is a [Flags] enum but can be treated as a non-flag enum as of Carnassial 2.2.0.3 as
                // its only values are None (0x0) and Magnifier (0x1).
                this.Database.ConvertNonFlagEnumStringColumnToInteger<FileSelection>(connection, transaction, Constant.DatabaseTable.ImageSet, Constant.ImageSetColumn.FileSelection);
                this.Database.ConvertNonFlagEnumStringColumnToInteger<ImageSetOptions>(connection, transaction, Constant.DatabaseTable.ImageSet, Constant.ImageSetColumn.Options);
            }
        }
    }
}
