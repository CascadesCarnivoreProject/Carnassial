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

        private void CreateImageSet(SQLiteConnection connection, SQLiteTransaction transaction)
        {
            List<ColumnDefinition> imageSetColumns = new List<ColumnDefinition>()
            {
                ColumnDefinition.CreatePrimaryKey(),
                new ColumnDefinition(Constant.DatabaseColumn.FileSelection, Constant.SqlColumnType.Text),
                new ColumnDefinition(Constant.DatabaseColumn.InitialFolderName, Constant.SqlColumnType.Text),
                new ColumnDefinition(Constant.DatabaseColumn.Log, Constant.SqlColumnType.Text),
                new ColumnDefinition(Constant.DatabaseColumn.Options, Constant.SqlColumnType.Text),
                new ColumnDefinition(Constant.DatabaseColumn.MostRecentFileID, Constant.SqlColumnType.Integer),
                new ColumnDefinition(Constant.DatabaseColumn.TimeZone, Constant.SqlColumnType.Text)
            };
            this.Database.CreateTable(connection, transaction, Constant.DatabaseTable.ImageSet, imageSetColumns);

            ColumnTuplesForInsert imageSetRow = ImageSetRow.CreateInsert(Path.GetFileName(this.FolderPath));
            imageSetRow.Insert(connection, transaction);
        }

        protected void GetControlsSortedByControlOrder(SQLiteConnection connection)
        {
            Select select = new Select(Constant.DatabaseTable.Controls)
            {
                OrderBy = Constant.Control.ControlOrder
            };

            this.Database.LoadDataTableFromSelect(this.Controls, connection, select);
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

                if (Constant.DatabaseColumn.ID != dataLabel)
                {
                    dataLabels.Add(dataLabel);
                }
            }
            return dataLabels;
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

            if (Constant.Control.StandardControls.Contains(controlToRemove.DataLabel))
            {
                throw new NotSupportedException(String.Format("Standard control {0} cannot be removed.", controlToRemove.DataLabel));
            }

            // capture state
            long removedControlOrder = controlToRemove.ControlOrder;
            long removedSpreadsheetOrder = controlToRemove.SpreadsheetOrder;

            // regenerate counter and spreadsheet orders; if they're greater than the one removed, decrement
            ColumnTuplesWithID controlOrderUpdates = new ColumnTuplesWithID(Constant.DatabaseTable.Controls, Constant.Control.ControlOrder);
            ColumnTuplesWithID spreadsheetOrderUpdates = new ColumnTuplesWithID(Constant.DatabaseTable.Controls, Constant.Control.SpreadsheetOrder);
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
            ColumnTuplesWithID controlUpdate = control.CreateUpdate();
            using (SQLiteConnection connection = this.Database.CreateConnection())
            {
                controlUpdate.Update(connection);
            }
            control.AcceptChanges();
            return true;
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

        public void UpdateDisplayOrder(string orderColumnName, Dictionary<string, long> newOrderByDataLabel)
        {
            using (SQLiteConnection connection = this.Database.CreateConnection())
            {
                this.UpdateDisplayOrder(connection, orderColumnName, newOrderByDataLabel);
            }
        }

        private void UpdateDisplayOrder(SQLiteConnection connection, string orderColumnName, Dictionary<string, long> newOrderByDataLabel)
        {
            // argument validation
            if (orderColumnName != Constant.Control.ControlOrder && orderColumnName != Constant.Control.SpreadsheetOrder)
            {
                throw new ArgumentOutOfRangeException(nameof(orderColumnName), String.Format("'{0}' is not a control order column.  Only '{1}' and '{2}' are order columns.", orderColumnName, Constant.Control.ControlOrder, Constant.Control.SpreadsheetOrder));
            }

            if (newOrderByDataLabel.Count != this.Controls.RowCount)
            {
                throw new NotSupportedException(String.Format("Partial order updates are not supported.  New ordering for {0} controls was passed but {1} controls are present for '{2}'.", newOrderByDataLabel.Count, this.Controls.RowCount, orderColumnName));
            }

            List<long> uniqueOrderValues = newOrderByDataLabel.Values.Distinct().ToList();
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
                controlsToUpdate.Add(control.ID, newOrder);
            }

            // sync new order to database
            using (SQLiteTransaction transaction = connection.BeginTransaction())
            {
                controlsToUpdate.Update(connection, transaction);
                transaction.Commit();
            }

            // if the control order changed rebuild the in memory table to sort it in the new order
            if (orderColumnName == Constant.Control.ControlOrder)
            {
                this.GetControlsSortedByControlOrder(connection);
            }
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

        protected virtual void OnDatabaseCreated(TemplateDatabase other)
        {
            using (SQLiteConnection connection = this.Database.CreateConnection())
            {
                using (SQLiteTransaction transaction = connection.BeginTransaction())
                {
                    // create Controls table
                    List<ColumnDefinition> templateTableColumns = new List<ColumnDefinition>()
                    {
                        ColumnDefinition.CreatePrimaryKey(),
                        new ColumnDefinition(Constant.Control.ControlOrder, Constant.SqlColumnType.Integer)
                        {
                            NotNull = true
                        },
                        new ColumnDefinition(Constant.Control.SpreadsheetOrder, Constant.SqlColumnType.Integer)
                        {
                            NotNull = true
                        },
                        new ColumnDefinition(Constant.Control.Type, Constant.SqlColumnType.Text)
                        {
                            NotNull = true
                        },
                        new ColumnDefinition(Constant.Control.DefaultValue, Constant.SqlColumnType.Text),
                        new ColumnDefinition(Constant.Control.Label, Constant.SqlColumnType.Text),
                        new ColumnDefinition(Constant.Control.DataLabel, Constant.SqlColumnType.Text)
                        {
                            NotNull = true
                        },
                        new ColumnDefinition(Constant.Control.AnalysisLabel, Constant.SqlColumnType.Integer)
                        {
                            DefaultValue = 0.ToString(),
                            NotNull = true
                        },
                        new ColumnDefinition(Constant.Control.Tooltip, Constant.SqlColumnType.Text),
                        new ColumnDefinition(Constant.Control.Width, Constant.SqlColumnType.Integer),
                        new ColumnDefinition(Constant.Control.Copyable, Constant.SqlColumnType.Text)
                        {
                            NotNull = true
                        },
                        new ColumnDefinition(Constant.Control.Visible, Constant.SqlColumnType.Text)
                        {
                            NotNull = true
                        },
                        new ColumnDefinition(Constant.Control.List, Constant.SqlColumnType.Text)
                    };
                    this.Database.CreateTable(connection, transaction, Constant.DatabaseTable.Controls, templateTableColumns);

                    // create ImageSet table and populate with default row
                    this.CreateImageSet(connection, transaction);

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

                        transaction.Commit();
                    }

                    this.GetControlsSortedByControlOrder(connection);
                    this.LoadImageSet(connection);
                    return;
                }

                // no existing table to clone, so add standard controls to template table
                this.GetControlsSortedByControlOrder(connection);
                long controlOrder = 0; // one based count incremented for every new control

                // standard controls
                ControlRow file = new ControlRow(ControlType.Note, Constant.DatabaseColumn.File, ++controlOrder)
                {
                    Label = Constant.DatabaseColumn.File,
                    Tooltip = Constant.ControlDefault.FileTooltip,
                    Copyable = false,
                };
                ControlRow relativePath = new ControlRow(ControlType.Note, Constant.DatabaseColumn.RelativePath, ++controlOrder)
                {
                    Tooltip = Constant.ControlDefault.RelativePathTooltip,
                    Copyable = false,
                };
                ControlRow dateTime = new ControlRow(ControlType.DateTime, Constant.DatabaseColumn.DateTime, ++controlOrder);
                ControlRow utcOffset = new ControlRow(ControlType.UtcOffset, Constant.DatabaseColumn.UtcOffset, ++controlOrder);
                ControlRow imageQuality = new ControlRow(ControlType.FixedChoice, Constant.DatabaseColumn.ImageQuality, ++controlOrder)
                {
                    List = Constant.ControlDefault.ImageQualityList,
                    Tooltip = Constant.ControlDefault.ImageQualityTooltip,
                    Copyable = false,
                };
                ControlRow deleteFlag = new ControlRow(ControlType.Flag, Constant.DatabaseColumn.DeleteFlag, ++controlOrder)
                {
                    Label = Constant.ControlDefault.DeleteFlagLabel,
                    Tooltip = Constant.ControlDefault.DeleteFlagTooltip,
                    Copyable = false,
                };

                // insert standard controls into the database
                ColumnTuplesForInsert controls = ControlRow.CreateInsert(new List<ControlRow>()
                {
                    file,
                    relativePath,
                    dateTime,
                    utcOffset,
                    imageQuality,
                    deleteFlag
                });
                controls.Insert(connection);

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
                if (tables.Contains(Constant.DatabaseTable.Controls) == false)
                {
                    return false;
                }

                // insert AnalysisLabel column in controls table if this is a .tdb from Carnassial 2.2.0.2 or earlier
                List<ColumnDefinition> controlColumns = this.Database.GetColumnDefinitions(connection, Constant.DatabaseTable.Controls);
                if (controlColumns.SingleOrDefault(column => column.Name == Constant.Control.AnalysisLabel) == null)
                {
                    ColumnDefinition analysisLabel = new ColumnDefinition(Constant.Control.AnalysisLabel, Constant.SqlColumnType.Integer)
                    {
                        DefaultValue = 0.ToString(),
                        NotNull = true
                    };
                    using (SQLiteTransaction transaction = connection.BeginTransaction())
                    {
                        this.Database.AddColumnToTable(connection, transaction, Constant.DatabaseTable.Controls, 6, analysisLabel);
                        transaction.Commit();
                    }
                }

                this.GetControlsSortedByControlOrder(connection);

                // update choices for file classification to Carnassial 2.2.0.3 schema if they're not already set as such
                ControlRow classification = this.FindControl(Constant.DatabaseColumn.ImageQuality);
                if (String.Equals(classification.List, Constant.ControlDefault.ImageQualityList, StringComparison.Ordinal) == false)
                {
                    classification.List = Constant.ControlDefault.ImageQualityList;
                    ColumnTuplesWithID classificationUpdate = classification.CreateUpdate();
                    classificationUpdate.Update(connection);
                }

                // create ImageSet table if this is a .tdb from Carnassial 2.2.0.1 or earlier
                if (tables.Contains(Constant.DatabaseTable.ImageSet) == false)
                {
                    using (SQLiteTransaction transaction = connection.BeginTransaction())
                    {
                        this.CreateImageSet(connection, transaction);
                        transaction.Commit();
                    }
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

            using (SQLiteConnection connection = this.Database.CreateConnection())
            {
                this.UpdateDisplayOrder(connection, Constant.Control.ControlOrder, newControlOrderByDataLabel);
                this.UpdateDisplayOrder(connection, Constant.Control.SpreadsheetOrder, newSpreadsheetOrderByDataLabel);
                this.GetControlsSortedByControlOrder(connection);
            }
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
            while (dataLabels.Contains(nextDataLabel))
            {
                ++dataLabelUniqueIdentifier;
                nextDataLabel = dataLabelPrefix + dataLabelUniqueIdentifier.ToString();
            }

            return nextDataLabel;
        }
    }
}
