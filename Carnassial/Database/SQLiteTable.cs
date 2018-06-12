using System.Collections;
using System.Collections.Generic;
using System.Data.SQLite;

namespace Carnassial.Database
{
    public abstract class SQLiteTable<TRow> : IEnumerable<TRow>
    {
        protected List<TRow> Rows { get; private set; }

        protected SQLiteTable()
        {
            this.Rows = new List<TRow>();
        }

        public int RowCount
        {
            get { return this.Rows.Count; }
        }

        public TRow this[int index]
        {
            get { return this.Rows[index]; }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public IEnumerator<TRow> GetEnumerator()
        {
            return this.Rows.GetEnumerator();
        }

        public abstract void Load(SQLiteDataReader reader);
    }
}
