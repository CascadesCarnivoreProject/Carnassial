using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Timelapse.Database;
using Timelapse.Util;

namespace Timelapse
{
    // TODO Modify this so it tells the user that they can't delete an image that is not there.
    // TODO Here and elsewhere, show the placeholder images scaled to the correct size. May have to keep width/height of original image to do this.

    /// <summary>
    /// This dialog box asks the user if he/she wants to delete the following images
    /// What actually happens is that the image is replaced by a 'dummy' placeholder image,
    /// and the original image is copied into a subfolder called Deleted.
    /// </summary>
    public partial class DialogDeleteImage : Window
    {
        // these variables will hold the values of the passed in parameters
        private ImageProperties imageProperties; // the filename of the current image
        private string imageFolderPath; // the full folder path where that image is located
        private bool deleteData;
        private ImageDatabase database;

        #region Public methods
        /// <summary>
        /// Ask the user if he/she wants to delete the given image indicated by the index.
        /// Other parameters indicate various specifics of that image that we will use to display and delete it
        /// </summary>
        public DialogDeleteImage(ImageDatabase databae, ImageProperties imageProperties, string imageFolderPath, bool deleteData)
        {
            this.InitializeComponent();

            // Set the local variables to the passed in parameters
            this.imageProperties = imageProperties;
            this.imageFolderPath = imageFolderPath;
            this.deleteData = deleteData;
            this.database = databae;

            if (this.deleteData == true)
            {
                this.textMain.Text = "Delete the current image file and its data: " + this.imageProperties; // put the file name in the title   
                this.TBDeleteImageAndData1.Visibility = Visibility.Visible;
                this.TBDeleteImageOnly1.Visibility = Visibility.Collapsed;
                this.TBDeleteImageOnly2.Visibility = Visibility.Collapsed;
                this.TBDeleteImageOnly3.Visibility = Visibility.Collapsed;
                this.LblImageOnly.Visibility = Visibility.Hidden;
                this.deletedImage.Visibility = Visibility.Hidden;
                this.chkboxConfirm.Visibility = Visibility.Visible;
                this.OkButton.IsEnabled = false;
            }
            else
            {
                this.textMain.Text = "Delete the current image file: " + this.imageProperties; // put the file name in the title  
                this.TBDeleteImageAndData1.Visibility = Visibility.Collapsed;
                this.TBDeleteImageOnly1.Visibility = Visibility.Visible;
                this.TBDeleteImageOnly2.Visibility = Visibility.Visible;
                this.TBDeleteImageOnly3.Visibility = Visibility.Visible;
                this.LblImageOnly.Visibility = Visibility.Visible;
                this.deletedImage.Visibility = Visibility.Visible;

                this.chkboxConfirm.Visibility = Visibility.Hidden;
                this.OkButton.IsEnabled = true;
            }
            this.deleteData = deleteData;
            this.ShowOriginalImage();
            this.ShowDeletedImage();
        }

        #endregion

        #region Private methods
        private void ConfirmBox_Checked(object sender, RoutedEventArgs e)
        {
            this.OkButton.IsEnabled = (bool)this.chkboxConfirm.IsChecked;
        }

        /// <summary>
        /// Cancel button selected
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        /// <summary>
        /// Ok button selected
        /// </summary>
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.MoveImageToBackupFolder();
            if (this.deleteData)
            {
                this.database.DeleteImage(this.database.CurrentImage.ID);
            }
            this.DialogResult = true;
        }

        /// <summary>
        /// Display the original image in the dialog box, or a placeholder if we cannot
        /// </summary>
        private void ShowOriginalImage()
        {
            // Get and display the bitmap
            BitmapImage image = new BitmapImage();
            image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;

            string imageFilePath = this.imageProperties.GetImagePath(this.imageFolderPath);
            if (this.imageProperties.ImageQuality == ImageQualityFilter.Corrupted)
            {
                // Show the corrupted iamge placeholder
                image = Utilities.BitmapFromResource(image, "corrupted.jpg", true);
            }
            else if (!File.Exists(imageFilePath))
            {
                image = Utilities.BitmapFromResource(image, "missing.jpg", true);
            }
            else
            {
                Utilities.BitmapFromFile(image, imageFilePath, true);
            }
            this.originalImage.Source = image;
        }

        /// <summary>
        /// Display the deleted image placeholder so the user knows what it looks like
        /// </summary>
        private void ShowDeletedImage()
        {
            // Get and display the bitmap
            BitmapImage image = new BitmapImage();
            image = Utilities.BitmapFromResource(image, "missing.jpg", true);
            this.deletedImage.Source = image;
        }

        /// <summary>
        /// Create a backup of the current image file in the backup folder
        /// </summary>
        private void MoveImageToBackupFolder()
        {
            string sourceFile = this.imageProperties.GetImagePath(this.imageFolderPath);
            string destFolder = Path.Combine(this.imageFolderPath, Constants.File.BackupFolder);
            string destFile = Path.Combine(destFolder, this.imageProperties.RelativeFolderPath);

            if (!File.Exists(sourceFile))
            {
                return;  // If there is no source file, its a missing file so we can't back it up
            }

            // Create a new target folder, if necessary.
            if (!Directory.Exists(destFolder))
            {
                Directory.CreateDirectory(destFolder);
            }

            // Move the image file to another location. 
            // This will overwrite the destination file  if it already exists .
            if (File.Exists(destFile))
            {
                File.Delete(destFile);
                File.Move(sourceFile, destFile);
            }
        }
        #endregion
    }
}
