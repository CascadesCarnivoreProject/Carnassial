using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Timelapse.Util
{
    // A FixedChoice comprises a stack panel containing
    // - a label containing the descriptive label) 
    // - a combobox (containing the content) at the given width
    public class DataEntryChoice : DataEntryControl<ComboBox, Label>
    {
        /// <summary>Gets or sets the content of the choice.</summary>
        public override string Content
        {
            get { return this.ContentControl.Text; }
            set { this.ContentControl.SelectedValue = value; }
        }

        public DataEntryChoice(string dataLabel, Controls dataEntryControls, bool createContextMenu, string choicesAsString)
            : base(dataLabel, dataEntryControls, null, ControlLabelStyle.LabelCodeBar, createContextMenu)
        {
            // The look of the combobox
            this.ContentControl.Height = 25;
            this.ContentControl.HorizontalAlignment = HorizontalAlignment.Left;
            this.ContentControl.VerticalAlignment = VerticalAlignment.Center;
            this.ContentControl.VerticalContentAlignment = VerticalAlignment.Center;
            this.ContentControl.BorderThickness = new Thickness(1);
            this.ContentControl.BorderBrush = Brushes.LightBlue;

            // The behaviour of the combobox
            this.ContentControl.IsTextSearchEnabled = true;
            this.ContentControl.Focusable = true;
            this.ContentControl.IsReadOnly = true;
            this.ContentControl.IsEditable = false;

            // Callback used to allow Enter to select the highlit item
            this.ContentControl.PreviewKeyDown += this.ContentCtl_PreviewKeyDown;

            // Add items to the combo box
            List<string> choices = choicesAsString.Split(new char[] { '|' }).ToList();
            foreach (string choice in choices)
            {
                this.ContentControl.Items.Add(choice.Trim());
            }
            // Add a separator and an empty field to the list, so the user can return it to an empty state. Note that this means ImageQuality can also be empty.. not sure if this is a good thing
            this.ContentControl.Items.Add(new Separator());
            this.ContentControl.Items.Add(String.Empty);
            this.ContentControl.SelectedIndex = 0;

            // Now configure the various elements
            this.Container.ToolTip = "Type in text";
        }

        // Users may want to use the text search facility on the combobox, where they type the first letter and then enter
        // For some reason, it wasn't working on pressing 'enter' so this handler does that.
        // Whenever a return or enter is detected on the combobox, it finds the highlight item (i.e., that is highlit from the text search)
        // and sets the combobox to that value.
        private void ContentCtl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Return || e.Key == System.Windows.Input.Key.Enter)
            {
                ComboBox cb = sender as ComboBox;
                for (int i = 0; i < cb.Items.Count; i++)
                {
                    ComboBoxItem cbi = (ComboBoxItem)cb.ItemContainerGenerator.ContainerFromIndex(i);
                    if (cbi.IsHighlighted)
                    {
                        cb.SelectedValue = cbi.Content.ToString();
                    }
                }
            }
        }
    }
}
