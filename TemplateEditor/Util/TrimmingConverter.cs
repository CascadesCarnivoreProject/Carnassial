using System;
using System.Globalization;
using System.Windows.Data;

namespace Carnassial.Editor.Util
{
    /// <summary>
    /// Converter for control grid text. Removes whitespace from beginning and end of string.
    /// </summary>
    public class TrimmingConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string valueAsString = value as string;
            if (String.IsNullOrEmpty(valueAsString))
            {
                return value;
            }
            return valueAsString.Trim();
        }
    }
}
