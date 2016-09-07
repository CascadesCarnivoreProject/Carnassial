using System;
using Timelapse.Util;

namespace Timelapse.Database
{
    /// <summary>
    /// A SearchTerm stores the search criteria for each column
    /// </summary>
    public class SearchTerm
    {
        public string DataLabel { get; set; }
        public string Label { get; set; }
        public string List { get; set; }
        public string Operator { get; set; }
        public string Type { get; set; }
        public bool UseForSearching { get; set; }
        public string DatabaseValue { get; set; }

        public SearchTerm()
        {
            this.DataLabel = String.Empty;
            this.Label = String.Empty;
            this.Operator = String.Empty;
            this.Type = String.Empty;
            this.UseForSearching = false;
            this.DatabaseValue = String.Empty;
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
