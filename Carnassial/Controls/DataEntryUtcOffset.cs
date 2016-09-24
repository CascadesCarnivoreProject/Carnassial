using Carnassial.Database;
using Carnassial.Util;
using System.Windows.Controls;

namespace Carnassial.Controls
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
