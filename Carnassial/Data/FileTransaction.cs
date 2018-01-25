using Carnassial.Images;
using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace Carnassial.Data
{
    public abstract class FileTransaction : IDisposable
    {
        private bool disposed;

        protected SQLiteConnection Connection { get; private set; }
        protected int FilesInTransaction { get; set; }
        protected SQLiteTransaction Transaction { get; set; }

        public FileTransaction(SQLiteConnection connection)
        {
            this.Connection = connection;
            this.disposed = false;
            this.FilesInTransaction = 0;
        }

        public void AddFiles(IList<FileLoad> files)
        {
            this.AddFiles(files, 0, files.Count);
        }

        public abstract int AddFiles(IList<FileLoad> files, int offset, int length);

        public virtual void Commit()
        {
            this.Transaction.Commit();
        }

        protected SQLiteCommand CommitAndBeginNew(SQLiteCommand command)
        {
            this.Transaction.Commit();
            this.FilesInTransaction = 0;

            this.Transaction = this.Connection.BeginTransaction();

            SQLiteCommand previousCommand = command;
            SQLiteCommand newCommand = new SQLiteCommand(previousCommand.CommandText, this.Connection, this.Transaction);
            foreach (SQLiteParameter parameter in previousCommand.Parameters)
            {
                newCommand.Parameters.Add(parameter);
            }
            previousCommand.Dispose();
            return newCommand;
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                this.Transaction.Dispose();
                this.Connection.Dispose();
            }

            this.disposed = true;
        }
    }
}