using Carnassial.Data;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Data;

namespace Carnassial.Util
{
    /// <summary>
    /// Display string converter for ControlTypes.  Uses preferred enum names rather than legacy values.
    /// </summary>
    public class ControlTypeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Debug.Assert(targetType == typeof(string), "Unhandled target type.");
            return ControlTypeConverter.Convert((ControlType)value);
        }

        public static string Convert(ControlType type)
        {
            switch (type)
            {
                case ControlType.Counter:
                    return "Counter";
                case ControlType.DateTime:
                    return "DateTime";
                case ControlType.FixedChoice:
                    return "FixedChoice";
                case ControlType.Flag:
                    return "Flag";
                case ControlType.Note:
                    return "Note";
                case ControlType.UtcOffset:
                    return "UtcOffset";
                default:
                    throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled control type {0}.", type));
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Debug.Assert(targetType == typeof(ControlType), "Unhandled target type.");
            return ControlTypeConverter.ConvertBack((string)value);
        }

        public static ControlType ConvertBack(string type)
        {
            if (String.Equals(type, "Counter", StringComparison.Ordinal))
            {
                return ControlType.Counter;
            }
            if (String.Equals(type, "DateTime", StringComparison.Ordinal))
            {
                return ControlType.DateTime;
            }
            if (String.Equals(type, "FixedChoice", StringComparison.Ordinal))
            {
                return ControlType.FixedChoice;
            }
            if (String.Equals(type, "Flag", StringComparison.Ordinal))
            {
                return ControlType.Flag;
            }
            if (String.Equals(type, "Note", StringComparison.Ordinal))
            {
                return ControlType.Note;
            }
            if (String.Equals(type, "UtcOffset", StringComparison.Ordinal))
            {
                return ControlType.UtcOffset;
            }

            throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled control type '{0}'.", type));
        }
    }
}
