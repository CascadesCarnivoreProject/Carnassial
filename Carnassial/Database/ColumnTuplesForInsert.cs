using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace Carnassial.Database
{
    public class ColumnTuplesForInsert : ColumnTuples
    {
        private string table;

        public ColumnTuplesForInsert(string table, params string[] columns)
            : base((IEnumerable<string>)columns)
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
            if (this.Columns.Count != values.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(values), String.Format("values needs to be of length {0} to match the columns configured but {1} values were passed.", this.Columns.Count, values.Length));
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
            List<string> parameterNames = new List<string>();
            foreach (string column in this.Columns)
            {
                parameterNames.Add("@" + column);
            }
            string commandText = String.Format("INSERT INTO {0} ({1}) VALUES ({2})", this.table, String.Join(", ", this.Columns), String.Join(", ", parameterNames));

            using (SQLiteCommand command = new SQLiteCommand(commandText, connection, transaction))
            {
                foreach (string column in this.Columns)
                {
                    SQLiteParameter parameter = new SQLiteParameter("@" + column);
                    command.Parameters.Add(parameter);
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
