using System;
using System.Text;
using System.Windows;
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
            ImageProperties imageProperties = null;
            int imgNumber = DateTimeHandler.SwapDayMonthIsPossible(this.database);
            int id = this.database.GetImageID(imgNumber);
            if (id >= 0)
            {
                // We can't swap the dates; provide appropriate feedback
                this.StackPanelCorrect.Visibility = Visibility.Collapsed;
                this.StackPanelError.Visibility = Visibility.Visible;
                this.OkButton.Visibility = Visibility.Collapsed;

                imageProperties = this.database.FindImageByID(id);
                this.lblOriginalDate.Content = imageProperties.Date;
                this.lblNewDate.Content = "No valid date possible";
            }
            else
            {
                // We can swap the dates; provide appropriate feedback
                imgNumber = this.database.FindFirstDisplayableImage(Constants.DefaultImageRowIndex);
                id = this.database.GetImageID(imgNumber);
                if (id >= 0)
                {
                    imageProperties = this.database.FindImageByID(id);
                    this.lblOriginalDate.Content = imageProperties.Date;
                    this.lblNewDate.Content = DateTimeHandler.SwapSingleDayMonth(imageProperties.Date);
                }
            }

            // Now show the image that we are using as our sample
            if (imageProperties == null)
            {
                return; // No valid image to show!
            }

            // Display the image. While we should be on a valid image (our assumption), we can still show a missing or corrupted image if needed
            this.imgDateImage.Source = imageProperties.LoadWriteableBitmap(this.database.FolderPath);
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

        // If the user click ok, swap the day and month field for all images selected by the current filter
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.database.ExchangeDayAndMonthInImageDate();
            StringBuilder log = new StringBuilder();
            log.AppendLine("System entry: Swapped the days and months for all dates.");
            log.AppendLine("                       Old sample date was: " + this.lblOriginalDate.Content + " and new date is " + this.lblNewDate.Content);
            this.database.AppendToImageSetLog(log);

            // Refresh the database / datatable to reflect the updated values
            this.database.TryGetImages(ImageQualityFilter.All);
            this.DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
        #endregion
    }
}
