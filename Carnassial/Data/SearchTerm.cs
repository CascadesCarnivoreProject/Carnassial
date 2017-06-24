using Carnassial.Database;
using Carnassial.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Carnassial.Data
{
    /// <summary>
    /// A SearchTerm stores the search criteria for a field.
    /// </summary>
    public class SearchTerm
    {
        public ControlType ControlType { get; set; }
        public object DatabaseValue { get; set; }
        public string DataLabel { get; set; }
        public string Label { get; set; }
        public List<string> List { get; set; }
        public string Operator { get; set; }
        public bool UseForSearching { get; set; }

        public SearchTerm(ControlRow control)
        {
            this.ControlType = control.Type;
            this.DataLabel = control.DataLabel;
            this.Label = control.Label;
            this.List = control.GetChoices();
            this.Operator = Constant.SearchTermOperator.Equal;
            this.UseForSearching = false;

            switch (control.Type)
            {
                case ControlType.Counter:
                    this.DatabaseValue = "0";
                    this.Operator = Constant.SearchTermOperator.GreaterThan;
                    break;
                case ControlType.DateTime:
                    // before the CustomViewSelection dialog is first opened CarnassialWindow changes the default date time to the date time of the 
                    // current file
                    this.DatabaseValue = Constant.ControlDefault.DateTimeValue;
                    this.Operator = Constant.SearchTermOperator.GreaterThanOrEqual;
                    break;
                case ControlType.Flag:
                    this.DatabaseValue = Boolean.FalseString;
                    break;
                case ControlType.FixedChoice:
                case ControlType.Note:
                    this.DatabaseValue = control.DefaultValue;
                    break;
                case ControlType.UtcOffset:
                    // the first time it's opened CustomViewSelection dialog changes this default to the offset of the current file
                    this.DatabaseValue = Constant.ControlDefault.DateTimeValue.Offset;
                    break;
                default:
                    throw new NotSupportedException(String.Format("Unhandled control type {0}.", control.Type));
            }
        }

        public SearchTerm(SearchTerm other)
        {
            this.ControlType = other.ControlType;
            this.DatabaseValue = other.DatabaseValue;
            this.DataLabel = other.DataLabel;
            this.Label = other.Label;
            if (other.List == null)
            {
                this.List = null;
            }
            else
            {
                this.List = new List<string>(other.List);
            }
            this.Operator = other.Operator;
            this.UseForSearching = other.UseForSearching;
        }

        public override bool Equals(object obj)
        {
            if (obj is SearchTerm)
            {
                return this.Equals((SearchTerm)obj);
            }
            return false;
        }

        public bool Equals(SearchTerm other)
        {
            if (other == null)
            {
                return false;
            }
            if (Object.ReferenceEquals(this, other))
            {
                return true;
            }

            if (this.ControlType != other.ControlType)
            {
                return false;
            }
            if (this.DatabaseValue != other.DatabaseValue)
            {
                return false;
            }
            if (this.DataLabel != other.DataLabel)
            {
                return false;
            }
            if (this.Label != other.Label)
            {
                return false;
            }
            // this.List is excluded as doesn't affect the query fragment the term generates
            if (this.Operator != other.Operator)
            {
                return false;
            }
            if (this.UseForSearching != other.UseForSearching)
            {
                return false;
            }

            return true;
        }

        public override int GetHashCode()
        {
            // this.List is excluded as doesn't affect the query fragment the term generates
            return Utilities.CombineHashCodes(this.ControlType, this.DatabaseValue, this.DataLabel, this.Label, this.Operator, this.UseForSearching);
        }

        public WhereClause GetWhereClause()
        {
            // convert operator from unicode to SQL expression
            string sqlOperator;
            switch (this.Operator)
            {
                case Constant.SearchTermOperator.Equal:
                    sqlOperator = Constant.SqlOperator.Equal;
                    break;
                case Constant.SearchTermOperator.Glob:
                    sqlOperator = Constant.SqlOperator.Glob;
                    break;
                case Constant.SearchTermOperator.GreaterThan:
                    sqlOperator = Constant.SqlOperator.GreaterThan;
                    break;
                case Constant.SearchTermOperator.GreaterThanOrEqual:
                    sqlOperator = Constant.SqlOperator.GreaterThanOrEqual;
                    break;
                case Constant.SearchTermOperator.LessThan:
                    sqlOperator = Constant.SqlOperator.LessThan;
                    break;
                case Constant.SearchTermOperator.LessThanOrEqual:
                    sqlOperator = Constant.SqlOperator.LessThanOrEqual;
                    break;
                case Constant.SearchTermOperator.NotEqual:
                    sqlOperator = Constant.SqlOperator.NotEqual;
                    break;
                default:
                    throw new NotSupportedException(String.Format("Unhandled search term operator '{0}'.", this.Operator));
            }

            if (this.ControlType == ControlType.DateTime)
            {
                return new WhereClause(this.DataLabel, sqlOperator, ((DateTimeOffset)this.DatabaseValue).UtcDateTime);
            }
            return new WhereClause(this.DataLabel, sqlOperator, this.DatabaseValue);
        }

        public override string ToString()
        {
            string value;
            switch (this.ControlType)
            {
                case ControlType.DateTime:
                    value = DateTimeHandler.ToDisplayDateTimeUtcOffsetString((DateTimeOffset)this.DatabaseValue);
                    break;
                case ControlType.UtcOffset:
                    value = DateTimeHandler.ToDisplayUtcOffsetString((TimeSpan)this.DatabaseValue);
                    break;
                default:
                    Debug.Assert((this.DatabaseValue == null) || (this.DatabaseValue is string), String.Format("Expected search term for '{0}' to be a string, not {1}.", this.DataLabel, this.DatabaseValue.GetType().Name));
                    value = (string)this.DatabaseValue;
                    break;
            }

            if (value.Length == 0)
            {
                value = "\"\"";  // an empty string, display it as ""
            }

            return this.DataLabel + " " + this.Operator + " " + value;
        }
    }
}
