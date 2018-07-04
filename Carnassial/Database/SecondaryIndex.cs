using Carnassial.Data;
using System;
using System.Data.SQLite;

namespace Carnassial.Database
{
    public class SecondaryIndex
    {
        public string Column { get; private set; }
        public string Name { get; private set; }
        public string Table { get; private set; }

        public SecondaryIndex(string table, string name, string column)
        {
            this.Column = column;
            this.Name = name;
            this.Table = table;
        }

        public void Create(SQLiteConnection connection, SQLiteTransaction transaction)
        {
            string createIndex = String.Format("CREATE INDEX '{0}' ON '{1}' ('{2}')", this.Name, this.Table, this.Column);
            using (SQLiteCommand command = new SQLiteCommand(createIndex, connection, transaction))
            {
                command.ExecuteNonQuery();
            }
        }

        public static SecondaryIndex CreateFileTableIndex(ControlRow control)
        {
            string indexName = "File" + control.DataLabel + "Index";
            return new SecondaryIndex(Constant.DatabaseTable.Files, indexName, control.DataLabel);
        }

        public void Drop(SQLiteConnection connection, SQLiteTransaction transaction)
        {
            string dropIndex = String.Format("DROP INDEX '{0}'", this.Name);
            using (SQLiteCommand command = new SQLiteCommand(dropIndex, connection, transaction))
            {
                command.ExecuteNonQuery();
            }
        }
    }
}
