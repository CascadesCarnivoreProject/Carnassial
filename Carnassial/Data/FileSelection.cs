namespace Carnassial.Data
{
    public enum FileSelection
    {
        // file selections also used as image qualities
        Ok,
        Corrupt,
        Dark,
        NoLongerAvailable,

        // file selections only
        All,
        MarkedForDeletion,
        Custom
    }
}
