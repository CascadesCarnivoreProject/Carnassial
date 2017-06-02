using Carnassial.Util;
using System;
using System.Collections.Generic;

namespace Carnassial.Data
{
    /// <summary>
    /// A SearchTerm stores the search criteria for a field.
    /// </summary>
    public class SearchTerm
    {
        public ControlType ControlType { get; set; }
        public string DatabaseValue { get; set; }
        public string DataLabel { get; set; }
        public string Label { get; set; }
        public List<string> List { get; set; }
        public string Operator { get; set; }
        public bool UseForSearching { get; set; }

        public SearchTerm()
        {
            this.ControlType = default(ControlType);
            this.DatabaseValue = String.Empty;
            this.DataLabel = String.Empty;
            this.Label = String.Empty;
            this.List = null;
            this.Operator = String.Empty;
            this.UseForSearching = false;
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

        public DateTime GetDateTime()
        {
            if (this.DataLabel != Constant.DatabaseColumn.DateTime)
            {
                throw new NotSupportedException(String.Format("Attempt to retrieve date/time from a SearchTerm with data label {0}.", this.DataLabel));
            }
            return DateTimeHandler.ParseDatabaseDateTimeString(this.DatabaseValue);
        }

        public override int GetHashCode()
        {
            // this.List is excluded as doesn't affect the query fragment the term generates
            return Utilities.CombineHashCodes(this.ControlType, this.DatabaseValue, this.DataLabel, this.Label, this.Operator, this.UseForSearching);
        }

        public TimeSpan GetUtcOffset()
        {
            if (this.DataLabel != Constant.DatabaseColumn.UtcOffset)
            {
                throw new NotSupportedException(String.Format("Attempt to retrieve UTC offset from a SearchTerm with data label {0}.", this.DataLabel));
            }
            return DateTimeHandler.ParseDatabaseUtcOffsetString(this.DatabaseValue);
        }

        public void SetDatabaseValue(DateTimeOffset dateTime)
        {
            if (this.DataLabel != Constant.DatabaseColumn.DateTime)
            {
                throw new NotSupportedException(String.Format("Attempt to retrieve date/time from a SearchTerm with data label {0}.", this.DataLabel));
            }
            this.DatabaseValue = DateTimeHandler.ToDatabaseDateTimeString(dateTime);
        }

        public void SetDatabaseValue(Nullable<DateTime> dateTime, TimeZoneInfo imageSetTimeZone)
        {
            if (dateTime.HasValue)
            {
                TimeSpan utcOffset = imageSetTimeZone.GetUtcOffset(dateTime.Value);
                DateTimeOffset imageSetDateTime = DateTimeHandler.FromDatabaseDateTimeOffset(dateTime.Value, utcOffset);
                this.DatabaseValue = DateTimeHandler.ToDatabaseDateTimeString(imageSetDateTime);
            }
            else
            {
                this.DatabaseValue = null;
            }
        }

        public void SetDatabaseValue(Nullable<TimeSpan> utcOffset)
        {
            if (utcOffset.HasValue)
            {
                this.DatabaseValue = DateTimeHandler.ToDatabaseUtcOffsetString(utcOffset.Value);
            }
            else
            {
                this.DatabaseValue = null;
            }
        }
    }
}
