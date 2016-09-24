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
        public ControlExpectations Folder { get; private set; }
        public ControlExpectations DateTime { get; private set; }
        public ControlExpectations UtcOffset { get; private set; }
        public ControlExpectations ImageQuality { get; private set; }
        public ControlExpectations DeleteFlag { get; private set; }

        protected TemplateTableExpectation(Version version)
        {
            long id = 1;
            this.File = ControlExpectations.CreateNote(Constants.DatabaseColumn.File, id++);
            this.File.Copyable = false;
            this.File.DefaultValue = Constants.ControlDefault.Value;
            this.File.List = Constants.ControlDefault.Value;
            this.File.TextBoxWidth = Int32.Parse(Constants.ControlDefault.FileWidth);
            this.File.Tooltip = Constants.ControlDefault.FileTooltip;
            this.File.Type = Constants.DatabaseColumn.File;
            this.RelativePath = ControlExpectations.CreateNote(Constants.DatabaseColumn.RelativePath, id++);
            this.RelativePath.Copyable = false;
            this.RelativePath.DefaultValue = Constants.ControlDefault.Value;
            this.RelativePath.List = Constants.ControlDefault.Value;
            this.RelativePath.TextBoxWidth = Int32.Parse(Constants.ControlDefault.RelativePathWidth);
            this.RelativePath.Tooltip = Constants.ControlDefault.RelativePathTooltip;
            this.RelativePath.Type = Constants.DatabaseColumn.RelativePath;
            this.RelativePath.Visible = true;
            this.Folder = ControlExpectations.CreateNote(Constants.DatabaseColumn.Folder, id++);
            this.Folder.Copyable = false;
            this.Folder.DefaultValue = Constants.ControlDefault.Value;
            this.Folder.List = Constants.ControlDefault.Value;
            this.Folder.TextBoxWidth = Int32.Parse(Constants.ControlDefault.FolderWidth);
            this.Folder.Tooltip = Constants.ControlDefault.FolderTooltip;
            this.Folder.Type = Constants.DatabaseColumn.Folder;
            this.DateTime = ControlExpectations.CreateNote(Constants.DatabaseColumn.DateTime, id++);
            this.DateTime.Copyable = false;
            this.DateTime.DefaultValue = DateTimeHandler.ToDatabaseDateTimeString(Constants.ControlDefault.DateTimeValue);
            this.DateTime.List = Constants.ControlDefault.Value;
            this.DateTime.TextBoxWidth = Int32.Parse(Constants.ControlDefault.DateTimeWidth);
            this.DateTime.Tooltip = Constants.ControlDefault.DateTimeTooltip;
            this.DateTime.Type = Constants.DatabaseColumn.DateTime;
            this.UtcOffset = ControlExpectations.CreateNote(Constants.DatabaseColumn.UtcOffset, id++);
            this.UtcOffset.Copyable = false;
            this.UtcOffset.DefaultValue = DateTimeHandler.ToDatabaseUtcOffsetString(Constants.ControlDefault.DateTimeValue.Offset);
            this.UtcOffset.List = Constants.ControlDefault.Value;
            this.UtcOffset.TextBoxWidth = Int32.Parse(Constants.ControlDefault.UtcOffsetWidth);
            this.UtcOffset.Tooltip = Constants.ControlDefault.UtcOffsetTooltip;
            this.UtcOffset.Type = Constants.DatabaseColumn.UtcOffset;
            this.UtcOffset.Visible = false;
            this.ImageQuality = ControlExpectations.CreateChoice(Constants.DatabaseColumn.ImageQuality, id++);
            this.ImageQuality.Copyable = false;
            this.ImageQuality.DefaultValue = Constants.ControlDefault.Value;
            this.ImageQuality.List = Constants.ImageQuality.ListOfValues;
            this.ImageQuality.TextBoxWidth = Int32.Parse(Constants.ControlDefault.ImageQualityWidth);
            this.ImageQuality.Tooltip = Constants.ControlDefault.ImageQualityTooltip;
            this.ImageQuality.Type = Constants.DatabaseColumn.ImageQuality;
            this.DeleteFlag = ControlExpectations.CreateFlag(Constants.DatabaseColumn.DeleteFlag, id++);
            this.DeleteFlag.Copyable = false;
            this.DeleteFlag.Label = Constants.ControlDefault.DeleteFlagLabel;
            this.DeleteFlag.List = String.Empty;
            this.DeleteFlag.Tooltip = Constants.ControlDefault.DeleteFlagTooltip;
            this.DeleteFlag.Type = Constants.DatabaseColumn.DeleteFlag;
        }

        public virtual void Verify(TemplateDatabase templateDatabase)
        {
            Assert.IsTrue(templateDatabase.TemplateTable.RowCount == TestConstant.DefaultImageTableColumns.Count - 1);

            int rowIndex = 0;
            this.File.Verify(templateDatabase.TemplateTable[rowIndex++]);
            this.RelativePath.Verify(templateDatabase.TemplateTable[rowIndex++]);
            this.Folder.Verify(templateDatabase.TemplateTable[rowIndex++]);
            this.DateTime.Verify(templateDatabase.TemplateTable[rowIndex++]);
            this.UtcOffset.Verify(templateDatabase.TemplateTable[rowIndex++]);
            this.ImageQuality.Verify(templateDatabase.TemplateTable[rowIndex++]);
            this.DeleteFlag.Verify(templateDatabase.TemplateTable[rowIndex++]);
        }
    }
}
