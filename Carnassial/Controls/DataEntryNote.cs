using Carnassial.Database;
using System.Windows.Controls;

namespace Carnassial.Controls
{
    // A note comprises a stack panel containing
    // - a label containing the descriptive label) 
    // - an editable textbox (containing the content) at the given width
    public class DataEntryNote : DataEntryControl<TextBox, Label>
    {
        /// <summary>Gets the content of the note</summary>
        public override string Content
        {
            get { return this.ContentControl.Text; }
        }

        public override bool ContentReadOnly
        {
            get { return this.ContentControl.IsReadOnly; }
            set { this.ContentControl.IsReadOnly = value; }
        }

        public DataEntryNote(ControlRow control, DataEntryControls styleProvider) : 
            base(control, styleProvider, ControlContentStyle.TextBoxCodeBar, ControlLabelStyle.LabelCodeBar)
        {
        }

        public override void SetContentAndTooltip(string value)
        {
            this.ContentControl.Text = value;
            this.ContentControl.ToolTip = value;
        }
    }
}
