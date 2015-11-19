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
    /// Dialog: tell the user we are using the last write time of the file
    /// </summary>
    public partial class DlgUsingFileLastWriteTime : Window
    {
        /// <summary>
        /// Dialog: tell the user we are using the last write time of the file
        /// </summary>
        public DlgUsingFileLastWriteTime()
        {
            InitializeComponent();
        }

        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
    }
}
