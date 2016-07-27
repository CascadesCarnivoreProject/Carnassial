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
            DateTime initialValue;
            if (imageToCorrect.TryGetDateTime(out initialValue))
            {
                this.originalDate.Content = DateTimeHandler.ToStandardDateTimeString(initialValue);
                this.dateTimePicker.Format = DateTimeFormat.Custom;
                this.dateTimePicker.FormatString = Constants.Time.DateTimeFormat;
                this.dateTimePicker.TimeFormat = DateTimeFormat.Custom;
                this.dateTimePicker.TimeFormatString = Constants.Time.TimeFormat;
                this.dateTimePicker.Value = initialValue;
                this.dateTimePicker.ValueChanged += this.DateTimePicker_ValueChanged;
            }
            else
            {
                DateTimeHandler.ShowDateTimeParseFailureDialog(imageToCorrect, this);
                this.Abort = true;
                return;
            }
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

            // Calculate the date/time difference
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
            this.OkButton.IsEnabled = true;
        }
    }
}
