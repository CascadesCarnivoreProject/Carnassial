using Carnassial.Data;
using Carnassial.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace Carnassial.UnitTests
{
    internal class ControlsExpectation
    {
        public ControlExpectations Classification { get; private set; }
        public ControlExpectations File { get; private set; }
        public ControlExpectations RelativePath { get; private set; }
        public ControlExpectations DateTime { get; private set; }
        public ControlExpectations UtcOffset { get; private set; }
        public ControlExpectations DeleteFlag { get; private set; }

        protected ControlsExpectation(Version version)
        {
            long id = 1;
            this.File = ControlExpectations.CreateNote(Constant.FileColumn.File, id++);
            this.File.Copyable = false;
            this.File.DefaultValue = Constant.ControlDefault.Value;
            this.File.WellKnownValues = Constant.ControlDefault.Value;
            this.File.MaxWidth = Constant.ControlDefault.MaxWidth;
            this.File.Tooltip = Constant.ControlDefault.FileTooltip;
            this.File.Type = ControlType.Note;
            this.RelativePath = ControlExpectations.CreateNote(Constant.FileColumn.RelativePath, id++);
            this.RelativePath.Copyable = false;
            this.RelativePath.DefaultValue = Constant.ControlDefault.Value;
            this.RelativePath.WellKnownValues = Constant.ControlDefault.Value;
            this.RelativePath.MaxWidth = Constant.ControlDefault.MaxWidth;
            this.RelativePath.Tooltip = Constant.ControlDefault.RelativePathTooltip;
            this.RelativePath.Type = ControlType.Note;
            this.RelativePath.Visible = true;
            this.DateTime = ControlExpectations.CreateNote(Constant.FileColumn.DateTime, id++);
            this.DateTime.Copyable = false;
            this.DateTime.DefaultValue = DateTimeHandler.ToDatabaseDateTimeString(Constant.ControlDefault.DateTimeValue);
            this.DateTime.WellKnownValues = Constant.ControlDefault.Value;
            this.DateTime.MaxWidth = Constant.ControlDefault.MaxWidth;
            this.DateTime.Tooltip = Constant.ControlDefault.DateTimeTooltip;
            this.DateTime.Type = ControlType.DateTime;
            this.UtcOffset = ControlExpectations.CreateNote(Constant.FileColumn.UtcOffset, id++);
            this.UtcOffset.Copyable = false;
            this.UtcOffset.DefaultValue = DateTimeHandler.ToDatabaseUtcOffsetString(Constant.ControlDefault.DateTimeValue.Offset);
            this.UtcOffset.WellKnownValues = Constant.ControlDefault.Value;
            this.UtcOffset.MaxWidth = Constant.ControlDefault.MaxWidth;
            this.UtcOffset.Tooltip = Constant.ControlDefault.UtcOffsetTooltip;
            this.UtcOffset.Type = ControlType.UtcOffset;
            this.UtcOffset.Visible = false;
            this.Classification = ControlExpectations.CreateChoice(Constant.FileColumn.Classification, id++);
            this.Classification.Copyable = false;
            this.Classification.DefaultValue = Constant.ControlDefault.Value;
            this.Classification.WellKnownValues = Constant.ControlDefault.ClassificationWellKnownValues;
            this.Classification.MaxWidth = Constant.ControlDefault.MaxWidth;
            this.Classification.Tooltip = Constant.ControlDefault.ClassificationTooltip;
            this.Classification.Type = ControlType.FixedChoice;
            this.DeleteFlag = ControlExpectations.CreateFlag(Constant.FileColumn.DeleteFlag, id++);
            this.DeleteFlag.Copyable = false;
            this.DeleteFlag.Label = Constant.ControlDefault.DeleteFlagLabel;
            this.DeleteFlag.WellKnownValues = String.Empty;
            this.DeleteFlag.Tooltip = Constant.ControlDefault.DeleteFlagTooltip;
            this.DeleteFlag.Type = ControlType.Flag;
        }

        public virtual void Verify(TemplateDatabase templateDatabase)
        {
            Assert.IsTrue(templateDatabase.Controls.RowCount == TestConstant.DefaultFileColumns.Count - 1);

            int rowIndex = 0;
            this.File.Verify(templateDatabase.Controls[rowIndex++]);
            this.RelativePath.Verify(templateDatabase.Controls[rowIndex++]);
            this.DateTime.Verify(templateDatabase.Controls[rowIndex++]);
            this.UtcOffset.Verify(templateDatabase.Controls[rowIndex++]);
            this.Classification.Verify(templateDatabase.Controls[rowIndex++]);
            this.DeleteFlag.Verify(templateDatabase.Controls[rowIndex++]);
        }
    }
}
