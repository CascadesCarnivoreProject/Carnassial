﻿using System;
using System.Globalization;

namespace Carnassial.Util
{
    public class DateTimeHandler
    {
        public static DateTimeOffset CreateDateTimeOffset(DateTime dateTime, TimeZoneInfo imageSetTimeZone)
        {
            if (dateTime.Kind == DateTimeKind.Unspecified)
            {
                TimeSpan utcOffset = imageSetTimeZone.GetUtcOffset(dateTime);
                return new DateTimeOffset(dateTime, utcOffset);
            }
            return new DateTimeOffset(dateTime);
        }

        public static DateTimeOffset FromDatabaseDateTimeOffset(DateTime dateTime, TimeSpan utcOffset)
        {
            return new DateTimeOffset((dateTime + utcOffset).AsUnspecifed(), utcOffset);
        }

        public static TimeSpan FromDatabaseUtcOffset(double hours)
        {
            return TimeSpan.FromHours(hours);
        }

        public static DateTime ParseDatabaseDateTimeString(string dateTimeAsString)
        {
            return DateTime.ParseExact(dateTimeAsString, Constant.Time.DateTimeDatabaseFormat, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
        }

        public static TimeSpan ParseDatabaseUtcOffsetString(string utcOffsetAsString)
        {
            TimeSpan utcOffset = TimeSpan.FromHours(double.Parse(utcOffsetAsString));
            if ((utcOffset < Constant.Time.MinimumUtcOffset) ||
                (utcOffset > Constant.Time.MaximumUtcOffset))
            {
                throw new ArgumentOutOfRangeException(nameof(utcOffsetAsString), String.Format("UTC offset must be between {0} and {1}, inclusive.", DateTimeHandler.ToDatabaseUtcOffsetString(Constant.Time.MinimumUtcOffset), DateTimeHandler.ToDatabaseUtcOffsetString(Constant.Time.MinimumUtcOffset)));
            }
            if (utcOffset.Ticks % Constant.Time.UtcOffsetGranularity.Ticks != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(utcOffsetAsString), String.Format("UTC offset must be an exact multiple of {0} ({1}).", DateTimeHandler.ToDatabaseUtcOffsetString(Constant.Time.UtcOffsetGranularity), DateTimeHandler.ToDisplayUtcOffsetString(Constant.Time.UtcOffsetGranularity)));
            }
            return utcOffset;
        }

        public static DateTime ParseDisplayDateTimeString(string dateTimeAsString)
        {
            return DateTime.ParseExact(dateTimeAsString, Constant.Time.DateTimeDisplayFormat, CultureInfo.InvariantCulture);
        }

        public static string ToDatabaseDateTimeString(DateTimeOffset dateTime)
        {
            return dateTime.UtcDateTime.ToString(Constant.Time.DateTimeDatabaseFormat);
        }

        public static double ToDatabaseUtcOffset(TimeSpan timeSpan)
        {
            return timeSpan.TotalHours;
        }

        public static string ToDatabaseUtcOffsetString(TimeSpan timeSpan)
        {
            return DateTimeHandler.ToDatabaseUtcOffset(timeSpan).ToString(Constant.Time.UtcOffsetDatabaseFormat);
        }

        /// <summary>
        /// Given a date as a DateTimeOffset, return it as a string in dd-MMM-yyyy format, e.g., 05-Apr-2016, with the offset.
        /// </summary>
        public static string ToDisplayDateString(DateTimeOffset date)
        {
            return date.DateTime.ToString(Constant.Time.DateFormat);
        }

        public static string ToDisplayDateTimeString(DateTimeOffset dateTime)
        {
            return dateTime.DateTime.ToString(Constant.Time.DateTimeDisplayFormat);
        }

        public static string ToDisplayDateTimeUtcOffsetString(DateTimeOffset dateTime)
        {
            return dateTime.DateTime.ToString(Constant.Time.DateTimeDisplayFormat) + " " + DateTimeHandler.ToDisplayUtcOffsetString(dateTime.Offset);
        }

