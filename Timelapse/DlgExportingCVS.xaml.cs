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
    /// Dialog: Tell the user that files are being exported, along with the option to not show this dialog again
    /// True: show again
    /// False: don't show again
    /// </summary>
    public partial class DlgExportingCSV : Window
    {
        /// <summary>
        /// Tell the user that files are being exported, along with the option to not show this dialog again
        /// True: show again
        /// False: don't show again
        /// </summary>
        public DlgExportingCSV()
        {
            InitializeComponent();
        }

        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = (this.chkboxShowAgain.IsChecked == true) ? true : false;
        }
    }
}
