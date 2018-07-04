using Carnassial.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace Carnassial.UnitTests
{
    internal class ControlExpectations
    {
        public bool AnalysisLabel { get; set; }
        public long ControlOrder { get; set; }
        public bool Copyable { get; set; }
        public string DataLabel { get; set; }
        public string DefaultValue { get; set; }
        public long ID { get; set; }
        public bool Index { get; set; }
        public string Label { get; set; }
        public int MaxWidth { get; set; }
        public long SpreadsheetOrder { get; set; }
        public string Tooltip { get; set; }
        public ControlType Type { get; set; }
        public bool Visible { get; set; }
        public string WellKnownValues { get; set; }

        public static ControlExpectations CreateCounter(string dataLabel, long id)
        {
            return ControlExpectations.CreateCounter(dataLabel, id, id, id);
        }

        public static ControlExpectations CreateCounter(string dataLabel, long id, long controlOrder, long spreadsheetOrder)
        {
            return new ControlExpectations()
            {
                AnalysisLabel = false,
                ControlOrder = controlOrder,
                Copyable = false,
                DataLabel = dataLabel,
                DefaultValue = Constant.ControlDefault.CounterValue,
                ID = id,
                Index = false,
                Label = dataLabel,
                MaxWidth = Constant.ControlDefault.MaxWidth,
                SpreadsheetOrder = spreadsheetOrder,
                Tooltip = Constant.ControlDefault.CounterTooltip,
                Type = ControlType.Counter,
                Visible = true,
                WellKnownValues = String.Empty
            };
        }

        public static ControlExpectations CreateChoice(string dataLabel, long id)
        {
            return CreateChoice(dataLabel, id, id, id);
        }

        public static ControlExpectations CreateChoice(string dataLabel, long id, long controlOrder, long spreadsheetOrder)
        {
            return new ControlExpectations()
            {
                AnalysisLabel = false,
                ControlOrder = controlOrder,
                Copyable = true,
                DataLabel = dataLabel,
                DefaultValue = Constant.ControlDefault.Value,
                ID = id,
                Index = false,
                Label = dataLabel,
                MaxWidth = Constant.ControlDefault.MaxWidth,
                SpreadsheetOrder = spreadsheetOrder,
                Tooltip = Constant.ControlDefault.FixedChoiceTooltip,
                Type = ControlType.FixedChoice,
                Visible = true,
                WellKnownValues = String.Empty
            };
        }

        public static ControlExpectations CreateFlag(string dataLabel, long id)
        {
            return ControlExpectations.CreateFlag(dataLabel, id, id, id);
        }

        public static ControlExpectations CreateFlag(string dataLabel, long id, long controlOrder, long spreadsheetOrder)
        {
            return new ControlExpectations()
            {
                AnalysisLabel = false,
                ControlOrder = controlOrder,
                Copyable = true,
                DataLabel = dataLabel,
                DefaultValue = Constant.ControlDefault.FlagValue,
                ID = id,
                Index = false,
                Label = dataLabel,
                MaxWidth = Constant.ControlDefault.MaxWidth,
                SpreadsheetOrder = spreadsheetOrder,
                Tooltip = Constant.ControlDefault.FlagTooltip,
                Type = ControlType.Flag,
                Visible = true,
                WellKnownValues = String.Empty
            };
        }

        public static ControlExpectations CreateNote(string dataLabel, long id)
        {
            return ControlExpectations.CreateNote(dataLabel, id, id, id);
        }

        public static ControlExpectations CreateNote(string dataLabel, long id, long controlOrder, long spreadsheetOrder)
        {
            return new ControlExpectations()
            {
                AnalysisLabel = false,
                ControlOrder = controlOrder,
                Copyable = true,
                DataLabel = dataLabel,
                DefaultValue = Constant.ControlDefault.Value,
                ID = id,
                Index = false,
                Label = dataLabel,
                MaxWidth = Constant.ControlDefault.MaxWidth,
                SpreadsheetOrder = spreadsheetOrder,
                Tooltip = Constant.ControlDefault.NoteTooltip,
                Type = ControlType.Note,
                Visible = true,
                WellKnownValues = String.Empty
            };
        }

        public void Verify(ControlRow control)
        {
            Assert.IsTrue(control.AnalysisLabel == this.AnalysisLabel, "{0}: Expected ControlOrder '{1}' but found '{2}'.", this.DataLabel, this.AnalysisLabel, control.AnalysisLabel);
            Assert.IsTrue(control.ControlOrder == this.ControlOrder, "{0}: Expected ControlOrder '{1}' but found '{2}'.", this.DataLabel, this.ControlOrder, control.ControlOrder);
            Assert.IsTrue(control.Copyable == this.Copyable, "{0}: Expected Copyable '{1}' but found '{2}'.", this.DataLabel, this.Copyable, control.Copyable);
            Assert.IsTrue(String.Equals(control.DataLabel, this.DataLabel, StringComparison.Ordinal), "{0}: Expected DataLabel '{1}' but found '{2}'.", this.DataLabel, this.DataLabel, control.DataLabel);
            Assert.IsTrue(control.DefaultValue == this.DefaultValue, "{0}: Expected DefaultValue '{1}' but found '{2}'.", this.DataLabel, this.DefaultValue, control.DefaultValue);
            Assert.IsTrue(control.ID == this.ID, "{0}: Expected ID '{1}' but found '{2}'.", this.DataLabel, this.ID, control.ID);
            Assert.IsTrue(control.IndexInFileTable == this.Index, "{0}: Expected Index '{1}' but found '{2}'.", this.DataLabel, this.Index, control.IndexInFileTable);
            Assert.IsTrue(control.Label == this.Label, "{0}: Expected Label '{1}' but found '{2}'.", this.DataLabel, this.Label, control.Label);
            Assert.IsTrue(control.MaxWidth == this.MaxWidth, "{0}: Expected TextBoxWidth '{1}' but found '{2}'.", this.DataLabel, this.MaxWidth, control.MaxWidth);
            Assert.IsTrue(control.SpreadsheetOrder == this.SpreadsheetOrder, "{0}: Expected SpreadsheetOrder '{1}' but found '{2}'.", this.DataLabel, this.SpreadsheetOrder, control.SpreadsheetOrder);
            Assert.IsTrue(control.Tooltip == this.Tooltip, "{0}: Expected Tooltip '{1}' but found '{2}'.", this.DataLabel, this.Tooltip, control.Tooltip);
            Assert.IsTrue(control.Type == this.Type, "{0}: Expected Type '{1}' but found '{2}'.", this.DataLabel, this.Type, control.Type);
            Assert.IsTrue(control.Visible == this.Visible, "{0}: Expected Visible '{1}' but found '{2}'.", this.DataLabel, this.Visible, control.Visible);
            Assert.IsTrue(control.WellKnownValues == this.WellKnownValues, "{0}: Expected List '{1}' but found '{2}'.", this.DataLabel, this.WellKnownValues, control.WellKnownValues);
        }
    }
}
