using System;

namespace Carnassial.Data
{
    public enum ControlType
    {
        // types are stored as integers in the database
        Counter = 1,
        DateTime = 4,
        Flag = 3,
        FixedChoice = 2,
        Note = 0,
        UtcOffset = 5,

        // values for backward compatibility with pre-2.2.0.2 .tdb and .ddb files where type and data label were used interchangeably for standard controls
        [Obsolete("Legacy value for backwards compatibility with Carnassial 2.2.0.1 and earlier.")]
        ImageQuality = FixedChoice,
        [Obsolete("Legacy value for backwards compatibility with Carnassial 2.2.0.1 and earlier.")]
        DeleteFlag = Flag,
        [Obsolete("Legacy value for backwards compatibility with Carnassial 2.2.0.1 and earlier.")]
        File = Note,
        [Obsolete("Legacy value for backwards compatibility with Carnassial 2.2.0.1 and earlier.")]
        RelativePath = Note
    }
}
