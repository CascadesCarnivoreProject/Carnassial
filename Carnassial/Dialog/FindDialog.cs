using Carnassial.Control;
using Carnassial.Data;
using Carnassial.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Carnassial.Dialog
{
    public class FindDialog : Window
    {
        protected event Action<SearchTerm> SearchTermValueChanged;

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

        protected System.Windows.Controls.Control CreateValueControl(SearchTerm searchTerm, FileDatabase fileDatabase)
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
                        Height = Constant.UserInterface.FindTextBoxHeight,
                        IsEnabled = searchTerm.UseForSearching,
                        Margin = Constant.UserInterface.FindCellMargin,
                        Tag = searchTerm,
                        Text = searchTerm.DatabaseValue?.ToString(),
                        TextWrapping = TextWrapping.NoWrap,
                        VerticalAlignment = VerticalAlignment.Center,
                        VerticalContentAlignment = VerticalAlignment.Center,
                        Width = Constant.UserInterface.FindValueWidth
                    };

                    textBox.TextAutocompleted += this.SearchTermDatabaseValue_TextAutocompleted;
                    if (controlType == ControlType.Counter)
                    {
                        // accept only numbers in counter text boxes
                        textBox.PreviewTextInput += this.Counter_PreviewTextInput;
                        DataObject.AddPastingHandler(textBox, this.Counter_Paste);
                    }
                    return textBox;
                case ControlType.DateTime:
                    DateTimeOffsetPicker dateTimePicker = new DateTimeOffsetPicker()
                    {
                        IsEnabled = searchTerm.UseForSearching,
                        Tag = searchTerm,
                        Value = (DateTime)searchTerm.DatabaseValue,
                        Width = Constant.UserInterface.FindValueWidth
                    };
                    dateTimePicker.ValueChanged += this.DateTime_ValueChanged;
                    return dateTimePicker;
                case ControlType.FixedChoice:
                    ComboBox comboBox = new ComboBox()
                    {
                        IsEnabled = searchTerm.UseForSearching,
                        ItemsSource = searchTerm.WellKnownValues,
                        Margin = Constant.UserInterface.FindCellMargin,
                        SelectedItem = searchTerm.DatabaseValue,
                        Tag = searchTerm,
                        Width = Constant.UserInterface.FindValueWidth
                    };
                    comboBox.SelectionChanged += this.FixedChoice_SelectionChanged;
                    return comboBox;
                case ControlType.Flag:
                    CheckBox checkBox = new CheckBox()
                    {
                        HorizontalAlignment = HorizontalAlignment.Left,
                        IsChecked = (bool)searchTerm.DatabaseValue,
                        IsEnabled = searchTerm.UseForSearching,
                        Margin = Constant.UserInterface.FindCellMargin,
                        Tag = searchTerm,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    checkBox.Checked += this.Flag_CheckedOrUnchecked;
                    checkBox.Unchecked += this.Flag_CheckedOrUnchecked;
                    return checkBox;
                case ControlType.UtcOffset:
                    UtcOffsetPicker utcOffsetPicker = new UtcOffsetPicker()
                    {
                        IsEnabled = searchTerm.UseForSearching,
                        IsTabStop = true,
                        Tag = searchTerm,
                        Value = (TimeSpan)searchTerm.DatabaseValue,
                        Width = Constant.UserInterface.FindValueWidth
                    };
                    utcOffsetPicker.ValueChanged += this.UtcOffset_ValueChanged;
                    return utcOffsetPicker;
                default:
                    throw new NotSupportedException(String.Format("Unhandled control type '{0}'.", controlType));
            }
        }

        private void DateTime_ValueChanged(DateTimeOffsetPicker datePicker, DateTimeOffset newDateTime)
        {
            SearchTerm searchTerm = (SearchTerm)datePicker.Tag;
            searchTerm.DatabaseValue = datePicker.Value.UtcDateTime;
            this.SearchTermValueChanged?.Invoke(searchTerm);
        }

        private void FixedChoice_SelectionChanged(object sender, SelectionChangedEventArgs args)
        {
            ComboBox comboBox = sender as ComboBox;
            SearchTerm searchTerm = (SearchTerm)comboBox.Tag;
            if (String.Equals(searchTerm.DataLabel, Constant.FileColumn.Classification, StringComparison.Ordinal))
            {
                searchTerm.DatabaseValue = (int)comboBox.SelectedValue;
            }
            else
            {
                searchTerm.DatabaseValue = (string)comboBox.SelectedValue;
            }

            this.SearchTermValueChanged?.Invoke(searchTerm);
        }

        private void Flag_CheckedOrUnchecked(object sender, RoutedEventArgs e)
        {
            CheckBox checkBox = sender as CheckBox;
            SearchTerm searchTerm = (SearchTerm)checkBox.Tag;

            Debug.Assert(checkBox.IsChecked.HasValue, "Expected check box to be either checked or unchecked but it doesn't have a value.");
            searchTerm.DatabaseValue = checkBox.IsChecked.Value;
            this.SearchTermValueChanged?.Invoke(searchTerm);
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

        protected void Operator_SelectionChanged(object sender, SelectionChangedEventArgs args)
        {
            ComboBox comboBox = sender as ComboBox;
            SearchTerm searchTerm = (SearchTerm)comboBox.Tag;
            searchTerm.Operator = (string)comboBox.SelectedValue;
            this.SearchTermValueChanged?.Invoke(searchTerm);
        }

        private void SearchTermDatabaseValue_TextAutocompleted(object sender, TextChangedEventArgs args)
        {
            TextBox textBox = sender as TextBox;
            SearchTerm searchTerm = (SearchTerm)textBox.Tag;
            if (searchTerm.ControlType == ControlType.Counter)
            {
                if (String.IsNullOrEmpty(textBox.Text))
                {
                    // while counter input is limited to numbers a user can delete all text during editing
                    searchTerm.DatabaseValue = null;
                }
                else
                {
                    searchTerm.DatabaseValue = Int32.Parse(textBox.Text, NumberStyles.AllowLeadingSign, CultureInfo.CurrentCulture);
                }
            }
            else
            {
                searchTerm.DatabaseValue = textBox.Text;
            }
            this.SearchTermValueChanged?.Invoke(searchTerm);
        }

        private void UtcOffset_ValueChanged(TimeSpanPicker utcOffsetPicker, TimeSpan newTimeSpan)
        {
            SearchTerm searchTerm = (SearchTerm)utcOffsetPicker.Tag;
            searchTerm.DatabaseValue = utcOffsetPicker.Value.TotalHours;
            this.SearchTermValueChanged?.Invoke(searchTerm);
        }
    }
}
