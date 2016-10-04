namespace Carnassial.Database
{
    public enum FileSelection
    {
        // image selections also used as image qualities
        Ok,
        CorruptFile,
        Dark,
        FileNoLongerAvailable,

        // image selections only
        All,
        MarkedForDeletion,
        Custom
    }
}
