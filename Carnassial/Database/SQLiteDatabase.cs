using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Carnassial.Database
{
    public class SQLiteDatabase : IDisposable
    {
        private int databasePragmaChangesSinceLastBackup;
        private bool disposed;
        private int schemaChangesSinceLastBackup;

        public Task<bool> BackupTask { get; private set; }

        public SQLiteConnection Connection { get; protected set; }

        /// <summary>Gets or sets the path of the database on disk.</summary>
        public string FilePath { get; protected set; }

        public int RowsDroppedSinceLastBackup { get; set; }
        public int RowsInsertedSinceLastBackup { get; set; }
        public int RowsUpdatedSinceLastBackup { get; set; }

        /// <summary>
        /// Prepares a database connection.  Creates the database file if it does not exist.
        /// </summary>
        /// <param name="filePath">the file containing the database</param>
        protected SQLiteDatabase(string filePath)
        {
            this.BackupTask = null;
            this.databasePragmaChangesSinceLastBackup = 0;
            this.disposed = false;
            this.RowsDroppedSinceLastBackup = 0;
            this.RowsInsertedSinceLastBackup = 0;
            this.RowsUpdatedSinceLastBackup = 0;
            this.schemaChangesSinceLastBackup = 0;

            if (!File.Exists(filePath))
            {
                SQLiteConnection.CreateFile(filePath);
            }
            this.Connection = this.OpenConnection(filePath);
            this.FilePath = filePath;
        }

        /// <summary>
        /// Add a column to the table at position columnNumber using the provided definition.
        /// </summary>
        protected void AddColumnToTable(SQLiteTransaction transaction, string table, int columnNumber, ColumnDefinition columnDefinition)
        {
            // get table's current schema
            SQLiteTableSchema currentSchema = this.GetTableSchema(table);
            if (currentSchema.ColumnDefinitions.Any(column => String.Equals(column.Name, columnDefinition.Name, StringComparison.Ordinal)))
            {
                throw new ArgumentException(String.Format("Column '{0}' is already present in table '{1}'.", columnDefinition.Name, table), nameof(columnDefinition));
            }
            if (columnNumber > currentSchema.ColumnDefinitions.Count)
            {
                throw new ArgumentOutOfRangeException(String.Format("Attempt to add column in position {0} but the '{1}' table has only {2} columns.", columnNumber, table, currentSchema.ColumnDefinitions.Count));
            }

            // optimization for appending column to end of table
            if (columnNumber >= currentSchema.ColumnDefinitions.Count)
            {
                using (SQLiteCommand command = new SQLiteCommand("ALTER TABLE " + table + " ADD COLUMN " + columnDefinition.ToString(), this.Connection))
                {
                    command.ExecuteNonQuery();
                    Debug.Assert(this.Connection.ResultCode() == SQLiteErrorCode.Ok, "Result code indicates error.");
                }
                return;
            }

            // otherwise, SQLite requires copying to a table with a new schema
            SQLiteTableSchema newSchema = new SQLiteTableSchema(table + "Replacement");
            newSchema.ColumnDefinitions.AddRange(currentSchema.ColumnDefinitions);
            newSchema.ColumnDefinitions.Insert(columnNumber, columnDefinition);
            this.CopyTableToNewSchema(transaction, table, newSchema, currentSchema.ColumnDefinitions, currentSchema.ColumnDefinitions);
            this.DropAndReplaceTable(transaction, table, newSchema.Table);
            newSchema.CreateIndices(this.Connection, transaction);

            ++this.schemaChangesSinceLastBackup;
        }

        /// <summary>
        /// Convert the specified column from True/False strings to 1/0 integers.
        /// </summary>
        /// <remarks>
        /// It's assumed the specified column contains only the values True for true.  All other values will be converted to 0 (false).
        /// </remarks>
        protected void ConvertBooleanStringColumnToInteger(SQLiteTransaction transaction, string table, string column)
        {
            this.ConvertStringColumnToInteger(transaction, table, column, () =>
            {
                return ColumnDefinition.CreateBoolean(column);
            },
            (SQLiteTableSchema newSchema) =>
            {
                string commandText = String.Format("UPDATE {0} SET {1} = @{1} WHERE {2} IN (SELECT {2} FROM {3} WHERE {1} = @{1}AsString)", newSchema.Table, column, Constant.DatabaseColumn.ID, table);
                using (SQLiteCommand command = new SQLiteCommand(commandText, this.Connection, transaction))
                {
                    SQLiteParameter integerParameter = new SQLiteParameter("@" + column, true);
                    SQLiteParameter stringParameter = new SQLiteParameter("@" + column + "AsString", Boolean.TrueString);
                    command.Parameters.Add(integerParameter);
                    command.Parameters.Add(stringParameter);

                    command.ExecuteNonQuery();
                    Debug.Assert(this.Connection.ResultCode() == SQLiteErrorCode.Ok, "Result code indicates error.");
                }
            });
        }

        protected void ConvertNonFlagEnumStringColumnToInteger<TEnum>(SQLiteTransaction transaction, string table, string column) where TEnum : struct, IComparable, IFormattable, IConvertible
        {
            this.ConvertStringColumnToInteger(transaction, table, column, () =>
            {
                return new ColumnDefinition(column, Constant.SQLiteAffninity.Integer)
                {
                    DefaultValue = 0.ToString(Constant.InvariantCulture),
                    NotNull = true
                };
            },
            (SQLiteTableSchema newSchema) =>
            {
                string commandText = String.Format("UPDATE {0} SET {1} = @{1} WHERE {2} IN (SELECT {2} FROM {3} WHERE {1} = @{1}AsString)", newSchema.Table, column, Constant.DatabaseColumn.ID, table);
                using (SQLiteCommand command = new SQLiteCommand(commandText, this.Connection, transaction))
                {
                    SQLiteParameter integerParameter = new SQLiteParameter("@" + column);
                    SQLiteParameter stringParameter = new SQLiteParameter("@" + column + "AsString");
                    command.Parameters.Add(integerParameter);
                    command.Parameters.Add(stringParameter);

                    foreach (string name in Enum.GetNames(typeof(TEnum)))
                    {
                        TEnum value = (TEnum)Enum.Parse(typeof(TEnum), name);
                        if (value.Equals(default(TEnum)))
                        {
                            continue;
                        }

                        integerParameter.Value = value.ToInt32(Constant.InvariantCulture);
                        stringParameter.Value = name;
                        command.ExecuteNonQuery();
                        Debug.Assert(this.Connection.ResultCode() == SQLiteErrorCode.Ok, "Result code indicates error.");
                    }
                }
            });
        }

        private void ConvertStringColumnToInteger(SQLiteTransaction transaction, string table, string column, Func<ColumnDefinition> createIntegerColumnDefinition, Action<SQLiteTableSchema> copyNonDefaultValues)
        {
            ColumnDefinition stringColumn = null;
            int stringColumnIndex = -1;
            List<ColumnDefinition> columnsToCopy = new List<ColumnDefinition>();
            SQLiteTableSchema currentSchema = this.GetTableSchema(table);
            for (int columnIndex = 0; columnIndex < currentSchema.ColumnDefinitions.Count; ++columnIndex)
            {
                ColumnDefinition candidateColumn = currentSchema.ColumnDefinitions[columnIndex];
                if (String.Equals(candidateColumn.Name, column, StringComparison.Ordinal))
                {
                    stringColumn = candidateColumn;
                    stringColumnIndex = columnIndex;
                }
                else
                {
                    columnsToCopy.Add(candidateColumn);
                }
            }
            if (stringColumn == null)
            {
                throw new ArgumentOutOfRangeException(nameof(column), String.Format("Column '{0}' is not present in table '{1}'.", column, table));
            }
            if (String.Equals(stringColumn.Type, Constant.SQLiteAffninity.Text, StringComparison.OrdinalIgnoreCase) == false)
            {
                throw new ArgumentException(nameof(column), String.Format("Column '{0}' has type '{1}'.", column, stringColumn.Type));
            }

            // change the column from string to integer
            // This results in a new table identical to the old except that all entries in the column being converted are defaulted.
            SQLiteTableSchema newSchema = new SQLiteTableSchema(currentSchema)
            {
                Table = table + "Replacement"
            };
            newSchema.ColumnDefinitions[stringColumnIndex] = createIntegerColumnDefinition.Invoke();
            Debug.Assert(String.Equals(newSchema.ColumnDefinitions[stringColumnIndex].DefaultValue, "0", StringComparison.Ordinal), "Default value of boolean column is expected to be false.");
            this.CopyTableToNewSchema(transaction, table, newSchema, columnsToCopy, columnsToCopy);

            // copy non-default values
            copyNonDefaultValues.Invoke(newSchema);

            this.DropAndReplaceTable(transaction, table, newSchema.Table);
            newSchema.CreateIndices(this.Connection, transaction);

            ++this.schemaChangesSinceLastBackup;
        }

        /// <summary>
        /// Add, remove, or rename columns in a table.
        /// </summary>
        /// <param name="transaction">The database transaction to use.</param>
        /// <param name="existingTable">The table to modify.</param>
        /// <param name="newSchema">The table's new schema.</param>
        /// <param name="sourceColumns">The existing columns to copy data from.</param>
        /// <param name="destinationColumns">The columns to copy the existing data to.</param>
        /// <remarks>
        /// To
        /// - add a column newColumns has one extra entry (for the new column) and sourceColumns and destinationColumns are the same
        /// - remove a column newColumns, sourceColumns, and destinationColumns are all the same
        /// - rename columns newColumns and destinationColumns are the same and sourceColumns differs only in the column names
        /// </remarks>
        private void CopyTableToNewSchema(SQLiteTransaction transaction, string existingTable, SQLiteTableSchema newSchema, List<ColumnDefinition> sourceColumns, List<ColumnDefinition> destinationColumns)
        {
            if (sourceColumns.Count != destinationColumns.Count)
            {
                throw new ArgumentException(String.Format("Source and destination column lists must be of the same length. Source list has {0} columns and destination list has {1} columns.", sourceColumns.Count, destinationColumns.Count));
            }
            if (newSchema.ColumnDefinitions.Count < sourceColumns.Count)
            {
                throw new ArgumentException(String.Format("Source and destination column lists exceed length of new column list. New column list has {0} columns while the other lists have {1} columns.", newSchema.ColumnDefinitions.Count, sourceColumns.Count));
            }

            // create replacement table with the new schema
            // Since the caller will most likely be dropping the original table and renaming the new table to replace the original
            // don't create indicies at this point. SQLite will remove any exisiting indices when the original table is dropped, 
            // allowing them to be recreated with the same names after the replacement table is renamed into place.
            newSchema.CreateTable(this.Connection, transaction);

            // copy specified part of the old table's contents to the new table
            // SQLite doesn't allow autoincrement columns to be copied but their values are preserved as rows are inserted in the
            // same order.
            List<string> sourceColumnNames = sourceColumns.Where(column => column.Autoincrement == false).Select(column => column.Name).ToList();
            List<string> destinationColumnNames = destinationColumns.Where(column => column.Autoincrement == false).Select(column => column.Name).ToList();

            string copyColumns = "INSERT INTO " + newSchema.Table + " (" + String.Join(", ", destinationColumnNames) + ") SELECT " + String.Join(", ", sourceColumnNames) + " FROM " + existingTable;
            using (SQLiteCommand command = new SQLiteCommand(copyColumns, this.Connection, transaction))
            {
                command.ExecuteNonQuery();
                Debug.Assert(this.Connection.ResultCode() == SQLiteErrorCode.Ok, "Result code indicates error.");
            }
        }

        protected void DeleteColumn(SQLiteTransaction transaction, string table, string column)
        {
            if (String.IsNullOrWhiteSpace(column))
            {
                throw new ArgumentOutOfRangeException(nameof(column));
            }

            SQLiteTableSchema currentSchema = this.GetTableSchema(table);
            if (currentSchema.ColumnDefinitions.Any(columnDefinition => String.Equals(columnDefinition.Name, column, StringComparison.Ordinal)))
            {
                throw new ArgumentOutOfRangeException(nameof(column));
            }

            // drop the requested column from the schema
            SQLiteTableSchema newSchema = new SQLiteTableSchema(currentSchema)
            {
                Table = table + "Replacement"
            };
            int columnToRemove = -1;
            for (int columnIndex = 0; columnIndex < currentSchema.ColumnDefinitions.Count; ++columnIndex)
            {
                ColumnDefinition columnDefinition = currentSchema.ColumnDefinitions[columnIndex];
                if (String.Equals(columnDefinition.Name, column, StringComparison.Ordinal))
                {
                    columnToRemove = columnIndex;
                    break;
                }
            }
            if (columnToRemove == -1)
            {
                throw new ArgumentOutOfRangeException(nameof(column), String.Format("Column '{0}' not found in table '{1}'.", column, table));
            }
            newSchema.ColumnDefinitions.RemoveAt(columnToRemove);

            this.CopyTableToNewSchema(transaction, table, newSchema, newSchema.ColumnDefinitions, newSchema.ColumnDefinitions);
            this.DropAndReplaceTable(transaction, table, newSchema.Table);
            newSchema.CreateIndices(this.Connection, transaction);

            ++this.schemaChangesSinceLastBackup;
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
                if (this.BackupTask != null)
                {
                    this.BackupTask.Wait();
                    this.BackupTask.Dispose();
                }
                this.Connection.Dispose();
            }
            this.disposed = true;
        }

        /// <summary>
        /// Drops the given table and renames the other specified to have the same name as the dropped table.
        /// </summary>
        private void DropAndReplaceTable(SQLiteTransaction transaction, string tableToDrop, string tableToRename)
        {
            // drop the old table and rename the new table into place
            using (SQLiteCommand command = new SQLiteCommand("DROP TABLE " + tableToDrop, this.Connection, transaction))
            {
                command.ExecuteNonQuery();
                Debug.Assert(this.Connection.ResultCode() == SQLiteErrorCode.Ok, "Result code indicates error.");
            }
            using (SQLiteCommand command = new SQLiteCommand("ALTER TABLE " + tableToRename + " RENAME TO " + tableToDrop, this.Connection, transaction))
            {
                command.ExecuteNonQuery();
                Debug.Assert(this.Connection.ResultCode() == SQLiteErrorCode.Ok, "Result code indicates error.");
            }
        }

        protected SQLiteAutoVacuum GetAutoVacuum()
        {
            using (SQLiteCommand command = new SQLiteCommand("PRAGMA AUTO_VACUUM", this.Connection))
            {
                SQLiteAutoVacuum vacuum = (SQLiteAutoVacuum)(long)command.ExecuteScalar();
                Debug.Assert(this.Connection.ResultCode() == SQLiteErrorCode.Ok, "Result code indicates error.");
                return vacuum;
            }
        }

        protected string GetBackupFilePath()
        {
            return Path.Combine(Path.GetDirectoryName(this.FilePath), Path.GetFileNameWithoutExtension(this.FilePath) + Constant.Database.BackupFileNameSuffix + Path.GetExtension(this.FilePath));
        }

        protected int GetCacheSize()
        {
            using (SQLiteCommand command = new SQLiteCommand("PRAGMA CACHE_SIZE", this.Connection))
            {
                int cacheSize = (int)(long)command.ExecuteScalar();
                Debug.Assert(this.Connection.ResultCode() == SQLiteErrorCode.Ok, "Result code indicates error.");
                return cacheSize;
            }
        }

        protected List<object> GetDistinctValuesInColumn(string table, string column)
        {
            using (SQLiteCommand command = new SQLiteCommand("SELECT DISTINCT " + column + " FROM " + table, this.Connection))
            {
                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    List<object> distinctValues = new List<object>();
                    while (reader.Read())
                    {
                        distinctValues.Add(reader[column]);
                    }

                    Debug.Assert(this.Connection.ResultCode() == SQLiteErrorCode.Done, "Result code indicates error.");
                    return distinctValues;
                }
            }
        }

        protected SQLiteJournalModeEnum GetJournalMode()
        {
            using (SQLiteCommand command = new SQLiteCommand("PRAGMA JOURNAL_MODE", this.Connection))
            {
                string mode = (string)command.ExecuteScalar();
                Debug.Assert(this.Connection.ResultCode() == SQLiteErrorCode.Ok, "Result code indicates error.");

                if (String.Equals(mode, "Delete", StringComparison.OrdinalIgnoreCase))
                {
                    return SQLiteJournalModeEnum.Delete;
                }
                else if (String.Equals(mode, "Memory", StringComparison.OrdinalIgnoreCase))
                {
                    return SQLiteJournalModeEnum.Memory;
                }
                else if (String.Equals(mode, "Off", StringComparison.OrdinalIgnoreCase))
                {
                    return SQLiteJournalModeEnum.Off;
                }
                else if (String.Equals(mode, "Persist", StringComparison.OrdinalIgnoreCase))
                {
                    return SQLiteJournalModeEnum.Persist;
                }
                else if (String.Equals(mode, "Truncate", StringComparison.OrdinalIgnoreCase))
                {
                    return SQLiteJournalModeEnum.Truncate;
                }
                else if (String.Equals(mode, "Wal", StringComparison.OrdinalIgnoreCase))
                {
                    return SQLiteJournalModeEnum.Wal;
                }
                else
                {
                    throw new NotSupportedException(String.Format("Unhandled synchronous mode '{0}'.", mode));
                }
            }
        }

        protected SQLiteLockMode GetLockingMode()
        {
            using (SQLiteCommand command = new SQLiteCommand("PRAGMA LOCKING_MODE", this.Connection))
            {
                string mode = (string)command.ExecuteScalar();
                Debug.Assert(this.Connection.ResultCode() == SQLiteErrorCode.Ok, "Result code indicates error.");

                if (String.Equals(mode, "Exclusive", StringComparison.OrdinalIgnoreCase))
                {
                    return SQLiteLockMode.Exclusive;
                }
                else if (String.Equals(mode, "Normal", StringComparison.OrdinalIgnoreCase))
                {
                    return SQLiteLockMode.Normal;
                }
                else
                {
                    throw new NotSupportedException(String.Format("Unhandled synchronous mode '{0}'.", mode));
                }
            }
        }

        protected SynchronizationModes GetSynchronous()
        {
            using (SQLiteCommand command = new SQLiteCommand("PRAGMA SYNCHRONOUS", this.Connection))
            {
                // as of SQLite 1.0.109.1 SynchronizationModes.Extra is missing; this can be worked around if needed
                SynchronizationModes mode = (SynchronizationModes)(long)command.ExecuteScalar();
                Debug.Assert(this.Connection.ResultCode() == SQLiteErrorCode.Ok, "Result code indicates error.");
                return mode;
            }
        }

        protected List<string> GetTableNames()
        {
            using (SQLiteCommand command = new SQLiteCommand("SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY name", this.Connection))
            {
                SQLiteDataReader reader = command.ExecuteReader();

                List<string> tableNames = new List<string>();
                while (reader.Read())
                {
                    tableNames.Add(reader[0].ToString());
                }

                Debug.Assert(this.Connection.ResultCode() == SQLiteErrorCode.Done, "Result code indicates error.");
                return tableNames;
            }
        }

        protected SQLiteTableSchema GetTableSchema(string table)
        {
            // as of SQLite 1.0.108 columns are returned in order so sorting by number is not strictly necessary
            // Column numbering is explicitly respected in the order of return for robustness, however.
            Dictionary<int, ColumnDefinition> columnDefinitionsByNumber = new Dictionary<int, ColumnDefinition>();
            using (SQLiteCommand command = new SQLiteCommand("PRAGMA TABLE_INFO(" + table + ")", this.Connection))
            {
                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Debug.Assert(reader.FieldCount > 2, "Encountered incomplete column.");
                        // field 1 is column name
                        // field 2 is type
                        ColumnDefinition columnDefinition = new ColumnDefinition(reader.GetString(1), reader.GetString(2));

                        if ((reader.FieldCount > 3) && (reader.GetInt32(3) != 0))
                        {
                            columnDefinition.NotNull = true;
                        }
                        if ((reader.FieldCount > 4) && (reader.IsDBNull(4) == false))
                        {
                            columnDefinition.DefaultValue = reader.GetString(4);
                            if ((reader.GetFieldType(4) == typeof(string)) && 
                                (columnDefinition.DefaultValue.Length > 1) &&
                                (columnDefinition.DefaultValue[0] == '\'') &&
                                (columnDefinition.DefaultValue[columnDefinition.DefaultValue.Length - 1] == '\''))
                            {
                                // SQLite 1.0.108 returns default values which are strings as 'Default Value' rather than Default Value
                                // Remove the leading and trailing quotes to avoid their accumulation as table schemas are read and
                                // modified.
                                columnDefinition.DefaultValue = columnDefinition.DefaultValue.Substring(1, columnDefinition.DefaultValue.Length - 2);
                            }
                        }
                        if ((reader.FieldCount > 5) && (reader.GetInt32(5) != 0))
                        {
                            columnDefinition.PrimaryKey = true;
                            // other constraints that may be set in the table schema aren't supported, including UNIQUE, 
                            // CHECK, FOREIGN KEYS, AUTOINCREMENT 
                            // Workaround: as all Carnassial tables have autoincrement primary keys the two flags are currently \
                            // equivalent
                            columnDefinition.Autoincrement = true;
                        }

                        // field 0 is column number
                        columnDefinitionsByNumber.Add(reader.GetInt32(0), columnDefinition);
                    }
                    Debug.Assert(this.Connection.ResultCode() == SQLiteErrorCode.Done, "Result code indicates error.");
                }
            }

            // add columns to schema
            SQLiteTableSchema schema = new SQLiteTableSchema(table);
            for (int columnNumber = 0; columnNumber < columnDefinitionsByNumber.Count; ++columnNumber)
            {
                schema.ColumnDefinitions.Add(columnDefinitionsByNumber[columnNumber]);
            }

            // add secondary indices to schema
            // Currently, only indices on single columns are supported.
            List<string> tableIndices = new List<string>();
            using (SQLiteCommand command = new SQLiteCommand("PRAGMA INDEX_LIST(" + table + ")", this.Connection))
            {
                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string origin = reader.GetString(3);
                        if (String.Equals(origin, "c", StringComparison.Ordinal))
                        {
                            tableIndices.Add(reader.GetString(1));
                        }
                        else
                        {
                            throw new NotSupportedException(String.Format("Unhandled index origin '{0}'.", origin));
                        }
                    }
                    Debug.Assert(this.Connection.ResultCode() == SQLiteErrorCode.Done, "Result code indicates error.");
                }
            }

            foreach (string index in tableIndices)
            {
                using (SQLiteCommand command = new SQLiteCommand("PRAGMA INDEX_INFO(" + index + ")", this.Connection))
                {
                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            schema.Indices.Add(new SecondaryIndex(table, index, reader.GetString(2)));
                        }
                        Debug.Assert(this.Connection.ResultCode() == SQLiteErrorCode.Done, "Result code indicates error.");
                    }
                }
            }

            return schema;
        }

        protected SQLiteTemporaryStore GetTemporaryStore()
        {
            using (SQLiteCommand command = new SQLiteCommand("PRAGMA TEMP_STORE", this.Connection))
            {
                SQLiteTemporaryStore store = (SQLiteTemporaryStore)(long)command.ExecuteScalar();
                Debug.Assert(this.Connection.ResultCode() == SQLiteErrorCode.Ok, "Result code indicates error.");
                return store;
            }
        }

        protected Version GetUserVersion()
        {
            using (SQLiteCommand command = new SQLiteCommand("PRAGMA USER_VERSION", this.Connection))
            {
                int version = (int)(long)command.ExecuteScalar();
                Debug.Assert(this.Connection.ResultCode() == SQLiteErrorCode.Ok, "Result code indicates error.");
                return new Version((version & 0x7f000000) >> 24, (version & 0x00ff0000) >> 16, (version & 0x0000ff00) >> 8, version & 0x000000ff);
            }
        }

        protected int GetWalAutocheckpoint()
        {
            using (SQLiteCommand command = new SQLiteCommand("PRAGMA WAL_AUTOCHECKPOINT", this.Connection))
            {
                int interval = (int)(long)command.ExecuteScalar();
                Debug.Assert(this.Connection.ResultCode() == SQLiteErrorCode.Ok, "Result code indicates error.");
                return interval;
            }
        }

        protected void IncrementalVacuum(SQLiteTransaction transaction)
        {
            using (SQLiteCommand command = new SQLiteCommand("PRAGMA INCREMENTAL_VACUUM", this.Connection, transaction))
            {
                command.ExecuteNonQuery();
                Debug.Assert(this.Connection.ResultCode() == SQLiteErrorCode.Ok, "Result code indicates error.");
            }
        }

        protected void LoadDataTableFromSelect<TRow>(SQLiteTable<TRow> table, Select select)
        {
            using (SQLiteCommand command = select.CreateSelect(this.Connection))
            {
                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    table.Load(reader);
                    Debug.Assert(this.Connection.ResultCode() == SQLiteErrorCode.Done, "Result code indicates error.");
                }
            }
        }

        protected SQLiteConnection OpenConnection(string databaseFilePath)
        {
            SQLiteConnectionStringBuilder connectionStringBuilder = new SQLiteConnectionStringBuilder()
            {
                DataSource = databaseFilePath,
                DateTimeKind = DateTimeKind.Utc,
                JournalMode = SQLiteJournalModeEnum.Memory
            };

            SQLiteConnection connection = new SQLiteConnection(connectionStringBuilder.ConnectionString);
            connection.Open();
            this.SetTemporaryStore(connection, SQLiteTemporaryStore.Memory);
            return connection;
        }

        protected void Optimize()
        {
            using (SQLiteCommand command = new SQLiteCommand("PRAGMA OPTIMIZE", this.Connection))
            {
                command.ExecuteNonQuery();
                Debug.Assert(this.Connection.ResultCode() == SQLiteErrorCode.Ok, "Result code indicates error.");
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

        protected void RenameColumn(SQLiteTransaction transaction, string table, string currentColumnName, string newColumnName, Action<ColumnDefinition> modifyColumnDefinition)
        {
            if (String.IsNullOrWhiteSpace(currentColumnName))
            {
                throw new ArgumentOutOfRangeException(nameof(currentColumnName));
            }
            if (String.IsNullOrWhiteSpace(newColumnName))
            {
                throw new ArgumentOutOfRangeException(nameof(newColumnName));
            }

            SQLiteTableSchema currentSchema = this.GetTableSchema(table);
            if (currentSchema.ColumnDefinitions.Any(column => String.Equals(column.Name, currentColumnName, StringComparison.Ordinal)) == false)
            {
                throw new ArgumentException(String.Format("No column named '{0}' exists to rename.", currentColumnName), nameof(currentColumnName));
            }
            if (currentSchema.ColumnDefinitions.Any(column => String.Equals(column.Name, newColumnName, StringComparison.Ordinal)))
            {
                throw new ArgumentException(String.Format("Column named '{0}' already exists.", newColumnName), nameof(newColumnName));
            }

            SQLiteTableSchema newSchema = new SQLiteTableSchema(currentSchema)
            {
                Table = table + "Replacement"
            };
            for (int columnIndex = 0; columnIndex < newSchema.ColumnDefinitions.Count; ++columnIndex)
            {
                ColumnDefinition column = newSchema.ColumnDefinitions[columnIndex];
                if (String.Equals(column.Name, currentColumnName, StringComparison.Ordinal))
                {
                    ColumnDefinition columnWithNameChanged = new ColumnDefinition(column)
                    {
                        Name = newColumnName
                    };
                    modifyColumnDefinition.Invoke(columnWithNameChanged);
                    newSchema.ColumnDefinitions[columnIndex] = columnWithNameChanged;
                    break;
                }
            }

            this.CopyTableToNewSchema(transaction, table, newSchema, currentSchema.ColumnDefinitions, newSchema.ColumnDefinitions);
            this.DropAndReplaceTable(transaction, table, newSchema.Table);
            newSchema.CreateIndices(this.Connection, transaction);

            ++this.schemaChangesSinceLastBackup;
        }

        protected void RenameTable(SQLiteTransaction transaction, string currentTable, string newTable)
        {
            using (SQLiteCommand command = new SQLiteCommand("ALTER TABLE " + currentTable + " RENAME TO " + newTable, this.Connection, transaction))
            {
                command.ExecuteNonQuery();
                Debug.Assert(this.Connection.ResultCode() == SQLiteErrorCode.Ok, "Result code indicates error.");
            }

            ++this.schemaChangesSinceLastBackup;
        }

        protected void SetAutoVacuum(SQLiteAutoVacuum vacuum)
        {
            using (SQLiteCommand command = new SQLiteCommand("PRAGMA AUTO_VACUUM = " + (int)vacuum, this.Connection))
            {
                command.ExecuteNonQuery();
                Debug.Assert(this.Connection.ResultCode() == SQLiteErrorCode.Ok, "Result code indicates error.");
            }

            ++this.databasePragmaChangesSinceLastBackup;
        }

        protected void SetLockingMode(SQLiteLockMode mode)
        {
            using (SQLiteCommand command = new SQLiteCommand("PRAGMA LOCKING_MODE = " + mode.ToString().ToUpperInvariant(), this.Connection))
            {
                command.ExecuteNonQuery();
                Debug.Assert(this.Connection.ResultCode() == SQLiteErrorCode.Ok, "Result code indicates error.");
            }

            ++this.databasePragmaChangesSinceLastBackup;
        }

        protected void SetTemporaryStore(SQLiteTemporaryStore store)
        {
            this.SetTemporaryStore(this.Connection, store);
        }

        private void SetTemporaryStore(SQLiteConnection connection, SQLiteTemporaryStore store)
        {
            using (SQLiteCommand command = new SQLiteCommand("PRAGMA TEMP_STORE = " + (int)store, connection))
            {
                command.ExecuteNonQuery();
                Debug.Assert(connection.ResultCode() == SQLiteErrorCode.Ok, "Result code indicates error.");
            }
        }

        protected void SetUserVersion(SQLiteTransaction transaction, Version version)
        {
            if ((version.Major < 0) || (version.Major > 127))
            {
                throw new ArgumentOutOfRangeException(nameof(version), "Major version must be between 0 and 127, inclusive.");
            }
            if ((version.Minor < 0) || (version.Minor > 255))
            {
                throw new ArgumentOutOfRangeException(nameof(version), "Minor version must be between 0 and 255, inclusive.");
            }
            if ((version.Build < 0) || (version.Build > 255))
            {
                throw new ArgumentOutOfRangeException(nameof(version), "Build must be between 0 and 255, inclusive.");
            }
            if ((version.Revision < 0) || (version.Revision > 255))
            {
                throw new ArgumentOutOfRangeException(nameof(version), "Revision must be between 0 and 255, inclusive.");
            }

            int versionAsInt = (version.Major << 24) | (version.Minor << 16) | (version.Build << 8) | version.Revision;
            using (SQLiteCommand command = new SQLiteCommand("PRAGMA USER_VERSION(" + versionAsInt.ToString(Constant.InvariantCulture) + ")", this.Connection, transaction))
            {
                command.ExecuteNonQuery();
                Debug.Assert(this.Connection.ResultCode() == SQLiteErrorCode.Ok, "Result code indicates error.");
            }

            ++this.databasePragmaChangesSinceLastBackup;
        }

        protected void SetWalAutocheckpoint(int interval)
        {
            using (SQLiteCommand command = new SQLiteCommand("PRAGMA WAL_AUTOCHECKPOINT = " + interval, this.Connection))
            {
                command.ExecuteNonQuery();
                Debug.Assert(this.Connection.ResultCode() == SQLiteErrorCode.Ok, "Result code indicates error.");
            }
        }

        public async Task<bool> TryBackupAsync()
        {
            int pendingChanges = this.databasePragmaChangesSinceLastBackup + this.RowsDroppedSinceLastBackup + this.RowsInsertedSinceLastBackup + this.RowsUpdatedSinceLastBackup + this.schemaChangesSinceLastBackup;
            if (pendingChanges < 1)
            {
                // nothing to do if no changes
                return false;
            }

            if (this.BackupTask != null)
            {
                this.BackupTask.Wait();
                this.BackupTask.Dispose();
            }

            this.BackupTask = Task.Run(() =>
            {
                string backupFilePath = this.GetBackupFilePath();
                using (SQLiteConnection connectionToBackup = this.OpenConnection(backupFilePath))
                {
                    this.databasePragmaChangesSinceLastBackup = 0;
                    this.RowsDroppedSinceLastBackup = 0;
                    this.RowsInsertedSinceLastBackup = 0;
                    this.RowsUpdatedSinceLastBackup = 0;
                    this.schemaChangesSinceLastBackup = 0;

                    this.Connection.BackupDatabase(connectionToBackup, Constant.Sql.MainDatabase, Constant.Sql.MainDatabase, -1, null, Constant.Database.BackupRetryIntervalInMilliseconds);
                }
                return true;
            });
            return await this.BackupTask;
        }

        protected void WalCheckpoint(SQLiteWalCheckpoint checkpoint)
        {
            using (SQLiteCommand command = new SQLiteCommand("PRAGMA WAL_CHECKPOINT(" + checkpoint.ToString().ToUpperInvariant() + ")", this.Connection))
            {
                command.ExecuteNonQuery();
                Debug.Assert(this.Connection.ResultCode() == SQLiteErrorCode.Ok, "Result code indicates error.");
            }
        }
    }
}