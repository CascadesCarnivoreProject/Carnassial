using Carnassial.Database;
using Carnassial.Util;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Carnassial.Controls
{
    public class DataEntryDateTime : DataEntryControl<DateTimeOffsetPicker, Label>
    {
        public override string Content
        {
            get { return DateTimeHandler.ToDatabaseDateTimeString(this.ContentControl.Value); }
        }

        public override bool ContentReadOnly
        {
            get { return !this.ContentControl.IsEnabled; }
            set { this.ContentControl.IsEnabled = !value; }
        }

        public DataEntryDateTime(ControlRow control, DataEntryControls styleProvider) : 
            base(control, styleProvider, null, ControlLabelStyle.DefaultLabel)
        {
            this.Container.ToolTip = "Enter a date/time";
        }

        public override void Focus(DependencyObject focusScope)
        {
            FocusManager.SetFocusedElement(focusScope, this.ContentControl.DateTimeDisplay);
        }

        public override void SetContentAndTooltip(string value)
        {
            // persist selection through value changes
            int selectionStart = this.ContentControl.DateTimeDisplay.SelectionStart;
            int selectionLength = this.ContentControl.DateTimeDisplay.SelectionLength;
            this.ContentControl.DateTimeDisplay.Text = value;
            this.ContentControl.DateTimeDisplay.ToolTip = value;
            this.ContentControl.DateTimeDisplay.SelectionStart = selectionStart;
            this.ContentControl.DateTimeDisplay.SelectionLength = selectionLength;
        }

        public override void SetValue(object valueAsObject)
        {
            DateTimeOffset value = (DateTimeOffset)valueAsObject;
            int selectionStart = this.ContentControl.DateTimeDisplay.SelectionStart;
            int selectionLength = this.ContentControl.DateTimeDisplay.SelectionLength;
            this.ContentControl.Value = value;
            this.ContentControl.DateTimeDisplay.ToolTip = value.ToString(this.ContentControl.Format);
            this.ContentControl.DateTimeDisplay.SelectionStart = selectionStart;
            this.ContentControl.DateTimeDisplay.SelectionLength = selectionLength;
        }

        public void ShowUtcOffset()
        {
            this.ContentControl.Format = Constant.Time.DateTimeOffsetDisplayFormat;
        }
    }
}
