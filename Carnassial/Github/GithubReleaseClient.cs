using Carnassial.Util;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Windows;
using MessageBox = Carnassial.Dialog.MessageBox;

namespace Carnassial.Github
{
    public class GithubReleaseClient
    {
        private string applicationName;
        private Uri latestReleaseAddress;

        public GithubReleaseClient(string applicationName, Uri latestRelease)
        {
            if (String.IsNullOrWhiteSpace(applicationName))
            {
                throw new ArgumentOutOfRangeException("applicationName");
            }
            if (latestRelease == null)
            {
                throw new ArgumentNullException("latestRelease");
            }

            this.applicationName = applicationName;
            this.latestReleaseAddress = latestRelease;
        }

        // Checks for updates by comparing the current version number with a version stored on the Carnassial website in an xml file.
        public bool TryGetAndParseRelease(bool showNoUpdatesMessage)
        {
            Version publicallyAvailableVersion = null;
            string description = null;

            using (WebClient webClient = new WebClient())
            {
                webClient.Headers.Add(HttpRequestHeader.UserAgent, "Carnassial-GithubReleaseClient");
                try
                {
                    string releaseJson = webClient.DownloadString(this.latestReleaseAddress);
                    if (String.IsNullOrWhiteSpace(releaseJson) || releaseJson.Contains("\"message\": \"Not Found\""))
                    {
                        // no releases, so nothing to do
                        return false;
                    }
                    JsonSerializer serializer = JsonSerializer.CreateDefault();
                    using (StringReader reader = new StringReader(releaseJson))
                    {
                        using (JsonReader jsonReader = new JsonTextReader(reader))
                        {
                            GithubRelease latestRelease = serializer.Deserialize<GithubRelease>(jsonReader);
                            description = latestRelease.Body;
                            publicallyAvailableVersion = Version.Parse(latestRelease.Name);
                        }
                    }
                }
                catch (WebException exception)
                {
                    if ((exception.Response is HttpWebResponse == false) || (((HttpWebResponse)exception.Response).StatusCode != HttpStatusCode.NotFound))
                    {
                        Debug.Fail(exception.ToString());
                    }
                    return false;
                }
                catch (Exception exception)
                {
                    Debug.Fail(exception.ToString());
                    return false;
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
                messageBox.Message.What = String.Format("You a running an old release of {0}: release {1}", this.applicationName, currentVersion);
                messageBox.Message.Reason = String.Format("A new version of {0} is available: version {1}", this.applicationName, publicallyAvailableVersion);
                messageBox.Message.Solution = "Select 'Yes' to go to the website and download it.";
                messageBox.Message.Result = "The new release contain these changes:";
                messageBox.Message.Result += description;
                messageBox.Message.Hint = "\u2022 We recommend downloading the latest release." + Environment.NewLine;
                messageBox.Message.Hint += String.Format(@"\u2022 To see all changes, go to {0}.", CarnassialConfigurationSettings.GetReleasesAddress());
                messageBox.Message.Icon = MessageBoxImage.Exclamation;
                messageBox.ShowDialog();
            }
            else if (showNoUpdatesMessage)
            {
                MessageBox messageBox = new MessageBox(String.Format("No updates to {0} are available.", this.applicationName), Application.Current.MainWindow);
                messageBox.Message.Reason = String.Format("You a running the latest release of {0}, release: {1}", this.applicationName, currentVersion);
                messageBox.Message.Icon = MessageBoxImage.Information;
                bool? messageBoxResult = messageBox.ShowDialog();
            }

            return true;
        }
    }
}
