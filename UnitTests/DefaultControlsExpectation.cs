using Carnassial.Data;
using System;

namespace Carnassial.UnitTests
{
    internal class DefaultControlsExpectation : ControlsExpectation
    {
        public ControlExpectations Counter0 { get; private set; }
        public ControlExpectations Choice0 { get; private set; }
        public ControlExpectations Note0 { get; private set; }
        public ControlExpectations Flag0 { get; private set; }
        public ControlExpectations CounterWithCustomDataLabel { get; private set; }
        public ControlExpectations ChoiceWithCustomDataLabel { get; private set; }
        public ControlExpectations NoteWithCustomDataLabel { get; private set; }
        public ControlExpectations FlagWithCustomDataLabel { get; private set; }
        public ControlExpectations CounterNotVisible { get; private set; }
        public ControlExpectations ChoiceNotVisible { get; private set; }
        public ControlExpectations NoteNotVisible { get; private set; }
        public ControlExpectations FlagNotVisible { get; private set; }
        public ControlExpectations Counter3 { get; private set; }
        public ControlExpectations Choice3 { get; private set; }
        public ControlExpectations Note3 { get; private set; }
        public ControlExpectations Flag3 { get; private set; }

        public DefaultControlsExpectation(Version version)
            : base(version)
        {
            long id = Constant.Control.StandardControls.Count + 1;
            this.Counter0 = ControlExpectations.CreateCounter(TestConstant.DefaultDatabaseColumn.Counter0, id++);
            this.Counter0.List = Constant.ControlDefault.Value;
            this.Choice0 = ControlExpectations.CreateChoice(TestConstant.DefaultDatabaseColumn.Choice0, id++);
            this.Choice0.DefaultValue = Constant.ControlDefault.Value;
            this.Choice0.List = "choice a|choice b|choice c|";
            this.Note0 = ControlExpectations.CreateNote(TestConstant.DefaultDatabaseColumn.Note0, id++);
            this.Note0.DefaultValue = Constant.ControlDefault.Value;
            this.Note0.List = Constant.ControlDefault.Value;
            this.Flag0 = ControlExpectations.CreateFlag(TestConstant.DefaultDatabaseColumn.Flag0, id++);
            this.Flag0.List = Constant.ControlDefault.Value;
            this.CounterWithCustomDataLabel = ControlExpectations.CreateCounter(TestConstant.DefaultDatabaseColumn.CounterWithCustomDataLabel, id++);
            this.CounterWithCustomDataLabel.DefaultValue = "100";
            this.CounterWithCustomDataLabel.Label = "CounterWithCustomLabel";
            this.CounterWithCustomDataLabel.List = Constant.ControlDefault.Value;
            this.CounterWithCustomDataLabel.MaxWidth = 75;
            this.CounterWithCustomDataLabel.Tooltip = "Counter with custom label.";
            this.ChoiceWithCustomDataLabel = ControlExpectations.CreateChoice(TestConstant.DefaultDatabaseColumn.ChoiceWithCustomDataLabel, id++);
            this.ChoiceWithCustomDataLabel.DefaultValue = Constant.ControlDefault.Value;
            this.ChoiceWithCustomDataLabel.Label = "ChoiceWithCustomLabel";
            this.ChoiceWithCustomDataLabel.List = Constant.ControlDefault.Value;
            this.ChoiceWithCustomDataLabel.List = "common name|Genus species|Genus species subspecies|with , comma|with ' apostrophe|";
            this.ChoiceWithCustomDataLabel.MaxWidth = 90;
            this.ChoiceWithCustomDataLabel.Tooltip = "Choice with custom label and some values of interest.";
            this.NoteWithCustomDataLabel = ControlExpectations.CreateNote(TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel, id++);
            this.NoteWithCustomDataLabel.DefaultValue = Constant.ControlDefault.Value;
            this.NoteWithCustomDataLabel.Label = "NoteWithCustomLabel";
            this.NoteWithCustomDataLabel.List = Constant.ControlDefault.Value;
            this.NoteWithCustomDataLabel.MaxWidth = 200;
            this.NoteWithCustomDataLabel.Tooltip = "Note with custom label.";
            this.FlagWithCustomDataLabel = ControlExpectations.CreateFlag(TestConstant.DefaultDatabaseColumn.FlagWithCustomDataLabel, id++);
            this.FlagWithCustomDataLabel.DefaultValue = Boolean.TrueString;
            this.FlagWithCustomDataLabel.Label = "FlagWithCustomLabel";
            this.FlagWithCustomDataLabel.List = Constant.ControlDefault.Value;
            this.FlagWithCustomDataLabel.MaxWidth = 30;
            this.FlagWithCustomDataLabel.Tooltip = "Flag with custom label.";

            this.CounterNotVisible = ControlExpectations.CreateCounter(TestConstant.DefaultDatabaseColumn.CounterNotVisible, id++);
            this.CounterNotVisible.DefaultValue = "3";
            this.CounterNotVisible.Label = "InvisibleCounter";
            this.CounterNotVisible.List = Constant.ControlDefault.Value;
            this.CounterNotVisible.MaxWidth = 111;
            this.CounterNotVisible.Tooltip = "Counter which can't be seen.";
            this.CounterNotVisible.Visible = false;
            this.ChoiceNotVisible = ControlExpectations.CreateChoice(TestConstant.DefaultDatabaseColumn.ChoiceNotVisible, id++);
            this.ChoiceNotVisible.DefaultValue = Constant.ControlDefault.Value;
            this.ChoiceNotVisible.Label = "InvisibleChoice";
            this.ChoiceNotVisible.List = "you can't see me";
            this.ChoiceNotVisible.MaxWidth = 150;
            this.ChoiceNotVisible.Tooltip = "Choice which can't be seen.";
            this.ChoiceNotVisible.Visible = false;
            this.NoteNotVisible = ControlExpectations.CreateNote(TestConstant.DefaultDatabaseColumn.NoteNotVisible, id++);
            this.NoteNotVisible.DefaultValue = Constant.ControlDefault.Value;
            this.NoteNotVisible.Label = "InvisibleNote";
            this.NoteNotVisible.List = Constant.ControlDefault.Value;
            this.NoteNotVisible.MaxWidth = 32;
            this.NoteNotVisible.Tooltip = "Note which can't be seen.";
            this.NoteNotVisible.Visible = false;
            this.FlagNotVisible = ControlExpectations.CreateFlag(TestConstant.DefaultDatabaseColumn.FlagNotVisible, id++);
            this.FlagNotVisible.Label = "InvisibleFlag";
            this.FlagNotVisible.List = Constant.ControlDefault.Value;
            this.FlagNotVisible.MaxWidth = 17;
            this.FlagNotVisible.Tooltip = "Flag which can't be seen.";
            this.FlagNotVisible.Visible = false;

            this.Counter3 = ControlExpectations.CreateCounter(TestConstant.DefaultDatabaseColumn.Counter3, id++);
            this.Counter3.Copyable = true;
            this.Counter3.Label = "CopyableCounter";
            this.Counter3.List = Constant.ControlDefault.Value;
            this.Counter3.Tooltip = "Counter which can be copied (rather than the default of not being copyable).";
            this.Choice3 = ControlExpectations.CreateChoice(TestConstant.DefaultDatabaseColumn.Choice3, id++);
            this.Choice3.Copyable = false;
            this.Choice3.DefaultValue = Constant.ControlDefault.Value;
            this.Choice3.Label = "NotCopyableChoice";
            this.Choice3.List = Constant.ControlDefault.Value;
            this.Choice3.Tooltip = "Choice which can't be copied (rather than the default of copyable).";
            this.Note3 = ControlExpectations.CreateNote(TestConstant.DefaultDatabaseColumn.Note3, id++);
            this.Note3.Copyable = false;
            this.Note3.DefaultValue = Constant.ControlDefault.Value;
            this.Note3.Label = "NotCopyableNote";
            this.Note3.List = Constant.ControlDefault.Value;
            this.Note3.Tooltip = "Note which can't be copied (rather than the default of copyable).";
            this.Flag3 = ControlExpectations.CreateFlag(TestConstant.DefaultDatabaseColumn.Flag3, id++);
            this.Flag3.Copyable = false;
            this.Flag3.Label = "NotCopyableFlag";
            this.Flag3.List = Constant.ControlDefault.Value;
            this.Flag3.Tooltip = "Flag which can't be copied (rather than the default of copyable).";
        }

