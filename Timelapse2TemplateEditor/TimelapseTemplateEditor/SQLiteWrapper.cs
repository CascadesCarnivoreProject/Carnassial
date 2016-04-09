using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Windows;
namespace TimelapseTemplateEditor
{
    // A wrapper to make it easy to invoke some basic SQLite commands
    // It is NOT a generalized wrapper, as it only handles a few simple things.
    class SQLiteWrapper
    {
        #region Constants and Private Variables
        const string DATASOURCE = "Data Source=";
        const string CREATE_TABLE = "CREATE TABLE ";
        const string INSERT_INTO = "INSERT INTO ";
        const string VALUES = " VALUES ";
        const string SELECT = " SELECT ";
        const string UNION_ALL = " UNION ALL";
        const string AS = " AS ";
        const string DELETE_FROM = " DELETE FROM ";
        const string WHERE = " WHERE ";
        const string NAME_FROM_SQLITE_MASTER = " NAME FROM SQLITE_MASTER ";
        const string TYPE_EQUAL_TABLE = " TYPE='table' ";
        const string ORDER_BY = " ORDER BY ";
        const string NAME = " NAME ";
        const string UPDATE = " UPDATE ";
        const string SET = " SET ";
        const string WHEN = " WHEN ";
        const string THEN = " THEN ";
        const string BEGIN = " BEGIN ";
        const string END = " END ";
        const string EQUALS_CASE_ID = " = CASE Id";
        const string WHERE_ID_IN = WHERE + "Id IN ";

        const string NULLED = "NULL";
        const string NULLEDAS = NULLED + " " + AS;
  
        const string COMMA = ", ";
        const string BRACKET_OPENING = " ( ";
        const string BRACKET_CLOSING = " ) ";
        const string SEMICOLON = " ; ";

        // A connection string identifying the  database file. Takes the form:
        // "Data Source=filepath" 
        String dbConnection; 

        bool state_inbegin = false;
        SQLiteConnection begin_cnn;

        #endregion

        #region Constructors
        /// <summary>
        ///     Constructor: Create a database file if it does not exist, and then create a connection string to that file
        ///     If the DB file does not exist, It will be created
        /// </summary>
        /// <param name="inputFile">The File containing the DB</param>
        public SQLiteWrapper(String inputFile)
        {
            if (!System.IO.File.Exists(inputFile)) SQLiteConnection.CreateFile(inputFile);
            dbConnection = String.Format("{0}{1}", DATASOURCE, inputFile);
        }

        /// <summary>
        ///  Constructor (Advanced): This version specifies advanced connection options.
        ///  The Dictionary parameter contains key, value pairs, which is used to constuct the connection string looking like
        ///  key1=value1; key2=value2; ...
        /// </summary>
        /// <param name="connectionOpts">A dictionary containing all desired options and their values</param>
        public SQLiteWrapper(Dictionary<String, String> connectionOpts)
        {
            String str = "";
            foreach (KeyValuePair<String, String> row in connectionOpts)
            {
                str += String.Format("{0}={1}; ", row.Key, row.Value);
            }
            str = str.Trim().Substring(0, str.Length - 1);
            dbConnection = str;
        }
        #endregion

        #region Table Creation 
        /// <summary>
        /// A simplified table creation routine. It expects the column definitions to be supplied
        /// as a column_name, datatype key value pair. 
        /// </summary>
        /// <param name="table_name"></param>
        /// <param name="column_definitions"></param>
        /// <param name="result"></param>
        /// <param name="command_executed">The query that was run</param>
        public void CreateTable(String table_name, Dictionary<string, string> column_definitions, out bool result, out string command_executed)
        {
            // The table creation syntax supported is:
            // CREATE TABLE table_name (
            //     column1name datatype,       e.g.,   Id INT PRIMARY KEY OT NULL,
            //     column2name datatype,               NAME TEXT NOT NULL,
            //     ...                                 ...
            //     columnNname datatype);              SALARY REAL);
            string query = "";
            command_executed = "";
            result = true;
            try
            {
                query = CREATE_TABLE + table_name + BRACKET_OPENING + Environment.NewLine;               // CREATE TABLE <tablename> (
                foreach (KeyValuePair<String, String> column in column_definitions)
                {
                    query += column.Key + " " + column.Value + COMMA + Environment.NewLine;             // columnname datatype,
                }
                query = query.Remove(query.Length - COMMA.Length - Environment.NewLine.Length);         //remove last comma / new line and replace with );
                query += BRACKET_CLOSING + SEMICOLON;
                command_executed = query;
                this.ExecuteNonQuery(query, out result);
            }
            catch
            {
                result = false;
            }
        }
        #endregion

