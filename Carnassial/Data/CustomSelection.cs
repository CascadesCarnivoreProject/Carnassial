using Carnassial.Database;
using Carnassial.Util;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Carnassial.Data
{
    /// <summary>
    /// A list of search terms containing criteria for a given field.
    /// </summary>
    public class CustomSelection
    {
        public List<SearchTerm> SearchTerms { get; private set; }
        public LogicalOperator TermCombiningOperator { get; set; }

        public CustomSelection(DataTableBackedList<ControlRow> controlTable, LogicalOperator termCombiningOperator)
        {
            this.SearchTerms = new List<SearchTerm>();
            this.TermCombiningOperator = termCombiningOperator;

            // generate search terms for all visible controls
            foreach (ControlRow control in controlTable)
            {
                // skip hidden controls as they're not normally a part of the user experience
                // This is potentially problematic in corner cases; an option to show terms for all controls can be added if needed.
                if (control.Visible == false)
                {
                    continue;
                }

                // create search term for the control
                SearchTerm searchTerm = new SearchTerm(control);
                this.SearchTerms.Add(searchTerm);

                if (control.Type == ControlType.DateTime)
                {
                    // provide turnkey support for querying on a range of datetimes by giving the user two search terms, one configured for the start of 
                    // an interval and one for the end
                    SearchTerm dateTimeLessThanOrEqual = new SearchTerm(searchTerm)
                    {
                        Operator = Constant.SearchTermOperator.LessThanOrEqual
                    };
                    this.SearchTerms.Add(dateTimeLessThanOrEqual);
                }
            }
        }

        public CustomSelection(CustomSelection other)
        {
            this.SearchTerms = new List<SearchTerm>(other.SearchTerms.Capacity);
            this.TermCombiningOperator = other.TermCombiningOperator;

            foreach (SearchTerm searchTerm in other.SearchTerms)
            {
                this.SearchTerms.Add(new SearchTerm(searchTerm));
            }
        }

        // create and return the query formed by the search term list
        public Select CreateSelect()
        {
            Select select = new Select(Constant.DatabaseTable.FileData)
            {
                WhereCombiningOperator = this.TermCombiningOperator
            };
            foreach (SearchTerm searchTerm in this.SearchTerms.Where(term => term.UseForSearching))
            {
                select.Where.Add(searchTerm.GetWhereClause());
            }

            return select;
        }

        public override bool Equals(object obj)
        {
            if (obj is CustomSelection)
            {
                return this.Equals((CustomSelection)obj);
            }
            return false;
        }

        public bool Equals(CustomSelection other)
        {
            if (other == null)
            {
                return false;
            }
            if (Object.ReferenceEquals(this, other))
            {
                return true;
            }

            if (this.SearchTerms.Count != other.SearchTerms.Count)
            {
                return false;
            }
            if (this.TermCombiningOperator != other.TermCombiningOperator)
            {
                return false;
            }

            for (int searchTermIndex = 0; searchTermIndex < this.SearchTerms.Count; ++searchTermIndex)
            {
                if (this.SearchTerms[searchTermIndex].Equals(other.SearchTerms[searchTermIndex]) == false)
                {
                    return false;
                }
            }

            return true;
        }

        public override int GetHashCode()
        {
            int hash = this.TermCombiningOperator.GetHashCode();
            foreach (SearchTerm searchTerm in this.SearchTerms)
            {
                Utilities.CombineHashCodes(hash, searchTerm.GetHashCode());
            }
            return hash;
        }

        public void SetDateTimesAndOffset(DateTimeOffset dateTimeOffset)
        {
            foreach (SearchTerm dateTimeTerm in this.SearchTerms.Where(term => term.DataLabel == Constant.DatabaseColumn.DateTime))
            {
                dateTimeTerm.DatabaseValue = dateTimeOffset;
            }

            SearchTerm utcOffsetTerm = this.SearchTerms.FirstOrDefault(term => term.DataLabel == Constant.DatabaseColumn.UtcOffset);
            if (utcOffsetTerm != null)
            {
                utcOffsetTerm.DatabaseValue = dateTimeOffset.Offset;
            }
        }
    }
}
