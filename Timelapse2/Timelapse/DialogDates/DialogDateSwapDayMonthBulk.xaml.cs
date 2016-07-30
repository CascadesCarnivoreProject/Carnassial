using System;
using System.IO;
using System.Windows;
using Timelapse.Database;
using Timelapse.Util;

namespace Timelapse
{
    /// <summary>
    /// Interaction logic for DialogDateSwapDayMonthBulk.xaml
    /// This Dialog box lets the user swap the day and months across all images.
    /// However, if this isn't doable (because a day field > 12) appropriate feedback is provided
    /// </summary>
    public partial class DialogDateSwapDayMonthBulk : Window
    {
        private ImageDatabase database;

        public DialogDateSwapDayMonthBulk(ImageDatabase database)
        {
            this.InitializeComponent();
            this.database = database;
            
            // imgNumber will point to the first image that is not swappable, else -1
            ImageRow imageProperties = null;
            int indexOfFirstNotSwappableImage = DateTimeHandler.SwapDayMonthIsPossible(this.database);
            if (indexOfFirstNotSwappableImage != -1)
            {
                // We can't swap the dates; provide appropriate feedback
                this.Message.Icon = MessageBoxImage.Exclamation;
                this.Message.Title = "Cannot Swap the Date's Day and Month";
                this.Message.Solution = "Unfortunately, no general solution is possible with this image set." + Environment.NewLine;
                this.Message.Solution += "At least one of the days is greater than 12, which means that there is no valid matching month." + Environment.NewLine;
                this.Message.Solution += "The image below is the first example of this found in this image set.";
                this.Message.Hint = "\u2022 Check to see if the dates in this image set were really reversed, or if its due to some other issue." + Environment.NewLine;
                this.Message.Hint += "\u2022 You can correct individual dates using the 'Check and Modify Ambiguous Dates menu option";
                this.Message.Result = String.Empty;
                this.OkButton.Visibility = Visibility.Collapsed;

                imageProperties = this.database.ImageDataTable[indexOfFirstNotSwappableImage];
                this.lblOriginalDate.Content = imageProperties.Date;
                this.lblNewDate.Content = "No valid date possible";
                this.lblImageName.Content = Path.GetFileName(imageProperties.FileName);
            }
            else
            {
                // We can swap the dates; provide appropriate feedback
                int indexOfFirstDisplayableImage = this.database.FindFirstDisplayableImage(Constants.DefaultImageRowIndex);
                if (indexOfFirstDisplayableImage != -1)
                {
                    imageProperties = this.database.ImageDataTable[indexOfFirstDisplayableImage];
                    this.lblOriginalDate.Content = imageProperties.Date;
                    string swappedDate;
                    this.lblNewDate.Content = DateTimeHandler.TrySwapSingleDayMonth(imageProperties.Date, out swappedDate) ? swappedDate : imageProperties.Date;
                }
            }

            // Now show the image that we are using as our sample
            if (imageProperties == null)
            {
                return; // No displayable, wappable images
            }

            // Display the image. While we should be on a valid image (our assumption), we can still show a missing or corrupted image if needed
            this.imgDateImage.Source = imageProperties.LoadBitmap(this.database.FolderPath);
            this.lblImageName.Content = Path.GetFileName(imageProperties.FileName);
        }

        private void DlgSwapDayMonth_Loaded(object sender, RoutedEventArgs e)
        {
            Utilities.SetDefaultDialogPosition(this);
            Utilities.TryFitWindowInWorkingArea(this);
        }

        // If the user click ok, swap the day and month field for all images selected by the current filter
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.database.ExchangeDayAndMonthInImageDates();
            this.DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
    }
}
