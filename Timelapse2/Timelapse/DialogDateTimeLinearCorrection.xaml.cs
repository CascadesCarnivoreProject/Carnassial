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
        private DateTime newestImageDateTime;
        private DateTime oldestImageDateTime;
        private ImageDatabase imageDatabase;

        public bool Abort { get; private set; }
        
        // Create the interface
        public DialogDateTimeLinearCorrection(ImageDatabase imageDatabase)
        {
            this.InitializeComponent();
            this.Abort = false;
            this.imageDatabase = imageDatabase;
            this.newestImageDateTime = DateTime.MinValue;
            this.oldestImageDateTime = DateTime.MaxValue;

            // locate the oldest and newest images in the selected set
            ImageRow newestImage = null;
            foreach (ImageRow image in imageDatabase.ImageDataTable)
            {
                DateTime imageDateTime;
                if (image.TryGetDateTime(out imageDateTime) == false)
                {
                    DateTimeHandler.ShowDateTimeParseFailureDialog(image, this);
                    this.Abort = true;
                    return;
                }

                if (newestImage == null || imageDateTime >= this.newestImageDateTime)
                {
                    newestImage = image;
                    this.newestImageDateTime = imageDateTime;
                }

                if (this.oldestImageDateTime > imageDateTime)
                {
                    this.oldestImageDateTime = imageDateTime;
                }
            }

            // configure datetime picker
            this.originalDate.Content = DateTimeHandler.ToStandardDateTimeString(this.newestImageDateTime);
            this.dateTimePicker.Format = DateTimeFormat.Custom;
            this.dateTimePicker.FormatString = Constants.Time.DateTimeFormat;
            this.dateTimePicker.TimeFormat = DateTimeFormat.Custom;
            this.dateTimePicker.TimeFormatString = Constants.Time.TimeFormat;
            this.dateTimePicker.Value = this.newestImageDateTime;
            this.dateTimePicker.ValueChanged += this.DateTimePicker_ValueChanged;

            // display the newest image
            this.imageName.Content = newestImage.FileName;
            this.image.Source = newestImage.LoadBitmap(this.imageDatabase.FolderPath);
        }

        private void DlgDateCorrectionName_Loaded(object sender, RoutedEventArgs e)
        {
            Utilities.SetDefaultDialogPosition(this);
            Utilities.TryFitWindowInWorkingArea(this);
        }

        // Try to update the database if the OK button is clicked
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.dateTimePicker.Value.HasValue == false)
            {
                this.DialogResult = false;
                return;
            }

            // calculate the date/time difference
            DateTime originalDateTime = DateTimeHandler.FromStandardDateTimeString((string)this.originalDate.Content);
            TimeSpan newestImageAdjustment = this.dateTimePicker.Value.Value - originalDateTime;
            if (newestImageAdjustment == TimeSpan.Zero)
            {
                // nothing to do
                this.DialogResult = false;
                return;
            }

            // update the database
            // in the single image case the the oldest and newest times will be the same
            // since Timelapse has only whole seconds resolution it's also possible with small selections from fast cameras that multiple images have the same time
            TimeSpan intervalFromOldestToNewestImage = this.newestImageDateTime - this.oldestImageDateTime;
            if (intervalFromOldestToNewestImage == TimeSpan.Zero)
            {
                this.imageDatabase.AdjustImageTimes(newestImageAdjustment);
            }
            else
            {
                this.imageDatabase.AdjustImageTimes(
                    (DateTime imageDateTime) =>
                    {
                        double imagePositionInInterval = (double)(imageDateTime - this.oldestImageDateTime).Ticks / (double)intervalFromOldestToNewestImage.Ticks;
                        Debug.Assert((-0.0000001 < imagePositionInInterval) && (imagePositionInInterval < 1.0000001), String.Format("Interval position {0} is not between 0.0 and 1.0.", imagePositionInInterval));
                        TimeSpan adjustment = TimeSpan.FromTicks((long)(imagePositionInInterval * newestImageAdjustment.Ticks + 0.5));
                        Debug.Assert((TimeSpan.Zero <= adjustment) && (adjustment <= newestImageAdjustment), String.Format("Expected adjustment {0} to be within [{1} {2}].", adjustment, TimeSpan.Zero, newestImageAdjustment));
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
