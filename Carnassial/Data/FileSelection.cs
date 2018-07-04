using System;

namespace Carnassial.Data
{
    public enum FileSelection
    {
        // values are stored in database as integers
        All = 0,
        Color = 1,
        Corrupt = 6,
        Custom = 8,
        Dark = 3,
        Greyscale = 2,
        MarkedForDeletion = 7,
        NoLongerAvailable = 5,
        Video = 4,

        [Obsolete("Legacy value for backwards compatibility with Carnassial 2.2.0.2 and earlier.")]
        Ok = Color
    }
}
