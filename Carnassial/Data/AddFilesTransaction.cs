using Carnassial.Database;
using Carnassial.Images;
using Carnassial.Util;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;

namespace Carnassial.Data
{
    public class AddFilesTransaction : FileTransaction
    {
        private SQLiteCommand addFiles;
        private SQLiteCommand addMarkers;
        private bool disposed;
        private FileDatabase fileDatabase;

        public AddFilesTransaction(FileDatabase fileDatabase, SQLiteConnection connection)
            : base(connection)
        {
            this.disposed = false;
            this.fileDatabase = fileDatabase;

            SQLiteParameter dateTime = new SQLiteParameter("@dateTime");
            SQLiteParameter fileName = new SQLiteParameter("@fileName");
            SQLiteParameter imageQuality = new SQLiteParameter("@imageQuality");
            SQLiteParameter relativePath = new SQLiteParameter("@relativePath");
            SQLiteParameter utcOffset = new SQLiteParameter("@utcOffset");

            bool counterPresent = false;
            List<string> dataLabels = new List<string>();
            List<string> defaultValues = new List<string>();
            string deleteFlagDefaultValue = null;
            foreach (KeyValuePair<string, ControlRow> column in fileDatabase.ControlsByDataLabel)
            {
                if (column.Value.Type == ControlType.Counter)
                {
                    counterPresent = true;
                }

                string dataLabel = column.Key;
                if ((dataLabel == Constant.DatabaseColumn.ID) ||
                    Constant.Control.StandardControls.Contains(dataLabel))
                {
                    if (dataLabel == Constant.DatabaseColumn.DeleteFlag)
                    {
                        deleteFlagDefaultValue = SQLiteDatabase.QuoteForSql(fileDatabase.FindControl(dataLabel).DefaultValue);
                    }
                    // don't specify ID in the insert statement as it's an autoincrement primary key
                    // don't generate parameters for standard controls they're coded explicitly
                    continue;
                }

                dataLabels.Add(dataLabel);
                defaultValues.Add(SQLiteDatabase.QuoteForSql(fileDatabase.FindControl(dataLabel).DefaultValue));
            }

            string dataLabelsConcatenated = null;
            string defaultValuesConcatenated = null;
            if (dataLabels.Count > 0)
            {
                dataLabelsConcatenated = ", " + String.Join(", ", dataLabels);
                defaultValuesConcatenated = ", " + String.Join(", ", defaultValues);
            }
            string fileInsertText = String.Format("INSERT INTO {0} ({1}, {2}, {3}, {4}, {5}, {6}{7}) VALUES (@dateTime, {8}, @fileName, @imageQuality, @relativePath, @utcOffset{9})",
                                                  Constant.DatabaseTable.FileData,
                                                  Constant.DatabaseColumn.DateTime,
                                                  Constant.DatabaseColumn.DeleteFlag,
                                                  Constant.DatabaseColumn.File,
                                                  Constant.DatabaseColumn.ImageQuality,
                                                  Constant.DatabaseColumn.RelativePath,
                                                  Constant.DatabaseColumn.UtcOffset,
                                                  dataLabelsConcatenated,
                                                  deleteFlagDefaultValue,
                                                  defaultValuesConcatenated);

            this.Transaction = this.Connection.BeginTransaction();
            this.addFiles = new SQLiteCommand(fileInsertText, this.Connection, this.Transaction);
            // order must be kept in sync with parameter value sets AddFiles()
            this.addFiles.Parameters.Add(dateTime);
            this.addFiles.Parameters.Add(fileName);
            this.addFiles.Parameters.Add(imageQuality);
            this.addFiles.Parameters.Add(relativePath);
            this.addFiles.Parameters.Add(utcOffset);

            if (counterPresent)
            {
                string markerInsertText = String.Format("INSERT INTO {0} DEFAULT VALUES", Constant.DatabaseTable.Markers);
                this.addMarkers = new SQLiteCommand(markerInsertText, this.Connection, this.Transaction);
            }
        }

        /// <summary>
        /// Inserts files in the file table with default values for most columns.  However, a file's name, relative path, date time offset, and image quality
        /// are populated.
        /// </summary>
        public override int AddFiles(IList<FileLoad> files, int offset, int length)
        {
            Debug.Assert(files != null, nameof(files) + " is null.");
            Debug.Assert(offset >= 0, nameof(offset) + " is less than zero.");
            Debug.Assert(length >= 0, nameof(length) + " is less than zero.");
            Debug.Assert((offset + length) <= files.Count, String.Format("Offset {0} plus length {1} exceeds length of files ({2}.", offset, length, files.Count));
            if (length < 1)
            {
                // nothing to do
                return 0;
            }

            // insert performance
            //                                   column defaults   specified defaults
            //                  unparameterized  parameterized     parameterized
            // 100 rows/call    245us/row        
            // 1000 rows/call   76us/row                           29us/row
            // single call      75us/row         27us/row          25us/row (40k rows/s)
            // Stopwatch stopwatch = new Stopwatch();
            // stopwatch.Start();
            int filesAdded = 0;
            int stopIndex = offset + length;
            for (int fileIndex = offset; fileIndex < stopIndex; ++fileIndex)
            {
                ImageRow fileToInsert = files[fileIndex].File;
                if (fileToInsert == null)
                {
                    continue;
                }
                this.addFiles.Parameters[0].Value = fileToInsert.UtcDateTime;
                this.addFiles.Parameters[1].Value = fileToInsert.FileName;
                this.addFiles.Parameters[2].Value = fileToInsert.ImageQuality.ToString();
                this.addFiles.Parameters[3].Value = fileToInsert.RelativePath;
                this.addFiles.Parameters[4].Value = DateTimeHandler.ToDatabaseUtcOffset(fileToInsert.UtcOffset);

                this.addFiles.ExecuteNonQuery();
                if (this.addMarkers != null)
                {
                    this.addMarkers.ExecuteNonQuery();
                }
                fileToInsert.AcceptChanges();
                ++filesAdded;

                ++this.FilesInTransaction;
                if (this.FilesInTransaction >= Constant.Database.RowsPerTransaction)
                {
                    this.addFiles = this.CommitAndBeginNew(this.addFiles);
                    if (this.addMarkers != null)
                    {
                        SQLiteCommand previousAddMarkers = this.addMarkers;
                        this.addMarkers = new SQLiteCommand(previousAddMarkers.CommandText, this.Connection, this.Transaction);
                        previousAddMarkers.Dispose();
                    }
                }
            }

            // stopwatch.Stop();
            // Trace.WriteLine(stopwatch.Elapsed.ToString("s\\.fffffff"));
            return filesAdded;
        }

        public override void Commit()
        {
            base.Commit();

            // refresh the marker table to keep it in sync
            // Files table doesn't need refresh as its contents were just flushed to the database.
            this.fileDatabase.GetMarkers(this.Connection);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                this.addFiles.Dispose();
                if (this.addMarkers != null)
                {
                    this.addMarkers.Dispose();
                }
            }

            this.disposed = true;
        }
    }
}
