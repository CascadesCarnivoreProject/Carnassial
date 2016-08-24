using System;
using System.IO;
using System.Windows;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// When there is more than one .ddb file in the image set folder, this dialog asks the user to choose the one they want.
    /// </summary>
    public partial class ChooseDatabaseFile : Window
    {
        // This will contain the file selected by the user
        public string SelectedFile { get; set; }

        public ChooseDatabaseFile(string[] fileNames)
        {
            this.InitializeComponent();
            Utilities.TryFitWindowInWorkingArea(this);
            this.SelectedFile = String.Empty;

            // file_names contains an array of .ddb files. We add each to the listbox.
            // by default, the first item in the listbox is shown selected.
            foreach (string fileName in fileNames)
            {
                this.lbFiles.Items.Add(Path.GetFileName(fileName));
                this.lbFiles.SelectedIndex = 0;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.SelectedFile = this.lbFiles.SelectedItem.ToString(); // The selected file
            this.DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
    }
}