        #region Insertion: Single Row
        /// <summary>
        ///     Insert a single row into the database. 
        ///     Warning: Very inefficient if there are a large number of consecutive inserts
        /// </summary>
        /// <param name="tableName">The table into which we insert the data.</param>
        /// <param name="data">A dictionary containing the column names and data for the insert.</param>
        /// <returns>A boolean true or false to signify success or failure.</returns>
        public void Insert(String table_name, Dictionary<String, String> data, out bool result, out string command_executed)
        {
            MyTrace.MethodName("SQL");
            // INSERT INTO table_name
            //      colname1, colname12, ... colnameN VALUES
            //      ('value1', 'value2', ... 'valueN');
            string columns = "";
            string values = "";
            string query = "";
            command_executed = "";

            result = true;
            foreach (KeyValuePair<String, String> val in data)
            {
                columns += String.Format(" {0}" + COMMA, val.Key.ToString()); // transform dictionary entries into a string "col1, col2, ... coln"
                values += String.Format(" {0}" + COMMA, quote (val.Value));         // transform dictionary entries into a string "'value1', 'value2', ... 'valueN'"
            }
            columns = columns.Substring(0, columns.Length - COMMA.Length);     // Remove last comma in the sequence to produce (col1, col2, ... coln)  
            values = values.Substring(0, values.Length - COMMA.Length);        // Remove last comma in the sequence 

            // Construct the query. The newlines are to format it for pretty printing
            query = INSERT_INTO + table_name;                                   // INSERT INTO table_name
            query += Environment.NewLine;
            query += String.Format("({0}) ", columns);                          //      (col1, col2, ... coln)
            query += Environment.NewLine;
            query += VALUES;                                                    // VALUES
            query += Environment.NewLine;                                           
            query += String.Format("({0}); ", values);                          //      ('value1', 'value2', ... 'valueN');

            command_executed = query;                                               
            try
            {
                this.ExecuteNonQuery(query, out result);
            }
            catch (Exception fail)
            {
                System.Windows.MessageBox.Show(fail.Message);
                result = false;
            }
        }

