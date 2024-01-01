using Carnassial.Util;

namespace Carnassial.Editor.Util
{
    internal class EditorUserSettings
    {
        public MostRecentlyUsedList<string> MostRecentTemplates { get; private init; }

        public EditorUserSettings()
        {
            this.MostRecentTemplates = new(EditorSettings.Default.MostRecentlyUsedTemplates, Constant.NumberOfMostRecentDatabasesToTrack);
        }

        public void SerializeToSettings()
        {
            EditorSettings.Default.MostRecentlyUsedTemplates = this.MostRecentTemplates.ToStringCollection();
        }
    }
}