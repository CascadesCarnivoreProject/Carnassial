using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using Timelapse.Util;

namespace Timelapse.Database
{
    // A wrapper to make it easy to invoke some basic SQLite commands
    // It is NOT a generalized wrapper, as it only handles a few simple things.
    public class SQLiteWrapper
    {
        // A connection string identifying the  database file. Takes the form:
        // "Data Source=filepath" 
        private string connectionString;

        #region Constructors
        /// <summary>
        /// Constructor: Create a database file if it does not exist, and then create a connection string to that file
        /// If the DB file does not exist, It will be created
        /// </summary>
        /// <param name="inputFile">The File containing the DB</param>
        public SQLiteWrapper(string inputFile)
        {
            if (!File.Exists(inputFile))
            {
                SQLiteConnection.CreateFile(inputFile);
            }
            this.connectionString = String.Format("{0}{1}", Constants.Sql.DataSource, inputFile);
        }

        /// <summary>
        /// Constructor (Advanced): This version specifies advanced connection options.
        /// The Dictionary parameter contains key, value pairs, which is used to construct the connection string looking like
        /// key1=value1; key2=value2; ...
        /// </summary>
        /// <param name="connectionOptions">A dictionary containing all desired options and their values</param>
        public SQLiteWrapper(Dictionary<string, string> connectionOptions)
        {
            string str = String.Empty;
            foreach (KeyValuePair<string, string> row in connectionOptions)
            {
                str += String.Format("{0}={1}; ", row.Key, row.Value);
            }
            str = str.Trim().Substring(0, str.Length - 1);
            this.connectionString = str;
        }
        #endregion

        #region Table Creation 
        /// <summary>
        /// A simplified table creation routine. It expects the column definitions to be supplied
        /// as a column_name, data type key value pair. 
        /// </summary>
        public void CreateTable(string tableName, List<ColumnTuple> columnDefinitions)
        {
            // The table creation syntax supported is:
            // CREATE TABLE table_name (
            //     column1name datatype,       e.g.,   Id INT PRIMARY KEY OT NULL,
            //     column2name datatype,               NAME TEXT NOT NULL,
            //     ...                                 ...
            //     columnNname datatype);              SALARY REAL);
            string query = Constants.Sql.CreateTable + tableName + Constants.Sql.OpenParenthesis + Environment.NewLine;               // CREATE TABLE <tablename> (
            foreach (ColumnTuple column in columnDefinitions)
            {
                query += column.Name + " " + column.Value + Constants.Sql.Comma + Environment.NewLine;             // columnname datatype,
            }
            query = query.Remove(query.Length - Constants.Sql.Comma.Length - Environment.NewLine.Length);         // remove last comma / new line and replace with );
            query += Constants.Sql.CloseParenthesis + Constants.Sql.Semicolon;
            this.ExecuteNonQuery(query);
        }

        // Assumes they have exactly the same creation string
        public void Insert(string tableName, DataTable datatable)
        {
            List<string> queries = new List<string>();
            for (int i = 0; i < datatable.Rows.Count; i++)
            {
                string query = Constants.Sql.InsertInto + tableName + " VALUES ";                             // INSERT INTO table_name;
                string values = String.Empty;
                for (int j = 0; j < datatable.Columns.Count; j++)
                {
                    if (j == 0)
                    {
                        values += String.Format(" {0}" + Constants.Sql.Comma, i + 1);
                    }
                    else if (j == 1 | j == 2)
                    {
                        values += String.Format(" {0}" + Constants.Sql.Comma, datatable.Rows[i][j]);         // "value1, value2, ... valueN"
                    }
                    else
                    {
                        string newvalue = (string)datatable.Rows[i][j];
                        newvalue = newvalue.Replace("'", "''");
                        values += String.Format(" {0}" + Constants.Sql.Comma, Utilities.QuoteForSql(newvalue));         // "'value1', 'value2', ... 'valueN'"
                    }
                }
                values = values.Substring(0, values.Length - Constants.Sql.Comma.Length);        // Remove last comma in the sequence 
                query += String.Format("({0}); ", values);                          // ('value1', 'value2', ... 'valueN');
                queries.Add(query);
            }

            this.ExecuteNonQueryWrappedInBeginEnd(queries);
        }
        #endregion

