using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace Carnassial.Util
{
    public class WellKnownValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return WellKnownValueConverter.Convert((string)value);
        }

        public static List<string> Convert(string value)
        {
            return value.Split(Constant.Control.WellKnownValuesDelimiter).ToList();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return WellKnownValueConverter.ConvertBack((IEnumerable<string>)value);
        }

        public static string ConvertBack(IEnumerable<string> values)
        {
            return String.Join(Constant.Control.WellKnownValuesDelimiter.ToString(), values);
        }
    }
}
