using Carnassial.Database;
using Carnassial.Images;
using Carnassial.Util;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Windows;

namespace Carnassial.Dialog
{
    public partial class DateRereadFromFiles : Window
    {
        private FileDatabase database;

        public DateRereadFromFiles(FileDatabase database, Window owner)
        {
            this.InitializeComponent();
            Utilities.TryFitWindowInWorkingArea(this);
            this.database = database;
            this.Owner = owner;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void DoneButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void StartDoneButton_Click(object sender, RoutedEventArgs e)
        {
            // This list will hold key / value pairs that will be bound to the datagrid feedback, 
            // which is the way to make those pairs appear in the data grid during background worker progress updates
            ObservableCollection<FeedbackTuple> feedbackRows = new ObservableCollection<FeedbackTuple>();
            this.feedbackGrid.ItemsSource = feedbackRows;
            this.cancelButton.IsEnabled = false;
            this.StartDoneButton.Content = "Done";
            this.StartDoneButton.Click -= this.StartDoneButton_Click;
            this.StartDoneButton.Click += this.DoneButton_Click;
            this.StartDoneButton.IsEnabled = false;

            BackgroundWorker backgroundWorker = new BackgroundWorker()
            {
                WorkerReportsProgress = true
            };

            backgroundWorker.DoWork += (ow, ea) =>
            {
                // this runs on the background thread; its written as an anonymous delegate
                // We need to invoke this to allow updates on the UI
                this.Dispatcher.Invoke(new Action(() =>
                {
                    // First, change the UIprovide some feedback
                    backgroundWorker.ReportProgress(0, new FeedbackTuple("Pass 1: Examining images and videos...", "Checking if dates/time differ"));
                }));

                // Pass 1. Check to see what dates/times need updating.
                List<ImageRow> imagesToAdjust = new List<ImageRow>();
                int count = this.database.CurrentlySelectedImageCount;
                TimeZoneInfo imageSetTimeZone = this.database.ImageSet.GetTimeZone();
                for (int row = 0; row < count; ++row)
                {
                    // We will store the various times here
                    ImageRow image = this.database.Files[row];
                    DateTimeOffset originalDateTime = image.GetDateTime();
                    string feedbackMessage = String.Empty;
                    try
                    {
                        // Get the image (if its there), get the new dates/times, and add it to the list of images to be updated 
                        // Note that if the image can't be created, we will just to the catch.
                        DateTimeAdjustment imageTimeAdjustment = image.TryReadDateTimeOriginalFromMetadata(this.database.FolderPath, imageSetTimeZone);
                        switch (imageTimeAdjustment)
                        {
                            case DateTimeAdjustment.MetadataNotUsed:
                                image.SetDateTimeOffsetFromFileInfo(this.database.FolderPath, imageSetTimeZone);  // We couldn't read the metadata, so get a candidate date/time from the file
                                feedbackMessage = "Using file timestamp: ";
                                break;
                            // includes DateTimeAdjustment.SameFileAndMetadataTime
                            default:
                                feedbackMessage = "Using metadata timestamp: ";
                                break;
                        }

                        DateTimeOffset rescannedDateTime = image.GetDateTime();
                        bool updateNeeded = false;
                        if (rescannedDateTime.Date == originalDateTime.Date)
                        {
                            feedbackMessage += "same date, ";
                        }
                        else
                        {
                            updateNeeded = true;
                            feedbackMessage += "different date, ";
                        }

                        if (rescannedDateTime.TimeOfDay == originalDateTime.TimeOfDay)
                        {
                            feedbackMessage += "same time, ";
                        }
                        else
                        {
                            updateNeeded = true;
                            feedbackMessage += "different time, ";
                        }

                        if (rescannedDateTime.Offset == originalDateTime.Offset)
                        {
                            feedbackMessage += "same UTC offset";
                        }
                        else
                        {
                            updateNeeded = true;
                            feedbackMessage += "different UTC offset";
                        }

                        if (updateNeeded)
                        {
                            imagesToAdjust.Add(image);
                        }
                    }
                    catch (Exception exception)
                    {
                        Debug.Fail(String.Format("Unexpected exception processing '{0}'.", image.FileName), exception.ToString());
                        feedbackMessage += String.Format(" , skipping due to {0}: {1}.", exception.GetType().FullName, exception.Message);
                    }

                    backgroundWorker.ReportProgress(0, new FeedbackTuple(image.FileName, feedbackMessage));
                    if (row % Constants.ThrottleValues.SleepForImageRenderInterval == 0)
                    {
                        Thread.Sleep(Constants.ThrottleValues.RenderingBackoffTime); // Put in a delay every now and then, as otherwise the UI won't update.
                    }
                }

                // Pass 2. Update each date as needed 
                string message = String.Empty;
                backgroundWorker.ReportProgress(0, new FeedbackTuple(String.Empty, String.Empty)); // A blank separator
                backgroundWorker.ReportProgress(0, new FeedbackTuple("Pass 2: Updating dates and times", String.Format("Updating {0} images and videos...", imagesToAdjust.Count)));
                Thread.Yield(); // Allow the UI to update.

                List<ColumnTuplesWithWhere> imagesToUpdate = new List<ColumnTuplesWithWhere>();
                foreach (ImageRow image in imagesToAdjust)
                {
                    imagesToUpdate.Add(image.GetDateTimeColumnTuples());
                }
                database.UpdateImages(imagesToUpdate);  // Write the updates to the database
                backgroundWorker.ReportProgress(0, new FeedbackTuple(null, "Done."));
            };
            backgroundWorker.ProgressChanged += (o, ea) =>
            {
                feedbackRows.Add((FeedbackTuple)ea.UserState);
                this.feedbackGrid.ScrollIntoView(feedbackGrid.Items[feedbackGrid.Items.Count - 1]);
            };
            backgroundWorker.RunWorkerCompleted += (o, ea) =>
            {
                this.StartDoneButton.IsEnabled = true;
            };
            backgroundWorker.RunWorkerAsync();
        }

        // Used to label the datagrid feedback columns with the appropriate headers
        private void DatagridFeedback_AutoGeneratedColumns(object sender, EventArgs e)
        {
            this.feedbackGrid.Columns[0].Header = "File Name";
            this.feedbackGrid.Columns[1].Header = "Date and Time Changes";
        }
    }
}
