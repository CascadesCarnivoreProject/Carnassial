using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Timelapse.Database;
using Timelapse.Util;

namespace Timelapse
{
    /// <summary>
    /// Interaction logic for DlgTimeChangeCorrection.xaml
    /// This dialog lets a user enter a time change correction of + / - 1 hour, which is propagated backwards/forwards 
    /// the current image as set by the user in the radio buttons.
    /// </summary>
    public partial class DialogDateTimeChangeCorrection : Window
    {
        private ImageDatabase database;

        public DialogDateTimeChangeCorrection(ImageDatabase database)
        {
            this.InitializeComponent();
            this.database = database;

            // Get the original date and display it
            this.database.GetIdOfCurrentRow();
            bool result;
            this.lblOriginalDate.Content = this.database.IDGetDate(out result) + " " + this.database.IDGetTime(out result);

            // Get the image filename and display it
            this.lblImageName.Content = this.database.IDGetFile(out result);

            // Display the image. While we should be on a valid image (our assumption), we can still show a missing or corrupted image if needed
            string path = Path.Combine(this.database.FolderPath, this.database.IDGetFile(out result));
            BitmapFrame bmap;
            try
            {
                bmap = BitmapFrame.Create(new Uri(path), BitmapCreateOptions.None, BitmapCacheOption.None);
            }
            catch
            {
                if (!File.Exists(path))
                {
                    bmap = BitmapFrame.Create(new Uri("pack://application:,,/Resources/missing.jpg"));
                }
                else
                {
                    bmap = BitmapFrame.Create(new Uri("pack://application:,,/Resources/corrupted.jpg"));
                }
            }
            this.imgDateImage.Source = bmap;
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

        // When the user clicks ok, add/subtrack an hour propagated forwards/backwards as specified
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int hours = (bool)rbAddHour.IsChecked ? 1 : -1;
                bool forward = (bool)rbForward.IsChecked;

                string direction = forward ? "forward" : "backwards";
                string operation = hours == 1 ? "added" : "subtracted";

                int initial = forward ? this.database.CurrentRow : 0;
                int final = forward ? this.database.DataTable.Rows.Count : this.database.CurrentRow + 1;

                TimeSpan ts = new TimeSpan(hours, 0, 0);
                // Update the database
                this.database.RowsUpdateAllDateTimeFieldsWithCorrectionValue(ts.Ticks, initial, final); // For all rows...

                // Add an entry into the log detailing what we just did
                this.database.Log += Environment.NewLine;
                this.database.Log += "System entry: Corrected for Daylight Saving Times.\n";
                this.database.Log += "                        Correction started at image " + this.lblImageName.Content + " and was propagated " + direction + "\n";
                this.database.Log += "                        An hour was " + operation + " to those images\n";

                // Refresh the database / datatable to reflect the updated values, which will also refressh the main timelpase display.
                int current_row = this.database.CurrentRow;
                this.database.GetImagesAll();
                this.database.CurrentRow = current_row;

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
