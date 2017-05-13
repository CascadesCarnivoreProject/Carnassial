using Carnassial.Data;
using Carnassial.Util;
using System;
using System.Windows;
using System.Windows.Controls;

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
            base(control, styleProvider, ControlContentStyle.DateTimeOffsetPicker, ControlLabelStyle.DefaultLabel)
        {
            this.Container.ToolTip = "Enter a date/time";

            this.ContentControl.DateTimeDisplay.Style = (Style)styleProvider.FindResource(ControlContentStyle.NoteCounterTextBox.ToString());
            // update label with direct target to get desired hotkey behavior
            // Since DateTimeOffsetPicker forwards focus to the date time display the base class code which sets the label's target to the picker results
            // in WPF setting focus to the picker again rather than moving to other controls assiociated with the hotkey.  It appears this occurs because
            // WPF always finds the date time picker as the first candidate for the hotkey and doesn't detect that focus is already within the picker.
            this.LabelControl.Target = this.ContentControl.DateTimeDisplay;
        }

        public override void SetValue(object valueAsObject)
        {
            DateTimeOffset value;
            if (valueAsObject is string)
            {
                if (DateTimeHandler.TryParseDatabaseDateTime((string)valueAsObject, out value) == false)
                {
                    if (DateTimeHandler.TryParseDisplayDateTime((string)valueAsObject, out value) == false)
                    {
                        throw new ArgumentOutOfRangeException(nameof(valueAsObject), String.Format("Unsupported date time format {0}.", valueAsObject));
                    }
                }
            }
            else if (valueAsObject is DateTimeOffset)
            {
                value = (DateTimeOffset)valueAsObject;
            }
            else
            {
                if (valueAsObject == null)
                {
                    throw new ArgumentNullException(nameof(valueAsObject));
                }
                throw new ArgumentOutOfRangeException(nameof(valueAsObject), String.Format("Unsupported value type {0}.", valueAsObject.GetType().Name));
            }

            // persist selection through value changes
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
