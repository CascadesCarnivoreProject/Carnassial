using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Timelapse.Database;

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
        /// Ask the user if he/she wants to delete one or more images and (optionally) the data associated with those images.
        /// Other parameters indicate various specifics of that image that we will use to display and delete it.
        /// deleteData is true when the associated data should be deleted.
        /// useDeleteFlags is true when the user is trying to delete images with the deletion flag set, otherwise its the current image being deleted
        /// </summary>
        public DialogDeleteImages(ImageDatabase database, DataTable deletedImageTable, bool deleteData, bool useDeleteFlags)
        {
            string imageFolderPath = database.FolderPath; 
            this.InitializeComponent();
            Mouse.OverrideCursor = Cursors.Wait;
            this.deletedImageTable = deletedImageTable;
            this.imageFolderPath = imageFolderPath;
            this.deleteData = deleteData;
            this.database = database;

            if (this.deleteData)
            {
                this.OkButton.IsEnabled = false;
                this.chkboxConfirm.Visibility = Visibility.Visible;
            }
            else
            {
                this.OkButton.IsEnabled = true;
                this.chkboxConfirm.Visibility = Visibility.Collapsed;
            }
            this.GridGallery.RowDefinitions.Clear();

            // Construct the UI text based on the state of the flags
            
            
            if (useDeleteFlags == false )
            {
                if (deleteData == false)
                {
                    // Case 1: Delete the current image, but not its data
                    this.Message.MessageTitle = "Delete the current image.";
                    this.Message.MessageWhat = "Deletes the current image (shown below) but not its data.";
                    this.Message.MessageResult = "The deleted image file will be backed up in a sub-folder named DeletedImages." + Environment.NewLine;
                    this.Message.MessageResult += "A placeholder image will be shown when you try to view a deleted image.";
                    this.Message.MessageHint = "Restore deleted images by manually copying or moving them back to their original location, or" + Environment.NewLine;
                    this.Message.MessageHint += "you can delete your image backups by deleting the DeletedImages folder.";
                }
                else
                {
                    // Case 2: Delete the current image and its data
                    this.Message.MessageTitle = "- Delete the current image and its data.";
                    this.Message.MessageWhat  = "- Deletes the current image (shown below) and the data associated with that image.";
                    this.Message.MessageResult = "The deleted image file will be backed up in a sub-folder named DeletedImages." + Environment.NewLine;
                    this.Message.MessageResult += "However, the data associated with that image will be permanently deleted.";
                    this.Message.MessageHint = "You can delete your image backups by deleting the DeletedImages folder.";
                }
            }
            else 
            {
                if (deleteData == false)
                {
                    // Case 3: Delete the images that have the delete flag set, but not their data
                    this.Message.MessageTitle = "Delete all images marked for deletion";
                    this.Message.MessageWhat = "Deletes all images marked for deletion (shown below) but not the data associated with those images.";
                    this.Message.MessageResult = "The deleted image file will be backed up in a sub-folder named DeletedImages." + Environment.NewLine;
                    this.Message.MessageResult += "A placeholder image will be shown when you try to view a deleted image.";
                    this.Message.MessageHint = "Restore deleted images by manually copying or moving them back to their original location, or" + Environment.NewLine;
                    this.Message.MessageHint += "you can delete your image backups by deleting the DeletedImages folder";
                }
                else
                {
                    // Case 4: Delete the images that have the delete flag set, and their data
                    this.Message.MessageTitle = "Delete all images marked for deletion and their data";
                    this.Message.MessageWhat = "Deletes all images marked for deletion (shown below) along with the data associated with those images.";
                    this.Message.MessageResult = "The deleted image file will be backed up in a sub-folder named DeletedImages" + Environment.NewLine;
                    this.Message.MessageResult += "However, the data associated with those images will be permanently deleted.";
                    this.Message.MessageHint = "You can delete your image backups by deleting the DeletedImages folder. ";
                }
            }
            //this.Title = this.Message.MessageTitle;

            // Set the local variables to the passed in parameters
            int col = 0;
            int row = 0;

            GridLength gridlength200 = new GridLength(1, GridUnitType.Auto);
            GridLength gridlength20 = new GridLength(1, GridUnitType.Auto);
            for (int i = 0; i < deletedImageTable.Rows.Count; i++)
            {
                ImageProperties imageProperties = new ImageProperties(deletedImageTable.Rows[i]);
                ImageSource bitmap = imageProperties.LoadWriteableBitmap(database.FolderPath);

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
                imageControl.Source = bitmap;

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
            // Don't allow the user to delete ALL their data. 
            // TODOSAUL: Need a general fix to this throughout, where we allow for an empty dataset 
            if (this.deleteData && (this.deletedImageTable.Rows.Count >= this.database.GetImageCount()))
            {
                DialogMessageBox dlgMsg = new DialogMessageBox();
                dlgMsg.IconType = MessageBoxImage.Error;
                dlgMsg.MessageTitle = "You can't delete all your images";
                dlgMsg.MessageProblem = "You can't delete all your images";
                dlgMsg.MessageReason = "Timelapse must have at least one image to display.";
                dlgMsg.MessageSolution = "Select only a subset of images to delete.";
                dlgMsg.ShowDialog();
                this.DialogResult = false;
                return;
            }
            List<long> imagesIDsToDelete = new List<long>();
            Mouse.OverrideCursor = Cursors.Wait;
            for (int i = 0; i < this.deletedImageTable.Rows.Count; i++)
            {
                ImageProperties imageProperties = new ImageProperties(this.deletedImageTable.Rows[i]);

                string deleteFlag = this.database.DataLabelFromColumnName[Constants.DatabaseColumn.DeleteFlag];
                this.database.UpdateImage((int)imageProperties.ID, deleteFlag, "false");
                if (this.deleteData)
                {
                    imagesIDsToDelete.Add(imageProperties.ID);
                }
                else
                {
                    // As only the image was deleted, mark its image quality as missing.
                    string dataLabel = this.database.DataLabelFromColumnName[Constants.DatabaseColumn.ImageQuality];
                    this.database.UpdateImage((int)imageProperties.ID, dataLabel, ImageQualityFilter.Missing.ToString());
                }
                this.TryMoveImageToBackupFolder(this.imageFolderPath, imageProperties);

            }

            if (this.deleteData)
            {
                this.database.DeleteImage(imagesIDsToDelete);
            }

            this.DialogResult = true;
            Mouse.OverrideCursor = null;
        }

        /// <summary>
        /// Create a backup of the current image file in the backup folder
        /// </summary>
        /// SAULTODO: For some reason, it won't delete a corrupted file because there is an access violation
        /// SAULTODO: I suspect that elsewhere in the code the image is being opened when it is initially checked but not released. Need track this down...
        private bool TryMoveImageToBackupFolder(string folderPath, ImageProperties imageProperties)
        {
            string sourceFilePath = imageProperties.GetImagePath(folderPath);
            if (!File.Exists(sourceFilePath))
            {
                return false;  // If there is no source file, its a missing file so we can't back it up
            }
            
            // Create a new target folder, if necessary.
            string destinationFolder = Path.Combine(folderPath, Constants.File.BackupFolder);
            if (!Directory.Exists(destinationFolder))
            {
                Directory.CreateDirectory(destinationFolder);
            }
            
            // Move the image file to another location. 
            // However, if the destination file already exists don't overwrite it as it's probably the original version.
            // TODOSAUL: is it really OK if backups are lossy?
            // Move the image file to another location. 
            // This will overwrite the destination file if it already exists.
            string destinationFilePath = Path.Combine(destinationFolder, imageProperties.FileName);
            //System.Diagnostics.Debug.Print(sourceFilePath + " " + destinationFilePath);
            if (File.Exists(destinationFilePath))
            {
                try
                {
                    File.Delete(sourceFilePath);
                    return true;
                }
                catch (IOException e)
                {
                    System.Diagnostics.Debug.Print(e.Message);
                    return false;
                }
            }
            else
            {
                try
                {
                    File.Move(sourceFilePath, destinationFilePath);
                    return true;
                }
                catch (IOException e)
                {
                    System.Diagnostics.Debug.Print(e.Message);
                    return false;
                }
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
