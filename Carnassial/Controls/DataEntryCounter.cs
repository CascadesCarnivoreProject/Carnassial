using Carnassial.Data;
using Carnassial.Util;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Carnassial.Controls
{
    // A counter comprises a stack panel containing
    // - a radio button containing the descriptive label
    // - an editable textbox (containing the content) at the given width
    public class DataEntryCounter : DataEntryControl<TextBox, RadioButton>
    {
        private bool labelControlIsChecked;

        /// <summary>Gets or sets the content of the counter.</summary>
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
                throw new ArgumentOutOfRangeException("valueAsObject", String.Format("Unsupported value type {0}.", valueAsObject.GetType()));
            }

            this.ContentControl.Text = valueAsString;
            this.ContentControl.ToolTip = valueAsString;
        }

        public void IncrementOrReset()
        {
            int count;
            if (Int32.TryParse(this.Content, out count))
            {
                ++count;
                this.SetValue(count.ToString());
            }
            else
            {
                // if the current value's not parseable assume it's due to the default value being non-integer in the template and revert to zero
                this.SetValue("0");
            }
        }

        public bool TryDecrementOrReset()
        {
            // decrement the counter only if it has a positive count
            int previousCount;
            int newCount = 0;
            if (Int32.TryParse(this.Content, out previousCount) && previousCount > 0)
            {
                newCount = previousCount - 1;
                this.SetValue(newCount.ToString());
            }
            else
            {
                previousCount = 0;
                this.SetValue("0");
            }

            return newCount != previousCount;
        }
    }
}