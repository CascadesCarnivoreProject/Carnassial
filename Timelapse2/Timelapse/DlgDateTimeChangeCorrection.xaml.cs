using System;
using System.Windows;
using System.Windows.Media.Imaging;
using System.IO;

namespace Timelapse
{
    /// <summary>
    /// Interaction logic for DlgTimeChangeCorrection.xaml
    /// THis dialog lets a user enter a time change correction of + / - 1 hour, which is propagated backwards/forwards 
    /// the current image as set by the user in the radio buttons.
    /// </summary>
    public partial class DlgDateTimeChangeCorrection : Window
    {

        private DBData dbData;

        public DlgDateTimeChangeCorrection(DBData db_data)
        {
            InitializeComponent();
            bool result;
            this.dbData = db_data;

            // Get the original date and display it
            this.dbData.GetIdOfCurrentRow();
            this.lblOriginalDate.Content = dbData.IDGetDate(out result) + " " + dbData.IDGetTime(out result);

            // Get the image filename and display it
            this.lblImageName.Content = dbData.IDGetFile(out result);

            // Display the image. While we should be on a valid image (our assumption), we can still show a missing or corrupted image if needed
            string path = System.IO.Path.Combine(dbData.FolderPath, dbData.IDGetFile(out result));
            BitmapFrame bmap;
            try
            {
                bmap = BitmapFrame.Create(new Uri(path), BitmapCreateOptions.None, BitmapCacheOption.None);
            }
            catch
            {
                if (! File.Exists  (path))
                    bmap = BitmapFrame.Create(new Uri("pack://application:,,/Resources/missing.jpg"));
                else
                    bmap = BitmapFrame.Create(new Uri("pack://application:,,/Resources/corrupted.jpg"));
            }
            this.imgDateImage.Source = bmap;
        }

        private void DlgDateCorrectionName_Loaded(object sender, RoutedEventArgs e)
        {
            // Make sure the title bar of the dialog box is on the screen. For small screens it may default to being off the screen
            if (this.Left < 10 || this.Top < 10)
            {
                this.Left = this.Owner.Left + (this.Owner.Width - this.ActualWidth) / 2; //Center it horizontally
                this.Top = this.Owner.Top + 20; // Offset it from the windows'top by 20 pixels downwards
            }
        }

        // When the user clicks ok, add/subtrack an hour propagated forwards/backwards as specified
        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int hours = ((bool)rbAddHour.IsChecked) ? 1 : -1;
                bool forward = ((bool)rbForward.IsChecked);

                string direction = (forward) ? "forward" : "backwards";
                string operation = (hours == 1) ? "added" : "subtracted";

                int initial = (forward) ? dbData.CurrentRow : 0;
                int final = (forward) ? dbData.dataTable.Rows.Count : dbData.CurrentRow + 1;

                TimeSpan ts = new TimeSpan(hours, 0, 0);
                // Update the database
                dbData.RowsUpdateAllDateTimeFieldsWithCorrectionValue(ts.Ticks, initial, final); //For all rows...

                // Add an entry into the log detailing what we just did
                this.dbData.Log += Environment.NewLine;
                this.dbData.Log += "System entry: Corrected for Daylight Saving Times.\n";
                this.dbData.Log += "                        Correction started at image " + this.lblImageName.Content + " and was propagated " + direction + "\n";
                this.dbData.Log += "                        An hour was " + operation + " to those images\n";

                // Refresh the database / datatable to reflect the updated values, which will also refressh the main timelpase display.
                int current_row = dbData.CurrentRow;
                dbData.GetImagesAll();
                dbData.CurrentRow = current_row;

                this.DialogResult = true;
            }
            catch
            {
                this.DialogResult = false;
            }
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        // Examine the checkboxes to see what state our selection is in, and provide feedback as appropriate
        private void rbButton_Checked(object sender, RoutedEventArgs e)
        {
            if ( (  (bool) rbAddHour.IsChecked  || (bool) rbSubtractHour.IsChecked) && ( (bool) rbBackwards.IsChecked  || (bool) rbForward.IsChecked))
            {
                DateTime dtTemp;
                bool succeeded = DateTime.TryParse(this.lblOriginalDate.Content.ToString(), out dtTemp);
                if (!succeeded)
                {
                    lblNewDate.Content = "Problem with this date..."; 
                    this.OkButton.IsEnabled = false;
                    return;
                }
                int hours = ((bool)rbAddHour.IsChecked) ? 1 : -1;
                dtTemp = dtTemp.AddHours(hours);
                lblNewDate.Content = DateTimeHandler.StandardDateString(dtTemp) + " " + DateTimeHandler.StandardTimeString(dtTemp);
                this.OkButton.IsEnabled = true;
            }
        }


    }
}
