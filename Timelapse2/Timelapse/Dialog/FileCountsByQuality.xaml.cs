﻿using System;
using System.Collections.Generic;
using System.Windows;
using Timelapse.Database;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Dialog to show the user some statistics about the images
    /// </summary>
    public partial class FileCountsByQuality : Window
    {
        /// <summary>
        /// Show the user some statistics about the images in a dialog box
        /// </summary>
        public FileCountsByQuality(Dictionary<ImageFilter, int> counts)
        {
            this.InitializeComponent();
            Utilities.TryFitWindowInWorkingArea(this);

            // Fill in the counts
            int ok = counts[ImageFilter.Ok];
            this.Ok.Text = String.Format("{0,5}", ok) + this.Ok.Text;
            int missing = counts[ImageFilter.Missing];
            this.Missing.Text = String.Format("{0,5}", missing) + this.Missing.Text;
            int dark = counts[ImageFilter.Dark];
            this.Dark.Text = String.Format("{0,5}", dark) + this.Dark.Text;
            int corrupted = counts[ImageFilter.Corrupted];
            this.Corrupted.Text = String.Format("{0,5}", corrupted) + this.Corrupted.Text;

            int total = ok + dark + corrupted + missing;
            this.Total.Text = String.Format("{0,5}", total) + this.Total.Text;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
    }
}