using Carnassial.Data;
using System;
using System.Collections.Generic;
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
            this.ContentControl.Focusable = true;
            this.ContentControl.Height = 25;
            this.ContentControl.IsEditable = false;
            this.ContentControl.IsTextSearchEnabled = true;
            this.ContentControl.HorizontalAlignment = HorizontalAlignment.Left;
            this.ContentControl.VerticalAlignment = VerticalAlignment.Center;
            this.ContentControl.VerticalContentAlignment = VerticalAlignment.Center;
            this.ContentControl.BorderThickness = new Thickness(1);
            this.ContentControl.BorderBrush = Brushes.LightBlue;

            // callback used to allow Enter to select the highlit item
            this.ContentControl.PreviewKeyDown += this.ContentCtl_PreviewKeyDown;

            // add items to the combo box
            this.SetChoices(control.GetChoices());
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

        public override List<string> GetChoices()
        {
            List<string> choices = new List<string>();
            foreach (object item in this.ContentControl.Items)
            {
                if (item is string)
                {
                    choices.Add((string)item);
                }
            }
            return choices;
        }

        public override void SetChoices(List<string> choices)
        {
            this.ContentControl.Items.Clear();

            bool emptyChoiceAllowed = false;
            foreach (string choice in choices)
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

        public override void SetValue(object valueAsObject)
        {
            string valueAsString;
            if ((valueAsObject is string) || (valueAsObject == null))
            {
                valueAsString = (string)valueAsObject;
            }
            else if (valueAsObject is FileSelection)
            {
                valueAsString = valueAsObject.ToString();
            }
            else
            {
                throw new ArgumentOutOfRangeException("valueAsObject", String.Format("Unsupported value type {0}.", valueAsObject.GetType()));
            }

            this.ContentControl.SelectedValue = valueAsString;
            this.ContentControl.ToolTip = valueAsString;
        }
    }
}
