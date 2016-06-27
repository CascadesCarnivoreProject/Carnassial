using System;
using System.Diagnostics;
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
    /// This dialog lets the user specify a corrected date and time of an file. All other dates and times are then corrected by the same amount.
    /// This is useful if (say) the camera was not initialized to the correct date and time.
    /// It assumes that Timelapse is configured to display all files, and the its currently displaying a valid file and thus a valid date.
    /// </summary>
    public partial class DialogDateCorrection : Window
    {
        private ImageDatabase database;
        private DateTime newDate;
        public bool Abort { get; set; }
        
        // Create the interface
        public DialogDateCorrection(ImageDatabase database, ImageRow imageToCorrect)
        {
            this.InitializeComponent();
            this.database = database;

            this.Abort = false;

            // Get the original date time and display it
            this.lblOriginalDate.Content = imageToCorrect.Date + " " + imageToCorrect.Time;

            // Get the image filename and display it
            this.lblImageName.Content = imageToCorrect.FileName;

            // Display the image. While we should be on a valid image (our assumption), we can still show a missing or corrupted image if needed
            this.imgDateImage.Source = imageToCorrect.LoadBitmap(this.database.FolderPath);

            // Configure the initial date of the date picker
            datePicker.Text = datePicker.DisplayDate.Date.ToString("dd-MMM-yyyy", System.Globalization.CultureInfo.InvariantCulture);

            // Try to put the original date / time into the corrected date field. If we can't, leave it as it is (i.e., as dd-mmm-yyyy hh:mm am).
            string format = "dd-MMM-yyyy HH:mm:ss";
            CultureInfo provider = CultureInfo.InvariantCulture;
            string dateAsString = this.lblOriginalDate.Content.ToString();
            try
            {
                // We expect all date formats to be in the above format, and if it isn't something is wrong and we should abort. 
                // While we could relax this to use other date formats, it reintroduces ambiguities e.g. the month/day uncertainty issue.
                datePicker.DisplayDate = DateTime.ParseExact(dateAsString, format, provider);
                datePicker.Text = datePicker.DisplayDate.Date.ToString("dd-MMM-yyyy", System.Globalization.CultureInfo.InvariantCulture);
                this.tbNewTime.Text = datePicker.DisplayDate.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (Exception exception)
            {
                Debug.Assert(false, String.Format("Parse or display of date '{0}' failed.", dateAsString), exception.ToString());
                DialogMessageBox messageBox = new DialogMessageBox();
                DialogMessageBox dlgMB = new DialogMessageBox();
                dlgMB.MessageTitle = "Timelapse could not read the date.";
                dlgMB.MessageProblem = "Timelapse could not read the date and time: " + dateAsString;
                dlgMB.MessageReason = "The date / time needs to be in a very specific format, for example, 01-Jan-2016 13:00:00.";
                dlgMB.MessageSolution = "Re-read in the dates from the images (see the Edit/Dates menu), and then try this again.";
                dlgMB.MessageResult = "Timelapse won't do anything for now.";
                dlgMB.ButtonType = MessageBoxButton.OK;
                dlgMB.IconType = MessageBoxImage.Error;
                dlgMB.ShowDialog();
                this.Abort = true;
                return;
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
                DateTime time = DateTime.Parse(this.tbNewTime.Text);
                DateTime correctedDateTime = this.newDate;
                correctedDateTime = new DateTime(correctedDateTime.Year, correctedDateTime.Month, correctedDateTime.Day, time.Hour, time.Minute, time.Second);
                TimeSpan timeDifference = correctedDateTime - originalDateTime;

                if (timeDifference == TimeSpan.Zero)
                {
                    this.DialogResult = false; // No difference, so nothing to correct
                }

                // Update the database
                this.database.AdjustImageTimes(timeDifference, 0, this.database.CurrentlySelectedImageCount); // For all rows...

                // Add an entry into the log detailing what we just did
                StringBuilder log = new StringBuilder(Environment.NewLine);
                log.AppendLine("System entry: Added a correction value to all dates.");
                log.AppendLine("                        Old sample date was: " + (string)this.lblOriginalDate.Content + " and new date is " + this.tbNewTime.Text);
                this.database.AppendToImageSetLog(log);

                // Refresh the database / datatable to reflect the updated values
                this.database.SelectDataTableImagesAll();
                this.DialogResult = true;
            }
            catch (Exception exception)
            {
                Debug.Assert(false, "Adjustment of image times failed.", exception.ToString());
                this.DialogResult = false;
                return;
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
            string format = "HH:mm:ss";
            CultureInfo provider = CultureInfo.InvariantCulture;
            string dateAsString = tbNewTime.Text;
            try
            {
                // We call ParseExact() as we expect this to strictly follow the format, as otherwise there could be screwups due to month/day ambiguity
                DateTime.ParseExact(dateAsString, format, provider);
                tblkStatus.Text = "\x221A"; // A checkmark

                BrushConverter bc = new BrushConverter();
                Brush brush;
                brush = (Brush)bc.ConvertFrom("#280EE800");
                this.tbNewTime.BorderBrush = brush;
                this.tblkStatus.Background = brush;

                this.OkButton.IsEnabled = true;
            }
            catch
            {
                // TODOSAUL: handle case where new time is left as hh:mm:ss
                // Debug.Assert(false, String.Format("Parse or display of date '{0}' failed.", dateAsString), exception.ToString());
                if (this.tblkStatus != null)
                {
                    // null check in case its not yet created
                    this.tblkStatus.Text = "x";

                    BrushConverter bc = new BrushConverter();
                    Brush brush;
                    brush = (Brush)bc.ConvertFrom("#46F50000");
                    this.tbNewTime.BorderBrush = brush;
                    this.tblkStatus.Background = brush;

                    this.OkButton.IsEnabled = false;
                }
            }
        }

        private void DatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            DatePicker dp = sender as DatePicker;
            DateTime? dateOrNull = dp.SelectedDate;
            if (dateOrNull != null)
            {
                this.newDate = dateOrNull.Value;
            }
        }
    }
}
