using Carnassial.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Carnassial.Control
{
    /// <summary>
    /// This class generates data entry controls based upon the controls table.
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

        public void Clear()
        {
            this.Controls.Clear();
            this.ControlsByDataLabel.Clear();
            this.ControlStack.Children.Clear();
            this.ControlStack.RowDefinitions.Clear();
        }

        public void CreateControls(TemplateDatabase database, DataEntryHandler dataEntryPropagator, Func<string, List<string>> getNoteAutocompletions)
        {
            // Depending on how the user interacts with the file import process image set loading can be aborted after controls are generated and then
            // another image set loaded.  Any existing controls therefore need to be cleared.
            this.Clear();

            DataEntryDateTimeOffset dateTimeControl = null;
            bool showUtcOffset = false;
            List<DataEntryControl> visibleControls = new List<DataEntryControl>();
            foreach (ControlRow control in database.Controls)
            {
                // no point in generating a control if it doesn't render in the UX
                if (control.Visible == false)
                {
                    continue;
                }

                if (control.Type == ControlType.DateTime)
                {
                    dateTimeControl = new DataEntryDateTimeOffset(control, this);
                    visibleControls.Add(dateTimeControl);
                }
                else if (control.Type == ControlType.Note)
                {
                    // standard controls rendering as notes aren't editable by the user 
                    List<string> autocompletions = null;
                    bool readOnly = control.IsFilePathComponent();
                    if (readOnly == false)
                    {
                        autocompletions = new List<string>(getNoteAutocompletions.Invoke(control.DataLabel));
                    }
                    DataEntryNote noteControl = new DataEntryNote(control, autocompletions, readOnly, this);
                    visibleControls.Add(noteControl);
                }
                else if (control.Type == ControlType.Flag)
                {
                    DataEntryFlag flagControl = new DataEntryFlag(control, this);
                    visibleControls.Add(flagControl);
                }
                else if (control.Type == ControlType.Counter)
                {
                    DataEntryCounter counterControl = new DataEntryCounter(control, this);
                    visibleControls.Add(counterControl);
                }
                else if (control.Type == ControlType.FixedChoice)
                {
                    DataEntryChoice choiceControl = new DataEntryChoice(control, this);
                    visibleControls.Add(choiceControl);
                }
                else if (control.Type == ControlType.UtcOffset)
                {
                    showUtcOffset = true;
                }
                else
                {
                    Debug.Fail(String.Format("Unhandled control type {0}.", control.Type));
                    continue;
                }
            }

            if (showUtcOffset && (dateTimeControl != null))
            {
                dateTimeControl.ShowUtcOffset();
            }

            for (int controlIndex = 0; controlIndex < visibleControls.Count; ++controlIndex)
            {
                DataEntryControl control = visibleControls[controlIndex];
                control.Container.SetValue(Grid.RowProperty, controlIndex);

                this.Controls.Add(control);
                this.ControlsByDataLabel.Add(control.DataLabel, control);

                this.ControlStack.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
                this.ControlStack.Children.Add(control.Container);
            }

            if (dataEntryPropagator != null)
            {
                dataEntryPropagator.SetDataEntryCallbacks(this.Controls);
            }
        }

        public void SetDataContext(ImageRow file)
        {
            foreach (DataEntryControl control in this.Controls)
            {
                control.DataContext = file;
            }
        }

        public bool TryFindDataEntryControl(Point hitLocation, out DataEntryControl control)
        {
            control = null;
            HitTestResult hitTest = VisualTreeHelper.HitTest(this, hitLocation);
            if (hitTest == null)
            {
                return false;
            }

            DependencyObject hitObject = hitTest.VisualHit;
            while (hitObject is StackPanel == false)
            {
                if (hitObject == null)
                {
                    return false;
                }

                FrameworkElement parent = (FrameworkElement)((FrameworkElement)hitObject).Parent;
                FrameworkElement templatedParent = (FrameworkElement)((FrameworkElement)hitObject).TemplatedParent;
                if (parent != null)
                {
                    hitObject = parent;
                }
                else if (templatedParent != null)
                {
                    hitObject = templatedParent;
                }
                else
                {
                    return false;
                }
            }

            control = this.Controls.SingleOrDefault(dataEntryControl => dataEntryControl.Container == hitObject);
            return control != null;
        }
    }
}