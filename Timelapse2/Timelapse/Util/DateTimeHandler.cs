using System;
using System.Globalization;

namespace Timelapse.Util
{
    public class DateTimeHandler
    {
        public static bool TryFromStandardDateString(string dateTimeAsString, out DateTime dateAsDateTime)
        {
            return DateTime.TryParseExact(dateTimeAsString, Constants.Time.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateAsDateTime);
        }

        public static DateTime FromStandardDateString(string dateTimeAsString)
        {
            return DateTime.ParseExact(dateTimeAsString, Constants.Time.DateFormat, CultureInfo.InvariantCulture);
        }

        public static DateTime FromStandardDateTimeString(string dateTimeAsString)
        {
            return DateTime.ParseExact(dateTimeAsString, Constants.Time.DateTimeFormat, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Given a date as a DateTime, return it as a string in dd-MMM-yyyy format, e.g., 05-Apr-2016.
        /// </summary>
        public static string ToStandardDateString(DateTime date)
        {
            return date.ToString(Constants.Time.DateFormat);
        }

        public static string ToStandardDateTimeString(DateTime dateTime)
        {
            return dateTime.ToString(Constants.Time.DateTimeFormat);
        }

        /// <summary>
        /// Given a time as a DateTime return it as a string in 24 hour format.
        /// </summary>
        public static string ToStandardTimeString(DateTime time)
        {
            return time.ToString(Constants.Time.TimeFormat);
        }

        // Swap the day and month, if possible.
        // However, if the date isn't valid return the date provided
        // If the date is valid, 
        public static bool TrySwapDayMonth(string dateAsString, out string swappedDateAsString)
        {
            DateTime date;
            // Make sure that we have a valid date, and that the day/month is swappable
            bool result = DateTime.TryParse(dateAsString, out date);
            if (result == true && date.Day <= 12)
            { 
                DateTime reversedDate = new DateTime(date.Year, date.Day, date.Month); // we have swapped the day with the month
                swappedDateAsString = DateTimeHandler.ToStandardDateString(reversedDate);
                return true;
            }
            else 
            {
               // Failure case, so we don't swap the date. Still, we return a valid date string as either the (possibly reformatted) date provided, or as 01-Jan-0001 if a bad date was provided 
                swappedDateAsString = (result == true && date.Day > 12) ? DateTimeHandler.ToStandardDateString(date) : DateTimeHandler.ToStandardDateString(new DateTime(0));  
                return false;
            }
        }
    }
}
