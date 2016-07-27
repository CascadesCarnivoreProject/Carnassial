using Timelapse;
using Timelapse.Util;

namespace Timelapse.Editor.Util
{
    internal class EditorRegistryUserSettings : RegistryUserSettings
    {
        public EditorRegistryUserSettings()
            : this(Constants.Registry.RootKey)
        {
        }

        internal EditorRegistryUserSettings(string keyPath)
            : base(keyPath)
        {
        }

        // Functions used to save and retrive most recent templates to and from the Registry
        public MostRecentlyUsedList<string> ReadMostRecentTemplates()
        {
            return this.ReadMostRecentlyUsedListFromRegistry(EditorConstant.Registry.EditorKey.MostRecentlyUsedTemplates);
        }

        public void WriteMostRecentTemplates(MostRecentlyUsedList<string> paths)
        {
            this.WriteToRegistry(EditorConstant.Registry.EditorKey.MostRecentlyUsedTemplates, paths);
        }
    }
}
