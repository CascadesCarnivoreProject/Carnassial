using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;

namespace Carnassial
{
    public class LocalizedApplication : Application
    {
        private static readonly Dictionary<string, ResourceDictionary> ResourcesByCultureName;

        static LocalizedApplication()
        {
            LocalizedApplication.ResourcesByCultureName = new Dictionary<string, ResourceDictionary>();
        }

        public static TResource FindResource<TResource>(string key)
        {
            return (TResource)App.Current.FindResource(key);
        }

        public static TResource FindResource<TResource>(string key, CultureInfo culture)
        {
            if (LocalizedApplication.ResourcesByCultureName.TryGetValue(culture.Name, out ResourceDictionary dictionary) == false)
            {
                LocalizedApplication.ResourcesByCultureName.TryGetValue(culture.TwoLetterISOLanguageName, out dictionary);
            }
            if (dictionary != null)
            {
                object resource = dictionary[key];
                if (resource != null)
                {
                    return (TResource)resource;
                }
            }

            return LocalizedApplication.FindResource<TResource>(key);
        }

        public static string FormatResource(string key, params object[] args)
        {
            string format = LocalizedApplication.FindResource<string>(key);
            return String.Format(format, args);
        }
    }
}
