using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using Timelapse.Database;

namespace Timelapse.Database
{
    // A wrapper to make it easy to invoke some basic SQLite commands
    // It is NOT a generalized wrapper, as it only handles a few simple things.
    internal class SQLiteWrapper
    {
        #region Constants and Private Variables
        private const string DATASOURCE = "Data Source=";
        private const string CREATE_TABLE = "CREATE TABLE ";
        private const string INSERT_INTO = "INSERT INTO ";
        private const string VALUES = " VALUES ";
        private const string SELECT = " SELECT ";
        private const string UNION_ALL = " UNION ALL";
        private const string AS = " AS ";
        private const string DELETE_FROM = " DELETE FROM ";
        private const string WHERE = " WHERE ";
        private const string NAME_FROM_SQLITE_MASTER = " NAME FROM SQLITE_MASTER ";
        private const string TYPE_EQUAL_TABLE = " TYPE='table' ";
        private const string ORDER_BY = " ORDER BY ";
        private const string NAME = " NAME ";
        private const string UPDATE = " UPDATE ";
        private const string SET = " SET ";
        private const string WHEN = " WHEN ";
        private const string THEN = " THEN ";
        private const string BEGIN = " BEGIN ";
        private const string END = " END ";
        private const string EQUALS_CASE_ID = " = CASE Id";
        private const string WHERE_ID_IN = WHERE + "Id IN ";

        private const string NULL = "NULL";
        private const string NULL_AS = NULL + " " + AS;

        private const string Comma = ", ";
        private const string OpenParenthesis = " ( ";
        private const string CloseParenthesis = " ) ";
        private const string Semicolon = " ; ";

        // A connection string identifying the  database file. Takes the form:
        // "Data Source=filepath" 
        private string connectionString;
        #endregion

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
            this.connectionString = String.Format("{0}{1}", DATASOURCE, inputFile);
        }

