using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
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
                List<ImageRow> imagePropertiesList = new List<ImageRow>();
                int count = this.database.CurrentlySelectedImageCount;
                for (int image = 0; image < count; ++image)
                {
                    // We will store the various times here
                    ImageRow imageProperties = this.database.ImageDataTable[image];
                    DateTime originalDateTime;
                    string feedbackMessage = String.Empty;
                    bool result = imageProperties.GetDateTime(out originalDateTime); // If the result is false, we know that the originalDateTime will be reset to 01-Jan-0001 00:00:00
                    try
                    {
                        // Get the image (if its there), get the new dates/times, and add it to the list of images to be updated 
                        // Note that if the image can't be created, we will just to the catch.
                        // see remarks about framework WriteableBitmap.Metadata slicing bug in TimelapseWindow.LoadByScanningImageFolder()
                        BitmapFrame bitmapFrame = imageProperties.LoadBitmapFrame(this.database.FolderPath);
                        DateTimeAdjustment imageTimeAdjustment = imageProperties.TryUseImageTaken((BitmapMetadata)bitmapFrame.Metadata);
                        switch (imageTimeAdjustment)
                        {
                            case DateTimeAdjustment.MetadataNotUsed:
                                imageProperties.SetDateAndTimeFromFileInfo(this.database.FolderPath);  // We couldn't read the metadata, so get a candidate date/time from the file
                                feedbackMessage = " Using file timestamp";
                                break;
                            // includes DateTimeAdjustment.SameFileAndMetadataTime
                            default:
                                feedbackMessage = " Using metadata timestamp";
                                break;
                        }

                        DateTime rescannedDateTime;
                        result = imageProperties.GetDateTime(out rescannedDateTime);
                        if (result == false)
                        {
                            feedbackMessage += ", invalid date and time";
                            imageProperties.Date = String.Empty;
                        }
                        else
                        {
                            if (rescannedDateTime.Date == originalDateTime.Date)
                            {
                                feedbackMessage += ", same date";
                                imageProperties.Date = String.Empty; // If its the same, we won't copy it
                            }
                            else
                            {
                                feedbackMessage += ", different date";
                            }
                            if (rescannedDateTime.TimeOfDay == originalDateTime.TimeOfDay)
                            {
                                feedbackMessage += ", same time";
                                imageProperties.Time = String.Empty; // If its the same, we won't copy it
                            }
                            else
                            {
                                feedbackMessage += ", different time";
                            }
                        }
                        imagePropertiesList.Add(imageProperties);
                    }
                    catch (Exception exception)
                    {
                        Debug.Assert(false, String.Format("Open of image '{0}' failed.", imageProperties.FileName), exception.ToString());
                        feedbackMessage += " , skipping as cannot open image.";
                    }
                    backgroundWorker.ReportProgress(0, new FeedbackMessage(imageProperties.FileName, feedbackMessage));
                    if (image % 100 == 0)
                    {
                        Thread.Sleep(25); // Put in a delay every now and then, as otherwise the UI won't update.
                    }
                }

                // Pass 2. Update each date as needed 
                string message = String.Empty;
                backgroundWorker.ReportProgress(0, new FeedbackMessage(String.Empty, String.Empty)); // A blank separator
                backgroundWorker.ReportProgress(0, new FeedbackMessage("Pass 2: For selected images", "Updating only when dates or times differ..."));

                // This tuple list will hold the id, key and value that we will want to update in the database
                List<ColumnTuplesWithWhere> imagesToUpdate = new List<ColumnTuplesWithWhere>();
                for (int image = 0; image < imagePropertiesList.Count; image++)
                {
                    bool skip = false;
                    ImageRow imageProperties = imagePropertiesList[image];

                    ColumnTuplesWithWhere imageUpdate = new ColumnTuplesWithWhere();
                    if (!imageProperties.Date.Equals(String.Empty) && !imageProperties.Time.Equals(String.Empty))
                    {
                        // Both date and time need updating
                        imageUpdate.Columns.Add(new ColumnTuple(Constants.DatabaseColumn.Date, imageProperties.Date));
                        imageUpdate.Columns.Add(new ColumnTuple(Constants.DatabaseColumn.Time, imageProperties.Time));
                        message = "Date / Time updated to: " + imageProperties.Date + " " + imageProperties.Time;
                    }
                    else if (!imageProperties.Date.Equals(String.Empty))
                    {
                        // Only date needs updating
                        imageUpdate.Columns.Add(new ColumnTuple(Constants.DatabaseColumn.Date, imageProperties.Date));
                        message = "Date updated to: " + imageProperties.Date;
                    }
                    else if (!imageProperties.Time.Equals(String.Empty))
                    {
                        imageUpdate.Columns.Add(new ColumnTuple(Constants.DatabaseColumn.Time, imageProperties.Time));
                        message = "Time updated to: " + imageProperties.Time;
                    }
                    else
                    {
                        message = "Updating not required";
                        skip = true;
                    }

                    if (imageUpdate.Columns.Count > 0)
                    {
                        imageUpdate.SetWhere(imageProperties.ID);
                        imagesToUpdate.Add(imageUpdate);
                    }

                    if (!skip)
                    { 
                        backgroundWorker.ReportProgress(0, new FeedbackMessage(imageProperties.FileName, message));
                        if (image % 100 == 0)
                        {
                            Thread.Yield(); // Allow the UI to update.
                        }
                    }
                }
                backgroundWorker.ReportProgress(0, new FeedbackMessage("Writing to database...", "Please wait"));
                Thread.Yield(); // Allow the UI to update.
                database.UpdateImages(imagesToUpdate);  // Write the updates to the database
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
                this.database.SelectDataTableImagesAll();
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
