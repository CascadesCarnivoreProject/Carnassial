using System;
using System.Configuration;

namespace Carnassial.Util
{
    public class CarnassialConfigurationSettings
    {
        public static Uri GetLatestReleaseApiAddress()
        {
            return CarnassialConfigurationSettings.GetUriSetting(Constant.GitHub.ApiBaseAddress, Constant.ApplicationSettings.GithubOrganizationAndRepo, "releases", "latest");
        }

        public static Uri GetReleasesBrowserAddress()
        {
            return CarnassialConfigurationSettings.GetUriSetting(Constant.GitHub.BaseAddress, Constant.ApplicationSettings.GithubOrganizationAndRepo, "releases");
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
