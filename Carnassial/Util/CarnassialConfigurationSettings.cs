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
            return CarnassialConfigurationSettings.GetUriSetting(Constant.ApplicationSettings.ReleasesAddress);
        }

        private static Uri GetUriSetting(string key)
        {
            string value = ConfigurationManager.AppSettings[key];
            if (String.IsNullOrEmpty(value) == false)
            {
                return new Uri(value);
            }
            return null;
        }
    }
}
