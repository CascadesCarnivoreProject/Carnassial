using System.Windows;
using Timelapse.Util;

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
            Utilities.TryFitWindowInWorkingArea(this);
            this.Message.What += filename;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = (this.chkboxShowAgain.IsChecked == true) ? true : false;
        }
    }
}
