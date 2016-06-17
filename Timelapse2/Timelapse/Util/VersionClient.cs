using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Xml;

namespace Timelapse.Util
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

        // Checks for updates by comparing the current version number with a version stored on the Timelapse website in an xml file.
        public bool TryGetAndParseVersion(bool showNoUpdatesMessage)
        {
            Version publicallyAvailableVersion = null;  // if a newVersion variable, we will store the version info from xml file   
            string url = String.Empty; // THE URL where the new version is located
            string changes = String.Empty; // A list of changes held in the xml file
            XmlTextReader reader = null;
            try
            {
                // provide the XmlTextReader with the URL of our xml document  
                reader = new XmlTextReader(this.latestVersionAddress.AbsoluteUri);
                reader.MoveToContent(); // simply (and easily) skip the junk at the beginning  

                // internal - as the XmlTextReader moves only forward, we save current xml element name in elementName variable. 
                // When we parse a  text node, we refer to elementName to check what was the node name  
                string elementName = String.Empty;
                // we check if the xml starts with a proper "ourfancyapp" element node  
                if ((reader.NodeType == XmlNodeType.Element) && (reader.Name == Constants.VersionXml.Timelapse))
                {
                    while (reader.Read())
                    {
                        // when we find an element node, we remember its name  
                        if (reader.NodeType == XmlNodeType.Element)
                        {
                            elementName = reader.Name;
                        }
                        else
                        {
                            // for text nodes...  
                            if ((reader.NodeType == XmlNodeType.Text) && reader.HasValue)
                            {
                                // we check what the name of the node was  
                                switch (elementName)
                                {
                                    case Constants.VersionXml.Version:
                                        // we keep the version info in xxx.xxx.xxx.xxx format as the Version class does the  parsing for us  
                                        publicallyAvailableVersion = new Version(reader.Value);
                                        break;
                                    case Constants.VersionXml.Url:
                                        url = reader.Value;
                                        break;
                                    case Constants.VersionXml.Changes:
                                        changes = reader.Value;
                                        break;
                                }
                            }
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
                DialogMessageBox messageBox = new DialogMessageBox();
                messageBox.MessageTitle = String.Format("A new version of {0} is available.", this.applicationName);
                messageBox.MessageWhat = String.Format("You a running an old version of {0}: version {1}", this.applicationName, currentVersion);
                messageBox.MessageReason = String.Format("A new version of {0} is available: version {1}", this.applicationName, publicallyAvailableVersion);
                messageBox.MessageSolution = "Select 'Yes' to go to the website and download it.";
                messageBox.MessageResult = "The new version will contain these changes and more:";
                messageBox.MessageResult += changes;
                messageBox.MessageHint = "\u2022 We recommend downloading the latest version." + Environment.NewLine;
                messageBox.MessageHint += "\u2022 To see all changes, go to http://saul.cpsc.ucalgary.ca/timelapse. Select 'Version history' from the side bar.";
                messageBox.IconType = MessageBoxImage.Exclamation;
                messageBox.ButtonType = MessageBoxButton.YesNo;
                bool? messageBoxResult = messageBox.ShowDialog();

                // Set the filter to show all images and a valid image
                if (messageBoxResult == true)
                {
                    // navigate the default web browser to our app homepage (the url comes from the xml content)  
                    Process.Start(url);
                }
            }
            else if (showNoUpdatesMessage)
            {
                DialogMessageBox messageBox = new DialogMessageBox();
                messageBox.MessageTitle = String.Format("No updates to {0} are available.", this.applicationName);
                messageBox.MessageReason = String.Format("You a running the latest version of {0}, version: {1}", this.applicationName, currentVersion);
                messageBox.IconType = MessageBoxImage.Information;
                messageBox.ButtonType = MessageBoxButton.OK;
                bool? messageBoxResult = messageBox.ShowDialog();
            }

            return true;
        }
    }
}
