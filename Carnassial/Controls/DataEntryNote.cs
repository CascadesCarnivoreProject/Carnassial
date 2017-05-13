﻿using Carnassial.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace Carnassial.Controls
{
    // A note lays out as a stack panel containing
    // - a label containing the descriptive label) 
    // - an editable textbox (containing the content) at the given width
    public class DataEntryNote : DataEntryControl<AutocompleteTextBox, Label>
    {
        private List<string> autocompletionsFromList;

        /// <summary>Gets the content of the note</summary>
        public override string Content
        {
            get { return this.ContentControl.Text; }
        }

        public bool ContentChanged { get; set; }

        public override bool ContentReadOnly
        {
            get { return this.ContentControl.IsReadOnly; }
            set { this.ContentControl.IsReadOnly = value; }
        }

        public DataEntryNote(ControlRow control, List<string> autocompletionsFromDatabase, bool readOnly, DataEntryControls styleProvider) : 
            base(control, styleProvider, ControlContentStyle.NoteCounterTextBox, ControlLabelStyle.DefaultLabel, readOnly)
        {
            this.SetChoices(control.GetChoices());
            this.autocompletionsFromList = control.GetChoices();
            if (this.autocompletionsFromList.Contains(control.DefaultValue) == false)
            {
                this.autocompletionsFromList.Add(control.DefaultValue);
            }
            this.MergeAutocompletions(autocompletionsFromDatabase);
            this.ContentChanged = false;
        }

        public override List<string> GetChoices()
        {
            return this.ContentControl.Autocompletions;
        }

        public void MergeAutocompletions(List<string> autocompletionsFromDatabase)
        {
            if (autocompletionsFromDatabase == null)
            {
                this.ContentControl.Autocompletions = this.autocompletionsFromList;
            }
            else
            {
                this.ContentControl.Autocompletions = this.autocompletionsFromList.Union(autocompletionsFromDatabase).Distinct().ToList();
            }
        }

        public override void SetChoices(List<string> choices)
        {
            this.autocompletionsFromList = choices;
            this.MergeAutocompletions(null);
        }

        public override void SetValue(object valueAsObject)
        {
            string valueAsString;
            if ((valueAsObject is string) || (valueAsObject == null))
            {
                valueAsString = (string)valueAsObject;
            }
            else
            {
                if (valueAsObject == null)
                {
                    throw new ArgumentNullException(nameof(valueAsObject));
                }
                throw new ArgumentOutOfRangeException("valueAsObject", String.Format("Unsupported value type {0}.", valueAsObject.GetType()));
            }

            this.ContentChanged = this.ContentControl.Text != valueAsString;
            this.ContentControl.Text = valueAsString;
            this.ContentControl.ToolTip = valueAsString;
        }
    }
}
