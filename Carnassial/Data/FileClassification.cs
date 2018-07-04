namespace Carnassial.Data
{
    public enum FileClassification
    {
        // values are stored in file database as integers
        Color = 0,
        Corrupt = 5,
        Dark = 2,
        Greyscale = 1,
        NoLongerAvailable = 4,
        Video = 3
    }
}
