using System.Windows.Controls;

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

        public DataEntryNote(string dataLabel, DataEntryControls styleProvider) : 
            base(dataLabel, styleProvider, ControlContentStyle.TextBoxCodeBar, ControlLabelStyle.LabelCodeBar)
        {
            // Now configure the various elements
            this.Container.ToolTip = "Type in text";
        }
    }
}
