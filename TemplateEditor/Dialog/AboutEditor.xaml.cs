using Carnassial.Github;
using Carnassial.Util;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace Carnassial.Editor.Dialog
{
    public partial class AboutEditor : Window
    {
        private Uri latestReleaseAddress;
        private Uri releasesAddress;

        public Nullable<DateTime> MostRecentCheckForUpdate { get; private set; }

        public AboutEditor(Window owner)
        {
            this.InitializeComponent();

            this.latestReleaseAddress = CarnassialConfigurationSettings.GetLatestReleaseApiAddress();
            this.MostRecentCheckForUpdate = null;
            this.Owner = owner;
            this.releasesAddress = CarnassialConfigurationSettings.GetReleasesBrowserAddress();
            this.Version.Text = typeof(AboutEditor).Assembly.GetName().Version.ToString();

            this.CheckForNewerRelease.IsEnabled = this.latestReleaseAddress != null;
            this.EmailLink.NavigateUri = CarnassialConfigurationSettings.GetDevTeamEmailLink();
            string emailAddress = this.EmailLink.NavigateUri.ToEmailAddress();
            this.EmailLink.Inlines.Clear();
            this.EmailLink.Inlines.Add(emailAddress);
            this.EmailLink.ToolTip = emailAddress;
            this.IssuesLink.NavigateUri = CarnassialConfigurationSettings.GetIssuesBrowserAddress();
            this.IssuesLink.Inlines.Clear();
            this.IssuesLink.Inlines.Add(this.IssuesLink.NavigateUri.AbsoluteUri);
            this.IssuesLink.ToolTip = this.IssuesLink.NavigateUri.AbsoluteUri;
            this.ViewReleases.IsEnabled = this.releasesAddress != null;
        }

        private void CheckForUpdate_Click(object sender, RoutedEventArgs e)
        {
            GithubReleaseClient updater = new GithubReleaseClient(Constant.ApplicationName, this.latestReleaseAddress);
            if (updater.TryGetAndParseRelease(true, out Version publiclyAvailableVersion))
            {
                this.MostRecentCheckForUpdate = DateTime.UtcNow;
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            if (e.Uri != null)
            {
                Process.Start(e.Uri.AbsoluteUri);
                e.Handled = true;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void VersionChanges_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(this.releasesAddress.AbsoluteUri);
            e.Handled = true;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CommonUserInterface.SetDefaultDialogPosition(this);
            CommonUserInterface.TryFitWindowInWorkingArea(this);
        }
    }
}
