using System;
using System.Collections.Generic;
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
                ControlRow control = new ControlRow(database.TemplateTable.Rows[row]);
                if (control.Visible == false)
                {
                    continue;
                }

                DataEntryControl controlToAdd;
                if (control.Type == Constants.DatabaseColumn.File ||
                    control.Type == Constants.DatabaseColumn.RelativePath ||
                    control.Type == Constants.DatabaseColumn.Folder ||
                    control.Type == Constants.DatabaseColumn.Date ||
                    control.Type == Constants.DatabaseColumn.Time ||
                    control.Type == Constants.Control.Note)
                {
                    DataEntryNote noteControl = new DataEntryNote(control.DataLabel, this);
                    noteControl.Label = control.Label;
                    noteControl.Width = control.TextBoxWidth;
                    if (control.Type == Constants.DatabaseColumn.Folder || 
                        control.Type == Constants.DatabaseColumn.RelativePath ||
                        control.Type == Constants.DatabaseColumn.File)
                    {
                        // File name and path aren't editable by the user 
                        noteControl.ReadOnly = true;
                    }
                    controlToAdd = noteControl;
                }
                else if (control.Type == Constants.Control.Flag || control.Type == Constants.Control.DeleteFlag)
                {
                    DataEntryFlag flagControl = new DataEntryFlag(control.DataLabel, this);
                    flagControl.Label = control.Label;
                    flagControl.Width = control.TextBoxWidth;
                    controlToAdd = flagControl;
                }
                else if (control.Type == Constants.Control.Counter)
                {
                    DataEntryCounter counterControl = new DataEntryCounter(control.DataLabel, this);
                    counterControl.Label = control.Label;
                    counterControl.Width = control.TextBoxWidth;
                    controlToAdd = counterControl;
                }
                else if (control.Type == Constants.Control.FixedChoice || control.Type == Constants.DatabaseColumn.ImageQuality)
                {
                    DataEntryChoice choiceControl = new DataEntryChoice(control.DataLabel, this, control.List);
                    choiceControl.Label = control.Label;
                    choiceControl.Width = control.TextBoxWidth;
                    controlToAdd = choiceControl;
                }
                else
                {
                    Debug.Assert(false, String.Format("Unhandled control type {0}.", control.Type));
                    continue;
                }

                controlToAdd.Content = control.DefaultValue;
                controlToAdd.Copyable = control.Copyable;
                controlToAdd.Tooltip = control.Tooltip;
                this.ControlGrid.Inlines.Add(controlToAdd.Container);
                this.Controls.Add(controlToAdd);
                this.ControlsByDataLabel.Add(control.DataLabel, controlToAdd);
            }

            dataEntryPropagator.SetDataEntryCallbacks(this.ControlsByDataLabel);
        }

        public void AddButton(Control button)
        {
            this.ButtonLocation.Child = button;
        }
    }
}