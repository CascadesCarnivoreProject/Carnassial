using System;
using System.Data.SQLite;

namespace Carnassial.Database
{
    public class TransactionSequence : IDisposable
    {
        private bool disposed;

        protected SQLiteConnection Connection { get; private set; }
        protected int FilesInTransaction { get; set; }
        protected SQLiteTransaction Transaction { get; set; }

        public TransactionSequence(SQLiteConnection connection)
        {
            this.Connection = connection;
            this.disposed = false;
            this.FilesInTransaction = 0;
        }

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