using Carnassial.Data;
using Carnassial.Util;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Carnassial.Control
{
    // A counter comprises
    // - a radio button containing the descriptive label
    // - an editable textbox (containing the content) at the given width
    public class DataEntryCounter : DataEntryControl<TextBox, RadioButton>
    {
        private bool labelControlIsChecked;

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

            this.ContentControl.SetBinding(TextBox.TextProperty, ImageRow.GetDataBindingPath(control));
        }

        /// <summary>Ensures only numbers are entered for counters.</summary>
        private void ContentControl_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Utilities.IsDigits(e.Text);
        }

        public override bool IsCopyableValue(object value)
        {
            return (int)value != 0;
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
    }
}