using System;

namespace Carnassial.Util
{
    internal static class DateTimeOffsetExtensions
    {
        public static DateTimeOffset SetOffset(this DateTimeOffset dateTime, TimeSpan offset)
        {
            return new DateTimeOffset(dateTime.DateTime.AsUnspecifed(), offset);
        }
    }
}
