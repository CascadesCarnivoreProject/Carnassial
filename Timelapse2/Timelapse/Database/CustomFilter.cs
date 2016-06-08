using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Timelapse.Database
{
    /// <summary>
    /// Class CustomFilter holds a dictionary of search term expressions, each entry reflecting search terms for a given field
    /// </summary>
    public class CustomFilter
    {
        private ImageDatabase database;

        public Dictionary<int, SearchTerm> SearchTermList { get; private set; }
        public CustomFilterOperator LogicalOperator { get; set; }

        /// <summary>
        /// Create a CustomFilter, where we build a list of potential search term expressions based on the controls found in the sorted template table
        /// The search term will be used only if its 'UseForSearching' field is true
        /// </summary>
        public CustomFilter(ImageDatabase database)
        {
            this.database = database;
            this.LogicalOperator = CustomFilterOperator.Or; // We default the search operation to this logical operation
            this.SearchTermList = new Dictionary<int, SearchTerm>();

            // Initialize the filter to reflect the desired controls in the template (in sorted order)
            int row_count = 1; // We start at 1 as there is already a header row
            for (int i = 0; i < this.database.TemplateTable.Rows.Count; i++)
            {
                // Get the values for each control
                DataRow row = this.database.TemplateTable.Rows[i];
                string type = row[Constants.Control.Type].ToString();

                // We only handle certain types, e.g., we don't give the user the opportunity to search over file names / folders / date / time
                if (type == Constants.Control.Note ||
                    type == Constants.Control.Counter ||
                    type == Constants.Control.FixedChoice ||
                    type == Constants.DatabaseColumn.ImageQuality ||
                    type == Constants.Control.Flag)
                {
                    // Create a new search expression for each row, where each row specifies a particular control and how it can be searched
                    string default_value = String.Empty;
                    string expression = Constants.Filter.Equal;
                    bool is_use_for_searching = false;
                    if (type == Constants.Control.Counter)
                    {
                        default_value = "0";
                        expression = Constants.Filter.GreaterThan;  // Makes more sense that people will test for > as the default rather than counters
                    }
                    else if (type == Constants.Control.Flag)
                    {
                        default_value = Constants.Boolean.False;
                    }

                    // Create a new search term and add it to the list
                    SearchTerm st = new SearchTerm();
                    st.UseForSearching = is_use_for_searching;
                    st.Type = type;
                    st.Label = row.GetStringField(Constants.Control.Label);
                    st.DataLabel = row.GetStringField(Constants.Control.DataLabel);
                    st.Expression = expression;
                    st.Value = default_value;
                    st.List = row.GetStringField(Constants.Control.List);
                    this.SearchTermList.Add(row_count, st);
                    row_count++;
                }
            }
        }

        #region Public methods to Run the Query
        /// <summary>Gets the count of how many results will be expected if the query is executed </summary>
        public int GetImageCount()
        {
            string where = this.CreateWhere();
            if (where.Trim() == String.Empty)
            {
                return this.database.GetImageCount(); // If there is no query, assume it is equivalent to all images
            }
            return this.database.GetImageCountWithCustomFilter(where);
        }

        /// <summary>Gets a value indicating whether there are two or more search terms selected in this query</summary>
        public bool QueryHasMultipleSelectedSearchTerms()
        {
            int count = 0;
            for (int row = 1; row <= this.SearchTermList.Count; row++)
            {
                if (this.SearchTermList[row].UseForSearching)
                {
                    count++;
                }
                if (count > 1)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Run the query on the database</summary>
        /// <returns>Returns success or failure</returns>
        public bool TryRunQuery()
        {
            string where = this.CreateWhere();
            if (where == String.Empty)
            {
                return this.database.TryGetImages(ImageQualityFilter.All);
            }
            return this.database.TryGetImages(where);
        }
        #endregion

        // Create and return the query from the search term list
        private string CreateWhere()
        {
            SearchTerm st;
            string where = String.Empty;
            for (int i = 0; i < this.SearchTermList.Count; i++)
            {
                st = this.SearchTermList.Values.ElementAt(i);

                if (!st.UseForSearching)
                {
                    continue; // Only consider entries that the user has flagged for searching
                }

                // Construct and show the search term only if that search row is activated
                // If there is already an expression in the query, then we add either and 'And' or an 'Or' to it 
                if (where.Length > 0)
                {
                    where += (this.LogicalOperator == CustomFilterOperator.And) ? " And " : " Or ";
                }

                // Now construct the rest of it: DataLabel expresson Value e.g., foo>"5" 
                where += st.DataLabel;
                where += this.TranslateExpression(st.Expression);
                where += "\"" + st.Value.Trim() + "\""; // Need to quote the value 
            }
            return where.Trim();
        }

        // return pretty printed expressions to database equivalents
        private string TranslateExpression(string expression)
        {
            switch (expression)
            {
                case Constants.Filter.Equal:
                    return "=";
                case Constants.Filter.NotEqual:
                    return "<>";
                case Constants.Filter.LessThan:
                    return "<";
                case Constants.Filter.GreaterThan:
                    return ">";
                case Constants.Filter.LessThanOrEqual:
                    return "<=";
                case Constants.Filter.GreaterThanOrEqual:
                    return ">=";
                case Constants.Filter.Glob:
                    return Constants.Filter.Glob;
                default:
                    return String.Empty;
            }
        }
    }
}
