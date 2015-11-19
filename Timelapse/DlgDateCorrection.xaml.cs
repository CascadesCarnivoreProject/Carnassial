using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Globalization;
using System.IO;
using System.Diagnostics;

namespace Timelapse
{
    /// <summary>
    /// Interaction logic for DlgDateCorrection.xaml
    /// This dialog lets the user specify a corrected date and time of an image. All other image dates and times are then correctes by the same amount.
    /// This is useful if (say) the camera was not initialized to the correct date and time.
    /// It assumes that Timelapse is configured to display all images, and the its currently displayiing a valid image (and thus a valid date)
    /// </summary>
    public partial class DlgDateCorrection : Window
    {
        DBData dbData;

        // Create the interface
        public DlgDateCorrection(DBData db_data)
        {
            InitializeComponent();
            this.dbData = db_data;
            bool result;
            // Get the original date and display it
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

            // Try to put the original date / time into the corrected date field. If we can't, leave it as it is (i.e., as dd-mmm-yyyy hh:mm am).
            string format = "dd-MMM-yyyy hh:mm tt";
            CultureInfo provider = CultureInfo.InvariantCulture;
            string sDate = this.lblOriginalDate.Content.ToString();
            try
            {
                DateTime.ParseExact(sDate, format, provider);
                this.tbNewDate.Text = this.lblOriginalDate.Content.ToString();
            }
            catch { };           
        }

        // Try to update the database if the OK button is clicked
        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Calculate the date/time difference
                DateTime dtOriginal = DateTime.Parse((string)this.lblOriginalDate.Content);
                DateTime dtCorrected = DateTime.Parse(this.tbNewDate.Text);
                long ticks_difference = dtCorrected.Ticks - dtOriginal.Ticks;

                if (ticks_difference == 0) return; // No difference, so nothing to correct

                // Update the database
                dbData.RowsUpdateAllDateTimeFieldsWithCorrectionValue(ticks_difference, 0, dbData.dataTable.Rows.Count); //For all rows...

                // Add an entry into the log detailing what we just did
                this.dbData.Log += Environment.NewLine;
                this.dbData.Log += "System entry: Added a correction value to all dates.\n";
                this.dbData.Log += "                        Old sample date was: " + (string)this.lblOriginalDate.Content + " and new date is " + this.tbNewDate.Text + "\n";

                // Refresh the database / datatable to reflect the updated values, which will also refressh the main timelpase display.
                int current_row = dbData.CurrentRow;
                dbData.GetImagesAll();
                dbData.CurrentRow = current_row;
                dbData.CurrentId = dbData.GetIdOfCurrentRow();
                this.DialogResult = true;
            }
            catch
            {
                this.DialogResult = false;
            }
        }

        // Cancel - do nothing
        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        // Update the little checkbox to indicate if the date entered is in a correct format
        // We could avoid all this if we used a proper date-time picker, but .NET 4 only has a date picker.
        private void tbNewDate_TextChanged(object sender, TextChangedEventArgs e)
        {
            string format = "dd-MMM-yyyy hh:mm tt";
            CultureInfo provider = CultureInfo.InvariantCulture;
            string sDate = tbNewDate.Text;
            try
            {
                DateTime.ParseExact(sDate, format, provider);
                tblkStatus.Text = "\x221A"; // A checkmark

                BrushConverter bc = new BrushConverter();
                Brush brush;
                brush = (Brush)bc.ConvertFrom("#280EE800");
                tbNewDate.BorderBrush = brush;
                tblkStatus.Background = brush;

                this.OkButton.IsEnabled = true;
            } catch
            {
                if (tblkStatus != null)  // null check in case its not yet created
                {
                    tblkStatus.Text = "x";

                    BrushConverter bc = new BrushConverter();
                    Brush brush;
                    brush = (Brush)bc.ConvertFrom("#46F50000");
                    tbNewDate.BorderBrush = brush;
                    tblkStatus.Background = brush;

                    this.OkButton.IsEnabled = false;
                }
            }
        }
    }
}
