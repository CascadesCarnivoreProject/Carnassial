using Carnassial.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Controls;

namespace Carnassial.Controls
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

        public void CreateControls(FileDatabase database, DataEntryHandler dataEntryPropagator)
        {
            // Depending on how the user interacts with the file import process image set loading can be aborted after controls are generated and then
            // another image set loaded.  Any existing controls therefore need to be cleared.
            this.ControlGrid.Children.Clear();
            this.Controls.Clear();
            this.ControlsByDataLabel.Clear();

            DataEntryDateTime dateTimeControl = null;
            DataEntryUtcOffset utcOffsetControl = null;
            List<DataEntryControl> visibleControls = new List<DataEntryControl>();
            foreach (ControlRow control in database.Controls)
            {
                // no point in generating a control if it doesn't render in the UX
                if (control.Visible == false)
                {
                    continue;
                }

                if (control.Type == Constant.DatabaseColumn.DateTime)
                {
                    dateTimeControl = new DataEntryDateTime(control, this);
                    visibleControls.Add(dateTimeControl);
                }
                else if (control.Type == Constant.DatabaseColumn.File ||
                         control.Type == Constant.DatabaseColumn.RelativePath ||
                         control.Type == Constant.Control.Note)
                {
                    // standard controls rendering as notes aren't editable by the user 
                    List<string> autocompletions = null;
                    bool readOnly = control.Type != Constant.Control.Note;
                    if (readOnly == false)
                    {
                        autocompletions = new List<string>(database.GetDistinctValuesInFileDataColumn(control.DataLabel));
                    }
                    DataEntryNote noteControl = new DataEntryNote(control, autocompletions, this);
                    noteControl.ContentReadOnly = readOnly;
                    visibleControls.Add(noteControl);
                }
                else if (control.Type == Constant.Control.Flag || control.Type == Constant.DatabaseColumn.DeleteFlag)
                {
                    DataEntryFlag flagControl = new DataEntryFlag(control, this);
                    visibleControls.Add(flagControl);
                }
                else if (control.Type == Constant.Control.Counter)
                {
                    DataEntryCounter counterControl = new DataEntryCounter(control, this);
                    visibleControls.Add(counterControl);
                }
                else if (control.Type == Constant.Control.FixedChoice || control.Type == Constant.DatabaseColumn.ImageQuality)
                {
                    DataEntryChoice choiceControl = new DataEntryChoice(control, this);
                    visibleControls.Add(choiceControl);
                }
                else if (control.Type == Constant.DatabaseColumn.UtcOffset)
                {
                    utcOffsetControl = new DataEntryUtcOffset(control, this);
                    visibleControls.Add(utcOffsetControl);
                }
                else
                {
                    Debug.Fail(String.Format("Unhandled control type {0}.", control.Type));
                    continue;
                }
            }

            if ((dateTimeControl != null) && (utcOffsetControl != null))
            {
                dateTimeControl.ShowUtcOffset();
                visibleControls.Remove(utcOffsetControl);
            }

            foreach (DataEntryControl control in visibleControls)
            {
                this.ControlGrid.Children.Add(control.Container);
                this.Controls.Add(control);
                this.ControlsByDataLabel.Add(control.DataLabel, control);
            }

            dataEntryPropagator.SetDataEntryCallbacks(this.ControlsByDataLabel);
        }
    }
}