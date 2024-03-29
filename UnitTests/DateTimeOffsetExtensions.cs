﻿using System;

namespace Carnassial.UnitTests
{
    internal static class DateTimeOffsetExtensions
    {
        public static DateTimeOffset ToNewOffset(this DateTimeOffset dateTime, TimeSpan changeInOffset)
        {
            return new DateTimeOffset(dateTime.DateTime.AsUnspecifed(), dateTime.Offset + changeInOffset);
        }
    }
}
