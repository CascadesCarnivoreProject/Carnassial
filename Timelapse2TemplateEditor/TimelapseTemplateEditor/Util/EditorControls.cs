using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Database;
using Timelapse.Util;

namespace Timelapse.Editor.Util
{
    /// <summary>Generates controls in the provided wrap panel based upon the information in the data grid templateTable.</summary>
    /// <remarks>
    /// It is meant to approximate what the controls will look like when rendered in the Timelapse UX by DataEntryControls but the
    /// two classes contain distinct code as rendering an immutable set of data entry controls is significantly different from the
    /// mutable set of controls which don't accept data in the editor.  Reusing the layout code in the DataEntryControl hierarchy
    /// is desirable but not currently feasible due to reliance on DataEntryControls.Propagate.
    /// </remarks>
    internal class EditorControls
    {
        public static void Generate(EditorWindow mainWindow, WrapPanel parent, DataTableBackedList<ControlRow> templateTable)
        {
            // used for styling all content and label controls except ComboBoxes since the combo box style is commented out in DataEntryControls.xaml
            // and defined instead in MainWindow.xaml as an exception workaround
            DataEntryControls styleProvider = new DataEntryControls();

            parent.Children.Clear();
            foreach (ControlRow control in templateTable)
            {
                if (control.DefaultValue == String.Empty)
                {
                    if (control.Type == Constants.DatabaseColumn.Date)
                    {
                        control.DefaultValue = DateTime.Now.ToString(Constants.Time.DateFormat); // "01-Jun-2016"
                    }
                    else if (control.Type == Constants.DatabaseColumn.Time)
                    {
                        control.DefaultValue = DateTime.Now.ToString(Constants.Time.TimeFormatForUser); // "07:39 PM"
                    }
                }

                // instantiate control UX objects
                StackPanel stackPanel;
                switch (control.Type)
                {
                    case Constants.DatabaseColumn.File:
                    case Constants.DatabaseColumn.RelativePath:
                    case Constants.DatabaseColumn.Folder:
                    case Constants.DatabaseColumn.Date:
                    case Constants.DatabaseColumn.Time:
                    case Constants.Control.Note:
                        Label noteLabel = CreateLabel(styleProvider, control.Label, control.Tooltip);
                        TextBox noteContent = CreateTextBox(styleProvider, control.DefaultValue, control.Tooltip, control.TextBoxWidth);
                        stackPanel = CreateStackPanel(styleProvider, noteLabel, noteContent);
                        break;
                    case Constants.Control.Counter:
                        RadioButton counterLabel = CreateRadioButton(styleProvider, control.Label, control.Tooltip);
                        TextBox coutnerContent = CreateTextBox(styleProvider, control.DefaultValue, control.Tooltip, control.TextBoxWidth);
                        stackPanel = CreateStackPanel(styleProvider, counterLabel, coutnerContent);
                        break;
                    case Constants.Control.Flag:
                    case Constants.Control.DeleteFlag:
                        Label flagLabel = CreateLabel(styleProvider, control.Label, control.Tooltip);
                        CheckBox flagContent = CreateFlag(styleProvider, String.Empty, control.Tooltip);
                        flagContent.IsChecked = (control.DefaultValue == Constants.Boolean.True) ? true : false;
                        stackPanel = CreateStackPanel(styleProvider, flagLabel, flagContent);
                        break;
                    case Constants.Control.FixedChoice:
                    case Constants.DatabaseColumn.ImageQuality:
                        Label choiceLabel = CreateLabel(styleProvider, control.Label, control.Tooltip);
                        ComboBox choiceContent = CreateComboBox(mainWindow, control.List, control.Tooltip, control.TextBoxWidth);
                        stackPanel = CreateStackPanel(styleProvider, choiceLabel, choiceContent);
                        break;
                    default:
                        throw new NotSupportedException(String.Format("Unhandled control type {0}.", control.Type));
                }

                stackPanel.Tag = control.DataLabel;
                if (control.Visible == false)
                {
                    stackPanel.Visibility = Visibility.Collapsed;
                }

                // add control to wrap panel
                parent.Children.Add(stackPanel);
            }
        }

        public static bool IsStandardControlType(string controlType)
        {
            return Constants.Control.StandardTypes.Contains(controlType);
        }

        // Returns a stack panel containing two controls
        // The stack panel ensures that controls are layed out as a single unit with certain spatial characteristcs 
        // i.e.,  a given height, right margin, where contents will not be broken durring (say) panel wrapping
        private static StackPanel CreateStackPanel(DataEntryControls styleProvider, Control label, Control content)
        {
            StackPanel stackPanel = new StackPanel();
            stackPanel.Children.Add(label);
            stackPanel.Children.Add(content);

            Style style = styleProvider.FindResource(Constants.ControlStyle.StackPanelCodeBar) as Style;
            stackPanel.Style = style;
            return stackPanel;
        }

        private static Label CreateLabel(DataEntryControls styleProvider, string labelText, string tooltip)
        {
            Label label = new Label();
            label.Content = labelText;
            label.ToolTip = tooltip;

            Style style = styleProvider.FindResource(ControlLabelStyle.LabelCodeBar.ToString()) as Style;
            label.Style = style;
            return label;
        }

        private static TextBox CreateTextBox(DataEntryControls styleProvider, string textBoxText, string tooltip, int width)
        {
            TextBox textBox = new TextBox();
            textBox.Width = width;
            textBox.Text = textBoxText;
            textBox.ToolTip = tooltip;

            Style style = styleProvider.FindResource(ControlContentStyle.TextBoxCodeBar.ToString()) as Style;
            textBox.Style = style;
            return textBox;
        }

        private static RadioButton CreateRadioButton(DataEntryControls styleProvider, string labelText, string tooltip)
        {
            RadioButton radioButton = new RadioButton();
            radioButton.GroupName = "A";
            radioButton.Content = labelText;
            radioButton.ToolTip = tooltip;

            Style style = styleProvider.FindResource(ControlLabelStyle.RadioButtonCodeBar.ToString()) as Style;
            radioButton.Style = style;
            return radioButton;
        }

        private static CheckBox CreateFlag(DataEntryControls styleProvider, string labelText, string tooltip)
        {
            CheckBox checkBox = new CheckBox();
            checkBox.Content = labelText;
            checkBox.Visibility = Visibility.Visible;
            checkBox.ToolTip = tooltip;

            Style style = styleProvider.FindResource(ControlContentStyle.FlagCodeBar.ToString()) as Style;
            checkBox.Style = style;
            return checkBox;
        }

        private static ComboBox CreateComboBox(EditorWindow styleProvider, string list, string tooltip, int width)
        {
            ComboBox comboBox = new ComboBox();
            comboBox.ToolTip = tooltip;
            comboBox.Width = width;
            List<string> result = list.Split(new char[] { '|' }).ToList();
            foreach (string str in result)
            {
                comboBox.Items.Add(str.Trim());
            }
            comboBox.SelectedIndex = 0;

            Style style = styleProvider.FindResource(Constants.ControlStyle.ComboBoxCodeBar) as Style;
            comboBox.Style = style;
            return comboBox;
        }
    }
}
