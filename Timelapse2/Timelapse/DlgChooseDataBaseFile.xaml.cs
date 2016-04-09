using System.Windows;

namespace Timelapse
{
    /// <summary>
    /// When there is more than one .ddb file in the image set folder, this dialog asks the user to choose the one they want.
    /// </summary>
    public partial class DlgChooseDataBaseFile : Window
    {
        public string selectedFile = "";  // This will contain the file selected by the user

        public DlgChooseDataBaseFile(string[] file_names)
        {
            InitializeComponent();

            // file_names contains an array of .ddb files. We add each to the listbox.
            // by default, the first item in the listbox is shown selected.
            foreach (string s in file_names)
            {
                lbFiles.Items.Add(System.IO.Path.GetFileName(s));
                lbFiles.SelectedIndex = 0;
            }
        }

        #region Private Methods
        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            selectedFile = lbFiles.SelectedItem.ToString(); // The selected file
            this.DialogResult = true;
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
        #endregion
    }
}
