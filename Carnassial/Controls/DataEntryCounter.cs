using Carnassial.Database;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace Carnassial.Controls
{
    // A counter comprises a stack panel containing
    // - a radio button containing the descriptive label
    // - an editable textbox (containing the content) at the given width
    public class DataEntryCounter : DataEntryControl<TextBox, RadioButton>
    {
        private bool previousLabelControlIsChecked;

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
            // Assign all counters to a single group so that selecting a new counter deselects any currently selected counter
            this.LabelControl.GroupName = "DataEntryCounter";

            this.LabelControl.Click += this.LabelControl_Click;
            this.previousLabelControlIsChecked = false;
        }

        private void LabelControl_Click(object sender, RoutedEventArgs e)
        {
            if (this.previousLabelControlIsChecked)
            {
                this.LabelControl.IsChecked = false;
            }
            this.previousLabelControlIsChecked = this.LabelControl.IsChecked.Value;
        }

        public override void SetContentAndTooltip(string valueAsString)
        {
            this.ContentControl.Text = valueAsString;
            this.ContentControl.ToolTip = valueAsString;
        }

        public override void SetValue(object valueAsObject)
        {
            // counter values are unsigned integers but are are manipulated as strings
            this.SetContentAndTooltip((string)valueAsObject);
        }
    }
}