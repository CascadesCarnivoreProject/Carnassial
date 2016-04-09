﻿using System.Windows;

namespace Timelapse
{
    /// <summary>
    /// Dialog to ask the user to indicate the path to a code template file, which is invoked when there is no code template file in the image folder. 
    /// If a code template file is found, it is copied to the image folder. 
    /// </summary>
    public partial class DlgImportImageDataXMLFile : Window
    {
        #region Public methods
        /// <summary>
        /// Ask the user to indicate the path to a code template file (called if there is no code template file in the image folder). 
        /// If a code template file is found, it is copied to the image folder. 
        /// </summary>
        /// <param name="path"></param>
        public DlgImportImageDataXMLFile()
        {
            InitializeComponent();
        }

        #endregion

        #region Private methods
        // Browse for a code template file
        private void UseOldDataButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void IgnoreOldDataButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
        #endregion        
    }
}