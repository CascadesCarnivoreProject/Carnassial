using Carnassial.Data;
using Carnassial.Native;
using Carnassial.Util;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Carnassial.Dialog
{
    /// <summary>
    /// Contract: the caller should not invoke DialogShow() if Abort is true.
    /// </summary>
    public partial class DateCorrectAmbiguous : Window
    {
        private List<AmbiguousDate> ambiguousDatesList; // all initial images containing ambiguous dates and their state
        private int ambiguousDatesListIndex;
        private FileDatabase database;
        private bool displayingPreview;

        // whether the operation should be aborted, ie., because there are no ambiguous dates
        public bool Abort { get; set; }

        public DateCorrectAmbiguous(FileDatabase database, Window owner)
        {
            this.InitializeComponent();
            this.ambiguousDatesList = new List<AmbiguousDate>();
            this.database = database;
            this.displayingPreview = false;
            this.Owner = owner;

            // set callbacks in code behind to avoid invoking callbacks when the dialog is created
            this.OriginalDate.Checked += this.DateBox_Checked;
            this.SwappedDate.Checked += this.DateBox_Checked;

            // find the ambiguous dates in the current selection
            if (this.FindAllAmbiguousDatesInSelectedImages() == true)
            {
                this.Abort = false;
            }
            else
            {
                this.Abort = true;
            }

            // Start displaying from the first ambiguous date.
            this.ambiguousDatesListIndex = 0;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Utilities.SetDefaultDialogPosition(this);
            Utilities.TryFitWindowInWorkingArea(this);

            await this.MoveToAmbiguousDateAsync(null); // go to first ambiguous date
            // If the caller invokes Show with Abort = true (i.e., count = 0), this will at least show an empty dialog.
            await this.UpdateDisplayAsync(this.ambiguousDatesList.Count > 0);
        }

        // Create a list of all initial images containing ambiguous dates.
        // This includes calculating the start and end rows of all images matching an ambiguous date
        private bool FindAllAmbiguousDatesInSelectedImages()
        {
            int start = this.SearchForNextAmbiguousDateInSelectedFiles(0);
            while (start != -1)
            {
                int count;
                int end = this.GetLastFileOnSameDay(start, out count);
                this.ambiguousDatesList.Add(new AmbiguousDate(start, end, count, false));
                start = this.SearchForNextAmbiguousDateInSelectedFiles(end + 1);
            }
            return (this.ambiguousDatesList.Count > 0) ? true : false;
        }

        // Starting from the index, navigate successive file rows until an ambiguous date is found
        // If it can't find an ambiguous date, it will return -1.
        private int SearchForNextAmbiguousDateInSelectedFiles(int startIndex)
        {
            for (int index = startIndex; index < this.database.CurrentlySelectedFileCount; index++)
            {
                ImageRow file = this.database.Files[index];
                DateTimeOffset imageDateTime = file.DateTimeOffset;
                if (imageDateTime.Day <= Constant.Time.MonthsInYear)
                {
                    return index; // If the date is ambiguous, return the row index. 
                }
            }
            return -1; // -1 means all dates are unambiguous
        }

        // Given a starting index, find its date and then go through the successive images until the date differs.
        // Return the final file that is dated the same date as this file
        // Assumption is that the index is valid and is pointing to an file with a valid date.
        // However, it still tests for problems and returns -1 if there was a problem.
        private int GetLastFileOnSameDay(int startIndex, out int count)
        {
            count = 1; // start at 1 to count the file indicated by startIndex

            // Check if index is in range
            if (this.database.IsFileRowInRange(startIndex) == false)
            {
                return Constant.Database.InvalidRow;
            }

            ImageRow file = this.database.Files[startIndex];
            DateTimeOffset desiredDateTime = file.DateTimeOffset;

            int lastMatchingDateIndex = startIndex;
            for (int index = startIndex + 1; index < this.database.CurrentlySelectedFileCount; index++)
            {
                file = this.database.Files[index];
                DateTimeOffset imageDateTime = file.DateTimeOffset;
                if (desiredDateTime.Date == imageDateTime.Date)
                {
                    lastMatchingDateIndex = index;
                    count++;
                    continue;
                }
                return lastMatchingDateIndex;
            }

            // arrived at end of files
            return lastMatchingDateIndex;
        }

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
        private async Task<bool> MoveToAmbiguousDateAsync(bool? directionForward)
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

            this.ambiguousDatesListIndex = index;

            // found an ambiguous date; provide appropriate feedback
            ImageRow file = this.database.Files[this.ambiguousDatesList[index].StartRange];
            this.OriginalDateLabel.Content = file.DateTimeOffset.Date;

            DateTimeOffset swappedDate;
            this.SwappedDateLabel.Content = DateTimeHandler.TrySwapDayMonth(file.DateTimeOffset, out swappedDate) ? DateTimeHandler.ToDisplayDateTimeString(swappedDate) : DateTimeHandler.ToDisplayDateTimeString(file.DateTimeOffset);

            this.NumberOfImagesWithSameDate.Content = this.ambiguousDatesList[this.ambiguousDatesListIndex].Count.ToString();

            using (MemoryImage image = await file.LoadAsync(this.database.FolderPath, (int)this.Width))
            {
                image.SetSource(this.Image);
            }
            this.FileName.Content = file.FileName;
            this.FileName.ToolTip = this.FileName.Content;

            return true;
        }

        private async Task UpdateDisplayAsync(bool isAmbiguousDate)
        {
            // enable/disable next and previous buttons
            this.NextDate.IsEnabled = this.IsThereAnAmbiguousDate(true);
            this.PreviousDate.IsEnabled = this.IsThereAnAmbiguousDate(false);

            if (isAmbiguousDate)
            {
                ImageRow imageProperties;
                imageProperties = this.database.Files[this.ambiguousDatesList[this.ambiguousDatesListIndex].StartRange];
                this.OriginalDateLabel.Content = imageProperties.DateTimeOffset.Date;

                DateTimeOffset swappedDate;
                this.SwappedDateLabel.Content = DateTimeHandler.TrySwapDayMonth(imageProperties.DateTimeOffset, out swappedDate) ? DateTimeHandler.ToDisplayDateTimeString(swappedDate) : DateTimeHandler.ToDisplayDateTimeString(imageProperties.DateTimeOffset);

                this.NumberOfImagesWithSameDate.Content = this.ambiguousDatesList[this.ambiguousDatesListIndex].Count.ToString();

                using (MemoryImage image = await imageProperties.LoadAsync(this.database.FolderPath, (int)this.Width))
                {
                    image.SetSource(this.Image);
                }
                this.FileName.Content = imageProperties.FileName;
                this.FileName.ToolTip = this.FileName.Content;

                // set the next button and the radio button back to their defaults
                // unlink and relink callback as to avoid a data update
                this.OriginalDate.Checked -= this.DateBox_Checked;
                this.OriginalDate.IsChecked = !this.ambiguousDatesList[this.ambiguousDatesListIndex].Swapped;
                this.SwappedDate.IsChecked = this.ambiguousDatesList[this.ambiguousDatesListIndex].Swapped;
                this.OriginalDate.Checked += this.DateBox_Checked;
            }
            else
            {
                // no more dates to swap
                // hide date specific items
                this.OriginalDateLabel.Visibility = Visibility.Hidden;
                this.SwappedDateLabel.Visibility = Visibility.Hidden;

                this.OriginalDate.Visibility = Visibility.Hidden;
                this.SwappedDate.Visibility = Visibility.Hidden;

                this.Image.Source = null;
                this.FileName.Content = String.Empty;
                this.FileName.ToolTip = this.FileName.Content;

                this.NumberOfImagesWithSameDate.Content = "No ambiguous dates left";
            }
        }

        private void PreviewDateTimeChanges()
        {
            foreach (AmbiguousDate ambiguousDate in this.ambiguousDatesList)
            {
                ImageRow file = this.database.Files[ambiguousDate.StartRange];
                string newDate = String.Empty;
                if (ambiguousDate.Swapped)
                {
                    DateTimeOffset swappedDate;
                    DateTimeHandler.TrySwapDayMonth(file.DateTimeOffset, out swappedDate);
                    newDate = DateTimeHandler.ToDisplayDateTimeString(swappedDate);
                }

                string status = ambiguousDate.Swapped ? "Swapped: " : "Unchanged: ";
                status += ambiguousDate.Count.ToString() + " images with same date";
                this.DateChangeFeedback.AddFeedbackRow(file.FileName, status, DateTimeHandler.ToDisplayDateString(file.DateTimeOffset), newDate, null);
            }

            this.PrimaryPanel.Visibility = Visibility.Collapsed;
            this.FeedbackPanel.Visibility = Visibility.Visible;
        }

        // Actually update the dates as needed
        private void ApplyDateTimeChanges()
        {
            foreach (AmbiguousDate ambDate in this.ambiguousDatesList)
            {
                ImageRow imageProperties;
                imageProperties = this.database.Files[ambDate.StartRange];
                string newDate = String.Empty;

                if (ambDate.Swapped)
                {
                    this.database.ExchangeDayAndMonthInFileDates(ambDate.StartRange, ambDate.EndRange);
                }
            }
        }

        private void DateBox_Checked(object sender, RoutedEventArgs e)
        {
            // determine if date should be swapped
            RadioButton selected = sender as RadioButton;
            this.ambiguousDatesList[this.ambiguousDatesListIndex].Swapped = selected == this.SwappedDate;
        }

        private void PreviewChangesButton_Click(object sender, RoutedEventArgs e)
        {
            // 1st click: Show the preview before actually making any changes.
            if (this.displayingPreview == false)
            {
                this.displayingPreview = true;
                this.PreviewDateTimeChanges();
                this.PreviewChangesButton.Content = "_Apply Changes";
                return;
            }

            // 2nd click: Make the changes
            this.ApplyDateTimeChanges();
            this.DialogResult = true;
        }
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        // If the user clicks the next button, try to show the next ambiguous date.
        private async void NextPreviousButton_Click(object senderAsObject, RoutedEventArgs e)
        {
            Button sender = senderAsObject as Button;
            bool result = await this.MoveToAmbiguousDateAsync(sender == this.NextDate);
            await this.UpdateDisplayAsync(result);
        }

        private async void SwapAllButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (AmbiguousDate ambDate in this.ambiguousDatesList)
            {
                ambDate.Swapped = true;
            }
            await this.UpdateDisplayAsync(true);
        }

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
    }
}
