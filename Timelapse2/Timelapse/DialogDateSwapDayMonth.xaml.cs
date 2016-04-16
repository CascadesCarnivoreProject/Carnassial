using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Timelapse.Database;
using Timelapse.Util;

namespace Timelapse
{
    /// <summary>
    /// Interaction logic for DialogSwapDayMonth.xaml
    /// This Dialog box lets the user swap the day and months across all images.
    /// However, if this isn't doable (because a day field > 12) appropriate feedback is provided
    /// </summary>
    public partial class DialogDateSwapDayMonth : Window
    {
        private ImageDatabase database;

        #region Public methods
        public DialogDateSwapDayMonth(ImageDatabase database)
        {
            this.InitializeComponent();
            this.database = database;

            // imgNumber will point to the first image  that is not swappable, else -1
            int imgNumber = DateTimeHandler.SwapDayMonthIsPossible(this.database);
            int id = this.database.RowGetID(imgNumber);
            bool result;
            if (id >= 0)
            {
                // We can't swap the dates; provide appropriate feedback
                this.StackPanelCorrect.Visibility = Visibility.Collapsed;
                this.StackPanelError.Visibility = Visibility.Visible;
                this.OkButton.Visibility = Visibility.Collapsed;

                this.lblOriginalDate.Content = this.database.IDGetDate(id, out result);
                this.lblNewDate.Content = "No valid date possible";
            }
            else
            {
                // We can swap the dates; provide appropriate feedback
                imgNumber = this.database.RowFindNextDisplayableImage(0);
                id = this.database.RowGetID(imgNumber);
                if (id >= 0)
                {
                    string sdate = (string)this.database.IDGetDate(id, out result);
                    this.lblOriginalDate.Content = sdate;
                    this.lblNewDate.Content = DateTimeHandler.SwapSingleDayMonth(sdate);
                }
            }

            // Now show the image that we are using as our sample
            if (imgNumber == -1)
            {
                return; // No valid image to show!
            }

            // Get the image filename and display it
            string fname = (string)this.database.IDGetFile(id, out result);
            this.lblImageName.Content = fname;

            // Display the image. While we should be on a valid image (our assumption), we can still show a missing or corrupted image if needed
            string path = System.IO.Path.Combine(this.database.FolderPath, fname);
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
        #endregion

        #region Private methods
        private void DlgSwapDayMonth_Loaded(object sender, RoutedEventArgs e)
        {
            // Make sure the title bar of the dialog box is on the screen. For small screens it may default to being off the screen
            if (this.Left < 10 || this.Top < 10)
            {
                this.Left = this.Owner.Left + (this.Owner.Width - this.ActualWidth) / 2; // Center it horizontally
                this.Top = this.Owner.Top + 20; // Offset it from the windows'top by 20 pixels downwards
            }
        }

        // If the user click ok, swap the day and month field
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.database.RowsUpdateSwapDayMonth();
            this.database.Log += "System entry: Swapped the days and months for all dates.\n";
            this.database.Log += "                       Old sample date was: " + this.lblOriginalDate.Content + " and new date is " + this.lblNewDate.Content + "\n";
            this.DialogResult = true;

            // Refresh the database / datatable to reflect the updated values, which will also refressh the main timelpase display.
            int current_row = this.database.CurrentRow;
            this.database.GetImagesAll();
            this.database.CurrentRow = current_row;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
        #endregion
    }
}
