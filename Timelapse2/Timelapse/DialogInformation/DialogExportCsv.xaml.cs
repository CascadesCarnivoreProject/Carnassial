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
        // Whether to display the dialog box next time around
        private bool showAgain = true;
        public bool ShowAgain
        {
            get
            {
                return this.showAgain;
            }
        }

        public DialogExportCsv(string filename)
        {
            this.InitializeComponent();
            Utilities.TryFitWindowInWorkingArea(this);
            this.Message.What += filename;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.showAgain = (this.chkboxShowAgain.IsChecked == true) ? true : false;
            this.DialogResult = true; 
        }

        private void ChkboxShowAgain_CheckedChanged(object sender, RoutedEventArgs e)
        {
            this.showAgain = ((bool)chkboxShowAgain.IsChecked) ? true : false;
        }
    }
}
