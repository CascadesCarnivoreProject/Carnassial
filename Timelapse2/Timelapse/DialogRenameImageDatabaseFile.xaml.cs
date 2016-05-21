using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Timelapse
{
    /// <summary>
    /// Interaction logic for DialogRenameImageDatabaseFile.xaml
    /// </summary>
    public partial class DialogRenameImageDatabaseFile : Window
    {
        private string originalFileName;
        public string NewFilename { get; private set; }

        public DialogRenameImageDatabaseFile(string fileName)
        {
            this.originalFileName = fileName;
            this.NewFilename = Path.GetFileNameWithoutExtension(fileName);
            this.InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
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
