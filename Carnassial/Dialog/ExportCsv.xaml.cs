using Carnassial.Util;
using System.Windows;

namespace Carnassial.Dialog
{
    /// <summary>
    /// Dialog: Tell the user that files are being exported, along with the option to not show this dialog again
    /// </summary>
    public partial class ExportCsv : Window
    {
        public ExportCsv(string filename, Window owner)
        {
            this.InitializeComponent();
            Utilities.TryFitWindowInWorkingArea(this);
            this.Message.What += filename;
            this.Owner = owner;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
    }
}
