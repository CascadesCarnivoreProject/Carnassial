using System.Windows;
using System.Windows.Controls;

namespace Timelapse
{
    /// <summary>
    /// Interaction logic for DlgRenameDataFile.xaml
    /// </summary>
    public partial class DlgRenameDataFile : Window
    {
        private string original_filename = "";
        public string new_filename = "";
        public DlgRenameDataFile(string file_name)
        {
            this.original_filename = file_name;
            this.new_filename = System.IO.Path.GetFileNameWithoutExtension (file_name);
            InitializeComponent();
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.runOriginalFileName.Text = this.original_filename;
            this.txtboxNewFileName.Text = this.new_filename;
            this.OkButton.IsEnabled = false;
            txtboxNewFileName.TextChanged += TxtboxNewFileName_TextChanged;
        }

        private void TxtboxNewFileName_TextChanged(object sender, TextChangedEventArgs e)
        {
            this.new_filename = this.txtboxNewFileName.Text + ".ddb";
            this.OkButton.IsEnabled = !(this.new_filename.Equals(this.original_filename)); // Enable the button only if the two names differ
        }

        #region Private Methods
        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
        #endregion
    }
}
