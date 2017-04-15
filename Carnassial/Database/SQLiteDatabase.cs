using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Carnassial.Database
{
    /// <summary>
    /// A helper layer over SQLiteConnection providing connection origination and some low level database operations.  See <see cref="ColumnTuple"/>,
    /// <see cref="ColumnTuplesForUpdate"/>, and <see cref="ColumnTuplesForInsert"/> for related functionality.
    /// </summary>
    public class SQLiteDatabase
    {
        private string connectionString;

        /// <summary>
        /// Prepares a database connection.  Creates the database file if it does not exist.
        /// </summary>
        /// <param name="inputFile">the file containing the database</param>
        public SQLiteDatabase(string inputFile)
        {
            if (!File.Exists(inputFile))
            {
                SQLiteConnection.CreateFile(inputFile);
            }
            SQLiteConnectionStringBuilder connectionStringBuilder = new SQLiteConnectionStringBuilder();
            connectionStringBuilder.DataSource = inputFile;
            connectionStringBuilder.DateTimeKind = DateTimeKind.Utc;
            this.connectionString = connectionStringBuilder.ConnectionString;
        }

        /// <summary>
        /// Add a column to the table at position columnNumber using the provided definition.
        /// </summary>
        public void AddColumnToTable(string table, int columnNumber, ColumnDefinition columnDefinition)
        {
            using (SQLiteConnection connection = this.CreateConnection())
            {
                List<ColumnDefinition> currentColumnDefinitions = this.GetColumnDefinitions(connection, table);
                if (currentColumnDefinitions.Any(column => column.Name == columnDefinition.Name))
                {
                    throw new ArgumentException(String.Format("Column '{0}' is already present in table '{1}'.", columnDefinition.Name, table), nameof(columnDefinition));
                }

                // optimization for appending column to end of table
                if (columnNumber >= currentColumnDefinitions.Count)
                {
                    using (SQLiteCommand command = new SQLiteCommand("ALTER TABLE " + table + " ADD COLUMN " + columnDefinition.ToString(), connection))
                    {
                        command.ExecuteNonQuery();
                    }
                    return;
                }

                List<ColumnDefinition> newColumnDefinitions = new List<ColumnDefinition>(currentColumnDefinitions);
                newColumnDefinitions.Insert(columnNumber, columnDefinition);
                this.ChangeTableColumns(connection, table, newColumnDefinitions, currentColumnDefinitions, currentColumnDefinitions);
            }
        }

        /// <summary>
        /// Add, remove, or rename columns in a table.
        /// </summary>
        /// <param name="connection">The database connection to use.</param>
        /// <param name="table">The table to modify.</param>
        /// <param name="newColumns">The table's new schema.</param>
        /// <param name="sourceColumns">The existing columns to copy data from.</param>
        /// <param name="destinationColumns">The columns to copy the existing data to.</param>
        /// <remarks>
        /// To
        /// - add a column newColumns has one extra entry (for the new column) and sourceColumns and destinationColumns are the same
        /// - remove a column newColumns, sourceColumns, and destinationColumns are all the same
        /// - rename columns newColumns and destinationColumns are the same and sourceColumns differs only in the column names
        /// </remarks>
        private void ChangeTableColumns(SQLiteConnection connection, string table, List<ColumnDefinition> newColumns, List<ColumnDefinition> sourceColumns, List<ColumnDefinition> destinationColumns)
        {
            // TODO: support full column schema - autoincrement + ?
            //       handling of secondary indicies?
            using (SQLiteTransaction transaction = connection.BeginTransaction())
            {
                // create table with the new schema
                string replacementTableName = table + "Replacement";
                this.CreateTable(connection, transaction, replacementTableName, newColumns);

                // copy specified part of the old table's contents to the new table
                string sourceColumnNames = String.Join(" ,", sourceColumns.Select(column => column.Name));
                string destinationColumnNames = String.Join(" ,", destinationColumns.Select(column => column.Name));
                string copyColumns = "INSERT INTO " + replacementTableName + " (" + destinationColumnNames + ") SELECT (" + sourceColumnNames + ") FROM " + table;
                using (SQLiteCommand command = new SQLiteCommand(copyColumns, connection, transaction))
                {
                    command.ExecuteNonQuery();
                }

                // drop the old table and rename the new table into place
                using (SQLiteCommand command = new SQLiteCommand("DROP TABLE " + table, connection, transaction))
                {
                    command.ExecuteNonQuery();
                }
                using (SQLiteCommand command = new SQLiteCommand("ALTER TABLE " + replacementTableName + " RENAME TO " + table, connection, transaction))
                {
                    command.ExecuteNonQuery();
                }

                transaction.Commit();
            }
        }

        public SQLiteConnection CreateConnection()
        {
            SQLiteConnection connection = new SQLiteConnection(this.connectionString);
            connection.Open();
            return connection;
        }

        public void CreateTable(SQLiteConnection connection, SQLiteTransaction transaction, string table, List<ColumnDefinition> columnDefinitions)
        {
            string columnDefinitionsAsString = String.Join(", ", columnDefinitions.ConvertAll(columnDefinition => columnDefinition.ToString()));
            using (SQLiteCommand command = new SQLiteCommand("CREATE TABLE " + table + " (" + columnDefinitionsAsString + " )", connection, transaction))
            {
                command.ExecuteNonQuery();
            }
        }

        private void DataTableColumns_Changed(object sender, CollectionChangeEventArgs columnChange)
        {
            // DateTime columns default to DataSetDateTime.UnspecifiedLocal, which converts fully qualified DateTimes returned from SQLite to DateTimeKind.Unspecified
            // Since the DateTime column in Carnassial is UTC change this to DataSetDateTime.Utc to get DateTimeKind.Utc.  This must be done before any rows 
            // are added to the table.  This callback is the only way to access the column schema from within DataTable.Load() to make the change.
            DataColumn columnChanged = (DataColumn)columnChange.Element;
            if (columnChanged.DataType == typeof(DateTime))
            {
                columnChanged.DateTimeMode = DataSetDateTime.Utc;
            }
        }

        public void DeleteColumn(string table, string column)
        {
            if (String.IsNullOrWhiteSpace(column))
            {
                throw new ArgumentOutOfRangeException(nameof(column));
            }

            using (SQLiteConnection connection = this.CreateConnection())
            {
                List<ColumnDefinition> columnDefinitions = this.GetColumnDefinitions(connection, table);
                if (columnDefinitions.Any(columnDefinition => columnDefinition.Name == column))
                {
                    throw new ArgumentOutOfRangeException(nameof(column));
                }

                // drop the requested column from the schema
                int columnToRemove = -1;
                for (int columnIndex = 0; columnIndex < columnDefinitions.Count; ++columnIndex)
                {
                    ColumnDefinition columnDefinition = columnDefinitions[columnIndex];
                    if (columnDefinition.Name == column)
                    {
                        columnToRemove = columnIndex;
                        break;
                    }
                }
                if (columnToRemove == -1)
                {
                    throw new ArgumentOutOfRangeException(String.Format("Column '{0}' not found in table '{1}'.", column, table));
                }
                columnDefinitions.RemoveAt(columnToRemove);

                this.ChangeTableColumns(connection, table, columnDefinitions, columnDefinitions, columnDefinitions);
            }
        }

        /// <summary>
        /// Get the Schema for a simple database table 'tableName' from the connected database.
        /// For each column, it can retrieve schema settings including:
        ///     Name, Type, If its the Primary Key, Constraints including its Default Value (if any) and Not Null 
        /// However other constraints that may be set in the table schema are NOT returned, including:
        ///     UNIQUE, CHECK, FOREIGN KEYS, AUTOINCREMENT 
        /// If you use those, the schema may either ignore them or return odd values. So check it!
        /// Usage example: SQLiteDataReader reader = this.GetSchema(connection, "tableName");
        /// </summary>
        private List<ColumnDefinition> GetColumnDefinitions(SQLiteConnection connection, string table)
        {
            using (SQLiteCommand command = new SQLiteCommand("PRAGMA TABLE_INFO (" + table + ")", connection))
            {
                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    List<ColumnDefinition> columnDefinitions = new List<ColumnDefinition>();
                    while (reader.Read())
                    {
                        // The schema as a SQLiteDataReader.To examine it, do a while loop over reader.Read() to read a column at a time after every read
                        // access the column's attributes, where 
                        // field 0 is column number (e.g., 0)
                        // field 1 is column name (e.g., Employee)
                        // field 2 is type (e.g., STRING)
                        // field 3 to 5 also returns values but more checking is needed
                        Debug.Assert(reader.FieldCount > 3, "Encountered incomplete column.");
                        ColumnDefinition columnDefinition = new ColumnDefinition(reader[1].ToString(), reader[2].ToString());
                        if (reader.FieldCount > 4 && reader[3].ToString() != "0")
                        {
                            columnDefinition.NotNull = true;
                        }
                        if (reader.FieldCount > 5 && reader[4].ToString() != String.Empty)
                        {
                            columnDefinition.DefaultValue = reader[4].ToString();
                        }
                        if (reader.FieldCount > 6 && reader[5].ToString() != "0")
                        {
                            columnDefinition.PrimaryKey = true;
                        }

                        columnDefinitions.Add(columnDefinition);
                    }
                    return columnDefinitions;
                }
            }
        }

        public DataTable GetDataTableFromSelect(SQLiteConnection connection, Select select)
        {
            using (SQLiteCommand command = select.CreateSelect(connection))
            {
                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    DataTable dataTable = new DataTable();
                    dataTable.Columns.CollectionChanged += this.DataTableColumns_Changed;
                    dataTable.Load(reader);
                    return dataTable;
                }
            }
        }

        public List<object> GetDistinctValuesInColumn(string table, string column)
        {
            using (SQLiteConnection connection = this.CreateConnection())
            {
                using (SQLiteCommand command = new SQLiteCommand(String.Format("SELECT DISTINCT {0} FROM {1}", column, table), connection))
                {
                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        List<object> distinctValues = new List<object>();
                        while (reader.Read())
                        {
                            distinctValues.Add(reader[column]);
                        }
                        return distinctValues;
                    }
                }
            }
        }

        public List<string> GetTableNames(SQLiteConnection connection)
        {
            using (SQLiteCommand command = new SQLiteCommand("SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY name", connection))
            {
                SQLiteDataReader reader = command.ExecuteReader();
                List<string> tableNames = new List<string>();
                while (reader.Read())
                {
                    tableNames.Add(reader[0].ToString());
                }
                return tableNames;
            }
        }

        public void RenameColumn(string table, string currentColumnName, string newColumnName)
        {
            if (String.IsNullOrWhiteSpace(currentColumnName))
            {
                throw new ArgumentOutOfRangeException("currentColumnName");
            }
            if (String.IsNullOrWhiteSpace(newColumnName))
            {
                throw new ArgumentOutOfRangeException("newColumnName");
            }

            using (SQLiteConnection connection = this.CreateConnection())
            {
                List<ColumnDefinition> columnDefinitions = this.GetColumnDefinitions(connection, table);
                if (columnDefinitions.Any(column => column.Name == currentColumnName) == false)
                {
                    throw new ArgumentException(String.Format("No column named '{0}' exists to rename.", currentColumnName), nameof(currentColumnName));
                }
                if (columnDefinitions.Any(column => column.Name == newColumnName))
                {
                    throw new ArgumentException(String.Format("Column named '{0}' already exists.", newColumnName), nameof(newColumnName));
                }

                List<ColumnDefinition> columnDefinitionsWithNameChange = new List<ColumnDefinition>();
                foreach (ColumnDefinition column in columnDefinitions)
                {
                    if (column.Name == currentColumnName)
                    {
                        ColumnDefinition columnWithNameChanged = new ColumnDefinition(column);
                        columnDefinitionsWithNameChange.Add(columnWithNameChanged);
                    }
                    else
                    {
                        columnDefinitionsWithNameChange.Add(column);
                    }
                }

                this.ChangeTableColumns(connection, table, columnDefinitionsWithNameChange, columnDefinitions, columnDefinitionsWithNameChange);
            }
        }
    }
}