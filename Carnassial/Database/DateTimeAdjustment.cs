using System;

namespace Carnassial.Database
{
    [Flags]
    public enum DateTimeAdjustment
    {
        None = 0x0,
        NoChange = 0x1,
        MetadataDate = 0x2,
        MetadataTime = 0x4,
        ImageSetOffset = 0x8,
        PreviousMetadata = 0x10
    }
}
