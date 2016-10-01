using Carnassial.Controls;
using Carnassial.Database;
using System;
using System.Windows;
using System.Windows.Controls;
using Xceed.Wpf.Toolkit;

namespace Carnassial.Editor.Util
{
    /// <summary>Generates controls in the provided wrap panel based upon the information in the data grid templateTable.</summary>
    /// <remarks>
    /// It is meant to approximate what the controls will look like when rendered in the Carnassial UX by DataEntryControls but the
    /// two classes contain distinct code as rendering an immutable set of data entry controls is significantly different from the
    /// mutable set of controls which don't accept data in the editor.  Reusing the layout code in the DataEntryControl hierarchy
    /// is desirable but not currently feasible due to reliance on DataEntryControls.Propagate.
    /// </remarks>
    internal class EditorControls
    {
        public void Generate(EditorWindow mainWindow, WrapPanel parent, DataTableBackedList<ControlRow> templateTable)
        {
            // used for styling all content and label controls except ComboBoxes since the combo box style is commented out in DataEntryControls.xaml
            // and defined instead in MainWindow.xaml as an exception workaround
            DataEntryControls styleProvider = new DataEntryControls();

            parent.Children.Clear();
            foreach (ControlRow control in templateTable)
            {
                // instantiate control UX objects
                StackPanel stackPanel;
                switch (control.Type)
                {
                    case Constants.Control.Note:
                    case Constants.DatabaseColumn.File:
                    case Constants.DatabaseColumn.Folder:
                    case Constants.DatabaseColumn.RelativePath:
                        Label noteLabel = this.CreateLabel(styleProvider, control);
                        TextBox noteContent = this.CreateTextBox(styleProvider, control);
                        stackPanel = this.CreateStackPanel(styleProvider, noteLabel, noteContent);
                        break;
                    case Constants.Control.Counter:
                        RadioButton counterLabel = this.CreateRadioButton(styleProvider, control);
                        TextBox coutnerContent = this.CreateTextBox(styleProvider, control);
                        stackPanel = this.CreateStackPanel(styleProvider, counterLabel, coutnerContent);
                        break;
                    case Constants.Control.Flag:
                    case Constants.DatabaseColumn.DeleteFlag:
                        Label flagLabel = this.CreateLabel(styleProvider, control);
                        CheckBox flagContent = this.CreateFlag(styleProvider, control);
                        flagContent.IsChecked = (control.DefaultValue == Constants.Boolean.True) ? true : false;
                        stackPanel = this.CreateStackPanel(styleProvider, flagLabel, flagContent);
                        break;
                    case Constants.Control.FixedChoice:
                    case Constants.DatabaseColumn.ImageQuality:
                        Label choiceLabel = this.CreateLabel(styleProvider, control);
                        ComboBox choiceContent = this.CreateComboBox(mainWindow, control);
                        stackPanel = this.CreateStackPanel(styleProvider, choiceLabel, choiceContent);
                        break;
                    case Constants.DatabaseColumn.DateTime:
                        Label dateTimeLabel = this.CreateLabel(styleProvider, control);
                        DateTimePicker dateTimeContent = this.CreateDateTimePicker(control);
                        stackPanel = this.CreateStackPanel(styleProvider, dateTimeLabel, dateTimeContent);
                        break;
                    case Constants.DatabaseColumn.UtcOffset:
                        Label utcOffsetLabel = this.CreateLabel(styleProvider, control);
                        UtcOffsetUpDown utcOffsetContent = this.CreateUtcOffsetPicker(control);
                        stackPanel = this.CreateStackPanel(styleProvider, utcOffsetLabel, utcOffsetContent);
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

        private DateTimePicker CreateDateTimePicker(ControlRow control)
        {
            DateTimePicker dateTimePicker = new DateTimePicker();
            dateTimePicker.ToolTip = control.Tooltip;
            dateTimePicker.Width = control.Width;
            DataEntryHandler.Configure(dateTimePicker, Constants.ControlDefault.DateTimeValue.DateTime);
            return dateTimePicker;
        }

        // Returns a stack panel containing two controls
        // The stack panel ensures that controls are layed out as a single unit with certain spatial characteristcs 
        // i.e.,  a given height, right margin, where contents will not be broken durring (say) panel wrapping
        private StackPanel CreateStackPanel(DataEntryControls styleProvider, Control label, Control content)
        {
            StackPanel stackPanel = new StackPanel();
            stackPanel.Children.Add(label);
            stackPanel.Children.Add(content);

            Style style = styleProvider.FindResource(Constants.ControlStyle.StackPanelCodeBar) as Style;
            stackPanel.Style = style;
            return stackPanel;
        }

        private UtcOffsetUpDown CreateUtcOffsetPicker(ControlRow control)
        {
            UtcOffsetUpDown utcOffsetPicker = new UtcOffsetUpDown();
            utcOffsetPicker.ToolTip = control.Tooltip;
            utcOffsetPicker.Value = Constants.ControlDefault.DateTimeValue.Offset;
            utcOffsetPicker.Width = control.Width;
            return utcOffsetPicker;
        }

        private Label CreateLabel(DataEntryControls styleProvider, ControlRow control)
        {
            Label label = new Label();
            label.Content = control.Label;
            label.ToolTip = control.Tooltip;

            Style style = styleProvider.FindResource(ControlLabelStyle.LabelCodeBar.ToString()) as Style;
            label.Style = style;
            return label;
        }

        private TextBox CreateTextBox(DataEntryControls styleProvider, ControlRow control)
        {
            TextBox textBox = new TextBox();
            textBox.Text = control.DefaultValue;
            textBox.ToolTip = control.Tooltip;
            textBox.Width = control.Width;

            Style style = styleProvider.FindResource(ControlContentStyle.TextBoxCodeBar.ToString()) as Style;
            textBox.Style = style;
            return textBox;
        }

        private RadioButton CreateRadioButton(DataEntryControls styleProvider, ControlRow control)
        {
            RadioButton radioButton = new RadioButton();
            radioButton.GroupName = "A";
            radioButton.Content = control.Label;
            radioButton.ToolTip = control.Tooltip;

            Style style = styleProvider.FindResource(ControlLabelStyle.RadioButtonCodeBar.ToString()) as Style;
            radioButton.Style = style;
            return radioButton;
        }

        private CheckBox CreateFlag(DataEntryControls styleProvider, ControlRow control)
        {
            CheckBox checkBox = new CheckBox();
            checkBox.Visibility = Visibility.Visible;
            checkBox.ToolTip = control.Tooltip;

            Style style = styleProvider.FindResource(ControlContentStyle.FlagCodeBar.ToString()) as Style;
            checkBox.Style = style;
            return checkBox;
        }

        private ComboBox CreateComboBox(EditorWindow styleProvider, ControlRow control)
        {
            ComboBox comboBox = new ComboBox();
            comboBox.ToolTip = control.Tooltip;
            comboBox.Width = control.Width;
            foreach (string choice in control.GetChoices())
            {
                comboBox.Items.Add(choice);
            }
            comboBox.SelectedIndex = 0;

            Style style = styleProvider.FindResource(Constants.ControlStyle.ComboBoxCodeBar) as Style;
            comboBox.Style = style;
            return comboBox;
        }
    }
}
