using Carnassial.Control;
using Carnassial.Data;
using Carnassial.Images;
using Carnassial.Native;
using Carnassial.Util;
using System;
using System.Diagnostics;
using System.Windows;

namespace Carnassial.Dialog
{
    /// <summary>
    /// This dialog lets the user specify a corrected date and time of an file. All other dates and times are then corrected by the same amount.
    /// This is useful if (say) the camera was not initialized to the correct date and time.
    /// It assumes that Carnassial is configured to display all files, and the its currently displaying a valid file and thus a valid date.
    /// </summary>
    public partial class DateTimeFixedCorrection : Window
    {
        private readonly FileDatabase fileDatabase;
        private readonly ImageCache imageCache;
        private readonly DateTimeOffset initialDateTime;

        private bool displayingPreview;

        public DateTimeFixedCorrection(FileDatabase fileDatabase, ImageCache imageCache, Window owner)
        {
            this.InitializeComponent();
            this.displayingPreview = false;
            this.fileDatabase = fileDatabase;
            this.imageCache = imageCache;
            this.Owner = owner;

            // get the file's name and display it
            ImageRow fileToDisplay = imageCache.Current;
            Debug.Assert(fileToDisplay != null, "Current file unexpectedly null.");
            this.FileName.Content = fileToDisplay.FileName;
            this.FileName.ToolTip = this.FileName.Content;

            // configure datetime picker
            this.initialDateTime = fileToDisplay.DateTimeOffset;
            this.OriginalDate.Content = DateTimeHandler.ToDisplayDateTimeString(this.initialDateTime);
            this.DateTimePicker.Value = this.initialDateTime;
            this.DateTimePicker.ValueChanged += this.DateTimePicker_ValueChanged;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void ChangesButton_Click(object sender, RoutedEventArgs e)
        {
            // 1st click: Show the preview before actually making any changes.
            if (this.displayingPreview == false)
            {
                this.PreviewDateTimeChanges();
                this.displayingPreview = true;
                this.ChangesButton.Content = "_Apply Changes";
                return;
            }

            // 2nd click
            // Calculate and apply the date/time difference
            DateTime originalDateTime = DateTimeHandler.ParseDisplayDateTimeString((string)this.OriginalDate.Content);
            TimeSpan adjustment = this.DateTimePicker.Value - originalDateTime;
            if (adjustment == TimeSpan.Zero)
            {
                this.DialogResult = false; // No difference, so nothing to correct
                return;
            }

            // Update the database
            this.fileDatabase.AdjustFileTimes(adjustment);
            this.DialogResult = true;
        }

        private void DateTimePicker_ValueChanged(DateTimeOffsetPicker sender, DateTimeOffset newDateTime)
        {
            TimeSpan difference = newDateTime - this.initialDateTime;
            this.ChangesButton.IsEnabled = difference != TimeSpan.Zero;
        }

        private void PreviewDateTimeChanges()
        {
            TimeSpan adjustment = this.DateTimePicker.Value - this.initialDateTime;
            foreach (ImageRow file in this.fileDatabase.Files)
            {
                string newDateTime = String.Empty;
                string status = "Skipped: invalid date/time";
                string difference = String.Empty;
                DateTimeOffset imageDateTime = file.DateTimeOffset;

                // Pretty print the adjustment time
                if (adjustment.Duration() >= Constant.Time.DateTimeDatabaseResolution)
                {
                    difference = DateTimeHandler.ToDisplayTimeSpanString(adjustment);
                    status = "Changed";
                    newDateTime = DateTimeHandler.ToDisplayDateTimeString(imageDateTime + adjustment);
                }
                else
                {
                    status = "Unchanged";
                }
                this.DateTimeChangeFeedback.AddFeedbackRow(file.FileName, status, file.GetDisplayDateTime(), newDateTime, difference);
            }

            this.PrimaryPanel.Visibility = Visibility.Collapsed;
            this.FeedbackPanel.Visibility = Visibility.Visible;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Utilities.SetDefaultDialogPosition(this);
            Utilities.TryFitWindowInWorkingArea(this);

            await this.FileDisplay.DisplayAsync(this.fileDatabase.FolderPath, this.imageCache);
        }
    }
}
