using Carnassial.Database;
using Carnassial.Util;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Carnassial.Dialog
{
    public partial class DateTimeSetTimeZone : Window
    {
        private bool displayingPreview;
        private ImageDatabase imageDatabase;

        public bool Abort { get; private set; }
        
        // Create the interface
        public DateTimeSetTimeZone(ImageDatabase imageDatabase, ImageRow imageToCorrect, Window owner)
        {
            this.InitializeComponent();
            this.Abort = false;
            this.displayingPreview = false;
            this.imageDatabase = imageDatabase;
            this.Owner = owner;

            // get the image's current time
            DateTimeOffset currentDateTime;
            TimeZoneInfo imageSetTimeZone = this.imageDatabase.ImageSet.GetTimeZone();
            if (imageToCorrect.TryGetDateTime(imageSetTimeZone, out currentDateTime) == false)
            {
                this.Abort = true;
                return;
            }
            this.originalDate.Content = DateTimeHandler.ToDisplayDateTimeUtcOffsetString(currentDateTime);

            // get the image filename and display it
            this.imageName.Content = imageToCorrect.FileName;

            // display the image
            this.image.Source = imageToCorrect.LoadBitmap(this.imageDatabase.FolderPath);

            // configure timezone picker
            this.TimeZones.SelectedItem = imageSetTimeZone.DisplayName;
            this.TimeZones.SelectionChanged += this.TimeZones_SelectionChanged;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Utilities.SetDefaultDialogPosition(this);
            Utilities.TryFitWindowInWorkingArea(this);
        }

        private void PreviewDateTimeChanges()
        {
            this.PrimaryPanel.Visibility = Visibility.Collapsed;
            this.FeedbackPanel.Visibility = Visibility.Visible;

            // Preview the changes
            TimeZoneInfo imageSetTimeZone = this.imageDatabase.ImageSet.GetTimeZone();
            TimeZoneInfo newTimeZone = this.TimeZones.TimeZonesByDisplayName[(string)this.TimeZones.SelectedItem];
            foreach (ImageRow row in this.imageDatabase.ImageDataTable)
            {
                string newDateTime = String.Empty;
                string status = "Skipped: invalid date/time";
                DateTimeOffset currentImageDateTime;
                if (row.TryGetDateTime(imageSetTimeZone, out currentImageDateTime))
                {
                    TimeSpan utcOffset = newTimeZone.GetUtcOffset(currentImageDateTime);
                    DateTimeOffset previewImageDateTime = currentImageDateTime.SetOffset(utcOffset);
                    // Pretty print the adjustment time
                    if (currentImageDateTime != previewImageDateTime)
                    {
                        status = "Changed";
                        newDateTime = DateTimeHandler.ToDisplayDateTimeUtcOffsetString(previewImageDateTime);
                    }
                    else
                    {
                        status = "Unchanged";
                    }
                }
                this.DateUpdateFeedbackCtl.AddFeedbackRow(row.FileName, status, DateTimeHandler.ToDisplayDateTimeUtcOffsetString(currentImageDateTime), newDateTime, String.Empty);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void ChangesButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.TimeZones.SelectedItem == null)
            {
                this.DialogResult = false;
                return;
            }

            // 1st click: Show the preview before actually making any changes.
            if (this.displayingPreview == false)
            {
                this.PreviewDateTimeChanges();
                this.displayingPreview = true;
                this.ChangesButton.Content = "_Apply Changes";
                return;
            }

            // 2nd click
            // Update the database
            TimeZoneInfo newTimeZone = this.TimeZones.TimeZonesByDisplayName[(string)this.TimeZones.SelectedItem];
            this.imageDatabase.AdjustImageTimes(
                (DateTimeOffset imageDateTime) =>
                {
                    TimeSpan utcOffset = newTimeZone.GetUtcOffset(imageDateTime);
                    return imageDateTime.SetOffset(utcOffset);
                },
                0,
                this.imageDatabase.CurrentlySelectedImageCount - 1);
            this.DialogResult = true;
        }

        private void TimeZones_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.ChangesButton.IsEnabled = true;
        }
    }
}
