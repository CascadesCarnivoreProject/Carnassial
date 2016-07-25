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
    /// This dialog lets the user specify a corrected date and time of an file. All other dates and times are then corrected by the same amount.
    /// This is useful if (say) the camera was not initialized to the correct date and time.
    /// It assumes that Timelapse is configured to display all files, and the its currently displaying a valid file and thus a valid date.
    /// </summary>
    public partial class DialogDateTimeLinearCorrection : Window
    {
        private DateTime latestImageDateTime;
        private DateTime earliestImageDateTime;
        private ImageDatabase imageDatabase;

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

                if (currentImageDateTime <= this.earliestImageDateTime  )
                {
                    earliestImageRow = currentImageRow;
                    this.earliestImageDateTime = currentImageDateTime;
                }
            }

            // At this point, we should have succeeded getting the oldest and newest data/time
            // If not, we should abort
            if (this.earliestImageDateTime == null || this.latestImageDateTime == null)
            {
                this.Abort = true;
                return;
            }

            // Configure feedback for earliest date and its iage
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
            this.dateTimePickerLatestDateTime.ValueChanged += this.DateTimePicker_ValueChanged;
            this.imageLatest.Source = latestImageRow.LoadBitmap(this.imageDatabase.FolderPath);
        }

        private void DlgDateCorrectionName_Loaded(object sender, RoutedEventArgs e)
        {
            Utilities.SetDefaultDialogPosition(this);
            Utilities.TryFitWindowInWorkingArea(this);
        }

        // Try to update the database if the OK button is clicked
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.dateTimePickerLatestDateTime.Value.HasValue == false)
            {
                this.DialogResult = false;
                return;
            }

            // Calculate the date/time difference
            DateTime originalDateTime = DateTimeHandler.FromStandardDateTimeString((string)this.earliestImageDate.Content);
            TimeSpan newestImageAdjustment = this.dateTimePickerLatestDateTime.Value.Value - latestImageDateTime;
            if (newestImageAdjustment == TimeSpan.Zero)
            {
                // nothing to do
                this.DialogResult = false;
                return;
            }

            // Update the database
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
                        TimeSpan adjustment = TimeSpan.FromTicks((long)(imagePositionInInterval * newestImageAdjustment.Ticks + 0.5)); // I think the .5 is to force rounding upwards
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
            this.OkButton.IsEnabled = true;
        }
    }
}
