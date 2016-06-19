﻿using System.Windows;

namespace Timelapse
{
    /// <summary>
    /// Dialog: Tell the user that files are being exported, along with the option to not show this dialog again
    /// True: show again
    /// False: don't show again
    /// </summary>
    public partial class DialogExportCsv : Window
    {
        /// <summary>
        /// Tell the user that files are being exported, along with the option to not show this dialog again
        /// True: show again
        /// False: don't show again
        /// </summary>
        public DialogExportCsv(string filename)
        {
            this.InitializeComponent();
            this.Message.MessageWhat += filename;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = (this.chkboxShowAgain.IsChecked == true) ? true : false;
        }
    }
}
