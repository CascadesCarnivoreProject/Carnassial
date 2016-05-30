using System;
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
                return ((bool)this.ContentControl.IsChecked) ? "true" : "false";
            }
            set
            {
                value = value.ToLower();
                this.ContentControl.IsChecked = (value == "true") ? true : false;
            }
        }

        public DataEntryFlag(string dataLabel, Controls dataEntryControls, bool createContextMenu)
            : base(dataLabel, dataEntryControls, ControlContentStyle.FlagCodeBar, ControlLabelStyle.LabelCodeBar, createContextMenu)
        {
            this.Container.ToolTip = "Toggle between true (checked) and false (unchecked)";

            // Change the menu text to indicate that propagate goes back to the last non-zero value
            this.MenuItemPropagateFromLastValue.Header = "Propagate from the last non-zero value to here";
        }

        protected override void MenuItemPropagateFromLastValue_Click(object sender, RoutedEventArgs e)
        {
            bool checkForZero = false;
            bool isflag = true;
            this.PassingContentValue = this.ControlsPanel.Propagate.FromLastValue(this.DataLabel, checkForZero, isflag);
            this.Refresh();
        }
    }
}
