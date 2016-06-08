using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Data;
using Timelapse.Database;

namespace Timelapse.UnitTests
{
    internal class ControlExpectations
    {
        public long ControlOrder { get; set; }

        public bool Copyable { get; set; }

        public string DataLabel { get; set; }

        public string DefaultValue { get; set; }

        public long ID { get; set; }

        public string Label { get; set; }

        public string List { get; set; }

        public long SpreadsheetOrder { get; set; }

        public string TextBoxWidth { get; set; }

        public string Tooltip { get; set; }

        public string Type { get; set; }

        public bool Visible { get; set; }

        public static ControlExpectations CreateCounter(string dataLabel, long id)
        {
            return new ControlExpectations()
            {
                ControlOrder = id,
                Copyable = false,
                DataLabel = dataLabel,
                DefaultValue = Constants.ControlDefault.CounterValue,
                ID = id,
                Label = dataLabel,
                List = String.Empty,
                SpreadsheetOrder = id,
                TextBoxWidth = Constants.ControlDefault.CounterWidth,
                Tooltip = Constants.ControlDefault.CounterTooltip,
                Type = Constants.Control.Counter,
                Visible = true
            };
        }

        public static ControlExpectations CreateChoice(string dataLabel, long id)
        {
            return new ControlExpectations()
            {
                ControlOrder = id,
                Copyable = true,
                DataLabel = dataLabel,
                DefaultValue = Constants.ControlDefault.Value,
                ID = id,
                Label = dataLabel,
                List = String.Empty,
                SpreadsheetOrder = id,
                TextBoxWidth = Constants.ControlDefault.FixedChoiceWidth,
                Tooltip = Constants.ControlDefault.FixedChoiceTooltip,
                Type = Constants.Control.FixedChoice,
                Visible = true
            };
        }

        public static ControlExpectations CreateFlag(string dataLabel, long id)
        {
            return new ControlExpectations()
            {
                ControlOrder = id,
                Copyable = true,
                DataLabel = dataLabel,
                DefaultValue = Constants.ControlDefault.FlagValue,
                ID = id,
                Label = dataLabel,
                List = String.Empty,
                SpreadsheetOrder = id,
                TextBoxWidth = Constants.ControlDefault.FlagWidth,
                Tooltip = Constants.ControlDefault.FlagTooltip,
                Type = Constants.Control.Flag,
                Visible = true
            };
        }

        public static ControlExpectations CreateNote(string dataLabel, long id)
        {
            return new ControlExpectations()
            {
                ControlOrder = id,
                Copyable = true,
                DataLabel = dataLabel,
                DefaultValue = Constants.ControlDefault.Value,
                ID = id,
                Label = dataLabel,
                List = String.Empty,
                SpreadsheetOrder = id,
                TextBoxWidth = Constants.ControlDefault.NoteWidth,
                Tooltip = Constants.ControlDefault.NoteTooltip,
                Type = Constants.Control.Note,
                Visible = true
            };
        }

        public void Verify(DataRow control)
        {
            Assert.IsTrue((long)control[Constants.Control.ControlOrder] == this.ControlOrder, "{0}: Expected ControlOrder '{1}' but found '{2}'.", this.DataLabel, this.ControlOrder, control[Constants.Control.ControlOrder]);
            Assert.IsTrue(String.Equals(control.GetStringField(Constants.Control.Copyable), this.Copyable.ToString(), StringComparison.OrdinalIgnoreCase), "{0}: Expected Copyable '{1}' but found '{2}'.", this.DataLabel, this.Copyable, control.GetStringField(Constants.Control.Copyable));
            Assert.IsTrue(control.GetStringField(Constants.Control.DataLabel) == this.DataLabel, "{0}: Expected DataLabel '{1}' but found '{2}'.", this.DataLabel, this.DataLabel, control.GetStringField(Constants.Control.DataLabel));
            Assert.IsTrue(control.GetStringField(Constants.Control.DefaultValue) == this.DefaultValue, "{0}: Expected DefaultValue '{1}' but found '{2}'.", this.DataLabel, this.DefaultValue, control.GetStringField(Constants.Control.DefaultValue));
            Assert.IsTrue((long)control[Constants.DatabaseColumn.ID] == this.ID, "{0}: Expected ID '{1}' but found '{2}'.", this.DataLabel, this.ID, control[Constants.DatabaseColumn.ID]);
            Assert.IsTrue(control.GetStringField(Constants.Control.Label) == this.Label, "{0}: Expected Label '{1}' but found '{2}'.", this.DataLabel, this.Label, control.GetStringField(Constants.Control.Label));
            Assert.IsTrue(control.GetStringField(Constants.Control.List) == this.List, "{0}: Expected List '{1}' but found '{2}'.", this.DataLabel, this.List, control.GetStringField(Constants.Control.List));
            Assert.IsTrue((long)control[Constants.Control.SpreadsheetOrder] == this.SpreadsheetOrder, "{0}: Expected SpreadsheetOrder '{1}' but found '{2}'.", this.DataLabel, this.SpreadsheetOrder, control[Constants.Control.SpreadsheetOrder]);
            Assert.IsTrue(control.GetStringField(Constants.Control.TextBoxWidth) == this.TextBoxWidth, "{0}: Expected TextBoxWidth '{1}' but found '{2}'.", this.DataLabel, this.TextBoxWidth, control[Constants.Control.TextBoxWidth]);
            Assert.IsTrue(control.GetStringField(Constants.Control.Tooltip) == this.Tooltip, "{0}: Expected Tooltip '{1}' but found '{2}'.", this.DataLabel, this.Tooltip, control.GetStringField(Constants.Control.Tooltip));
            Assert.IsTrue(control.GetStringField(Constants.Control.Type) == this.Type, "{0}: Expected Type '{1}' but found '{2}'.", this.DataLabel, this.Type, control.GetStringField(Constants.Control.Type));
            Assert.IsTrue(String.Equals(control.GetStringField(Constants.Control.Visible), this.Visible.ToString(), StringComparison.OrdinalIgnoreCase), "{0}: Expected Visible '{1}' but found '{2}'.", this.DataLabel, this.Visible, control.GetStringField(Constants.Control.Visible));
        }
    }
}
