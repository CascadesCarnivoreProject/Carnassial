using System;
using System.Windows;
using Timelapse.Controls;
using Timelapse.Database;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// This dialog lets the user specify a corrected date and time of an file. All other dates and times are then corrected by the same amount.
    /// This is useful if (say) the camera was not initialized to the correct date and time.
    /// It assumes that Timelapse is configured to display all files, and the its currently displaying a valid file and thus a valid date.
    /// </summary>
    public partial class DateTimeFixedCorrection : Window
    {
        private ImageDatabase imageDatabase;
        private DateTimeOffset initialDate;
        private bool displayingPreview = false;

        public bool Abort { get; private set; }
        
        // Create the interface
        public DateTimeFixedCorrection(ImageDatabase imageDatabase, ImageRow imageToCorrect, Window owner)
        {
            this.InitializeComponent();
            this.imageDatabase = imageDatabase;
            this.Owner = owner;

            this.Abort = false;

            // get the image filename and display it
            this.imageName.Content = imageToCorrect.FileName;

            // display the image
            this.image.Source = imageToCorrect.LoadBitmap(this.imageDatabase.FolderPath);

            // configure datetime picker
            TimeZoneInfo imageSetTimeZone = this.imageDatabase.ImageSet.GetTimeZone();
            if (imageToCorrect.TryGetDateTime(imageSetTimeZone, out this.initialDate))
            {
                this.originalDate.Content = DateTimeHandler.ToDisplayDateTimeString(this.initialDate);
                DataEntryHandler.Configure(this.DateTimePicker, this.initialDate.DateTime);
                this.DateTimePicker.ValueChanged += this.DateTimePicker_ValueChanged;
            }
            else
            {
                this.Abort = true;
                return;
            }
        }

        private void DlgDateCorrectionName_Loaded(object sender, RoutedEventArgs e)
        {
            Utilities.SetDefaultDialogPosition(this);
            Utilities.TryFitWindowInWorkingArea(this);
        }

        private void PreviewDateTimeChanges()
        {
            this.PrimaryPanel.Visibility = Visibility.Collapsed;
            this.FeedbackPanel.Visibility = Visibility.Visible;

            TimeSpan adjustment = this.DateTimePicker.Value.Value - this.initialDate.DateTime;

            // Preview the changes
            TimeZoneInfo imageSetTimeZone = this.imageDatabase.ImageSet.GetTimeZone();
            foreach (ImageRow row in this.imageDatabase.ImageDataTable)
            {
                string newDateTime = String.Empty;
                string status = "Skipped: invalid date/time";
                string difference = String.Empty;
                DateTimeOffset imageDateTime;
                if (row.TryGetDateTime(imageSetTimeZone, out imageDateTime))
                {
                    // Pretty print the adjustment time
                    if (adjustment.Duration() >= Constants.Time.DateTimeDatabaseResolution)
                    {
                        string sign = (adjustment < TimeSpan.Zero) ? "-" : "+";
                        status = "Changed";

                        // Pretty print the adjustment time, depending upon how many day(s) were included 
                        string format;
                        if (adjustment.Days == 0)
                        {
                            format = "{0:s}{1:D2}:{2:D2}:{3:D2}"; // Don't show the days field
                        }
                        else if (adjustment.Duration().Days == 1)
                        {
                            format = "{0:s}{1:D2}:{2:D2}:{3:D2} {0:s} {4:D} day";
                        }
                        else
                        {
                            format = "{0:s}{1:D2}:{2:D2}:{3:D2} {0:s} {4:D} days";
                        }
                        difference = String.Format(format, sign, adjustment.Duration().Hours, adjustment.Duration().Minutes, adjustment.Duration().Seconds, adjustment.Duration().Days);

                        // Get the new date/time
                        newDateTime = DateTimeHandler.ToDisplayDateTimeString(imageDateTime + adjustment);
                    }
                    else
                    {
                        status = "Unchanged";
                    }
                }
                this.DateUpdateFeedbackCtl.AddFeedbackRow(row.FileName, status, row.GetDisplayDateTime(imageSetTimeZone), newDateTime, difference);
            }
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.DateTimePicker.Value.HasValue == false)
            {
                this.DialogResult = false;
                return;
            }

            // 1st click: Show the preview before actually making any changes.
            if (this.displayingPreview == false)
            {
                this.PreviewDateTimeChanges();
                this.displayingPreview = true;
                this.StartButton.Content = "_Apply Changes";
                return;
            }

            // 2nd click
            // Calculate and apply the date/time difference
            DateTime originalDateTime = DateTimeHandler.ParseDisplayDateTimeString((string)this.originalDate.Content);
            TimeSpan adjustment = this.DateTimePicker.Value.Value - originalDateTime;
            if (adjustment == TimeSpan.Zero)
            {
                this.DialogResult = false; // No difference, so nothing to correct
                return;
            }

            // Update the database
            this.imageDatabase.AdjustImageTimes(adjustment);
            this.DialogResult = true;
        }

        // Cancel - do nothing
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void DateTimePicker_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            TimeSpan difference = this.DateTimePicker.Value.Value - this.initialDate;
            this.StartButton.IsEnabled = (difference == TimeSpan.Zero) ? false : true;
        }
    }
}
