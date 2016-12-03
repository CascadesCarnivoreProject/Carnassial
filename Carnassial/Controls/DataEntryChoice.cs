using Carnassial.Database;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Carnassial.Controls
{
    // A choice comprises a stack panel containing
    // - a label containing the descriptive label) 
    // - a combobox (containing the content) at the given width
    public class DataEntryChoice : DataEntryControl<ComboBox, Label>
    {
        /// <summary>Gets the current choice.</summary>
        public override string Content
        {
            get { return (string)this.ContentControl.SelectedItem; }
        }

        public override bool ContentReadOnly
        {
            get { return this.ContentControl.IsReadOnly; }
            set { this.ContentControl.IsReadOnly = value; }
        }

        public DataEntryChoice(ControlRow control, DataEntryControls styleProvider)
            : base(control, styleProvider, ControlContentStyle.ChoiceComboBox, ControlLabelStyle.DefaultLabel)
        {
            // The look of the combobox
            this.ContentControl.Height = 25;
            this.ContentControl.HorizontalAlignment = HorizontalAlignment.Left;
            this.ContentControl.VerticalAlignment = VerticalAlignment.Center;
            this.ContentControl.VerticalContentAlignment = VerticalAlignment.Center;
            this.ContentControl.BorderThickness = new Thickness(1);
            this.ContentControl.BorderBrush = Brushes.LightBlue;

            // The behaviour of the combobox
            this.ContentControl.Focusable = true;
            this.ContentControl.IsEditable = false;
            this.ContentControl.IsTextSearchEnabled = true;

            // Callback used to allow Enter to select the highlit item
            this.ContentControl.PreviewKeyDown += this.ContentCtl_PreviewKeyDown;

            // Add items to the combo box
            bool emptyChoiceAllowed = false;
            foreach (string choice in control.GetChoices())
            {
                if (choice == String.Empty)
                {
                    emptyChoiceAllowed = true;
                }
                else
                {
                    this.ContentControl.Items.Add(choice);
                }
            }

            if (emptyChoiceAllowed)
            {
                // put empty choices at the end below a separator for visual clarity
                this.ContentControl.Items.Add(new Separator());
                this.ContentControl.Items.Add(String.Empty);
            }

            this.ContentControl.SelectedIndex = 0;
        }

        // Users may want to use the text search facility on the combobox, where they type the first letter and then enter
        // For some reason, it wasn't working on pressing 'enter' so this handler does that.
        // Whenever a return or enter is detected on the combobox, it finds the highlight item (i.e., that is highlit from the text search)
        // and sets the combobox to that value.
        private void ContentCtl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return || e.Key == Key.Enter)
            {
                ComboBox comboBox = sender as ComboBox;
                for (int i = 0; i < comboBox.Items.Count; i++)
                {
                    ComboBoxItem comboBoxItem = (ComboBoxItem)comboBox.ItemContainerGenerator.ContainerFromIndex(i);
                    if (comboBoxItem != null && comboBoxItem.IsHighlighted)
                    {
                        comboBox.SelectedValue = comboBoxItem.Content.ToString();
                    }
                }
            }
        }

        public override void SetContentAndTooltip(string value)
        {
            this.ContentControl.SelectedValue = value;
            this.ContentControl.ToolTip = value;
        }

        public override void SetValue(object valueAsObject)
        {
            if (valueAsObject is FileSelection)
            {
                this.SetContentAndTooltip(valueAsObject.ToString());
            }
            else if ((valueAsObject is string) || (valueAsObject == null))
            {
                this.SetContentAndTooltip((string)valueAsObject);
            }
            else
            {
                throw new ArgumentOutOfRangeException("valueAsObject", String.Format("Unexpected value type {0}.", valueAsObject.GetType()));
            }
        }
    }
}
