namespace Carnassial.Data
{
    public enum ControlType
    {
        Counter,
        DateTime,
        Flag,
        FixedChoice,
        Note,
        UtcOffset,

        // values for backward compatibility with pre-2.2.0.2 .tdb and .ddb files where type and data label were used interchangeably for standard controls
        ImageQuality = FixedChoice,
        DeleteFlag = Flag,
        File = Note,
        RelativePath = Note
    }
}
