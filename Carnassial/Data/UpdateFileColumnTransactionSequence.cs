using Carnassial.Database;
using Carnassial.Images;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.Globalization;

namespace Carnassial.Data
{
    public class UpdateFileColumnTransactionSequence : WindowedTransactionSequence<FileLoad>
    {
        private readonly string dataLabel;
        private bool disposed;
        private SQLiteCommand updateFiles;

        public UpdateFileColumnTransactionSequence(string dataLabel, SQLiteDatabase database)
            : base(database)
        {
            this.dataLabel = dataLabel;

            string fileUpdateText = String.Format(CultureInfo.InvariantCulture, "UPDATE {0} SET {1}=@Value WHERE {2}=@{2}", Constant.DatabaseTable.Files, SQLiteDatabase.QuoteIdentifier(dataLabel), Constant.DatabaseColumn.ID);
            this.Transaction = database.Connection.BeginTransaction();
            this.updateFiles = new SQLiteCommand(fileUpdateText, this.Database.Connection, this.Transaction);
            this.updateFiles.Parameters.Add(new SQLiteParameter("@" + Constant.DatabaseColumn.ID));
            this.updateFiles.Parameters.Add(new SQLiteParameter("@Value"));
        }

        public override int AddToSequence(IList<FileLoad> files, int offset, int length)
        {
            Debug.Assert(files != null, nameof(files) + " is null.");
            Debug.Assert(offset >= 0, nameof(offset) + " is less than zero.");
            Debug.Assert(length >= 0, nameof(length) + " is less than zero.");
            Debug.Assert((offset + length) <= files.Count, String.Format(CultureInfo.InvariantCulture, "Offset {0} plus length {1} exceeds length of files ({2}.", offset, length, files.Count));

            int filesAdded = 0;
            int stopIndex = offset + length;
            for (int fileIndex = offset; fileIndex < stopIndex; ++fileIndex)
            {
                ImageRow? file = files[fileIndex].File;
                if ((file == null) || (file.HasChanges == false))
                {
                    continue;
                }
                this.updateFiles.Parameters[0].Value = file.ID;
                this.updateFiles.Parameters[1].Value = file.GetDatabaseValue(this.dataLabel);

                this.updateFiles.ExecuteNonQuery();
                file.AcceptChanges();
                ++filesAdded;

                ++this.RowsInCurrentTransaction;
                if (this.RowsInCurrentTransaction >= Constant.Database.RowsPerTransaction)
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

        public void UpdateFiles(IList<ImageRow> files)
        {
            Debug.Assert(files != null, nameof(files) + " is null.");
            for (int fileIndex = 0; fileIndex < files.Count; ++fileIndex)
            {
                ImageRow file = files[fileIndex];
                if (file.HasChanges == false)
                {
                    continue;
                }
                this.updateFiles.Parameters[0].Value = file.ID;
                this.updateFiles.Parameters[1].Value = file.GetDatabaseValue(this.dataLabel);

                this.updateFiles.ExecuteNonQuery();
                file.AcceptChanges();

                ++this.RowsInCurrentTransaction;
                if (this.RowsInCurrentTransaction >= Constant.Database.RowsPerTransaction)
                {
                    this.updateFiles = this.CommitAndBeginNew(this.updateFiles);
                }
            }
        }
    }
}
