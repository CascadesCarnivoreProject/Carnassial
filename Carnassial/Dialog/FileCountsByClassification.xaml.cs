using Carnassial.Data;
using Carnassial.Util;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;

namespace Carnassial.Dialog
{
    /// <summary>
    /// Displays breakdown of file classifications.
    /// </summary>
    public partial class FileCountsByClassification : WindowWithSystemMenu
    {
        public FileCountsByClassification(Dictionary<FileClassification, int> fileCountByClassification, Window owner)
        {
            this.InitializeComponent();
            this.Message.SetVisibility();
            this.Owner = owner;

            // fill in the counts
            int color = fileCountByClassification[FileClassification.Color];
            this.Color.Text = String.Create(CultureInfo.InvariantCulture, $"{color,5}");
            int greyscale = fileCountByClassification[FileClassification.Greyscale];
            this.Greyscale.Text = String.Create(CultureInfo.InvariantCulture, $"{greyscale,5}");
            int video = fileCountByClassification[FileClassification.Video];
            this.Video.Text = String.Create(CultureInfo.InvariantCulture, $"{video,5}");
            int fileNoLongerAvailable = fileCountByClassification[FileClassification.NoLongerAvailable];
            this.FileNoLongerAvailable.Text = String.Create(CultureInfo.InvariantCulture, $"{fileNoLongerAvailable,5}");
            int dark = fileCountByClassification[FileClassification.Dark];
            this.Dark.Text = String.Create(CultureInfo.InvariantCulture, $"{dark,5}");
            int corrupted = fileCountByClassification[FileClassification.Corrupt];
            this.Corrupted.Text = String.Create(CultureInfo.InvariantCulture, $"{corrupted,5}");

            int total = color + greyscale + dark + video + corrupted + fileNoLongerAvailable;
            this.Total.Text = String.Create(CultureInfo.InvariantCulture, $"{total,5}");
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CommonUserInterface.SetDefaultDialogPosition(this);
            CommonUserInterface.TryFitWindowInWorkingArea(this);
        }
    }
}