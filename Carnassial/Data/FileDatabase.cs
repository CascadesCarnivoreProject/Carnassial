﻿using Carnassial.Control;
using Carnassial.Database;
using Carnassial.Util;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using ColumnDefinition = Carnassial.Database.ColumnDefinition;

namespace Carnassial.Data
{
    public class FileDatabase : TemplateDatabase
    {
        private bool disposed;

        public List<string> ControlSynchronizationIssues { get; private set; }

        public CustomSelection CustomSelection { get; set; }

        /// <summary>Gets the file name of the database on disk.</summary>
        public string FileName { get; private set; }

        public Dictionary<string, ControlRow> ControlsByDataLabel { get; private set; }

        // contains the results of the data query
        public FileTable Files { get; private set; }

        // contains the markers
        public DataTableBackedList<MarkerRow> Markers { get; private set; }

        public bool OrderFilesByDateTime { get; set; }

        private FileDatabase(string filePath)
            : base(filePath)
        {
            this.ControlSynchronizationIssues = new List<string>();
            this.disposed = false;
            this.FileName = Path.GetFileName(filePath);
            this.ControlsByDataLabel = new Dictionary<string, ControlRow>();
            this.OrderFilesByDateTime = false;
        }

        /// <summary>Gets the number of files currently in the files table.</summary>
        public int CurrentlySelectedFileCount
        {
            get { return this.Files.RowCount; }
        }

        public void AdjustFileTimes(TimeSpan adjustment)
        {
            this.AdjustFileTimes(adjustment, 0, this.CurrentlySelectedFileCount - 1);
        }

        public void AdjustFileTimes(TimeSpan adjustment, int startIndex, int endIndex)
        {
            this.AdjustFileTimes((DateTimeOffset fileTime) => { return fileTime + adjustment; }, startIndex, endIndex);
        }

        // invoke the passed function to modify the DateTime field over the specified range of files
        public void AdjustFileTimes(Func<DateTimeOffset, DateTimeOffset> adjustment, int startIndex, int endIndex)
        {
            if (this.IsFileRowInRange(startIndex) == false)
            {
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            }
            if (this.IsFileRowInRange(endIndex) == false)
            {
                throw new ArgumentOutOfRangeException(nameof(endIndex));
            }
            if (endIndex < startIndex)
            {
                throw new ArgumentOutOfRangeException(nameof(endIndex), "End must be greater than or equal to start index.");
            }
            if (this.CurrentlySelectedFileCount == 0)
            {
                return;
            }

            // modify date/times
            List<ImageRow> filesToAdjust = new List<ImageRow>();
            TimeSpan mostRecentAdjustment = TimeSpan.Zero;
            for (int row = startIndex; row <= endIndex; ++row)
            {
                ImageRow file = this.Files[row];
                Debug.Assert(file.HasChanges == false, "File has unexpected pending changes.");
                DateTimeOffset currentImageDateTime = file.DateTimeOffset;

                // adjust the date/time
                DateTimeOffset newFileDateTime = adjustment.Invoke(currentImageDateTime);
                if (newFileDateTime == currentImageDateTime)
                {
                    continue;
                }

                mostRecentAdjustment = newFileDateTime - currentImageDateTime;
                file.DateTimeOffset = newFileDateTime;
                filesToAdjust.Add(file);
                file.AcceptChanges();
            }

            // update the database with the new date/times
            if (filesToAdjust.Count > 0)
            {
                this.CreateBackupIfNeeded();
                this.UpdateFiles(ImageRow.CreateDateTimeUpdate(filesToAdjust));

                // add log entry recording the change
                StringBuilder log = new StringBuilder(Environment.NewLine);
                log.AppendFormat("System entry: Adjusted dates and times of {0} selected files.{1}", filesToAdjust.Count, Environment.NewLine);
                log.AppendFormat("The first file adjusted was '{0}', the last '{1}', and the last file was adjusted by {2}.{3}", filesToAdjust[0].FileName, filesToAdjust[filesToAdjust.Count - 1].FileName, mostRecentAdjustment, Environment.NewLine);
                this.AppendToImageSetLog(log);
            }
        }

        public void AppendToImageSetLog(StringBuilder logEntry)
        {
            this.ImageSet.Log += logEntry;
            this.SyncImageSetToDatabase();
        }

