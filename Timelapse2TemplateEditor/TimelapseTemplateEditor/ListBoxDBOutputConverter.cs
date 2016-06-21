using System;
using System.Globalization;
using System.Windows.Data;
using Timelapse.Util;

namespace Timelapse.Editor
{
    /// <summary>
    /// Converter for ComboBox. Turns the CSV string into an array
    /// Also adds the edit item element. 
    /// Done here, we don't have to ever check for it when performing delete/edit/add (surprises/scares me a bit)
    /// </summary>
    public class ListBoxDBOutputConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string newList = Utilities.ConvertLineBreaksToBars(value as string);
            return newList;
        }

        // This does nothing, but it has to be here.
        // Kinda surprised I don't need it for the binding... but I don't.
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
