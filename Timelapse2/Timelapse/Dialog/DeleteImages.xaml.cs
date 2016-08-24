using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Timelapse.Database;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// This dialog box asks the user if he/she wants to delete the images (and possibly the data) of images rows as specified in the deletedImageTable
    /// What actually happens is that the image is replaced by a 'dummy' placeholder image,
    /// and the original image is copied into a subfolder called Deleted.
    /// </summary>
    public partial class DeleteImages : Window
    {
        // these variables will hold the values of the passed in parameters
        private const int LargeNumberOfDeletedImages = 30;
        private bool deleteData;
        private ImageDatabase imageDatabase;
        private List<ImageRow> imagesToDelete;

        public List<long> ImageFilesRemovedByID { get; private set; }

        /// <summary>
        /// Ask the user if he/she wants to delete one or more images and (depending on whether deleteData is set) the data associated with those images.
        /// Other parameters indicate various specifics of how the deletion was specified, which also determines what is displayed in the interface:
        /// -deleteData is true when the data associated with that image should be deleted.
        /// -useDeleteFlags is true when the user is trying to delete images with the deletion flag set, otherwise its the current image being deleted
        /// </summary>
        public DeleteImages(ImageDatabase database, List<ImageRow> deletedImageTable, bool deleteData, bool deletingCurrentImage)
        {
            this.InitializeComponent();
            Mouse.OverrideCursor = Cursors.Wait;
            this.imagesToDelete = deletedImageTable;
            this.deleteData = deleteData;
            this.imageDatabase = database;
            int imagecount = this.imagesToDelete.Count;

            this.ImageFilesRemovedByID = new List<long>();

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

            // Construct the dialog's text based on the state of the flags
            if (deletingCurrentImage)
            {
                string imageOrVideo = deletedImageTable[0].IsVideo ? "video" : "image";
                if (deleteData == false)
                {
                    // Case 1: Delete the current image, but not its data.
                    this.Message.Title = String.Format("Delete the current {0} but not its data.", imageOrVideo);
                    this.Message.What = String.Format("Deletes the current {0} file (shown below) but not its data.", imageOrVideo);
                    this.Message.Result = String.Format("\u2022 The deleted {0} file will be backed up in a sub-folder named DeletedImages.{1}", imageOrVideo, Environment.NewLine);
                    this.Message.Result += String.Format("\u2022 A placeholder {0} will be shown when you try to view a deleted {0}.", imageOrVideo);
                    this.Message.Hint = String.Format("\u2022 Restore deleted {0}s by manually copying or moving them back to their original location, or{1}", imageOrVideo, Environment.NewLine);
                    this.Message.Hint += String.Format("\u2022 Delete your {0} backups by deleting the DeletedImages folder.", imageOrVideo);
                }
                else
                {
                    // Case 2: Delete the current image and its data
                    this.Message.Title = String.Format("Delete the current {0} and its data", imageOrVideo);
                    this.Message.What = String.Format("Deletes the current {0} file (shown below) and the data associated with that {0}.", imageOrVideo);
                    this.Message.Result = String.Format("\u2022 The deleted {0} file will be backed up in a sub-folder named DeletedImages.{1}", imageOrVideo, Environment.NewLine);
                    this.Message.Result += String.Format("\u2022 However, the data associated with that {0} will be permanently deleted.", imageOrVideo);
                    this.Message.Hint = String.Format("You can permanently delete your {0} backup by deleting the DeletedImages folder.", imageOrVideo);
                }
            }
            else
            {
                if (deleteData == false)
                {
                    // Case 3: Delete the images that have the delete flag set, but not their data
                    this.Message.Title = "Delete " + imagecount.ToString() + " images and videos marked for deletion in this filter";
                    this.Message.What = "Deletes " + imagecount.ToString() + " image and video file(s) in this filter that are marked for deletion (shown below), but not the data entered for them.";
                    this.Message.Result = String.Empty;
                    if (imagecount > LargeNumberOfDeletedImages)
                    {
                        this.Message.Result += "Deleting " + imagecount.ToString() + " files will take a few moments. Please be patient." + Environment.NewLine;
                    }
                    this.Message.Result += "\u2022 The deleted file will be backed up in a sub-folder named DeletedImages." + Environment.NewLine;
                    this.Message.Result += "\u2022 A placeholder image will be shown when you try to view a deleted image.";
                    this.Message.Hint = "\u2022 Restore deleted files by manually copying or moving them back to their original location, or" + Environment.NewLine;
                    this.Message.Hint += "\u2022 Delete the backup files by deleting the DeletedImages folder";
                }
                else
                {
                    // Case 4: Delete the images that have the delete flag set, and their data
                    this.Message.Title = "Delete " + imagecount.ToString() + " images and videos marked for deletion and their data in this filter";
                    this.Message.What = "Deletes the image and video files that are marked for deletion (shown below), along with the data entered for them.";
                    this.Message.Result = String.Empty;
                    if (imagecount > LargeNumberOfDeletedImages)
                    {
                        this.Message.Result += "Deleting " + imagecount.ToString() + " files will take a few moments. Please be patient." + Environment.NewLine;
                    }
                    this.Message.Result += "\u2022 The deleted files will be backed up in a sub-folder named DeletedImages" + Environment.NewLine;
                    this.Message.Result += "\u2022 However, the data associated with those files will be permanently deleted.";
                    this.Message.Hint = "You can permanently delete those backup files by deleting the DeletedImages folder.";
                }
            }
            this.Title = this.Message.Title;

            // Set the local variables to the passed in parameters
            int column = 0;
            int row = 0;

            GridLength gridlength200 = new GridLength(1, GridUnitType.Auto);
            GridLength gridlength20 = new GridLength(1, GridUnitType.Auto);

            foreach (ImageRow imageProperties in deletedImageTable)
            {
                ImageSource bitmap = imageProperties.LoadBitmap(database.FolderPath, Constants.Images.ThumbnailSmall);
                
                if (column == 0)
                {
                    this.GridGallery.RowDefinitions.Add(new RowDefinition() { Height = gridlength20 });
                    this.GridGallery.RowDefinitions.Add(new RowDefinition() { Height = gridlength200 });
                }

                Label imageLabel = new Label();
                imageLabel.Content = imageProperties.FileName;
                imageLabel.Height = 25;
                imageLabel.VerticalAlignment = VerticalAlignment.Top;

                Image imageControl = new Image();
                imageControl.Source = bitmap;

                Grid.SetRow(imageLabel, row);
                Grid.SetRow(imageControl, row + 1);
                Grid.SetColumn(imageLabel, column);
                Grid.SetColumn(imageControl, column);
                this.GridGallery.Children.Add(imageLabel);
                this.GridGallery.Children.Add(imageControl);
                column++;
                if (column == 5)
                {
                    // A new row is started every five columns
                    column = 0;
                    row += 2;
                }
            }

            this.scroller.CanContentScroll = true;
            Mouse.OverrideCursor = null;
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
            List<long> imageIDsToDeleteFromDatabase = new List<long>();
            Mouse.OverrideCursor = Cursors.Wait;
            foreach (ImageRow imageProperties in this.imagesToDelete)
            {
                string deleteFlagDataLabel = this.imageDatabase.DataLabelFromStandardControlType[Constants.Control.DeleteFlag];
                this.imageDatabase.UpdateImage(imageProperties.ID, deleteFlagDataLabel, Constants.Boolean.False);
                if (this.deleteData)
                {
                    imageIDsToDeleteFromDatabase.Add(imageProperties.ID);
                }
                else
                {
                    // as only the image file was deleted, change its image quality to missing in the database
                    string imageQualityDataLabel = this.imageDatabase.DataLabelFromStandardControlType[Constants.DatabaseColumn.ImageQuality];
                    this.imageDatabase.UpdateImage(imageProperties.ID, imageQualityDataLabel, ImageFilter.Missing.ToString());
                    this.ImageFilesRemovedByID.Add(imageProperties.ID);
                }
                this.TryMoveImageToDeletedImagesFolder(this.imageDatabase.FolderPath, imageProperties);
            }

            if (this.deleteData)
            {
                this.imageDatabase.DeleteImages(imageIDsToDeleteFromDatabase);
            }

            this.DialogResult = true;
            Mouse.OverrideCursor = null;
        }

        /// <summary>
        /// Create a backup of the current image file in the backup folder
        /// </summary>
        private bool TryMoveImageToDeletedImagesFolder(string folderPath, ImageRow imageProperties)
        {
            string sourceFilePath = imageProperties.GetImagePath(folderPath);
            if (!File.Exists(sourceFilePath))
            {
                return false;  // If there is no source file, its a missing file so we can't back it up
            }

            // Create a new target folder, if necessary.
            string destinationFolder = Path.Combine(folderPath, Constants.File.DeletedImagesFolder);
            if (!Directory.Exists(destinationFolder))
            {
                Directory.CreateDirectory(destinationFolder);
            }

            // Move the image file to the backup location.           
            string destinationFilePath = Path.Combine(destinationFolder, imageProperties.FileName);
            if (File.Exists(destinationFilePath))
            {
                try
                {
                    // Becaue move doesn't allow overwriting, delete the destination file if it already exists.
                    File.Delete(sourceFilePath);
                    return true;
                }
                catch (IOException e)
                {
                    Debug.Print(e.Message);
                    return false;
                }
            }
            try
            {
                File.Move(sourceFilePath, destinationFilePath);
                return true;
            }
            catch (IOException e)
            {
                Debug.Print(e.Message);
                return false;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Utilities.SetDefaultDialogPosition(this);
            Utilities.TryFitWindowInWorkingArea(this);
        }

        private void ConfirmBox_Checked(object sender, RoutedEventArgs e)
        {
            this.OkButton.IsEnabled = (bool)this.chkboxConfirm.IsChecked;
        }
    }
}
