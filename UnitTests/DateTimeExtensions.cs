﻿using System;

namespace Carnassial.UnitTests
{
    internal static class DateTimeExtensions
    {
        public static DateTime AsUnspecifed(this DateTime dateTime)
        {
            return new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, dateTime.Minute, dateTime.Second, dateTime.Millisecond, DateTimeKind.Unspecified);
        }
    }
}
