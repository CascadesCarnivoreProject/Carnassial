using System;
using System.Diagnostics;
using System.Windows;
using Timelapse.Util;

namespace TimelapseTemplateEditor
{
    /// <summary>
    /// Interaction logic for DialogAboutTimelapseEditor.xaml
    /// </summary>
    public partial class DialogAboutTimelapseEditor : Window
    {
        // TO DO: The Version string is not used. Can delete all references to this argument.
        public DialogAboutTimelapseEditor()
        {
            this.InitializeComponent();
            Version curVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            this.Version.Text = curVersion.ToString();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void BtnCheckForUpdate_Click(object sender, RoutedEventArgs e)
        {
            CheckForUpdate.GetAndParseVersion(this, true);
        }

        private void BtnVersionChanges_Click(object sender, RoutedEventArgs e)
        {
            Uri versionUri = new Uri("http://saul.cpsc.ucalgary.ca/timelapse/pmwiki.php?n=Main.TimelapseVersions#Editor");
            Process.Start(new ProcessStartInfo(versionUri.AbsoluteUri));
        }
    }
}
