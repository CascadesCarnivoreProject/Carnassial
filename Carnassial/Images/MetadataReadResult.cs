using System;

namespace Carnassial.Images
{
    [Flags]
    public enum MetadataReadResult
    {
        None = 0x0,
        Classification = 0x1,
        DateTime = 0x2,
        DateTimeInferredFromPrevious = 0x4,
        Failed = 0x8,
        Thumbnail = 0x10
    }
}
