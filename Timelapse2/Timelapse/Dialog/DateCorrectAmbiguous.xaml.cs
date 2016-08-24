using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Database;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Contract: the abort state should be checked by the caller. If it is true, the
    /// .Show should not be invoked.
    /// </summary>
    public partial class DateCorrectAmbiguous : Window
    {
        // Whether the operation is aborted, ie., because there are no ambiguous dates
        public bool Abort { get; set; }

        private List<AmbiguousDate> ambiguousDatesList = new List<AmbiguousDate>(); // Will contain a list of all initial images containing ambiguous dates and their state
        private int ambiguousDatesListIndex;
        private ImageDatabase database;
        private bool displayingPreview = false;

        #region Constructor and Loading
        public DateCorrectAmbiguous(ImageDatabase database)
        {
            this.InitializeComponent();
            this.database = database;

            // We add this in code behind as we don't want to invoke the radiobutton callbacks when the interface is created.
            this.cboxOriginalDate.Checked += this.DateBox_Checked;
            this.cboxSwappedDate.Checked += this.DateBox_Checked;

            // Find the ambiguous dates in the current filtered set
            if (this.FindAllAmbiguousDatesInFilteredImageSet() == true)
            {
                this.Abort = false;
                this.MoveToAmbiguousDate(null); // Go to first ambiguous date
            }
            else
            {
                this.Abort = true;
            }

            // Start displaying from the first ambiguous date.
            this.ambiguousDatesListIndex = 0;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Utilities.SetDefaultDialogPosition(this);
            Utilities.TryFitWindowInWorkingArea(this);
            // If the caller invokes Show with Abort = true (i.e., count = 0), this will at least show an empty dialog.
            this.UpdateDisplay(this.ambiguousDatesList.Count > 0);
        }
        #endregion

        #region Create the ambiguous date list
        // Create a list of all initial images containing ambiguous dates.
        // This includes calculating the start and end rows of all images matching an ambiguous date
        private bool FindAllAmbiguousDatesInFilteredImageSet()
        {
            int start = this.SearchForNextAmbiguousDateInFilteredImageSet(0);
            while (start != -1)
            {
                int count;
                int end = this.GetLastImageOnSameDay(start, out count);
                this.ambiguousDatesList.Add(new AmbiguousDate(start, end, count, false));
                start = this.SearchForNextAmbiguousDateInFilteredImageSet(end + 1);
            }
            return (this.ambiguousDatesList.Count > 0) ? true : false;
        }

        // Starting from the index, navigate successive image rows until an ambiguous date is found
        // If it can't find an ambiguous date, it will return -1.
        private int SearchForNextAmbiguousDateInFilteredImageSet(int startIndex)
        {
            for (int index = startIndex; index < this.database.CurrentlySelectedImageCount; index++)
            {
                ImageRow imageProperties = this.database.ImageDataTable[index];
                DateTime imageDateTime;
                if (imageProperties.TryGetDateTime(out imageDateTime) == false)
                {
                    continue; // if we can't get a valid DateTime, skip over this image i.e., don't consider it ambiguous as we can't alter it anyways.
                }
                if (imageDateTime.Day <= Constants.MonthsInYear)
                {
                    return index; // If the date is ambiguous, return the row index. 
                }
            }
            return -1; // -1 means all dates are unambiguous
        }

        // Given a starting index, find its date and then go through the successive images until the date differs.
        // Return the final image that is dated the same date as this image
        // Assumption is that the index is valid and is pointing to an image with a valid date.
        // However, it still tests for problems and returns -1 if there was a problem.
        private int GetLastImageOnSameDay(int startIndex, out int count)
        {
            count = 1; // We start at 1 as we have at least one image (the starting image) with this date
            int lastMatchingDate = -1;

            // Check if index is in range
            if (startIndex >= this.database.CurrentlySelectedImageCount || startIndex < 0)
            {
                return -1;   // The index is out of range.
            }

            // Parse the provided starting date. Return -1 if it cannot.
            ImageRow imageProperties = this.database.ImageDataTable[startIndex];
            DateTime desiredDateTime;
            if (imageProperties.TryGetDateTime(out desiredDateTime) == false)
            {
                return -1;  // The starting date is not a valid date
            }

            lastMatchingDate = startIndex;
            for (int index = startIndex + 1; index < this.database.CurrentlySelectedImageCount; index++)
            {
                // Parse the date for the given row.
                imageProperties = this.database.ImageDataTable[index];
                DateTime imageDateTime;
                if (imageProperties.TryGetDateTime(out imageDateTime) == false)
                {
                    // skip over invalid dates
                    continue; // if we get to an invalid date, return the prior index
                }

                if (desiredDateTime.Date == imageDateTime.Date)
                {
                    lastMatchingDate = index;
                    count++;
                    continue;
                }
                return lastMatchingDate; // This statement is reached only when the date differs, which means the last valid image is the one before it.
            }
            return lastMatchingDate; // if we got here, it means that we arrived at the end of the records
        }
        #endregion

        #region Navigate through ambiguous dates
        // return true if there is an amiguous date in the forward / backwards direction in the ambiguous date list
        private bool IsThereAnAmbiguousDate(bool directionForward)
        {
            if (directionForward == true)
            {
                return (this.ambiguousDatesListIndex + 1) < this.ambiguousDatesList.Count;
            }
            else
            {
                return directionForward == false && (this.ambiguousDatesListIndex - 1) >= 0;
            }
        }

        // From the current starting range, show the next or previous ambiguous date in the list. 
        // While it tests to ensure there is one, this really should be done before this is called
        private bool MoveToAmbiguousDate(bool? directionForward)
        {
            int index;
            if (directionForward == null)
            {
                index = this.ambiguousDatesListIndex;
            }
            else
            {
                index = (bool)directionForward ? this.ambiguousDatesListIndex + 1 : this.ambiguousDatesListIndex - 1;
            }

            // It shouldn't be out of range, but if it is, return false
            if (index > this.ambiguousDatesList.Count || index < 0)
            {
                return false;
            }

            ImageRow imageProperties;
            this.ambiguousDatesListIndex = index;

            // We found an ambiguous date; provide appropriate feedback
            imageProperties = this.database.ImageDataTable[this.ambiguousDatesList[index].StartRange];
            this.lblOriginalDate.Content = imageProperties.Date;

            // If we can't swap the date, we just return the original unaltered date. However, we expect that swapping would always work at this point.
            string swappedDate;
            this.lblNewDate.Content = DateTimeHandler.TrySwapSingleDayMonth(imageProperties.Date, out swappedDate) ? swappedDate : imageProperties.Date;

            this.lblNumberOfImagesWithSameDate.Content = this.ambiguousDatesList[this.ambiguousDatesListIndex].Count.ToString();

            // Display the image. While we expect it to be on a valid image (our assumption), we can still show a missing or corrupted file if needed
            this.imgDateImage.Source = imageProperties.LoadBitmap(this.database.FolderPath);
            this.lblImageName.Content = imageProperties.FileName;

            return true;
        }
        #endregion

        #region Feedback of state
        // Update the display
        private void UpdateDisplay(bool isAmbiguousDate)
        {
            // Enable / Disable the Next / Previous buttons as needed
            this.btnNext.IsEnabled = this.IsThereAnAmbiguousDate(true);
            this.btnPrevious.IsEnabled = this.IsThereAnAmbiguousDate(false);

            if (isAmbiguousDate)
            {
                ImageRow imageProperties;
                imageProperties = this.database.ImageDataTable[this.ambiguousDatesList[this.ambiguousDatesListIndex].StartRange];
                this.lblOriginalDate.Content = imageProperties.Date;

                // If we can't swap the date, we just return the original unaltered date. However, we expect that swapping would always work at this point.
                string swappedDate;
                this.lblNewDate.Content = DateTimeHandler.TrySwapSingleDayMonth(imageProperties.Date, out swappedDate) ? swappedDate : imageProperties.Date;

                this.lblNumberOfImagesWithSameDate.Content = this.ambiguousDatesList[this.ambiguousDatesListIndex].Count.ToString();

                // Display the image. While we expect it to be on a valid image (our assumption), we can still show a missing or corrupted file if needed
                this.imgDateImage.Source = imageProperties.LoadBitmap(this.database.FolderPath);
                this.lblImageName.Content = imageProperties.FileName;

                // Set the next button and the radio button back to their defaults
                // As we do this, unlink and then relink the callback as we don't want to invoke the data update
                this.cboxOriginalDate.Checked -= this.DateBox_Checked;
                this.cboxOriginalDate.IsChecked = !this.ambiguousDatesList[this.ambiguousDatesListIndex].Swapped;
                this.cboxSwappedDate.IsChecked = this.ambiguousDatesList[this.ambiguousDatesListIndex].Swapped;
                this.cboxOriginalDate.Checked += this.DateBox_Checked;
            }
            else
            {
                // Hide date-specific items so they are no longer visible on the screen
                this.lblOriginalDate.Visibility = Visibility.Hidden;
                this.lblNewDate.Visibility = Visibility.Hidden;

                this.cboxOriginalDate.Visibility = Visibility.Hidden;
                this.cboxSwappedDate.Visibility = Visibility.Hidden;

                this.lblImageName.Content = "--";
                this.lblNumberOfImagesWithSameDate.Content = "No ambiguous dates left";
                this.imgDateImage.Source = null;
            }
        }
        #endregion

        private void PreviewDateTimeChanges()
        {
            this.DateUpdateFeedbackCtl.ShowDifferenceColumn = false;
            this.PrimaryPanel.Visibility = Visibility.Collapsed;
            this.FeedbackPanel.Visibility = Visibility.Visible;
            foreach (AmbiguousDate ambDate in this.ambiguousDatesList)
            {
                ImageRow imageProperties;
                imageProperties = this.database.ImageDataTable[ambDate.StartRange];
                string filename = imageProperties.FileName;
                string status = ambDate.Swapped ? "Swapped: " : "Unchanged: ";
                status += ambDate.Count.ToString() + " images with same date";
                string olddate = imageProperties.Date;
                string newDate = String.Empty;

                if (ambDate.Swapped)
                {
                    DateTimeHandler.TrySwapSingleDayMonth(imageProperties.Date, out newDate);
                }
                this.DateUpdateFeedbackCtl.AddFeedbackRow(filename, status, olddate, newDate, "--");
            }
        }

        // Actually update the dates as needed
        private void ApplyDateTimeChanges()
        {
            foreach (AmbiguousDate ambDate in this.ambiguousDatesList)
            {
                ImageRow imageProperties;
                imageProperties = this.database.ImageDataTable[ambDate.StartRange];
                string newDate = String.Empty;

                if (ambDate.Swapped)
                {
                    this.database.ExchangeDayAndMonthInImageDates(ambDate.StartRange, ambDate.EndRange);
                }
            }
        }
        #region UI Callbacks
        // This handler is triggered only when the radio button state is changed. This means
        // we should swap the dates regardless of which radio button was actually pressed.
        private void DateBox_Checked(object sender, RoutedEventArgs e)
        {
            // determine if we should swap the dates or not
            RadioButton selected = sender as RadioButton;
            if (selected == this.cboxSwappedDate)
            {
                this.ambiguousDatesList[this.ambiguousDatesListIndex].Swapped = true;
            }
            else
            {
                this.ambiguousDatesList[this.ambiguousDatesListIndex].Swapped = false;
            }
        }

        // If the user click ok, then exit
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // 1st real click of Ok: Show the preview before actually making any changes.
            if (this.displayingPreview == false)
            {
                this.displayingPreview = true;
                this.PreviewDateTimeChanges();
                this.OkButton.Content = "Apply Changes";
                return;
            }
            // 2nd real click of Ok: Make the changes
            this.ApplyDateTimeChanges();
            this.DialogResult = true;
        }
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        // If the user clicks the next button, try to show the next ambiguous date.
        private void NextPreviousButton_Click(object sender, RoutedEventArgs e)
        {
            Button btnDirection = sender as Button;
            bool result = this.MoveToAmbiguousDate(btnDirection == btnNext);
            this.UpdateDisplay(result);
        }

        private void SwapAllButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (AmbiguousDate ambDate in this.ambiguousDatesList)
            {
                ambDate.Swapped = true;
            }
            this.UpdateDisplay(true);
        }
        #endregion

        #region Convenience classes
        // A class that stores various properties for each ambiguous date found
        internal class AmbiguousDate
        {
            public int StartRange { get; set; }
            public int EndRange { get; set; }
            public int Count { get; set; }

            public bool Swapped { get; set; }

            public AmbiguousDate(int startRange, int endRange, int count, bool swapped)
            {
                this.StartRange = startRange;
                this.EndRange = endRange;
                this.Swapped = swapped;
                this.Count = count;
            }
        }
        #endregion
    }
}
