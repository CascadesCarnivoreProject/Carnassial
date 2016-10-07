using Carnassial.Controls;
using Carnassial.Database;
using Carnassial.Util;
using System;
using System.Diagnostics;
using System.Windows;

namespace Carnassial.Dialog
{
    /// <summary>
    /// This dialog lets the user specify a corrected date and time of a file. All other dates and times are then corrected by the same amount.
    /// This is useful if (say) the camera was not initialized to the correct date and time.
    /// </summary>
    public partial class DateTimeLinearCorrection : Window
    {
        private readonly DateTimeOffset earliestImageDateTimeUncorrected;
        private readonly DateTimeOffset latestImageDateTimeUncorrected;

        private bool displayingPreview;
        private FileDatabase fileDatabase;

        public bool Abort { get; private set; }
        
        public DateTimeLinearCorrection(FileDatabase fileDatabase, Window owner)
        {
            this.InitializeComponent();
            this.Abort = false;
            this.Owner = owner;

            this.displayingPreview = false;
            this.earliestImageDateTimeUncorrected = DateTime.MaxValue.ToUniversalTime();
            this.latestImageDateTimeUncorrected = DateTime.MinValue.ToUniversalTime();
            this.fileDatabase = fileDatabase;

            // Skip images with bad dates
            ImageRow earliestImage = null;
            bool foundEarliest = false;
            bool foundLatest = false;
            ImageRow latestImage = null;
            foreach (ImageRow currentImageRow in fileDatabase.Files)
            {
                DateTimeOffset currentImageDateTime = currentImageRow.GetDateTime();

                // If the current image's date is later, then its a candidate latest image  
                if (currentImageDateTime >= this.latestImageDateTimeUncorrected)
                {
                    latestImage = currentImageRow;
                    this.latestImageDateTimeUncorrected = currentImageDateTime;
                    foundLatest = true;
                }

                if (currentImageDateTime <= this.earliestImageDateTimeUncorrected)
                {
                    earliestImage = currentImageRow;
                    this.earliestImageDateTimeUncorrected = currentImageDateTime;
                    foundEarliest = true;
                }
            }

            // At this point, we should have succeeded getting the oldest and newest data/time
            // If not, we should abort
            if ((foundEarliest == false) || (foundLatest == false))
            {
                this.Abort = true;
                return;
            }

            // configure earliest and latest images
            this.EarliestImageFileName.Content = earliestImage.FileName;
            this.EarliestImage.Source = earliestImage.LoadBitmap(this.fileDatabase.FolderPath);

            this.LatestImageFileName.Content = latestImage.FileName;
            this.LatestImage.Source = latestImage.LoadBitmap(this.fileDatabase.FolderPath);

            // configure interval
            DataEntryHandler.Configure(this.ClockLastCorrect, this.earliestImageDateTimeUncorrected.DateTime);
            DataEntryHandler.Configure(this.ClockDriftMeasured, this.latestImageDateTimeUncorrected.DateTime);
            this.ClockLastCorrect.Maximum = this.earliestImageDateTimeUncorrected.DateTime;
            this.ClockDriftMeasured.Minimum = this.latestImageDateTimeUncorrected.DateTime;
            this.ClockLastCorrect.ValueChanged += this.Interval_ValueChanged;
            this.ClockDriftMeasured.ValueChanged += this.Interval_ValueChanged;

            // configure adjustment
            this.ClockDrift.DefaultValue = TimeSpan.Zero;
            this.ClockDrift.DisplayDefaultValueOnEmptyText = true;
            this.ClockDrift.Value = this.ClockDrift.Value;
            this.ClockDrift.ValueChanged += this.ClockDrift_ValueChanged;

            // show image times
            this.RefreshImageTimes();
        }

        // Cancel - do nothing
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void ClockDrift_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            this.RefreshImageTimes();

            // the faster the camera's clock the more likely it is the latest image is in the future relative to when it should be
            // relax the measurement time constraint accordingly
            this.ClockDriftMeasured.Minimum = this.latestImageDateTimeUncorrected.DateTime;
            if (this.ClockDrift.Value.Value < TimeSpan.Zero)
            {
                this.ClockDriftMeasured.Minimum += this.ClockDrift.Value.Value;
            }

            // Enable the start button when there's a correction to apply
            if (this.ClockDrift.Value.HasValue == false)
            {
                this.ChangesButton.IsEnabled = false;
            }
            else
            {
                this.ChangesButton.IsEnabled = this.ClockDrift.Value.Value != TimeSpan.Zero;
            }
        }

