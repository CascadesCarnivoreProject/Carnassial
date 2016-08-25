using Microsoft.Win32;
using Timelapse.Util;

namespace Timelapse.Editor.Util
{
    internal class EditorUserRegistrySettings : UserRegistrySettings
    {
        public MostRecentlyUsedList<string> MostRecentTemplates { get; private set; }

        public EditorUserRegistrySettings()
            : this(Constants.Registry.RootKey)
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
                this.MostRecentTemplates = registryKey.ReadMostRecentlyUsedList(EditorConstant.Registry.EditorKey.MostRecentlyUsedTemplates);
            }
        }

        public void WriteToRegistry()
        {
            using (RegistryKey registryKey = this.OpenRegistryKey())
            {
                registryKey.Write(EditorConstant.Registry.EditorKey.MostRecentlyUsedTemplates, this.MostRecentTemplates);
            }
        }
    }
}
