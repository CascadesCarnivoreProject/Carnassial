using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Xml;
namespace Timelapse {
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

                reader = new XmlTextReader(Constants.URL_CONTAINING_LATEST_VERSION_INFO);
                reader.MoveToContent(); // simply (and easily) skip the junk at the beginning  

                // internal - as the XmlTextReader moves only forward, we save current xml element name in elementName variable. 
                // When we parse a  text node, we refer to elementName to check what was the node name  
                string elementName = "";
                // we check if the xml starts with a proper "ourfancyapp" element node  
                if ((reader.NodeType == XmlNodeType.Element) && (reader.Name == Constants.APPLICATION_NAME))
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
                string title =    "A new version of Timelapse available.";
                string question = "A new version of Timelapse is available: version: " + newVersion.ToString()   + Environment.NewLine ;
                question +=       "You a running an old version of Timelapse: version: " + curVersion.ToString() + Environment.NewLine;
                if (!changes.Equals("")) question += "Changes include: " + changes + Environment.NewLine;
                question += Environment.NewLine;
                question += "Select 'Yes' to go to the website and download it." + Environment.NewLine;

                MessageBoxResult result = MessageBox.Show(win, question, title, MessageBoxButton.YesNo, MessageBoxImage.Question); 
                if (result == MessageBoxResult.Yes)  
                {  
                        // navigate the default web browser to our app homepage (the url comes from the xml content)  
                        System.Diagnostics.Process.Start(url);  
                }  
            }
             else if (showNoUpdatesMessage)
             {
                 // tell the user that there a no updates  
                 string title = "No updates to Timelapse are available.";
                 string question = "You a running the latest version of Timelapse: version: " + curVersion.ToString();

                 MessageBox.Show(win, question, title, MessageBoxButton.OK, MessageBoxImage.Question);
             }
        }
    }
}
