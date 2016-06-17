using System;
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
    public partial class DataEntryControls : UserControl
    {
        // Given a key, return its associated control
        public Dictionary<string, DataEntryControl> ControlsByDataLabel { get; private set; }
        public List<DataEntryControl> Controls { get; private set; } // list of all our counter controls

        public DataEntryControls()
        {
            this.InitializeComponent();
            this.ControlsByDataLabel = new Dictionary<string, DataEntryControl>();
            this.Controls = new List<DataEntryControl>();
        }

        public void Generate(ImageDatabase database, DataEntryHandler dataEntryPropagator)
        {
            for (int row = 0; row < database.TemplateTable.Rows.Count; row++)
            {
                // no point in generating a control if it doesn't render in the UX
                DataRow dataRow = database.TemplateTable.Rows[row];
                string visiblityAsString = dataRow.GetStringField(Constants.Control.Visible);
                bool visible = String.Equals(Boolean.TrueString, visiblityAsString, StringComparison.OrdinalIgnoreCase) ? true : false;
                if (visible == false)
                {
                    continue;
                }

                // get the values for the control
                string copyableAsString = dataRow.GetStringField(Constants.Control.Copyable);
                bool copyable = String.Equals(Boolean.TrueString, copyableAsString, StringComparison.OrdinalIgnoreCase) ? true : false;
                string dataLabel = dataRow.GetStringField(Constants.Control.DataLabel);
                string defaultValue = dataRow.GetStringField(Constants.Control.DefaultValue);
                long id = (long)dataRow[Constants.DatabaseColumn.ID]; // TODO Need to use this ID to pass between controls and data
                string label = dataRow.GetStringField(Constants.Control.Label);
                string list = dataRow.GetStringField(Constants.Control.List);
                string tooltip = dataRow.GetStringField(Constants.Control.Tooltip);
                string controlType = dataRow.GetStringField(Constants.Control.Type);
                string widthAsString = dataRow.GetStringField(Constants.Control.TextBoxWidth);
                int width = (widthAsString == String.Empty) ? 0 : Int32.Parse(widthAsString);

                DataEntryControl controlToAdd;
                if (controlType == Constants.DatabaseColumn.File ||
                    controlType == Constants.DatabaseColumn.RelativePath ||
                    controlType == Constants.DatabaseColumn.Folder ||
                    controlType == Constants.DatabaseColumn.Date ||
                    controlType == Constants.DatabaseColumn.Time ||
                    controlType == Constants.Control.Note)
                {
                    DataEntryNote noteControl = new DataEntryNote(dataLabel, this);
                    noteControl.Label = label;
                    noteControl.Width = width;
                    if (controlType == Constants.DatabaseColumn.Folder || 
                        controlType == Constants.DatabaseColumn.RelativePath ||
                        controlType == Constants.DatabaseColumn.File)
                    {
                        // File name and path aren't editable by the user 
                        noteControl.ReadOnly = true;
                    }
                    controlToAdd = noteControl;
                }
                else if (controlType == Constants.Control.Flag || controlType == Constants.Control.DeleteFlag)
                {
                    DataEntryFlag flagControl = new DataEntryFlag(dataLabel, this);
                    flagControl.Label = label;
                    flagControl.Width = width;
                    controlToAdd = flagControl;
                }
                else if (controlType == Constants.Control.Counter)
                {
                    DataEntryCounter counterControl = new DataEntryCounter(dataLabel, this);
                    counterControl.Label = label;
                    counterControl.Width = width;
                    controlToAdd = counterControl;
                }
                else if (controlType == Constants.Control.FixedChoice || controlType == Constants.DatabaseColumn.ImageQuality)
                {
                    DataEntryChoice choiceControl = new DataEntryChoice(dataLabel, this, list);
                    choiceControl.Label = label;
                    choiceControl.Width = width;
                    controlToAdd = choiceControl;
                }
                else
                {
                    Debug.Assert(false, String.Format("Unhandled control type {0}.", controlType));
                    continue;
                }

                controlToAdd.Content = defaultValue;
                controlToAdd.Copyable = copyable;
                controlToAdd.Tooltip = tooltip;
                this.ControlGrid.Inlines.Add(controlToAdd.Container);
                this.Controls.Add(controlToAdd);
                this.ControlsByDataLabel.Add(dataLabel, controlToAdd);
            }

            dataEntryPropagator.SetDataEntryCallbacks(this.ControlsByDataLabel);
        }

        public void AddButton(Control button)
        {
            this.ButtonLocation.Child = button;
        }
    }
}