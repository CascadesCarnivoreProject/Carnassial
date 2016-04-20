using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Timelapse.Database;
using Timelapse.Util;

namespace Timelapse
{
    /// <summary>
    /// Interaction logic for DialogCheckModifyAmbiguousDates.xaml
    /// </summary>
    public partial class DialogDateModifyAmbiguousDates : Window
    {
        private ImageDatabase database;
        private int rangeStart = 0;
        private int rangeEnd = -1;

        public DialogDateModifyAmbiguousDates(ImageDatabase database)
        {
            this.InitializeComponent();
            this.database = database;

            // We add this in code behind as we don't want to invoke these callbacks when the interface is created.
            // TODO: Saul  both combo boxes route to the same handler which doesn't vary its action depending on the sending control; is this a bug?
            this.cboxOriginalDate.Checked += this.DateBox_Checked;
            this.cboxNewDate.Checked += this.DateBox_Checked;

            // Find the first ambiguous date
            this.NextAmbiguousDate();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Make sure the title bar of the dialog box is on the screen. For small screens it may default to being off the screen
            if (this.Left < 10 || this.Top < 10)
            {
                this.Left = this.Owner.Left + (this.Owner.Width - this.ActualWidth) / 2; // Center it horizontally
                this.Top = this.Owner.Top + 20; // Offset it from the windows'top by 20 pixels downwards
            }
        }

        private bool NextAmbiguousDate()
        {
            // The search will search inclusively from the given image number, and will return the first image number that is ambiguous, else -1
            this.rangeStart = this.GetNextAmbiguousDate(this.rangeStart);
            ImageProperties imageProperties = null;
            if (this.rangeStart >= 0)
            {
                // We found an ambiguous date; provide appropriate feedback
                int id = this.database.GetImageID(this.rangeStart);
                if (id >= 0)
                {
                    imageProperties = new ImageProperties(this.database.ImageDataTable.Rows.Find(id));
                    this.lblOriginalDate.Content = imageProperties.Date;
                    this.lblNewDate.Content = DateTimeHandler.SwapSingleDayMonth(imageProperties.Date);

                    this.rangeEnd = this.GetDateRangeWithSameDate(this.rangeStart);
                    this.lblNumberOfImagesWithSameDate.Content = " Images from the same day: ";
                    this.lblNumberOfImagesWithSameDate.Content += (this.rangeEnd - this.rangeStart + 1).ToString();
                }
            }
            else
            {
                // No dates are ambiguous; provide appropriate feedback
                this.btnNext.IsEnabled = false; // Disable the 'Next' button

                // Hide date-specific items so they are no longer visible on the screen
                this.lblOriginalDate.Visibility = Visibility.Hidden;
                this.lblNewDate.Visibility = Visibility.Hidden;
                this.cboxOriginalDate.Visibility = Visibility.Hidden;
                this.cboxNewDate.Visibility = Visibility.Hidden;
                this.lblImageName.Visibility = Visibility.Hidden;
                this.spImageArea.Visibility = Visibility.Hidden;
                this.label2.Visibility = Visibility.Hidden;
                this.label3.Visibility = Visibility.Hidden;
                this.lblNumberOfImagesWithSameDate.Content = "No ambiguous dates left";
                this.imgDateImage.Source = null;
            }

            // No valid image to show!
            if (imageProperties == null)
            {
                return false;
            }

            // Display the image. While we should be on a valid image (our assumption), we can still show a missing or corrupted image if needed
            this.imgDateImage.Source = imageProperties.LoadImage(this.database.FolderPath);
            this.lblImageName.Content = imageProperties.FileName;
            return true;
        }

        #region Callbacks
        private void DateBox_Checked(object sender, RoutedEventArgs e)
        {
            if (sender == this.cboxNewDate)
            {
                this.database.ExchangeDayAndMonthInImageDate(this.rangeStart, this.rangeEnd);
            }
            else
            {
                this.database.ExchangeDayAndMonthInImageDate(this.rangeStart, this.rangeEnd);
            }
        }

        // If the user click ok, then exit
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // this.database.RowsUpdateSwapDayMonth();
            this.DialogResult = true;

            // Refresh the database / datatable to reflect the updated values, which will also refressh the main timelpase display.
            int currentImage = this.database.CurrentImageRow;
            this.database.TryGetImagesAll();
            this.database.TryMoveToImage(currentImage);
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            this.rangeStart = this.rangeEnd + 1; // Go to the next image
            this.NextAmbiguousDate();

            // Set the radio button back to the orginal date (default)
            // As we do this, unlink and then relink the callback as we don't want to invoke the data update
            this.cboxOriginalDate.Checked -= this.DateBox_Checked;
            this.cboxOriginalDate.IsChecked = true;
            this.cboxOriginalDate.Checked += this.DateBox_Checked;
        }
        #endregion

        private int GetNextAmbiguousDate(int startIndex)
        {
            // Starting from the index, get the date from successive rows and see if the date is ambiguous
            // Note that if the index is out of range, it will return -1, so that's ok.
            for (int index = startIndex; index < this.database.ImageCount; index++)
            {
                // Ignore corrupted images
                // if (this.database.RowIsImageCorrupted(i)) continue;

                // Parse the date. Note that this should never fail at this point, but just in case, put out a debug message
                string dateAsString = (string)this.database.ImageDataTable.Rows[index][Constants.DatabaseColumn.Date] + " " + (string)this.database.ImageDataTable.Rows[index][Constants.DatabaseColumn.Time];
                DateTime date;
                bool succeeded = DateTime.TryParse(dateAsString, out date);
                if (succeeded)
                {
                    if (date.Day < 13 && date.Month < 13)
                    {
                        return index; // If the date is ambiguous, return the row index. 
                    }
                }
                else
                {
                    Debug.Print("In SwapDayMonth - something went wrong trying to parse a date!");
                }
            }
            return -1; // -1 means all dates are fine
        }

        // Given a starting index, find its date and then go through the successive images untilthe date differs.
        // That is, return the final image that is dated the same date as this image
        private int GetDateRangeWithSameDate(int startIndex)
        {
            if (startIndex >= this.database.ImageCount)
            {
                return -1;   // Make sure index is in range.
            }

            // Parse the provided starting date. Note that this should never fail at this point, but just in case, put out a debug message
            string startingDateAsString = (string)this.database.ImageDataTable.Rows[startIndex][Constants.DatabaseColumn.Date] + " " + (string)this.database.ImageDataTable.Rows[startIndex][Constants.DatabaseColumn.Time];
            DateTime startingDate;
            if (!DateTime.TryParse(startingDateAsString, out startingDate))
            {
                return -1; // Should never fail, but just in case.
            }

            for (int index = startIndex + 1; index < this.database.ImageCount; index++)
            {
                // Parse the date for the given record.
                string currentDateAsString = (string)this.database.ImageDataTable.Rows[index][Constants.DatabaseColumn.Date] + " " + (string)this.database.ImageDataTable.Rows[index][Constants.DatabaseColumn.Time];
                DateTime currentDate;
                if (!DateTime.TryParse(currentDateAsString, out currentDate))
                {
                    return index - 1; // If we can't parse the date, the return the previous date 
                }
                if (startingDate.Day == currentDate.Day && startingDate.Month == currentDate.Month && startingDate.Year == currentDate.Year)
                {
                    continue;
                }
                else
                {
                    return index - 1;
                }
            }
            return this.database.ImageCount - 1; // if we got here, it means that we arrived at the end of the records
        }
    }
}
