namespace Carnassial.Database
{
    public enum ImageSelection
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
