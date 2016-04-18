using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
    public partial class DialogDeleteImages : Window
    {
        // these variables will hold the values of the passed in parameters
        private string imageFolderPath; // the full folder path where that image is located
        private DataTable deletedImageTable;
        private bool deleteData;
        private ImageDatabase database;

        #region Public methods
        /// <summary>
        /// Ask the user if he/she wants to delete the given image indicated by the index.
        /// Other parameters indicate various specifics of that image that we will use to display and delete it
        /// </summary>
        public DialogDeleteImages(ImageDatabase database, DataTable deletedImageTable, string imageFolderPath, bool deleteData)
        {
            this.InitializeComponent();
            Mouse.OverrideCursor = Cursors.Wait;
            this.deletedImageTable = deletedImageTable;
            this.imageFolderPath = imageFolderPath;
            this.deleteData = deleteData;
            this.database = database;

            if (this.deleteData)
            {
                this.sp_DeleteAll.Visibility = Visibility.Visible;
                this.sp_DeleteImages.Visibility = Visibility.Collapsed;
                this.OkButton.IsEnabled = false;
                this.chkboxConfirm.Visibility = Visibility.Visible;
            }
            else
            {
                this.sp_DeleteAll.Visibility = Visibility.Collapsed;
                this.sp_DeleteImages.Visibility = Visibility.Visible;
                this.OkButton.IsEnabled = true;
                this.chkboxConfirm.Visibility = Visibility.Collapsed;
            }
            this.GridGallery.RowDefinitions.Clear();

            // Set the local variables to the passed in parameters
            int col = 0;
            int row = 0;

            GridLength gridlength200 = new GridLength(1, GridUnitType.Auto);
            GridLength gridlength20 = new GridLength(1, GridUnitType.Auto);
            for (int i = 0; i < deletedImageTable.Rows.Count; i++)
            {
                ImageProperties imageProperties = new ImageProperties(deletedImageTable.Rows[i]);
                string path = imageProperties.GetImagePath(this.imageFolderPath);
                BitmapImage image;
                if (ImageQualityFilter.Corrupted == imageProperties.ImageQuality)
                {
                    image = this.GetImage(path, "corrupted");
                }
                else if (File.Exists(path))
                {
                    image = this.GetImage(path, "ok");
                }
                else
                {
                    image = this.GetImage(path, "missing");
                }

                if (col == 0)
                {
                    this.GridGallery.RowDefinitions.Add(new RowDefinition() { Height = gridlength20 });
                    this.GridGallery.RowDefinitions.Add(new RowDefinition() { Height = gridlength200 });
                }

                Label imageLabel = new Label();
                imageLabel.Content = imageProperties.FileName;
                imageLabel.Height = 25;
                imageLabel.VerticalAlignment = VerticalAlignment.Top;

                System.Windows.Controls.Image imageControl = new System.Windows.Controls.Image();
                imageControl.Source = image;

                Grid.SetRow(imageLabel, row);
                Grid.SetRow(imageControl, row + 1);
                Grid.SetColumn(imageLabel, col);
                Grid.SetColumn(imageControl, col);
                this.GridGallery.Children.Add(imageLabel);
                this.GridGallery.Children.Add(imageControl);
                col++;
                if (col == 5)
                {
                    // A new row is started every 5th time
                    col = 0;
                    row += 2;
                }
            }

            this.scroller.CanContentScroll = true;
            Mouse.OverrideCursor = null;
        }

        #endregion

        #region Private methods
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
            List<long> imagesIDsToDelete = new List<long>();
            Mouse.OverrideCursor = Cursors.Wait;
            for (int i = 0; i < this.deletedImageTable.Rows.Count; i++)
            {
                ImageProperties imageProperties = new ImageProperties(this.deletedImageTable.Rows[i]);

                string deleteFlag = this.database.DataLabelFromControlType[Constants.DatabaseColumn.DeleteFlag];
                this.database.UpdateImage((int)imageProperties.ID, deleteFlag, "false");
                if (this.deleteData)
                {
                    imagesIDsToDelete.Add(imageProperties.ID);
                }

                string path = imageProperties.GetImagePath(this.imageFolderPath);
                if (File.Exists(path))
                {
                    this.MoveImageToBackupFolder(this.imageFolderPath, imageProperties.FileName);
                }
            }

            if (this.deleteData)
            {
                foreach (long id in imagesIDsToDelete)
                {
                    this.database.DeleteImage(id);
                }
            }

            this.DialogResult = true;
            Mouse.OverrideCursor = null;
        }

        /// <summary>
        /// Display the original image in the dialog box, or a placeholder if we cannot
        /// </summary>
        private BitmapImage GetImage(string path, string state)
        {
            // Get and display the bitmap
            BitmapImage bitmap = new BitmapImage();
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;

            if (state.Equals("ok"))
            {
                Utilities.BitmapFromFile(bitmap, path, false);
            }
            else if (state.Equals("missing"))
            {
                bitmap = Utilities.BitmapFromResource(bitmap, "missing.jpg", true);
            }
            else if (state.Equals("corrupted"))
            {
                bitmap = Utilities.BitmapFromResource(bitmap, "corrupted.jpg", true);
            }
            return bitmap;
        }

        /// <summary>
        /// Create a backup of the current image file in the backup folder
        /// </summary>
        private void MoveImageToBackupFolder(string folderPath, string fileName)
        {
            string sourceFile = Path.Combine(folderPath, fileName);
            if (!File.Exists(sourceFile))
            {
                return;  // If there is no source file, its a missing file so we can't back it up
            }

            string destFolder = Path.Combine(folderPath, Constants.File.BackupFolder);
            string destFile = Path.Combine(destFolder, fileName);

            // Create a new target folder, if necessary.
            if (!Directory.Exists(destFolder))
            {
                Directory.CreateDirectory(destFolder);
            }

            // Move the image file to another location. 
            // However, if the destination file already exists don't overwrite it as its probably the original version.
            if (!File.Exists(destFile))
            {
                File.Move(sourceFile, destFile);
            }
        }
        #endregion

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Make sure the title bar of the dialog box is on the screen. For small screens it may default to being off the screen
            if (this.Left < 10 || this.Top < 10)
            {
                this.Left = this.Owner.Left + (this.Owner.Width - this.ActualWidth) / 2; // Center it horizontally
                this.Top = this.Owner.Top + 20; // Offset it from the windows'top by 20 pixels downwards
            }
        }

        private void ConfirmBox_Checked(object sender, RoutedEventArgs e)
        {
            this.OkButton.IsEnabled = (bool)this.chkboxConfirm.IsChecked;
        }
    }
}
