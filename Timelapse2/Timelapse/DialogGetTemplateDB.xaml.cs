using System.IO;
using System.Windows;
using System.Windows.Forms;

namespace Timelapse
{
    /// <summary>
    /// Dialog to ask the user to indicate the path to a code template file, which is invoked when there is no code template file in the image folder. 
    /// If a code template file is found, it is copied to the image folder. 
    /// </summary>
    public partial class DialogGetTemplateDB : Window
    {
        private string path;

        /// <summary>
        /// Ask the user to indicate the path to a code template file (called if there is no code template file in the image folder). 
        /// If a code template file is found, it is copied to the image folder. 
        /// </summary>
        public DialogGetTemplateDB(string path)
        {
            this.InitializeComponent();
            this.path = path;
            this.RunPath.Text += path;
            if (File.Exists(Path.Combine(path, Constants.File.XmlTemplateFileName)))
            {
                this.CodeTemplateMessage1.Visibility = Visibility.Visible;
                this.CodeTemplateMessage2.Visibility = Visibility.Visible;
            }
            else
            {
                this.CodeTemplateMessage1.Visibility = Visibility.Collapsed;
                this.CodeTemplateMessage2.Visibility = Visibility.Collapsed;
            }
        }

        #region Private methods
        // Browse for a code template file
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            string templateFile = this.GetTemplatePathFromUser(this.path);
            if (templateFile != null)
            {
                File.Copy(templateFile, System.IO.Path.Combine(this.path, Constants.File.DefaultTemplateDatabaseFileName));
                this.DialogResult = true;
            }
            else
            {
                this.DialogResult = false;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
        #endregion

        // Get a location for the Template file from the user. Return null on failure
        private string GetTemplatePathFromUser(string defaultPath)
        {
            // Get the folder where the images reside
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.CheckFileExists = true;
            openFileDialog.CheckPathExists = true;
            openFileDialog.Multiselect = false;
            openFileDialog.InitialDirectory = defaultPath;

            // Set filter for file extension and default file extension 
            openFileDialog.DefaultExt = ".tdb";
            openFileDialog.Filter = "Template files (.tdb)|*.tdb";

            openFileDialog.Title = "Select a TimelapseTemplate.tdb file,  which will be copied to your image folder";
            string path = defaultPath;                     // Retrieve the last opened image path from the registry
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                return openFileDialog.FileName;                                      // Standard user-selected path
            }
            return null;                                                        // User must have aborted the operation
        }
    }
}
