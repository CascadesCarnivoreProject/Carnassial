using Carnassial.Control;
using Carnassial.Data;
using Carnassial.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using WpfControl = System.Windows.Controls.Control;

namespace Carnassial.Dialog
{
    public class FindDialog : WindowWithSystemMenu
    {
        // accept only numbers in counter textboxes
        private void Counter_Paste(object sender, DataObjectPastingEventArgs args)
        {
            bool isText = args.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, true);
            if (!isText)
            {
                args.CancelCommand();
            }

            string text = args.SourceDataObject.GetData(DataFormats.UnicodeText) as string;
            if (Utilities.IsDigits(text) == false)
            {
                args.CancelCommand();
            }
        }

        private void Counter_PreviewTextInput(object sender, TextCompositionEventArgs args)
        {
            // counters accept only numbers
            args.Handled = !Utilities.IsDigits(args.Text);
        }

        protected WpfControl CreateValueControl(SearchTerm searchTerm, FileDatabase fileDatabase)
        {
            ControlType controlType = searchTerm.ControlType;
            switch (controlType)
            {
                case ControlType.Counter:
                case ControlType.Note:
                    AutocompleteTextBox textBox = new AutocompleteTextBox()
                    {
                        AllowLeadingWhitespace = true,
                        Autocompletions = fileDatabase.GetDistinctValuesInFileDataColumn(searchTerm.DataLabel),
                        DataContext = searchTerm,
                        Height = Constant.UserInterface.FindTextBoxHeight,
                        IsEnabled = searchTerm.UseForSearching,
                        Margin = Constant.UserInterface.FindCellMargin,
                        TextWrapping = TextWrapping.NoWrap,
                        VerticalAlignment = VerticalAlignment.Center,
                        VerticalContentAlignment = VerticalAlignment.Center,
                        Width = Constant.UserInterface.FindValueWidth
                    };
                    Binding binding = new Binding(nameof(searchTerm.DatabaseValue))
                    {
                        UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                    };
                    if (searchTerm.ControlType == ControlType.Counter)
                    {
                        binding.Converter = new IntegerConverter();
                    }
                    BindingOperations.SetBinding(textBox, AutocompleteTextBox.TextProperty, binding);

                    if (controlType == ControlType.Counter)
                    {
                        // accept only numbers in counter text boxes
                        textBox.PreviewTextInput += this.Counter_PreviewTextInput;
                        DataObject.AddPastingHandler(textBox, this.Counter_Paste);
                    }
                    return textBox;
                case ControlType.DateTime:
                    DateTimeOffsetPicker dateTimeOffset = new DateTimeOffsetPicker()
                    {
                        DataContext = searchTerm,
                        IsEnabled = searchTerm.UseForSearching,
                        Value = (DateTime)searchTerm.DatabaseValue,
                        Width = Constant.UserInterface.FindValueWidth
                    };
                    BindingOperations.SetBinding(dateTimeOffset, DateTimeOffsetPicker.ValueProperty, new Binding(nameof(searchTerm.DatabaseValue))
                    {
                        Converter = new DateTimeOffsetConverter(),
                        UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                    });
                    return dateTimeOffset;
                case ControlType.FixedChoice:
                    ComboBox comboBox = new ComboBox()
                    {
                        DataContext = searchTerm,
                        IsEnabled = searchTerm.UseForSearching,
                        ItemsSource = searchTerm.WellKnownValues,
                        Margin = Constant.UserInterface.FindCellMargin,
                        SelectedItem = searchTerm.DatabaseValue,
                        Width = Constant.UserInterface.FindValueWidth
                    };
                    binding = new Binding(nameof(searchTerm.DatabaseValue));
                    BindingOperations.SetBinding(comboBox, ComboBox.SelectedItemProperty, binding);
                    return comboBox;
                case ControlType.Flag:
                    CheckBox checkBox = new CheckBox()
                    {
                        DataContext = searchTerm,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        IsChecked = (bool)searchTerm.DatabaseValue,
                        IsEnabled = searchTerm.UseForSearching,
                        Margin = Constant.UserInterface.FindCellMargin,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    BindingOperations.SetBinding(checkBox, CheckBox.IsCheckedProperty, new Binding(nameof(searchTerm.DatabaseValue))
                    {
                        Converter = new BooleanConverter()
                    });
                    return checkBox;
                case ControlType.UtcOffset:
                    UtcOffsetPicker utcOffset = new UtcOffsetPicker()
                    {
                        DataContext = searchTerm,
                        IsEnabled = searchTerm.UseForSearching,
                        IsTabStop = true,
                        Value = (TimeSpan)searchTerm.DatabaseValue,
                        Width = Constant.UserInterface.FindValueWidth
                    };
                    BindingOperations.SetBinding(utcOffset, UtcOffsetPicker.ValueProperty, new Binding(nameof(searchTerm.DatabaseValue))
                    {
                        Converter = new UtcOffsetConverter()
                    });
                    return utcOffset;
                default:
                    throw new NotSupportedException(String.Format("Unhandled control type '{0}'.", controlType));
            }
        }

        protected List<string> GetOperators(SearchTerm searchTerm)
        {
            // keep in sync with FileFindReplace.Match*()
            switch (searchTerm.ControlType)
            {
                case ControlType.Counter:
                case ControlType.DateTime:
                case ControlType.FixedChoice:
                    // no globs in counters they use only numbers
                    // no globs in date times the date entries are constrained by the date picker
                    // no globs in fixed choices as choice entries are constrained by menu selection
                    return new List<string>()
                    {
                        Constant.SearchTermOperator.Equal,
                        Constant.SearchTermOperator.NotEqual,
                        Constant.SearchTermOperator.LessThan,
                        Constant.SearchTermOperator.GreaterThan,
                        Constant.SearchTermOperator.LessThanOrEqual,
                        Constant.SearchTermOperator.GreaterThanOrEqual
                    };
                case ControlType.Flag:
                    return new List<string>()
                    {
                        Constant.SearchTermOperator.Equal,
                        Constant.SearchTermOperator.NotEqual
                    };
                default:
                    // notes
                    return new List<string>()
                    {
                        Constant.SearchTermOperator.Equal,
                        Constant.SearchTermOperator.NotEqual,
                        Constant.SearchTermOperator.LessThan,
                        Constant.SearchTermOperator.GreaterThan,
                        Constant.SearchTermOperator.LessThanOrEqual,
                        Constant.SearchTermOperator.GreaterThanOrEqual,
                        Constant.SearchTermOperator.Glob
                    };
            }
        }

        protected class BooleanConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                Debug.Assert(targetType == typeof(Nullable<bool>), "Expected to convert to Nullable<bool>.");
                return new Nullable<bool>((bool)value);
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                Debug.Assert(targetType == typeof(object), "Expected request to convert bool to object.");
                return ((Nullable<bool>)value).Value;
            }
        }

        protected class DateTimeOffsetConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                Debug.Assert(targetType == typeof(DateTimeOffset), "Expected to convert to DateTimeOffset.");
                return new DateTimeOffset((DateTime)value);
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                Debug.Assert(targetType == typeof(object), "Expected request to convert DateTimeOffset to object.");
                return ((DateTimeOffset)value).UtcDateTime;
            }
        }

        protected class IntegerConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                Debug.Assert(targetType == typeof(string), "Expected to convert to string.");
                return value.ToString();
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                Debug.Assert(targetType == typeof(object), "Expected request to convert string to object.");
                string valueAsString = (string)value;
                if (String.IsNullOrEmpty(valueAsString))
                {
                    // while counter input is limited to numbers a user can delete all text during editing
                    return null;
                }
                else
                {
                    return Int32.Parse(valueAsString, NumberStyles.AllowLeadingSign, CultureInfo.CurrentCulture);
                }
            }
        }

        protected class UtcOffsetConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                Debug.Assert(targetType == typeof(TimeSpan), "Expected to convert to TimeSpan.");
                if (value is TimeSpan timeSpan)
                {
                    return timeSpan;
                }
                return DateTimeHandler.FromDatabaseUtcOffset((double)value);
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                Debug.Assert(targetType == typeof(double), "Expected to convert to double.");
                return DateTimeHandler.ToDatabaseUtcOffset((TimeSpan)value);
            }
        }
    }
}