        #region Insertion: Single Row
        public void Insert(string tableName, List<List<ColumnTuple>> insertionStatements)
        {
            // Construct each individual query in the form 
            // INSERT INTO table_name
            //      colname1, colname12, ... colnameN VALUES
            //      ('value1', 'value2', ... 'valueN');
            List<string> queries = new List<string>();
            foreach (List<ColumnTuple> columnsToUpdate in insertionStatements)
            {
                string columns = String.Empty;
                string values = String.Empty;
                foreach (ColumnTuple column in columnsToUpdate)
                {
                    columns += String.Format(" {0}" + Constants.Sql.Comma, column.Name);      // transform dictionary entries into a string "col1, col2, ... coln"
                    values += String.Format(" {0}" + Constants.Sql.Comma, Utilities.QuoteForSql(column.Value));         // transform dictionary entries into a string "'value1', 'value2', ... 'valueN'"
                }
                if (columns.Length > 0)
                {
                    columns = columns.Substring(0, columns.Length - Constants.Sql.Comma.Length);     // Remove last comma in the sequence to produce (col1, col2, ... coln)  
                }
                if (values.Length > 0)
                {
                    values = values.Substring(0, values.Length - Constants.Sql.Comma.Length);        // Remove last comma in the sequence 
                }

                // Construct the query. The newlines are to format it for pretty printing
                string query = Constants.Sql.InsertInto + tableName;               // INSERT INTO table_name
                query += Environment.NewLine;
                query += String.Format("({0}) ", columns);                         // (col1, col2, ... coln)
                query += Environment.NewLine;
                query += Constants.Sql.Values;                                     // VALUES
                query += Environment.NewLine;
                query += String.Format("({0}); ", values);                         // ('value1', 'value2', ... 'valueN');
                queries.Add(query);
            }

            // Now try to invoke the batch queries
            this.ExecuteNonQueryWrappedInBeginEnd(queries);
        }
        #endregion

