using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;

namespace Timelapse.Database
{
    // A wrapper to make it easy to invoke some basic SQLite commands
    // It is NOT a generalized wrapper, as it only handles a few simple things.
    internal class SQLiteWrapper
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
                        values += String.Format(" {0}" + Constants.Sql.Comma, this.Quote(newvalue));         // "'value1', 'value2', ... 'valueN'"
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
                    values += String.Format(" {0}" + Constants.Sql.Comma, this.Quote(column.Value));         // transform dictionary entries into a string "'value1', 'value2', ... 'valueN'"
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
                Debug.Assert(false, "ExecuteReader() failed.", exception.ToString());
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
                Debug.Assert(false, "ExecuteScalar() failed.", exception.ToString());
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
                Debug.Assert(false, "ExecuteNonQuery() failed.", exception.ToString());
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
            try
            {
                using (SQLiteConnection connection = new SQLiteConnection(this.connectionString))
                {
                    connection.Open();
                    SQLiteCommand command;

                    // Invoke each query in the queries list
                    int rowsUpdated = 0;
                    int statementsInQuery = 0;
                    foreach (string statement in statements)
                    {
                        statementsInQuery++;
                        // Insert a BEGIN if we are at the beginning of the count
                        if (statementsInQuery == 1)
                        {
                            using (command = new SQLiteCommand(connection))
                            {
                                command.CommandText = Constants.Sql.Begin;
                                rowsUpdated += command.ExecuteNonQuery();
                                // Debug.Print(mycommand.CommandText);
                            }
                        }

                        using (command = new SQLiteCommand(connection))
                        {
                            command.CommandText = statement;
                            rowsUpdated += command.ExecuteNonQuery();
                            // Debug.Print(query_count.ToString());
                        }

                        // END
                        if (statementsInQuery >= MaxStatementCount)
                        {
                            using (command = new SQLiteCommand(connection))
                            {
                                command.CommandText = Constants.Sql.End;
                                rowsUpdated += command.ExecuteNonQuery();
                                statementsInQuery = 0;
                                // Debug.Print(mycommand.CommandText);
                            }
                        }
                    }
                    // END
                    if (statementsInQuery != 0)
                    {
                        using (command = new SQLiteCommand(connection))
                        {
                            command.CommandText = Constants.Sql.End;
                            rowsUpdated += command.ExecuteNonQuery();
                            // Debug.Print(mycommand.CommandText);
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.Assert(false, "ExecuteNonQuery() failed.", exception.ToString());
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
                    query += String.Format(" {0} = {1}{2}", column.Name, this.Quote(column.Value), Constants.Sql.Comma);
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
        /// <summary>delete specific rows from the DB where...</summary>
        /// <param name="tableName">The table from which to delete.</param>
        /// <param name="where">The where clause for the delete.</param>
        public void Delete(string tableName, string where)
        {
            // DELETE FROM table_name WHERE where
            string query = Constants.Sql.DeleteFrom + tableName;        // DELETE FROM table_name
            if (!where.Trim().Equals(String.Empty))
            {
                // Add the WHERE clause only when where is not empty
                query += Constants.Sql.Where;                   // WHERE
                query += where;                                 // where
            }
            this.ExecuteNonQuery(query);
        }
        #endregion

        #region Utilities
        private string Quote(string s)
        {
            return "'" + s + "'";
        }
        #endregion
    }
}