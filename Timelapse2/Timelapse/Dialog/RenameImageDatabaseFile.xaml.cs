using System.IO;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    public partial class RenameImageDatabaseFile : Window
    {
        private string originalFileName;
        public string NewFilename { get; private set; }

        public RenameImageDatabaseFile(string fileName)
        {
            this.InitializeComponent();

            this.originalFileName = fileName;
            this.NewFilename = Path.GetFileNameWithoutExtension(fileName);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Utilities.SetDefaultDialogPosition(this);
            Utilities.TryFitWindowInWorkingArea(this);

            this.runOriginalFileName.Text = this.originalFileName;
            this.txtboxNewFileName.Text = this.NewFilename;
            this.OkButton.IsEnabled = false;
            this.txtboxNewFileName.TextChanged += this.TxtboxNewFileName_TextChanged;
        }

        private void TxtboxNewFileName_TextChanged(object sender, TextChangedEventArgs e)
        {
            this.NewFilename = this.txtboxNewFileName.Text + ".ddb";
            this.OkButton.IsEnabled = !this.NewFilename.Equals(this.originalFileName); // Enable the button only if the two names differ
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
    }
}
