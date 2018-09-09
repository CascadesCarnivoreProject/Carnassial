using System;

namespace Carnassial.Util
{
    internal static class TimeSpanExtensions
    {
        public static TimeSpan Limit(this TimeSpan value, TimeSpan minimum, TimeSpan maximum)
        {
            if (value > maximum)
            {
                return maximum;
            }
            if (value < minimum)
            {
                value = minimum;
            }
            return value;
        }
    }
}
