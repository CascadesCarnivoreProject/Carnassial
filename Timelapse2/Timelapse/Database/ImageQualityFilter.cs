namespace Timelapse.Database
{
    public enum ImageQualityFilter : int
    {
        Ok = 0,
        Dark = 1,
        Missing = 2,
        Corrupted = 3,
        All = 4,
        MarkedForDeletion = 5,
        Custom = 6
    }
}
