using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Drawing;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Interop;
using System.IO;
using System.Data;


namespace Timelapse
{
    //TODO Modify this so it tells the user that they can't delete an image that is not there.
    //TODO Here and elsewhere, show the placeholder images scaled to the correct size. May have to keep width/height of original image to do this.

    /// <summary>
    /// This dialog box asks the user if he/she wants to delete the following images
    /// What actually happens is that the image is replaced by a 'dummy' placeholder image,
    /// and the original image is copied into a subfolder called Deleted.
    /// </summary>
    public partial class DlgDeleteImages : Window
    {
        // these variables will hold the values of the passed in parameters
        private string imageFolderPath; // the full folder path where that image is located
        DataTable deletedTable;
        bool deleteData;
        DBData dbData;
        #region Public methods
        /// <summary>
        /// Ask the user if he/she wants to delete the given image indicated by the index.
        /// Other parameters indicate various specifics of that image that we will use to display and delete it
        /// </summary>
        /// <param name="currentImageIndex"></param>
        /// <param name="imageFilename"></param>
        /// <param name="imageFolderPath"></param>
        /// <param name="isCorrupted"></param>
        public DlgDeleteImages(DBData db_data, DataTable deletedTable, string imageFolderPath, bool delete_data)
        {
            InitializeComponent();
            Mouse.OverrideCursor = Cursors.Wait; 
            this.deletedTable = deletedTable;
            this.imageFolderPath = imageFolderPath;
            this.deleteData = delete_data;
            this.dbData = db_data;

            string fname= "";
            string path="";
            BitmapImage image;
            System.Windows.Controls.Image imagectl;
            Label label;

            if (this.deleteData)
            {
                this.sp_DeleteAll.Visibility = Visibility.Visible;
                this.sp_DeleteImages.Visibility = Visibility.Collapsed;
                this.OkButton.IsEnabled = false;
                this.chkboxConfirm.Visibility= Visibility.Visible;
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
            for (int i = 0; i < deletedTable.Rows.Count; i++ )
            {
                fname = (string) deletedTable.Rows[i][Constants.FILE];
                
                label = new Label();
                label.Content = fname;
                label.Height = 25;
                label.VerticalAlignment = VerticalAlignment.Top;

                path = System.IO.Path.Combine(this.imageFolderPath, fname);

                if (Constants.IMAGEQUALITY_CORRUPTED == (string)deletedTable.Rows[i][Constants.IMAGEQUALITY])
                    image = this.getImage(path, "corrupted");
                else if (File.Exists(path))
                    image = this.getImage(path, "ok");
                else
                    image = this.getImage(path, "missing");

                imagectl = new System.Windows.Controls.Image() ;
                imagectl.Source = image;
               
                if (col == 0)
                {
                    this.GridGallery.RowDefinitions.Add(new RowDefinition() { Height = gridlength20 });
                    this.GridGallery.RowDefinitions.Add(new RowDefinition() { Height = gridlength200 });
                }
                Grid.SetRow(label, row);
                Grid.SetRow(imagectl, row+1);
                Grid.SetColumn(label, col);
                Grid.SetColumn(imagectl, col);
                this.GridGallery.Children.Add(label);
                this.GridGallery.Children.Add(imagectl);
                col++;
                if (col == 5) // A new row is started every 5th time
                {
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
            string fname;
            string path;
            List <Int64> IDs = new List<Int64>();
            Mouse.OverrideCursor = Cursors.Wait; 
            for (int i = 0; i < deletedTable.Rows.Count; i++)
            {
                fname = (string)this.deletedTable.Rows[i][Constants.FILE];
                Int64 id = (Int64) this.deletedTable.Rows[i][Constants.ID];
                string datalabel = (string) this.dbData.DataLabelFromType[Constants.DELETEFLAG];
                this.dbData.UpdateRow((int) id, datalabel, "false");
                path = System.IO.Path.Combine(this.imageFolderPath, fname);
                if (this.deleteData) IDs.Add((Int64) this.deletedTable.Rows[i][Constants.ID]);
                if (File.Exists(path))
                    moveImageToBackupFolder(this.imageFolderPath, fname);
            }
            if (this.deleteData)
            {
                foreach ( int id in IDs)
                {
                    this.dbData.DeleteRow(id);
                }
            }
            this.DialogResult = true;
            Mouse.OverrideCursor = null;
        }

       /// <summary>
        /// Display the original image in the dialog box, or a placeholder if we cannot
       /// </summary>
       /// <param name="index"></param>
        private BitmapImage getImage(string path, string state)
        {
            // Get and display the bitmap
            BitmapImage bi = new BitmapImage();

            bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            if (state.Equals("ok")) // Show the corrupted image placeholder
            {
                Utilities.BitmapFromFile(bi, path, false);  
            }
            else if (state.Equals("missing"))
            {
                bi = Utilities.BitmapFromResource(bi, "missing.jpg", true, 0, 0);
            }
            else if (state.Equals("corrupted"))
            {
                bi = Utilities.BitmapFromResource(bi, "corrupted.jpg", true, 0, 0);
            }
            return bi;
        }


        /// <summary>
        /// Display the deleted image placeholder so the user knows what it looks like
        /// </summary>
        private void showDeletedImage()
        {
            //// Get and display the bitmap
            //var bi = new BitmapImage();
            //bi = Utilities.BitmapFromResource(bi, "missing.jpg", true, 0, 0);
            //this.deletedImage.Source = bi;
        }

        /// <summary>
        /// Create a backup of the current image file in the backupfolder
        /// </summary>
        private void moveImageToBackupFolder(string folderpath, string fname)
        {
            string sourceFile = System.IO.Path.Combine(folderpath, fname);
            if (!File.Exists(sourceFile)) return;  // If there is no source file, its a missing file so we can't back it up

            string destFolder = System.IO.Path.Combine(folderpath, Constants.BACKUPFOLDER);
            string destFile = System.IO.Path.Combine(destFolder, fname);

            // Create a new target folder, if necessary.
            if (!Directory.Exists(destFolder))  Directory.CreateDirectory(destFolder);

            // Move the image file to another location. 
            // However, if the destination file already exists don't overwrite it as its probably the original version.
            if (!File.Exists(destFile)) 
                File.Move(sourceFile, destFile);
        }
        #endregion

        private void chkboxConfirm_Checked(object sender, RoutedEventArgs e)
        {
            this.OkButton.IsEnabled = ((bool)chkboxConfirm.IsChecked); 
        }
    }
}
