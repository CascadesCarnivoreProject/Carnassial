using System;
using System.Windows;

namespace Timelapse.Database
{
    /// <summary>
    /// The code in here propagates values of a control across the various images in various ways.
    /// TODO Update to make this work with the new set of controls
    /// </summary>
    public class PropagateControl
    {
        private ImageDatabase database;
        private ImageTableEnumerator imageEnumerator;

        public PropagateControl(ImageDatabase database, int startingPosition)
        {
            this.database = database;  // We need a reference to the database if we are going to update it.
            this.imageEnumerator = new ImageTableEnumerator(database, startingPosition);
        }

        /// <summary>
        /// Copy the last non-empty value  in this control preceding this image up to the current image
        /// </summary>
        public string FromLastValue(string dataLabel, bool checkForZero, bool isflag)
        {
            string valueToCopy = checkForZero ? "0" : String.Empty;

            int targetRow = -1;
            for (int index = this.imageEnumerator.CurrentRow - 1; index >= 0; index--)
            {
                // Search for the row with some value in it, starting from the previous row
                valueToCopy = (string)this.database.ImageDataTable.Rows[index][dataLabel];
                valueToCopy = valueToCopy.Trim();
                if (valueToCopy.Length > 0)
                {
                    if ((checkForZero && !valueToCopy.Equals("0"))             // Skip over non-zero values for counters
                        || (isflag && !valueToCopy.ToLower().Equals("false")) // Skip over false values for flags
                        || (!checkForZero && !isflag))
                    {
                        targetRow = index;    // We found a non-empty value
                        break;
                    }
                }
            }
            if (targetRow < 0)
            {
                // Nothing to propagate. Note that we shouldn't see this, as the menu item should be deactivated if this is the case.
                // But just in case.
                DialogMessageBox dlgMB = new DialogMessageBox();
                dlgMB.IconType = MessageBoxImage.Exclamation;
                dlgMB.ButtonType = MessageBoxButton.OK;

                dlgMB.MessageTitle = "Nothing to Propagate to Here.";
                dlgMB.MessageReason = "None of the earlier images have anything in this field, so there are no values to propagate.";
                dlgMB.ShowDialog();
                return (string)this.database.ImageDataTable.Rows[this.imageEnumerator.CurrentRow][dataLabel]; // No change, so return the current value
            }
            int number_images_affected = this.imageEnumerator.CurrentRow - targetRow;
            if (this.PropagateFromLastValue(valueToCopy, number_images_affected.ToString()) != true)
            {
                return (string)this.database.ImageDataTable.Rows[this.imageEnumerator.CurrentRow][dataLabel]; // No change, so return the current value
            }

            // Update. Note that we start on the next row, as we are copying from the current row.
            this.database.UpdateImages(dataLabel, valueToCopy, targetRow + 1, this.imageEnumerator.CurrentRow);
            return valueToCopy;
        }

        /// <summary>Copy the current value of this control to all images </summary>
        public void CopyValues(string dataLabel, bool checkForZero)
        {
            string valueToCopy = this.database.GetImageValue(this.imageEnumerator.CurrentRow, dataLabel);
            valueToCopy = valueToCopy.Trim();

            int number_images_affected = this.database.CurrentlySelectedImageCount;

            if (this.CopyCurrentValueToAll(valueToCopy, number_images_affected.ToString(), checkForZero) != true)
            {
                return;
            }
            this.database.UpdateAllImagesInFilteredView(dataLabel, valueToCopy);
        }

        /// <summary> Propagate the current value of this control forward from this point across the current set of filtered images </summary>
        public void Forward(string dataLabel, bool checkForZero)
        {
            string valueToCopy = this.database.GetImageValue(this.imageEnumerator.CurrentRow, dataLabel);
            valueToCopy = valueToCopy.Trim();

            int number_images_affected = this.database.CurrentlySelectedImageCount - this.imageEnumerator.CurrentRow - 1;

            if (number_images_affected == 0)
            {
                // Nothing to propagate. Note that we shouldn't really see this, as the menu shouldn't be highlit if we are on the last image
                // But just in case...
                DialogMessageBox dlgMB = new DialogMessageBox();
                dlgMB.IconType = MessageBoxImage.Exclamation;
                dlgMB.ButtonType = MessageBoxButton.OK;

                dlgMB.MessageTitle = "Nothing to copy forward.";
                dlgMB.MessageReason = "As you are  on the last image, there are no other images after this.";
                dlgMB.ShowDialog();
                return;
            }
            if (this.CopyForward(valueToCopy, number_images_affected.ToString(), checkForZero) != true)
            {
                return;
            }

            // Update. Note that we start on the next row, as we are copying from the current row.
            this.database.UpdateImages(dataLabel, valueToCopy, this.imageEnumerator.CurrentRow + 1, this.database.CurrentlySelectedImageCount - 1);
        }