        #region Select Invocations
        /// <summary>
        /// Run a generic select query against the database, with results returned as rows in a DataTable.
        /// </summary>
        /// <param name="query">The SQL to run</param>
        /// <returns>A DataTable containing the result set.</returns>
        public DataTable GetDataTableFromSelect(string query)
        {
            try
            {
                // Open the connection
                using (SQLiteConnection connection = new SQLiteConnection(this.connectionString))
                {
                    connection.Open();
                    using (SQLiteCommand command = new SQLiteCommand(connection))
                    {
                        command.CommandText = query;
                        using (SQLiteDataReader reader = command.ExecuteReader())
                        {
                            DataTable dataTable = new DataTable();
                            dataTable.Load(reader);
                            return dataTable;
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.Assert(false, String.Format("Failure executing query '{0}'."), exception.ToString());
                return null;
            }
        }

        /// <summary>
        /// Run a generic Select query against the Database, with a single result returned as an object that must be cast. 
        /// </summary>
        /// <param name="query">The SQL to run</param>
        /// <returns>A value containing the single result.</returns>
        private object GetObjectFromSelect(string query)
        {
            try
            {
                // Open the connection
                using (SQLiteConnection connection = new SQLiteConnection(this.connectionString))
                {
                    connection.Open();
                    using (SQLiteCommand command = new SQLiteCommand(connection))
                    {
                        command.CommandText = query;
                        return command.ExecuteScalar();
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.Assert(false, String.Format("Failure executing query '{0}'.", query), exception.ToString());
                return null;
            }
        }
        #endregion

        #region Query Execution
        /// <summary>
        /// Allows the programmer to interact with the database for purposes other than a query.
        /// </summary>
        /// <param name="statement">The SQL to be run.</param>
        public void ExecuteNonQuery(string statement)
        {
            try
            {
                using (SQLiteConnection connection = new SQLiteConnection(this.connectionString))
                {
                    connection.Open();
                    using (SQLiteCommand command = new SQLiteCommand(connection))
                    {
                        command.CommandText = statement;
                        int rowsUpdated = command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.Assert(false, String.Format("Failure executing statement '{0}'.", statement), exception.ToString());
            }
        }

        /// <summary>
        /// Given a list of complete queries, wrap up to 500 of them in a BEGIN/END statement so they are all executed in one go for efficiency
        /// Continue for the next up to 500, and so on.
        /// </summary>
        public void ExecuteNonQueryWrappedInBeginEnd(List<string> statements)
        {
            // BEGIN
            //      query1
            //      query2
            //      ...
            //      queryn
            // END
            const int MaxStatementCount = 500;
            string mostRecentStatement = null;
            try
            {
                using (SQLiteConnection connection = new SQLiteConnection(this.connectionString))
                {
                    connection.Open();

                    using (SQLiteCommand command = new SQLiteCommand(connection))
                    {
                        // Invoke each query in the queries list
                        int rowsUpdated = 0;
                        int statementsInQuery = 0;
                        foreach (string statement in statements)
                        {
                            // capture the most recent statement so it's available for debugging
                            mostRecentStatement = statement;
                            statementsInQuery++;

                            // Insert a BEGIN if we are at the beginning of the count
                            if (statementsInQuery == 1)
                            {
                                command.CommandText = Constants.Sql.Begin;
                                command.ExecuteNonQuery();
                                // Debug.Print(command.CommandText);
                            }

                            command.CommandText = statement;
                            rowsUpdated += command.ExecuteNonQuery();
                            //  Debug.Print(command.CommandText);

                            // END
                            if (statementsInQuery >= MaxStatementCount)
                            {
                                command.CommandText = Constants.Sql.End;
                                rowsUpdated += command.ExecuteNonQuery();
                                statementsInQuery = 0;
                                // Debug.Print(command.CommandText);

                            }
                        }
                        // END
                        if (statementsInQuery != 0)
                        {
                            command.CommandText = Constants.Sql.End;
                            rowsUpdated += command.ExecuteNonQuery();
                            // Debug.Print(command.CommandText);
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.Assert(false, String.Format("Failure near executing statement '{0}'.", mostRecentStatement), exception.ToString());
            }
        }
        #endregion

        #region Updates
        // Trims all the white space from the data held in the list of column_names int table_name
        // Note: this is needed as earlier versions didn't trim the white space from the data. This allows us to trim it in the database after the fact.
        public void TrimWhitespace(string tableName, List<string> columnNames)
        {
            foreach (string columnName in columnNames)
            {
                string command = "Update " + tableName + " SET " + columnName + " = TRIM (" + columnName + ")"; // Form: UPDATE tablename SET columname = TRIM(columnname)
                this.ExecuteNonQuery(command);  // TODO MAKE THIS ALL IN ONE OPERATION
            }
        }

        public void Update(string tableName, List<ColumnTuplesWithWhere> updateQueryList)
        {
            // TODO: support splitting the query into 100 row (or similar size) chunks here rather than requiring all callers implement it
            List<string> queries = new List<string>();
            foreach (ColumnTuplesWithWhere updateQuery in updateQueryList)
            {
                string query = this.CreateUpdateQuery(tableName, updateQuery);
                if (query.Equals(String.Empty))
                {
                    continue; // skip non-queries
                }
                queries.Add(query);
            }

            this.ExecuteNonQueryWrappedInBeginEnd(queries);
        }

        /// <summary>
        /// Update specific rows in the DB as specified in the where clause.
        /// </summary>
        /// <param name="tableName">The table to update.</param>
        /// <param name="columnsToUpdate">The column names and their new values.</param>
        public void Update(string tableName, ColumnTuplesWithWhere columnsToUpdate)
        {
            // UPDATE table_name SET 
            // colname1 = value1, 
            // colname2 = value2,
            // ...
            // colnameN = valueN
            // WHERE
            // <condition> e.g., ID=1;
            string query = this.CreateUpdateQuery(tableName, columnsToUpdate);
            this.ExecuteNonQuery(query);
        }

        // Return a single update query as a string
        private string CreateUpdateQuery(string tableName, ColumnTuplesWithWhere columnsToUpdate)
        {
            // UPDATE tableName SET 
            // colname1 = value1, 
            // colname2 = value2,
            // ...
            // colnameN = valueN
            // WHERE
            // <condition> e.g., ID=1;
            string query = Constants.Sql.Update + tableName + Constants.Sql.Set;
            if (columnsToUpdate.Columns.Count < 0)
            {
                return String.Empty;     // No data, so nothing to update. This isn't really an error, so...
            }

            // column_name = 'value'
            foreach (ColumnTuple column in columnsToUpdate.Columns)
            {
                // we have to cater to different formats for integers, NULLS and strings...
                if (column.Value == null)
                {
                    query += String.Format(" {0} = {1}{2}", column.Name.ToString(), Constants.Sql.Null, Constants.Sql.Comma);
                }
                else
                {
                    query += String.Format(" {0} = {1}{2}", column.Name, Utilities.QuoteForSql(column.Value), Constants.Sql.Comma);
                }
            }
            query = query.Substring(0, query.Length - Constants.Sql.Comma.Length); // Remove the last comma

            if (String.IsNullOrWhiteSpace(columnsToUpdate.Where) == false)
            {
                query += Constants.Sql.Where;
                query += columnsToUpdate.Where;
            }
            query += Constants.Sql.Semicolon;
            return query;
        }
        #endregion

        #region Counts
        /// <summary>
        /// Retrieve a count of items from the DB. Select statement must be of the form "Select Count(*) FROM "
        /// </summary>
        /// <param name="query">The query to run.</param>
        /// <returns>The number of items selected.</returns>
        public int GetCountFromSelect(string query)
        {
            return Convert.ToInt32(this.GetObjectFromSelect(query));
        }
        #endregion

        #region Columns: Checking and Adding 
        // This method will check if a column exists in a table
        public bool IsColumnInTable(string tableName, string columnName)
        {
            // TODO: change this to a proper IF EXISTS rather than relying on a try catch
            try
            {
                // Open the connection
                using (SQLiteConnection connection = new SQLiteConnection(this.connectionString))
                {
                    connection.Open();
                    using (SQLiteCommand command = new SQLiteCommand(connection))
                    {
                        command.CommandText = "SELECT " + columnName + " from " + tableName;
                        using (SQLiteDataReader reader = command.ExecuteReader())
                        {
                            DataTable dataTable = new DataTable();
                            dataTable.Load(reader);
                            return true;
                        }
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        // This method will create a column in a table, where it is added to its end
        public void CreateColumn(string tableName, string columnName)
        {
            string query = "ALTER TABLE " + tableName + " ADD COLUMN " + columnName + " TEXT";
            this.ExecuteNonQuery(query);
        }
        #endregion

        #region Deleting Rows 
        /// <summary>delete a single row from the DB as specified in the where clause ...</summary>
        /// <param name="tableName">The table from which to delete </param>
        /// <param name="where">The where clause for the row to delete (e.g., ID=1).</param>
        public void Delete(string tableName, string where)
        {
            // DELETE FROM table_name WHERE where
            string query = Constants.Sql.DeleteFrom + tableName;        // DELETE FROM table_name
            if (!String.IsNullOrWhiteSpace(where))
            {
                // Add the WHERE clause only when where is not empty
                query += Constants.Sql.Where;                   // WHERE
                query += where;                                 // where
            }
            this.ExecuteNonQuery(query);
        }
        /// <summary>
        /// Delete one or more rows from the DB, where each row is specified in the list of where clauses ..
        /// </summary>
        /// <param name="tableName">The table from which to delete</param>
        /// <param name="WhereList">The where clauses for the row to delete (e.g., ID=1 ID=3 etc</param>
        public void Delete(string tableName, List<string> WhereList)
        {
            List<string> queries = new List<string>();                      // A list of SQL queries

            // Construct a list containing queries of the form DELETE FROM table_name WHERE where
            foreach (string whereClause in WhereList)
            {
                // Add the WHERE clause only when where is not empty
                if (!whereClause.Trim().Equals(String.Empty))
                {                                                            // Construct each query statement
                    string query = Constants.Sql.DeleteFrom + tableName;     // DELETE FROM table_name
                    query += Constants.Sql.Where;                            // DELETE FROM table_name WHERE
                    query += whereClause;                                    // DELETE FROM table_name WHERE whereClause
                    query += "; ";                                           // DELETE FROM table_name WHERE whereClause;
                    queries.Add(query);
                }
            }
            // Now try to invoke the batch queries
            if(queries.Count > 0)
            {
                this.ExecuteNonQueryWrappedInBeginEnd(queries);
            }
        }
        #endregion

        #region Public methods for Add/Delete/Rename Columns
        /// <summary>
        /// Add a column to the table named sourceTable at position columnNumber using the provided columnDefinition
        /// </summary>
        public bool AddColumn(string sourceTable, int columnNumber, string columnDefinition)
        {
            try
            {
                using (SQLiteConnection dbConnection = new SQLiteConnection(this.connectionString))
                {
                    dbConnection.Open();

                    // Some basic error checking to make sure we can do the operation
                    List<string> columnNames = GetColumnNamesAsList(dbConnection, sourceTable);

                    // get the column name (the first word) from the column definition
                    string columnName = (columnDefinition.IndexOf(" ") == -1) ? columnDefinition : columnDefinition.Substring(0, columnDefinition.IndexOf(" "));

                    // Check if a column called columnName already exists in the source Table. If so, abort as we cannot add duplicate column names
                    if (columnNames.Contains(columnName))
                    {
                        return false; // A column called columnName already exists in the source Table
                    }

                    // If columnNumber would result in the column being inserted at the end of the table, then use the more efficient method to do so.
                    if (columnNumber >= columnNames.Count)
                    {
                        AddColumnToEndOfTable(dbConnection, sourceTable, columnDefinition);
                        return true;
                    }

                    // We need to add a column elsewhere than the end. This requires us to 
                    // create a new schema, create a new table from that schema, copy data over to it, remove the old table
                    // and rename the new table to the name of the old one.

                    // Get a schema definition identical to the schema in the existing table, 
                    // but with a new column definition added at the given position 
                    string newSchema = this.CloneSchemaButWithAddedColumn(dbConnection, sourceTable, columnNumber, columnDefinition);

                    // Create a new table 
                    string destTable = sourceTable + "NEW";
                    string sql = "CREATE TABLE " + destTable + " (" + newSchema + ")";
                    SQLiteCommand command = new SQLiteCommand(sql, dbConnection);
                    command.ExecuteNonQuery();

                    // Copy the old table's contents to the new table
                    this.CopyAllValuesFromTable(dbConnection, sourceTable, sourceTable, destTable);

                    // Now drop the source table and rename the destination table to that of the source table
                    this.DropTable(dbConnection, sourceTable);

                    // Rename the table
                    this.RenameTable(dbConnection, destTable, sourceTable);
                    return true;
                }
            }
            catch (Exception exception)
            {
                Debug.Assert(false, String.Format("Failure in AddColumn executing query '{0}'."), exception.ToString());
                return false;
            }
        }

        public bool DeleteColumn(string sourceTable, string columnName)
        {
            try
            {
                using (SQLiteConnection dbConnection = new SQLiteConnection(this.connectionString))
                {
                    dbConnection.Open();
                    // Some basic error checking to make sure we can do the operation
                    if (columnName.Trim() == String.Empty)
                    {
                        return false;  // The provided column names= is an empty string
                    }
                    List<string> columnNames = GetColumnNamesAsList(dbConnection, sourceTable);
                    if (!columnNames.Contains(columnName))
                    {
                        return false; // There is no column called columnName in the source Table, so we can't delete ti
                    }

                    // Get a schema definition identical to the schema in the existing table, 
                    // but with the column named columnName deleted from it
                    string newSchema = this.CloneSchemaButDeleteNamedColumn(dbConnection, sourceTable, columnName);

                    // Create a new table 
                    string destTable = sourceTable + "NEW";
                    string sql = "CREATE TABLE " + destTable + " (" + newSchema + ")";
                    SQLiteCommand command = new SQLiteCommand(sql, dbConnection);
                    command.ExecuteNonQuery();

                    // Copy the old table's contents to the new table
                    this.CopyAllValuesFromTable(dbConnection, destTable, sourceTable, destTable);

                    // Now drop the source table and rename the destination table to that of the source table
                    this.DropTable(dbConnection, sourceTable);

                    // Rename the table
                    this.RenameTable(dbConnection, destTable, sourceTable);
                    return true;
                }
            }
            catch (Exception exception)
            {
                Debug.Assert(false, String.Format("Failure in DeleteColumn executing query '{0}'."), exception.ToString());
                return false;
            }
        }

        public bool RenameColumn(string sourceTable, string currentColumnName, string newColumnName)
        {
            try
            {
                using (SQLiteConnection dbConnection = new SQLiteConnection(this.connectionString))
                {
                    dbConnection.Open();
                    // Some basic error checking to make sure we can do the operation
                    if (currentColumnName.Trim() == String.Empty || newColumnName.Trim() == String.Empty)
                    {
                        return false;  // One of the provided column names is an empty string
                    }
                    List<string> columnNames = GetColumnNamesAsList(dbConnection, sourceTable);
                    bool result1 = columnNames.Contains(currentColumnName);
                    bool result2 = columnNames.Contains(newColumnName);

                    if (columnNames.Contains(currentColumnName) == false) return false; // There is no column called currentColumnName in the source Table
                    if (columnNames.Contains(newColumnName) == true) return false; // There is already a column called newColumnName in the source Table

                    string newSchema = this.CloneSchemaButRenameColumn(dbConnection, sourceTable, currentColumnName, newColumnName);

                    // Create a new table 
                    string destTable = sourceTable + "NEW";
                    string sql = "CREATE TABLE " + destTable + " (" + newSchema + ")";
                    SQLiteCommand command = new SQLiteCommand(sql, dbConnection);
                    command.ExecuteNonQuery();

                    // Copy the old table's contents to the new table
                    this.CopyAllValuesBetweenTables(dbConnection, sourceTable, destTable, sourceTable, destTable);

                    // Now drop the source table and rename the destination table to that of the source table
                    this.DropTable(dbConnection, sourceTable);

                    // Rename the table
                    this.RenameTable(dbConnection, destTable, sourceTable);
                    return true;
                }
            }
            catch (Exception exception)
            {
                Debug.Assert(false, String.Format("Failure in RenameColumn executing query '{0}'."), exception.ToString());
                return false;
            }

        }
        #endregion

        #region Private Methods supporting Adding/Deleting/Renaming Columns
        /// <summary>
        /// Add a column to the end of the database table 
        /// This does NOT require the table to be cloned.
        /// Note: Some of the AddColumnToEndOfTable methods are currently not referenced, but may be handy in the future.
        /// </summary>
        /// <param name="dbConnection">the open and valid connection to the database</param> 
        /// <param name="tableName">the name of the  table</param> 
        /// <param name="name">the name of the new column</param> 
        /// <param name="type">the type of the new column</param> 
        private void AddColumnToEndOfTable(SQLiteConnection dbConnection, string tableName, string name, string type)
        {
            string columnDefinition = name + " " + type;
            AddColumnToEndOfTable(dbConnection, tableName, columnDefinition);
        }
        /// <summary>
        /// Add a column to the end of the database table. 
        /// This does NOT require the table to be cloned.
        /// </summary>
        /// <param name="dbConnection">the open and valid connection to the database</param> 
        /// <param name="tableName">the name of the  table</param> 
        /// <param name="name">the name of the new column</param> 
        /// <param name="type">the type of the new column</param> 
        /// <param name="other_options">space-separated options such as PRIMARY KEY AUTOINCREMENT, NULL or NOT NULL etc</param>
        private void AddColumnToEndOfTable(SQLiteConnection dbConnection, string tableName, string name, string type, string other_options)
        {
            string columnDefinition = name + " " + type;
            if (other_options != string.Empty)
            {
                columnDefinition += " " + other_options;
            }
            AddColumnToEndOfTable(dbConnection, tableName, columnDefinition);
        }

        private void AddColumnToEndOfTable(SQLiteConnection dbConnection, string tableName, string columnDefinition)
        {
            string sql = "ALTER TABLE " + tableName + " ADD COLUMN " + columnDefinition;
            SQLiteCommand command = new SQLiteCommand(sql, dbConnection);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Copy all the values from the source table into the destination table. Assumes that both tables are populated with identically-named columns
        /// </summary>
        /// <param name="dbConnection"></param>
        /// <param name="dataSourceTable">the source table whose values we will copy</param>
        /// <param name="dataDestinationTable">the destination table where values will be copied into</param>
        private void CopyAllValuesFromTable(SQLiteConnection dbConnection, string schemaFromTable, string dataSourceTable, string dataDestinationTable)
        {
            string commaSeparatedColumns = this.GetColumnNamesAsString(dbConnection, schemaFromTable);
            string sql = "INSERT INTO " + dataDestinationTable + " (" + commaSeparatedColumns + ") SELECT " + commaSeparatedColumns + " FROM " + dataSourceTable;

            SQLiteCommand command = new SQLiteCommand(sql, dbConnection);
            command = new SQLiteCommand(sql, dbConnection);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Copy all the values from the source table into the destination table. Assumes that both tables are populated with identically-named columns
        /// </summary>
        /// <param name="dbConnection"></param>
        /// <param name="dataSourceTable">the source table whose values we will copy</param>
        /// <param name="dataDestinationTable">the destination table where values will be copied into</param>
        private void CopyAllValuesBetweenTables(SQLiteConnection dbConnection, string schemaFromSourceTable, string schemaFromDestinationTable, string dataSourceTable, string dataDestinationTable)
        {
            string commaSeparatedColumnsSource = this.GetColumnNamesAsString(dbConnection, schemaFromSourceTable);
            string commaSeparatedColumnsDestination = this.GetColumnNamesAsString(dbConnection, schemaFromDestinationTable);
            string sql = "INSERT INTO " + dataDestinationTable + " (" + commaSeparatedColumnsDestination + ") SELECT " + commaSeparatedColumnsSource + " FROM " + dataSourceTable;

            SQLiteCommand command = new SQLiteCommand(sql, dbConnection);
            command = new SQLiteCommand(sql, dbConnection);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Drop the database table 'tableName' from the connected database.
        /// </summary>
        /// <param name="dbConnection">the open and valid connection to the database</param>
        /// <param name="tableName">the name of the table</param>
        private void DropTable(SQLiteConnection dbConnection, string tableName)
        {
            string sql = "DROP TABLE " + tableName;
            SQLiteCommand command = new SQLiteCommand(sql, dbConnection);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Return a list of all the column names in the  table named 'tableName' from the connected database.
        /// </summary>
        /// <param name="dbConnection">the open and valid connection to the database</param>
        /// <param name="tableName">the name of the table</param>
        /// <returns>a list of all the column names in the  table</returns>
        private List<string> GetColumnNamesAsList(SQLiteConnection dbConnection, string tableName)
        {
            SQLiteDataReader reader = this.GetSchema(dbConnection, tableName);
            List<string> columnNames = new List<string>();
            while (reader.Read())
            {
                columnNames.Add(reader[1].ToString());
            }
            return columnNames;
        }

        /// <summary>
        /// Return a comma-spearated string of all the column names in the  table named 'tableName' from the connected database.
        /// </summary>
        /// <param name="dbConnection">the open and valid connection to the database</param>
        /// <param name="tableName">the name of the table</param>
        /// <returns>a comma-spearated string of all the column names in the  table</returns>
        private string GetColumnNamesAsString(SQLiteConnection dbConnection, string tableName)
        {
            string commaSeparatedColumns = string.Empty; // Construct a comma-separated string representation of the columns
            foreach (string column in this.GetColumnNamesAsList(dbConnection, tableName))
            {
                commaSeparatedColumns += column + ", ";
            }
            return commaSeparatedColumns.TrimEnd(',', ' ');
        }

        /// <summary>
        /// Get the Schema for a simple database table 'tableName' from the connected database.
        /// For each column, it can retrieve schema settings including:
        ///     Name, Type, If its the Primary Key, Constraints including its Default Value (if any) and Not Null 
        /// However other constraints that may be set in the table schema are NOT returned, including:
        ///     UNIQUE, CHECK, FOREIGN KEYS, AUTOINCREMENT 
        /// If you use those, the schema may either ignore them or return odd values. So check it!
        /// Usage example: SQLiteDataReader reader = GetSchema (dbConnection, "tableName");
        /// To use the schema, do a while loop over reader.Read () to read a column at a time after every read
        //// access the column's attributes, where 
        //// -reader[0] is column number (e.g., 0)
        //// -reader[1] is  columnname (e.g., Employee)
        //// -reader[2] is  type (e.g., STRING)
        //// -reader[3] to [5] also returns values, but not yet sure what they stand for.. maybe 'Primary Key Autoincrement'?
        /// </summary>
        /// <param name="dbConnection">the open and valid connection to the database</param>
        /// <param name="tableName">the  name of the table</param>
        /// <returns>
        /// The schema as a SQLiteDataReader.To examine it, do a while loop over reader.Read () to read a column at a time after every read
        /// access the column's attributes, where 
        /// -reader[0] is column number (e.g., 0)
        /// -reader[1] is  columnname (e.g., Employee)
        /// -reader[2] is  type (e.g., STRING)
        /// -reader[3] to [5] also returns values, but not yet sure what they stand for.. maybe 'Primary Key Autoincrement'?
        /// </returns>
        private SQLiteDataReader GetSchema(SQLiteConnection dbConnection, string tableName)
        {
            string sql = "PRAGMA TABLE_INFO (" + tableName + ")"; // Syntax is: PRAGMA TABLE_INFO (tableName)
            SQLiteCommand command = new SQLiteCommand(sql, dbConnection);
            command = new SQLiteCommand(sql, dbConnection);
            return command.ExecuteReader();
        }

        /// <summary>
        /// Add a column definition into the provided schema at the given column location
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="columnNumber"></param>
        /// <param name="columnDefinition"></param>
        /// <returns></returns>
        private string CloneSchemaButWithAddedColumn(SQLiteConnection dbConnection, string tableName, int columnNumber, string columnDefinition)
        {
            string newSchema = string.Empty;
            int currentColumn = 0;
            bool columnAdded = false;
            SQLiteDataReader reader = this.GetSchema(dbConnection, tableName);
            while (reader.Read())
            {
                string existingColumnDefinition = string.Empty;

                // If we are at the spot where we should insert the new columm definition, do so.
                if (currentColumn == columnNumber)
                {
                    existingColumnDefinition += columnDefinition + ", ";
                    columnAdded = true;
                }

                // Add the existing column definition
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    switch (i)
                    {
                        case 0:  // cid (Column Index)
                            break;
                        case 1:  // name (Column Name)
                        case 2:  // type (Column type)
                            existingColumnDefinition += reader[i].ToString() + " ";
                            break;
                        case 3:  // notnull (Column has a NOT NULL constraint)
                            if (reader[i].ToString() != "0")
                            {
                                existingColumnDefinition += "NOT NULL ";
                            }
                            break;
                        case 4:  // dflt_value (Column has a default value)
                            if (reader[i].ToString() != string.Empty)
                            {
                                existingColumnDefinition += "DEFAULT " + reader[i].ToString() + " ";
                            }
                            break;
                        case 5:  // pk (Column is part of the primary key)
                            if (reader[i].ToString() != "0")
                            {
                                existingColumnDefinition += "PRIMARY KEY ";
                            }
                            break;
                    }
                }
                existingColumnDefinition = existingColumnDefinition.TrimEnd(' ');
                newSchema += existingColumnDefinition + ", ";
                currentColumn++;
            }
            // If we haven't added the column yet, its because the columnNumber provided 
            // is greater than the number of columns in the table.
            // So add it to the end.
            if (columnAdded == false)
            {
                newSchema += columnDefinition + ", ";
            }
            newSchema = newSchema.TrimEnd(',', ' '); // remove last comma
            return newSchema;
        }

        /// <summary>
        /// Create a schema cloned from tableName, except with the column definition for columnName deleted
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="columnNumber"></param>
        /// <param name="columnDefinition"></param>
        /// <returns></returns>
        private string CloneSchemaButDeleteNamedColumn(SQLiteConnection dbConnection, string tableName, string columnName)
        {
            string newSchema = string.Empty;
            int currentColumn = 0;
            SQLiteDataReader reader = this.GetSchema(dbConnection, tableName);
            while (reader.Read())
            {
                string existingColumnDefinition = string.Empty;
                if (reader[1].ToString() == columnName) continue; // skip the column if is is named columnName, which will delete it from the schema

                // Copy the existing column definition unless its the column named columnNam
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    switch (i)
                    {
                        case 0:  // cid (Column Index)
                            break;
                        case 1:  // name (Column Name)
                        case 2:  // type (Column type)
                            existingColumnDefinition += reader[i].ToString() + " ";
                            break;
                        case 3:  // notnull (Column has a NOT NULL constraint)
                            if (reader[i].ToString() != "0")
                            {
                                existingColumnDefinition += "NOT NULL ";
                            }
                            break;
                        case 4:  // dflt_value (Column has a default value)
                            if (reader[i].ToString() != string.Empty)
                            {
                                existingColumnDefinition += "DEFAULT " + reader[i].ToString() + " ";
                            }
                            break;
                        case 5:  // pk (Column is part of the primary key)
                            if (reader[i].ToString() != "0")
                            {
                                existingColumnDefinition += "PRIMARY KEY ";
                            }
                            break;
                    }
                }
                existingColumnDefinition = existingColumnDefinition.TrimEnd(' ');
                newSchema += existingColumnDefinition + ", ";
                currentColumn++;
            }
            newSchema = newSchema.TrimEnd(',', ' '); // remove last comma
            return newSchema;
        }


        /// <summary>
        /// Create a schema cloned from tableName, except with the column definition for columnName deleted
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="columnNumber"></param>
        /// <param name="columnDefinition"></param>
        /// <returns></returns>
        private string CloneSchemaButRenameColumn(SQLiteConnection dbConnection, string tableName, string existingColumnName, string newColumnName)
        {
            string newSchema = string.Empty;
            SQLiteDataReader reader = this.GetSchema(dbConnection, tableName);
            while (reader.Read())
            {
                string existingColumnDefinition = string.Empty;

                // Copy the existing column definition unless its the column named columnNam
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    switch (i)
                    {
                        case 0:  // cid (Column Index)
                            break;
                        case 1:  // name (Column Name)
                            // Rename the column if it is the one to be renamed
                            existingColumnDefinition += (reader[1].ToString() == existingColumnName) ? newColumnName : reader[1].ToString();
                            existingColumnDefinition += " ";
                            break;
                        case 2:  // type (Column type)
                            existingColumnDefinition += reader[i].ToString() + " ";
                            break;
                        case 3:  // notnull (Column has a NOT NULL constraint)
                            if (reader[i].ToString() != "0")
                            {
                                existingColumnDefinition += "NOT NULL ";
                            }
                            break;
                        case 4:  // dflt_value (Column has a default value)
                            if (reader[i].ToString() != string.Empty)
                            {
                                existingColumnDefinition += "DEFAULT " + reader[i].ToString() + " ";
                            }
                            break;
                        case 5:  // pk (Column is part of the primary key)
                            if (reader[i].ToString() != "0")
                            {
                                existingColumnDefinition += "PRIMARY KEY ";
                            }
                            break;
                    }
                }
                existingColumnDefinition = existingColumnDefinition.TrimEnd(' ');
                newSchema += existingColumnDefinition + ", ";
            }
            newSchema = newSchema.TrimEnd(',', ' '); // remove last comma
            return newSchema;
        }
        /// <summary>
        /// Rename the database table named 'tableName' to 'new_tableName'  
        /// </summary>
        /// <param name="dbConnection">the open and valid connection to the database</param>
        /// <param name="old_tableName">the current name of the existing table</param> 
        /// <param name="new_tableName">the new name of the table</param> 
        private void RenameTable(SQLiteConnection dbConnection, string tableName, string new_tableName)
        {
            string sql = "ALTER TABLE " + tableName + " RENAME TO " + new_tableName;
            SQLiteCommand command = new SQLiteCommand(sql, dbConnection);
            command.ExecuteNonQuery();
        }
        #endregion
    }
}