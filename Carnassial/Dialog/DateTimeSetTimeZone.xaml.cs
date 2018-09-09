using Carnassial.Data;
using Carnassial.Images;
using Carnassial.Native;
using Carnassial.Util;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Carnassial.Dialog
{
    public partial class DateTimeSetTimeZone : Window
    {
        private readonly FileDatabase fileDatabase;
        private readonly ImageCache imageCache;

        private bool displayingPreview;

        public DateTimeSetTimeZone(FileDatabase fileDatabase, ImageCache imageCache, Window owner)
        {
            this.InitializeComponent();
            this.displayingPreview = false;
            this.fileDatabase = fileDatabase;
            this.imageCache = imageCache;
            this.Owner = owner;

            // get the file's current time
            DateTimeOffset currentDateTime = this.imageCache.Current.DateTimeOffset;
            this.OriginalDate.Content = DateTimeHandler.ToDisplayDateTimeUtcOffsetString(currentDateTime);

            // get the filename and display it
            this.FileName.Content = this.imageCache.Current.FileName;

            // configure timezone picker
            this.TimeZones.SelectTimeZone(this.fileDatabase.ImageSet.GetTimeZoneInfo());
            this.TimeZones.SelectionChanged += this.TimeZones_SelectionChanged;
        }

        private void PreviewDateTimeChanges()
        {
            TimeZoneInfo newTimeZone = this.TimeZones.TimeZonesByDisplayIdentifier[(string)this.TimeZones.SelectedItem];
            foreach (ImageRow file in this.fileDatabase.Files)
            {
                string newDateTime = String.Empty;
                string status = "Skipped: invalid date/time";
                DateTimeOffset currentFileDateTime = file.DateTimeOffset;
                TimeSpan utcOffset = newTimeZone.GetUtcOffset(currentFileDateTime);
                DateTimeOffset previewFileDateTime = currentFileDateTime.SetOffset(utcOffset);

                // Pretty print the adjustment time
                if (currentFileDateTime != previewFileDateTime)
                {
                    status = "Changed";
                    newDateTime = DateTimeHandler.ToDisplayDateTimeUtcOffsetString(previewFileDateTime);
                }
                else
                {
                    status = "Unchanged";
                }

                this.TimeZoneUpdateFeedback.AddFeedbackRow(file.FileName, status, DateTimeHandler.ToDisplayDateTimeUtcOffsetString(currentFileDateTime), newDateTime, String.Empty);
            }

            this.PrimaryPanel.Visibility = Visibility.Collapsed;
            this.FeedbackPanel.Visibility = Visibility.Visible;
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
            TimeZoneInfo newTimeZone = this.TimeZones.TimeZonesByDisplayIdentifier[(string)this.TimeZones.SelectedItem];
            this.fileDatabase.AdjustFileTimes(
                (DateTimeOffset fileDateTime) =>
                {
                    TimeSpan utcOffset = newTimeZone.GetUtcOffset(fileDateTime);
                    return fileDateTime.SetOffset(utcOffset);
                },
                0,
                this.fileDatabase.CurrentlySelectedFileCount - 1);
            this.DialogResult = true;
        }

        private void TimeZones_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.ChangesButton.IsEnabled = true;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CommonUserInterface.SetDefaultDialogPosition(this);
            CommonUserInterface.TryFitWindowInWorkingArea(this);

            await this.FileDisplay.DisplayAsync(this.fileDatabase.FolderPath, this.imageCache);
        }
    }
}
