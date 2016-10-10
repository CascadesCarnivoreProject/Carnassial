using Carnassial.Database;
using Carnassial.Util;
using System;
using System.Collections.Generic;
using System.Windows;

namespace Carnassial.Dialog
{
    /// <summary>
    /// Dialog to show the user some statistics about the images
    /// </summary>
    public partial class FileCountsByQuality : Window
    {
        /// <summary>
        /// Show the user some statistics about the images in a dialog box
        /// </summary>
        public FileCountsByQuality(Dictionary<FileSelection, int> counts, Window owner)
        {
            this.InitializeComponent();
            Utilities.TryFitWindowInWorkingArea(this);
            this.Owner = owner;

            // Fill in the counts
            int ok = counts[FileSelection.Ok];
            this.Ok.Text = String.Format("{0,5}", ok) + this.Ok.Text;
            int fileNoLongerAvailable = counts[FileSelection.NoLongerAvailable];
            this.FileNoLongerAvailable.Text = String.Format("{0,5}", fileNoLongerAvailable) + this.FileNoLongerAvailable.Text;
            int dark = counts[FileSelection.Dark];
            this.Dark.Text = String.Format("{0,5}", dark) + this.Dark.Text;
            int corrupted = counts[FileSelection.Corrupt];
            this.Corrupted.Text = String.Format("{0,5}", corrupted) + this.Corrupted.Text;

            int total = ok + dark + corrupted + fileNoLongerAvailable;
            this.Total.Text = String.Format("{0,5}", total) + this.Total.Text;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
    }
}