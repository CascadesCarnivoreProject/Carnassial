using System.Collections.Generic;
using System.Data.SQLite;

namespace Carnassial.Database
{
    public abstract class ColumnTuplesForUpdate : ColumnTuples
    {
        protected string Table { get; private set; }

        protected ColumnTuplesForUpdate(string table, IEnumerable<string> columns)
            : base(columns)
        {
            this.Table = table;
        }

        protected ColumnTuplesForUpdate(string table, IList<ColumnTuple> columnTuples)
            : base(columnTuples)
        {
            this.Table = table;
        }

        public void Update(SQLiteConnection connection)
        {
            using (SQLiteTransaction transaction = connection.BeginTransaction())
            {
                this.Update(connection, transaction);
                transaction.Commit();
            }
        }

        public abstract void Update(SQLiteConnection connection, SQLiteTransaction transaction);
    }
}
