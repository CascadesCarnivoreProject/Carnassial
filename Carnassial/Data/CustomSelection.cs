using Carnassial.Database;
using Carnassial.Util;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Carnassial.Data
{
    /// <summary>
    /// Holds a list search term particles, each reflecting criteria for a given field
    /// </summary>
    public class CustomSelection
    {
        public List<SearchTerm> SearchTerms { get; private set; }
        public LogicalOperator TermCombiningOperator { get; set; }

        public CustomSelection(DataTableBackedList<ControlRow> controlTable, LogicalOperator termCombiningOperator)
        {
            this.SearchTerms = new List<SearchTerm>();
            this.TermCombiningOperator = termCombiningOperator;

            // generate search terms for all relevant controls in the template (in control order)
            foreach (ControlRow control in controlTable)
            {
                // skip hidden controls as they're not normally a part of the user experience
                // this is potentially problematic in corner cases; an option to show terms for all controls can be added if needed
                if (control.Visible == false)
                {
                    continue;
                }

                // create search term for the control
                SearchTerm searchTerm = new SearchTerm();
                searchTerm.ControlType = control.Type;
                searchTerm.DataLabel = control.DataLabel;
                searchTerm.DatabaseValue = control.DefaultValue;
                searchTerm.Operator = Constant.SearchTermOperator.Equal;
                searchTerm.Label = control.Label;
                searchTerm.List = control.GetChoices();
                searchTerm.UseForSearching = false;
                this.SearchTerms.Add(searchTerm);

                switch (searchTerm.ControlType)
                {
                    case ControlType.Counter:
                        searchTerm.DatabaseValue = "0";
                        searchTerm.Operator = Constant.SearchTermOperator.GreaterThan;  // Makes more sense that people will test for > as the default rather than counters
                        break;
                    case ControlType.DateTime:
                        // the first time the CustomViewSelection dialog is popped CarnassialWindow calls SetDateTime() to changes the default date time to the date time 
                        // of the current image
                        searchTerm.DatabaseValue = DateTimeHandler.ToDatabaseDateTimeString(Constant.ControlDefault.DateTimeValue);
                        searchTerm.Operator = Constant.SearchTermOperator.GreaterThanOrEqual;

                        // support querying on a range of datetimes by giving the user two search terms, one configured for the start of the interval and one
                        // for the end
                        SearchTerm dateTimeLessThanOrEqual = new SearchTerm(searchTerm);
                        dateTimeLessThanOrEqual.Operator = Constant.SearchTermOperator.LessThanOrEqual;
                        this.SearchTerms.Add(dateTimeLessThanOrEqual);
                        break;
                    case ControlType.Flag:
                        searchTerm.DatabaseValue = Boolean.FalseString;
                        break;
                    case ControlType.FixedChoice:
                    case ControlType.Note:
                        // default values as above
                        break;
                    case ControlType.UtcOffset:
                        // the first time it's popped CustomViewSelection dialog changes this default to the date time of the current image
                        searchTerm.SetDatabaseValue(Constant.ControlDefault.DateTimeValue.Offset);
                        break;
                    default:
                        throw new NotSupportedException(String.Format("Unhandled control type {0}.", searchTerm.ControlType));
                }
            }
        }

        // create and return the query formed by the search term list
        public Select CreateSelect()
        {
            Select select = new Select(Constant.DatabaseTable.FileData);
            select.WhereCombiningOperator = this.TermCombiningOperator;
            foreach (SearchTerm searchTerm in this.SearchTerms.Where(term => term.UseForSearching))
            {
                select.Where.Add(new WhereClause(searchTerm.DataLabel, this.TermToSqlOperator(searchTerm.Operator), searchTerm.DatabaseValue));
            }

            return select;
        }

        public DateTimeOffset GetDateTime(int dateTimeSearchTermIndex, TimeZoneInfo imageSetTimeZone)
        {
            DateTime dateTime = this.SearchTerms[dateTimeSearchTermIndex].GetDateTime();
            return DateTimeHandler.FromDatabaseDateTimeOffset(dateTime, imageSetTimeZone.GetUtcOffset(dateTime));
        }

        public void SetDateTime(int dateTimeSearchTermIndex, DateTimeOffset newDateTime, TimeZoneInfo imageSetTimeZone)
        {
            DateTimeOffset dateTime = this.GetDateTime(dateTimeSearchTermIndex, imageSetTimeZone);
            this.SearchTerms[dateTimeSearchTermIndex].SetDatabaseValue(new DateTimeOffset(newDateTime.DateTime, dateTime.Offset));
        }

        public void SetDateTimesAndOffset(DateTimeOffset dateTime)
        {
            foreach (SearchTerm dateTimeTerm in this.SearchTerms.Where(term => term.DataLabel == Constant.DatabaseColumn.DateTime))
            {
                dateTimeTerm.SetDatabaseValue(dateTime);
            }

            SearchTerm utcOffsetTerm = this.SearchTerms.FirstOrDefault(term => term.DataLabel == Constant.DatabaseColumn.UtcOffset);
            if (utcOffsetTerm != null)
            {
                utcOffsetTerm.SetDatabaseValue(dateTime.Offset);
            }
        }

        // return SQL expressions to database equivalents
        // this is needed as the searchterm operators are unicodes representing symbols rather than real opeators 
        // e.g., \u003d is the symbol for '='
        private string TermToSqlOperator(string expression)
        {
            switch (expression)
            {
                case Constant.SearchTermOperator.Equal:
                    return Constant.SqlOperator.Equal;
                case Constant.SearchTermOperator.Glob:
                    return Constant.SqlOperator.Glob;
                case Constant.SearchTermOperator.GreaterThan:
                    return Constant.SqlOperator.GreaterThan;
                case Constant.SearchTermOperator.GreaterThanOrEqual:
                    return Constant.SqlOperator.GreaterThanOrEqual;
                case Constant.SearchTermOperator.LessThan:
                    return Constant.SqlOperator.LessThan;
                case Constant.SearchTermOperator.LessThanOrEqual:
                    return Constant.SqlOperator.LessThanOrEqual;
                case Constant.SearchTermOperator.NotEqual:
                    return Constant.SqlOperator.NotEqual;
                default:
                    throw new NotSupportedException(String.Format("Unhandled search term operator '{0}'.", expression));
            }
        }
    }
}
