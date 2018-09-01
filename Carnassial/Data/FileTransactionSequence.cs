using Carnassial.Database;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.Text;

namespace Carnassial.Data
{
    public class FileTransactionSequence : TransactionSequence
    {
        private bool disposed;
        private readonly FileTable fileTable;
        private SQLiteCommand insertOrUpdateFiles;

        protected FileTransactionSequence(StringBuilder command, SQLiteDatabase database, FileTable fileTable)
            : base(database)
        {
            this.disposed = false;
            this.fileTable = fileTable;
            this.Transaction = database.Connection.BeginTransaction();

            this.insertOrUpdateFiles = new SQLiteCommand(command.ToString(), this.Database.Connection, this.Transaction);
            foreach (string standardColumn in Constant.Control.StandardControls)
            {
                this.insertOrUpdateFiles.Parameters.Add(new SQLiteParameter("@" + standardColumn));
            }
            foreach (string userColumn in fileTable.UserColumnsByName.Keys)
            {
                this.insertOrUpdateFiles.Parameters.Add(new SQLiteParameter("@" + userColumn));
            }
            this.insertOrUpdateFiles.Parameters.Add(new SQLiteParameter("@" + Constant.DatabaseColumn.ID));
        }

        public static FileTransactionSequence CreateInsert(SQLiteDatabase database, FileTable fileTable)
        {
            List<string> columns = new List<string>(1 + Constant.Control.StandardControls.Count + fileTable.UserColumnsByName.Count);
            List<string> parameterNames = new List<string>(1 + Constant.Control.StandardControls.Count + fileTable.UserColumnsByName.Count);

            foreach (string standardColumn in Constant.Control.StandardControls)
            {
                columns.Add(standardColumn);
                parameterNames.Add("@" + standardColumn);
            }
            foreach (string userColumn in fileTable.UserColumnsByName.Keys)
            {
                columns.Add(userColumn);
                parameterNames.Add("@" + userColumn);
            }

            StringBuilder insertCommand = new StringBuilder("INSERT INTO " + Constant.DatabaseTable.Files + " (" + String.Join(", ", columns) + ") VALUES (" + String.Join(", ", parameterNames) + ")");

            return new FileTransactionSequence(insertCommand, database, fileTable);
        }

        public static FileTransactionSequence CreateUpdate(SQLiteDatabase database, FileTable fileTable)
        {
            StringBuilder updateCommand = new StringBuilder("UPDATE " + Constant.DatabaseTable.Files + " SET ");
            List<string> parameters = new List<string>(Constant.Control.StandardControls.Count + fileTable.UserColumnsByName.Count);
            foreach (string standardColumn in Constant.Control.StandardControls)
            {
                parameters.Add(standardColumn + "=@" + standardColumn);
            }
            foreach (string userColumn in fileTable.UserColumnsByName.Keys)
            {
                parameters.Add(userColumn + "=@" + userColumn);
            }
            updateCommand.Append(String.Join(", ", parameters));
            updateCommand.Append(" WHERE " + Constant.DatabaseColumn.ID + "=@" + Constant.DatabaseColumn.ID);

            return new FileTransactionSequence(updateCommand, database, fileTable);
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
                this.insertOrUpdateFiles.Dispose();
            }

            this.disposed = true;
        }

        public void AddFile(ImageRow file)
        {
            this.AddFiles(new List<ImageRow>() { file });
        }

        public void AddFiles(IList<ImageRow> files)
        {
            Debug.Assert(files != null, nameof(files) + " is null.");
            for (int fileIndex = 0; fileIndex < files.Count; ++fileIndex)
            {
                ImageRow file = files[fileIndex];
                if (file.HasChanges == false)
                {
                    continue;
                }
                Debug.Assert(this.insertOrUpdateFiles.CommandText.StartsWith("INSERT", StringComparison.Ordinal) || (file.ID != Constant.Database.InvalidID), "File ID not set in update.");
                Debug.Assert(this.insertOrUpdateFiles.CommandText.StartsWith("UPDATE", StringComparison.Ordinal) || (file.ID == Constant.Database.InvalidID), "File ID set in insert.");

                int parameterIndex = 0;
                foreach (string standardColumn in Constant.Control.StandardControls)
                {
                    this.insertOrUpdateFiles.Parameters[parameterIndex++].Value = file.GetDatabaseValue(standardColumn);
                }
                foreach (string userColumn in this.fileTable.UserColumnsByName.Keys)
                {
                    this.insertOrUpdateFiles.Parameters[parameterIndex++].Value = file.GetDatabaseValue(userColumn);
                }
                this.insertOrUpdateFiles.Parameters[parameterIndex].Value = file.ID;

                this.insertOrUpdateFiles.ExecuteNonQuery();
                file.AcceptChanges();

                ++this.RowsInCurrentTransaction;
                if (this.RowsInCurrentTransaction >= Constant.Database.RowsPerTransaction)
                {
                    this.insertOrUpdateFiles = this.CommitAndBeginNew(this.insertOrUpdateFiles);
                }
            }
        }
    }
}
