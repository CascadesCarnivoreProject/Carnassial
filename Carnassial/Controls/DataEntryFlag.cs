using Carnassial.Database;
using System;
using System.Diagnostics;
using System.Windows.Controls;

namespace Carnassial.Controls
{
    // A flag comprises a stack panel containing
    // - a label containing the descriptive label) 
    // - checkbobox (the content) at the given width
    public class DataEntryFlag : DataEntryControl<CheckBox, Label>
    {
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

        public DataEntryFlag(ControlRow control, DataEntryControls styleProvider)
            : base(control, styleProvider, ControlContentStyle.FlagCheckBox, ControlLabelStyle.DefaultLabel)
        {
        }

        public override void SetContentAndTooltip(string valueAsString)
        {
            this.ContentControl.IsChecked = Boolean.Parse(valueAsString);
            this.ContentControl.ToolTip = valueAsString;
        }

        public override void SetValue(object valueAsObject)
        {
            if (valueAsObject is bool)
            {
                bool value = (bool)valueAsObject;
                this.ContentControl.IsChecked = value;
                this.ContentControl.ToolTip = value ? Boolean.TrueString : Boolean.FalseString;
            }
            else if (valueAsObject is string)
            {
                string valueAsString = (string)valueAsObject;
                Debug.Assert(String.Equals(valueAsString, Boolean.FalseString, StringComparison.Ordinal) || String.Equals(valueAsString, Boolean.TrueString, StringComparison.Ordinal), String.Format("Unexpected value '{0}'.", valueAsString));
                this.SetContentAndTooltip(valueAsString);
            }
            else
            {
                throw new ArgumentOutOfRangeException("valueAsObject", String.Format("Unexpected value type {0}.", valueAsObject.GetType()));
            }
        }
    }
}
