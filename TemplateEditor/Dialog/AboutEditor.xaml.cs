using Carnassial.Util;
using System;
using System.Diagnostics;
using System.Windows;

namespace Carnassial.Editor.Dialog
{
    public partial class AboutEditor : Window
    {
        private Uri latestVersionAddress;
        private Uri versionChangesAddress;

        public AboutEditor(Window owner)
        {
            this.InitializeComponent();
            Utilities.TryFitWindowInWorkingArea(this);

            this.latestVersionAddress = CarnassialConfigurationSettings.GetLatestVersionAddress();
            this.Owner = owner;
            this.Version.Text = typeof(AboutEditor).Assembly.GetName().Version.ToString();
            this.versionChangesAddress = CarnassialConfigurationSettings.GetVersionChangesAddress();

            this.CheckForUpdate.IsEnabled = this.latestVersionAddress != null;
            this.VersionChanges.IsEnabled = this.versionChangesAddress != null;
        }

        private void CheckForUpdate_Click(object sender, RoutedEventArgs e)
        {
            VersionClient updater = new VersionClient(Constants.ApplicationName, this.latestVersionAddress);
            updater.TryGetAndParseVersion(true);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void VersionChanges_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo(this.versionChangesAddress.AbsoluteUri));
        }
    }
}
