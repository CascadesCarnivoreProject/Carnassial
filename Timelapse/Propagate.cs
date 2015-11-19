using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows;

namespace Timelapse
{
    /// <summary>
    /// The code in here propogates values of a control across the various images in various ways.
    /// TODO Update to make this work with the new set of controls
    /// </summary>
    public class Propagate
    {
        DBData dbData { get; set; }
        public Propagate (DBData dbData)
        {
            this.dbData = dbData;  // We need a reference to the database if we are going to update it.
        }
        /// <summary>
        ///  Copy the last non-empty value  in this control preceding this image up to the current image
        ///  
        /// </summary>
        /// <param name="mainWindow"></param>
        /// <param name="control"></param>
        public string FromLastValue(string key, bool checkForZero, bool isflag)
        {
            string valueToCopy = (checkForZero) ? "0" : "";
            int row = -1;
            for (int i = dbData.CurrentRow - 1; i >=0; i--) // Search for the row with some value in it, starting from the previous row
            {
                valueToCopy = (string) dbData.dataTable.Rows[i][key];

                if ( valueToCopy.Trim().Length  > 0 )  
                {
                    if ( (checkForZero && !valueToCopy.Equals("0"))             // Skip over non-zero values for counters
                        || (isflag     && !valueToCopy.ToLower().Equals("false")) // Skip over false values for flags
                        || (!checkForZero && !isflag)) { 
                        row = i;    //We found a non-empty value
                        break;
                    }
                }
            }
            if (row < 0)
            {
                Messages.PropagateNothingToPropagate();
                return (string) dbData.dataTable.Rows[dbData.CurrentRow][key]; // No change, so return the current value
            }
            int number_images_affected = this.dbData.CurrentRow - row  - 1;
            if (Messages.PropagateFromLastValue(valueToCopy, number_images_affected.ToString(), (number_images_affected + 1).ToString()) == MessageBoxResult.No) 
            {
                return (string)dbData.dataTable.Rows[dbData.CurrentRow][key]; // No change, so return the current value
            };

            // Update. Note that we start on the next row, as we are copying from the current row.
            this.dbData.RowsUpdateFromRowToRowFilteredView(key, valueToCopy, row+1, this.dbData.CurrentRow);
            return (valueToCopy);
        }

        /// <summary> Copy the current value of this control to all images </summary>
        /// <param name="key"></param>
        public void CopyValues(string key, bool checkForZero)
        {
            string valueToCopy = this.dbData.RowGetValueFromDataLabel(key);
            int number_images_affected = this.dbData.dataTable.Rows.Count;
            
            if (Messages.CopyCurrentValueToAll(valueToCopy, number_images_affected.ToString(), checkForZero ) == MessageBoxResult.No) return;
            this.dbData.RowsUpdateAllFilteredView (key, valueToCopy);
        }

        /// <summary> Propagate the current value of this control forward from this point across the current set of filtered images </summary>
        /// <param name="key"></param>
        public void Forward(string key, bool checkForZero)
        {
            string valueToCopy = this.dbData.RowGetValueFromDataLabel(key);
            int number_images_affected = this.dbData.dataTable.Rows.Count - this.dbData.CurrentRow - 1;

            if (number_images_affected == 0)
            {
                Messages.PropagateNothingToCopyForward();
                return;
            }
            if (Messages.CopyForward(valueToCopy, number_images_affected.ToString(), checkForZero) == MessageBoxResult.No) return;

            // Update. Note that we start on the next row, as we are copying from the current row.
            this.dbData.RowsUpdateFromRowToRowFilteredView(key, valueToCopy, this.dbData.CurrentRow + 1, this.dbData.ImageCount - 1);
        }

        // Return true if there is an actual non-empty value that we can copy
        public bool FromLastValue_IsPossible(string key, bool checkForZero)
        {
            string valueToCopy = "";
            int row = -1;
            for (int i = dbData.CurrentRow - 1; i >= 0; i--) // Search for the row with some value in it, starting from the previous row
            {
                valueToCopy = (string)dbData.dataTable.Rows[i][key];

                if (valueToCopy.Trim().Length > 0)
                {
                    if (checkForZero && !valueToCopy.Equals("0") || !checkForZero)
                    {
                        row = i;    //We found a non-empty value
                        break;
                    }
                } 
            }
            return (row >= 0) ? true : false;
        }
        
        public bool Forward_IsPossible(string key)
        {
            string valueToCopy = this.dbData.RowGetValueFromDataLabel(key);
            int number_images_affected = this.dbData.dataTable.Rows.Count - this.dbData.CurrentRow - 1;

            return (number_images_affected > 0) ? true : false;
        }
    }
}
