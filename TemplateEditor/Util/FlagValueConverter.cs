using Carnassial.Util;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;

namespace Carnassial.Editor.Util
{
    internal class FlagValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return null;
            }

            List<string> values = WellKnownValueConverter.Convert((string)value);
            if (values.Count == 1)
            {
                // default value case
                return this.Convert((string)value);
            }

            // well known values
            List<string> displayWellKnownValues = new List<string>(values.Count);
            foreach (string wellKnownValue in values)
            {
                displayWellKnownValues.Add(this.Convert(wellKnownValue));
            }
            return displayWellKnownValues;
        }

        private string Convert(string value)
        {
            switch (value)
            {
                case Constant.Sql.FalseString:
                    return EditorConstant.Resources.DisplayFalseString;
                case Constant.Sql.TrueString:
                    return EditorConstant.Resources.DisplayTrueString;
                default:
                    throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled value '{0}'.", value));
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return null;
            }
            if (value is string valueAsString)
            {
                return this.ConvertBack(valueAsString);
            }

            List<string> displayWellKnownValues = (List<string>)value;
            List<string> wellKnownValues = new List<string>(displayWellKnownValues.Count);
            foreach (string displayWellKnownValue in displayWellKnownValues)
            {
                wellKnownValues.Add(this.ConvertBack(displayWellKnownValue));
            }
            return WellKnownValueConverter.ConvertBack(wellKnownValues);
        }

        private string ConvertBack(string value)
        {
            switch (value)
            {
                case EditorConstant.Resources.DisplayFalseString:
                    return Constant.Sql.FalseString;
                case EditorConstant.Resources.DisplayTrueString:
                    return Constant.Sql.TrueString;
                default:
                    throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled value '{0}'.", value));
            }
        }
    }
}
