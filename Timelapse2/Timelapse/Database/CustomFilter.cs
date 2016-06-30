using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            for (int rowIndex = 0; rowIndex < this.database.TemplateTable.RowCount; rowIndex++)
            {
                // Get the values for each control
                ControlRow control = this.database.TemplateTable[rowIndex];

                // We only handle certain types, e.g., we don't give the user the opportunity to search over file names / folders / date / time
                string controlType = control.Type;
                if (controlType == Constants.Control.Note ||
                    controlType == Constants.Control.Counter ||
                    controlType == Constants.Control.FixedChoice ||
                    controlType == Constants.DatabaseColumn.ImageQuality ||
                    controlType == Constants.DatabaseColumn.RelativePath ||
                    controlType == Constants.Control.Flag)
                {
                    // Create a new search expression for each row, where each row specifies a particular control and how it can be searched
                    string defaultValue = String.Empty;
                    string expression = Constants.Filter.Equal;
                    if (controlType == Constants.Control.Counter)
                    {
                        defaultValue = "0";
                        expression = Constants.Filter.GreaterThan;  // Makes more sense that people will test for > as the default rather than counters
                    }
                    else if (controlType == Constants.Control.Flag)
                    {
                        defaultValue = Constants.Boolean.False;
                    }

                    // Create a new search term and add it to the list
                    SearchTerm searchTerm = new SearchTerm();
                    searchTerm.UseForSearching = false;
                    searchTerm.Type = controlType;
                    searchTerm.Label = control.Label;
                    searchTerm.DataLabel = control.DataLabel;
                    searchTerm.Expression = expression;
                    searchTerm.Value = defaultValue;
                    searchTerm.List = control.List;
                    // We start at 1 as there is already a header row
                    this.SearchTermList.Add(rowIndex + 1, searchTerm);
                }
            }
        }

        /// <summary>Gets the count of how many results will be expected if the query is executed </summary>
        public int GetImageCount()
        {
            string where = this.GetImagesWhere();
            if (String.IsNullOrWhiteSpace(where))
            {
                return this.database.GetImageCount(ImageFilter.All); // If there is no query, assume it is equivalent to all images
            }
            return this.database.GetImageCountWithCustomFilter(where);
        }

        /// <summary>Gets a value indicating whether there are two or more search terms selected in this query</summary>
        public bool QueryHasMultipleSelectedSearchTerms()
        {
            int count = 0;
            SearchTerm st;

            for (int i = 0; i < this.SearchTermList.Count; i++)
            {
                st = this.SearchTermList.Values.ElementAt(i);
                if (st.UseForSearching)
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

        // Create and return the query from the search term list
        public string GetImagesWhere()
        {
            string where = String.Empty;
            for (int index = 0; index < this.SearchTermList.Count; index++)
            {
                SearchTerm searchTerm = this.SearchTermList.Values.ElementAt(index);

                if (!searchTerm.UseForSearching)
                {
                    continue; // Only consider entries that the user has flagged for searching
                }

                // Construct and show the search term only if that search row is activated
                // If there is already an expression in the query, then we add either and 'And' or an 'Or' to it 
                if (where.Length > 0)
                {
                    switch (this.LogicalOperator)
                    {
                        case CustomFilterOperator.And:
                            where += " And ";
                            break;
                        case CustomFilterOperator.Or:
                            where += " Or ";
                            break;
                        default:
                            throw new NotSupportedException(String.Format("Unhandled logical operator {0}.", this.LogicalOperator));
                    }
                }

                // Now construct the rest of it
                // Check to see if the search should match an empty value, in which case we also need to deal with NULLs 
                if (String.IsNullOrWhiteSpace(searchTerm.Value) && searchTerm.Expression == Constants.Filter.Equal)
                {
                    // The where expression constructed should look something like: (DataLabel IS NULL OR DataLabel = '')
                    where += " (" + searchTerm.DataLabel + " IS NULL OR " + searchTerm.DataLabel + " = '') ";
                }
                else
                {
                    // The where expression constructed should look something like DataLabel > "5"
                    where += searchTerm.DataLabel;
                    where += this.TranslateExpression(searchTerm.Expression);
                    Debug.Assert(searchTerm.Value.Contains("\"") == false, String.Format("Search term '{0}' contains quotation marks and could be used for SQL injection.", searchTerm.Value));
                    where += "\"" + searchTerm.Value.Trim() + "\""; // Need to quote the value 
                }
            }
            return where.Trim();
        }

        // return SQL expressions to database equivalents
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
