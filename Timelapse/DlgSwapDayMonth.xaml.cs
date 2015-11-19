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
using System.Diagnostics;
using System.IO;
using System.Globalization;

namespace Timelapse
{
    /// <summary>
    /// Interaction logic for DlgSwapDayMonth.xaml
    /// This Dialog box lets the user swap the day and months across all images.
    /// However, if this isn't doable (because a day field > 12) appropriate feedback is provided
    /// </summary>
    public partial class DlgSwapDayMonth : Window
    {
        private DBData dbData;

        #region Public methods
        public DlgSwapDayMonth(DBData db_data)
        {
            int id;
            bool result;

            InitializeComponent();
            this.dbData = db_data;

            // imgNumber will point to the first image  that is not swappable, else -1
            int imgNumber = DateTimeHandler.SwapDayMonthIsPossible(this.dbData);
            id = dbData.RowGetID(imgNumber);
            if (id >= 0)
            {
                // We can't swap the dates; provide appropriate feedback
                this.StackPanelCorrect.Visibility = Visibility.Collapsed;
                this.StackPanelError.Visibility = Visibility.Visible;
                this.OkButton.Visibility = Visibility.Collapsed;

                this.lblOriginalDate.Content = this.dbData.IDGetDate(id, out result);
                this.lblNewDate.Content = "No valid date possible";
            }
            else
            {
                // We can swap the dates; provide appropriate feedback
                imgNumber = dbData.RowFindNextDisplayableImage (0);
                id = dbData.RowGetID(imgNumber);
                if (id >= 0)
                {
                    string sdate = (string) this.dbData.IDGetDate (id, out result);
                    this.lblOriginalDate.Content = sdate;
                    this.lblNewDate.Content = DateTimeHandler.SwapSingleDayMonth(sdate);
                }
            }


            /// Now show the image that we are using as our sample
            if (imgNumber == -1) return; //No valid image to show!

            // Get the image filename and display it
            string fname = (string) this.dbData.IDGetFile (id, out result);
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
        }
        #endregion

        #region Private methods

        // If the user click ok, swap the day and month field
        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            this.dbData.RowsUpdateSwapDayMonth();
            this.dbData.Log += "System entry: Swapped the days and months for all dates.\n";
            this.dbData.Log += "                       Old sample date was: " + this.lblOriginalDate.Content + " and new date is " + this.lblNewDate.Content + "\n";
            this.DialogResult = true;

            // Refresh the database / datatable to reflect the updated values, which will also refressh the main timelpase display.
            int current_row = dbData.CurrentRow;
            dbData.GetImagesAll();
            dbData.CurrentRow = current_row;
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        #endregion
    }
}
