using Carnassial.Github;
using Carnassial.Util;
using System;
using System.Diagnostics;
using System.Windows;

namespace Carnassial.Dialog
{
    public partial class About : Window
    {
        private Uri latestReleaseAddress;
        private Uri releasesAddress;

        public About(Window owner)
        {
            this.InitializeComponent();
            Utilities.TryFitWindowInWorkingArea(this);

            this.latestReleaseAddress = CarnassialConfigurationSettings.GetLatestReleaseAddress();
            this.Owner = owner;
            this.releasesAddress = CarnassialConfigurationSettings.GetReleasesAddress();
            this.Version.Text = typeof(About).Assembly.GetName().Version.ToString();

            this.CheckForNewerRelease.IsEnabled = this.latestReleaseAddress != null;
            this.ViewReleases.IsEnabled = this.releasesAddress != null;
        }

        private void CheckForUpdate_Click(object sender, RoutedEventArgs e)
        {
            GithubReleaseClient updater = new GithubReleaseClient(Constant.ApplicationName, this.latestReleaseAddress);
            updater.TryGetAndParseRelease(true);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void VersionChanges_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo(this.releasesAddress.AbsoluteUri));
        }
    }
}
