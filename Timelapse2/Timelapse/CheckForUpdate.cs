using System;
using System.Windows;
using System.Xml;
namespace Timelapse
{
    class CheckForUpdate 
    {
        // Checks for updates by comparing the current version number with a version stored on the Timelapse website in an xml file.
        public static void GetAndParseVersion (Window win, bool showNoUpdatesMessage)
        {
            Version newVersion = null;  // if a newVersion variable, we will store the version info from xml file   
            string url = ""; // THE URL where the new version is located
            string changes = ""; // A list of changes held in the xml file
            XmlTextReader reader = null;
            try
            {
                // provide the XmlTextReader with the URL of   our xml document  

                reader = new XmlTextReader(Constants.LatestVersionAddress.AbsoluteUri);
                reader.MoveToContent(); // simply (and easily) skip the junk at the beginning  

                // internal - as the XmlTextReader moves only forward, we save current xml element name in elementName variable. 
                // When we parse a  text node, we refer to elementName to check what was the node name  
                string elementName = "";
                // we check if the xml starts with a proper "ourfancyapp" element node  
                if ((reader.NodeType == XmlNodeType.Element) && (reader.Name == Constants.ApplicationName))
                {
                    while (reader.Read())
                    {
                        // when we find an element node, we remember its name  
                        if (reader.NodeType == XmlNodeType.Element)
                            elementName = reader.Name;
                        else
                        {
                            // for text nodes...  
                            if ((reader.NodeType == XmlNodeType.Text) && (reader.HasValue))
                            {
                                // we check what the name of the node was  
                                switch (elementName)
                                {
                                    case "version":
                                        // we keep the version info in xxx.xxx.xxx.xxx format as the Version class does the  parsing for us  
                                        newVersion = new Version(reader.Value);
                                        break;
                                    case "url":
                                        url = reader.Value;
                                        break;
                                    case "changes":
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
                return;
            }
            finally
            {
                if (reader != null) reader.Close();
            }

             // get the running version  
             Version curVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

            // compare the versions  
             if (curVersion.CompareTo(newVersion) < 0)  
             {
                // ask the user if he would like to download the new version  
                DlgMessageBox dlgMB = new DlgMessageBox();
                dlgMB.MessageTitle = "A new version of Timelapse available.";
                dlgMB.MessageProblem = "You a running an old version of Timelapse: version " + curVersion.ToString();
                dlgMB.MessageReason = "A new version of Timelapse is available: version " + newVersion.ToString();
                dlgMB.MessageSolution = "Select 'Yes' to go to the Timelapse website and download it.";
                dlgMB.MessageResult = "The new version will contain these changes and more:" ;
                dlgMB.MessageResult += changes;
                dlgMB.MessageHint = "\u2022 We recommend downloading the latest version." + Environment.NewLine;
                dlgMB.MessageHint += "\u2022 To see all changes, go to http://saul.cpsc.ucalgary.ca/timelapse. Select 'Version history' from the side bar.";
                dlgMB.IconType = MessageBoxImage.Exclamation;
                dlgMB.ButtonType = MessageBoxButton.YesNo;
                bool? msg_result = dlgMB.ShowDialog();

                // Set the filter to show all images and a valid image
                if (msg_result == true)
                {  
                        // navigate the default web browser to our app homepage (the url comes from the xml content)  
                        System.Diagnostics.Process.Start(url);  
                }  
             }
             else if (showNoUpdatesMessage)
             {
                DlgMessageBox dlgMB = new DlgMessageBox();
                dlgMB.MessageTitle = "No updates to Timelapse are available.";
                dlgMB.MessageReason = "You a running the latest version of Timelapse, version: " + curVersion.ToString();
                dlgMB.IconType = MessageBoxImage.Information;
                dlgMB.ButtonType = MessageBoxButton.OK;
                bool? msg_result = dlgMB.ShowDialog();
             }
        }
    }
}
