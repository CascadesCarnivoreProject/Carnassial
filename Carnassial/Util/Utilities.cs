﻿using System;

namespace Carnassial.Util
{
    /// <summary>
    /// A variety of miscellaneous utility functions.
    /// </summary>
    public class Utilities
    {
        // Tuple.Create().GetHashCode() without the instantiation overhead, see Tuple.cs at https://github.com/dotnet/coreclr/
        public static int CombineHashCodes(object obj1, object obj2)
        {
            int hash = obj1.GetHashCode();
            return (hash << 5) + hash ^ obj2.GetHashCode();
        }

        public static int CombineHashCodes(params object[] objects)
        {
            int hash = objects[0].GetHashCode();
            for (int index = 1; index < objects.Length; ++index)
            {
                hash = (hash << 5) + hash ^ objects[index].GetHashCode();
            }
            return hash;
        }

        public static bool IsDigits(string value)
        {
            foreach (char character in value)
            {
                if (!Char.IsDigit(character))
                {
                    return false;
                }
            }
            return true;
        }

        public static unsafe bool TryParseFixedPointInvariant(string valueAsString, out float value)
        {
            if ((valueAsString == null) || (valueAsString.Length < 1))
            {
                value = float.NaN;
                return false;
            }

            fixed (char* characters = valueAsString)
            {
                bool isNegative = *characters == '-';
                int index = isNegative ? 1 : 0;
                int length = valueAsString.Length;
                int integerPart = 0;
                while (index < length)
                {
                    if (*(characters + index) == '.')
                    {
                        ++index;
                        break;
                    }
                    if ((*(characters + index) < '0') || (*(characters + index) > '9'))
                    {
                        value = float.MinValue;
                        return false;
                    }
                    integerPart = 10 * integerPart + *(characters + index) - '0';
                    ++index;
                }

                int decimalDivisor = 1;
                int decimalPart = 0;
                while (index < length)
                {
                    if ((*(characters + index) < '0') || (*(characters + index) > '9'))
                    {
                        value = float.MaxValue;
                        return false;
                    }
                    decimalDivisor *= 10;
                    decimalPart = 10 * decimalPart + *(characters + index) - '0';
                    ++index;
                }

                value = (float)(integerPart * (isNegative ? -1 : 1)) + (float)decimalPart / (float)decimalDivisor;
                return true;
            }
        }
    }
}
