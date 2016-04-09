﻿using System;
using System.Diagnostics;
using System.Windows;

namespace TimelapseTemplateEditor
{
    /// <summary>
    /// Interaction logic for DlgAboutTImelapseEditor.xaml
    /// </summary>
    public partial class DlgAboutTimelapseEditor : Window
    {
        // TO DO: The Version string is not used. Can delete all references to this argument.
        public DlgAboutTimelapseEditor()
        {
            InitializeComponent();
            Version curVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            this.Version.Text = curVersion.ToString();
        }

        private void okButton_Click(object sender, RoutedEventArgs e)
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