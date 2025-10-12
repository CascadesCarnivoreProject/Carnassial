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

        public ControlExpectations()
        {
            this.AnalysisLabel = false;
            this.ControlOrder = -1;
            this.Copyable = false;
            this.DataLabel = String.Empty;
            this.DefaultValue = String.Empty;
            this.ID = -1;
            this.Index = false;
            this.Label = String.Empty;
            this.MaxWidth = -1;
            this.SpreadsheetOrder = -1;
            this.Tooltip = String.Empty;
            this.Type = (ControlType)(-1);
            this.Visible = false;
            this.WellKnownValues = String.Empty;
        }

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
            Assert.IsTrue(control.AnalysisLabel == this.AnalysisLabel, $"{this.DataLabel}: Expected ControlOrder '{this.AnalysisLabel}' but found '{control.AnalysisLabel}'.");
            Assert.IsTrue(control.ControlOrder == this.ControlOrder, $"{this.DataLabel}: Expected ControlOrder '{this.ControlOrder}' but found '{control.ControlOrder}'.");
            Assert.IsTrue(control.Copyable == this.Copyable, $"{this.DataLabel}: Expected Copyable '{this.Copyable}' but found '{control.Copyable}'.");
            Assert.IsTrue(String.Equals(control.DataLabel, this.DataLabel, StringComparison.Ordinal), $"{this.DataLabel}: Expected DataLabel '{this.DataLabel}' but found '{control.DataLabel}'.");
            Assert.IsTrue(control.DefaultValue == this.DefaultValue, $"{this.DataLabel}: Expected DefaultValue '{this.DefaultValue}' but found '{control.DefaultValue}'.");
            Assert.IsTrue(control.ID == this.ID, $"{this.DataLabel}: Expected ID '{this.ID}' but found '{control.ID}'.");
            Assert.IsTrue(control.IndexInFileTable == this.Index, $"{this.DataLabel}: Expected Index '{this.Index}' but found '{control.IndexInFileTable}'.");
            Assert.IsTrue(control.Label == this.Label, $"{this.DataLabel}: Expected Label '{this.Label}' but found '{control.Label}'.");
            Assert.IsTrue(control.MaxWidth == this.MaxWidth, $"{this.DataLabel}: Expected TextBoxWidth '{this.MaxWidth}' but found '{control.MaxWidth}'.");
            Assert.IsTrue(control.SpreadsheetOrder == this.SpreadsheetOrder, $"{this.DataLabel}: Expected SpreadsheetOrder '{this.SpreadsheetOrder}' but found '{control.SpreadsheetOrder}'.");
            Assert.IsTrue(control.Tooltip == this.Tooltip, $"{this.DataLabel}: Expected Tooltip '{this.Tooltip}' but found '{control.Tooltip}'.");
            Assert.IsTrue(control.ControlType == this.Type, $"{this.DataLabel}: Expected Type '{this.Type}' but found '{control.ControlType}'.");
            Assert.IsTrue(control.Visible == this.Visible, $"{this.DataLabel}: Expected Visible '{this.Visible}' but found '{control.Visible}'.");
            Assert.IsTrue(control.WellKnownValues == this.WellKnownValues, $"{this.DataLabel}: Expected List '{this.WellKnownValues}' but found '{control.WellKnownValues}'.");
        }
    }
}
