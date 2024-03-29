﻿using Carnassial.Data;
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
            this.Color.Text = String.Format(CultureInfo.InvariantCulture, "{0,5}", color);
            int greyscale = fileCountByClassification[FileClassification.Greyscale];
            this.Greyscale.Text = String.Format(CultureInfo.InvariantCulture, "{0,5}", greyscale);
            int video = fileCountByClassification[FileClassification.Video];
            this.Video.Text = String.Format(CultureInfo.InvariantCulture, "{0,5}", video);
            int fileNoLongerAvailable = fileCountByClassification[FileClassification.NoLongerAvailable];
            this.FileNoLongerAvailable.Text = String.Format(CultureInfo.InvariantCulture, "{0,5}", fileNoLongerAvailable);
            int dark = fileCountByClassification[FileClassification.Dark];
            this.Dark.Text = String.Format(CultureInfo.InvariantCulture, "{0,5}", dark);
            int corrupted = fileCountByClassification[FileClassification.Corrupt];
            this.Corrupted.Text = String.Format(CultureInfo.InvariantCulture, "{0,5}", corrupted);

            int total = color + greyscale + dark + video + corrupted + fileNoLongerAvailable;
            this.Total.Text = String.Format(CultureInfo.InvariantCulture, "{0,5}", total);
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