        public override void Verify(TemplateDatabase templateDatabase)
        {
            base.Verify(templateDatabase);

            int rowIndex = Constant.Control.StandardControls.Count;
            this.Counter0.Verify(templateDatabase.Controls[rowIndex++]);
            this.Choice0.Verify(templateDatabase.Controls[rowIndex++]);
            this.Note0.Verify(templateDatabase.Controls[rowIndex++]);
            this.Flag0.Verify(templateDatabase.Controls[rowIndex++]);
            this.CounterWithCustomDataLabel.Verify(templateDatabase.Controls[rowIndex++]);
            this.ChoiceWithCustomDataLabel.Verify(templateDatabase.Controls[rowIndex++]);
            this.NoteWithCustomDataLabel.Verify(templateDatabase.Controls[rowIndex++]);
            this.FlagWithCustomDataLabel.Verify(templateDatabase.Controls[rowIndex++]);
            this.CounterNotVisible.Verify(templateDatabase.Controls[rowIndex++]);
            this.ChoiceNotVisible.Verify(templateDatabase.Controls[rowIndex++]);
            this.NoteNotVisible.Verify(templateDatabase.Controls[rowIndex++]);
            this.FlagNotVisible.Verify(templateDatabase.Controls[rowIndex++]);
            this.Counter3.Verify(templateDatabase.Controls[rowIndex++]);
            this.Choice3.Verify(templateDatabase.Controls[rowIndex++]);
            this.Note3.Verify(templateDatabase.Controls[rowIndex++]);
            this.Flag3.Verify(templateDatabase.Controls[rowIndex++]);
        }
    }
}
