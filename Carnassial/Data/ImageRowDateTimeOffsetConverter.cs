using Carnassial.Util;
using System;
using System.Globalization;
using System.Windows.Data;

namespace Carnassial.Data
{
    internal class DateTimeOffsetToOffsetConverter : IValueConverter
    {
        private DateTimeOffset mostRecentValue;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            this.mostRecentValue = (DateTimeOffset)value;
            return this.mostRecentValue.Offset;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return new DateTimeOffset(this.mostRecentValue.UtcDateTime, (TimeSpan)value);
        }
    }
}
