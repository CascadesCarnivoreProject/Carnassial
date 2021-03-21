using Carnassial.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Carnassial.Dialog
{
    /// <summary>
    /// When there is more than one .ddb file in the image set folder, this dialog asks the user to choose the one they want.
    /// </summary>
    public partial class ChooseFileDatabase : WindowWithSystemMenu
    {
        public string SelectedFile { get; set; }

        public ChooseFileDatabase(List<string> fileDatabasePaths, string templateDatabasePath, Window owner)
        {
            this.InitializeComponent();
            this.Message.SetVisibility();
            this.Owner = owner;
            this.SelectedFile = String.Empty;

            // populate list of file database names, setting the default to either the first or whichever has the same name as the template
            int defaultDatabaseIndex = 0;
            string templateDatabaseNameWithoutExtension = Path.GetFileNameWithoutExtension(templateDatabasePath);
            for (int index = 0; index < fileDatabasePaths.Count; ++index)
            {
                string databaseName = Path.GetFileName(fileDatabasePaths[index]);
                string databaseNameWithoutExtension = Path.GetFileNameWithoutExtension(databaseName);
                if (String.Equals(databaseNameWithoutExtension, templateDatabaseNameWithoutExtension, StringComparison.OrdinalIgnoreCase))
                {
                    defaultDatabaseIndex = index;
                }
                this.FileDatabases.Items.Add(databaseName);
            }

            this.FileDatabases.SelectedIndex = defaultDatabaseIndex;
        }

        private void FileDatabases_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (this.FileDatabases.SelectedIndex != -1)
            {
                this.OkButton_Click(sender, e);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void FileDatabases_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.OkButton.IsEnabled = this.FileDatabases.SelectedIndex != -1;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            string? selectedFile = this.FileDatabases.SelectedItem.ToString();
            Debug.Assert(String.IsNullOrWhiteSpace(selectedFile) == false);

            this.SelectedFile = selectedFile; // The selected file
            this.DialogResult = true;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CommonUserInterface.SetDefaultDialogPosition(this);
            CommonUserInterface.TryFitWindowInWorkingArea(this);

            // marking the OK button IsDefault to associate it with dialog completion also gives it initial focus
            // It's more helpful to put focus on the database list as this saves having to tab to the list as a first step.
            this.FileDatabases.Focus();
        }
    }
}