        public static string ToDisplayTimeSpanString(TimeSpan timeSpan)
        {
            // Pretty print the adjustment time, depending upon how many day(s) were included 
            string sign = (timeSpan < TimeSpan.Zero) ? "-" : null;
            string timeSpanAsString = sign + timeSpan.ToString(Constant.Time.TimeSpanDisplayFormat);

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

        public static string ToDisplayUtcOffsetString(TimeSpan utcOffset)
        {
            string displayString = utcOffset.ToString(Constant.Time.UtcOffsetDisplayFormat);
            if (utcOffset < TimeSpan.Zero)
            {
                displayString = "-" + displayString;
            }
            return displayString;
        }

        public static bool TryParseDatabaseDateTime(string dateTimeAsString, out DateTime dateTime)
        {
            return DateTime.TryParseExact(dateTimeAsString, Constant.Time.DateTimeDatabaseFormat, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out dateTime);
        }

        public static bool TryParseDatabaseDateTime(string dateTimeOffsetAsString, out DateTimeOffset dateTimeOffset)
        {
            return DateTimeOffset.TryParseExact(dateTimeOffsetAsString, Constant.Time.DateTimeDatabaseFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTimeOffset);
        }

        /// <summary>
        /// Converts a display string to a DateTime of DateTimeKind.Unspecified.
        /// </summary>
        /// <param name="dateTimeAsString">string potentially containing a date time in display format</param>
        /// <param name="dateTime">the date time in the string, if any</param>
        /// <returns>true if string was in the date time display format, false otherwise</returns>
        public static bool TryParseDisplayDateTime(string dateTimeAsString, out DateTime dateTime)
        {
            return DateTime.TryParseExact(dateTimeAsString, Constant.Time.DateTimeDisplayFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTime);
        }

        public static bool TryParseDisplayDateTime(string dateTimeAsString, out DateTimeOffset dateTimeOffset)
        {
            return DateTimeOffset.TryParseExact(dateTimeAsString, Constant.Time.DateTimeDisplayFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTimeOffset);
        }

        public static bool TryParseDatabaseUtcOffsetString(string utcOffsetAsString, out TimeSpan utcOffset)
        {
            if (double.TryParse(utcOffsetAsString, out double utcOffsetAsDouble))
            {
                utcOffset = TimeSpan.FromHours(utcOffsetAsDouble);
                return (utcOffset >= Constant.Time.MinimumUtcOffset) &&
                       (utcOffset <= Constant.Time.MaximumUtcOffset) && 
                       (utcOffset.Ticks % Constant.Time.UtcOffsetGranularity.Ticks == 0);
            }

            utcOffset = TimeSpan.Zero;
            return false;
        }

        public static bool TryParseDateTaken(string dateTimeAsString, TimeZoneInfo imageSetTimeZone, out DateTimeOffset dateTimeOffset)
        {
            // use current culture as BitmapMetadata.DateTaken is not invariant
            if (DateTime.TryParse(dateTimeAsString, CultureInfo.CurrentCulture, DateTimeStyles.None, out DateTime dateTime) == false)
            {
                dateTimeOffset = DateTimeOffset.MinValue;
                return false;
            }

            dateTimeOffset = DateTimeHandler.CreateDateTimeOffset(dateTime, imageSetTimeZone);
            return true;
        }

        public static bool TryParseMetadataDateTaken(string dateTimeAsString, TimeZoneInfo imageSetTimeZone, out DateTimeOffset dateTimeOffset)
        {
            if (DateTime.TryParseExact(dateTimeAsString, Constant.Time.DateTimeMetadataFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dateTime) == false)
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
            if (imageDate.Day > Constant.Time.MonthsInYear)
            {
                return false;
            }
            swappedDate = new DateTimeOffset(imageDate.Year, imageDate.Day, imageDate.Month, imageDate.Hour, imageDate.Minute, imageDate.Second, imageDate.Millisecond, imageDate.Offset);
            return true;
        }
    }
}