        /// <summary>
        /// Constructor (Advanced): This version specifies advanced connection options.
        /// The Dictionary parameter contains key, value pairs, which is used to construct the connection string looking like
        /// key1=value1; key2=value2; ...
        /// </summary>
        /// <param name="connectionOpts">A dictionary containing all desired options and their values</param>
        public SQLiteWrapper(Dictionary<string, string> connectionOpts)
        {
            string str = String.Empty;
            foreach (KeyValuePair<string, string> row in connectionOpts)
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
        public void CreateTable(string table_name, Dictionary<string, string> column_definitions, out bool result, out string command_executed)
        {
            // The table creation syntax supported is:
            // CREATE TABLE table_name (
            //     column1name datatype,       e.g.,   Id INT PRIMARY KEY OT NULL,
            //     column2name datatype,               NAME TEXT NOT NULL,
            //     ...                                 ...
            //     columnNname datatype);              SALARY REAL);
            string query = String.Empty;
            command_executed = String.Empty;
            result = true;
            try
            {
                query = CREATE_TABLE + table_name + OpenParenthesis + Environment.NewLine;               // CREATE TABLE <tablename> (
                foreach (KeyValuePair<string, string> column in column_definitions)
                {
                    query += column.Key + " " + column.Value + Comma + Environment.NewLine;             // columnname datatype,
                }
                query = query.Remove(query.Length - Comma.Length - Environment.NewLine.Length);         //remove last comma / new line and replace with );
                query += CloseParenthesis + Semicolon;
                command_executed = query;
                this.ExecuteNonQuery(query, out result);
            }
            catch
            {
                result = false;
            }
        }

        // Assumes they have exactly the same creation string
        public void InsertDataTableIntoTable(string table_name, DataTable datatable, out bool result, out string command_executed)
        {
            List<string> queries = new List<string>();
            string query;
            string values;

            command_executed = String.Empty;
            result = true;
            for (int i = 0; i < datatable.Rows.Count; i++)
            {
                query = INSERT_INTO + table_name + " VALUES ";                                   // INSERT INTO table_name;
                values = String.Empty;
                for (int j = 0; j < datatable.Columns.Count; j++)
                {
                    if (j == 0)
                    {
                        values += String.Format(" {0}" + Comma, i + 1); // "NULL" + COMMA;
                    }
                    else if (j == 1 | j == 2)
                    {
                        values += String.Format(" {0}" + Comma, datatable.Rows[i][j]);         // "value1, value2, ... valueN"
                    }
                    else
                    {
                        string newvalue = (string)datatable.Rows[i][j];
                        newvalue = newvalue.Replace("'", "''");
                        values += String.Format(" {0}" + Comma, this.Quote(newvalue));         // "'value1', 'value2', ... 'valueN'"
                    }
                }
                values = values.Substring(0, values.Length - Comma.Length);        // Remove last comma in the sequence 
                query += String.Format("({0}); ", values);                          //      ('value1', 'value2', ... 'valueN');
                queries.Add(query);
                command_executed += query + Environment.NewLine;
            }
            this.ExecuteNonQueryWrappedInBeginEnd(queries, out result);
        }
        #endregion

        #region Insertion: Single Row
        /// <summary>
        ///     Insert a single row into the database. 
        ///     Warning: Very inefficient if there are a large number of consecutive inserts
        /// </summary>
        /// <param name="tableName">The table into which we insert the data.</param>
        /// <param name="data">A dictionary containing the column names and data for the insert.</param>
        public void Insert(string tableName, Dictionary<string, string> data, out bool result, out string command_executed)
        {
            // INSERT INTO table_name
            //      colname1, colname12, ... colnameN VALUES
            //      ('value1', 'value2', ... 'valueN');
            string columns = String.Empty;
            string values = String.Empty;
            string query = String.Empty;
            command_executed = String.Empty;

            result = true;
            foreach (KeyValuePair<string, string> val in data)
            {
                columns += String.Format(" {0}" + Comma, val.Key.ToString()); // transform dictionary entries into a string "col1, col2, ... coln"
                values += String.Format(" {0}" + Comma, this.Quote(val.Value));         // transform dictionary entries into a string "'value1', 'value2', ... 'valueN'"
            }
            columns = columns.Substring(0, columns.Length - Comma.Length);     // Remove last comma in the sequence to produce (col1, col2, ... coln)  
            values = values.Substring(0, values.Length - Comma.Length);        // Remove last comma in the sequence 

            // Construct the query. The newlines are to format it for pretty printing
            query = INSERT_INTO + tableName;                                   // INSERT INTO table_name
            query += Environment.NewLine;
            query += String.Format("({0}) ", columns);                          //      (col1, col2, ... coln)
            query += Environment.NewLine;
            query += VALUES;                                                    // VALUES
            query += Environment.NewLine;
            query += String.Format("({0}); ", values);                          //      ('value1', 'value2', ... 'valueN');

            command_executed = query;
            this.ExecuteNonQuery(query, out result);
        }

        public void InsertMultiplesBeginEnd(string table_name, List<Dictionary<string, string>> insertion_statements, out bool result, out string command_executed)
        {
            // Construct each individual query in the form 
            // INSERT INTO table_name
            //      colname1, colname12, ... colnameN VALUES
            //      ('value1', 'value2', ... 'valueN');
            string columns;
            string values;
            List<string> queries = new List<string>();
            string query;
            command_executed = String.Empty;

            result = true;
            foreach (Dictionary<string, string> data in insertion_statements)
            {
                columns = String.Empty;
                values = String.Empty;
                query = String.Empty;
                foreach (KeyValuePair<string, string> val in data)
                {
                    columns += String.Format(" {0}" + Comma, val.Key.ToString());      // transform dictionary entries into a string "col1, col2, ... coln"
                    values += String.Format(" {0}" + Comma, this.Quote(val.Value));         // transform dictionary entries into a string "'value1', 'value2', ... 'valueN'"
                }
                if (columns.Length > 0)
                {
                    columns = columns.Substring(0, columns.Length - Comma.Length);     // Remove last comma in the sequence to produce (col1, col2, ... coln)  
                }
                if (values.Length > 0)
                {
                    values = values.Substring(0, values.Length - Comma.Length);        // Remove last comma in the sequence 
                }

                // Construct the query. The newlines are to format it for pretty printing
                query = INSERT_INTO + table_name;                                   // INSERT INTO table_name
                query += Environment.NewLine;
                query += String.Format("({0}) ", columns);                          //      (col1, col2, ... coln)
                query += Environment.NewLine;
                query += VALUES;                                                    // VALUES
                query += Environment.NewLine;
                query += String.Format("({0}); ", values);                          //      ('value1', 'value2', ... 'valueN');
                queries.Add(query);
                command_executed += query + Environment.NewLine;                    // And add it to our list of queries
            }
            // Now try to invoke the batch queries
            this.ExecuteNonQueryWrappedInBeginEnd(queries, out result);
        }
        #endregion

        #region Insertion: Multiple Rows
        /// <summary>
        /// Efficiently insert 1 to 500 rows into the database in one operation. 
        /// TODO MODIFY IT SO THAT IT DOES IT IN MULTIPLES OF 500 AS NEEDED
        /// </summary>
        public void InsertMultiples(string tableName, List<string> columnsList, List<List<string>> valuesList, out bool result, out string command_executed)
        {
            // The multiple insertion syntax supported is:
            // INSERT INTO 'table_name' 
            //     ('column1', 'column2', ... 'columnN') 
            // VALUES
            //     ('value11',  value 12, ... value1N),
            //     ('value21',  value 22, ... value2N),
            //     ...  
            //     ('valueM1',  value M2, ... valueMN);

            int maxRows = 500;
            int minRows = 2;
            command_executed = String.Empty;
            result = true;

            // Check limits on how many rows (unions) we can update
            // Just return false if we are trying to do too many or too few  (i.e, between 2 - 100)
            if (valuesList.Count > maxRows || valuesList.Count < minRows)
            {
                result = false;
                return;
            }

            // The first row
            string query = INSERT_INTO + this.Quote(tableName);    // INSERT INTO 'table' 

            query += Environment.NewLine + OpenParenthesis;   // ('column1', 'column2', ... 'columnN') 
            foreach (string column_name in columnsList)
            {
                query += this.Quote(column_name) + Comma;
            }
            query = query.Remove(query.Length - Comma.Length);
            query += CloseParenthesis;

            query += Environment.NewLine + VALUES;              // VALUES

            // For each row to insert, provide the corresponding values
            // ('value11',  value 12, ... value1N),
            foreach (List<string> row_values in valuesList)
            {
                query += Environment.NewLine;
                query += OpenParenthesis;

                foreach (string value in row_values)
                {
                    query += this.Quote(value) + Comma;
                }
                // Remove the last comma
                query = query.Remove(query.Length - Comma.Length);
                query += CloseParenthesis + Comma;
            }
            query = query.Remove(query.Length - Comma.Length);  // ('valueM1',  value M2, ... valueMN);
            query += ";";

            command_executed = query;
            try
            {
                this.ExecuteNonQuery(query, out result);
            }
            catch
            {
                result = false;
            }
        }
        #endregion

        #region Select Invocations
        /// <summary>
        ///  Run a generic Select query against the Database, with results returned as rows in a DataTable.
        /// </summary>
        /// <param name="query">The SQL to run</param>
        /// <returns>A DataTable containing the result set.</returns>
        public bool TryGetDataTableFromSelect(string query, out DataTable dataTable)
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
                            dataTable = new DataTable();
                            dataTable.Load(reader);
                        }
                    }
                }
                return true;
            }
            catch
            {
                dataTable = null;
                return false;
            }
        }

        /// <summary>
        /// Run a generic Select query against the Database, with a single result returned as an object that must be cast. 
        /// </summary>
        /// <param name="query">The SQL to run</param>
        /// <returns>A value containing the single result.</returns>
        public Object GetObjectFromSelect(string query, out bool result, out string command_executed)
        {
            result = true;
            command_executed = String.Empty;
            try
            {
                // Open the connection
                SQLiteConnection cnn = new SQLiteConnection(this.connectionString);
                cnn.Open();
                SQLiteCommand mycommand = new SQLiteCommand(cnn);
                command_executed = query;
                mycommand.CommandText = query;

                object value = mycommand.ExecuteScalar();
                cnn.Close();
                return value;
            }
            catch
            {
                result = false;
                return null;
            }
        }

        /// <summary>
        /// Run a generic Select query against the Database, with a single result returned as an integer 
        /// </summary>
        public int GetIntFromSelect(string query, out bool result, out string command_executed)
        {
            return Convert.ToInt32(this.GetObjectFromSelect(query, out result, out command_executed));
        }

        /// <summary>
        /// Run a generic Select query against the Database, with a single result returned as a string 
        /// </summary>
        public string GetStringFromSelect(string query, out bool result, out string command_executed)
        {
            return (string)this.GetObjectFromSelect(query, out result, out command_executed);
        }
        #endregion

        #region Query Execution
        /// <summary>
        ///  Allows the programmer to interact with the database for purposes other than a query.
        /// </summary>
        /// <param name="query">The SQL to be run.</param>
        /// <returns>An Integer containing the number of rows updated.</returns>
        public int ExecuteNonQuery(string query, out bool result)
        {
            result = true;
            try
            {
                SQLiteConnection cnn = new SQLiteConnection(this.connectionString);
                cnn.Open();
                SQLiteCommand mycommand = new SQLiteCommand(cnn);
                mycommand.CommandText = query;
                int rowsUpdated = mycommand.ExecuteNonQuery();
                cnn.Close();
                return rowsUpdated;
            }
            catch
            {
                result = false;
                return 0;
            }
        }

        /// <summary>
        /// Given a list of complete queries, wrap up to 500 of them in a BEGIN/END statement so they are all executed in one go for efficiency
        /// Continue for the next up to 500, and so on.
        /// </summary>
        public int ExecuteNonQueryWrappedInBeginEnd(List<string> queries, out bool result)
        {
            // BEGIN
            //      query1
            //      query2
            //      ...
            //      queryn
            // END
            result = true;
            int rowsUpdated = 0;
            int query_count = 0;
            int max_count = 500;
            try
            {
                SQLiteConnection cnn = new SQLiteConnection(this.connectionString);
                cnn.Open();
                SQLiteCommand mycommand;

                // Invoke each query in the queries list
                foreach (string query in queries)
                {
                    query_count++;
                    // Insert a BEGIN if we are at the beginning of the count
                    if (query_count == 1)
                    {
                        mycommand = new SQLiteCommand(cnn);
                        mycommand.CommandText = BEGIN;
                        rowsUpdated += mycommand.ExecuteNonQuery();
                        // Debug.Print(mycommand.CommandText);
                    }

                    mycommand = new SQLiteCommand(cnn);
                    mycommand.CommandText = query;
                    rowsUpdated += mycommand.ExecuteNonQuery();
                    // Debug.Print(query_count.ToString());

                    // END
                    if (query_count == max_count)
                    {
                        mycommand = new SQLiteCommand(cnn);
                        mycommand.CommandText = END;
                        rowsUpdated += mycommand.ExecuteNonQuery();
                        query_count = 0;
                        // Debug.Print(mycommand.CommandText);
                    }
                }
                // END
                if (query_count != 0)
                {
                    mycommand = new SQLiteCommand(cnn);
                    mycommand.CommandText = END;
                    rowsUpdated += mycommand.ExecuteNonQuery();
                    // Debug.Print(mycommand.CommandText);
                }
                cnn.Close();
                result = true;
                return rowsUpdated;
            }
            catch
            {
                result = false;
                return 0;
            }
        }
        #endregion

        #region Updates
        // Trims all the white space from the data held in the list of column_names int table_name
        // Note: this is needed as earlier versions didn't trim the white space from the data. This allows us to trim it in the database after the fact.
        public void UpdateColumnTrimWhiteSpace(string table_name, List<string> column_names, out bool result, out string command_executed)
        {
            command_executed = String.Empty;
            result = true;
            foreach (string column_name in column_names)
            {
                command_executed = "Update " + table_name + " SET " + column_name + " = TRIM (" + column_name + ")"; // Form: UPDATE tablename SET columname = TRIM(columnname)
                this.ExecuteNonQuery(command_executed, out result);  // TODO MAKE THIS ALL IN ONE OPERATION
            }
        }

        public void UpdateWhereBeginEnd(string table_name, Dictionary<Dictionary<string, Object>, string> update_query_list, out bool result, out string command_executed)
        {
            string query = String.Empty;
            command_executed = String.Empty;
            result = true;
            List<string> queries = new List<string>();
            foreach (KeyValuePair<Dictionary<string, Object>, string> update_query in update_query_list)
            {
                query = this.UpdateCreateSingleUpdateQuery(table_name, update_query.Key, update_query.Value);
                if (query.Equals(String.Empty))
                {
                    continue; // skip non-queries
                }
                queries.Add(query);
                command_executed += query; // The string of queries
            }
            this.ExecuteNonQueryWrappedInBeginEnd(queries, out result);
        }

        // A version of the above that uses a different data structure to the same effect
        public void UpdateWhereBeginEnd(string table_name, List<ColumnTupleListWhere> update_query_list, out bool result, out string command_executed)
        {
            string query = String.Empty;
            command_executed = String.Empty;
            result = true;
            List<string> queries = new List<string>();

            foreach (ColumnTupleListWhere ctlw in update_query_list)
            {
                query = this.UpdateCreateSingleUpdateQuery(table_name, ctlw.ListPair, ctlw.Where);
                if (query.Equals(String.Empty))
                {
                    continue; // skip non-queries
                }
                queries.Add(query);
                command_executed += query; // The string of queries
            }
            this.ExecuteNonQueryWrappedInBeginEnd(queries, out result);
        }

        /// <summary>
        /// Update specific rows in the DB as specified in the where clause.
        /// </summary>
        /// <param name="tableName">The table to update.</param>
        /// <param name="columnname_value_list">A dictionary containing Column names and their new values.</param>
        /// <param name="where">The where clause for the update statement.</param>
        public void UpdateWhere(string tableName, Dictionary<string, Object> columnname_value_list, string where, out bool result, out string command_executed)
        {
            // UPDATE table_name SET 
            // colname1 = value1, 
            // colname2 = value2,
            // ...
            // colnameN = valueN
            // WHERE
            // <condition> e.g., ID=1;
            string query = String.Empty;
            result = true;
            command_executed = String.Empty;
            query = this.UpdateCreateSingleUpdateQuery(tableName, columnname_value_list, where);
            command_executed = query;
            try
            {
                this.ExecuteNonQuery(query, out result);
            }
            catch
            {
                result = false;
            }
        }

        // Return a single update query as a string
        private string UpdateCreateSingleUpdateQuery(string table_name, Dictionary<string, Object> columnname_value_list, string where)
        {
            // UPDATE table_name SET 
            // colname1 = value1, 
            // colname2 = value2,
            // ...
            // colnameN = valueN
            // WHERE
            // <condition> e.g., ID=1;
            string cells_to_update = String.Empty;

            if (columnname_value_list.Count < 0)
            {
                return String.Empty;     // No data, so nothing to update. This isn't really an error, so...
            }

            // column_name = 'value'
            foreach (KeyValuePair<string, Object> val in columnname_value_list)
            {
                // we have to cater to different formats for integers, NULLS and strings...
                if (this.IsNumber(val.Value))
                {
                    cells_to_update += String.Format(" {0} = {1}{2}", val.Key, val.Value.ToString(), Comma);
                }
                else if (val.Value == null)
                {
                    cells_to_update += String.Format(" {0} = NULL{1}", val.Key.ToString(), Comma);
                }
                else if (val.Value is string)
                {
                    cells_to_update += String.Format(" {0} = {1}{2}", val.Key.ToString(), this.Quote(val.Value.ToString()), Comma);
                }
                else
                {
                    // This shouldn't happen, but just in case...
                    return String.Empty;
                }
            }
            cells_to_update = cells_to_update.Substring(0, cells_to_update.Length - Comma.Length); // Remove the last comma

            string query = UPDATE + table_name + SET;
            query += cells_to_update;
            query += WHERE;
            query += where;
            query += Semicolon;
            return query;
        }
        // A version of the above that uses a different data structure. Returns a single update query as a string
        private string UpdateCreateSingleUpdateQuery(string table_name, List<ColumnTuple> columnname_value_list, string where)
        {
            // UPDATE table_name SET 
            // colname1 = value1, 
            // colname2 = value2,
            // ...
            // colnameN = valueN
            // WHERE
            // <condition> e.g., ID=1;
            string cells_to_update = String.Empty;

            if (columnname_value_list.Count < 0)
            {
                return String.Empty;     // No data, so nothing to update. This isn't really an error, so...
            }

            // column_name = 'value'
            foreach (ColumnTuple val in columnname_value_list)
            {
                // we have to cater to different formats for integers, NULLS and strings...
                if (this.IsNumber(val.ColumnValue))
                {
                    cells_to_update += String.Format(" {0} = {1}{2}", val.ColumnName, val.ColumnValue.ToString(), Comma);
                }
                else if (val.ColumnValue == null)
                {
                    cells_to_update += String.Format(" {0} = NULL{1}", val.ColumnName.ToString(), Comma);
                }
                else if (val.ColumnValue is string)
                {
                    cells_to_update += String.Format(" {0} = {1}{2}", val.ColumnName.ToString(), this.Quote(val.ColumnValue.ToString()), Comma);
                }
                else
                {
                    // This shouldn't happen, but just in case...
                    return String.Empty;
                }
            }
            cells_to_update = cells_to_update.Substring(0, cells_to_update.Length - Comma.Length); // Remove the last comma

            string query = UPDATE + table_name + SET;
            query += cells_to_update;
            query += WHERE;
            query += where;
            query += Semicolon;
            return query;
        }

        public void UpdateMultipleRows(string table_name, List<Tuple<string, int, string>> tuple_list, out bool result, out string command_executed)
        {
            // http://www.karlrixon.co.uk/writing/update-multiple-rows-with-different-values-and-a-single-sql-query/
            /*UPDATE table_name
            //    SET 
            //      column_name1 = CASE id
            //          WHEN 1 THEN 'newval11'
            //          WHEN 2 THEN 'newval12'
            //          WHEN 3 THEN 'newval13'
            //      END,
            //      column_name2 = CASE id
            //          WHEN 1 THEN 'newval21'
            //          WHEN 2 THEN 'newval22'
            //          WHEN 3 THEN 'newval23'
            //      END
            //WHERE id IN (1,2,3) */
            result = true;
            command_executed = String.Empty;

            tuple_list.Sort(Comparer<Tuple<string, int, string>>.Default);

            string last_column = String.Empty;
            string current_column;
            List<int> id_list = new List<int>();

            bool first_time = true;
            string query = UPDATE + table_name;             // UPDATE table_name
            query += SET + Environment.NewLine;                                   // SET
            foreach (Tuple<string, int, string> tuple in tuple_list)
            {
                current_column = tuple.Item1;

                if (!current_column.Equals(last_column))
                {
                    // Start a new column set
                    if (!first_time)
                    {
                        query += END + Comma;
                    }
                    else
                    {
                        first_time = false;
                    }
                    query += Environment.NewLine + " " + current_column + EQUALS_CASE_ID;                         // <column_name> EQUALS CASE Id
                }
                query += Environment.NewLine + WHEN + tuple.Item2.ToString() + THEN + this.Quote(tuple.Item3);    // WHEN <ID> THEN <new value>
                if (!id_list.Contains(tuple.Item2))
                {
                    id_list.Add(tuple.Item2);   // A running list of the IDs seen so far
                }
                last_column = current_column;
            }
            query = query.Remove(query.Length - Comma.Length);
            query += END;
            query += WHERE_ID_IN + OpenParenthesis;
            foreach (int i in id_list)
            {
                query += i.ToString();
                query += Comma;
            }
            query = query.Remove(query.Length - Comma.Length);
            query += CloseParenthesis + Semicolon;

            // MessageBox.Show (query);
            query = String.Empty;
            query = UPDATE + table_name;
            query += SET;
            query += "Col2" + " = CASE id ";
            query += WHEN + "1 " + THEN + "'new21'";
            query += WHEN + "2 " + THEN + "'new22'";
            query += WHEN + "3 " + THEN + "'new23'";
            query += END + Comma;

            query += "Col3" + " = CASE id ";
            query += WHEN + "2 " + THEN + "'new32'";
            query += WHEN + "3 " + THEN + "'new33'";
            query += WHEN + "4 " + THEN + "'new34'";
            query += END;
            query += WHERE + "id in (1,2,3,4)";  // ADDED FOR EFFICIENCY AS IT REDUCES THE NUMBER OF TESTS, BUT CAN BE LEFT OUT

            ///////////
            // query = String.Empty;
            // query += UPDATE + table_name + SET + " Col2 = 'new21'  WHERE Id = 2; ";
            // query += UPDATE + table_name + SET + " Col2 = 'new22'  WHERE Id = 2; ";
            // query += UPDATE + table_name + SET + " Col4 = 'new24'  WHERE Id = 4; ";
            // query += UPDATE + table_name + SET + " Col5 = 'new25'  WHERE Id = 5; ";
            // query += UNION_ALL + SET + " Col2 = 'new21'  WHERE Id = 1 ";
            // query += UNION_ALL + SET + " Col3 = 'new33'  WHERE Id = 3;";
            command_executed = query;
            // MessageBox.Show(command_executed);
            try
            {
                // this.ExecuteBeginNonQueryEnd (query, out result);
            }
            catch
            {
                result = false;
            }
        }

        #endregion

        #region Counts
        /// <summary>
        ///  Retrieve a count of items from the DB. Select statement must be of the form "Select Count(*) FROM "
        /// </summary>
        /// <param name="query">The query to run.</param>
        /// <returns>The number of items selected.</returns>
        public int GetCountFromSelect(string query, out bool result, out string command_executed)
        {
            return this.GetIntFromSelect(query, out result, out command_executed);
        }
        #endregion

        #region Columns: Checking and Adding 
        // This method will check if a column exists in a table
        public bool IsColumnInTable(string tableName, string columnName)
        {
            string query = "SELECT " + columnName + " from " + tableName;
            try
            {
                DataTable dummy;
                return this.TryGetDataTableFromSelect(query, out dummy);
            }
            catch
            {
                return false;
            }
        }

        // This method will create a column in a table, where it is added to its end
        public bool CreateColumnInTable(string tableName, string columnName)
        {
            bool result = false;
            string query = "ALTER TABLE " + tableName + " ADD COLUMN " + columnName + " TEXT";
            try
            {
                this.ExecuteNonQuery(query, out result);
            }
            catch
            {
                return false;
            }
            return result;
        }
        #endregion
        #region Deleting Rows 
        /// <summary>elete specific rows from the DB where...</summary>
        /// <param name="tableName">The table from which to delete.</param>
        /// <param name="where">The where clause for the delete.</param>
        public void DeleteFromTable(string tableName, string where, out bool result, out string command_executed)
        {
            // DELETE FROM table_name WHERE where
            result = true;
            command_executed = String.Empty;
            string query = DELETE_FROM + tableName;        // DELETE FROM table_name
            if (!where.Trim().Equals(String.Empty))
            {
                // Add the WHERE clause only when where is not empty
                query += WHERE;                                 // WHERE
                query += where;                                 // where
            }
            try
            {
                this.ExecuteNonQuery(query, out result);
            }
            catch
            {
                result = false;
            }
        }

        /// <summary>Delete all rows from a specific table.</summary>
        public void DeleteAllFromTable(string table_name, out bool result, out string command_executed)
        {
            this.DeleteFromTable(table_name, String.Empty, out result, out command_executed);
        }

        /// <summary>Delete all rows from all tables in the database.</summary>
        public void DeleteAllFromAllTables(out bool result, out string command_executed)
        {
            try
            {
                // SELECT NAME FROM SQLITE_MASTER WHERE TYPE='table' ORDER BY NAME;
                string query = SELECT;
                query += NAME_FROM_SQLITE_MASTER;
                query += WHERE;
                query += TYPE_EQUAL_TABLE;
                query += ORDER_BY + NAME;
                DataTable tables;
                result = this.TryGetDataTableFromSelect(query, out tables);
                command_executed = query;

                foreach (DataRow table in tables.Rows)
                {
                    this.DeleteAllFromTable(table["NAME"].ToString(), out result, out command_executed);
                }
            }
            catch
            {
                result = false;
                command_executed = null;
            }
        }
        #endregion

        #region Utilities
        private string Quote(string s)
        {
            const string QUOTE = "'";
            return QUOTE + s + QUOTE;
        }

        public bool IsNumber(object value)
        {
            return value is sbyte
                    || value is byte
                    || value is short
                    || value is ushort
                    || value is int
                    || value is uint
                    || value is long
                    || value is ulong
                    || value is float
                    || value is double
                    || value is decimal;
        }
        #endregion
    }
}