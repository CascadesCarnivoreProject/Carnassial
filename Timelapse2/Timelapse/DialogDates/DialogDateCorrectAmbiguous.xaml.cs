using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Database;
using Timelapse.Util;

namespace Timelapse.DialogDates
{
    /// <summary>
    /// Interaction logic for DialogDateCorrectAmbiguous.xaml
    /// </summary>
    public partial class DialogDateCorrectAmbiguous : Window
    {
        private ImageDatabase database;
        private int rangeStart = 0;
        private int rangeEnd = -1;
        private bool swapTheDates = false;
        
        private List<AmbiguousDate> ambiguousDatesList = new List<AmbiguousDate>(); // Will contain a list of all initial images containing ambiguous dates and their state
        private int ambiguousDatesListIndex = 0;

        #region Constructor and Loading
        public DialogDateCorrectAmbiguous(ImageDatabase database)
        {
            this.InitializeComponent();
            this.database = database;
            bool result = FindAllAmbiguousDates();
            // TODO: IF RESULT IS 0, SAY NO DATES


            // We add this in code behind as we don't want to invoke the radiobutton callbacks when the interface is created.
            this.cboxOriginalDate.Checked += this.DateBox_Checked;
            this.cboxSwapTheDates.Checked += this.DateBox_Checked;

            // Start displaying from the first ambiguous date.
            this.ambiguousDatesListIndex = 0;

            // Find the first ambiguous date
            this.TryShowNextAmbiguousDate();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Utilities.SetDefaultDialogPosition(this);
            Utilities.TryFitWindowInWorkingArea(this);
        }
        #endregion

        // Create a list of all initial images containing ambiguous dates and their state
        private bool FindAllAmbiguousDates ()
        {
            int start = 0;
            int end = 0;
            start = this.GetNextAmbiguousDate(start);
            while ( start != -1)
            { 
                end = this.GetLastImageOnSameDay(start);
                this.ambiguousDatesList.Add(new AmbiguousDate(start, end, false));
                start = this.GetNextAmbiguousDate(end + 1);
            }
            return (ambiguousDatesList.Count > 0) ? true : false;
        }


        // From the current starting range, show the next ambiguous date in the interface, if any are left. 
        private bool TryShowNextAmbiguousDate()
        {
            // The search will search inclusively from the given image number, and will return the first image number that is ambiguous, else -1
            if ( (this.ambiguousDatesListIndex + 1) < this.ambiguousDatesList.Count)
            {
                this.ambiguousDatesListIndex++;
                this.rangeStart = this.ambiguousDatesList[this.ambiguousDatesListIndex].StartRange;
                this.rangeEnd = this.ambiguousDatesList[this.ambiguousDatesListIndex].EndRange;
                ImageRow imageProperties;

                // We found an ambiguous date; provide appropriate feedback
                imageProperties = this.database.ImageDataTable[this.rangeStart];
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
            else
            {
                // No dates are ambiguous; provide appropriate feedback
                this.btnApplyAndShowNext.IsEnabled = false; // Disable the 'Next' button

                // Hide date-specific items so they are no longer visible on the screen
                this.lblOriginalDate.Visibility = Visibility.Hidden;
                this.lblNewDate.Visibility = Visibility.Hidden;
                this.cboxOriginalDate.Visibility = Visibility.Hidden;
                this.cboxSwapTheDates.Visibility = Visibility.Hidden;
                this.btnApplyAndShowNext.Visibility = Visibility.Hidden;
                this.btnApplyToAll.Visibility = Visibility.Hidden;


                this.lblImageName.Content = "--";
                this.lblNumberOfImagesWithSameDate.Content = "No ambiguous dates left";
                this.imgDateImage.Source = null;
                return false;
            }


        }

        // We go to the previous date simply by popping the stack of ambiguous dates seen so far
        //private bool TryShowPreviousAmbiguousDate()
        //{
        //    if (this.ambiguousDatesList.Count == 0)
         //   {
        //        return false;
        //    }
        //    this.rangeStart = this.ambiguousDatesList.Pop();
        //    this.TryShowNextAmbiguousDate();
       //     return true;
       // }
        #region Callbacks
            // This handler is triggered only when the radio button state is changed. This means
            // we should swap the dates regardless of which radio button was actually pressed.
        private void DateBox_Checked(object sender, RoutedEventArgs e)
        {
            // determine if we should swap the dates or not
            RadioButton selected = sender as RadioButton;
            if (selected == this.cboxSwapTheDates)
            {
                this.swapTheDates = true;
                this.btnApplyAndShowNext.Content = "Apply to " + (this.rangeEnd - this.rangeStart + 1).ToString() + " images & " + Environment.NewLine + "Show next ambiguous date";
                this.ambiguousDatesList[this.ambiguousDatesListIndex].Swapped = true;
            }
            else
            {
                this.swapTheDates = false;
                this.btnApplyAndShowNext.Content = "Show next ambiguous date";
                this.ambiguousDatesList[this.ambiguousDatesListIndex].Swapped = false;
            }
        }

        // If the user click ok, then exit
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        // If the user clicks the next button, try to show the next ambiguous date.
        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            // Swap the dates of the current day, if needed.
            if (this.swapTheDates)
            {
                this.database.ExchangeDayAndMonthInImageDates(this.rangeStart, this.rangeEnd);
            }

            this.rangeStart = this.rangeEnd + 1; // Go to the next image
            this.TryShowNextAmbiguousDate();

            // Set the next buttona nd the radio button back to their defaults
            // As we do this, unlink and then relink the callback as we don't want to invoke the data update
            this.cboxOriginalDate.Checked -= this.DateBox_Checked;
            this.cboxOriginalDate.IsChecked = true;
            this.cboxOriginalDate.Checked += this.DateBox_Checked;

            this.btnApplyAndShowNext.Content = "Show next ambiguous date";
        }

