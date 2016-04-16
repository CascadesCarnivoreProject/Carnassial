using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Timelapse.Database;
using Timelapse.Images;

namespace Timelapse
{
    /// <summary>
    /// Interaction logic for DialogDateCorrection.xaml
    /// This dialog lets the user specify a corrected date and time of an image. All other image dates and times are then correctes by the same amount.
    /// This is useful if (say) the camera was not initialized to the correct date and time.
    /// It assumes that Timelapse is configured to display all images, and the its currently displayiing a valid image (and thus a valid date)
    /// </summary>
    public partial class DialogDateCorrection : Window
    {
        private ImageDatabase database;

        // Create the interface
        public DialogDateCorrection(ImageDatabase database)
        {
            this.InitializeComponent();
            this.database = database;
            // Get the original date and display it
            bool result;
            this.lblOriginalDate.Content = this.database.IDGetDate(out result) + " " + this.database.IDGetTime(out result);

            // Get the image filename and display it
            this.lblImageName.Content = this.database.IDGetFile(out result);

            // Display the image. While we should be on a valid image (our assumption), we can still show a missing or corrupted image if needed
            ImageProperties imageProperties = new ImageProperties();
            imageProperties.File = this.database.IDGetFile(out result);
            imageProperties.Folder = this.database.IDGetFolder(out result);
            string path = imageProperties.GetImagePath(this.database.FolderPath);
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

            // Try to put the original date / time into the corrected date field. If we can't, leave it as it is (i.e., as dd-mmm-yyyy hh:mm am).
            string format = "dd-MMM-yyyy hh:mm tt";
            CultureInfo provider = CultureInfo.InvariantCulture;
            string dateAsString = this.lblOriginalDate.Content.ToString();
            try
            {
                // TODO: Saul  why call ParseExact() here?
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
                long ticks_difference = correctedDateTime.Ticks - originalDateTime.Ticks;

                if (ticks_difference == 0)
                {
                    return; // No difference, so nothing to correct
                }

                // Update the database
                this.database.RowsUpdateAllDateTimeFieldsWithCorrectionValue(ticks_difference, 0, this.database.DataTable.Rows.Count); // For all rows...

                // Add an entry into the log detailing what we just did
                this.database.Log += Environment.NewLine;
                this.database.Log += "System entry: Added a correction value to all dates.\n";
                this.database.Log += "                        Old sample date was: " + (string)this.lblOriginalDate.Content + " and new date is " + this.tbNewDate.Text + "\n";

                // Refresh the database / datatable to reflect the updated values, which will also refressh the main timelpase display.
                int current_row = this.database.CurrentRow;
                this.database.GetImagesAll();
                this.database.CurrentRow = current_row;
                this.database.CurrentId = this.database.GetIdOfCurrentRow();
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
            string format = "dd-MMM-yyyy hh:mm tt";
            CultureInfo provider = CultureInfo.InvariantCulture;
            string dateAsString = tbNewDate.Text;
            try
            {
                // TODO: Saul  why call ParseExact() here?
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
