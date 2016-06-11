using System;
using System.Diagnostics;
using Timelapse.Database;

namespace Timelapse.Util
{
    public class DateTimeHandler
    {
        /// <summary>
        /// Given a date as a DateTime, return it as a string in dd-MMM-yyyy format, e.g., 05-Apr-2016.
        /// </summary>
        public static string StandardDateString(DateTime date)
        {
            return date.ToString(Constants.Time.DateFormat);
        }

        /// <summary>
        /// Given a time as a DateTime return it as a string in 24 hour format.
        /// </summary>
        public static string DatabaseTimeString(DateTime time)
        {
            return time.ToString(Constants.Time.TimeFormatForDatabase);
        }

        /// <summary>
        /// Check to see if we can swap the day and month in all date fields. It checks to see if this is possible.
        /// If it isn't, it returns false, else true
        /// Assumes that we are showing all images (i.e., it checks the current data table)
        /// TODOSAUL: Change it to use a temp table?
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
                string dateAsString = database.ImageDataTable.Rows[image].GetStringField(Constants.DatabaseColumn.Date) + " " + database.ImageDataTable.Rows[image].GetStringField(Constants.DatabaseColumn.Time);
                DateTime date; // Month/Day order
                bool succeeded = DateTime.TryParse(dateAsString, out date);
                if (!succeeded)
                {
                    Debug.Print("In SwapDayMonth - something went wrong trying to parse a date!");
                }

                // Now check to see if the reversed date is legit. If it throws an exception, we know it's a problem.
                // TODOSAUL: add code to check if day and month are swappable rather than throwing
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
    }
}
