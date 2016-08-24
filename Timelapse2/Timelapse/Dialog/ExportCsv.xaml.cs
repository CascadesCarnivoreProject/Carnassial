using System.Windows;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Dialog: Tell the user that files are being exported, along with the option to not show this dialog again
    /// True: show again
    /// False: don't show again
    /// </summary>
    public partial class ExportCsv : Window
    {
        // Whether to display the dialog box next time around
        public bool ShowAgain { get; private set; }

        public ExportCsv(string filename)
        {
            this.InitializeComponent();
            Utilities.TryFitWindowInWorkingArea(this);
            this.Message.What += filename;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.ShowAgain = (this.chkboxShowAgain.IsChecked == true) ? true : false;
            this.DialogResult = true; 
        }

        private void ChkboxShowAgain_CheckedChanged(object sender, RoutedEventArgs e)
        {
            this.ShowAgain = ((bool)chkboxShowAgain.IsChecked) ? true : false;
        }
    }
}
