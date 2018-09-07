using System;
using System.Diagnostics;

namespace Carnassial.Util
{
    internal static class DateTimeOffsetExtensions
    {
        public static DateTimeOffset SetOffset(this DateTimeOffset dateTime, TimeSpan offset)
        {
            Debug.Assert(dateTime.DateTime.Kind == DateTimeKind.Unspecified, "Expected unspecified date time.");
            return new DateTimeOffset(dateTime.DateTime, offset);
        }
    }
}
