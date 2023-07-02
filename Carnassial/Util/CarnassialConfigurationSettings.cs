using System;
using System.Globalization;

namespace Carnassial.Util
{
    public static class CarnassialConfigurationSettings
    {
        public static Uri GetDevTeamEmailLink()
        {
            UriBuilder mailto = new(Uri.UriSchemeMailto + ":" + CarnassialSettings.Default.DevTeamEmail)
            {
                Query = String.Format(CultureInfo.InvariantCulture, "subject={0} {1}: feedback", Constant.ApplicationName, typeof(CarnassialConfigurationSettings).Assembly.GetName().Version)
            };
            return mailto.Uri;
        }

        public static Uri GetLatestReleaseApiAddress()
        {
            return CarnassialConfigurationSettings.GetUriSetting(Constant.GitHub.BaseAddress, "releases.atom");
        }

        public static Uri GetIssuesBrowserAddress()
        {
            return CarnassialConfigurationSettings.GetUriSetting(Constant.GitHub.BaseAddress, "issues");
        }

        public static Uri GetReleasesBrowserAddress()
        {
            return CarnassialConfigurationSettings.GetUriSetting(Constant.GitHub.BaseAddress, "releases");
        }

        public static Uri GetTutorialBrowserAddress()
        {
            return CarnassialConfigurationSettings.GetUriSetting(Constant.GitHub.BaseAddress, "wiki", "Tutorial");
        }

        private static Uri GetUriSetting(Uri baseAddress, params string[] additionalPath)
        {
            UriBuilder uriBuilder = new(baseAddress);
            uriBuilder.Path += CarnassialSettings.Default.GithubOrganizationAndRepo;
            foreach (string token in additionalPath)
            {
                uriBuilder.Path += "/" + token;
            }
            return uriBuilder.Uri;
        }
    }
}
