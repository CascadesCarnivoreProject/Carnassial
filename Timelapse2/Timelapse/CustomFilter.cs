using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Timelapse
{
    /// <summary>
    /// Class CustomFilter holds a dictionary of search term expressions, each entry reflecting search terms for a given field
    /// </summary>
    public class CustomFilter
    {
        #region Public Properties and Enums 
        public Dictionary<int, SearchTerm> SearchTermList { get; set; }
        public LogicalOperators LogicalOperator { get; set; }

        /// <summary>The query that will be formed from the collected search terms</summary>
        public string Query { get { return CreateQuery(); } }

        /// <summary>Whether there is any query to be made </summary>
        public bool IsQueryEmpty { get { return (Query == ""); } }
        /// <summary>Where there are two or more search terms selected in this query</summary>
        public bool IsQueryHasMultipleSelectedSearchTerms
        {
            get
            {
                int count = 0;
                for (int row = 1; row <= SearchTermList.Count; row++)
                {
                    if (SearchTermList[row].UseForSearching) count++;
                    if (count > 1) return true;
                }
                return false;
            }
        }
        /// <summary> The count of how many results will be expected if the query is executed </summary>
        public int QueryResultCount {
            get
            {
                if (this.Query.Trim() == "") return (this.dbData.GetNoFilterCount()); // If there is no query, assume it is equivalent to all images
                return dbData.GetCustomFilterCount(this.Query);
            }
        }

        public enum LogicalOperators { And, Or };
        #endregion

        #region Private variables and constants
        private DBData dbData;

        private const string CH_EQUALS = "\u003D";
        private const string CH_NOT_EQUALS = "\u2260";
        private const string CH_LESS_THAN = "\u003C";
        private const string CH_GREATER_THAN = "\u003E";
        private const string CH_LESS_THAN_EQUALS = "\u2264";
        private const string CH_GREATER_THAN_EQUALS = "\u2267";
        private const string CH_GLOB = " GLOB ";

        #endregion

        #region Initializer
        /// <summary>
        /// Create a CustomFilter, where we build a list of potential search term expressions based on the controls found in the sorted template table
        /// The search term will be used only if its 'UseForSearching' field is true
        /// </summary>
        /// <param name="db_data"></param>
        public CustomFilter(DBData db_data)
        {
            dbData = db_data;
            LogicalOperator = LogicalOperators.Or; // We default the search operation to this logical operation
            SearchTermList = new Dictionary<int, SearchTerm>();
            
            DataTable sortedTemplateTable = dbData.TemplateGetSortedByControls();

            // Initialize the filter to reflect the desired controls in the template (in sorted order)
            int row_count = 1; // We start at 1 as there is already a header row
            for (int i = 0; i < sortedTemplateTable.Rows.Count; i++)
            {
                // Get the values for each control
                DataRow row = sortedTemplateTable.Rows[i];
                string type = row[Constants.TYPE].ToString();

                // We only handle certain types, e.g., we don't give the user the opportunity to search over file names / folders / date / time
                if (type == Constants.NOTE || type == Constants.COUNTER || type == Constants.FIXEDCHOICE || type == Constants.IMAGEQUALITY || type == Constants.FLAG)
                {
                    // Create a new search expression for each row, where each row specifies a particular control and how it can be searched
                    string default_value = "";
                    string expression = CH_EQUALS;
                    bool is_use_for_searching = false;
                    if (type == Constants.COUNTER)
                    {
                        default_value = "0";
                        expression = CH_GREATER_THAN;  // Makes more sense that people will test for > as the default rather than counters
                    }
                    else if (type == Constants.FLAG)
                    {
                        default_value = "false";
                    }

                    // Create a new search term and add it to the list
                    SearchTerm st = new SearchTerm();
                    st.UseForSearching = is_use_for_searching;
                    st.Type = type;
                    st.Label = (string) row[Constants.LABEL];
                    st.DataLabel = (string) row[Constants.DATALABEL];
                    st.Expression = expression;
                    st.Value = default_value;
                    st.List = (string)row[Constants.LIST];
                    this.SearchTermList.Add(row_count, st);
                    row_count++;
                }
            }
        }
        #endregion

        #region Public methods to Run the Query
        /// <summary>Run the query on the database</summary>
        /// <returns>Returns success or failure</returns>
        public bool RunQuery()
        {
            string query = this.Query;
            if (query == "") return dbData.GetImagesAll();
            return dbData.GetImagesCustom(Query);
        }
        #endregion

        #region Private methods
        // Create and return the query from the search term list
        private string CreateQuery()
        {
            SearchTerm st;
            string query = "";
            for (int i = 0; i < this.SearchTermList.Count; i++)
            {
                st = this.SearchTermList.Values.ElementAt(i);

                if (!st.UseForSearching) continue; // Only consider entries that the user has flagged for searching

                // Construct and show the search term only if that search row is activated
                // If there is already an expression in the query, then we add either and 'And' or an 'Or' to it 
                if (query.Length > 0) query += (this.LogicalOperator == LogicalOperators.And) ? " And " : " Or "; 

                // Now construct the rest of it: DataLabel expresson Value e.g., foo>"5" 
                query += st.DataLabel;
                query += TranslateExpression(st.Expression);
                query += "\"" + st.Value.Trim() + "\""; // Need to quote the value 
            }
            return query.Trim();
        }
 
        // return pretty printed expressions to database equivalents
        private string TranslateExpression(string expression)
        {
            switch (expression)
            {
                case CH_EQUALS:
                    return "=";
                case CH_NOT_EQUALS:
                    return "<>";
                case CH_LESS_THAN:
                    return "<";
                case CH_GREATER_THAN:
                    return ">";
                case CH_LESS_THAN_EQUALS:
                    return "<=";
                case CH_GREATER_THAN_EQUALS:
                    return ">=";
                case CH_GLOB:
                    return " GLOB ";
                default:
                    return "";
            }
        }
        #endregion
    }

    #region SearchTerm Class 
    /// <summary>
    /// Class SearchTerms stores the search term expressions for each field
    /// </summary>
    public class SearchTerm
    {
        public bool UseForSearching { get; set; }
        public string Type { get; set; }
        public string Label { get; set; }
        public string DataLabel { get; set; }
        public string Expression { get; set; }
        public string Value { get; set; }
        public string List { get; set; }
        public SearchTerm()
        {
            Type = "";
            UseForSearching = false;
            Label = "";
            DataLabel = "";
            Expression = "";
            Value = "";
        }
    }
    #endregion

}
