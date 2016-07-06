using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using Timelapse.Database;

namespace Timelapse.Util
{
    public class DateTimeHandler
    {
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

        public static void ShowDateTimeParseFailureDialog(ImageRow image, Window owner)
        {
            Debug.Assert(false, String.Format("Parse of '{0} {1}' failed.", image.Date, image.Time));
            DialogMessageBox messageBox = new DialogMessageBox("Timelapse could not read the date / time.", owner);
            messageBox.Message.Problem = String.Format("Timelapse could not read the date and time '{0} {1}'", image.Date, image.Time);
            messageBox.Message.Reason = "The date / time needs to be in a very specific format, for example, 01-Jan-2016 13:00:00.";
            messageBox.Message.Solution = "Re-read in the dates from the images (see the Edit/Dates menu), and then try this again.";
            messageBox.Message.Result = "Timelapse won't do anything for now.";
            messageBox.Message.Icon = MessageBoxImage.Error;
            messageBox.ShowDialog();
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
                if (imageProperties.TryGetDateTime(out date) == false)
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
