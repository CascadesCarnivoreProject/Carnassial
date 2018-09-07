using System;

namespace Carnassial.UnitTests
{
    internal static class DateTimeOffsetExtensions
    {
        public static DateTimeOffset ToNewOffset(this DateTimeOffset dateTime, TimeSpan changeInOffset)
        {
            DateTime asUnspecified = new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, dateTime.Minute, dateTime.Second, dateTime.Millisecond, DateTimeKind.Unspecified);
            return new DateTimeOffset(dateTime.DateTime.AsUnspecifed(), dateTime.Offset + changeInOffset);
        }
    }
}