        #region Public utilites to check if we can actually do particular operations
        // Return true if there is an actual non-empty value that we can copy
        public bool FromLastValue_IsPossible(string dataLabel, bool checkForZero)
        {
            string valueToCopy = String.Empty;
            int row = -1;
            for (int i = this.imageEnumerator.CurrentRow - 1; i >= 0; i--)
            {
                // Search for the row with some value in it, starting from the previous row
                valueToCopy = (string)this.database.ImageDataTable.Rows[i][dataLabel];

                if (valueToCopy.Trim().Length > 0)
                {
                    // TODO: Saul  fix SA1408
                    if (checkForZero && !valueToCopy.Equals("0") || !checkForZero)
                    {
                        row = i;    // We found a non-empty value
                        break;
                    }
                }
            }
            return (row >= 0) ? true : false;
        }

        public bool Forward_IsPossible(string dataLabel)
        {
            string valueToCopy = this.database.GetImageValue(this.imageEnumerator.CurrentRow, dataLabel);
            int number_images_affected = this.database.CurrentlySelectedImageCount - this.imageEnumerator.CurrentRow - 1;

            return (number_images_affected > 0) ? true : false;
        }
        #endregion

        #region Private Dialog Box Messages
        // Ask the user to confirm value propagation from the last value
        private bool? CopyForward(String text, string number, bool checkForZero)
        {
            text = text.Trim();

            DialogMessageBox dlgMB = new DialogMessageBox();
            dlgMB.IconType = MessageBoxImage.Question;
            dlgMB.ButtonType = MessageBoxButton.YesNo;
            dlgMB.MessageTitle = "Please confirm 'Copy Forward' for this field.";

            dlgMB.MessageProblem = "The Copy Forward operation is not undoable, and can overwrite existing values.";
            dlgMB.MessageResult = "If you select yes, this operation will:" + Environment.NewLine;
            if (!checkForZero && text.Equals(String.Empty))
            {
                dlgMB.MessageResult += "\u2022 copy the (empty) value \u00AB" + text + "\u00BB in this field from here to the last image of your filtered images.";
            }
            else
            {
                dlgMB.MessageResult += "\u2022 copy the value \u00AB" + text + "\u00BB in this field from here to the last image of your filtered images.";
            }
            dlgMB.MessageResult += Environment.NewLine + "\u2022 over-write any existing data values in those fields";
            dlgMB.MessageResult += Environment.NewLine + "\u2022 will affect " + number + " images.";
            return dlgMB.ShowDialog();
        }

        // Ask the user to confirm value propagation
        private bool? CopyCurrentValueToAll(String text, string number, bool checkForZero)
        {
            text = text.Trim();

            DialogMessageBox dlgMB = new DialogMessageBox();
            dlgMB.IconType = MessageBoxImage.Question;
            dlgMB.ButtonType = MessageBoxButton.YesNo;
            dlgMB.MessageTitle = "Please confirm 'Copy to All' for this field.";

            dlgMB.MessageProblem = "The Copy to All operation is not undoable, and can overwrite existing values.";
            dlgMB.MessageResult = "If you select yes, this operation will:" + Environment.NewLine;
            if (!checkForZero && text.Equals(String.Empty))
            {
                dlgMB.MessageResult += "\u2022 clear this field across all " + number + " of your filtered images.";
            }
            else
            {
                dlgMB.MessageResult += "\u2022  set this field to \u00AB" + text + "\u00BB across all " + number + " of your filtered images.";
            }
            dlgMB.MessageResult += Environment.NewLine + "\u2022 over-write any existing data values in those fields";
            return dlgMB.ShowDialog();
        }

        // Ask the user to confirm value propagation from the last value
        private bool? PropagateFromLastValue(String text, string number_images_affected)
        {
            text = text.Trim();
            DialogMessageBox dlgMB = new DialogMessageBox();
            dlgMB.IconType = MessageBoxImage.Question;
            dlgMB.ButtonType = MessageBoxButton.YesNo;
            dlgMB.MessageTitle = "Please confirm 'Propagate to Here' for this field.";

            dlgMB.MessageProblem = "The 'Propagate to Here' operation is not undoable, and can overwrite existing values.";
            dlgMB.MessageReason = "\u2022 The last non-empty value \u00AB" + text + "\u00BB was seen " + number_images_affected + " images back." + Environment.NewLine;
            dlgMB.MessageReason += "\u2022 That field's value will be copied across all images between that image and this one in this filtered image set";
            dlgMB.MessageResult = "If you select yes: " + Environment.NewLine;
            dlgMB.MessageResult = "\u2022 " + number_images_affected + " images will be affected.";
            return dlgMB.ShowDialog();
        }
        #endregion
    }
}
