using System;
using System.Diagnostics;
using System.Windows;
using Timelapse.Database;
using Timelapse.Util;
using Xceed.Wpf.Toolkit;

namespace Timelapse
{
    /// <summary>
    /// Interaction logic for DialogDateTimeLinearCorrection.xaml
    /// This dialog lets the user specify a corrected date and time of a file. All other dates and times are then corrected by the same amount.
    /// This is useful if (say) the camera was not initialized to the correct date and time.
    /// </summary>
    public partial class DialogDateTimeLinearCorrection : Window
    {
        private DateTime latestImageDateTime;
        private DateTime earliestImageDateTime;
        private DateTime lastDateEnteredWithDateTimePicker; // Keeps track of the last valid date in the date picker so we can revert to it if needed.
        private ImageDatabase imageDatabase;
        private bool displayingPreview = false;

        public bool Abort { get; private set; }
        
        // Create the interface
        public DialogDateTimeLinearCorrection(ImageDatabase imageDatabase)
        {
            this.InitializeComponent();
            this.Abort = false;
            this.imageDatabase = imageDatabase;
            this.latestImageDateTime = DateTime.MinValue;
            this.earliestImageDateTime = DateTime.MaxValue;

            // Skip images with bad dates
            ImageRow latestImageRow = null;
            ImageRow earliestImageRow = null;
            foreach (ImageRow currentImageRow in imageDatabase.ImageDataTable)
            {
                DateTime currentImageDateTime;
                // Skip images with bad dates
                if (currentImageRow.TryGetDateTime(out currentImageDateTime) == false)
                {
                    continue;
                }

                // If the current image's date is later, then its a candidate latest image  
                if (currentImageDateTime >= this.latestImageDateTime)
                {
                    latestImageRow = currentImageRow;
                    this.latestImageDateTime = currentImageDateTime;
                }

                if (currentImageDateTime <= this.earliestImageDateTime)
                {
                    earliestImageRow = currentImageRow;
                    this.earliestImageDateTime = currentImageDateTime;
                }
            }

            // At this point, we should have succeeded getting the oldest and newest data/time
            // If not, we should abort
            if (this.earliestImageDateTime == DateTime.MaxValue || this.latestImageDateTime == DateTime.MinValue)
            {
                this.Abort = true;
                return;
            }

            // Configure feedback for earliest date and its image
            this.earliestImageName.Content = earliestImageRow.FileName;
            this.earliestImageDate.Content = DateTimeHandler.ToStandardDateTimeString(this.earliestImageDateTime);
            this.imageEarliest.Source = earliestImageRow.LoadBitmap(this.imageDatabase.FolderPath);

            // Configure feedback for latest date (in datetime picker) and its image
            this.latestImageName.Content = latestImageRow.FileName;
            this.dateTimePickerLatestDateTime.Format = DateTimeFormat.Custom;
            this.dateTimePickerLatestDateTime.FormatString = Constants.Time.DateTimeFormat;
            this.dateTimePickerLatestDateTime.TimeFormat = DateTimeFormat.Custom;
            this.dateTimePickerLatestDateTime.TimeFormatString = Constants.Time.TimeFormat;
            this.dateTimePickerLatestDateTime.Value = this.latestImageDateTime;
            this.lastDateEnteredWithDateTimePicker = this.latestImageDateTime; 
            this.dateTimePickerLatestDateTime.ValueChanged += this.DateTimePicker_ValueChanged;
            this.imageLatest.Source = latestImageRow.LoadBitmap(this.imageDatabase.FolderPath);
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

            // Preview the changes
            TimeSpan newestImageAdjustment = this.dateTimePickerLatestDateTime.Value.Value - this.latestImageDateTime;
            TimeSpan intervalFromOldestToNewestImage = this.latestImageDateTime - this.earliestImageDateTime;
            TimeSpan mostRecentAdjustment = TimeSpan.Zero;
            foreach (ImageRow row in this.imageDatabase.ImageDataTable)
            {
                string oldDT = row.Date + " " + row.Time;
                string newDT = String.Empty;
                string status = "Skipped: invalid date/time";
                string difference = string.Empty;

                DateTime imageDateTime;
                TimeSpan oneSecond = TimeSpan.FromSeconds(1);

                if (row.TryGetDateTime(out imageDateTime))
                {
                    double imagePositionInInterval;
                    // adjust the date / time
                    if (intervalFromOldestToNewestImage == TimeSpan.Zero)
                    {
                        imagePositionInInterval = 1;
                    }
                    else
                    {
                        imagePositionInInterval = (double)(imageDateTime - this.earliestImageDateTime).Ticks / (double)intervalFromOldestToNewestImage.Ticks;
                    }

                    TimeSpan adjustment = TimeSpan.FromTicks((long)(imagePositionInInterval * newestImageAdjustment.Ticks));

                    // Pretty print the adjustment time
                    if (adjustment.Duration() >= oneSecond)
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
                        difference = string.Format(format, sign, adjustment.Duration().Hours, adjustment.Duration().Minutes, adjustment.Duration().Seconds, adjustment.Duration().Days);

                        // Get the new date/time
                        newDT = DateTimeHandler.ToStandardDateTimeString(imageDateTime + adjustment);
                    }
                    else
                    {
                        status = "Unchanged";
                    }
                }
                this.DateUpdateFeedbackCtl.AddFeedbackRow(row.FileName, status, oldDT, newDT, difference);
            }
        }

