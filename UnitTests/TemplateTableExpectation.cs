using Carnassial.Database;
using Carnassial.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace Carnassial.UnitTests
{
    internal class TemplateTableExpectation
    {
        public ControlExpectations File { get; private set; }
        public ControlExpectations RelativePath { get; private set; }
        public ControlExpectations DateTime { get; private set; }
        public ControlExpectations UtcOffset { get; private set; }
        public ControlExpectations ImageQuality { get; private set; }
        public ControlExpectations DeleteFlag { get; private set; }

        protected TemplateTableExpectation(Version version)
        {
            long id = 1;
            this.File = ControlExpectations.CreateNote(Constant.DatabaseColumn.File, id++);
            this.File.Copyable = false;
            this.File.DefaultValue = Constant.ControlDefault.Value;
            this.File.List = Constant.ControlDefault.Value;
            this.File.TextBoxWidth = Int32.Parse(Constant.ControlDefault.FileWidth);
            this.File.Tooltip = Constant.ControlDefault.FileTooltip;
            this.File.Type = Constant.DatabaseColumn.File;
            this.RelativePath = ControlExpectations.CreateNote(Constant.DatabaseColumn.RelativePath, id++);
            this.RelativePath.Copyable = false;
            this.RelativePath.DefaultValue = Constant.ControlDefault.Value;
            this.RelativePath.List = Constant.ControlDefault.Value;
            this.RelativePath.TextBoxWidth = Int32.Parse(Constant.ControlDefault.RelativePathWidth);
            this.RelativePath.Tooltip = Constant.ControlDefault.RelativePathTooltip;
            this.RelativePath.Type = Constant.DatabaseColumn.RelativePath;
            this.RelativePath.Visible = true;
            this.DateTime = ControlExpectations.CreateNote(Constant.DatabaseColumn.DateTime, id++);
            this.DateTime.Copyable = false;
            this.DateTime.DefaultValue = DateTimeHandler.ToDatabaseDateTimeString(Constant.ControlDefault.DateTimeValue);
            this.DateTime.List = Constant.ControlDefault.Value;
            this.DateTime.TextBoxWidth = Int32.Parse(Constant.ControlDefault.DateTimeWidth);
            this.DateTime.Tooltip = Constant.ControlDefault.DateTimeTooltip;
            this.DateTime.Type = Constant.DatabaseColumn.DateTime;
            this.UtcOffset = ControlExpectations.CreateNote(Constant.DatabaseColumn.UtcOffset, id++);
            this.UtcOffset.Copyable = false;
            this.UtcOffset.DefaultValue = DateTimeHandler.ToDatabaseUtcOffsetString(Constant.ControlDefault.DateTimeValue.Offset);
            this.UtcOffset.List = Constant.ControlDefault.Value;
            this.UtcOffset.TextBoxWidth = Int32.Parse(Constant.ControlDefault.UtcOffsetWidth);
            this.UtcOffset.Tooltip = Constant.ControlDefault.UtcOffsetTooltip;
            this.UtcOffset.Type = Constant.DatabaseColumn.UtcOffset;
            this.UtcOffset.Visible = false;
            this.ImageQuality = ControlExpectations.CreateChoice(Constant.DatabaseColumn.ImageQuality, id++);
            this.ImageQuality.Copyable = false;
            this.ImageQuality.DefaultValue = Constant.ControlDefault.Value;
            this.ImageQuality.List = Constant.ImageQuality.ListOfValues;
            this.ImageQuality.TextBoxWidth = Int32.Parse(Constant.ControlDefault.ImageQualityWidth);
            this.ImageQuality.Tooltip = Constant.ControlDefault.ImageQualityTooltip;
            this.ImageQuality.Type = Constant.DatabaseColumn.ImageQuality;
            this.DeleteFlag = ControlExpectations.CreateFlag(Constant.DatabaseColumn.DeleteFlag, id++);
            this.DeleteFlag.Copyable = false;
            this.DeleteFlag.Label = Constant.ControlDefault.DeleteFlagLabel;
            this.DeleteFlag.List = String.Empty;
            this.DeleteFlag.Tooltip = Constant.ControlDefault.DeleteFlagTooltip;
            this.DeleteFlag.Type = Constant.DatabaseColumn.DeleteFlag;
        }

        public virtual void Verify(TemplateDatabase templateDatabase)
        {
            Assert.IsTrue(templateDatabase.Controls.RowCount == TestConstant.DefaultFileDataColumns.Count - 1);

            int rowIndex = 0;
            this.File.Verify(templateDatabase.Controls[rowIndex++]);
            this.RelativePath.Verify(templateDatabase.Controls[rowIndex++]);
            this.DateTime.Verify(templateDatabase.Controls[rowIndex++]);
            this.UtcOffset.Verify(templateDatabase.Controls[rowIndex++]);
            this.ImageQuality.Verify(templateDatabase.Controls[rowIndex++]);
            this.DeleteFlag.Verify(templateDatabase.Controls[rowIndex++]);
        }
    }
}
