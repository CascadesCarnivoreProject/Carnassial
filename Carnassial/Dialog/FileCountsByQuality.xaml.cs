using Carnassial.Data;
using Carnassial.Util;
using System;
using System.Collections.Generic;
using System.Windows;

namespace Carnassial.Dialog
{
    /// <summary>
    /// Displays breakdown of file classifications.
    /// </summary>
    public partial class FileCountsByClassification : Window
    {
        public FileCountsByClassification(Dictionary<FileClassification, int> fileCountByClassification, Window owner)
        {
            this.InitializeComponent();
            Utilities.TryFitWindowInWorkingArea(this);
            this.Owner = owner;

            // fill in the counts
            int color = fileCountByClassification[FileClassification.Color];
            this.Color.Text = String.Format("{0,5}", color);
            int greyscale = fileCountByClassification[FileClassification.Greyscale];
            this.Greyscale.Text = String.Format("{0,5}", greyscale);
            int video = fileCountByClassification[FileClassification.Video];
            this.Video.Text = String.Format("{0,5}", video);
            int fileNoLongerAvailable = fileCountByClassification[FileClassification.NoLongerAvailable];
            this.FileNoLongerAvailable.Text = String.Format("{0,5}", fileNoLongerAvailable);
            int dark = fileCountByClassification[FileClassification.Dark];
            this.Dark.Text = String.Format("{0,5}", dark);
            int corrupted = fileCountByClassification[FileClassification.Corrupt];
            this.Corrupted.Text = String.Format("{0,5}", corrupted);

            int total = color + greyscale + dark + video + corrupted + fileNoLongerAvailable;
            this.Total.Text = String.Format("{0,5}", total);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Utilities.SetDefaultDialogPosition(this);
            Utilities.TryFitWindowInWorkingArea(this);
        }
    }
}