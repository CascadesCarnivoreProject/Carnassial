using System;
using System.Diagnostics;
using System.Windows;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    public partial class AboutTimelapse : Window
    {
        public AboutTimelapse(Window owner)
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

        private void CheckForUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            VersionClient updater = new VersionClient(Constants.ApplicationName, Constants.LatestVersionAddress);
            updater.TryGetAndParseVersion(true);
        }

        private void VersionChangesButton_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo(Constants.VersionChangesAddress.AbsoluteUri));
        }
    }
}
