using Carnassial.Database;
using Carnassial.Util;
using System;
using System.Diagnostics;
using System.Windows;

namespace Carnassial.Dialog
{
    /// <summary>
    /// This dialog lets a user enter a time change correction of +/-1 hour, which is propagated backwards/forwards.
    /// The current image as set by the user in the radio buttons.
    /// </summary>
    public partial class DateDaylightSavingsTimeCorrection : Window
    {
        private int currentFileIndex;
        private FileDatabase database;

        public DateDaylightSavingsTimeCorrection(FileDatabase database, FileTableEnumerator fileEnumerator, Window owner)
        {
            this.InitializeComponent();
            this.currentFileIndex = fileEnumerator.CurrentRow;
            this.database = database;
            this.Owner = owner;

            // Get the original date and display it
            this.OriginalDate.Content = fileEnumerator.Current.GetDisplayDateTime();

            this.Image.Source = fileEnumerator.Current.LoadBitmap(this.database.FolderPath);
            this.FileName.Content = fileEnumerator.Current.FileName;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Utilities.SetDefaultDialogPosition(this);
            Utilities.TryFitWindowInWorkingArea(this);
        }

        // When the user clicks ok, add/subtract an hour propagated forwards/backwards as specified
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            try
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

                // Update the database
                int hours = (bool)this.AddHour.IsChecked ? 1 : -1;
                TimeSpan daylightSavingsAdjustment = new TimeSpan(hours, 0, 0);
                this.database.AdjustFileDateTimes(daylightSavingsAdjustment, startRow, endRow); // For all rows...
                this.DialogResult = true;
            }
            catch (Exception exception)
            {
                Debug.Fail("Adjustment of image times failed.", exception.ToString());
                this.DialogResult = false;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        // Examine the checkboxes to see what state our selection is in, and provide feedback as appropriate
        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if ((bool)this.AddHour.IsChecked || (bool)this.SubtractHour.IsChecked) 
            {
                DateTime dateTime;
                if (DateTimeHandler.TryParseDisplayDateTime((string)this.OriginalDate.Content, out dateTime) == false)
                {
                    this.NewDate.Content = "Problem with this date...";
                    this.OkButton.IsEnabled = false;
                    return;
                }
                int hours = ((bool)this.AddHour.IsChecked) ? 1 : -1;
                TimeSpan daylightSavingsAdjustment = new TimeSpan(hours, 0, 0);
                dateTime = dateTime.Add(daylightSavingsAdjustment);
                this.NewDate.Content = DateTimeHandler.ToDisplayDateTimeString(dateTime);
            }
            if (((bool)this.AddHour.IsChecked || (bool)this.SubtractHour.IsChecked) && ((bool)this.PropagateBackwards.IsChecked || (bool)this.PropagateForward.IsChecked))
            {
                this.OkButton.IsEnabled = true;
            }
            else
            {
                this.OkButton.IsEnabled = false;
            }
        }
    }
}
