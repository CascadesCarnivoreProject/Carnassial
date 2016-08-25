using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Timelapse.Util;

namespace Timelapse.Database
{
    /// <summary>
    /// Class CustomFilter holds a list search term particles, each reflecting criteria for a given field
    /// </summary>
    public class CustomFilter
    {
        public List<SearchTerm> SearchTerms { get; private set; }
        public CustomFilterOperator TermCombiningOperator { get; set; }

        /// <summary>
        /// Create a CustomFilter, where we build a list of potential search terms based on the controls found in the sorted template table
        /// The search term will be used only if its 'UseForSearching' field is true
        /// </summary>
        public CustomFilter(DataTableBackedList<ControlRow> templateTable, CustomFilterOperator termCombiningOperator)
        {
            this.SearchTerms = new List<SearchTerm>();
            this.TermCombiningOperator = termCombiningOperator;

            // Initialize the filter to reflect the desired controls in the template (in control order)
            foreach (ControlRow control in templateTable)
            {
                string controlType = control.Type;
                // add a control here to prevent it from appearing in CustomFilter
                // folder is usually the same for all files in the image set and not useful for filtering
                // date, time, and UTC offset are redundant with DateTime
                if (controlType == Constants.DatabaseColumn.Date ||
                    controlType == Constants.DatabaseColumn.Folder ||
                    controlType == Constants.DatabaseColumn.Time) 
                {
                    continue;
                }

                // Create a new search term for each row, where each row specifies a particular control and how it can be searched
                string defaultValue = String.Empty;
                string termOperator = Constants.SearchTermOperator.Equal;
                if (controlType == Constants.Control.Counter)
                {
                    defaultValue = "0";
                    termOperator = Constants.SearchTermOperator.GreaterThan;  // Makes more sense that people will test for > as the default rather than counters
                }
                else if (controlType == Constants.DatabaseColumn.DateTime)
                {
                    // TimelapseWindow and CustomViewFilter typically replace this default with the date time of the current image.
                    defaultValue = DateTimeHandler.ToDisplayDateString(DateTime.Now - TimeSpan.FromDays(30));
                    termOperator = Constants.SearchTermOperator.GreaterThanOrEqual;
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
                searchTerm.Operator = termOperator;
                searchTerm.DatabaseValue = defaultValue;
                searchTerm.List = control.List;
                this.SearchTerms.Add(searchTerm);
            }
        }

        // Create and return the query composed from the search term list
        public string GetImagesWhere()
        {
            string where = String.Empty;
            // Construct and show the search term only if that search row is activated
            foreach (SearchTerm searchTerm in this.SearchTerms.Where(term => term.UseForSearching))
            {
                string whereForTerm;
                // Check to see if the search should match an empty value, in which case we also need to deal with NULLs 
                if (String.IsNullOrEmpty(searchTerm.DatabaseValue))
                {
                    // The where expression constructed should look something like: (DataLabel IS NULL OR DataLabel = '')
                    whereForTerm = " (" + searchTerm.DataLabel + " IS NULL OR " + searchTerm.DataLabel + " = '') ";
                }
                else
                {
                    // The where expression constructed should look something like DataLabel > "5"
                    Debug.Assert(searchTerm.DatabaseValue.Contains("\"") == false, String.Format("Search term '{0}' contains quotation marks and could be used for SQL injection.", searchTerm.DatabaseValue));
                    whereForTerm = searchTerm.DataLabel + this.TermToSqlOperator(searchTerm.Operator) + "\"" + searchTerm.DatabaseValue.Trim() + "\""; // Need to quote the value 
                }

                // if there is already a term in the query add either and 'And' or an 'Or' to it 
                if (where.Length > 0)
                {
                    switch (this.TermCombiningOperator)
                    {
                        case CustomFilterOperator.And:
                            where += " And ";
                            break;
                        case CustomFilterOperator.Or:
                            where += " Or ";
                            break;
                        default:
                            throw new NotSupportedException(String.Format("Unhandled logical operator {0}.", this.TermCombiningOperator));
                    }
                }
                where += whereForTerm;
            }
            return where.Trim();
        }

        // return SQL expressions to database equivalents
        // this is needed as the searchterm operators are unicodes representing symbols rather than real opeators 
        // e.g., \u003d is the symbol for '='
        private string TermToSqlOperator(string expression)
        {
            switch (expression)
            {
                case Constants.SearchTermOperator.Equal:
                    return "=";
                case Constants.SearchTermOperator.NotEqual:
                    return "<>";
                case Constants.SearchTermOperator.LessThan:
                    return "<";
                case Constants.SearchTermOperator.GreaterThan:
                    return ">";
                case Constants.SearchTermOperator.LessThanOrEqual:
                    return "<=";
                case Constants.SearchTermOperator.GreaterThanOrEqual:
                    return ">=";
                case Constants.SearchTermOperator.Glob:
                    return Constants.SearchTermOperator.Glob;
                default:
                    return String.Empty;
            }
        }
    }
}
