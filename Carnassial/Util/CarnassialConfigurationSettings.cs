using System;
using System.Configuration;

namespace Carnassial.Util
{
    public class CarnassialConfigurationSettings
    {
        public static Uri GetLatestVersionAddress()
        {
            return CarnassialConfigurationSettings.GetUriSetting(Constants.ApplicationSettings.LatestVersionAddress);
        }

        public static Uri GetVersionChangesAddress()
        {
            return CarnassialConfigurationSettings.GetUriSetting(Constants.ApplicationSettings.VersionChangesAddress);
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
