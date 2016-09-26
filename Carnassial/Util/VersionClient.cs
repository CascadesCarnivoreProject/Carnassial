using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Xml;
using MessageBox = Carnassial.Dialog.MessageBox;

namespace Carnassial.Util
{
    public class VersionClient
    {
        private Uri latestVersionAddress;
        private string applicationName;

        public VersionClient(string applicationName, Uri latestVersionAddress)
        {
            this.applicationName = applicationName;
            this.latestVersionAddress = latestVersionAddress;
        }

        // Checks for updates by comparing the current version number with a version stored on the Carnassial website in an xml file.
        public bool TryGetAndParseVersion(bool showNoUpdatesMessage)
        {
            Version publicallyAvailableVersion = null;  // if a newVersion variable, we will store the version info from xml file   
            string changes = null; // a list of changes held in the xml file
            string url = null; // the URL to go to for a new version
            XmlTextReader reader = null;
            try
            {
                reader = new XmlTextReader(this.latestVersionAddress.AbsoluteUri);
                reader.MoveToContent();

                if ((reader.NodeType == XmlNodeType.Element) && (reader.Name == Constants.VersionXml.Carnassial))
                {
                    while (reader.Read())
                    {
                        if (reader.IsStartElement(Constants.VersionXml.Version))
                        {
                            publicallyAvailableVersion = new Version(reader.ReadElementContentAsString());
                        }
                        else if (reader.IsStartElement(Constants.VersionXml.Version))
                        {
                            url = reader.ReadElementContentAsString();
                        }
                        else if (reader.IsStartElement(Constants.VersionXml.Version))
                        {
                            changes = reader.ReadElementContentAsString();
                        }
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                if (reader != null)
                {
                    reader.Close();
                }
            }

            // get the running version  
            Version currentVersion = Assembly.GetExecutingAssembly().GetName().Version;

            // compare the versions  
            if (currentVersion < publicallyAvailableVersion)
            {
                // ask the user if he would like to download the new version  
                string title = String.Format("A new version of {0} is available.", this.applicationName);
                MessageBox messageBox = new MessageBox(title, Application.Current.MainWindow, MessageBoxButton.YesNo);
                messageBox.Message.What = String.Format("You a running an old version of {0}: version {1}", this.applicationName, currentVersion);
                messageBox.Message.Reason = String.Format("A new version of {0} is available: version {1}", this.applicationName, publicallyAvailableVersion);
                messageBox.Message.Solution = "Select 'Yes' to go to the website and download it.";
                messageBox.Message.Result = "The new version will contain these changes and more:";
                messageBox.Message.Result += changes;
                messageBox.Message.Hint = "\u2022 We recommend downloading the latest version." + Environment.NewLine;
                messageBox.Message.Hint += String.Format(@"\u2022 To see all changes, go to {0} and select 'Version history'.", url);
                messageBox.Message.Icon = MessageBoxImage.Exclamation;
                bool? messageBoxResult = messageBox.ShowDialog();

                if (messageBoxResult == true)
                {
                    // navigate the default web browser to our app homepage (the url comes from the xml content)  
                    Process.Start(url);
                }
            }
            else if (showNoUpdatesMessage)
            {
                MessageBox messageBox = new MessageBox(String.Format("No updates to {0} are available.", this.applicationName), Application.Current.MainWindow);
                messageBox.Message.Reason = String.Format("You a running the latest version of {0}, version: {1}", this.applicationName, currentVersion);
                messageBox.Message.Icon = MessageBoxImage.Information;
                bool? messageBoxResult = messageBox.ShowDialog();
            }

            return true;
        }
    }
}
