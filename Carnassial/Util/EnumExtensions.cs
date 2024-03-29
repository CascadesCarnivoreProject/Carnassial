﻿using System;
using System.Diagnostics;
using System.Globalization;

namespace Carnassial.Util
{
    internal static class EnumExtensions
    {
        public static TEnum SetFlag<TEnum>(this TEnum enumeration, TEnum flag, bool value) where TEnum : struct, IComparable, IFormattable, IConvertible
        {
            Debug.Assert(Enum.GetUnderlyingType(typeof(TEnum)) == typeof(int), "Only integer backed enums are currently supported.");
            int enumAsInt = Convert.ToInt32(enumeration, CultureInfo.InvariantCulture);
            int flagAsInt = Convert.ToInt32(flag, CultureInfo.InvariantCulture);
            if (value)
            {
                // set flag
                return (TEnum)Enum.ToObject(typeof(TEnum), enumAsInt |= flagAsInt);
            }
            else
            {
                // clear flag
                return (TEnum)Enum.ToObject(typeof(TEnum), enumAsInt &= ~flagAsInt);
            }
        }
    }
}
