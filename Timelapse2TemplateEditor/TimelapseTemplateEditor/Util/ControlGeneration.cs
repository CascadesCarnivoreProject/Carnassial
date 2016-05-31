using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Timelapse;

namespace TimelapseTemplateEditor.Util
{
    // This static class generates controls in the provided wrap panel based upon the information in the data grid templateTable
    // It is meant to roughly approximate what the controls will look like in the user interface
    internal class ControlGeneration
    {
        public static void GenerateControls(Window win, WrapPanel wp, DataTable tempTable)
        {
            const string EXAMPLE_DATE = "28-Dec-2014";
            const string EXAMPLE_TIME = "04:00 PM";

            wp.Children.Clear();

            for (int i = 0; i < tempTable.Rows.Count; i++)
            {
                DataRow row = tempTable.Rows[i];
                string type = row[Constants.DatabaseColumn.Type].ToString();
                string defaultValue = row[Constants.Control.DefaultValue].ToString();
                string label = row[Constants.Control.Label].ToString();
                string datalabel = row[Constants.Control.DataLabel].ToString();
                string tooltip = row[Constants.Control.Tooltip].ToString();
                string width = row[Constants.Control.TextBoxWidth].ToString();
                string visiblity = row[Constants.Control.Visible].ToString();
                string list = row[Constants.Control.List].ToString();

                int iwidth = (width == String.Empty) ? 0 : Convert.ToInt32(width);

                bool bvisiblity = (visiblity == "true" || visiblity == "True") ? true : false;

                StackPanel sp = null;

                if (type == Constants.DatabaseColumn.Date && defaultValue == String.Empty)
                {
                    defaultValue = EXAMPLE_DATE;
                }
                else if (type == Constants.DatabaseColumn.Time && defaultValue == String.Empty)
                {
                    defaultValue = EXAMPLE_TIME;
                }

                if (type == Constants.DatabaseColumn.File || type == Constants.DatabaseColumn.Folder || type == Constants.DatabaseColumn.Date || type == Constants.DatabaseColumn.Time || type == Constants.Control.Note)
                {
                    Label labelctl = CreateLabel(win, label, tooltip);
                    TextBox txtbox = CreateTextBox(win, defaultValue, tooltip, iwidth);
                    sp = CreateStackPanel(win, labelctl, txtbox);
                }
                else if (type == Constants.Control.Counter)
                {
                    RadioButton rb = CreateRadioButton(win, label, tooltip);
                    TextBox txtbox = CreateTextBox(win, defaultValue, tooltip, iwidth);
                    sp = CreateStackPanel(win, rb, txtbox);
                }
                else if (type == Constants.Control.Flag || type == Constants.DatabaseColumn.DeleteFlag)
                {
                    Label labelctl = CreateLabel(win, label, tooltip);
                    CheckBox flag = CreateFlag(win, String.Empty, tooltip);
                    flag.IsChecked = (defaultValue == "true") ? true : false;
                    sp = CreateStackPanel(win, labelctl, flag);
                }
                else if (type == Constants.Control.FixedChoice || type == Constants.DatabaseColumn.ImageQuality)
                {
                    Label labelctl = CreateLabel(win, label, tooltip);
                    ComboBox combobox = CreateComboBox(win, list, tooltip, iwidth);
                    sp = CreateStackPanel(win, labelctl, combobox);
                }

                if (sp != null)
                {
                    sp.Tag = datalabel;
                    wp.Children.Add(sp);
                }
                if (true != bvisiblity)
                {
                    sp.Visibility = Visibility.Collapsed;
                }
            }
        }

        // Returns a stack panel containing two controls
        // The stack panel ensures that controls are layed out as a single unit with certain spatial characteristcs 
        // i.e.,  a given height, right margin, where contents will not be broken durring (say) panel wrapping
        private static StackPanel CreateStackPanel(Window win, Control control1, Control control2)
        {
            StackPanel sp = new StackPanel();

            // Add controls to the stack panel
            sp.Children.Add(control1);
            if (control2 != null)
            {
                sp.Children.Add(control2);
            }

            // Its look is dictated by this style
            Style style = win.FindResource("StackPanelCodeBar") as Style;
            sp.Style = style;
            return sp;
        }

        private static Label CreateLabel(Window win, string labeltxt, string tooltiptxt)
        {
            Label label = new Label();

            // Configure it
            label.Content = labeltxt;
            label.ToolTip = tooltiptxt;

            // Its look is dictated by this style
            Style style = win.FindResource("LabelCodeBar") as Style;
            label.Style = style;

            return label;
        }

        private static TextBox CreateTextBox(Window win, string textboxtxt, string tooltiptxt, int width)
        {
            TextBox txtbox = new TextBox();

            // Configure the Textbox
            txtbox.Width = width;
            txtbox.Text = textboxtxt;
            txtbox.ToolTip = tooltiptxt;

            // Its look is dictated by this style
            Style style = win.FindResource("TextBoxCodeBar") as Style;
            txtbox.Style = style;

            return txtbox;
        }

        private static RadioButton CreateRadioButton(Window win, string labeltxt, string tooltiptxt)
        {
            RadioButton radiobtn = new RadioButton();

            // Configure the Radio Button
            radiobtn.GroupName = "A";
            radiobtn.Content = labeltxt;
            radiobtn.ToolTip = tooltiptxt;

            // Its look is dictated by this style
            Style style = win.FindResource("RadioButtonCodeBar") as Style;
            radiobtn.Style = style;

            return radiobtn;
        }

        private static CheckBox CreateFlag(Window win, string labeltxt, string tooltiptxt)
        {
            CheckBox flagbtn = new CheckBox();

            // Configure the Checkbox 
            flagbtn.Content = labeltxt;
            flagbtn.Visibility = Visibility.Visible;
            flagbtn.ToolTip = tooltiptxt;

            // Its look is dictated by this style
            Style style = win.FindResource("FlagCodeBar") as Style;
            flagbtn.Style = style;

            return flagbtn;
        }

        private static ComboBox CreateComboBox(Window win, string list, string tooltiptxt, int width)
        {
            ComboBox combobox = new ComboBox();

            // Configure the ComboBox
            combobox.ToolTip = tooltiptxt;
            combobox.Width = width;
            List<string> result = list.Split(new char[] { '|' }).ToList();
            foreach (string str in result)
            {
                combobox.Items.Add(str.Trim());
            }
            combobox.SelectedIndex = 0;

            // Its look is dictated by this style
            Style style = win.FindResource("ComboBoxCodeBar") as Style;
            combobox.Style = style;
            return combobox;
        }
    }
}
