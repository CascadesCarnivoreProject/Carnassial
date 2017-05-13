using Carnassial.Data;
using Carnassial.Util;
using System;
using System.Windows.Controls;

namespace Carnassial.Controls
{
    public class DataEntryUtcOffset : DataEntryControl<UtcOffsetPicker, Label>
    {
        public override string Content
        {
            get { return this.ContentControl.TimeSpanDisplay.Text; }
        }

        public override bool ContentReadOnly
        {
            get { return !this.ContentControl.IsEnabled; }
            set { this.ContentControl.IsEnabled = !value; }
        }

        public DataEntryUtcOffset(ControlRow control, DataEntryControls styleProvider) :
            base(control, styleProvider, ControlContentStyle.UtcOffsetPicker, ControlLabelStyle.DefaultLabel)
        {
        }

        public override void SetValue(object valueAsObject)
        {
            TimeSpan value;
            if (valueAsObject is string)
            {
                value = DateTimeHandler.ParseDatabaseUtcOffsetString((string)valueAsObject);
            }
            else if (valueAsObject is TimeSpan)
            {
                value = (TimeSpan)valueAsObject;
            }
            else
            {
                if (valueAsObject == null)
                {
                    throw new ArgumentNullException(nameof(valueAsObject));
                }
                throw new ArgumentOutOfRangeException("valueAsObject", String.Format("Unsupported value type {0}.", valueAsObject.GetType()));
            }

            // persist selection through value changes
            int selectionStart = this.ContentControl.TimeSpanDisplay.SelectionStart;
            int selectionLength = this.ContentControl.TimeSpanDisplay.SelectionLength;
            this.ContentControl.Value = value;
            this.ContentControl.ToolTip = value.ToString(this.ContentControl.Format);
            this.ContentControl.TimeSpanDisplay.SelectionStart = selectionStart;
            this.ContentControl.TimeSpanDisplay.SelectionLength = selectionLength;
        }
    }
}
