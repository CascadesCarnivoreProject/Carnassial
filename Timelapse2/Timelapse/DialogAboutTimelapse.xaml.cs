using System;
using System.Diagnostics;
using System.Windows;
using Timelapse.Util;

namespace Timelapse
{
    /// <summary>
    /// Interaction logic for DialogAboutTimelapse.xaml
    /// </summary>
    public partial class DialogAboutTimelapse : Window
    {
        public DialogAboutTimelapse()
        {
            this.InitializeComponent();
            Version curVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            this.Version.Text = curVersion.ToString();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void CheckForUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            CheckForUpdate.GetAndParseVersion(this, true);
        }

        private void VersionChangesButton_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo(Constants.VersionChangesAddress.AbsoluteUri));
        }
    }
}
