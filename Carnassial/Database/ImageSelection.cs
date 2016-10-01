namespace Carnassial.Database
{
    public enum ImageSelection : int
    {
        Ok = 0,
        Dark = 1,
        FileNoLongerAvailable = 2,
        Corrupted = 3,
        All = 4,
        MarkedForDeletion = 5,
        Custom = 6
    }
}
