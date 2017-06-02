using Carnassial.Data;
using Carnassial.Images;
using Carnassial.Native;
using Carnassial.Util;
using System;
using System.Windows;

namespace Carnassial.Dialog
{
    /// <summary>
    /// This dialog lets a user enter a time change correction of +/-1 hour, which is propagated backwards or forwards.
    /// </summary>
    public partial class DateDaylightSavingsTimeCorrection : Window
    {
        private readonly int currentFileIndex;
        private readonly FileDatabase database;
        private readonly ImageRow fileToDisplay;
        private readonly DateTimeOffset orignalDateTimeOffset;

        public DateDaylightSavingsTimeCorrection(FileDatabase database, ImageCache imageCache, Window owner)
        {
            this.InitializeComponent();
            this.currentFileIndex = imageCache.CurrentRow;
            this.database = database;
            this.fileToDisplay = imageCache.Current;
            this.orignalDateTimeOffset = imageCache.Current.DateTimeOffset;
            this.Owner = owner;

            // display file properties
            this.FileName.Content = this.fileToDisplay.FileName;
            this.OriginalDate.Content = this.fileToDisplay.GetDisplayDateTime();
            MemoryImage image = imageCache.GetCurrentImage();
            image.SetSource(this.Image);
            this.HourButton_Checked(null, null);

            // hook event handlers
            this.AddHour.Checked += this.HourButton_Checked;
            this.SubtractHour.Checked += this.HourButton_Checked;
        }

        // add/subtract an hour propagated forwards/backwards as specified
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            int startRow = this.currentFileIndex;
            int endRow = this.currentFileIndex;
            if ((bool)this.PropagateForward.IsChecked)
            {
                endRow = this.database.CurrentlySelectedFileCount - 1;
            }
            else
            {
                startRow = 0;
            }

            // update the database
            int hours = (bool)this.AddHour.IsChecked ? 1 : -1;
            TimeSpan daylightSavingsAdjustment = new TimeSpan(hours, 0, 0);
            this.database.AdjustFileTimes(daylightSavingsAdjustment, startRow, endRow); // For all rows...
            this.DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void HourButton_Checked(object sender, RoutedEventArgs e)
        {
            int hours = ((bool)this.AddHour.IsChecked) ? 1 : -1;
            TimeSpan daylightSavingsAdjustment = new TimeSpan(hours, 0, 0);
            DateTime dateTime = this.orignalDateTimeOffset.DateTime.Add(daylightSavingsAdjustment);
            this.NewDate.Content = DateTimeHandler.ToDisplayDateTimeString(dateTime);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Utilities.SetDefaultDialogPosition(this);
            Utilities.TryFitWindowInWorkingArea(this);
        }
    }
}
