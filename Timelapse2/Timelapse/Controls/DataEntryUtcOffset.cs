using System.Windows.Controls;
using Timelapse.Database;
using Timelapse.Util;

namespace Timelapse.Controls
{
    public class DataEntryUtcOffset : DataEntryControl<UtcOffsetUpDown, Label>
    {
        public override string Content
        {
            get { return this.ContentControl.Text; }
            set { this.ContentControl.Value = DateTimeHandler.ParseDatabaseUtcOffsetString(value); }
        }

        public DataEntryUtcOffset(ControlRow control, DataEntryControls styleProvider) :
            base(control, styleProvider, null, ControlLabelStyle.LabelCodeBar)
        {
            // configure the various elements
            this.Container.ToolTip = "Enter a UTC offset";
        }
    }
}
