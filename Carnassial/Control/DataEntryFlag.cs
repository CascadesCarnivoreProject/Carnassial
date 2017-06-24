using Carnassial.Data;
using System;
using System.Diagnostics;
using System.Windows.Controls;

namespace Carnassial.Control
{
    // A flag comprises a stack panel containing
    // - a label containing the descriptive label) 
    // - checkbobox (the content) at the given width
    public class DataEntryFlag : DataEntryControl<CheckBox, Label>
    {
        public DataEntryFlag(ControlRow control, DataEntryControls styleProvider)
            : base(control, styleProvider, ControlContentStyle.FlagCheckBox, ControlLabelStyle.Label)
        {
            this.ContentControl.SetBinding(CheckBox.IsCheckedProperty, ImageRow.GetDataBindingPath(control.DataLabel));
        }

        /// <summary>Gets the current state of the flag</summary>
        /// <remarks>true if the flag is checked, false otherwise</remarks>
        public override string Content
        {
            get { return (bool)this.ContentControl.IsChecked ? Boolean.TrueString : Boolean.FalseString; }
        }

        public override bool ContentReadOnly
        {
            get { return !this.ContentControl.IsEnabled; }
            set { this.ContentControl.IsEnabled = !value; }
        }

        public override void SetValue(object valueAsObject)
        {
            if (valueAsObject is string)
            {
                string valueAsString = (string)valueAsObject;
                Debug.Assert(String.Equals(valueAsString, Boolean.FalseString, StringComparison.OrdinalIgnoreCase) || String.Equals(valueAsString, Boolean.TrueString, StringComparison.OrdinalIgnoreCase), String.Format("Unexpected value '{0}'.", valueAsString));
                this.ContentControl.IsChecked = Boolean.Parse(valueAsString);
                this.ContentControl.ToolTip = valueAsString;
            }
            else if (valueAsObject is bool)
            {
                bool value = (bool)valueAsObject;
                this.ContentControl.IsChecked = value;
                this.ContentControl.ToolTip = value ? Boolean.TrueString : Boolean.FalseString;
            }
            else
            {
                if (valueAsObject == null)
                {
                    throw new ArgumentNullException(nameof(valueAsObject));
                }
                throw new ArgumentOutOfRangeException(nameof(valueAsObject), String.Format("Unexpected value type {0}.", valueAsObject.GetType()));
            }
        }
    }
}
