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
        /// The initial date to display is passed into the constructor
        /// </summary>
        public CustomFilter(DataTableBackedList<ControlRow> templateTable, CustomFilterOperator termCombiningOperator)
        {
            this.SearchTerms = new List<SearchTerm>();
            this.TermCombiningOperator = termCombiningOperator;

            // Initialize the filter to reflect the desired controls in the template (in control order)
            foreach (ControlRow control in templateTable)
            {
                // SAUL TODO: We temporarily disable the date until we fie the date problem in the custom filter (see issue 96)
                string controlType = control.Type;
                if (controlType == Constants.DatabaseColumn.Folder ||
                    controlType == Constants.DatabaseColumn.Time ||
                    controlType == Constants.DatabaseColumn.Date) 
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
                else if (controlType == Constants.DatabaseColumn.Date)
                {
                    // most likely case for date filtering is to select files from the most recent station service
                    // as a default, assume a roughly monthly servicing cadence
                    defaultValue = DateTimeHandler.ToStandardDateString(DateTime.Now - TimeSpan.FromDays(30));
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
                searchTerm.Value = defaultValue;
                searchTerm.List = control.List;
                this.SearchTerms.Add(searchTerm);
            }
        }

        public void FilterByDate(DataTable images)
        {
            SearchTerm dateTerm = this.SearchTerms.Single(term => term.DataLabel == Constants.DatabaseColumn.Date);
            Debug.Print(dateTerm.Value.ToString());
            Debug.Assert(dateTerm.UseForSearching, "Date search term is not selected.");

            Func<DateTime, DateTime, bool> dateFilter;
            switch (dateTerm.Operator)
            {
                case Constants.SearchTermOperator.GreaterThan:
                    dateFilter = (DateTime imageDate, DateTime dateTermDate) => { return imageDate > dateTermDate; };
                    break;
                case Constants.SearchTermOperator.GreaterThanOrEqual:
                    dateFilter = (DateTime imageDate, DateTime dateTermDate) => { return imageDate >= dateTermDate; };
                    break;
                case Constants.SearchTermOperator.LessThan:
                    dateFilter = (DateTime imageDate, DateTime dateTermDate) => { return imageDate < dateTermDate; };
                    break;
                case Constants.SearchTermOperator.LessThanOrEqual:
                    dateFilter = (DateTime imageDate, DateTime dateTermDate) => { return imageDate <= dateTermDate; };
                    break;
                default:
                    throw new NotSupportedException(String.Format("Unhandled search term operator '{0}'.", dateTerm.Operator));
            }

            DateTime dateTermValue = DateTimeHandler.FromStandardDateString(dateTerm.Value);
            for (int row = 0; row < images.Rows.Count; ++row)
            {
                ImageRow image = new ImageRow(images.Rows[row]);
                DateTime imageDatetime;
                if (image.TryGetDateTime(out imageDatetime) == false)
                {
                    // ambiguous what to do if an image doesn't have a valid timestamp; leave such images in the set for now
                    continue;
                }

                if (dateFilter(imageDatetime.Date, dateTermValue.Date) == false)
                {
                    images.Rows.RemoveAt(row);
                    --row;
                }
            }
        }

        // Create and return the query from the search term list
        public string GetImagesWhere(out bool dateFilteringRequired)
        {
            dateFilteringRequired = false;
            string where = String.Empty;
            // Construct and show the search term only if that search row is activated
            foreach (SearchTerm searchTerm in this.SearchTerms.Where(term => term.UseForSearching))
            {
                string whereForTerm;
                // Check to see if the search should match an empty value, in which case we also need to deal with NULLs 
                if (String.IsNullOrEmpty(searchTerm.Value))
                {
                    // The where expression constructed should look something like: (DataLabel IS NULL OR DataLabel = '')
                    whereForTerm = " (" + searchTerm.DataLabel + " IS NULL OR " + searchTerm.DataLabel + " = '') ";
                }
                else if (searchTerm.DataLabel == Constants.DatabaseColumn.Date &&
                        ((searchTerm.Operator != Constants.SearchTermOperator.Equal) && (searchTerm.Operator != Constants.SearchTermOperator.NotEqual)))
                {
                    // Date column is not in a format supported by SQLite (#81) so operators other than equality must be done in code
                    dateFilteringRequired = true;
                    continue;
                }
                else
                {
                    // The where expression constructed should look something like DataLabel > "5"
                    Debug.Assert(searchTerm.Value.Contains("\"") == false, String.Format("Search term '{0}' contains quotation marks and could be used for SQL injection.", searchTerm.Value));
                    whereForTerm = searchTerm.DataLabel + this.TermToSqlOperator(searchTerm.Operator) + "\"" + searchTerm.Value.Trim() + "\""; // Need to quote the value 
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
