﻿using System.Windows;
using System.Windows.Controls;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// This dialog lets the user edit text notes attached to this image set, ideally to keep a log of what is going on, if needed.
    /// The log is persisted.
    /// </summary>
    public partial class EditLog : Window
    {
        /// <summary>
        /// Raise a dialog that lets the user edit text given to it as a parameter  
        /// If the dialog returns true, the property LogContents will contain the modified text. 
        /// </summary>
        public EditLog(string text, Window owner)
        {
            this.InitializeComponent();
            Utilities.TryFitWindowInWorkingArea(this);
            this.Owner = owner;

            this.Log.Text = text;
            this.OkButton.IsEnabled = false;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void LogTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            this.OkButton.IsEnabled = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
    }
}
