using Carnassial.Util;
using System;
using System.Diagnostics;
using System.Windows;

namespace Carnassial.Dialog
{
    public partial class About : Window
    {
        private Uri latestVersionAddress;
        private Uri versionChangesAddress;

        public About(Window owner)
        {
            this.InitializeComponent();
            Utilities.TryFitWindowInWorkingArea(this);

            this.latestVersionAddress = CarnassialConfigurationSettings.GetLatestVersionAddress();
            this.Owner = owner;
            this.Version.Text = typeof(About).Assembly.GetName().Version.ToString();
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
