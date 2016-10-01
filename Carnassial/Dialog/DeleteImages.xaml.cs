using Carnassial.Database;
using Carnassial.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Carnassial.Dialog
{
    /// <summary>
    /// This dialog box asks the user if he/she wants to delete the images (and possibly the data) of images rows as specified in the deletedImageTable
    /// What actually happens is that the image is replaced by a 'dummy' placeholder image,
    /// and the original image is copied into a subfolder called Deleted.
    /// </summary>
    public partial class DeleteImages : Window
    {
        // these variables will hold the values of the passed in parameters
        private bool deleteImageAndData;
        private ImageDatabase imageDatabase;
        private List<ImageRow> imagesToDelete;

        /// <summary>
        /// Ask the user if he/she wants to delete one or more images and (depending on whether deleteData is set) the data associated with those images.
        /// Other parameters indicate various specifics of how the deletion was specified, which also determines what is displayed in the interface:
        /// -deleteData is true when the data associated with that image should be deleted.
        /// -useDeleteFlags is true when the user is trying to delete images with the deletion flag set, otherwise its the current image being deleted
        /// </summary>
        public DeleteImages(ImageDatabase database, List<ImageRow> imagesToDelete, bool deleteImageAndData, bool deleteCurrentImageOnly, Window owner)
        {
            this.InitializeComponent();
            this.deleteImageAndData = deleteImageAndData;
            this.imageDatabase = database;
            this.imagesToDelete = imagesToDelete;
            this.Owner = owner;

            if (this.deleteImageAndData)
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
            if (deleteCurrentImageOnly)
            {
                string imageOrVideo = imagesToDelete[0].IsVideo ? "video" : "image";
                if (deleteImageAndData == false)
                {
                    // Case 1: Delete the current image, but not its data.
                    this.Message.Title = String.Format("Delete the current {0} but not its data.", imageOrVideo);
                    this.Message.What = String.Format("Deletes the current {0} file (shown below) but not its data.", imageOrVideo);
                    this.Message.Result = String.Format("\u2022 The deleted {0} file will be backed up in a sub-folder named {1}.{2}", imageOrVideo, Constants.File.DeletedFilesFolder, Environment.NewLine);
                    this.Message.Result += String.Format("\u2022 A placeholder {0} will be shown when you try to view a deleted {0}.", imageOrVideo);
                    this.Message.Hint = String.Format("\u2022 Restore deleted {0}s by manually copying or moving them back to their original location, or{1}", imageOrVideo, Environment.NewLine);
                    this.Message.Hint += String.Format("\u2022 Delete your {0} backups by deleting the {1} folder.", imageOrVideo, Constants.File.DeletedFilesFolder);
                }
                else
                {
                    // Case 2: Delete the current image and its data
                    this.Message.Title = String.Format("Delete the current {0} and its data", imageOrVideo);
                    this.Message.What = String.Format("Deletes the current {0} file (shown below) and the data associated with that {0}.", imageOrVideo);
                    this.Message.Result = String.Format("\u2022 The deleted {0} file will be backed up in a sub-folder named {1}.{2}", imageOrVideo, Constants.File.DeletedFilesFolder, Environment.NewLine);
                    this.Message.Result += String.Format("\u2022 However, the data associated with that {0} will be permanently deleted.", imageOrVideo);
                    this.Message.Hint = String.Format("You can permanently delete your {0} backup by deleting the {1} folder.", imageOrVideo, Constants.File.DeletedFilesFolder);
                }
            }
            else
            {
                int numberOfImagesToDelete = this.imagesToDelete.Count;
                if (deleteImageAndData == false)
                {
                    // Case 3: Delete the images that have the delete flag set but not their data
                    this.Message.Title = "Delete " + numberOfImagesToDelete.ToString() + " images and videos marked for deletion in this selection";
                    this.Message.What = "Deletes " + numberOfImagesToDelete.ToString() + " image and video file(s) marked for deletion (shown below) in this selection, but not the data entered for them.";
                    this.Message.Result = String.Empty;
                    if (numberOfImagesToDelete > Constants.Images.LargeNumberOfDeletedImages)
                    {
                        this.Message.Result += "Deleting " + numberOfImagesToDelete.ToString() + " files will take a few moments. Please be patient." + Environment.NewLine;
                    }
                    this.Message.Result += String.Format("\u2022 The deleted file will be backed up in a sub-folder named {0}.{1}", Constants.File.DeletedFilesFolder, Environment.NewLine);
                    this.Message.Result += "\u2022 A placeholder image will be shown when you view a deleted file.";
                    this.Message.Hint = "\u2022 Restore deleted files by manually copying or moving them back to their original location, or" + Environment.NewLine;
                    this.Message.Hint += String.Format("\u2022 Delete the backup files by deleting the {0} folder", Constants.File.DeletedFilesFolder);
                }
                else
                {
                    // Case 4: Delete the images that have the delete flag set and their data
                    this.Message.Title = "Delete " + numberOfImagesToDelete.ToString() + " images and videos marked for deletion";
                    this.Message.What = "Deletes the image and video files that are marked for deletion (shown below), along with the data entered for them.";
                    this.Message.Result = String.Empty;
                    if (numberOfImagesToDelete > Constants.Images.LargeNumberOfDeletedImages)
                    {
                        this.Message.Result += "Deleting " + numberOfImagesToDelete.ToString() + " files will take a moment. Please be patient." + Environment.NewLine;
                    }
                    this.Message.Result += String.Format("\u2022 The deleted files will be backed up in a sub-folder named {0}{1}", Constants.File.DeletedFilesFolder, Environment.NewLine);
                    this.Message.Result += "\u2022 However, the data associated with those files will be permanently deleted.";
                    this.Message.Hint = String.Format("You can permanently delete those backup files by deleting the {0} folder.", Constants.File.DeletedFilesFolder);
                }
            }
            this.Title = this.Message.Title;

            // load thumbnails of images which will be deleted
            Mouse.OverrideCursor = Cursors.Wait;
            this.GridGallery.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Auto) });
            this.GridGallery.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Auto) });
            int columnIndex = 0;
            int rowIndex = 0;
            foreach (ImageRow imageProperties in imagesToDelete)
            {
                Label imageLabel = new Label();
                imageLabel.Content = imageProperties.FileName;
                imageLabel.Height = 25;
                imageLabel.VerticalAlignment = VerticalAlignment.Top;

                Image imageControl = new Image();
                imageControl.Source = imageProperties.LoadBitmap(database.FolderPath, Constants.Images.ThumbnailWidth);

                Grid.SetRow(imageLabel, rowIndex);
                Grid.SetRow(imageControl, rowIndex + 1);
                Grid.SetColumn(imageLabel, columnIndex);
                Grid.SetColumn(imageControl, columnIndex);
                this.GridGallery.Children.Add(imageLabel);
                this.GridGallery.Children.Add(imageControl);
                ++columnIndex;
                if (columnIndex == 5)
                {
                    // A new row is started every five columns
                    columnIndex = 0;
                    rowIndex += 2;
                }
            }
            Mouse.OverrideCursor = null;

            this.scroller.CanContentScroll = true;
        }

        /// <summary>
        /// Cancel button selected
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void ConfirmBox_Checked(object sender, RoutedEventArgs e)
        {
            this.OkButton.IsEnabled = (bool)this.chkboxConfirm.IsChecked;
        }

        /// <summary>
        /// Ok button selected
        /// </summary>
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Utilities.SetDefaultDialogPosition(this);
            Utilities.TryFitWindowInWorkingArea(this);
        }
    }
}
