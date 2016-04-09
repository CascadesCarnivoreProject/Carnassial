using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace Timelapse
{
    /// <summary>
    /// Interaction logic for DlgCheckModifyAmbiguousDates.xaml
    /// </summary>
    public partial class DlgDateModifyAmbiguousDates : Window
    {
        #region Public methods
        private DBData dbData;
        private int rangeStart = 0;
        private int rangeEnd = -1;
        public DlgDateModifyAmbiguousDates(DBData db_data)
        {

            InitializeComponent();
            this.dbData = db_data;

            // We add this in code behind as we don't want to invoke these callbacks when the interface is created.
            this.cboxOriginalDate.Checked +=cboxDate_Checked;
            this.cboxNewDate.Checked += cboxDate_Checked;

            // Find the first ambiguous date
            this.NextAmbiguousDate();
        }
        #endregion

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Make sure the title bar of the dialog box is on the screen. For small screens it may default to being off the screen
            if (this.Left < 10 || this.Top < 10)
            {
                this.Left = this.Owner.Left + (this.Owner.Width - this.ActualWidth) / 2; //Center it horizontally
                this.Top = this.Owner.Top + 20; // Offset it from the windows'top by 20 pixels downwards
            }
        }
        private bool NextAmbiguousDate()
        {
            int id = -1; // the ID of the record
            bool result;

            // The search will search inclusively from the given image number, and will return the first image number that is ambiguous, else -1
            this.rangeStart = this.GetNextAmbiguousDate(this.dbData, this.rangeStart);
            if (this.rangeStart >= 0)
            {
                // We found an ambiguous date; provide appropriate feedback
                id = dbData.RowGetID(this.rangeStart);
                if (id >= 0)
                {
                    string sdate = (string)this.dbData.IDGetDate(id, out result);
                    this.lblOriginalDate.Content = sdate;
                    this.lblNewDate.Content = DateTimeHandler.SwapSingleDayMonth(sdate);

                    this.rangeEnd = this.GetDateRangeWithSameDate(dbData, rangeStart);
                    this.lblNumberOfImagesWithSameDate.Content = " Images from the same day: ";
                    this.lblNumberOfImagesWithSameDate.Content += (this.rangeEnd - this.rangeStart + 1).ToString();  
                }
            } 
            else
            {
                // No dates are ambiguous; provide appropriate feedback
                this.btnNext.IsEnabled = false; // Disable the 'Next' button

                // Hide date-specific items so they are no longer visible on the screen
                this.lblOriginalDate.Visibility = Visibility.Hidden;
                this.lblNewDate.Visibility = Visibility.Hidden;
                this.cboxOriginalDate.Visibility = Visibility.Hidden;
                this.cboxNewDate.Visibility = Visibility.Hidden;
                this.lblImageName.Visibility = Visibility.Hidden;
                this.spImageArea.Visibility = Visibility.Hidden;
                this.label2.Visibility = Visibility.Hidden;
                this.label3.Visibility = Visibility.Hidden;
                this.lblNumberOfImagesWithSameDate.Content = "No ambiguous dates left";
                this.imgDateImage.Source = null;
            }
            //Now show the image that we are using as our sample
            if (this.rangeStart == -1) return false; //No valid image to show!

            // Get the image filename and display it
            string fname = (string)this.dbData.IDGetFile(id, out result);
            this.lblImageName.Content = fname;

            // Display the image. While we should be on a valid image (our assumption), we can still show a missing or corrupted image if needed
            string path = System.IO.Path.Combine(dbData.FolderPath, fname);
            BitmapFrame bmap;
            try
            {
                bmap = BitmapFrame.Create(new Uri(path), BitmapCreateOptions.None, BitmapCacheOption.None);
            }
            catch
            {
                if (!File.Exists(path))
                    bmap = BitmapFrame.Create(new Uri("pack://application:,,/Resources/missing.jpg"));
                else
                    bmap = BitmapFrame.Create(new Uri("pack://application:,,/Resources/corrupted.jpg"));
            }
            this.imgDateImage.Source = bmap;
            return true;
        }

        #region Callbacks
        private void cboxDate_Checked(object sender, RoutedEventArgs e)
        {
            RadioButton rbSender = sender as RadioButton;
            if (rbSender == cboxNewDate)
            {
                this.dbData.RowsUpdateSwapDayMonth(this.rangeStart, this.rangeEnd);
            }
            else
            {
                this.dbData.RowsUpdateSwapDayMonth(this.rangeStart, this.rangeEnd);
            }
        }

        // If the user click ok, then exit
        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            //this.dbData.RowsUpdateSwapDayMonth();
            this.DialogResult = true;

            // Refresh the database / datatable to reflect the updated values, which will also refressh the main timelpase display.
            int current_row = dbData.CurrentRow;
            dbData.GetImagesAll();
            dbData.CurrentRow = current_row;
        }

        private void btnNext_Click(object sender, RoutedEventArgs e)
        {
            this.rangeStart = this.rangeEnd + 1; // Go to the next image
            this.NextAmbiguousDate();

            // Set the radio button back to the orginal date (default)
            // As we do this, unlink and then relink the callback as we don't want to invoke the data update
            this.cboxOriginalDate.Checked -= cboxDate_Checked;
            this.cboxOriginalDate.IsChecked = true;
            this.cboxOriginalDate.Checked += cboxDate_Checked;
        }


        #endregion

        #region Private Methods: GetNextAmbiguousDate
        private int GetNextAmbiguousDate(DBData dbData, int startIndex)
        {
            DateTime date;
            bool succeeded = true;
            string sdate;

            // Starting from the index, get the date from successive rows and see if the date is ambiguous
            // Note that if the index is out of range, it will return -1, so that's ok.
            for (int index = startIndex; index < dbData.dataTable.Rows.Count; index++)
            {
                // Ignore corrupted images
                // if (dbData.RowIsImageCorrupted(i)) continue;

                // Parse the date. Note that this should never fail at this point, but just in case, put out a debug message
                sdate = (string)dbData.dataTable.Rows[index][Constants.DATE] + " " + (string)dbData.dataTable.Rows[index][Constants.TIME];
                succeeded = DateTime.TryParse(sdate, out date);
                if (succeeded)
                {
                    if (date.Day < 13 && date.Month < 13)
                        return (index); // If the date is ambiguous, return the row index. 
                }
                else
                {
                    System.Diagnostics.Debug.Print("In SwapDayMonth - something went wrong trying to parse a date!");
                }
            }
            return -1; //-1 means all dates are fine
        }
        #endregion

        #region Private Methods:GetDateRangeWithSameDate

        // Given a starting index, find its date and then go through the successive images untilthe date differs.
        // That is, return the final image that is dated the same date as this image
        private int GetDateRangeWithSameDate(DBData dbData, int startIndex)
        {
            DateTime dStartingDate;
            DateTime dCurrentDate;
            string sStartingDate;
            string sCurrentDate;

            if (startIndex >= dbData.dataTable.Rows.Count) return -1;   // Make sure index is in range.

            // Parse the provided starting date. Note that this should never fail at this point, but just in case, put out a debug message
            sStartingDate = (string)dbData.dataTable.Rows[startIndex][Constants.DATE] + " " + (string)dbData.dataTable.Rows[startIndex][Constants.TIME];
            if (!DateTime.TryParse(sStartingDate, out dStartingDate)) return -1; // Should never fail, but just in case.

            for (int index = startIndex + 1; index < dbData.dataTable.Rows.Count; index++)
            {
                // Parse the date for the given record.
                sCurrentDate = (string)dbData.dataTable.Rows[index][Constants.DATE] + " " + (string)dbData.dataTable.Rows[index][Constants.TIME];
                if (!DateTime.TryParse(sCurrentDate, out dCurrentDate)) return (index - 1); // If we can't parse the date, the return the previous date 
                if (dStartingDate.Day == dCurrentDate.Day && dStartingDate.Month == dCurrentDate.Month && dStartingDate.Year == dCurrentDate.Year)
                {
                    continue;
                }
                else
                {
                    return index - 1;
                }
            }
            return dbData.dataTable.Rows.Count - 1; //if we got here, it means that we arrived at the end of the records
        }
        #endregion


    }
}
