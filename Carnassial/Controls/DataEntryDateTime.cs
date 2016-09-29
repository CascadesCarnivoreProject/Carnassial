using Carnassial.Database;
using System.Windows.Controls;
using Xceed.Wpf.Toolkit;

namespace Carnassial.Controls
{
    public class DataEntryDateTime : DataEntryControl<DateTimePicker, Label>
    {
        public override string Content
        {
            get { return this.ContentControl.Text; }
        }

        public override bool ContentReadOnly
        {
            get { return this.ContentControl.IsReadOnly; }
            set { this.ContentControl.IsReadOnly = value; }
        }

        public DataEntryDateTime(ControlRow control, DataEntryControls styleProvider) : 
            base(control, styleProvider, null, ControlLabelStyle.LabelCodeBar)
        {
            // configure the various elements
            this.Container.ToolTip = "Enter a date/time";

            DataEntryHandler.Configure(this.ContentControl, null);
        }

        public override void SetContentAndTooltip(string value)
        {
            this.ContentControl.Text = value;
            this.ContentControl.ToolTip = value;
        }
    }
}
