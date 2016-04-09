using System.IO;
using System.Windows;

namespace Timelapse
{
    /// <summary>
    /// Dialog to ask the user to indicate the path to a code template file, which is invoked when there is no code template file in the image folder. 
    /// If a code template file is found, it is copied to the image folder. 
    /// </summary>
    public partial class DlgGetTemplateDB : Window
    {
        private string path;

        #region Public methods
        /// <summary>
        /// Ask the user to indicate the path to a code template file (called if there is no code template file in the image folder). 
        /// If a code template file is found, it is copied to the image folder. 
        /// </summary>
        /// <param name="path"></param>
        public DlgGetTemplateDB(string path)
        {
            InitializeComponent();
            this.path = path;
            RunPath.Text += path;
            if (File.Exists (System.IO.Path.Combine (path, Constants.XMLTEMPLATEFILENAME)))
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

        #endregion

        #region Private methods
        // Browse for a code template file
        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            string templateFile = this.GetTemplatePathFromUser(path);
            if (null != templateFile)
            {
                File.Copy(templateFile, System.IO.Path.Combine(this.path, Constants.DBTEMPLATEFILENAME));
                this.DialogResult = true;
            }
            else this.DialogResult = false;
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
        #endregion

        // Get a location for the Template file from the user. Return null on failure
        private string GetTemplatePathFromUser(string defaultPath)
        {
            // Get the folder where the images reside
            var fbd = new System.Windows.Forms.OpenFileDialog();

            fbd.CheckFileExists = true;
            fbd.CheckPathExists = true;
            fbd.Multiselect = false;
            fbd.InitialDirectory = defaultPath;

            // Set filter for file extension and default file extension 
            fbd.DefaultExt = ".tdb";
            fbd.Filter = "Template files (.tdb)|*.tdb";


            fbd.Title = "Select a TimelapseTemplate.tdb file,  which will be copied to your image folder";
            string path = defaultPath;                     // Retrieve the last opened image path from the registry

            if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                return (fbd.FileName);                                      // Standard user-selected path
            }
            return null;                                                        // User must have aborted the operation
        }
        
    }
}
