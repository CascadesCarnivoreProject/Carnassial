using Carnassial.Util;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Reflection;
using MessageBox = Carnassial.Dialog.MessageBox;

namespace Carnassial.Github
{
    public class GithubReleaseClient
    {
        private readonly string applicationName;
        private readonly Uri latestReleaseAddress;

        public GithubReleaseClient(string applicationName, Uri? latestRelease)
        {
            if (String.IsNullOrWhiteSpace(applicationName))
            {
                throw new ArgumentOutOfRangeException(nameof(applicationName));
            }

            this.applicationName = applicationName;
            this.latestReleaseAddress = latestRelease ?? throw new ArgumentNullException(nameof(latestRelease));
        }

        // Checks for updates by comparing the current version number with a version stored on the Carnassial website in an xml file.
        public bool TryGetAndParseRelease(bool showNoUpdatesMessage, out Version? publiclyAvailableVersion)
        {
            publiclyAvailableVersion = null;

            using HttpClient httpClient = new();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Carnassial-GithubReleaseClient");
            try
            {
                string releaseXml = httpClient.GetStringAsync(this.latestReleaseAddress).GetAwaiter().GetResult();
                if (String.IsNullOrWhiteSpace(releaseXml))
                {
                    // no releases, so nothing to do
                    return false;
                }

                Feed feed = new(releaseXml, 1); // for now, assume most recent entry is most recent release
                if (feed.Entries.Count < 1)
                {
                    // also no releases
                    return false;
                }

                Entry latestRelease = feed.Entries[0];
                publiclyAvailableVersion = latestRelease.GetVersion();
            }
            catch (WebException exception)
            {
                // 404 if no production releases (Github's latest endpoint doesn't return releases with prerelease = true)
                if ((exception.Response is HttpWebResponse response == false) || (response.StatusCode != HttpStatusCode.NotFound))
                {
                    Debug.Fail(exception.ToString());
                }
                return false;
            }
            catch (Exception exception)
            {
                Debug.Fail(exception.ToString());
                throw;
            }

            // get the running version  
            Version? currentVersion = Assembly.GetExecutingAssembly().GetName().Version;

            // compare the versions  
            if (currentVersion < publiclyAvailableVersion)
            {
                // ask the user if they would like to download the new version  
                MessageBox messageBox = MessageBox.FromResource(Constant.ResourceKey.GithubReleaseClientGetNewVersion, App.Current.MainWindow,
                                                                publiclyAvailableVersion,
                                                                this.applicationName,
                                                                currentVersion,
                                                                CarnassialConfigurationSettings.GetReleasesBrowserAddress());
                if (messageBox.ShowDialog() == true)
                {
                    Uri releasesAddress = CarnassialConfigurationSettings.GetReleasesBrowserAddress();
                    Process.Start(releasesAddress.AbsoluteUri);
                }
            }
            else if (showNoUpdatesMessage)
            {
                MessageBox messageBox = MessageBox.FromResource(Constant.ResourceKey.GithubReleaseClientNoUpdates, App.Current.MainWindow, this.applicationName, currentVersion);
                messageBox.ShowDialog();
            }

            return true;
        }
    }
}
