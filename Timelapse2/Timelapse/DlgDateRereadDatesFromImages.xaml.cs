using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Timelapse
{
    /// <summary>
    /// Interaction logic for DlgDateRereadDatesFromImages.xaml
    /// </summary>
    public partial class DlgDateRereadDatesFromImages : Window
    {
        DBData dbData;
        public DlgDateRereadDatesFromImages(DBData dbData)
        {
            InitializeComponent();
            this.dbData = dbData;
        }

        #region Callbacks

        // If the user click ok, re-read the dates 
        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            //this.CancelButton.IsEnabled = false; // We won't allow the operation to be cancelled, as I am concerned about the database getting corrupted.
            this.RescanDates();
            this.CancelButton.IsEnabled = true;
            this.CancelButton.Content = "Done";
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = ((string) CancelButton.Content == "Cancel") ? false : true;
        }
        #endregion

        private void RescanDates()
        {
            // Collect the image properties for for the 2nd pass into a list...
            List<ImageProperties> imgprop_list = new List<ImageProperties>();

            // This list will hold key / value pairs that will be bound to the datagrid feedback, 
            // which is the way to make those pairs appear in the data grid during background worker progress updates
            ObservableCollection<MyFeedbackPair> feedbackPairList = new ObservableCollection<MyFeedbackPair>();
            this.dgFeedback.ItemsSource = feedbackPairList;

            BackgroundWorker bgw = new BackgroundWorker()
            {
                WorkerReportsProgress = true
            };

            bgw.DoWork += (ow, ea) =>
            {   // this runs on the background thread; its written as an anonymous delegate
                // We need to invoke this to allow updates on the UI
                this.Dispatcher.Invoke(new Action(() =>
                {
                    // First, change the UIprovide some feedback
                    bgw.ReportProgress(0, new FeedbackMessage("Pass 1: Examining all images...", "Checking if dates/time differ"));
                }));

                // Pass 1. Check to see what dates/times need updating.
                int count = dbData.DataTable.Rows.Count;
                int j = 1;
                for (int i = 0; i < count; i++)
                {
                    // We will store the various times here
                    ImageProperties imageProperties = new ImageProperties();
                    imageProperties.File = dbData.DataTable.Rows[i][Constants.DatabaseElement.File].ToString();
                    imageProperties.Folder = dbData.DataTable.Rows[i][Constants.DatabaseElement.Folder].ToString();
                    imageProperties.ID = Int32.Parse(dbData.DataTable.Rows[i][Constants.Database.ID].ToString());
                    string message = "";
                    try
                    {
                        // Get the image (if its there), get the new dates/times, and add it to the list of images to be updated 
                        // Note that if the image can't be created, we will just to the catch.
                        BitmapSource bmap = imageProperties.Load(dbData.FolderPath);

                        // First we try to see if  can get a valid and parsable metadata date and time
                        BitmapMetadata meta = (BitmapMetadata)bmap.Metadata;        // Get the data from the metadata
                        if (null != meta.DateTaken)
                        {
                           DateTime dtDate;
                           // all the different formats used by cameras, including ambiguities in month/day vs day/month orders.
                           if (DateTime.TryParse(meta.DateTaken, CultureInfo.InvariantCulture, DateTimeStyles.None, out dtDate))
                           {
                               imageProperties.FinalDate = DateTimeHandler.StandardDateString(dtDate);
                               imageProperties.FinalTime = DateTimeHandler.StandardTimeString(dtDate);
                               message += " Using metadata timestamp";
                           }
                        }
                        else  // Fallback as no meta data: We have to use the file date
                        {
                            // For some reason, different versions of Windows treat creation time and modification time differently, 
                            // giving inconsistent values. So I just check both and take the lesser of the two.
                            FileInfo fileInfo = imageProperties.GetFileInfo(dbData.FolderPath);
                            DateTime creationTime = File.GetCreationTime(fileInfo.FullName);
                            DateTime writeTime = File.GetLastWriteTime(fileInfo.FullName);
                            DateTime fileTime = (DateTime.Compare(creationTime, writeTime) < 0) ? creationTime : writeTime;
                            imageProperties.FinalDate = DateTimeHandler.StandardDateString(fileTime);
                            imageProperties.FinalTime = DateTimeHandler.StandardTimeString(fileTime);
                            message += " Using File timestamp";
                        }
                        if (imageProperties.FinalDate.Equals(dbData.DataTable.Rows[i][Constants.DatabaseElement.Date].ToString()))
                        {
                            message += ", same date";
                            imageProperties.FinalDate = ""; // If its the same, we won't copy it
                        }
                        else
                        {
                            message += ", different date";
                        }
                        if (imageProperties.FinalTime.Equals(dbData.DataTable.Rows[i][Constants.DatabaseElement.Time].ToString()))
                        {
                            message += ", same time";
                            imageProperties.FinalTime = ""; // If its the same, we won't copy it
                        }
                        else
                        {
                            message += ", different time";
                        }
                        imgprop_list.Add(imageProperties);
                    }
                    catch // Image isn't there
                    {
                        message += " , skipping as cannot open image.";
                    }
                    j++;
                    bgw.ReportProgress(0, new FeedbackMessage(imageProperties.File, message));
                    if (i % 100 == 0) System.Threading.Thread.Sleep(25); // Put in a delay every now and then, as otherwise the UI won't update.
                }

                // Pass 2. Update each date as needed 
                string msg = "";
                bgw.ReportProgress(0, new FeedbackMessage("Pass 2: For selected images", "Updating only when dates or times differ..."));

                // This tuple list will hold the id, key and value that we will want to update in the database
                List<Tuple<int, string, string>> list_to_update_db = new List<Tuple<int, string, string>>();
                for (int i = 0; i < imgprop_list.Count; i++)
                {
                    if ( ! (imgprop_list[i].FinalDate.Equals("")) && !(imgprop_list[i].FinalTime.Equals("")))
                    {
                        // Both date and time need updating
                        list_to_update_db.Add(new Tuple<int, string, string>(imgprop_list[i].ID, Constants.DatabaseElement.Date, imgprop_list[i].FinalDate));
                        list_to_update_db.Add(new Tuple<int, string, string>(imgprop_list[i].ID, Constants.DatabaseElement.Time, imgprop_list[i].FinalTime));
                        msg = "Date / Time updated to: " + imgprop_list[i].FinalDate + " " + imgprop_list[i].FinalTime;
                    }
                    else if ( !(imgprop_list[i].FinalDate.Equals ("")))
                    {
                        // Only date needs updating
                        list_to_update_db.Add(new Tuple<int, string, string>(imgprop_list[i].ID, Constants.DatabaseElement.Date, imgprop_list[i].FinalDate));
                        msg = "Date updated to: " + imgprop_list[i].FinalDate;
                    }
                    else if ( !(imgprop_list[i].FinalTime.Equals ("")))
                    {
                        list_to_update_db.Add(new Tuple<int, string, string>(imgprop_list[i].ID, Constants.DatabaseElement.Time, imgprop_list[i].FinalTime));
                        // dbData.RowSetValueFromID(Constants.TIME, imgprop_list[i].FinalTime, imgprop_list[i].ID); // OLD WAY: ONE ROW AT A TIME. Can DELETE THIS
                        msg = "Time updated to: " + imgprop_list[i].FinalTime;
                    }
                    else 
                    {
                        msg = "Updating not required";  
                    }
                    bgw.ReportProgress(0, new FeedbackMessage(imgprop_list[i].File, msg));
                    if (i % 100 == 0) System.Threading.Thread.Sleep(25); // Put in a delay every now and then, as otherwise the UI won't update.
                }
                bgw.ReportProgress(0, new FeedbackMessage("Writing to database...", "Please wait"));
                System.Threading.Thread.Sleep(25);
                dbData.RowsUpdateByRowIdKeyVaue(list_to_update_db);  // Write the updates to the database
                bgw.ReportProgress(0, new FeedbackMessage("Done", "Done"));
            };
            bgw.ProgressChanged += (o, ea) =>
            {
                FeedbackMessage message = (FeedbackMessage)ea.UserState;
                feedbackPairList.Add(new MyFeedbackPair { Image = message.ImageName, Message = message.Message });
                this.dgFeedback.ScrollIntoView(dgFeedback.Items[dgFeedback.Items.Count - 1]);
            };
            bgw.RunWorkerCompleted += (o, ea) =>
            {
                this.OkButton.IsEnabled = false;
                this.CancelButton.IsEnabled = true;
            };
            bgw.RunWorkerAsync();
        }

        // Used to label the datagrid feedback columns with the appropriate headers
        private void dgFeedback_AutoGeneratedColumns(object sender, EventArgs e)
        {
            dgFeedback.Columns[0].Header = "Image Name";
            dgFeedback.Columns[1].Header = "Date and Time Changes";
        }

        #region Helper classes to allow us to update our feedback in the datagrid via the background work
        // A class that tracks our progress as we load the images
        public class FeedbackMessage
        {
            public string ImageName { get; set; }
            public string Message { get; set; }
            public FeedbackMessage(string imagename, string message)
            {
                ImageName = imagename;
                Message = message;
            }
        }
        private class MyFeedbackPair
        {
            public string Image { get; set; }
            public string Message { get; set; }
        }
        #endregion
    }

}
