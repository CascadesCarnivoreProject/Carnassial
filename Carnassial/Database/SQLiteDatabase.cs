using System;
using System.Collections.Generic;
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
            SQLiteConnectionStringBuilder connectionStringBuilder = new SQLiteConnectionStringBuilder()
            {
                DataSource = inputFile,
                DateTimeKind = DateTimeKind.Utc
            };
            this.connectionString = connectionStringBuilder.ConnectionString;
        }

        /// <summary>
        /// Add a column to the table at position columnNumber using the provided definition.
        /// </summary>
        public void AddColumnToTable(SQLiteConnection connection, SQLiteTransaction transaction, string table, int columnNumber, ColumnDefinition columnDefinition)
        {
            // get table's current schema
            List<ColumnDefinition> currentColumnDefinitions = this.GetColumnDefinitions(connection, table);
            if (currentColumnDefinitions.Any(column => column.Name == columnDefinition.Name))
            {
                throw new ArgumentException(String.Format("Column '{0}' is already present in table '{1}'.", columnDefinition.Name, table), nameof(columnDefinition));
            }
            if (columnNumber > currentColumnDefinitions.Count)
            {
                throw new ArgumentOutOfRangeException(String.Format("Attempt to add column in position {0} but the '{1}' table has only {2} columns.", columnNumber, table, currentColumnDefinitions.Count));
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

            // otherwise, SQLite requires copying to a table with a new schema
            List<ColumnDefinition> newColumnDefinitions = new List<ColumnDefinition>(currentColumnDefinitions);
            newColumnDefinitions.Insert(columnNumber, columnDefinition);
            this.ChangeTableColumns(connection, transaction, table, newColumnDefinitions, currentColumnDefinitions, currentColumnDefinitions);
        }

        /// <summary>
        /// Add, remove, or rename columns in a table.
        /// </summary>
        /// <param name="connection">The database connection to use.</param>
        /// <param name="transaction">The database transaction to use.</param>
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
        private void ChangeTableColumns(SQLiteConnection connection, SQLiteTransaction transaction, string table, List<ColumnDefinition> newColumns, List<ColumnDefinition> sourceColumns, List<ColumnDefinition> destinationColumns)
        {
            if (sourceColumns.Count != destinationColumns.Count)
            {
                throw new ArgumentException(String.Format("Source and destination column lists must be of the same length. Source list has {0} columns and destination list has {1} columns.", sourceColumns.Count, destinationColumns.Count));
            }
            if (newColumns.Count < sourceColumns.Count)
            {
                throw new ArgumentException(String.Format("Source and destination column lists exceed length of new column list. New column list has {0} columns while the other lists have {1} columns.", newColumns.Count, sourceColumns.Count));
            }

            // TODO: support full column schema - autoincrement + ?
            //       handling of secondary indicies?
            // create table with the new schema
            string replacementTableName = table + "Replacement";
            this.CreateTable(connection, transaction, replacementTableName, newColumns);

            // copy specified part of the old table's contents to the new table
            // SQLite doesn't allow autoincrement columns to be copied but their values are preserved as rows are inserted in the
            // same order.
            List<string> sourceColumnNames = sourceColumns.Where(column => column.Autoincrement == false).Select(column => column.Name).ToList();
            List<string> destinationColumnNames = destinationColumns.Where(column => column.Autoincrement == false).Select(column => column.Name).ToList();

            string copyColumns = "INSERT INTO " + replacementTableName + " (" + String.Join(", ", destinationColumnNames) + ") SELECT " + String.Join(", ", sourceColumnNames) + " FROM " + table;
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

        public void DeleteColumn(SQLiteConnection connection, SQLiteTransaction transaction, string table, string column)
        {
            if (String.IsNullOrWhiteSpace(column))
            {
                throw new ArgumentOutOfRangeException(nameof(column));
            }

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
                throw new ArgumentOutOfRangeException(nameof(column), String.Format("Column '{0}' not found in table '{1}'.", column, table));
            }
            columnDefinitions.RemoveAt(columnToRemove);

            this.ChangeTableColumns(connection, transaction, table, columnDefinitions, columnDefinitions, columnDefinitions);
        }

        public List<ColumnDefinition> GetColumnDefinitions(SQLiteConnection connection, string table)
        {
            // as of SQLite 1.0.108 columns are returned in order so sorting by number is not strictly necessary
            // Column numbering is explicitly respected in the order of return for robustness, however.
            Dictionary<long, ColumnDefinition> columnDefinitionsByNumber = new Dictionary<long, ColumnDefinition>();
            using (SQLiteCommand command = new SQLiteCommand("PRAGMA TABLE_INFO (" + table + ")", connection))
            {
                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Debug.Assert(reader.FieldCount > 2, "Encountered incomplete column.");
                        // field 1 is column name
                        // field 2 is type
                        ColumnDefinition columnDefinition = new ColumnDefinition(reader[1].ToString(), reader[2].ToString());

                        if ((reader.FieldCount > 3) && (reader[3].ToString() != "0"))
                        {
                            columnDefinition.NotNull = true;
                        }
                        if ((reader.FieldCount > 4) && (reader[4].ToString() != String.Empty))
                        {
                            columnDefinition.DefaultValue = reader[4].ToString();
                        }
                        if ((reader.FieldCount > 5) && (reader[5].ToString() != "0"))
                        {
                            columnDefinition.PrimaryKey = true;
                            // other constraints that may be set in the table schema aren't supported, including UNIQUE, 
                            // CHECK, FOREIGN KEYS, AUTOINCREMENT 
                            // Workaround: as all Carnassial tables have autoincrement primary keys the two flags are currently \
                            // equivalent
                            columnDefinition.Autoincrement = true;
                        }

                        // field 0 is column number
                        columnDefinitionsByNumber.Add((long)reader[0], columnDefinition);
                    }
                }
            }

            List<string> indicies = new List<string>();
            using (SQLiteCommand command = new SQLiteCommand("PRAGMA INDEX_LIST (" + table + ")", connection))
            {
                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string indexName = reader.GetString(1);
                        string indexSource = reader.GetString(3);
                    }
                }
            }

            List<ColumnDefinition> columnDefinitions = new List<ColumnDefinition>(columnDefinitionsByNumber.Count);
            for (long columnNumber = 0; columnNumber < columnDefinitionsByNumber.Count; ++columnNumber)
            {
                columnDefinitions.Add(columnDefinitionsByNumber[columnNumber]);
            }
            return columnDefinitions;
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

        public void LoadDataTableFromSelect<TRow>(SQLiteTable<TRow> table, SQLiteConnection connection, Select select)
        {
            using (SQLiteCommand command = select.CreateSelect(connection))
            {
                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    table.Load(reader);
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

        /// <summary>
        /// SQLite doesn't support parameters for default values when defining table schemas so escaping has to be done by its caller.
        /// </summary>
        public static string QuoteForSql(string value)
        {
            // promote null values to empty strings
            if (value == null)
            {
                return "''";
            }

            // for an input of "foo's bar" the output is "'foo''s bar'"
            return "'" + value.Replace("'", "''") + "'";
        }

        public void RenameColumn(SQLiteConnection connection, SQLiteTransaction transaction, string table, string currentColumnName, string newColumnName)
        {
            if (String.IsNullOrWhiteSpace(currentColumnName))
            {
                throw new ArgumentOutOfRangeException(nameof(currentColumnName));
            }
            if (String.IsNullOrWhiteSpace(newColumnName))
            {
                throw new ArgumentOutOfRangeException(nameof(newColumnName));
            }

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

            this.ChangeTableColumns(connection, transaction, table, columnDefinitionsWithNameChange, columnDefinitions, columnDefinitionsWithNameChange);
        }
    }
}