        public void InsertMultiplesBeginEnd(String table_name, List <Dictionary<String, String>> insertion_statements, out bool result, out string command_executed)
        {
            MyTrace.MethodName("SQL");
            // Construct each individual query in the form 
            // INSERT INTO table_name
            //      colname1, colname12, ... colnameN VALUES
            //      ('value1', 'value2', ... 'valueN');
            string columns;
            string values;
            List <string> queries = new List<string> ();
            string query;
            command_executed = "";


            result = true;
            foreach (Dictionary<String, String> data in insertion_statements)
            {
                columns = "";
                values = "";
                query = "";
                foreach (KeyValuePair<String, String> val in data)
                {
                    columns += String.Format(" {0}" + COMMA, val.Key.ToString()); // transform dictionary entries into a string "col1, col2, ... coln"
                    values += String.Format(" {0}" + COMMA, quote(val.Value));         // transform dictionary entries into a string "'value1', 'value2', ... 'valueN'"
                }
                columns = columns.Substring(0, columns.Length - COMMA.Length);     // Remove last comma in the sequence to produce (col1, col2, ... coln)  
                values = values.Substring(0, values.Length - COMMA.Length);        // Remove last comma in the sequence 

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
            ExecuteNonQueryWrappedInBeginEnd (queries, out result);
        }
        public void InsertMultiplesBeginEnd(String table_name, List<Dictionary<String, Object>> insertion_statements, out bool result, out string command_executed)
        {
            MyTrace.MethodName("SQL");
            // Construct each individual query in the form 
            // INSERT INTO table_name
            //      colname1, colname12, ... colnameN VALUES
            //      ('value1', 'value2', ... 'valueN');
            string columns;
            string values;
            List<string> queries = new List<string>();
            string query;
            command_executed = "";


            result = true;
            foreach (Dictionary<String, Object> data in insertion_statements)
            {
                columns = "";
                values = "";
                query = "";
                foreach (KeyValuePair<String, Object> val in data)
                {
                    columns += String.Format(" {0}" + COMMA, val.Key.ToString()); // transform dictionary entries into a string "col1, col2, ... coln"
                    if (this.IsNumber(val.Value))
                        values += String.Format(" {0}" + COMMA, val.Value);          // transform dictionary entries into a string "'value1', 'value2', ... 'valueN'"
                    else if (null == val.Value)
                    {
                        values += String.Format(" {0} = NULL{1}", val.Key.ToString(), COMMA);
                    }
                    else // it must be a string
                        values += String.Format(" {0}" + COMMA, quote((string) val.Value));
                }
                columns = columns.Substring(0, columns.Length - COMMA.Length);     // Remove last comma in the sequence to produce (col1, col2, ... coln)  
                values = values.Substring(0, values.Length - COMMA.Length);        // Remove last comma in the sequence 

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
            ExecuteNonQueryWrappedInBeginEnd(queries, out result);
        }

        #endregion


        #region Insertion: Multiple Rows
        /// <summary>
        /// Efficiently insert 1 to 500 rows into the database in one operation. 
        /// TODO MODIFY IT SO THAT IT DOES IT IN MULTIPLES OF 500 AS NEEDED
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="columnsList"></param>
        /// <param name="valuesList"></param>
        /// <param name="result"></param>
        /// <param name="command_executed"></param>
        public void InsertMultiples(String tableName, List <String> columnsList, List<List<String>> valuesList, out bool result, out string command_executed)
        {
            MyTrace.MethodName("SQL");
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
            command_executed = "";
            result = true;

            // Check limits on how many rows (unions) we can update
            // Just return false if we are trying to do too many or too few  (i.e, between 2 - 100)
            if (valuesList.Count > maxRows || valuesList.Count < minRows)
            {
                result = false;
                return;
            }

            // The first row
            String query = INSERT_INTO + quote(tableName);    // INSERT INTO 'table' 

            query += Environment.NewLine + BRACKET_OPENING;   // ('column1', 'column2', ... 'columnN') 
            foreach (string column_name in columnsList)         
            {
                query += quote(column_name) + COMMA;         
            }
            query = query.Remove(query.Length - COMMA.Length);
            query += BRACKET_CLOSING ;

            query += Environment.NewLine + VALUES;              // VALUES

            // For each row to insert, provide the corresponding values
            foreach (List<String> row_values in valuesList)     // ('value11',  value 12, ... value1N),
            {
                query += Environment.NewLine;
                query += BRACKET_OPENING;      
                                              
                foreach (string value in row_values)
                {
                    query += quote(value) + COMMA;
                }
                // Remove the last comma
                query = query.Remove(query.Length - COMMA.Length); 
                query += BRACKET_CLOSING + COMMA;
            }
            query = query.Remove(query.Length - COMMA.Length);  // ('valueM1',  value M2, ... valueMN);
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
        ///     Run a generic Select query against the Database, with results returned as rows in a datatable.
        /// </summary>
        /// <param name="query">The SQL to run</param>
        /// <returns>A DataTable containing the result set.</returns>
        public DataTable GetDataTableFromSelect(string query,  out bool result, out string command_executed)
        {
            MyTrace.MethodName("SQL");
            result = true;
            command_executed = "";

            DataTable dt = new DataTable();
            try
            {
                // Open the connection
                SQLiteConnection cnn = new SQLiteConnection(dbConnection);
                cnn.Open();
                SQLiteCommand mycommand = new SQLiteCommand(cnn);
                command_executed = query;
                mycommand.CommandText = query;
                SQLiteDataReader reader = mycommand.ExecuteReader();
                dt.Load(reader);
                reader.Close();
                cnn.Close();
            }
            catch 
            {
                result = false;
            }
            return dt;
        }

        /// <summary>
        ///     Run a generic Select query against the Database, with a single result returned as an object that must be cast. 
        /// </summary>
        /// <param name="query">The SQL to run</param>
        /// <returns>A value containing the single result.</returns>
        public Object GetObjectFromSelect(string query, out bool result, out string command_executed)
        {
            MyTrace.MethodName("SQL");
            result = true;
            command_executed = "";
            try
            {
                // Open the connection
                SQLiteConnection cnn = new SQLiteConnection(dbConnection);
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
                //return value.ToString();
            }
        }

        /// <summary>
        ///  Run a generic Select query against the Database, with a single result returned as an integer 
        /// </summary>
        /// <param name="query"></param>
        /// <param name="result"></param>
        /// <param name="command_executed"></param>
        /// <returns></returns>
        public int GetIntFromSelect(string query, out bool result, out string command_executed)
        {
            return (Convert.ToInt32 (GetObjectFromSelect(query, out result, out command_executed)));
        }

        /// <summary>
        ///  Run a generic Select query against the Database, with a single result returned as a string 
        /// </summary>
        /// <param name="query"></param>
        /// <param name="result"></param>
        /// <param name="command_executed"></param>
        /// <returns></returns>
        public string GetStringFromSelect(string query, out bool result, out string command_executed)
        {
            MyTrace.MethodName("SQL");
            return (string)GetObjectFromSelect(query, out result, out command_executed);
        }
        #endregion

        #region Query Execution
        /// <summary>
        ///     Allows the programmer to interact with the database for purposes other than a query.
        /// </summary>
        /// <param name="query">The SQL to be run.</param>
        /// <returns>An Integer containing the number of rows updated.</returns>
        public int ExecuteNonQuery(string query, out bool result)
        {
            MyTrace.MethodName("SQL");
            result = true;
            try
            {
                SQLiteConnection cnn = new SQLiteConnection(dbConnection);
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
        /// <param name="queries"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        public int ExecuteNonQueryWrappedInBeginEnd(List<string> queries, out bool result)
        {
            MyTrace.MethodName("SQL");
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
                if (state_inbegin == false)
                {
                    this.begin_cnn = new SQLiteConnection(dbConnection);
                    this.begin_cnn.Open();
                }
                
                SQLiteCommand mycommand;

                // Invoke each query in the queries list
                foreach (string query in queries)
                {
                    query_count++;
                    // Insert a BEGIN if we are at the beginning of the count
                    if (query_count == 1 && state_inbegin == false)
                    {
                        mycommand = new SQLiteCommand(this.begin_cnn);
                        mycommand.CommandText = BEGIN;
                        //Debug.Print ("BeforeBegin in Exec");
                        rowsUpdated += mycommand.ExecuteNonQuery();
                        //Debug.Print(mycommand.CommandText);
                    }

                    mycommand = new SQLiteCommand(this.begin_cnn);
                    mycommand.CommandText = query;
                    //Debug.Print(mycommand.CommandText);
                    rowsUpdated +=mycommand.ExecuteNonQuery();
                    //Debug.Print(query_count.ToString());
                

                    // END
                     if (query_count == max_count && state_inbegin == false)
                     {
                         //Debug.Print("First End in Exec");
                         mycommand = new SQLiteCommand(this.begin_cnn);
                         mycommand.CommandText = END;
                         rowsUpdated += mycommand.ExecuteNonQuery();
                         query_count = 0;
                         //Debug.Print(mycommand.CommandText);
                     }
                }
                // END
                if (query_count != 0 && state_inbegin == false)
                {
                    //Debug.Print("2nd End in Exec");
                    mycommand = new SQLiteCommand(this.begin_cnn);
                    mycommand.CommandText = END;
                    rowsUpdated += mycommand.ExecuteNonQuery();
                    //Debug.Print(mycommand.CommandText);
                }
                if (state_inbegin == false)
                {
                    this.begin_cnn.Close();
                }
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
        // BEGIN command.So we can create a bunch of queries elsewhere but have them executed on mass
        public void WrapWithBegin(out bool result, out string command_executed)
        {
            MyTrace.MethodName("SQL");
            result = true;
            command_executed = "";
            this.state_inbegin = true;
            try
            {
                this.begin_cnn = new SQLiteConnection(dbConnection);
                this.begin_cnn.Open();
                SQLiteCommand mycommand;
                mycommand = new SQLiteCommand(this.begin_cnn);
                mycommand.CommandText = BEGIN;
                command_executed = mycommand.CommandText;
                //Debug.Print("BeforeBegin");
                mycommand.ExecuteNonQuery();
                //Debug.Print(mycommand.CommandText);
                //ExecuteNonQuery(BEGIN, out result);
                //MessageBox.Show(result.ToString());
            }
            catch
            {
                result = false;
            }
        }

        // END command. Where we created a bunch of queries elsewhere (prefixed with a BEGIN) and have them executed on mass
        public void WrapWithEnd(out bool result, out string command_executed)
        {
            MyTrace.MethodName("SQL");
            result = true;
            command_executed = "";
            this.state_inbegin = false;
            try
            {
                SQLiteCommand mycommand;
                mycommand = new SQLiteCommand(this.begin_cnn);
                mycommand.CommandText = END;
                command_executed = mycommand.CommandText;
                //Debug.Print("BeforeEnd");
                mycommand.ExecuteNonQuery();   
                //ExecuteNonQuery(END, out result);
                this.begin_cnn.Close();
                //Debug.Print(mycommand.CommandText);
            }
            catch
            {
                result = false;
            }
        } 
        #region Updates
        public void UpdateWhereBeginEnd(String table_name, Dictionary <Dictionary<String, Object>, String> update_query_list, out bool result, out string command_executed)
        {
            MyTrace.MethodName("SQL");
            string query = "";
            command_executed = "";
            result = true;
            List<String> queries = new List<String>();
            foreach (KeyValuePair<Dictionary<String, Object>, String> update_query in update_query_list)
            {
                query = UpdateCreateSingleUpdateQuery(table_name, update_query.Key, update_query.Value);
                if (query.Equals ("")) continue; // skip non-queries
                queries.Add (query);
                command_executed += query; // The string of queries
            }
            ExecuteNonQueryWrappedInBeginEnd(queries, out result);
        }

        /// <summary>
        /// Update specific rows in the DB as specified in the where clause.
        /// </summary>
        /// <param name="tableName">The table to update.</param>
        /// <param name="columnname_value_list">A dictionary containing Column names and their new values.</param>
        /// <param name="where">The where clause for the update statement.</param>
        /// <returns>A boolean true or false to signify success or failure.</returns>
        public void UpdateWhere (String table_name, Dictionary<String, Object> columnname_value_list, String where, out bool result, out string command_executed)
        {
            MyTrace.MethodName("SQL");
            // UPDATE table_name SET 
            // colname1 = value1, 
            // colname2 = value2,
            // ...
            // colnameN = valueN
            // WHERE
            // <condition> e.g., ID=1;
            string query = ""; 
            result = true;
            command_executed = "";
            query = UpdateCreateSingleUpdateQuery(table_name, columnname_value_list, where);
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
        private string UpdateCreateSingleUpdateQuery (String table_name, Dictionary<String, Object> columnname_value_list, String where)
        {
            MyTrace.MethodName("SQL");
            // UPDATE table_name SET 
            // colname1 = value1, 
            // colname2 = value2,
            // ...
            // colnameN = valueN
            // WHERE
            // <condition> e.g., ID=1;
            string cells_to_update = "";

            if (columnname_value_list.Count < 0) return "";     // No data, so nothing to update. This isn't really an error, so...

            foreach (KeyValuePair<String, Object> val in columnname_value_list)                          // column_name = 'value',
            {
                // we have to cater to different formats for integers, NULLS and strings...
                if (this.IsNumber(val.Value))
                {
                    cells_to_update += String.Format(" {0} = {1}{2}", val.Key, val.Value.ToString(), COMMA);
                }
                else if (val.Value == null)
                {
                    cells_to_update += String.Format(" {0} = NULL{1}", val.Key.ToString(), COMMA);
                }
                else if (val.Value is string)
                {
                    cells_to_update += String.Format(" {0} = {1}{2}", val.Key.ToString(), quote(val.Value.ToString()), COMMA);
                }
                else
                {
                    // This shouldn't happen, but just in case...
                    return "";
                }
            }
            cells_to_update = cells_to_update.Substring(0, cells_to_update.Length - COMMA.Length); // Remove the last comma

            string query = UPDATE + table_name + SET;
            query += cells_to_update;
            query += WHERE;
            query += where;
            query += SEMICOLON;
            return query;
        }


        public void UpdateMultipleRows(String table_name, List<Tuple<string, int, string>> tuple_list, out bool result, out string command_executed)
        {
            MyTrace.MethodName("SQL");
            // http://www.karlrixon.co.uk/writing/update-multiple-rows-with-different-values-and-a-single-sql-query/
            //UPDATE table_name
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
            //WHERE id IN (1,2,3)
            result = true;
            command_executed = "";

            tuple_list.Sort(Comparer<Tuple<string, int, string>>.Default);

            string last_column = "";
            string current_column;
            List <int> id_list = new List<int>();

            bool first_time = true;
            string query = UPDATE + table_name;             // UPDATE table_name
            query += SET + Environment.NewLine;                                   // SET
            foreach (Tuple<string, int, string> tuple in tuple_list)
            {
                current_column = tuple.Item1;
                
                if (! current_column.Equals (last_column))  // Start a new column set
                {
                    if (!first_time) 
                    {
                        query += END + COMMA;
                    }
                    else
                    {
                        first_time = false;
                    }
                    query += Environment.NewLine + " " + current_column + EQUALS_CASE_ID ;                          //<column_name> EQUALS CASE Id
                }
                query += Environment.NewLine + WHEN + tuple.Item2.ToString() + THEN + quote(tuple.Item3);    // WHEN <ID> THEN <new value>
                if (!id_list.Contains(tuple.Item2)) id_list.Add(tuple.Item2);   // A running list of the IDs seen so far
                last_column = current_column;
            }
            query = query.Remove(query.Length - COMMA.Length);
            query += END;
            query += WHERE_ID_IN + BRACKET_OPENING;
            foreach (int i in id_list)
            {
                query += i.ToString();
                query += COMMA;
            }
            query = query.Remove(query.Length - COMMA.Length);
            query += BRACKET_CLOSING + SEMICOLON;

       //     MessageBox.Show (query);
            query = "";
            query = UPDATE + table_name;
            query += SET;
            query += "Col2" + " = CASE id ";
            query += WHEN + "1 " + THEN + "'new21'" ;
            query += WHEN + "2 " + THEN + "'new22'";
            query += WHEN + "3 " + THEN + "'new23'";
            query += END + COMMA;

            query += "Col3" + " = CASE id ";
            query += WHEN + "2 " + THEN + "'new32'";
            query += WHEN + "3 " + THEN + "'new33'";
            query += WHEN + "4 " + THEN + "'new34'";
            query += END;
            query += WHERE + "id in (1,2,3,4)";  /// ADDED FOR EFFICIENCY AS IT REDUCES THE NUMBER OF TESTS, BUT CAN BE LEFT OUT
            
            ///////////
            //query = "";
            //query += UPDATE + table_name + SET + " Col2 = 'new21'  WHERE Id = 2; ";
            //query += UPDATE + table_name + SET + " Col2 = 'new22'  WHERE Id = 2; ";
            //query += UPDATE + table_name + SET + " Col4 = 'new24'  WHERE Id = 4; ";
            //query += UPDATE + table_name + SET + " Col5 = 'new25'  WHERE Id = 5; ";
          //  query += UNION_ALL + SET + " Col2 = 'new21'  WHERE Id = 1 ";
         //   query += UNION_ALL + SET + " Col3 = 'new33'  WHERE Id = 3;";
            command_executed = query;
            MessageBox.Show(command_executed);
            try
            {
       
              //  this.ExecuteBeginNonQueryEnd (query, out result);
               
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
        /// <returns>An int</returns>
        public int GetCountFromSelect(string query, out bool result, out string command_executed)
        {
            return GetIntFromSelect(query, out result, out command_executed);
        }
        #endregion

        #region Deleting Rows 
        /// <summary>
        ///  Delete specific rows from the DB where...
        /// </summary>
        /// <param name="tableName">The table from which to delete.</param>
        /// <param name="where">The where clause for the delete.</param>
        /// <returns>A boolean true or false to signify success or failure.</returns>
        public void DeleteFromTable(String table_name, String where, out bool result, out string command_executed)
        {
            MyTrace.MethodName("SQL");
            // DELETE FROM table_name WHERE where
            result = true;
            command_executed = "";
            string query = DELETE_FROM + table_name;        // DELETE FROM table_name
            if (! where.Trim ().Equals ("") )         // Add the WHERE clause only when where is not empty
            {
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

        /// <summary>
        /// Delete all rows from a specific table.
        /// </summary>
        /// <param name="table_name"></param>
        /// <param name="result"></param>
        /// <param name="command_executed"></param>
        public void DeleteAllFromTable(String table_name, out bool result, out string command_executed)
        {
            MyTrace.MethodName("SQL");
            DeleteFromTable(table_name, "", out result, out command_executed);
        }

        /// <summary>
        ///   Delete all rows from all tables in the database.
        /// </summary>
        /// <param name="result"></param>
        /// <param name="command_executed"></param>
        public void DeleteAllFromAllTables(out bool result, out string command_executed)
        {
            MyTrace.MethodName("SQL");
            // SELECT NAME FROM SQLITE_MASTER WHERE TYPE='table' ORDER BY NAME;
            result = true;
            command_executed = "";
            DataTable tables;
            try
            {
                string query = SELECT;                  
                query += NAME_FROM_SQLITE_MASTER; 
                query += WHERE;
                query += TYPE_EQUAL_TABLE;
                query += ORDER_BY + NAME;
                command_executed = query;
                tables = this.GetDataTableFromSelect(query, out result, out command_executed);
                foreach (DataRow table in tables.Rows)
                {
                    this.DeleteAllFromTable (table["NAME"].ToString(), out result, out command_executed);
                }
            }
            catch
            {
                result = false;
            }
        }
        #endregion

        #region Utilities
        private string quote(string s)
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