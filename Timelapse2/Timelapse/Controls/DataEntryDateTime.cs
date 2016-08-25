using System.Windows.Controls;
using Timelapse.Database;
using Xceed.Wpf.Toolkit;

namespace Timelapse.Controls
{
    public class DataEntryDateTime : DataEntryControl<DateTimePicker, Label>
    {
        public override string Content
        {
            get { return this.ContentControl.Text; }
            set { this.ContentControl.Text = value; }
        }

        public DataEntryNote DateControl { get; set; }
        public DataEntryNote TimeControl { get; set; }

        public DataEntryDateTime(ControlRow control, DataEntryControls styleProvider) : 
            base(control, styleProvider, null, ControlLabelStyle.LabelCodeBar)
        {
            // configure the various elements
            this.Container.ToolTip = "Enter a date/time";

            DataEntryHandler.Configure(this.ContentControl, null);
        }
    }
}
