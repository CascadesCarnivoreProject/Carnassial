using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using Timelapse.Database;
using Timelapse.Images;

namespace Timelapse
{
    /// <summary>
    /// Interaction logic for DialogDateRereadDatesFromImages.xaml
    /// </summary>
    public partial class DialogDateRereadDatesFromImages : Window
    {
        private ImageDatabase database;

        public DialogDateRereadDatesFromImages(ImageDatabase database)
        {
            this.InitializeComponent();
            this.database = database;
        }

        #region Callbacks

        // If the user click ok, re-read the dates 
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // this.CancelButton.IsEnabled = false; // We won't allow the operation to be cancelled, as I am concerned about the database getting corrupted.
            this.RescanDates();
            this.CancelButton.IsEnabled = true;
            this.CancelButton.Content = "Done";
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = ((string)CancelButton.Content == "Cancel") ? false : true;
        }
        #endregion

        private void RescanDates()
        {
            // This list will hold key / value pairs that will be bound to the datagrid feedback, 
            // which is the way to make those pairs appear in the data grid during background worker progress updates
            ObservableCollection<MyFeedbackPair> feedbackPairList = new ObservableCollection<MyFeedbackPair>();
            this.dgFeedback.ItemsSource = feedbackPairList;

            BackgroundWorker backgroundWorker = new BackgroundWorker()
            {
                WorkerReportsProgress = true
            };

            backgroundWorker.DoWork += (ow, ea) =>
            {   // this runs on the background thread; its written as an anonymous delegate
                // We need to invoke this to allow updates on the UI
                this.Dispatcher.Invoke(new Action(() =>
                {
                    // First, change the UIprovide some feedback
                    backgroundWorker.ReportProgress(0, new FeedbackMessage("Pass 1: Examining all images...", "Checking if dates/time differ"));
                }));

                // Pass 1. Check to see what dates/times need updating.
                List<ImageProperties> imagePropertiesList = new List<ImageProperties>();
                int count = database.CurrentlySelectedImageCount;
                for (int image = 0; image < count; image++)
                {
                    // We will store the various times here
                    ImageProperties imageProperties = new ImageProperties(database.ImageDataTable.Rows[image]);
                    string message = String.Empty;
                    try
                    {
                        // Get the image (if its there), get the new dates/times, and add it to the list of images to be updated 
                        // Note that if the image can't be created, we will just to the catch.
                        BitmapSource bitmap = imageProperties.LoadImage(database.FolderPath);
                        DateTimeAdjustment imageTimeAdjustment = imageProperties.TryUseImageTaken((BitmapMetadata)bitmap.Metadata);
                        switch (imageTimeAdjustment)
                        {
                            case DateTimeAdjustment.MetadataNotUsed:
                                message += " Using file timestamp";
                                break;
                            // includes DateTimeAdjustment.SameFileAndMetadataTime
                            default:
                                message += " Using metadata timestamp";
                                break;
                        }

                        if (imageProperties.Date.Equals(database.ImageDataTable.Rows[image][Constants.DatabaseColumn.Date].ToString()))
                        {
                            message += ", same date";
                            imageProperties.Date = String.Empty; // If its the same, we won't copy it
                        }
                        else
                        {
                            message += ", different date";
                        }
                        if (imageProperties.Time.Equals(database.ImageDataTable.Rows[image][Constants.DatabaseColumn.Time].ToString()))
                        {
                            message += ", same time";
                            imageProperties.Time = String.Empty; // If its the same, we won't copy it
                        }
                        else
                        {
                            message += ", different time";
                        }

                        imagePropertiesList.Add(imageProperties);
                    }
                    catch // Image isn't there
                    {
                        message += " , skipping as cannot open image.";
                    }

                    backgroundWorker.ReportProgress(0, new FeedbackMessage(imageProperties.FileName, message));
                    if (image % 100 == 0)
                    {
                        Thread.Sleep(25); // Put in a delay every now and then, as otherwise the UI won't update.
                    }
                }

                // Pass 2. Update each date as needed 
                string msg = String.Empty;
                backgroundWorker.ReportProgress(0, new FeedbackMessage("Pass 2: For selected images", "Updating only when dates or times differ..."));

                // This tuple list will hold the id, key and value that we will want to update in the database
                List<Tuple<long, string, string>> list_to_update_db = new List<Tuple<long, string, string>>();
                for (int i = 0; i < imagePropertiesList.Count; i++)
                {
                    if (!imagePropertiesList[i].Date.Equals(String.Empty) && !imagePropertiesList[i].Time.Equals(String.Empty))
                    {
                        // Both date and time need updating
                        list_to_update_db.Add(new Tuple<long, string, string>(imagePropertiesList[i].ID, Constants.DatabaseColumn.Date, imagePropertiesList[i].Date));
                        list_to_update_db.Add(new Tuple<long, string, string>(imagePropertiesList[i].ID, Constants.DatabaseColumn.Time, imagePropertiesList[i].Time));
                        msg = "Date / Time updated to: " + imagePropertiesList[i].Date + " " + imagePropertiesList[i].Time;
                    }
                    else if (!imagePropertiesList[i].Date.Equals(String.Empty))
                    {
                        // Only date needs updating
                        list_to_update_db.Add(new Tuple<long, string, string>(imagePropertiesList[i].ID, Constants.DatabaseColumn.Date, imagePropertiesList[i].Date));
                        msg = "Date updated to: " + imagePropertiesList[i].Date;
                    }
                    else if (!imagePropertiesList[i].Time.Equals(String.Empty))
                    {
                        list_to_update_db.Add(new Tuple<long, string, string>(imagePropertiesList[i].ID, Constants.DatabaseColumn.Time, imagePropertiesList[i].Time));
                        // dbData.RowSetValueFromID(Constants.TIME, imgprop_list[i].FinalTime, imgprop_list[i].ID); // OLD WAY: ONE ROW AT A TIME. Can DELETE THIS
                        msg = "Time updated to: " + imagePropertiesList[i].Time;
                    }
                    else
                    {
                        msg = "Updating not required";
                    }
                    backgroundWorker.ReportProgress(0, new FeedbackMessage(imagePropertiesList[i].FileName, msg));
                    if (i % 100 == 0)
                    {
                        Thread.Sleep(25); // Put in a delay every now and then, as otherwise the UI won't update.
                    }
                }
                backgroundWorker.ReportProgress(0, new FeedbackMessage("Writing to database...", "Please wait"));
                Thread.Sleep(25);
                database.UpdateImages(list_to_update_db);  // Write the updates to the database
                backgroundWorker.ReportProgress(0, new FeedbackMessage("Done", "Done"));
            };
            backgroundWorker.ProgressChanged += (o, ea) =>
            {
                FeedbackMessage message = (FeedbackMessage)ea.UserState;
                feedbackPairList.Add(new MyFeedbackPair { Image = message.ImageName, Message = message.Message });
                this.dgFeedback.ScrollIntoView(dgFeedback.Items[dgFeedback.Items.Count - 1]);
            };
            backgroundWorker.RunWorkerCompleted += (o, ea) =>
            {
                this.OkButton.IsEnabled = false;
                this.CancelButton.IsEnabled = true;
            };
            backgroundWorker.RunWorkerAsync();
        }

        // Used to label the datagrid feedback columns with the appropriate headers
        private void DatagridFeedback_AutoGeneratedColumns(object sender, EventArgs e)
        {
            this.dgFeedback.Columns[0].Header = "Image Name";
            this.dgFeedback.Columns[1].Header = "Date and Time Changes";
        }

        private class MyFeedbackPair
        {
            public string Image { get; set; }
            public string Message { get; set; }
        }
    }
}