        // If the user clicks the next button, try to show the next ambiguous date.
        private void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
 // TO DO
        }
        #endregion

        #region Helper methods
        private int GetNextAmbiguousDate(int startIndex)
        {
            // Starting from the index, get the date from successive rows and see if the date is ambiguous
            // Note that if the index is out of range, it will return -1, so that's ok.
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
        // That is, return the final image that is dated the same date as this image
        private int GetLastImageOnSameDay(int startIndex)
        {
            if (startIndex >= this.database.CurrentlySelectedImageCount)
            {
                return -1;   // Make sure index is in range.
            }

            // Parse the provided starting date. Note that this should never fail at this point, but just in case, put out a debug message
            ImageRow imageProperties = this.database.ImageDataTable[startIndex];
            DateTime desiredDateTime;
            if (imageProperties.TryGetDateTime(out desiredDateTime) == false)
            {
                return -1;  // The starting date is not a valid date
            }
            for (int index = startIndex + 1; index < this.database.CurrentlySelectedImageCount; index++)
            {
                // Parse the date for the given record.
                imageProperties = this.database.ImageDataTable[index];
                DateTime imageDateTime;
                if (imageProperties.TryGetDateTime(out imageDateTime) == false)
                {
                    // TODOSAUL: code and comment are inconsistent; which is the desired behaviour?
                    continue; // if we get to an invalid date, return the prior index
                }

                if (desiredDateTime.Date == imageDateTime.Date)
                {
                    continue;
                }
                return index - 1;
            }
            return this.database.CurrentlySelectedImageCount - 1; // if we got here, it means that we arrived at the end of the records
        }
    }
    #endregion

    #region Convenience classes
    // A class that tracks our progress as we load the images
    internal class AmbiguousDate
    {
        public int StartRange { get; set; }
        public int EndRange { get; set; }
        public int Count 
        {
            get
            {
                return this.EndRange - this.StartRange + 1;
            }
        }
        public bool Swapped { get; set; }

        public AmbiguousDate(int startRange, int endRange, bool swapped)
        {
            this.StartRange = startRange;
            this.EndRange = endRange;
            this.Swapped  = swapped;
        }
    }
    #endregion
}