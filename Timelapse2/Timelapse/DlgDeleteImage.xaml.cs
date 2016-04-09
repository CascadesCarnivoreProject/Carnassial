using System;
using System.Windows;
using System.Windows.Media.Imaging;
using System.IO;


namespace Timelapse
{
    //TODO Modify this so it tells the user that they can't delete an image that is not there.
    //TODO Here and elsewhere, show the placeholder images scaled to the correct size. May have to keep width/height of original image to do this.

    /// <summary>
    /// This dialog box asks the user if he/she wants to delete the following images
    /// What actually happens is that the image is replaced by a 'dummy' placeholder image,
    /// and the original image is copied into a subfolder called Deleted.
    /// </summary>
    public partial class DlgDeleteImage : Window
    {
        // these variables will hold the values of the passed in parameters
        private string imageFilename; // the filename of the current image
        private string imageFolderPath; // the full folder path where that image is located
        private bool isCorrupted; // whether that image was corrupted or not
        private bool deleteData;
        private DBData dbData;
        #region Public methods
        /// <summary>
        /// Ask the user if he/she wants to delete the given image indicated by the index.
        /// Other parameters indicate various specifics of that image that we will use to display and delete it
        /// </summary>
        /// <param name="currentImageIndex"></param>
        /// <param name="imageFilename"></param>
        /// <param name="imageFolderPath"></param>
        /// <param name="isCorrupted"></param>
        public DlgDeleteImage(DBData db_data, string imageFilename, string imageFolderPath, bool isCorrupted, bool delete_data)
        {
            InitializeComponent();

            // Set the local variables to the passed in parameters
            this.imageFilename = imageFilename;
            this.imageFolderPath = imageFolderPath;
            this.deleteData = delete_data;
            this.dbData = db_data;

            if (this.deleteData == true)
            {
                this.textMain.Text = "Delete the current image file and its data: " + this.imageFilename; // put the file name in the title   
                this.TBDeleteImageAndData1.Visibility = Visibility.Visible;
                this.TBDeleteImageOnly1.Visibility = Visibility.Collapsed;
                this.TBDeleteImageOnly2.Visibility = Visibility.Collapsed;
                this.TBDeleteImageOnly3.Visibility = Visibility.Collapsed;
                this.LblImageOnly.Visibility = Visibility.Hidden;
                this.deletedImage.Visibility = Visibility.Hidden;
                this.chkboxConfirm.Visibility = Visibility.Visible;
                this.OkButton.IsEnabled = false;            }
            else
            { 
               this.textMain.Text = "Delete the current image file: " + this.imageFilename; // put the file name in the title  
               this.TBDeleteImageAndData1.Visibility = Visibility.Collapsed;
               this.TBDeleteImageOnly1.Visibility = Visibility.Visible;
               this.TBDeleteImageOnly2.Visibility = Visibility.Visible;
               this.TBDeleteImageOnly3.Visibility = Visibility.Visible;
               this.LblImageOnly.Visibility = Visibility.Visible;
               this.deletedImage.Visibility = Visibility.Visible;

               this.chkboxConfirm.Visibility = Visibility.Hidden;
               this.OkButton.IsEnabled = true; 
            }
            this.isCorrupted = isCorrupted;
            this.deleteData = delete_data;
            this.showOriginalImage();
            this.showDeletedImage();
        }

        #endregion

        #region Private methods
        private void chkboxConfirm_Checked(object sender, RoutedEventArgs e)
        {
            this.OkButton.IsEnabled = ((bool)chkboxConfirm.IsChecked);
        }
        
        /// <summary>
        /// Cancel button selected
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        /// <summary>
        /// Ok button selected
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            moveImageToBackupFolder();
            // createPlaceholderImage();
            if (this.deleteData)
            {
                Int64 id = (Int64)this.dbData.GetIdOfCurrentRow();
                this.dbData.DeleteRow((int) id);
            }
            this.DialogResult = true;
        }

       /// <summary>
        /// Display the original image in the dialog box, or a placeholder if we cannot
       /// </summary>
       /// <param name="index"></param>
        private void showOriginalImage()
        {
            // Get and display the bitmap
            var bi = new BitmapImage();

            bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            if (this.isCorrupted) // Show the corrupted iamge placeholder
            {
                bi = Utilities.BitmapFromResource(bi, "corrupted.jpg", true, 0, 0);
            }
            else if (!File.Exists(System.IO.Path.Combine(this.imageFolderPath, this.imageFilename)))
            {
                bi = Utilities.BitmapFromResource(bi, "missing.jpg", true, 0, 0);
            }
            else
            {
                Utilities.BitmapFromFile(bi, System.IO.Path.Combine(this.imageFolderPath, this.imageFilename), true);
            }
            this.originalImage.Source = bi;
        }


        /// <summary>
        /// Display the deleted image placeholder so the user knows what it looks like
        /// </summary>
        private void showDeletedImage()
        {
            // Get and display the bitmap
            var bi = new BitmapImage();
            bi = Utilities.BitmapFromResource(bi, "missing.jpg", true, 0, 0);
            this.deletedImage.Source = bi;
        }

        /// <summary>
        /// Create a backup of the current image file in the backupfolder
        /// </summary>
        private void moveImageToBackupFolder()
        {
            string sourceFile = System.IO.Path.Combine(this.imageFolderPath, this.imageFilename);
            string destFolder = System.IO.Path.Combine(this.imageFolderPath, Constants.BACKUPFOLDER);
            string destFile = System.IO.Path.Combine(destFolder, this.imageFilename);

            if (!File.Exists (sourceFile)) return;  // If there is no source file, its a missing file so we can't back it up

            // Create a new target folder, if necessary.
            if (!Directory.Exists(destFolder))  Directory.CreateDirectory(destFolder);

            // Move the image file to another location. 
            //This will overwrite the destination file  if it already exists .
            if (File.Exists(destFile)) File.Delete(destFile);
            File.Move(sourceFile, destFile);
        }
        #endregion
    }
}
