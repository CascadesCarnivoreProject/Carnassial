using Carnassial.Database;
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

        public DataEntryNote(ControlRow control, List<string> autocompletionsFromDatabase, DataEntryControls styleProvider) : 
            base(control, styleProvider, ControlContentStyle.NoteCounterTextBox, ControlLabelStyle.DefaultLabel)
        {
            this.autocompletionsFromList = control.GetChoices();
            if (this.autocompletionsFromList.Contains(control.DefaultValue) == false)
            {
                this.autocompletionsFromList.Add(control.DefaultValue);
            }
            this.SetAutocompletions(autocompletionsFromDatabase);
            this.ContentChanged = false;
        }

        public void SetAutocompletions(List<string> autocompletionsFromDatabase)
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

        public override void SetContentAndTooltip(string value)
        {
            this.ContentChanged = this.ContentControl.Text != value;
            this.ContentControl.Text = value;
            this.ContentControl.ToolTip = value;
        }
    }
}
