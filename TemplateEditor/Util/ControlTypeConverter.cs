using Carnassial.Data;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Data;

namespace Carnassial.Editor.Util
{
    /// <summary>
    /// Display string converter for ControlTypes.  Uses preferred enum names rather than legacy values.
    /// </summary>
    public class ControlTypeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Debug.Assert(targetType == typeof(string), "Unhandled target type.");

            switch ((ControlType)value)
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
                    throw new NotSupportedException(String.Format("Unhandled control type {0}.", parameter));
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Debug.Assert(targetType == typeof(ControlType), "Unhandled target type.");

            string valueAsString = (string)value;
            if (String.Equals(valueAsString, "Counter", StringComparison.Ordinal))
            {
                return ControlType.Counter;
            }
            if (String.Equals(valueAsString, "DateTime", StringComparison.Ordinal))
            {
                return ControlType.DateTime;
            }
            if (String.Equals(valueAsString, "FixedChoice", StringComparison.Ordinal))
            {
                return ControlType.FixedChoice;
            }
            if (String.Equals(valueAsString, "Flag", StringComparison.Ordinal))
            {
                return ControlType.Flag;
            }
            if (String.Equals(valueAsString, "Note", StringComparison.Ordinal))
            {
                return ControlType.Note;
            }
            if (String.Equals(valueAsString, "UtcOffset", StringComparison.Ordinal))
            {
                return ControlType.UtcOffset;
            }

            throw new NotSupportedException(String.Format("Unhandled control type {0}.", parameter));
        }
    }
}