        public AddFilesTransaction CreateAddFilesTransaction()
        {
            this.CreateBackupIfNeeded();
            return new AddFilesTransaction(this, this.Database.CreateConnection());
        }

        private ColumnDefinition CreateFileDataColumnDefinition(ControlRow control)
        {
            if (control.DataLabel == Constant.DatabaseColumn.DateTime)
            {
                return new ColumnDefinition(control.DataLabel, Constant.ControlDefault.DateTimeValue.DateTime);
            }
            if (control.DataLabel == Constant.DatabaseColumn.UtcOffset)
            {
                // UTC offsets are typically represented as TimeSpans but the least awkward way to store them in SQLite is as a real column containing the offset in
                // hours.  This is because SQLite
                // - handles TIME columns as DateTime rather than TimeSpan, requiring the associated DataTable column also be of type DateTime
                // - doesn't support negative values in time formats, requiring offsets for time zones west of Greenwich be represented as positive values
                // - imposes an upper bound of 24 hours on time formats, meaning the 26 hour range of UTC offsets (UTC-12 to UTC+14) cannot be accomodated
                // - lacks support for DateTimeOffset, so whilst offset information can be written to the database it cannot be read from the database as .NET
                //   supports only DateTimes whose offset matches the current system time zone
                // Storing offsets as ticks, milliseconds, seconds, minutes, or days offers equivalent functionality.  Potential for rounding error in roundtrip 
                // calculations on offsets is similar to hours for all formats other than an INTEGER (long) column containing ticks.  Ticks are a common 
                // implementation choice but testing shows no roundoff errors at single tick precision (100 nanoseconds) when using hours.  Even with TimeSpans 
                // near the upper bound of 256M hours, well beyond the plausible range of time zone calculations.  So there does not appear to be any reason to 
                // avoid using hours for readability when working with the database directly.
                return new ColumnDefinition(control.DataLabel, Constant.ControlDefault.DateTimeValue.Offset);
            }
            if (String.IsNullOrWhiteSpace(control.DefaultValue))
            {
                return new ColumnDefinition(control.DataLabel, Constant.SqlColumnType.Text);
            }
            return new ColumnDefinition(control.DataLabel, Constant.SqlColumnType.Text, control.DefaultValue);
        }

        public UpdateFileDateTimeOffsetTransaction CreateUpdateDateTimeTransaction()
        {
            this.CreateBackupIfNeeded();
            return new UpdateFileDateTimeOffsetTransaction(this.Database.CreateConnection());
        }

        public UpdateFileColumnTransaction CreateUpdateSingleColumnTransaction(string dataLabel)
        {
            this.CreateBackupIfNeeded();
            return new UpdateFileColumnTransaction(dataLabel, this.Database.CreateConnection());
        }

        private Select CreateSelect(FileSelection selection)
        {
            Select select = new Select(Constant.DatabaseTable.FileData);
            switch (selection)
            {
                case FileSelection.All:
                    break;
                case FileSelection.Corrupt:
                case FileSelection.Dark:
                case FileSelection.NoLongerAvailable:
                case FileSelection.Ok:
                    select.Where.Add(new WhereClause(Constant.DatabaseColumn.ImageQuality, Constant.SqlOperator.Equal, selection.ToString()));
                    break;
                case FileSelection.MarkedForDeletion:
                    select.Where.Add(new WhereClause(Constant.DatabaseColumn.DeleteFlag, Constant.SqlOperator.Equal, Boolean.TrueString));
                    break;
                case FileSelection.Custom:
                    return this.CustomSelection.CreateSelect();
                default:
                    throw new NotSupportedException(String.Format("Unhandled selection {0}.", selection));
            }
            return select;
        }

