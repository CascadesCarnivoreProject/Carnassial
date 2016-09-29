using Carnassial.Database;
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
            get { return ((bool)this.ContentControl.IsChecked) ? Constants.Boolean.True : Constants.Boolean.False; }
        }

        public override bool ContentReadOnly
        {
            get { return !this.ContentControl.IsEnabled; }
            set { this.ContentControl.IsEnabled = !value; }
        }

        public DataEntryFlag(ControlRow control, DataEntryControls styleProvider)
            : base(control, styleProvider, ControlContentStyle.FlagCodeBar, ControlLabelStyle.LabelCodeBar)
        {
        }

        public override void SetContentAndTooltip(string value)
        {
            value = value.ToLower();
            this.ContentControl.IsChecked = (value == Constants.Boolean.True) ? true : false;
            this.ContentControl.ToolTip = value;
        }
    }
}
