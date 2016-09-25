﻿using Carnassial.Database;
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
        private int currentImageRow;
        private ImageDatabase database;

        public DateDaylightSavingsTimeCorrection(ImageDatabase database, ImageTableEnumerator image, Window owner)
        {
            this.InitializeComponent();
            this.currentImageRow = image.CurrentRow;
            this.database = database;
            this.Owner = owner;

            // Get the original date and display it
            this.OriginalDate.Content = image.Current.GetDisplayDateTime();

            // Display the image. While we should be on a valid image (our assumption), we can still show a missing or corrupted image if needed
            this.Image.Source = image.Current.LoadBitmap(this.database.FolderPath);
            this.ImageName.Content = image.Current.FileName;
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
                bool forward = (bool)rbForward.IsChecked;
                int startRow;
                int endRow;
                if (forward)
                {
                    startRow = this.currentImageRow;
                    endRow = this.database.CurrentlySelectedImageCount - 1;
                }
                else
                {
                    startRow = 0;
                    endRow = this.currentImageRow;
                }

                // Update the database
                int hours = (bool)rbAddHour.IsChecked ? 1 : -1;
                TimeSpan daylightSavingsAdjustment = new TimeSpan(hours, 0, 0);
                this.database.AdjustImageTimes(daylightSavingsAdjustment, startRow, endRow); // For all rows...
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
            if ((bool)rbAddHour.IsChecked || (bool)rbSubtractHour.IsChecked) 
            {
                DateTime dateTime;
                if (DateTimeHandler.TryParseDisplayDateTime((string)this.OriginalDate.Content, out dateTime) == false)
                {
                    this.NewDate.Content = "Problem with this date...";
                    this.OkButton.IsEnabled = false;
                    return;
                }
                int hours = ((bool)rbAddHour.IsChecked) ? 1 : -1;
                TimeSpan daylightSavingsAdjustment = new TimeSpan(hours, 0, 0);
                dateTime = dateTime.Add(daylightSavingsAdjustment);
                this.NewDate.Content = DateTimeHandler.ToDisplayDateTimeString(dateTime);
            }
            if (((bool)rbAddHour.IsChecked || (bool)rbSubtractHour.IsChecked) && ((bool)rbBackwards.IsChecked || (bool)rbForward.IsChecked))
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
