using Carnassial.Database;
using Carnassial.Util;
using System.Windows.Controls;

namespace Carnassial.Controls
{
    public class DataEntryDateTime : DataEntryControl<DateTimeOffsetPicker, Label>
    {
        public override string Content
        {
            get { return DateTimeHandler.ToDatabaseDateTimeString(this.ContentControl.Value); }
        }

        public override bool ContentReadOnly
        {
            get { return !this.ContentControl.IsEnabled; }
            set { this.ContentControl.IsEnabled = !value; }
        }

        public DataEntryDateTime(ControlRow control, DataEntryControls styleProvider) : 
            base(control, styleProvider, null, ControlLabelStyle.DefaultLabel)
        {
            this.Container.ToolTip = "Enter a date/time";
        }

        public override void SetContentAndTooltip(string value)
        {
            this.ContentControl.DateTimeDisplay.Text = value;
            this.ContentControl.DateTimeDisplay.ToolTip = value;
        }
    }
}