        private void Interval_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            this.RefreshImageTimes();
        }

        private TimeSpan GetAdjustment(TimeSpan intervalFromCorrectToMeasured, DateTimeOffset imageDateTime)
        {
            Debug.Assert(intervalFromCorrectToMeasured >= TimeSpan.Zero, "Interval cannot be negative.");
            if (intervalFromCorrectToMeasured == TimeSpan.Zero)
            {
                return this.ClockDrift.Value.Value;
            }

            double imagePositionInInterval = (double)(imageDateTime.DateTime - this.ClockLastCorrect.Value.Value).Ticks / (double)intervalFromCorrectToMeasured.Ticks;
            Debug.Assert((-0.0000001 < imagePositionInInterval) && (imagePositionInInterval < 1.0000001), String.Format("Interval position {0} is not between 0.0 and 1.0.", imagePositionInInterval));

            TimeSpan adjustment = TimeSpan.FromTicks((long)(imagePositionInInterval * this.ClockDrift.Value.Value.Ticks + 0.5));
            // check the duration of the adjustment as slow clocks have negative adjustments
            Debug.Assert((TimeSpan.Zero <= adjustment.Duration()) && (adjustment.Duration() <= this.ClockDrift.Value.Value.Duration()), String.Format("Expected adjustment {0} to be within [{1} {2}].", adjustment, TimeSpan.Zero, this.ClockDrift.Value.Value));
            return adjustment;
        }

        private TimeSpan GetInterval()
        {
            return this.ClockDriftMeasured.Value.Value - this.ClockLastCorrect.Value.Value;
        }

        private void PreviewDateTimeChanges()
        {
            this.AdjustmentEntryPanel.Visibility = Visibility.Collapsed;
            this.FeedbackPanel.Visibility = Visibility.Visible;

            // Preview the changes
            TimeSpan intervalFromCorrectToMeasured = this.GetInterval();
            TimeZoneInfo imageSetTimeZone = this.fileDatabase.ImageSet.GetTimeZone();
            foreach (ImageRow image in this.fileDatabase.Files)
            {
                string newDateTime = String.Empty;
                string status = "Skipped: invalid date/time";
                string difference = String.Empty;
                DateTimeOffset imageDateTime = image.GetDateTime();

                TimeSpan adjustment = this.GetAdjustment(intervalFromCorrectToMeasured, imageDateTime);
                if (adjustment.Duration() >= Constants.Time.DateTimeDatabaseResolution)
                {
                    difference = DateTimeHandler.ToDisplayTimeSpanString(adjustment);
                    status = "Changed";
                    newDateTime = DateTimeHandler.ToDisplayDateTimeString(imageDateTime + adjustment);
                }
                else
                {
                    status = "Unchanged";
                }

                this.DateTimeChangeFeedback.AddFeedbackRow(image.FileName, status, image.GetDisplayDateTime(), newDateTime, difference);
            }
        }

        private void RefreshImageTimes()
        {
            TimeSpan intervalFromCorrectToMeasured = this.GetInterval();

            DateTimeOffset earliestImageDateTime = this.earliestImageDateTimeUncorrected + this.GetAdjustment(intervalFromCorrectToMeasured, this.earliestImageDateTimeUncorrected);
            this.EarliestImageDateTime.Content = DateTimeHandler.ToDisplayDateTimeString(earliestImageDateTime);

            DateTimeOffset latestImageDateTime = this.latestImageDateTimeUncorrected + this.GetAdjustment(intervalFromCorrectToMeasured, this.latestImageDateTimeUncorrected);
            this.LatestImageDateTime.Content = DateTimeHandler.ToDisplayDateTimeString(latestImageDateTime);

            this.ClockDriftMeasured.Minimum = this.latestImageDateTimeUncorrected.DateTime;
        }

        private void ChangesButton_Click(object sender, RoutedEventArgs e)
        {
            // A few checks just to make sure we actually have something to do...
            if (this.ClockDriftMeasured.Value.HasValue == false)
            {
                this.DialogResult = false;
                return;
            }

            // 1st click: Show the preview before actually making any changes.
            if (this.displayingPreview == false)
            {
                this.PreviewDateTimeChanges();
                this.displayingPreview = true;
                this.ChangesButton.Content = "_Apply Changes";
                return;
            }

            // 2nd click
            // We've shown the preview, which means the user actually wants to do the changes. 
            // Calculate the date/time difference and Update the database

            // In the single image case the the oldest and newest times will be the same
            // since Carnassial has only whole seconds resolution it's also possible with small selections from fast cameras that multiple images have the same time
            TimeSpan intervalFromCorrectToMeasured = this.GetInterval();
            if (intervalFromCorrectToMeasured == TimeSpan.Zero)
            {
                this.fileDatabase.AdjustFileTimes(this.ClockDrift.Value.Value);
            }
            else
            {
                this.fileDatabase.AdjustFileTimes(
                    (DateTimeOffset imageDateTime) =>
                    {
                        return imageDateTime + this.GetAdjustment(intervalFromCorrectToMeasured, imageDateTime);
                    },
                    0,
                    this.fileDatabase.CurrentlySelectedFileCount - 1);
            }
            this.DialogResult = true;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Utilities.SetDefaultDialogPosition(this);
            Utilities.TryFitWindowInWorkingArea(this);
        }
    }
}
