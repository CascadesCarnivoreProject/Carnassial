﻿using System.Windows;
using Timelapse.Util;

namespace Timelapse
{
    /// <summary>
    /// Dialog to ask the user to indicate the path to a code template file, which is invoked when there is no code template file in the image folder. 
    /// If a code template file is found, it is copied to the image folder. 
    /// </summary>
    public partial class DialogImportImageSetXmlFile : Window
    {
        /// <summary>
        /// Ask the user to indicate the path to a code template file (called if there is no code template file in the image folder). 
        /// If a code template file is found, it is copied to the image folder. 
        /// </summary>
        public DialogImportImageSetXmlFile()
        {
            this.InitializeComponent();
            Utilities.TryFitWindowInWorkingArea(this);
        }

        // Browse for a code template file
        private void UseOldDataButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void IgnoreOldDataButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
    }
}
