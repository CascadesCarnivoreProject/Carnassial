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
        }

        public override bool ContentReadOnly
        {
            get { return this.ContentControl.IsReadOnly; }
            set { this.ContentControl.IsReadOnly = value; }
        }

        public DataEntryUtcOffset(ControlRow control, DataEntryControls styleProvider) :
            base(control, styleProvider, null, ControlLabelStyle.DefaultLabel)
        {
        }

        public override void SetContentAndTooltip(string value)
        {
            this.ContentControl.Value = DateTimeHandler.ParseDatabaseUtcOffsetString(value);
            this.ContentControl.ToolTip = value;
        }
    }
}
