using Carnassial.Database;
using System;
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

        public override void SetContentAndTooltip(string value)
        {
            this.ContentControl.IsChecked = Boolean.Parse(value);
            this.ContentControl.ToolTip = value;
        }
    }
}
