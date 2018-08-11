using Carnassial.Images;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;

namespace Carnassial.Data
{
    public class UpdateFileColumnTransaction : FileTransaction
    {
        private string dataLabel;
        private bool disposed;
        private SQLiteCommand updateFiles;

        public UpdateFileColumnTransaction(string dataLabel, SQLiteConnection connection)
            : base(connection)
        {
            this.dataLabel = dataLabel;

            string fileUpdateText = String.Format("UPDATE {0} SET {1}=@{1} WHERE {2}=@{2}", Constant.DatabaseTable.Files, dataLabel, Constant.DatabaseColumn.ID);
            this.Transaction = connection.BeginTransaction();
            this.updateFiles = new SQLiteCommand(fileUpdateText, this.Connection, this.Transaction);
            this.updateFiles.Parameters.Add(new SQLiteParameter("@" + Constant.DatabaseColumn.ID));
            this.updateFiles.Parameters.Add(new SQLiteParameter("@" + dataLabel));
        }

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

            int filesAdded = 0;
            int stopIndex = offset + length;
            for (int fileIndex = offset; fileIndex < stopIndex; ++fileIndex)
            {
                ImageRow fileToUpdate = files[fileIndex].File;
                if ((fileToUpdate == null) || (fileToUpdate.HasChanges == false))
                {
                    continue;
                }
                this.updateFiles.Parameters[0].Value = fileToUpdate.ID;
                this.updateFiles.Parameters[1].Value = fileToUpdate.GetDatabaseValue(this.dataLabel);

                this.updateFiles.ExecuteNonQuery();
                fileToUpdate.AcceptChanges();
                ++filesAdded;

                ++this.FilesInTransaction;
                if (this.FilesInTransaction >= Constant.Database.RowsPerTransaction)
                {
                    this.updateFiles = this.CommitAndBeginNew(this.updateFiles);
                }
            }

            return filesAdded;
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
                this.updateFiles.Dispose();
            }

            this.disposed = true;
        }
    }
}
