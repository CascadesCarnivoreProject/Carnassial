using Carnassial.Util;
using Microsoft.Win32;
using System;

namespace Carnassial.Editor.Util
{
    internal class EditorUserRegistrySettings : UserRegistrySettings
    {
        // same key as Carnassial uses; intentional as both Carnassial and template editor are released together
        public DateTime MostRecentCheckForUpdates { get; set; }

        public MostRecentlyUsedList<string> MostRecentTemplates { get; private set; }

        public EditorUserRegistrySettings()
            : this(Constant.Registry.RootKey)
        {
        }

        internal EditorUserRegistrySettings(string keyPath)
            : base(keyPath)
        {
            this.ReadFromRegistry();
        }

        public void ReadFromRegistry()
        {
            using (RegistryKey registryKey = this.OpenRegistryKey())
            {
                this.MostRecentCheckForUpdates = registryKey.ReadDateTime(Constant.Registry.CarnassialKey.MostRecentCheckForUpdates, DateTime.UtcNow);
                this.MostRecentTemplates = registryKey.ReadMostRecentlyUsedList(EditorConstant.Registry.EditorKey.MostRecentlyUsedTemplates);
            }
        }

        public void WriteToRegistry()
        {
            using (RegistryKey registryKey = this.OpenRegistryKey())
            {
                registryKey.Write(Constant.Registry.CarnassialKey.MostRecentCheckForUpdates, this.MostRecentCheckForUpdates);
                registryKey.Write(EditorConstant.Registry.EditorKey.MostRecentlyUsedTemplates, this.MostRecentTemplates);
            }
        }
    }
}
