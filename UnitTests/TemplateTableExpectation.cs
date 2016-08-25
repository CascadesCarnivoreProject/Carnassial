using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Timelapse.Database;
using Timelapse.Editor;

namespace Timelapse.UnitTests
{
    internal class TemplateTableExpectation
    {
        public ControlExpectations File { get; private set; }
        public ControlExpectations RelativePath { get; private set; }
        public ControlExpectations Folder { get; private set; }
        public ControlExpectations Date { get; private set; }
        public ControlExpectations Time { get; private set; }
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
            this.Date = ControlExpectations.CreateNote(Constants.DatabaseColumn.Date, id++);
            this.Date.Copyable = false;
            this.Date.DefaultValue = Constants.ControlDefault.Value;
            this.Date.List = Constants.ControlDefault.Value;
            this.Date.TextBoxWidth = Int32.Parse(Constants.ControlDefault.DateWidth);
            this.Date.Tooltip = Constants.ControlDefault.DateTooltip;
            this.Date.Type = Constants.DatabaseColumn.Date;
            this.Time = ControlExpectations.CreateNote(Constants.DatabaseColumn.Time, id++);
            this.Time.Copyable = false;
            this.Time.DefaultValue = Constants.ControlDefault.Value;
            this.Time.List = Constants.ControlDefault.Value;
            this.Time.TextBoxWidth = Int32.Parse(Constants.ControlDefault.TimeWidth);
            this.Time.Tooltip = Constants.ControlDefault.TimeTooltip;
            this.Time.Type = Constants.DatabaseColumn.Time;
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

            if (version < TestConstant.Version2104)
            {
                this.File.DefaultValue = " ";
                this.File.List = " ";
                this.File.Tooltip = "The image file name";

                this.Date.DefaultValue = " ";
                this.Date.List = " ";
                this.Date.TextBoxWidth = 100;
                this.Date.Tooltip = "Date the image was taken";

                this.Folder.DefaultValue = " ";
                this.Folder.List = " ";
                this.Folder.Tooltip = "Name of the folder containing the images";

                this.Time.DefaultValue = " ";
                this.Time.List = " ";
                this.Time.TextBoxWidth = 100;
                this.Time.Tooltip = "Time the image was taken";

                this.ImageQuality.DefaultValue = " ";
                this.ImageQuality.List = "Ok| Dark| Corrupted | Missing";
                this.ImageQuality.TextBoxWidth = 80;
                this.ImageQuality.Tooltip = "System-determined image quality: Ok, dark if mostly black, corrupted if it can not be read";

                this.DeleteFlag.Tooltip = "Mark a file as one to be deleted. You can then confirm deletion through the Edit Menu";
            }
        }

        public virtual void Verify(TemplateDatabase templateDatabase)
        {
            Assert.IsTrue(templateDatabase.TemplateTable.RowCount == TestConstant.DefaultImageTableColumns.Count - 1);

            int rowIndex = 0;
            this.File.Verify(templateDatabase.TemplateTable[rowIndex++]);
            this.RelativePath.Verify(templateDatabase.TemplateTable[rowIndex++]);
            this.Folder.Verify(templateDatabase.TemplateTable[rowIndex++]);
            this.Date.Verify(templateDatabase.TemplateTable[rowIndex++]);
            this.Time.Verify(templateDatabase.TemplateTable[rowIndex++]);
            this.ImageQuality.Verify(templateDatabase.TemplateTable[rowIndex++]);
            this.DeleteFlag.Verify(templateDatabase.TemplateTable[rowIndex++]);
        }
    }
}
