using System;
using System.Diagnostics;
using System.Windows;
using Timelapse.Util;

namespace Timelapse.Editor
{
    public partial class DialogAboutTimelapseEditor : Window
    {
        public DialogAboutTimelapseEditor(Window owner)
        {
            this.InitializeComponent();
            Utilities.TryFitWindowInWorkingArea(this);

            this.Owner = owner;
            Version curVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            this.Version.Text = curVersion.ToString();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void BtnCheckForUpdate_Click(object sender, RoutedEventArgs e)
        {
            VersionClient updater = new VersionClient(EditorConstant.ApplicationName, EditorConstant.LatestVersionAddress);
            updater.TryGetAndParseVersion(true);
        }

        private void BtnVersionChanges_Click(object sender, RoutedEventArgs e)
        {
            Uri versionUri = new Uri("http://saul.cpsc.ucalgary.ca/timelapse/pmwiki.php?n=Main.TimelapseVersions#Editor");
            Process.Start(new ProcessStartInfo(versionUri.AbsoluteUri));
        }
    }
}
