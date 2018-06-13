using System;

namespace Carnassial.Data
{
    public enum FileSelection
    {
        All,
        Color,
        Corrupt,
        Custom,
        Dark,
        Greyscale,
        MarkedForDeletion,
        NoLongerAvailable,
        Video,

        [Obsolete("Legacy value for backwards compatibility with Carnassial 2.2.0.2 and earlier.")]
        Ok
    }
}
