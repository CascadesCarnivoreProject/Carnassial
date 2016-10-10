using System;
using System.Configuration;

namespace Carnassial.Util
{
    public class CarnassialConfigurationSettings
    {
        public static Uri GetLatestReleaseAddress()
        {
            Uri releases = CarnassialConfigurationSettings.GetReleasesAddress();
            if (releases == null)
            {
                return null;
            }

            UriBuilder latestRelease = new UriBuilder(releases);
            latestRelease.Path += "/latest";
            return latestRelease.Uri;
        }

        public static Uri GetReleasesAddress()
        {
            return CarnassialConfigurationSettings.GetUriSetting(Constant.GitHub.ApiBaseAddress, Constant.ApplicationSettings.GithubOrganizationAndRepo, "releases");
        }

        private static Uri GetUriSetting(Uri baseAddress, string key, params string[] additionalPath)
        {
            UriBuilder uriBuilder = new UriBuilder(baseAddress);
            uriBuilder.Path += "/" + ConfigurationManager.AppSettings[key];
            foreach (string token in additionalPath)
            {
                uriBuilder.Path += "/" + token;
            }
            return uriBuilder.Uri;
        }
    }
}
