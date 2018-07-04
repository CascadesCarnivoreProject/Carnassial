using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace Carnassial.Database
{
    public class ColumnTuplesWithID : ColumnTuplesForUpdate
    {
        private List<long> ids;

        public ColumnTuplesWithID(string table, params string[] columns)
            : this(table, (IEnumerable<string>)columns)
        {
        }

        public ColumnTuplesWithID(string table, IEnumerable<string> columns)
            : base(table, columns)
        {
            this.ids = new List<long>();
        }

        public ColumnTuplesWithID(string table, IList<ColumnTuple> columnTuples, long id)
            : base(table, columnTuples)
        {
            this.ids = new List<long>() { id };
        }

        public void Add(long id, List<object> values)
        {
            if (id <= Constant.Database.InvalidID)
            {
                throw new ArgumentOutOfRangeException(nameof(id));
            }
            if (this.Columns.Count != values.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(values), String.Format("values needs to be of length {0} to match the columns configured but {1} values were passed.", this.Columns.Count, values.Count));
            }

            this.ids.Add(id);
            this.Values.Add(values);
        }

        public void Add(long id, params object[] values)
        {
            this.Add(id, new List<object>(values));
        }

        public override void Update(SQLiteConnection connection, SQLiteTransaction transaction)
        {
            // UPDATE tableName SET column1 = @column1, column2 = @column2, ... columnN = @columnN WHERE Id = @Id
            List<string> columnsToUpdate = new List<string>();
            foreach (string column in this.Columns)
            {
                columnsToUpdate.Add(column + " = @" + column);
            }
            string commandText = String.Format("UPDATE {0} SET {1} WHERE {2} = @{2}", this.Table, String.Join(", ", columnsToUpdate), Constant.DatabaseColumn.ID);

            using (SQLiteCommand command = new SQLiteCommand(commandText, connection, transaction))
            {
                foreach (string column in this.Columns)
                {
                    command.Parameters.Add(new SQLiteParameter("@" + column));
                }
                SQLiteParameter idParameter = new SQLiteParameter("@" + Constant.DatabaseColumn.ID);
                command.Parameters.Add(idParameter);

                int parameters = this.Columns.Count;
                for (int rowIndex = 0; rowIndex < this.ids.Count; ++rowIndex)
                {
                    List<object> values = this.Values[rowIndex];
                    for (int parameter = 0; parameter < parameters; ++parameter)
                    {
                        command.Parameters[parameter].Value = values[parameter];
                    }
                    idParameter.Value = this.ids[rowIndex];

                    command.ExecuteNonQuery();
                }
            }
        }
    }
}
