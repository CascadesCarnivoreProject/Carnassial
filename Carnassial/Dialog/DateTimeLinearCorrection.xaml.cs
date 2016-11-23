﻿using Carnassial.Controls;
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
        private readonly DateTimeOffset earliestFileDateTimeUncorrected;
        private readonly DateTimeOffset latestFileDateTimeUncorrected;

        private bool displayingPreview;
        private FileDatabase fileDatabase;

        public bool Abort { get; private set; }
        
        public DateTimeLinearCorrection(FileDatabase fileDatabase, Window owner)
        {
            this.InitializeComponent();
            this.Abort = false;
            this.Owner = owner;

            this.displayingPreview = false;
            this.earliestFileDateTimeUncorrected = DateTime.MaxValue.ToUniversalTime();
            this.latestFileDateTimeUncorrected = DateTime.MinValue.ToUniversalTime();
            this.fileDatabase = fileDatabase;

            ImageRow earliestImage = null;
            bool foundEarliest = false;
            bool foundLatest = false;
            ImageRow latestImage = null;
            foreach (ImageRow file in fileDatabase.Files)
            {
                DateTimeOffset fileDateTime = file.GetDateTime();

                if (fileDateTime >= this.latestFileDateTimeUncorrected)
                {
                    latestImage = file;
                    this.latestFileDateTimeUncorrected = fileDateTime;
                    foundLatest = true;
                }

                if (fileDateTime <= this.earliestFileDateTimeUncorrected)
                {
                    earliestImage = file;
                    this.earliestFileDateTimeUncorrected = fileDateTime;
                    foundEarliest = true;
                }
            }

            if ((foundEarliest == false) || (foundLatest == false))
            {
                this.Abort = true;
                return;
            }

            // configure earliest and latest images
            this.EarliestImageFileName.Content = earliestImage.FileName;
            this.EarliestImageFileName.ToolTip = this.EarliestImageFileName.Content;
            this.EarliestImage.Source = earliestImage.LoadBitmap(this.fileDatabase.FolderPath);

            this.LatestImageFileName.Content = latestImage.FileName;
            this.LatestImageFileName.ToolTip = this.LatestImageFileName.Content;
            this.LatestImage.Source = latestImage.LoadBitmap(this.fileDatabase.FolderPath);

            // configure interval
            this.ClockLastCorrect.Maximum = this.earliestFileDateTimeUncorrected;
            this.ClockLastCorrect.Value = this.earliestFileDateTimeUncorrected;
            this.ClockLastCorrect.ValueChanged += this.Interval_ValueChanged;

            this.ClockDriftMeasured.Minimum = this.latestFileDateTimeUncorrected;
            this.ClockDriftMeasured.Value = this.latestFileDateTimeUncorrected;
            this.ClockDriftMeasured.ValueChanged += this.Interval_ValueChanged;

            // configure adjustment
            this.ClockDrift.Value = this.ClockDrift.Value;
            this.ClockDrift.ValueChanged += this.ClockDrift_ValueChanged;

            // show image times
            this.RefreshImageTimes();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void ClockDrift_ValueChanged(TimeSpanPicker sender, TimeSpan newTimeSpan)
        {
            this.RefreshImageTimes();

            // the faster the camera's clock the more likely it is the latest image is in the future relative to when it should be
            // relax the measurement time constraint accordingly
            this.ClockDriftMeasured.Minimum = this.latestFileDateTimeUncorrected.DateTime;
            if (this.ClockDrift.Value < TimeSpan.Zero)
            {
                this.ClockDriftMeasured.Minimum += this.ClockDrift.Value;
            }

            // Enable the start button when there's a correction to apply
            this.ChangesButton.IsEnabled = this.ClockDrift.Value != TimeSpan.Zero;
        }

        private void Interval_ValueChanged(DateTimeOffsetPicker sender, DateTimeOffset newDateTime)
        {
            this.RefreshImageTimes();
        }

        private TimeSpan GetAdjustment(TimeSpan intervalFromCorrectToMeasured, DateTimeOffset imageDateTime)
        {
            Debug.Assert(intervalFromCorrectToMeasured >= TimeSpan.Zero, "Interval cannot be negative.");
            if (intervalFromCorrectToMeasured == TimeSpan.Zero)
            {
                return this.ClockDrift.Value;
            }

            double imagePositionInInterval = (double)(imageDateTime - this.ClockLastCorrect.Value).Ticks / (double)intervalFromCorrectToMeasured.Ticks;
            Debug.Assert((-0.0000001 < imagePositionInInterval) && (imagePositionInInterval < 1.0000001), String.Format("Interval position {0} is not between 0.0 and 1.0.", imagePositionInInterval));

            TimeSpan adjustment = TimeSpan.FromTicks((long)(imagePositionInInterval * this.ClockDrift.Value.Ticks + 0.5));
            // check the duration of the adjustment as slow clocks have negative adjustments
            Debug.Assert((TimeSpan.Zero <= adjustment.Duration()) && (adjustment.Duration() <= this.ClockDrift.Value.Duration()), String.Format("Expected adjustment {0} to be within [{1} {2}].", adjustment, TimeSpan.Zero, this.ClockDrift.Value));
            return adjustment;
        }

        private TimeSpan GetInterval()
        {
            return this.ClockDriftMeasured.Value - this.ClockLastCorrect.Value;
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

                this.DateTimeChangeFeedback.AddFeedbackRow(image.FileName, status, image.GetDisplayDateTime(), newDateTime, difference);
            }
        }

        private void RefreshImageTimes()
        {
            TimeSpan intervalFromCorrectToMeasured = this.GetInterval();

            DateTimeOffset earliestImageDateTime = this.earliestFileDateTimeUncorrected + this.GetAdjustment(intervalFromCorrectToMeasured, this.earliestFileDateTimeUncorrected);
            this.EarliestImageDateTime.Content = DateTimeHandler.ToDisplayDateTimeString(earliestImageDateTime);

            DateTimeOffset latestImageDateTime = this.latestFileDateTimeUncorrected + this.GetAdjustment(intervalFromCorrectToMeasured, this.latestFileDateTimeUncorrected);
            this.LatestImageDateTime.Content = DateTimeHandler.ToDisplayDateTimeString(latestImageDateTime);

            this.ClockDriftMeasured.Minimum = this.latestFileDateTimeUncorrected.DateTime;
        }

        private void ChangesButton_Click(object sender, RoutedEventArgs e)
        {
            // 1st click: show the preview before actually making any changes.
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
                this.fileDatabase.AdjustFileTimes(this.ClockDrift.Value);
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
