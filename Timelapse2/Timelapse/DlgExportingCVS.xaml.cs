using System.Windows;

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
        public DlgExportingCSV(string filename)
        {
            InitializeComponent();
            this.runFname.Text = filename;
        }

        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = (this.chkboxShowAgain.IsChecked == true) ? true : false;
        }
    }
}
