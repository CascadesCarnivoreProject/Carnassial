using Microsoft.Win32;
using System;

namespace Timelapse.Util
{
    /// <summary>
    /// Base class for manipulating application's user preferences and related information in the registry.
    /// </summary>
    public class UserRegistrySettings
    {
        private string keyPath;

        public UserRegistrySettings(string keyPath)
        {
            this.keyPath = keyPath;
        }

        protected RegistryKey OpenRegistryKey()
        {
            RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(this.keyPath, true);
            if (registryKey == null)
            {
                registryKey = Registry.CurrentUser.CreateSubKey(this.keyPath);
            }
            return registryKey;
        }
    }
}
