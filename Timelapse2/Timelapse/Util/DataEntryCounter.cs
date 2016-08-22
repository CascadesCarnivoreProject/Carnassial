using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Timelapse.Util
{
    // A counter comprises a stack panel containing
    // - a radio button containing the descriptive label
    // - an editable textbox (containing the content) at the given width
    public class DataEntryCounter : DataEntryControl<TextBox, RadioButton>
    {
        /// <summary>Gets or sets the content of the counter.</summary>
        public override string Content
        {
            get { return this.ContentControl.Text; }
            set { this.ContentControl.Text = value; }
        }

        public bool IsSelected
        {
            get { return this.LabelControl.IsChecked.HasValue ? (bool)this.LabelControl.IsChecked : false; }
        }

        public DataEntryCounter(string dataLabel, DataEntryControls styleProvider) : 
            base(dataLabel, styleProvider, ControlContentStyle.TextBoxCodeBar, ControlLabelStyle.RadioButtonCodeBar)
        {
            // Modify the context menu so it can have a propage submenu
            // TODO DISCRETIONARY: the context menu's attached to the container rather than the content?
            //             and if the context menu is nulled out after creation why is it possible to pass createContextMenu = true?
            this.ContentControl.ContextMenu = null;

            // Now configure the various elements
            this.Container.ToolTip = "Select the button, then click on the entity in the image to increment its count OR type in a number";

            // Make this part of a group with all the other radio buttons of this type
            this.LabelControl.GroupName = "A";
        }
    }
}
