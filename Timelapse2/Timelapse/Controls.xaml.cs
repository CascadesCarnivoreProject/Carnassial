using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Windows.Controls;
using Timelapse.Database;
using Timelapse.Util;

namespace Timelapse
{
    /// <summary>
    /// This class generates controls based upon the information passed into it from the data grid templateTable
    /// </summary>
    public partial class Controls : UserControl
    {
        // Given a key, return its associated control
        public Dictionary<string, DataEntryControl> ControlFromDataLabel { get; private set; }
        public List<DataEntryCounter> CounterControls { get; private set; } // list of all our counter controls
        // The wrap panel will contain all our controls. If we want to reparent things, we do it by reparenting the wrap panel
        public PropagateControl Propagate { get; private set; }

        public Controls()
        {
            this.InitializeComponent();
            this.ControlFromDataLabel = new Dictionary<string, DataEntryControl>();
            this.CounterControls = new List<DataEntryCounter>();
        }

        public void GenerateControls(ImageDatabase database)
        {
            const string EXAMPLE_DATE = "28-Dec-2014";
            const string EXAMPLE_TIME = "04:00 PM";

            this.Propagate = new PropagateControl(database);

            DataTable sortedControlTable = database.TemplateGetSortedByControls();
            for (int i = 0; i < sortedControlTable.Rows.Count; i++)
            {
                // Get the values for each control
                DataRow dataRow = sortedControlTable.Rows[i];
                string copyableAsString = dataRow[Constants.Control.Copyable].ToString();
                bool copyable = ("true" == copyableAsString.ToLower()) ? true : false;
                string dataLabel = (string)dataRow[Constants.Control.DataLabel];
                string defaultValue = dataRow[Constants.Control.DefaultValue].ToString();
                int id = Convert.ToInt32(dataRow[Constants.Database.ID].ToString()); // TODO Need to use this ID to pass between controls and data
                string label = dataRow[Constants.Control.Label].ToString();
                string list = dataRow[Constants.Control.List].ToString();
                string tooltip = dataRow[Constants.Control.Tooltop].ToString();
                string type = dataRow[Constants.Database.Type].ToString();
                string visiblityAsString = dataRow[Constants.Control.Visible].ToString();
                bool visiblity = ("true" == visiblityAsString.ToLower()) ? true : false;
                string widthAsString = dataRow[Constants.Control.TextBoxWidth].ToString();
                int width = (widthAsString == String.Empty) ? 0 : Convert.ToInt32(widthAsString);

                if (type == Constants.DatabaseElement.Date && defaultValue == String.Empty)
                {
                    defaultValue = EXAMPLE_DATE;
                }
                else if (type == Constants.DatabaseElement.Time && defaultValue == String.Empty)
                {
                    defaultValue = EXAMPLE_TIME;
                }

                if (type == Constants.DatabaseElement.File ||
                    type == Constants.DatabaseElement.Folder ||
                    type == Constants.DatabaseElement.Date ||
                    type == Constants.DatabaseElement.Time ||
                    type == Constants.DatabaseElement.Note)
                {
                    bool createContextMenu = (type == Constants.DatabaseElement.File) ? false : true;
                    DataEntryNote myNote = new DataEntryNote(dataLabel, this, createContextMenu);
                    myNote.Label = label;
                    myNote.Tooltip = tooltip;
                    myNote.Width = width;
                    myNote.Visible = visiblity;
                    myNote.Content = defaultValue;
                    myNote.ReadOnly = (type == Constants.DatabaseElement.Folder || type == Constants.DatabaseElement.File) ? true : false; // File and Folder Notes are read only i.e., non-editable by the user 
                    myNote.Copyable = copyable;
                    this.ControlGrid.Inlines.Add(myNote.Container);
                    this.ControlFromDataLabel.Add(dataLabel, myNote);
                }
                else if (type == Constants.DatabaseElement.Flag || type == Constants.DatabaseElement.DeleteFlag)
                {
                    DataEntryFlag myFlag = new DataEntryFlag(dataLabel, this, true);
                    myFlag.Label = label;
                    myFlag.Tooltip = tooltip;
                    myFlag.Width = width;
                    myFlag.Visible = visiblity;
                    myFlag.Content = defaultValue;
                    myFlag.ReadOnly = false; // Flags are editable by the user 
                    myFlag.Copyable = copyable;
                    this.ControlGrid.Inlines.Add(myFlag.Container);
                    this.ControlFromDataLabel.Add(dataLabel, myFlag);
                }
                else if (type == Constants.DatabaseElement.Counter)
                {
                    DataEntryCounter myCounter = new DataEntryCounter(dataLabel, this, true);
                    myCounter.Label = label;
                    myCounter.Tooltip = tooltip;
                    myCounter.Width = width;
                    myCounter.Visible = visiblity;
                    myCounter.Content = defaultValue;
                    myCounter.ReadOnly = false; // Couonters are editable by the user 
                    myCounter.Copyable = copyable;
                    this.ControlGrid.Inlines.Add(myCounter.Container);
                    this.ControlFromDataLabel.Add(dataLabel, myCounter);
                }
                else if (type == Constants.DatabaseElement.FixedChoice || type == Constants.DatabaseElement.ImageQuality)
                {
                    DataEntryChoice myFixedChoice = new DataEntryChoice(dataLabel, this, true, list);
                    myFixedChoice.Label = label;
                    myFixedChoice.Tooltip = tooltip;
                    myFixedChoice.Width = width;
                    myFixedChoice.Visible = visiblity;
                    myFixedChoice.Content = defaultValue;
                    myFixedChoice.ReadOnly = false; // Fixed choices are editable (by selecting a menu) by the user 
                    myFixedChoice.Copyable = copyable;
                    this.ControlGrid.Inlines.Add(myFixedChoice.Container);
                    this.ControlFromDataLabel.Add(dataLabel, myFixedChoice);
                }
                else
                {
                    Debug.Assert(false, String.Format("Unhandled control type {0}.", type));
                }
            }
        }

        public void AddButton(Control button)
        {
            this.ButtonLocation.Child = button;
        }
    }
}