        // Delete the data (including markers) associated with the files identified by the list of IDs.
        public void DeleteFilesAndMarkers(List<long> fileIDs)
        {
            if (fileIDs.Count < 1)
            {
                // nothing to do
                return;
            }

            this.CreateBackupIfNeeded();
            using (SQLiteConnection connection = this.Database.CreateConnection())
            {
                using (SQLiteTransaction transaction = connection.BeginTransaction())
                {
                    SQLiteCommand deleteFiles = new SQLiteCommand(String.Format("DELETE FROM {0} WHERE {1} = @Id", Constant.DatabaseTable.FileData, Constant.DatabaseColumn.ID), connection, transaction);
                    SQLiteCommand deleteMarkers = new SQLiteCommand(String.Format("DELETE FROM {0} WHERE {1} = @Id", Constant.DatabaseTable.Markers, Constant.DatabaseColumn.ID), connection, transaction);
                    try
                    {
                        SQLiteParameter id = new SQLiteParameter("@Id");
                        deleteFiles.Parameters.Add(id);
                        deleteMarkers.Parameters.Add(id);

                        foreach (long fileID in fileIDs)
                        {
                            id.Value = fileID;
                            deleteFiles.ExecuteNonQuery();
                            deleteMarkers.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                    finally
                    {
                        deleteFiles.Dispose();
                        deleteMarkers.Dispose();
                    }
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                if (this.Markers != null)
                {
                    this.Markers.Dispose();
                }
            }

            base.Dispose(disposing);
            this.disposed = true;
        }

        // Update all the date fields between the start and end index by swapping the days and months.
        public void ExchangeDayAndMonthInFileDates(int startRow, int endRow)
        {
            if (this.IsFileRowInRange(startRow) == false)
            {
                throw new ArgumentOutOfRangeException(nameof(startRow));
            }
            if (this.IsFileRowInRange(endRow) == false)
            {
                throw new ArgumentOutOfRangeException(nameof(endRow));
            }
            if (endRow < startRow)
            {
                throw new ArgumentOutOfRangeException(nameof(endRow), "endRow must be greater than or equal to startRow.");
            }
            if (this.CurrentlySelectedFileCount == 0)
            {
                return;
            }

            // swap day and month for each file where it's possible
            List<ImageRow> filesToUpdate = new List<ImageRow>();
            ImageRow firstFile = this.Files[startRow];
            ImageRow lastFile = null;
            DateTimeOffset mostRecentOriginalDateTime = DateTime.MinValue;
            DateTimeOffset mostRecentReversedDateTime = DateTime.MinValue;
            for (int row = startRow; row <= endRow; row++)
            {
                ImageRow file = this.Files[row];
                DateTimeOffset originalDateTime = file.DateTimeOffset;
                if (DateTimeHandler.TrySwapDayMonth(originalDateTime, out DateTimeOffset reversedDateTime) == false)
                {
                    continue;
                }

                // update in memory table with the new datetime
                file.DateTimeOffset = reversedDateTime;
                filesToUpdate.Add(file);
                lastFile = file;
                mostRecentOriginalDateTime = originalDateTime;
                mostRecentReversedDateTime = reversedDateTime;
            }

            // update database with new datetimes
            if (filesToUpdate.Count > 0)
            {
                this.CreateBackupIfNeeded();
                FileTuplesWithID dateTimeUpdate = ImageRow.CreateDateTimeUpdate(filesToUpdate);
                using (SQLiteConnection connection = this.Database.CreateConnection())
                {
                    dateTimeUpdate.Update(connection);
                }

                StringBuilder log = new StringBuilder(Environment.NewLine);
                log.AppendFormat("System entry: Swapped days and months for {0} files.{1}", filesToUpdate.Count, Environment.NewLine);
                log.AppendFormat("The first file adjusted was '{0}' and the last '{1}'.{2}", firstFile.FileName, lastFile.FileName, Environment.NewLine);
                log.AppendFormat("The last file's date was changed from '{0}' to '{1}'.{2}", DateTimeHandler.ToDisplayDateString(mostRecentOriginalDateTime), DateTimeHandler.ToDisplayDateString(mostRecentReversedDateTime), Environment.NewLine);
                this.AppendToImageSetLog(log);
            }
        }

        // Find the next displayable file at or after the provided row in the current image set.
        // If there is no next displayable file, then find the closest previous file before the provided row that is displayable.
        public int GetCurrentOrNextDisplayableFile(int startIndex)
        {
            for (int index = startIndex; index < this.CurrentlySelectedFileCount; index++)
            {
                if (this.IsFileDisplayable(index))
                {
                    return index;
                }
            }
            for (int index = startIndex - 1; index >= 0; index--)
            {
                if (this.IsFileDisplayable(index))
                {
                    return index;
                }
            }
            return -1;
        }

        /// <summary>
        /// Find the file whose ID is closest to the provided ID in the current image set. 
        /// </summary>
        /// <returns>If the passed ID does not exist, the index of the file with and ID just greater than the provided one.  Or, if no greater ID, the index
        /// of the file with the ID.</returns>
        public int GetFileOrNextFileIndex(long fileID)
        {
            // try primary key lookup first as typically the requested ID will be present in the data table
            // (ideally the caller could use the ImageRow found directly, but this doesn't compose with index based navigation)
            if (this.Files.TryFind(fileID, out ImageRow file, out int fileIndex))
            {
                return fileIndex;
            }

            // when sorted by ID ascending so an inexact binary search works
            // Sorting by datetime is usually identical to ID sorting in single camera image sets, so ignoring this.OrderFilesByDateTime has no effect in 
            // simple cases.  In complex, multi-camera image sets it will be wrong but typically still tend to select a plausibly reasonable file rather
            // than a ridiculous one.  But no datetime seed is available if direct ID lookup fails.  Thw API can be reworked to provide a datetime hint
            // if this proves too troublesome.
            int firstIndex = 0;
            int lastIndex = this.CurrentlySelectedFileCount - 1;
            while (firstIndex <= lastIndex)
            {
                int midpointIndex = (firstIndex + lastIndex) / 2;
                file = this.Files[midpointIndex];
                long midpointID = file.ID;

                if (fileID > midpointID)
                {
                    // look at higher index partition next
                    firstIndex = midpointIndex + 1;
                }
                else if (fileID < midpointID)
                {
                    // look at lower index partition next
                    lastIndex = midpointIndex - 1;
                }
                else
                {
                    // found the ID closest to fileID
                    return midpointIndex;
                }
            }

            // all IDs in the selection are smaller than fileID
            if (firstIndex >= this.CurrentlySelectedFileCount)
            {
                return this.CurrentlySelectedFileCount - 1;
            }

            // all IDs in the selection are larger than fileID
            return firstIndex;
        }

        public FileTable GetFilesMarkedForDeletion()
        {
            Select select = new Select(Constant.DatabaseTable.FileData, new WhereClause(Constant.DatabaseColumn.DeleteFlag, Constant.SqlOperator.Equal, Boolean.TrueString));
            using (SQLiteConnection connection = this.Database.CreateConnection())
            {
                return this.Files = this.Database.GetDataTableFromSelect<FileTable>(connection, select);
            }
        }

        public List<string> GetDistinctValuesInFileDataColumn(string dataLabel)
        {
            List<string> distinctValues = new List<string>();
            foreach (object value in this.Database.GetDistinctValuesInColumn(Constant.DatabaseTable.FileData, dataLabel))
            {
                distinctValues.Add(value.ToString());
            }
            return distinctValues;
        }

        public int GetFileCount(FileSelection fileSelection)
        {
            using (SQLiteConnection connection = this.Database.CreateConnection())
            {
                return this.GetFileCount(connection, fileSelection);
            }
        }

        private int GetFileCount(SQLiteConnection connection, FileSelection selection)
        {
            Select select = this.CreateSelect(selection);
            if ((select.Where.Count < 1) && (selection == FileSelection.Custom))
            {
                // if no search terms are active the file count is undefined as no filtering is in operation
                return -1;
            }
            // otherwise, the query is for all files as no where clause is present
            return (int)select.Count(connection);
        }

        public Dictionary<FileSelection, int> GetFileCountsBySelection()
        {
            Dictionary<FileSelection, int> counts = new Dictionary<FileSelection, int>(4);
            using (SQLiteConnection connection = this.Database.CreateConnection())
            {
                counts[FileSelection.Dark] = this.GetFileCount(connection, FileSelection.Dark);
                counts[FileSelection.Corrupt] = this.GetFileCount(connection, FileSelection.Corrupt);
                counts[FileSelection.NoLongerAvailable] = this.GetFileCount(connection, FileSelection.NoLongerAvailable);
                counts[FileSelection.Ok] = this.GetFileCount(connection, FileSelection.Ok);
            }
            return counts;
        }

        public void GetMarkers(SQLiteConnection connection)
        {
            this.Markers = new DataTableBackedList<MarkerRow>(this.Database.GetDataTableFromSelect(connection, new Select(Constant.DatabaseTable.Markers)), (DataRow row) => { return new MarkerRow(row); });
        }

        /// <summary>A convenience routine for checking to see if the file in the given row is displayable (i.e., not corrupted or missing)</summary>
        public bool IsFileDisplayable(int fileIndex)
        {
            if (this.IsFileRowInRange(fileIndex) == false)
            {
                return false;
            }

            return this.Files[fileIndex].IsDisplayable();
        }

        public bool IsFileRowInRange(int fileIndex)
        {
            return (fileIndex >= 0) && (fileIndex < this.CurrentlySelectedFileCount) ? true : false;
        }

        public List<string> MoveSelectedFilesToFolder(string destinationFolderPath)
        {
            Debug.Assert(destinationFolderPath.StartsWith(this.FolderPath, StringComparison.OrdinalIgnoreCase), String.Format("Destination path '{0}' is not under '{1}'.", destinationFolderPath, this.FolderPath));

            FileTuplesWithID filesToUpdate = new FileTuplesWithID(Constant.DatabaseColumn.RelativePath);
            List<string> immovableFiles = new List<string>();
            foreach (ImageRow file in this.Files)
            {
                Debug.Assert(file.HasChanges == false, "File has unexpected pending changes.");
                if (file.TryMoveFileToFolder(this.FolderPath, destinationFolderPath))
                {
                    filesToUpdate.Add(file.ID, file.RelativePath);
                    file.AcceptChanges();
                }
                else
                {
                    immovableFiles.Add(file.GetRelativePath());
                }
            }

            this.UpdateFiles(filesToUpdate);
            return immovableFiles;
        }

        /// <summary>
        /// Make an empty Data Table based on the information in the Template Table.
        /// Assumes that the database has already been opened and that the Template Table is loaded, where the data label always has a valid value.
        /// Then create both the ImageSet table and the Markers table
        /// </summary>
        protected override void OnDatabaseCreated(TemplateDatabase templateDatabase)
        {
            // copy the template's controls and image set table
            base.OnDatabaseCreated(templateDatabase);

            // derive FileData schema from the controls defined
            List<ColumnDefinition> fileDataColumns = new List<ColumnDefinition>() { ColumnDefinition.CreatePrimaryKey() };
            foreach (ControlRow control in this.Controls)
            {
                fileDataColumns.Add(this.CreateFileDataColumnDefinition(control));
            }

            using (SQLiteConnection connection = this.Database.CreateConnection())
            {
                // create Files and Markers tables
                using (SQLiteTransaction transaction = connection.BeginTransaction())
                {
                    // create FileData table and index the DateTime column
                    this.Database.CreateTable(connection, transaction, Constant.DatabaseTable.FileData, fileDataColumns);
                    string createIndex = String.Format("CREATE INDEX 'FileDateTimeIndex' ON '{0}' ('{1}')", Constant.DatabaseTable.FileData, Constant.DatabaseColumn.DateTime);
                    using (SQLiteCommand command = new SQLiteCommand(createIndex, connection, transaction))
                    {
                        command.ExecuteNonQuery();
                    }

                    // create the Markers table
                    List<ColumnDefinition> markerColumns = new List<ColumnDefinition>() { ColumnDefinition.CreatePrimaryKey() };
                    string type = String.Empty;
                    foreach (ControlRow control in this.Controls)
                    {
                        if (control.Type == ControlType.Counter)
                        {
                            markerColumns.Add(new ColumnDefinition(control.DataLabel, Constant.SqlColumnType.Text, String.Empty));
                        }
                    }
                    this.Database.CreateTable(connection, transaction, Constant.DatabaseTable.Markers, markerColumns);

                    transaction.Commit();
                }

                // load in memory Files and Markers tables
                this.GetMarkers(connection);
                this.SelectFiles(connection, this.ImageSet.FileSelection);
            }
        }

        protected override bool OnExistingDatabaseOpened(TemplateDatabase templateDatabase)
        {
            // perform Controls and ImageSet initializations and migrations, then check for synchronization issues
            if (base.OnExistingDatabaseOpened(templateDatabase) == false)
            {
                return false;
            }

            List<string> templateDataLabels = templateDatabase.GetDataLabelsExceptIDInSpreadsheetOrder();
            List<string> dataLabels = this.GetDataLabelsExceptIDInSpreadsheetOrder();
            List<string> dataLabelsInTemplateButNotFileDatabase = templateDataLabels.Except(dataLabels).ToList();
            foreach (string dataLabel in dataLabelsInTemplateButNotFileDatabase)
            {
                this.ControlSynchronizationIssues.Add("- A field with data label '" + dataLabel + "' was found in the template, but nothing matches that in the file database." + Environment.NewLine);
            }
            List<string> dataLabelsInIFileButNotTemplateDatabase = dataLabels.Except(templateDataLabels).ToList();
            foreach (string dataLabel in dataLabelsInIFileButNotTemplateDatabase)
            {
                this.ControlSynchronizationIssues.Add("- A field with data label '" + dataLabel + "' was found in the file database, but nothing matches that in the template." + Environment.NewLine);
            }

            if (this.ControlSynchronizationIssues.Count == 0)
            {
                foreach (string dataLabel in dataLabels)
                {
                    ControlRow fileDatabaseControl = this.FindControl(dataLabel);
                    ControlRow templateControl = templateDatabase.FindControl(dataLabel);

                    if (fileDatabaseControl.Type != templateControl.Type)
                    {
                        this.ControlSynchronizationIssues.Add(String.Format("- Field with data label '{0}' is of type '{1}' in the file database but of type '{2}' in the template.{3}", dataLabel, fileDatabaseControl.Type, templateControl.Type, Environment.NewLine));
                    }

                    if (fileDatabaseControl.Type == ControlType.FixedChoice)
                    {
                        List<string> fileDatabaseChoices = fileDatabaseControl.GetChoices();
                        List<string> templateChoices = templateControl.GetChoices();
                        List<string> choiceValuesRemovedInTemplate = fileDatabaseChoices.Except(templateChoices).ToList();
                        foreach (string removedValue in choiceValuesRemovedInTemplate)
                        {
                            this.ControlSynchronizationIssues.Add(String.Format("- Choice with data label '{0}' allows the value '{1}' in the file database but not in the template.{2}", dataLabel, removedValue, Environment.NewLine));
                        }
                    }
                }
            }

            // if there are no synchronization difficulties synchronize the file database's TemplateTable with the template's TemplateTable
            using (SQLiteConnection connection = this.Database.CreateConnection())
            {
                if (this.ControlSynchronizationIssues.Count == 0)
                {
                    foreach (string dataLabel in dataLabels)
                    {
                        ControlRow fileDatabaseControl = this.FindControl(dataLabel);
                        ControlRow templateControl = templateDatabase.FindControl(dataLabel);
                        if (fileDatabaseControl.Synchronize(templateControl))
                        {
                            this.SyncControlToDatabase(fileDatabaseControl);
                        }
                    }
                }

                // load in memory Files and Markers tables
                this.SelectFiles(connection, this.ImageSet.FileSelection);
                this.GetMarkers(connection);
            }

            // return true if there are synchronization issues as the database was still opened successfully
            return true;
        }

        public void RenameFile(string newFileName)
        {
            if (File.Exists(Path.Combine(this.FolderPath, this.FileName)))
            {
                File.Move(Path.Combine(this.FolderPath, this.FileName), Path.Combine(this.FolderPath, newFileName));
                this.FileName = newFileName;
                this.Database = new SQLiteDatabase(Path.Combine(this.FolderPath, newFileName));
            }
        }

        /// <summary> 
        /// Rebuild the file table with all files in the database table which match the specified selection.
        /// </summary>
        // performance of    time to load 10k files
        // DataTable.Load()  326ms
        // List<object>      200ms
        public void SelectFiles(FileSelection selection)
        {
            // Stopwatch stopwatch = new Stopwatch();
            // stopwatch.Start();
            using (SQLiteConnection connection = this.Database.CreateConnection())
            {
                this.SelectFiles(connection, selection);
            }
            // stopwatch.Stop();
            // Trace.WriteLine(stopwatch.Elapsed.ToString("s\\.fffffff"));
        }

        private void SelectFiles(SQLiteConnection connection, FileSelection selection)
        {
            Select select = this.CreateSelect(selection);
            if (this.OrderFilesByDateTime)
            {
                select.OrderBy = Constant.DatabaseColumn.DateTime;
            }
            this.Files = this.Database.GetDataTableFromSelect<FileTable>(connection, select);

            // persist the current selection
            this.ImageSet.FileSelection = selection;
        }

        public void SyncFileToDatabase(ImageRow file)
        {
            this.UpdateFiles(file.CreateUpdate());
        }

        /// <summary>
        /// Set the list of markers in the marker table. 
        /// </summary>
        public void SyncMarkersToDatabase(MarkerRow markers)
        {
            // update the database
            this.CreateBackupIfNeeded();
            ColumnTuplesWithID markerUpdate = markers.CreateUpdate();
            using (SQLiteConnection connection = this.Database.CreateConnection())
            {
                markerUpdate.Update(connection);
            }
        }

        public static bool TryCreateOrOpen(string filePath, TemplateDatabase templateDatabase, bool orderFilesByDate, LogicalOperator customSelectionTermCombiningOperator, out FileDatabase fileDatabase)
        {
            // check for an existing database before instantiating the databse as SQL wrapper instantiation creates the database file
            bool populateDatabase = !File.Exists(filePath);

            fileDatabase = new FileDatabase(filePath);
            if (populateDatabase)
            {
                // initialize the database if it's newly created
                fileDatabase.OnDatabaseCreated(templateDatabase);
            }
            else
            {
                // if it's an existing database check if it needs updating to current structure and load data tables
                if (fileDatabase.OnExistingDatabaseOpened(templateDatabase) == false)
                {
                    return false;
                }
            }

            // check all tables have been loaded from the database
            Debug.Assert(fileDatabase.Controls != null, "Controls wasn't loaded.");
            Debug.Assert(fileDatabase.Files != null, "Files wasn't loaded.");
            Debug.Assert(fileDatabase.ImageSet != null, "ImageSet wasn't loaded.");
            Debug.Assert(fileDatabase.Markers != null, "Markers wasn't loaded.");

            fileDatabase.CustomSelection = new CustomSelection(fileDatabase.Controls, customSelectionTermCombiningOperator);
            fileDatabase.OrderFilesByDateTime = orderFilesByDate;
            foreach (ControlRow control in fileDatabase.Controls)
            {
                fileDatabase.ControlsByDataLabel.Add(control.DataLabel, control);
            }

            // indicate failure if there are synchronization issues as the caller needs to determine if the database can be used anyway
            // This is different semantics from OnExistingDatabaseOpened().
            return fileDatabase.ControlSynchronizationIssues.Count == 0;
        }

        public void UpdateFiles(FileTuplesWithID update)
        {
            if (update.RowCount < 1)
            {
                // nothing to do
                return;
            }

            this.CreateBackupIfNeeded();
            using (SQLiteConnection connection = this.Database.CreateConnection())
            {
                update.Update(connection);
            }
        }

        public void UpdateFiles(FileTuplesWithID updateExisting, FileTuplesWithPath updateJustAdded)
        {
            this.CreateBackupIfNeeded();
            using (SQLiteConnection connection = this.Database.CreateConnection())
            {
                using (SQLiteTransaction transaction = connection.BeginTransaction())
                {
                    updateExisting.Update(connection, transaction);
                    updateJustAdded.Update(connection, transaction);

                    transaction.Commit();
                }
            }
        }

        // set one property on all rows in the selection to a given value
        public void UpdateFiles(ImageRow valueSource, DataEntryControl control)
        {
            this.UpdateFiles(valueSource, control, 0, this.CurrentlySelectedFileCount - 1);
        }

        public void UpdateFiles(ImageRow valueSource, DataEntryControl control, int fromIndex, int toIndex)
        {
            if (fromIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(fromIndex));
            }
            if (toIndex < fromIndex || toIndex > this.CurrentlySelectedFileCount - 1)
            {
                throw new ArgumentOutOfRangeException(nameof(toIndex));
            }

            object value = valueSource.GetValue(control.DataLabel);
            FileTuplesWithID filesToUpdate = new FileTuplesWithID(control.DataLabel);
            for (int index = fromIndex; index <= toIndex; index++)
            {
                // update data table
                ImageRow file = this.Files[index];
                Debug.Assert(file.HasChanges == false, "File has unexpected pending changes.");
                file[control.PropertyName] = value;

                // capture change for database update
                filesToUpdate.Add(file.ID, value);
                file.AcceptChanges();
            }

            this.CreateBackupIfNeeded();
            this.UpdateFiles(filesToUpdate);
        }
    }
}