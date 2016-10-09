﻿using Carnassial.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Carnassial.Database
{
    /// <summary>
    /// Holds a list search term particles, each reflecting criteria for a given field
    /// </summary>
    public class CustomSelection
    {
        public List<SearchTerm> SearchTerms { get; private set; }
        public CustomSelectionOperator TermCombiningOperator { get; set; }

        public CustomSelection(DataTableBackedList<ControlRow> templateTable, CustomSelectionOperator termCombiningOperator)
        {
            this.SearchTerms = new List<SearchTerm>();
            this.TermCombiningOperator = termCombiningOperator;

            // generate search terms for all relevant controls in the template (in control order)
            foreach (ControlRow control in templateTable)
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
                    case Constant.Control.Counter:
                        searchTerm.DatabaseValue = "0";
                        searchTerm.Operator = Constant.SearchTermOperator.GreaterThan;  // Makes more sense that people will test for > as the default rather than counters
                        break;
                    case Constant.DatabaseColumn.DateTime:
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
                    case Constant.Control.Flag:
                        searchTerm.DatabaseValue = Constant.Boolean.False;
                        break;
                    case Constant.DatabaseColumn.UtcOffset:
                        // the first time it's popped CustomViewSelection dialog changes this default to the date time of the current image
                        searchTerm.SetDatabaseValue(Constant.ControlDefault.DateTimeValue.Offset);
                        break;
                    default:
                        // default values above
                        break;
                }
            }
        }

        public DateTimeOffset GetDateTime(int dateTimeSearchTermIndex, TimeZoneInfo imageSetTimeZone)
        {
            DateTime dateTime = this.SearchTerms[dateTimeSearchTermIndex].GetDateTime();
            return DateTimeHandler.FromDatabaseDateTimeOffset(dateTime, imageSetTimeZone.GetUtcOffset(dateTime));
        }

        // Create and return the query composed from the search term list
        public string GetFilesWhere()
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
                        case CustomSelectionOperator.And:
                            where += " And ";
                            break;
                        case CustomSelectionOperator.Or:
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
                    return "=";
                case Constant.SearchTermOperator.NotEqual:
                    return "<>";
                case Constant.SearchTermOperator.LessThan:
                    return "<";
                case Constant.SearchTermOperator.GreaterThan:
                    return ">";
                case Constant.SearchTermOperator.LessThanOrEqual:
                    return "<=";
                case Constant.SearchTermOperator.GreaterThanOrEqual:
                    return ">=";
                case Constant.SearchTermOperator.Glob:
                    return Constant.SearchTermOperator.Glob;
                default:
                    return String.Empty;
            }
        }
    }
}
