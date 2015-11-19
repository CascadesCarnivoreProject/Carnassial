using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Windows;
using System.Diagnostics;

namespace Timelapse
{

    public class DateTimeHandler
    {
        // The standard date format, e.g., 05-Apr-2011
        private const string DATE_FORMAT = "dd-MMM-yyyy";
        private const string TIME_FORMAT = "HH:mm:ss";
        private const int NOYEAR = 1900;
        private const int NOMONTH = 1;
        private const int NODAY = 1;
        private const int NOHOUR = 0;
        private const int NOMINUTE = 0;
        private const int NOSECONDS = 0;

        #region Public Static Methods

        public static bool VerifyAndUpdateDates(List<ImageProperties> imgprop_list)
        {
            DateTime dtDate = new DateTime(); // the DateTime date that we will get
            bool result = false;
            bool ambiguous_date_order = true;

            // The invariant culture tends to handle a broad variety of date formats. Hopefully this will work correctly to differentiate
            // all the different formats used by cameras, including ambiguities in month/day vs day/month orders.
            DateTimeStyles styles;
            CultureInfo invariantCulture;
            invariantCulture = CultureInfo.CreateSpecificCulture("");       
            styles = DateTimeStyles.None;

            foreach (ImageProperties imgprop in imgprop_list)
            {
                // We ideally want to use the metadata date. 
                // If the image is corrupted we fall back to the file date
                imgprop.UseMetadata = (imgprop.ImageQuality == (int)Constants.ImageQualityFilters.Corrupted) ? false : true;

                if (imgprop.UseMetadata)
                {
                    // Try to parse the date in both day/month and month/day order. 
                    result = DateTime.TryParse(imgprop.DateMetadata, invariantCulture, styles, out dtDate);
                    if (true == result)
                    {
                        imgprop.FinalDate = DateTimeHandler.StandardDateString(dtDate);
                        imgprop.FinalTime = DateTimeHandler.StandardTimeString(dtDate);
                        if (dtDate.Day > 12) ambiguous_date_order = false;
                    }
                    else
                    {
                        // We can't read in the metadata date correctly, so use the file creation date instead
                        imgprop.FinalDate = DateTimeHandler.StandardDateString(imgprop.DateFileCreation);
                        imgprop.FinalTime = DateTimeHandler.StandardTimeString(imgprop.DateFileCreation);
                        imgprop.UseMetadata = false;
                        if (dtDate.Day > 12) ambiguous_date_order = false;
                    }
                }
                else
                {
                    imgprop.FinalDate = DateTimeHandler.StandardDateString(imgprop.DateFileCreation);
                    imgprop.FinalTime = DateTimeHandler.StandardTimeString(imgprop.DateFileCreation);
                    if (dtDate.Day > 12) ambiguous_date_order = false;
                }
                imgprop.DateOrder = (int)Constants.DateOrder.DayMonth; // REMOVE THIS WHEN WE FIGURE THINGS OUT
            }
            return ambiguous_date_order;
        }
        #endregion

        #region Static Methods: Return the date or time as a string

        /// <summary>
        /// Given a date as a string or as a DateTime, return it as a string in dd-MMM-yyyy format, e.g., 05-Apr-2011.
        /// If the date cannot be parsed, it returns 01-Jan-0001
        /// </summary>
        /// <param name="date"></param>
        /// <returns></returns>
        static public string StandardDateString(DateTime date)
        {
            return date.ToString(DATE_FORMAT);
        }

        static public string StandardDateString(string date)
        {
            DateTime dt;
            bool succeeded = DateTime.TryParse(date, out dt);

            if (succeeded)
            {
                //Debug.Print("XX " + dt.ToString(DATE_FORMAT));
                return dt.ToString(DATE_FORMAT);

            }
            else
            {
                dt = new DateTime(NOYEAR, NOMONTH, NODAY);
                //Debug.Print("YY " + dt.ToString(DATE_FORMAT));
                return dt.ToString(DATE_FORMAT);
            }
        }

        /// <summary>
        /// Given a time as a string or as a DateTime, return it as a string in hh:mm tt format, e.g., 01:00 pm.
        /// If the time cannot be parsed, it returns 12:00 AM
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        static public string StandardTimeString(DateTime time)
        {
            return time.ToString(TIME_FORMAT);
        }

        static public string StandardTimeString(string time)
        {
            DateTime dt;
            bool succeeded = DateTime.TryParse(time, out dt);
            if (succeeded)
            {
                return dt.ToString(TIME_FORMAT);
            }
            else
            {
                dt = new DateTime(NOYEAR, NOMONTH, NODAY, NOHOUR, NOMINUTE, NOSECONDS);
                return dt.ToString(TIME_FORMAT);
            }
        }
        #endregion

        #region SwapDayMonth
        /// <summary>
        /// Check to see if we can swap the day and month in all date fields. It checks to see if this is possible.
        /// If it isn't, it returns false, else true
        /// Assumes that we are showing all images (i.e., it checks the current datatable)
        /// TODO: Change it to use a temp table?
        /// </summary>
        /// <param name="main"></param>
        /// 
        static public int SwapDayMonthIsPossible(DBData dbData)
        {
            DateTime date; //Month/Day order
            DateTime reversedDate;
            bool succeeded = true;
            string sdate ;

            // First, do a pass to see if swapping the date/time order is even possible
            for (int i = 0; i < dbData.dataTable.Rows.Count; i++)
            {
               // Skip over corrupted images for now, as we know those dates are likley wrong
               if (dbData.RowIsImageCorrupted(i)) continue;

               // Parse the date, which should always work at this point. But just in case, put out a debug message
               sdate = (string) dbData.dataTable.Rows[i][Constants.DATE] + " " + (string) dbData.dataTable.Rows[i][Constants.TIME];
               succeeded = DateTime.TryParse(sdate, out date);
               if (!succeeded) Debug.Print("In SwapDayMonth - something went wrong trying to parse a date!");

               // Now check to see if the reversed date is legit. If it throws an exception, we know its a problem so get out of here.
               try
               {
                   reversedDate = new DateTime(date.Year, date.Day, date.Month); // we have swapped the day with the month
                   succeeded = true;
               }
               catch
               {
                   return (i); // Return the first image where we couldn't swap the date
               }
               if (!succeeded) break;
           }

            return -1; //-1 means we can reverse the dates
        }

        #endregion

        static public string SwapSingleDayMonth(string date)
        {
            // Parse the date, which should always work at this point. 
            DateTime dtDate = DateTime.Parse(date);

            DateTime reversedDate = new DateTime(dtDate.Year, dtDate.Day, dtDate.Month); // we have swapped the day with the month
            return (DateTimeHandler.StandardDateString(reversedDate));
        }

    }
}

