using System;
using System.Windows;
using Timelapse.Database;
using Timelapse.Util;
using Xceed.Wpf.Toolkit;

namespace Timelapse
{
    /// <summary>
    /// Interaction logic for DialogDateTimeFixedCorrection.xaml
    /// This dialog lets the user specify a corrected date and time of an file. All other dates and times are then corrected by the same amount.
    /// This is useful if (say) the camera was not initialized to the correct date and time.
    /// It assumes that Timelapse is configured to display all files, and the its currently displaying a valid file and thus a valid date.
    /// </summary>
    public partial class DialogDateTimeFixedCorrection : Window
    {
        private ImageDatabase imageDatabase;
        private DateTime initialDate;
        private bool displayingPreview = false;

        public bool Abort { get; private set; }
        
        // Create the interface
        public DialogDateTimeFixedCorrection(ImageDatabase imageDatabase, ImageRow imageToCorrect)
        {
            this.InitializeComponent();
            this.imageDatabase = imageDatabase;

            this.Abort = false;

            // get the image filename and display it
            this.imageName.Content = imageToCorrect.FileName;

            // display the image
            this.image.Source = imageToCorrect.LoadBitmap(this.imageDatabase.FolderPath);

            // configure datetime picker
            if (imageToCorrect.TryGetDateTime(out this.initialDate))
            {
                this.originalDate.Content = DateTimeHandler.ToStandardDateTimeString(this.initialDate);
                this.dateTimePicker.Format = DateTimeFormat.Custom;
                this.dateTimePicker.FormatString = Constants.Time.DateTimeFormat;
                this.dateTimePicker.TimeFormat = DateTimeFormat.Custom;
                this.dateTimePicker.TimeFormatString = Constants.Time.TimeFormat;
                this.dateTimePicker.Value = this.initialDate;
                this.dateTimePicker.ValueChanged += this.DateTimePicker_ValueChanged;
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

            TimeSpan adjustment = this.dateTimePicker.Value.Value - this.initialDate;

            // Preview the changes
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
                    DateTime originalDateTime = DateTimeHandler.FromStandardDateTimeString((string)this.originalDate.Content);

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

        // Try to update the database if the OK button is clicked
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.dateTimePicker.Value.HasValue == false)
            {
                this.DialogResult = false;
                return;
            }

            // 1st real click of Ok: Show the preview before actually making any changes.
            if (this.displayingPreview == false)
            {
                this.PreviewDateTimeChanges();
                this.displayingPreview = true;
                return;
            }

            // 2nd click of Ok
            // Calculate and apply the date/time difference
            DateTime originalDateTime = DateTimeHandler.FromStandardDateTimeString((string)this.originalDate.Content);
            TimeSpan adjustment = this.dateTimePicker.Value.Value - originalDateTime;
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
            TimeSpan difference = this.dateTimePicker.Value.Value - this.initialDate;
            this.OkButton.IsEnabled = (difference == TimeSpan.Zero) ? false : true;
        }
    }
}
