using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Timelapse
{
    /// <summary>
    /// Dialog that shows a helpful error message when the user tries to open a folder with no images in it.
    /// </summary>
    public partial class DlgNoImagesInFolder : Window
    {
        /// <summary>
        /// Dialog that shows a helpful error message when the user tries to open a folder with no images in it.
        /// </summary>
        /// <param name="path"></param>
        public DlgNoImagesInFolder(string path)
        {
            InitializeComponent();
            this.FolderName.Text += path; // put the folder name in the dialog box           
        }

        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
    }
}
