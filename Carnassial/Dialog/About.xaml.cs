using Carnassial.Github;
using Carnassial.Util;
using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Navigation;

namespace Carnassial.Dialog
{
    public partial class About : WindowWithSystemMenu
    {
        private Uri latestReleaseAddress;
        private Uri releasesAddress;

        public Nullable<DateTime> MostRecentCheckForUpdate { get; private set; }

        public About(Window owner)
        {
            this.InitializeComponent();

            this.latestReleaseAddress = CarnassialConfigurationSettings.GetLatestReleaseApiAddress();
            this.MostRecentCheckForUpdate = null;
            this.Owner = owner;
            this.releasesAddress = CarnassialConfigurationSettings.GetReleasesBrowserAddress();
            this.Version.Text = typeof(About).Assembly.GetName().Version.ToString();

            this.CheckForNewerRelease.IsEnabled = this.latestReleaseAddress != null;
            this.ViewReleases.IsEnabled = this.releasesAddress != null;

            // configure hyperlinks
            // Since InlineCollection invalidates its iterator when the overall sequence of inlines is modified buffering is 
            // required to replace hyperlinks' own inlines.  Hence the call to ToList() in the foreach() below.  From a code
            // perspective completing link configuration from code behind is somewhat awkward but it's less complex than the
            // alternatives of 1) converting configuration settings into resources and constructing hierarchical resources, 
            // 2) sharding the terms of use into many small resources for localization (which, among other difficulties, 
            // prevents ordering the hyperlinks in whichever sequence is culturally most natural), 3) creating a user control 
            // to make the hyperlinks accessible from property getters, or 4) hooking Initialized or an equivalent event on 
            // the resource.  The check on .Tag here is equivalent of Lazy<Span>; while the user might open the about box
            // many times all of the about instances will use the same terms of use since it's a singleton in the resource
            // dictionary.
            Span termsOfUse = App.FindResource<Span>(Constant.ResourceKey.AboutTermsOfUse);
            if (termsOfUse.Tag == null)
            {
                Hyperlink emailLink = (Hyperlink)LogicalTreeHelper.FindLogicalNode(termsOfUse, Constant.ResourceName.AboutEmailLink);
                emailLink.NavigateUri = CarnassialConfigurationSettings.GetDevTeamEmailLink();
                emailLink.Inlines.Clear();
                emailLink.Inlines.Add(emailLink.NavigateUri.ToEmailAddress());
                emailLink.RequestNavigate += this.Hyperlink_RequestNavigate;
                emailLink.ToolTip = emailLink.NavigateUri.ToEmailAddress();

                Hyperlink issuesLink = (Hyperlink)LogicalTreeHelper.FindLogicalNode(termsOfUse, Constant.ResourceName.AboutIssuesLink);
                issuesLink.NavigateUri = CarnassialConfigurationSettings.GetIssuesBrowserAddress();
                issuesLink.Inlines.Clear();
                issuesLink.Inlines.Add(issuesLink.NavigateUri.AbsoluteUri);
                issuesLink.RequestNavigate += this.Hyperlink_RequestNavigate;
                issuesLink.ToolTip = issuesLink.NavigateUri.AbsoluteUri;

                termsOfUse.Tag = true;
            }
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
