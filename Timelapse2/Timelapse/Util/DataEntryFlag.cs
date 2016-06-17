using System.Windows;
using System.Windows.Controls;

namespace Timelapse.Util
{
    // A flag comprises a stack panel containing
    // - a label containing the descriptive label) 
    // - checkbobox (the content) at the given width
    public class DataEntryFlag : DataEntryControl<CheckBox, Label>
    {
        /// <summary>Gets or sets the Content of the Note</summary>
        public override string Content
        {
            get
            {
                return ((bool)this.ContentControl.IsChecked) ? Constants.Boolean.True : Constants.Boolean.False;
            }
            set
            {
                value = value.ToLower();
                this.ContentControl.IsChecked = (value == Constants.Boolean.True) ? true : false;
            }
        }

        public DataEntryFlag(string dataLabel, DataEntryControls styleProvider)
            : base(dataLabel, styleProvider, ControlContentStyle.FlagCodeBar, ControlLabelStyle.LabelCodeBar)
        {
            this.Container.ToolTip = "Toggle between true (checked) and false (unchecked)";
        }
    }
}
