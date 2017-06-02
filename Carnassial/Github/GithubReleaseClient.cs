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
                throw new ArgumentOutOfRangeException(nameof(applicationName));
            }
            if (latestRelease == null)
            {
                throw new ArgumentNullException(nameof(latestRelease));
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
                            if (latestRelease == null || String.IsNullOrWhiteSpace(latestRelease.TagName))
                            {
                                return false;
                            }
                            // Version's parse implementation requires the leading v of Github convention be removed
                            if (Version.TryParse(latestRelease.TagName.Substring(1), out publicallyAvailableVersion) == false)
                            {
                                return false;
                            }
                            description = latestRelease.Name + Environment.NewLine + latestRelease.Body;
                        }
                    }
                }
                catch (WebException exception)
                {
                    // 404 if no production releases (Github's latest endpoint doesn't return releases with prerelease = true)
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
                // ask the user if they would like to download the new version  
                string title = String.Format("Get the new version of {0}?", this.applicationName);
                MessageBox messageBox = new MessageBox(title, Application.Current.MainWindow, MessageBoxButton.YesNo);
                messageBox.Message.StatusImage = MessageBoxImage.Question;
                messageBox.Message.What = String.Format("You're running an old release, {0} {1}.", this.applicationName, currentVersion);
                messageBox.Message.Reason = String.Format("A new version is available, {0} {1}", this.applicationName, publicallyAvailableVersion);
                messageBox.Message.Solution = "Select 'Yes' to go to the website and download it.";
                messageBox.Message.Result = description;
                messageBox.Message.Hint = "\u2022 We recommend downloading the latest release." + Environment.NewLine;
                messageBox.Message.Hint += String.Format(@"\u2022 To see all releases, go to {0}.", CarnassialConfigurationSettings.GetReleasesBrowserAddress());
                if (messageBox.ShowDialog() == true)
                {
                    Uri releasesAddress = CarnassialConfigurationSettings.GetReleasesBrowserAddress();
                    Process.Start(releasesAddress.AbsoluteUri);
                }
            }
            else if (showNoUpdatesMessage)
            {
                MessageBox messageBox = new MessageBox(String.Format("No updates to {0} are available.", this.applicationName), Application.Current.MainWindow);
                messageBox.Message.Reason = String.Format("You're running the latest release, {0} {1}.", this.applicationName, currentVersion);
                messageBox.Message.StatusImage = MessageBoxImage.Information;
                messageBox.ShowDialog();
            }

            return true;
        }
    }
}
