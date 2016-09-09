using System;
using System.Globalization;

namespace Timelapse.Util
{
    public class DateTimeHandler
    {
        public static DateTimeOffset FromDatabaseDateTimeOffset(DateTime dateTime, TimeSpan utcOffset)
        {
            return new DateTimeOffset((dateTime + utcOffset).AsUnspecifed(), utcOffset);
        }

        public static DateTime ParseDatabaseDateTimeString(string dateTimeAsString)
        {
            return DateTime.ParseExact(dateTimeAsString, Constants.Time.DateTimeDatabaseFormat, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
        }

        public static TimeSpan ParseDatabaseUtcOffsetString(string utcOffsetAsString)
        {
            TimeSpan utcOffset = TimeSpan.FromHours(double.Parse(utcOffsetAsString));
            if ((utcOffset < Constants.Time.MinimumUtcOffset) ||
                (utcOffset > Constants.Time.MaximumUtcOffset))
            {
                throw new ArgumentOutOfRangeException("utcOffsetAsString", String.Format("UTC offset must be between {0} and {1}, inclusive.", DateTimeHandler.ToDatabaseUtcOffsetString(Constants.Time.MinimumUtcOffset), DateTimeHandler.ToDatabaseUtcOffsetString(Constants.Time.MinimumUtcOffset)));
            }
            if (utcOffset.Ticks % Constants.Time.UtcOffsetGranularity.Ticks != 0)
            {
                throw new ArgumentOutOfRangeException("utcOffsetAsString", String.Format("UTC offset must be an exact multiple of {0} ({1}).", DateTimeHandler.ToDatabaseUtcOffsetString(Constants.Time.UtcOffsetGranularity), DateTimeHandler.ToDisplayUtcOffsetString(Constants.Time.UtcOffsetGranularity)));
            }
            return utcOffset;
        }

        public static DateTime ParseDisplayDateTimeString(string dateTimeAsString)
        {
            return DateTime.ParseExact(dateTimeAsString, Constants.Time.DateTimeDisplayFormat, CultureInfo.InvariantCulture);
        }

        public static string ToDatabaseDateTimeString(DateTimeOffset dateTime)
        {
            return dateTime.UtcDateTime.ToString(Constants.Time.DateTimeDatabaseFormat);
        }

        public static string ToDatabaseUtcOffsetString(TimeSpan timeSpan)
        {
            return timeSpan.TotalHours.ToString(Constants.Time.UtcOffsetDatabaseFormat);
        }

        /// <summary>
        /// Given a date as a DateTimeOffset, return it as a string in dd-MMM-yyyy format, e.g., 05-Apr-2016, with the offset.
        /// </summary>
        public static string ToDisplayDateString(DateTimeOffset date)
        {
            return date.DateTime.ToString(Constants.Time.DateFormat);
        }

        public static string ToDisplayDateTimeString(DateTimeOffset dateTime)
        {
            return dateTime.DateTime.ToString(Constants.Time.DateTimeDisplayFormat);
        }

        public static string ToDisplayDateTimeUtcOffsetString(DateTimeOffset dateTime)
        {
            return dateTime.DateTime.ToString(Constants.Time.DateTimeDisplayFormat) + " " + DateTimeHandler.ToDisplayUtcOffsetString(dateTime.Offset);
        }

        public static string ToDisplayTimeSpanString(TimeSpan timeSpan)
        {
            // Pretty print the adjustment time, depending upon how many day(s) were included 
            string sign = (timeSpan < TimeSpan.Zero) ? "-" : null;
            string timeSpanAsString = sign + timeSpan.ToString(Constants.Time.TimeSpanDisplayFormat);

            TimeSpan duration = timeSpan.Duration();
            if (duration.Days == 0)
            {
                return timeSpanAsString;
            }
            if (duration.Days == 1)
            {
                return sign + "1 day " + timeSpanAsString;
            }

            return sign + duration.Days.ToString("D") + " days " + timeSpanAsString;
        }

        /// <summary>
        /// Given a time as a DateTimeOffset return it as a string in 24 hour forma with the offset.
        /// </summary>
        public static string ToDisplayTimeString(DateTimeOffset time)
        {
            return time.DateTime.ToString(Constants.Time.TimeFormat);
        }

        public static string ToDisplayUtcOffsetString(TimeSpan utcOffset)
        {
            string displayString = utcOffset.ToString(Constants.Time.UtcOffsetDisplayFormat);
            if (utcOffset < TimeSpan.Zero)
            {
                displayString = "-" + displayString;
            }
            return displayString;
        }

        public static bool TryParseDatabaseDateTime(string dateTimeAsString, out DateTime dateTime)
        {
            return DateTime.TryParseExact(dateTimeAsString, Constants.Time.DateTimeDatabaseFormat, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out dateTime);
        }

        /// <summary>
        /// Converts a display string to a DateTime of DateTimeKind.Unspecified.
        /// </summary>
        /// <param name="dateTimeAsString">string potentially containing a date time in display format</param>
        /// <param name="dateTime">the date time in the string, if any</param>
        /// <returns>true if string was in the date time display format, false otherwise</returns>
        public static bool TryParseDisplayDateTime(string dateTimeAsString, out DateTime dateTime)
        {
            return DateTime.TryParseExact(dateTimeAsString, Constants.Time.DateTimeDisplayFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTime);
        }

        public static bool TryParseDatabaseUtcOffsetString(string utcOffsetAsString, out TimeSpan utcOffset)
        {
            double utcOffsetAsDouble;
            if (double.TryParse(utcOffsetAsString, out utcOffsetAsDouble))
            {
                utcOffset = TimeSpan.FromHours(utcOffsetAsDouble);
                return (utcOffset >= Constants.Time.MinimumUtcOffset) &&
                       (utcOffset <= Constants.Time.MaximumUtcOffset) && 
                       (utcOffset.Ticks % Constants.Time.UtcOffsetGranularity.Ticks == 0);
            }

            utcOffset = TimeSpan.Zero;
            return false;
        }

        public static bool TryParseLegacyDateTime(string date, string time, TimeZoneInfo imageSetTimeZone, out DateTimeOffset dateTimeOffset)
        {
            return DateTimeHandler.TryParseDateTaken(date + " " + time, imageSetTimeZone, out dateTimeOffset);
        }

        public static bool TryParseDateTaken(string dateTimeAsString, TimeZoneInfo imageSetTimeZone, out DateTimeOffset dateTimeOffset)
        {
            // use current culture as BitmapMetadata.DateTaken is not invariant
            DateTime dateTime;
            if (DateTime.TryParse(dateTimeAsString, CultureInfo.CurrentCulture, DateTimeStyles.None, out dateTime) == false)
            {
                dateTimeOffset = DateTimeOffset.MinValue;
                return false;
            }

            dateTimeOffset = DateTimeHandler.CreateDateTimeOffset(dateTime, imageSetTimeZone);
            return true;
        }

        public static bool TryParseMetadataDateTaken(string dateTimeAsString, TimeZoneInfo imageSetTimeZone, out DateTimeOffset dateTimeOffset)
        {
            DateTime dateTime;
            if (DateTime.TryParseExact(dateTimeAsString, Constants.Time.DateTimeMetadataFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTime) == false)
            {
                dateTimeOffset = DateTimeOffset.MinValue;
                return false;
            }

            dateTimeOffset = DateTimeHandler.CreateDateTimeOffset(dateTime, imageSetTimeZone);
            return true;
        }

        // Swap the day and month, if possible.
        // However, if the date isn't valid return the date provided
        // If the date is valid, 
        public static bool TrySwapDayMonth(DateTimeOffset imageDate, out DateTimeOffset swappedDate)
        {
            swappedDate = DateTimeOffset.MinValue;
            if (imageDate.Day > Constants.Time.MonthsInYear)
            {
                return false;
            }
            swappedDate = new DateTimeOffset(imageDate.Year, imageDate.Day, imageDate.Month, imageDate.Hour, imageDate.Minute, imageDate.Second, imageDate.Millisecond, imageDate.Offset);
            return true;
        }

        private static DateTimeOffset CreateDateTimeOffset(DateTime dateTime, TimeZoneInfo imageSetTimeZone)
        {
            if (dateTime.Kind == DateTimeKind.Unspecified)
            {
                TimeSpan utcOffset = imageSetTimeZone.GetUtcOffset(dateTime);
                return new DateTimeOffset(dateTime, utcOffset);
            }
            return new DateTimeOffset(dateTime);
        }
    }
}
