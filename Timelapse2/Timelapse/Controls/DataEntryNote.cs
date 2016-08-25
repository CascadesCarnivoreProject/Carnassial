using System.Windows.Controls;
using Timelapse.Database;

namespace Timelapse.Controls
{
    // A note comprises a stack panel containing
    // - a label containing the descriptive label) 
    // - an editable textbox (containing the content) at the given width
    public class DataEntryNote : DataEntryControl<TextBox, Label>
    {
        /// <summary>Gets or sets the content of the note</summary>
        public override string Content
        {
            get { return this.ContentControl.Text; }
            set { this.ContentControl.Text = value; }
        }

        public DataEntryNote(ControlRow control, DataEntryControls styleProvider) : 
            base(control, styleProvider, ControlContentStyle.TextBoxCodeBar, ControlLabelStyle.LabelCodeBar)
        {
            // Now configure the various elements
            this.Container.ToolTip = "Type in text";
        }
    }
}
