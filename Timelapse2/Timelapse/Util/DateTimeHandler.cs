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
        /// If it isn't, it returns -1, else the index to the first image that is not swappable
        /// Assumes that we are showing all images (i.e., it checks the current data table)
        /// TODOSAUL: Change it to use a temp table?
        /// </summary>
        public static int SwapDayMonthIsPossible(ImageDatabase database)
        {
            // First, do a pass to see if swapping the date/time order is even possible
            for (int image = 0; image < database.CurrentlySelectedImageCount; image++)
            {
                ImageRow imageProperties = database.ImageDataTable[image];
                // Skip over corrupted images for now, as we know those dates are likley wrong
                if (imageProperties.ImageQuality == ImageFilter.Corrupted)
                {
                    continue;
                }

                // Now check to see if the reversed date is legit. If it throws an exception, we know it's a problem.
                // TODOSAUL: add code to check if day and month are swappable rather than throwing
                DateTime date;
                if (imageProperties.GetDateTime(out date) == false)
                {
                    return image; // Not a valid date, so its not swappable either.
                }
                try
                {
                    if (date.Day > Constants.MonthsInYear)
                    {
                        return image;
                    }
                    DateTime reversedDate = new DateTime(date.Year, date.Day, date.Month); // swapped day and month
                }
                catch (Exception exception)
                {
                    Debug.Assert(false, String.Format("Reverse of date {0} failed.", date), exception.ToString());
                    return image; // return the first image where we couldn't swap the date
                }
            }
            return -1; // -1 means we can reverse the dates
        }

        // Swap the day and month, if possible.
        // However, if the date isn't valid return the date provided
        // If the date is valid, 
        public static bool TrySwapSingleDayMonth(string dateAsString, out string swappedDateAsString)
        {
            DateTime date;
            // Make sure that we have a valid date, and that the day/month is swappable
            bool result = DateTime.TryParse(dateAsString, out date);
            if (result == true && date.Day <= 12)
            { 
                DateTime reversedDate = new DateTime(date.Year, date.Day, date.Month); // we have swapped the day with the month
                swappedDateAsString = DateTimeHandler.StandardDateString(reversedDate);
                return true;
            }
            else 
            {
               // Failure case, so we don't swap the date. Still, we return a valid date string as either the (possibly reformatted) date provided, or as 01-Jan-0001 if a bad date was provided 
                swappedDateAsString = (result == true && date.Day > 12) ? DateTimeHandler.StandardDateString(date) : DateTimeHandler.StandardDateString(new DateTime(0));  
                return false;
            }
        }
    }
}
