using System;
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

        public DataEntryCounter(string dataLabel, Controls dataEntryControls, bool createContextMenu) : 
            base(dataLabel, dataEntryControls, ControlContentStyle.TextBoxCodeBar, ControlLabelStyle.RadioButtonCodeBar, createContextMenu)
        {
            // Modify the context menu so it can have a propage submenu
            // TODOSAUL: the context menu's attached to the container rather than the content?
            //             and if the context menu is nulled out after creation why is it possible to pass createContextMenu = true?
            this.ContentControl.ContextMenu = null;

            // Now configure the various elements
            this.Container.ToolTip = "Select the button, then click on the entity in the image to increment its count OR type in a number";

            // Make this part of a group with all the other radio buttons of this type
            this.LabelControl.GroupName = "A";

            // Change the menu text to indicate that propagate goes back to the last non-zero value
            this.MenuItemPropagateFromLastValue.Header = "Propagate from the last non-zero value to here";
        }

        // Menu selections for propagating or copying the current value of this control to all images
        // Note that this overrides the default where checkForZero is false, as Counters use '0' as the empty value
        protected override void MenuItemPropagateFromLastValue_Click(object sender, RoutedEventArgs e)
        {
            bool checkForZero = true;
            bool isflag = false;
            this.PassingContentValue = this.ControlsPanel.Propagate.FromLastValue(this.DataLabel, checkForZero, isflag);
            this.Refresh();
        }

        // Copy the current value of this control to all images
        protected override void MenuItemCopyCurrentValue_Click(object sender, RoutedEventArgs e)
        {
            bool checkForZero = true;
            this.ControlsPanel.Propagate.CopyValues(this.DataLabel, checkForZero);
        }

        // Propagate the current value of this control forward from this point across the current set of filtered images
        protected override void MenuItemPropagateForward_Click(object sender, RoutedEventArgs e)
        {
            bool checkForZero = true;
            this.ControlsPanel.Propagate.Forward(this.DataLabel, checkForZero);
        }

        // A callback allowing us to enable or disable particular context menu items
        protected override void Container_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Decide which context menu items to enable
            // May not be able to do this without the dbData!
            bool checkForZero = true;
            this.CopyForwardEnabled = this.ControlsPanel.Propagate.Forward_IsPossible(this.DataLabel);
            this.PropagateFromLastValueEnabled = this.ControlsPanel.Propagate.FromLastValue_IsPossible(this.DataLabel, checkForZero);
        }
    }
}