        // 1st click of Ok: Show a preview of the changes.
        // 2nd click of OK: Update the database if the OK button is clicked
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // A few checks just to make sure we actually have something to do...
            if (this.dateTimePickerLatestDateTime.Value.HasValue == false)
            {
                this.DialogResult = false;
                return;
            }

            TimeSpan newestImageAdjustment = this.dateTimePickerLatestDateTime.Value.Value - this.latestImageDateTime;
            if (newestImageAdjustment == TimeSpan.Zero)
            {
                // nothing to do
                this.DialogResult = false;
                return;
            }

            // 1st real click of Ok: Show the preview before actually making any changes.
            if (this.displayingPreview == false)
            {
                this.PreviewDateTimeChanges();
                this.displayingPreview = true;
                this.OkButton.Content = "Apply Changes";
                return;
            }

            // 2nd click of Ok
            // We've shown the preview, which means the user actually wants to do the changes. 
            // Calculate the date/time difference and Update the database

            // In the single image case the the oldest and newest times will be the same
            // since Timelapse has only whole seconds resolution it's also possible with small selections from fast cameras that multiple images have the same time
            TimeSpan intervalFromOldestToNewestImage = this.latestImageDateTime - this.earliestImageDateTime;
            if (intervalFromOldestToNewestImage == TimeSpan.Zero)
            {
                this.imageDatabase.AdjustImageTimes(newestImageAdjustment);
            }
            else
            {
                this.imageDatabase.AdjustImageTimes(
                    (DateTime imageDateTime) =>
                    {
                        double imagePositionInInterval = (double)(imageDateTime - this.earliestImageDateTime).Ticks / (double)intervalFromOldestToNewestImage.Ticks;
                        Debug.Assert((-0.0000001 < imagePositionInInterval) && (imagePositionInInterval < 1.0000001), String.Format("Interval position {0} is not between 0.0 and 1.0.", imagePositionInInterval));
                        TimeSpan adjustment = TimeSpan.FromTicks((long)(imagePositionInInterval * newestImageAdjustment.Ticks)); // Used to have a  .5 increment, I think to force rounding upwards
                        // TimeSpan.Duration means we do these checks on the absolute value (positive) of the Timespan, as slow clocks will have negative adjustments.
                        Debug.Assert((TimeSpan.Zero <= adjustment.Duration()) && (adjustment.Duration() <= newestImageAdjustment.Duration()), String.Format("Expected adjustment {0} to be within [{1} {2}].", adjustment, TimeSpan.Zero, newestImageAdjustment));
                        return adjustment;
                    },
                    0,
                    this.imageDatabase.CurrentlySelectedImageCount - 1);
            }
            this.DialogResult = true;
        }

        // Cancel - do nothing
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void DateTimePicker_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            // Don't let the date picker go below the oldest time. If it does, don't change the date and play a beep.
            if (this.dateTimePickerLatestDateTime.Value.Value <= this.earliestImageDateTime)
            {
                this.dateTimePickerLatestDateTime.Value = this.lastDateEnteredWithDateTimePicker;
                DialogMessageBox messageBox = new DialogMessageBox("Your new time has to be later than the earliest time", this);
                messageBox.Message.Icon = MessageBoxImage.Exclamation;
                messageBox.Message.Problem = "Your new time has to be later than the earliest time   ";
                messageBox.Message.Reason = "Even the slowest clock gains some time.";
                messageBox.Message.Solution = "The date/time was unchanged from where you last left it.";
                messageBox.Message.Hint = "The image on the left shows the earliest time recorded for images in this filtered view  shown over the left image";
                messageBox.ShowDialog();
            }
            else
            {
                // Keep track of the last valid date in the date picker so we can revert to it if needed.
                this.lastDateEnteredWithDateTimePicker = this.dateTimePickerLatestDateTime.Value.Value;
            }
            // Enable the Ok button only if the latest time has actually changed from its original version
            TimeSpan newestImageAdjustment = this.dateTimePickerLatestDateTime.Value.Value - this.latestImageDateTime;
            this.OkButton.IsEnabled = (newestImageAdjustment == TimeSpan.Zero) ? false : true;
        }
    }
}
