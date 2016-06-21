using System;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Timelapse.Database;

namespace Timelapse
{
    /// <summary>
    /// Interaction logic for DialogDateCorrection.xaml
    /// This dialog lets the user specify a corrected date and time of an image. All other image dates and times are then corrected by the same amount.
    /// This is useful if (say) the camera was not initialized to the correct date and time.
    /// It assumes that Timelapse is configured to display all images, and the its currently displaying a valid image (and thus a valid date)
    /// </summary>
    public partial class DialogDateCorrection : Window
    {
        private ImageDatabase database;

        // Create the interface
        public DialogDateCorrection(ImageDatabase database, ImageRow imageToCorrect)
        {
            this.InitializeComponent();
            this.database = database;

            // Get the original date time and display it
            this.lblOriginalDate.Content = imageToCorrect.Date + " " + imageToCorrect.Time;

            // Get the image filename and display it
            this.lblImageName.Content = imageToCorrect.FileName;

            // Display the image. While we should be on a valid image (our assumption), we can still show a missing or corrupted image if needed
            this.imgDateImage.Source = imageToCorrect.LoadWriteableBitmap(this.database.FolderPath);

            // Try to put the original date / time into the corrected date field. If we can't, leave it as it is (i.e., as dd-mmm-yyyy hh:mm am).
            string format = "dd-MMM-yyyy HH:mm:ss";
            CultureInfo provider = CultureInfo.InvariantCulture;
            string dateAsString = this.lblOriginalDate.Content.ToString();
            try
            {
                // TODOSAUL: why call ParseExact() here?
                DateTime.ParseExact(dateAsString, format, provider);
                this.tbNewDate.Text = this.lblOriginalDate.Content.ToString();
            }
            catch
            {
            }
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

        // Try to update the database if the OK button is clicked
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Calculate the date/time difference
                DateTime originalDateTime = DateTime.Parse((string)this.lblOriginalDate.Content);
                DateTime correctedDateTime = DateTime.Parse(this.tbNewDate.Text);
                TimeSpan timeDifference = correctedDateTime - originalDateTime;

                if (timeDifference == TimeSpan.Zero)
                {
                    return; // No difference, so nothing to correct
                }

                // Update the database
                this.database.AdjustAllImageTimes(timeDifference, 0, this.database.CurrentlySelectedImageCount); // For all rows...

                // Add an entry into the log detailing what we just did
                StringBuilder log = new StringBuilder(Environment.NewLine);
                log.AppendLine("System entry: Added a correction value to all dates.");
                log.AppendLine("                        Old sample date was: " + (string)this.lblOriginalDate.Content + " and new date is " + this.tbNewDate.Text);
                this.database.AppendToImageSetLog(log);

                // Refresh the database / datatable to reflect the updated values
                this.database.TryGetImages(ImageQualityFilter.All);
                this.DialogResult = true;
            }
            catch
            {
                this.DialogResult = false;
            }
        }

        // Cancel - do nothing
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        // Update the little checkbox to indicate if the date entered is in a correct format
        // We could avoid all this if we used a proper date-time picker, but .NET 4 only has a date picker.
        private void NewDate_TextChanged(object sender, TextChangedEventArgs e)
        {
            string format = "dd-MMM-yyyy HH:mm:ss";
            CultureInfo provider = CultureInfo.InvariantCulture;
            string dateAsString = tbNewDate.Text;
            try
            {
                // TODOSAUL: why call ParseExact() here?
                DateTime.ParseExact(dateAsString, format, provider);
                tblkStatus.Text = "\x221A"; // A checkmark

                BrushConverter bc = new BrushConverter();
                Brush brush;
                brush = (Brush)bc.ConvertFrom("#280EE800");
                this.tbNewDate.BorderBrush = brush;
                this.tblkStatus.Background = brush;

                this.OkButton.IsEnabled = true;
            }
            catch
            {
                if (this.tblkStatus != null)
                {
                    // null check in case its not yet created
                    this.tblkStatus.Text = "x";

                    BrushConverter bc = new BrushConverter();
                    Brush brush;
                    brush = (Brush)bc.ConvertFrom("#46F50000");
                    this.tbNewDate.BorderBrush = brush;
                    this.tblkStatus.Background = brush;

                    this.OkButton.IsEnabled = false;
                }
            }
        }
    }
}
