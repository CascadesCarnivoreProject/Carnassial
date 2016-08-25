using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Controls;
using Timelapse.Database;

namespace Timelapse.Controls
{
    /// <summary>
    /// This class generates controls based upon the information passed into it from the data grid templateTable
    /// </summary>
    public partial class DataEntryControls : UserControl
    {
        public List<DataEntryControl> Controls { get; private set; }
        public Dictionary<string, DataEntryControl> ControlsByDataLabel { get; private set; }

        public DataEntryControls()
        {
            this.InitializeComponent();
            this.Controls = new List<DataEntryControl>();
            this.ControlsByDataLabel = new Dictionary<string, DataEntryControl>();
        }

        public void CreateControls(ImageDatabase database, DataEntryHandler dataEntryPropagator)
        {
            // Depending on how the user interacts with the file import process image set loading can be aborted after controls are generated and then
            // another image set loaded.  Any existing controls therefore need to be cleared.
            this.ControlGrid.Inlines.Clear();
            this.Controls.Clear();
            this.ControlsByDataLabel.Clear();

            foreach (ControlRow control in database.TemplateTable)
            {
                // no point in generating a control if it doesn't render in the UX
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
                else if (control.Type == Constants.Control.Flag || control.Type == Constants.DatabaseColumn.DeleteFlag)
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
                    Debug.Fail(String.Format("Unhandled control type {0}.", control.Type));
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