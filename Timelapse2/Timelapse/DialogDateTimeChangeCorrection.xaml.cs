using System;
using System.Text;
using System.Windows;
using Timelapse.Database;
using Timelapse.Util;

namespace Timelapse
{
    /// <summary>
    /// Interaction logic for DialogTimeChangeCorrection.xaml
    /// This dialog lets a user enter a time change correction of + / - 1 hour, which is propagated backwards/forwards 
    /// the current image as set by the user in the radio buttons.
    /// </summary>
    public partial class DialogDateTimeChangeCorrection : Window
    {
        private int currentImageRow;
        private ImageDatabase database;

        public DialogDateTimeChangeCorrection(ImageDatabase database, ImageTableEnumerator image)
        {
            this.InitializeComponent();
            this.database = database;
            this.currentImageRow = image.CurrentRow;

            // Get the original date and display it
            this.lblOriginalDate.Content = image.Current.Date + " " + image.Current.Time;

            // Display the image. While we should be on a valid image (our assumption), we can still show a missing or corrupted image if needed
            this.imgDateImage.Source = image.Current.LoadWriteableBitmap(this.database.FolderPath);
            this.lblImageName.Content = image.Current.FileName;
        }

        private void DlgDateCorrectionName_Loaded(object sender, RoutedEventArgs e)
        {
            // Make sure the title bar of the dialog box is on the screen. For small screens it may default to being off the screen
            if (this.Left < 10 || this.Top < 10)
            {
                this.Left = this.Owner.Left + (this.Owner.Width - this.ActualWidth) / 2; // Center it horizontally
                this.Top = this.Owner.Top + 20; // Offset it from the windows'top by 20 pixels downwards
            }
        }

        // When the user clicks ok, add/subtract an hour propagated forwards/backwards as specified
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int hours = (bool)rbAddHour.IsChecked ? 1 : -1;
                bool forward = (bool)rbForward.IsChecked;

                string direction = forward ? "forward" : "backwards";
                string operation = hours == 1 ? "added" : "subtracted";

                int initial = forward ? this.currentImageRow : 0;
                int final = forward ? this.database.CurrentlySelectedImageCount : this.currentImageRow + 1;

                TimeSpan timeDifference = new TimeSpan(hours, 0, 0);
                // Update the database
                this.database.AdjustAllImageTimes(timeDifference, initial, final); // For all rows...

                // Add an entry into the log detailing what we just did
                StringBuilder log = new StringBuilder(Environment.NewLine);
                log.AppendLine("System entry: Corrected for Daylight Saving Times.");
                log.AppendLine("                        Correction started at image " + this.lblImageName.Content + " and was propagated " + direction);
                log.AppendLine("                        An hour was " + operation + " to those images");
                this.database.AppendToImageSetLog(log);

                // Refresh the database / datatable to reflect the updated values
                this.database.TryGetImagesAll();
                this.DialogResult = true;
            }
            catch
            {
                this.DialogResult = false;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        // Examine the checkboxes to see what state our selection is in, and provide feedback as appropriate
        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (((bool)rbAddHour.IsChecked || (bool)rbSubtractHour.IsChecked) && ((bool)rbBackwards.IsChecked || (bool)rbForward.IsChecked))
            {
                DateTime dateTime;
                bool succeeded = DateTime.TryParse(this.lblOriginalDate.Content.ToString(), out dateTime);
                if (!succeeded)
                {
                    lblNewDate.Content = "Problem with this date...";
                    this.OkButton.IsEnabled = false;
                    return;
                }
                int hours = ((bool)rbAddHour.IsChecked) ? 1 : -1;
                lblNewDate.Content = DateTimeHandler.StandardDateString(dateTime) + " " + DateTimeHandler.StandardTimeString(dateTime);
                this.OkButton.IsEnabled = true;
            }
        }
    }
}
