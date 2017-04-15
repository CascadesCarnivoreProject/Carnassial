using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace Carnassial.Database
{
    public class FileTuplesWithPath : ColumnTuplesForUpdate
    {
        private List<string> fileNames;
        private List<string> relativePaths;

        public FileTuplesWithPath(IEnumerable<string> columns)
            : base(Constant.DatabaseTable.FileData, columns)
        {
            this.fileNames = new List<string>();
            this.relativePaths = new List<string>();
        }

        public void Add(string relativePath, string fileName, List<object> values)
        {
            if (this.Columns.Count != values.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(values), String.Format("values needs to be of length {0} to match the columns configured but {1} values were passed.", this.Columns.Count, values.Count));
            }

            this.fileNames.Add(fileName);
            this.relativePaths.Add(relativePath);
            this.Values.Add(values);
        }

        public override void Update(SQLiteConnection connection, SQLiteTransaction transaction)
        {
            // UPDATE tableName SET column1 = @column1, column2 = @column2, ... columnN = @columnN WHERE Id = @Id
            List<string> columnsToUpdate = new List<string>();
            foreach (string column in this.Columns)
            {
                columnsToUpdate.Add(column + " = @" + column);
            }
            string commandText = String.Format("UPDATE {0} SET {1} WHERE {2} = @{2} AND {3} = @{3}", this.Table, String.Join(", ", columnsToUpdate), Constant.DatabaseColumn.RelativePath, Constant.DatabaseColumn.File);

            using (SQLiteCommand command = new SQLiteCommand(commandText, connection, transaction))
            {
                foreach (string column in this.Columns)
                {
                    SQLiteParameter parameter = new SQLiteParameter("@" + column);
                    command.Parameters.Add(parameter);
                }
                SQLiteParameter fileNameParameter = new SQLiteParameter("@" + Constant.DatabaseColumn.File);
                command.Parameters.Add(fileNameParameter);
                SQLiteParameter relativePathParameter = new SQLiteParameter("@" + Constant.DatabaseColumn.RelativePath);
                command.Parameters.Add(relativePathParameter);

                int parameters = this.Columns.Count;
                for (int rowIndex = 0; rowIndex < this.fileNames.Count; ++rowIndex)
                {
                    List<object> values = this.Values[rowIndex];
                    for (int parameter = 0; parameter < parameters; ++parameter)
                    {
                        command.Parameters[parameter].Value = values[parameter];
                    }
                    fileNameParameter.Value = this.fileNames[rowIndex];
                    relativePathParameter.Value = this.relativePaths[rowIndex];

                    command.ExecuteNonQuery();
                }
            }
        }
    }
}
