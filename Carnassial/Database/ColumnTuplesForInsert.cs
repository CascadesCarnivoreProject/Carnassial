using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace Carnassial.Database
{
    public class ColumnTuplesForInsert : ColumnTuples
    {
        private string table;

        public ColumnTuplesForInsert(string table, params string[] columns)
            : this(table, (IEnumerable<string>)columns)
        {
        }

        public ColumnTuplesForInsert(string table, IEnumerable<string> columns)
            : base(columns)
        {
            this.table = table;
        }

        public ColumnTuplesForInsert(string table, IList<ColumnTuple> columnTuples)
            : base(columnTuples)
        {
            this.table = table;
        }

        public void Add(params object[] values)
        {
            this.Add((IList<object>)values);
        }

        public void Add(IList<object> values)
        {
            if (this.Columns.Count != values.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(values), String.Format("values needs to be of length {0} to match the columns configured but {1} values were passed.", this.Columns.Count, values.Count));
            }

            this.Values.Add(new List<object>(values));
        }

        public void Insert(SQLiteConnection connection)
        {
            using (SQLiteTransaction transaction = connection.BeginTransaction())
            {
                this.Insert(connection, transaction);
                transaction.Commit();
            }
        }

        public void Insert(SQLiteConnection connection, SQLiteTransaction transaction)
        {
            // INSERT INTO tableName (column1, column2, ... columnN) VALUES (@column1, @column2, ... @columnN)
            string commandText = "INSERT INTO " + this.table + " (" + String.Join(", ", this.Columns) + ") VALUES (@" + String.Join(", @", this.Columns) + ")";

            using (SQLiteCommand command = new SQLiteCommand(commandText, connection, transaction))
            {
                foreach (string column in this.Columns)
                {
                    command.Parameters.Add(new SQLiteParameter("@" + column));
                }

                int parameters = this.Columns.Count;
                for (int rowIndex = 0; rowIndex < this.RowCount; ++rowIndex)
                {
                    List<object> values = this.Values[rowIndex];
                    for (int parameter = 0; parameter < parameters; ++parameter)
                    {
                        command.Parameters[parameter].Value = values[parameter];
                    }

                    command.ExecuteNonQuery();
                }
            }
        }
    }
}
