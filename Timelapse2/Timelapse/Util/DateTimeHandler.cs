using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Timelapse.Database;

namespace Timelapse.Util
{
    public class DateTimeHandler
    {
        // The standard date format, e.g., 05-Apr-2011
        private const string DateFormat = "dd-MMM-yyyy";
        private const string TimeFormat = "HH:mm:ss";
        private const int NoYear = 1900;
        private const int NoMonth = 1;
        private const int NoDay = 1;
        private const int NoHour = 0;
        private const int NoMinute = 0;
        private const int NOSECONDS = 0;

        // To keep track of image counts and the order of dates
        private enum DateOrder : int
        {
            DayMonth = 0,
            MonthDay = 1,
            Unknown = 2
        }

        #region Static Methods: Return the date or time as a string

        /// <summary>
        /// Given a date as a string or as a DateTime, return it as a string in dd-MMM-yyyy format, e.g., 05-Apr-2011.
        /// If the date cannot be parsed, it returns 01-Jan-0001
        /// </summary>
        public static string StandardDateString(DateTime date)
        {
            return date.ToString(DateFormat);
        }

        public static string StandardDateString(string date)
        {
            DateTime dt;
            bool succeeded = DateTime.TryParse(date, out dt);

            if (succeeded)
            {
                // Debug.Print("XX " + dt.ToString(DATE_FORMAT));
                return dt.ToString(DateFormat);
            }
            else
            {
                dt = new DateTime(NoYear, NoMonth, NoDay);
                // Debug.Print("YY " + dt.ToString(DATE_FORMAT));
                return dt.ToString(DateFormat);
            }
        }

        /// <summary>
        /// Given a time as a string or as a DateTime, return it as a string in hh:mm tt format, e.g., 01:00 pm.
        /// If the time cannot be parsed, it returns 12:00 AM
        /// </summary>
        public static string StandardTimeString(DateTime time)
        {
            return time.ToString(TimeFormat);
        }

        public static string StandardTimeString(string time)
        {
            DateTime dt;
            bool succeeded = DateTime.TryParse(time, out dt);
            if (succeeded)
            {
                return dt.ToString(TimeFormat);
            }
            else
            {
                dt = new DateTime(NoYear, NoMonth, NoDay, NoHour, NoMinute, NOSECONDS);
                return dt.ToString(TimeFormat);
            }
        }
        #endregion

        #region SwapDayMonth
        /// <summary>
        /// Check to see if we can swap the day and month in all date fields. It checks to see if this is possible.
        /// If it isn't, it returns false, else true
        /// Assumes that we are showing all images (i.e., it checks the current data table)
        /// TODO: Change it to use a temp table?
        /// </summary>
        public static int SwapDayMonthIsPossible(ImageDatabase database)
        {
            // First, do a pass to see if swapping the date/time order is even possible
            for (int image = 0; image < database.CurrentlySelectedImageCount; image++)
            {
                // Skip over corrupted images for now, as we know those dates are likley wrong
                if (database.IsImageCorrupt(image))
                {
                    continue;
                }

                // Parse the date, which should always work at this point. But just in case, put out a debug message
                string dateAsString = (string)database.ImageDataTable.Rows[image][Constants.DatabaseColumn.Date] + " " + (string)database.ImageDataTable.Rows[image][Constants.DatabaseColumn.Time];
                DateTime date; // Month/Day order
                bool succeeded = DateTime.TryParse(dateAsString, out date);
                if (!succeeded)
                {
                    Debug.Print("In SwapDayMonth - something went wrong trying to parse a date!");
                }

                // Now check to see if the reversed date is legit. If it throws an exception, we know it's a problem.
                // TODO: Saul  add code to check if day and month are swappable rather than throwing
                try
                {
                    DateTime reversedDate = new DateTime(date.Year, date.Day, date.Month); // swapped day and month
                    succeeded = true;
                }
                catch
                {
                    return image; // return the first image where we couldn't swap the date
                }
                if (!succeeded)
                {
                    break;
                }
            }

            return -1; // -1 means we can reverse the dates
        }

        public static string SwapSingleDayMonth(string dateAsString)
        {
            // Parse the date, which should always work at this point. 
            DateTime date = DateTime.Parse(dateAsString);

            DateTime reversedDate = new DateTime(date.Year, date.Day, date.Month); // we have swapped the day with the month
            return DateTimeHandler.StandardDateString(reversedDate);
        }
        #endregion
    }
}
