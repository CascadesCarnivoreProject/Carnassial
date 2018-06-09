using Carnassial.Data;
using Carnassial.Util;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace Carnassial.Control
{
    // A counter comprises a stack panel containing
    // - a radio button containing the descriptive label
    // - an editable textbox (containing the content) at the given width
    public class DataEntryCounter : DataEntryControl<TextBox, RadioButton>
    {
        private bool labelControlIsChecked;

        /// <summary>Gets the content of the counter.</summary>
        public override string Content
        {
            get { return this.ContentControl.Text; }
        }

        public override bool ContentReadOnly
        {
            get { return this.ContentControl.IsReadOnly; }
            set { this.ContentControl.IsReadOnly = value; }
        }

        public bool IsSelected
        {
            get { return this.LabelControl.IsChecked.HasValue ? (bool)this.LabelControl.IsChecked : false; }
        }

        public DataEntryCounter(ControlRow control, DataEntryControls styleProvider) :
            base(control, styleProvider, ControlContentStyle.NoteCounterTextBox, ControlLabelStyle.CounterButton)
        {
            this.ContentControl.PreviewTextInput += this.ContentControl_PreviewTextInput;

            // assign all counters to a single group so that selecting a new counter deselects any currently selected counter
            this.LabelControl.GroupName = "DataEntryCounter";
            this.LabelControl.Click += this.LabelControl_Click;
            this.labelControlIsChecked = false;

            Binding binding = new Binding(ImageRow.GetDataBindingPath(control.DataLabel))
            {
                Converter = new CounterDataBindingValueConverter()
            };
            this.ContentControl.SetBinding(TextBox.TextProperty, binding);
        }

        /// <summary>Ensures only numbers are entered for counters.</summary>
        private void ContentControl_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Utilities.IsDigits(e.Text);
        }

        private void LabelControl_Click(object sender, RoutedEventArgs e)
        {
            if (this.labelControlIsChecked)
            {
                this.LabelControl.IsChecked = false;
            }
            this.labelControlIsChecked = this.LabelControl.IsChecked.Value;
        }

        public override void SetValue(object valueAsObject)
        {
            // counter values are unsigned integers but are are manipulated as strings
            string valueAsString;
            if ((valueAsObject is string) || (valueAsObject == null))
            {
                valueAsString = (string)valueAsObject;
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(valueAsObject), String.Format("Unsupported value type {0}.", valueAsObject.GetType()));
            }

            this.ContentControl.Text = valueAsString;
            this.ContentControl.ToolTip = valueAsString;
        }

        private class CounterDataBindingValueConverter : IValueConverter
        {
            private string markers;

            public CounterDataBindingValueConverter()
            {
                this.markers = null;
            }

            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                if (value == null)
                {
                    return null;
                }

                string valueAsString = (string)value;
                int barIndex = valueAsString.IndexOf(Constant.Database.MarkerBar);
                if ((barIndex < 1) || (barIndex >= valueAsString.Length))
                {
                    this.markers = null;
                    return value;
                }

                this.markers = valueAsString.Substring(barIndex, valueAsString.Length - barIndex);
                return valueAsString.Substring(0, barIndex);
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                if (String.IsNullOrEmpty(this.markers))
                {
                    return value;
                }

                return String.Concat(value, this.markers);
            }
        }
    }
}