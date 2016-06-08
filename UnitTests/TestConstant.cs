using System.Collections.Generic;
using System.Collections.ObjectModel;
using Timelapse.Database;
using TimelapseTemplateEditor;

namespace Timelapse.UnitTests
{
    internal static class TestConstant
    {
        public const double DarkPixelFractionTolerance = 0.00000001;

        public static readonly ReadOnlyCollection<string> DefaultImageTableColumns = new List<string>()
        {
            Constants.DatabaseColumn.ID,
            Constants.DatabaseColumn.File,
            Constants.DatabaseColumn.RelativePath,
            Constants.DatabaseColumn.Folder,
            Constants.DatabaseColumn.Date,
            Constants.DatabaseColumn.Time,
            Constants.DatabaseColumn.ImageQuality,
            EditorConstant.Control.MarkForDeletion,
            TestConstant.DefaultDatabaseColumn.Counter0,
            TestConstant.DefaultDatabaseColumn.Choice0,
            TestConstant.DefaultDatabaseColumn.Note0,
            TestConstant.DefaultDatabaseColumn.Flag0,
            TestConstant.DefaultDatabaseColumn.CounterWithCustomDataLabel,
            TestConstant.DefaultDatabaseColumn.ChoiceWithCustomDataLabel,
            TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel,
            TestConstant.DefaultDatabaseColumn.FlagWithCustomDataLabel,
            TestConstant.DefaultDatabaseColumn.CounterNotVisible,
            TestConstant.DefaultDatabaseColumn.ChoiceNotVisible,
            TestConstant.DefaultDatabaseColumn.NoteNotVisible,
            TestConstant.DefaultDatabaseColumn.FlagNotVisible,
            TestConstant.DefaultDatabaseColumn.Counter3,
            TestConstant.DefaultDatabaseColumn.Choice3,
            TestConstant.DefaultDatabaseColumn.Note3,
            TestConstant.DefaultDatabaseColumn.Flag3
        }.AsReadOnly();

        public static readonly ReadOnlyCollection<string> DefaultMarkerTableColumns = new List<string>()
        {
            Constants.DatabaseColumn.ID,
            TestConstant.DefaultDatabaseColumn.Counter0,
            TestConstant.DefaultDatabaseColumn.CounterWithCustomDataLabel,
            TestConstant.DefaultDatabaseColumn.CounterNotVisible,
            TestConstant.DefaultDatabaseColumn.Counter3
        }.AsReadOnly();

        public static readonly ReadOnlyCollection<string> ImageSetTableColumns = new List<string>()
        {
            Constants.DatabaseColumn.ID,
        }.AsReadOnly();

        public static readonly ReadOnlyCollection<string> TemplateTableColumns = new List<string>()
            {
                Constants.Control.ControlOrder,
                Constants.Control.SpreadsheetOrder,
                Constants.Control.DefaultValue,
                Constants.Control.Label,
                Constants.Control.DataLabel,
                Constants.Control.Tooltip,
                Constants.Control.TextBoxWidth,
                Constants.Control.Copyable,
                Constants.Control.Visible,
                Constants.Control.List,
                Constants.DatabaseColumn.ID,
                Constants.Control.Type
            }.AsReadOnly();

        public static class DefaultDatabaseColumn
        {
            public const string Counter0 = "Counter0";
            public const string Choice0 = "Choice0";
            public const string Note0 = "Note0";
            public const string Flag0 = "Flag0";
            public const string CounterWithCustomDataLabel = "CounterWithCustomDataLabel";
            public const string ChoiceWithCustomDataLabel = "ChoiceWithCustomDataLabel";
            public const string NoteWithCustomDataLabel = "NoteWithCustomDataLabel";
            public const string FlagWithCustomDataLabel = "FlagWithCustomDataLabel";
            public const string CounterNotVisible = "CounterNotVisible";
            public const string ChoiceNotVisible = "ChoiceNotVisible";
            public const string NoteNotVisible = "NoteNotVisible";
            public const string FlagNotVisible = "FlagNotVisible";
            public const string Counter3 = "Counter3";
            public const string Choice3 = "Choice3";
            public const string Note3 = "Note3";
            public const string Flag3 = "Flag3";
        }

        public static class Expectations
        {
            // controls
            public static readonly ControlExpectations File;
            public static readonly ControlExpectations RelativePath;
            public static readonly ControlExpectations Folder;
            public static readonly ControlExpectations Date;
            public static readonly ControlExpectations Time;
            public static readonly ControlExpectations ImageQuality;
            public static readonly ControlExpectations MarkForDeletion;
            public static readonly ControlExpectations Counter0;
            public static readonly ControlExpectations Choice0;
            public static readonly ControlExpectations Note0;
            public static readonly ControlExpectations Flag0;
            public static readonly ControlExpectations CounterWithCustomDataLabel;
            public static readonly ControlExpectations ChoiceWithCustomDataLabel;
            public static readonly ControlExpectations NoteWithCustomDataLabel;
            public static readonly ControlExpectations FlagWithCustomDataLabel;
            public static readonly ControlExpectations CounterNotVisible;
            public static readonly ControlExpectations ChoiceNotVisible;
            public static readonly ControlExpectations NoteNotVisible;
            public static readonly ControlExpectations FlagNotVisible;
            public static readonly ControlExpectations Counter3;
            public static readonly ControlExpectations Choice3;
            public static readonly ControlExpectations Note3;
            public static readonly ControlExpectations Flag3;

            // images
            public static readonly ImageExpectations DaylightBobcatImage;
            public static readonly ImageExpectations DaylightCoyoteImage;
            public static readonly ImageExpectations DaylightMartenPairImage;
            public static readonly ImageExpectations InfraredMartenImage;

            static Expectations()
            {
                // standard controls
                Expectations.File = ControlExpectations.CreateNote(Constants.DatabaseColumn.File, 1);
                Expectations.File.Copyable = false;
                Expectations.File.DefaultValue = " ";
                Expectations.File.List = " ";
                Expectations.File.TextBoxWidth = Constants.ControlDefault.FileWidth;
                Expectations.File.Tooltip = Constants.ControlDefault.FileTooltip;
                Expectations.File.Type = Constants.DatabaseColumn.File;
                Expectations.RelativePath = ControlExpectations.CreateNote(Constants.DatabaseColumn.RelativePath, 23);
                Expectations.RelativePath.Copyable = false;
                Expectations.RelativePath.DefaultValue = Constants.ControlDefault.Value;
                Expectations.RelativePath.List = Constants.ControlDefault.Value;
                Expectations.RelativePath.TextBoxWidth = Constants.ControlDefault.RelativePathWidth;
                Expectations.RelativePath.Tooltip = Constants.ControlDefault.RelativePathTooltip;
                Expectations.RelativePath.Type = Constants.DatabaseColumn.RelativePath;
                Expectations.RelativePath.Visible = false;
                Expectations.Folder = ControlExpectations.CreateNote(Constants.DatabaseColumn.Folder, 2);
                Expectations.Folder.Copyable = false;
                Expectations.Folder.DefaultValue = " ";
                Expectations.Folder.List = " ";
                Expectations.Folder.TextBoxWidth = Constants.ControlDefault.FolderWidth;
                Expectations.Folder.Tooltip = Constants.ControlDefault.FolderTooltip;
                Expectations.Folder.Type = Constants.DatabaseColumn.Folder;
                Expectations.Date = ControlExpectations.CreateNote(Constants.DatabaseColumn.Date, 3);
                Expectations.Date.Copyable = false;
                Expectations.Date.DefaultValue = " ";
                Expectations.Date.List = " ";
                Expectations.Date.TextBoxWidth = Constants.ControlDefault.DateWidth;
                Expectations.Date.Tooltip = Constants.ControlDefault.DateTooltip;
                Expectations.Date.Type = Constants.DatabaseColumn.Date;
                Expectations.Time = ControlExpectations.CreateNote(Constants.DatabaseColumn.Time, 4);
                Expectations.Time.Copyable = false;
                Expectations.Time.DefaultValue = " ";
                Expectations.Time.List = " ";
                Expectations.Time.TextBoxWidth = Constants.ControlDefault.TimeWidth;
                Expectations.Time.Tooltip = Constants.ControlDefault.TimeTooltip;
                Expectations.Time.Type = Constants.DatabaseColumn.Time;
                Expectations.ImageQuality = ControlExpectations.CreateFlag(Constants.DatabaseColumn.ImageQuality, 5);
                Expectations.ImageQuality.Copyable = false;
                Expectations.ImageQuality.DefaultValue = " ";
                Expectations.ImageQuality.List = TestConstant.ImageQuality.LegacyListOfValues;
                Expectations.ImageQuality.TextBoxWidth = Constants.ControlDefault.ImageQualityWidth;
                Expectations.ImageQuality.Tooltip = Constants.ControlDefault.ImageQualityTooltip;
                Expectations.ImageQuality.Type = Constants.DatabaseColumn.ImageQuality;
                Expectations.MarkForDeletion = ControlExpectations.CreateFlag(EditorConstant.Control.MarkForDeletion, 6);
                Expectations.MarkForDeletion.Copyable = false;
                Expectations.MarkForDeletion.Label = EditorConstant.Control.MarkForDeletionLabel;
                Expectations.MarkForDeletion.List = " ";
                Expectations.MarkForDeletion.Tooltip = Constants.ControlDefault.MarkForDeletionTooltip;
                Expectations.MarkForDeletion.Type = Constants.Control.DeleteFlag;

                // controls
                Expectations.Counter0 = ControlExpectations.CreateCounter(TestConstant.DefaultDatabaseColumn.Counter0, 7);
                Expectations.Counter0.List = " ";
                Expectations.Choice0 = ControlExpectations.CreateChoice(TestConstant.DefaultDatabaseColumn.Choice0, 8);
                Expectations.Choice0.DefaultValue = " ";
                Expectations.Choice0.List = "choice a|choice b|choice c";
                Expectations.Note0 = ControlExpectations.CreateNote(TestConstant.DefaultDatabaseColumn.Note0, 9);
                Expectations.Note0.DefaultValue = " ";
                Expectations.Note0.List = " ";
                Expectations.Flag0 = ControlExpectations.CreateFlag(TestConstant.DefaultDatabaseColumn.Flag0, 10);
                Expectations.Flag0.List = " ";
                Expectations.CounterWithCustomDataLabel = ControlExpectations.CreateCounter(TestConstant.DefaultDatabaseColumn.CounterWithCustomDataLabel, 11);
                Expectations.CounterWithCustomDataLabel.DefaultValue = "100";
                Expectations.CounterWithCustomDataLabel.Label = "CounterWithCustomLabel";
                Expectations.CounterWithCustomDataLabel.List = " ";
                Expectations.CounterWithCustomDataLabel.TextBoxWidth = "75";
                Expectations.CounterWithCustomDataLabel.Tooltip = "Counter with custom label.";
                Expectations.ChoiceWithCustomDataLabel = ControlExpectations.CreateChoice(TestConstant.DefaultDatabaseColumn.ChoiceWithCustomDataLabel, 12);
                Expectations.ChoiceWithCustomDataLabel.DefaultValue = " ";
                Expectations.ChoiceWithCustomDataLabel.Label = "ChoiceWithCustomLabel";
                Expectations.ChoiceWithCustomDataLabel.List = " ";
                Expectations.ChoiceWithCustomDataLabel.List = "value|Genus species|Genus species subspecies|with , comma|with ' apostrophe";
                Expectations.ChoiceWithCustomDataLabel.TextBoxWidth = "90";
                Expectations.ChoiceWithCustomDataLabel.Tooltip = "Choice with custom label and some values of interest.";
                Expectations.NoteWithCustomDataLabel = ControlExpectations.CreateNote(TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel, 13);
                Expectations.NoteWithCustomDataLabel.DefaultValue = " ";
                Expectations.NoteWithCustomDataLabel.Label = "NoteWithCustomLabel";
                Expectations.NoteWithCustomDataLabel.List = " ";
                Expectations.NoteWithCustomDataLabel.TextBoxWidth = "200";
                Expectations.NoteWithCustomDataLabel.Tooltip = "Note with custom label.";
                Expectations.FlagWithCustomDataLabel = ControlExpectations.CreateFlag(TestConstant.DefaultDatabaseColumn.FlagWithCustomDataLabel, 14);
                Expectations.FlagWithCustomDataLabel.DefaultValue = Constants.Boolean.True;
                Expectations.FlagWithCustomDataLabel.Label = "FlagWithCustomLabel";
                Expectations.FlagWithCustomDataLabel.List = " ";
                Expectations.FlagWithCustomDataLabel.TextBoxWidth = "30";
                Expectations.FlagWithCustomDataLabel.Tooltip = "Flag with custom label.";

                Expectations.CounterNotVisible = ControlExpectations.CreateCounter(TestConstant.DefaultDatabaseColumn.CounterNotVisible, 15);
                Expectations.CounterNotVisible.DefaultValue = "3";
                Expectations.CounterNotVisible.Label = "InvisibleCounter";
                Expectations.CounterNotVisible.List = " ";
                Expectations.CounterNotVisible.TextBoxWidth = "111";
                Expectations.CounterNotVisible.Tooltip = "Counter which can't be seen.";
                Expectations.CounterNotVisible.Visible = false;
                Expectations.ChoiceNotVisible = ControlExpectations.CreateChoice(TestConstant.DefaultDatabaseColumn.ChoiceNotVisible, 16);
                Expectations.ChoiceNotVisible.DefaultValue = " ";
                Expectations.ChoiceNotVisible.Label = "InvisibleChoice";
                Expectations.ChoiceNotVisible.List = "you can't see me";
                Expectations.ChoiceNotVisible.TextBoxWidth = "150";
                Expectations.ChoiceNotVisible.Tooltip = "Choice which can't be seen.";
                Expectations.ChoiceNotVisible.Visible = false;
                Expectations.NoteNotVisible = ControlExpectations.CreateNote(TestConstant.DefaultDatabaseColumn.NoteNotVisible, 17);
                Expectations.NoteNotVisible.DefaultValue = " ";
                Expectations.NoteNotVisible.Label = "InvisibleNote";
                Expectations.NoteNotVisible.List = " ";
                Expectations.NoteNotVisible.TextBoxWidth = "32";
                Expectations.NoteNotVisible.Tooltip = "Note which can't be seen.";
                Expectations.NoteNotVisible.Visible = false;
                Expectations.FlagNotVisible = ControlExpectations.CreateFlag(TestConstant.DefaultDatabaseColumn.FlagNotVisible, 18);
                Expectations.FlagNotVisible.Label = "InvisibleFlag";
                Expectations.FlagNotVisible.List = " ";
                Expectations.FlagNotVisible.TextBoxWidth = "17";
                Expectations.FlagNotVisible.Tooltip = "Flag which can't be seen.";
                Expectations.FlagNotVisible.Visible = false;

                Expectations.Counter3 = ControlExpectations.CreateCounter(TestConstant.DefaultDatabaseColumn.Counter3, 19);
                Expectations.Counter3.Copyable = true;
                Expectations.Counter3.Label = "CopyableCounter";
                Expectations.Counter3.List = " ";
                Expectations.Counter3.Tooltip = "Counter which can be copied (rather than the default of not being copyable).";
                Expectations.Choice3 = ControlExpectations.CreateChoice(TestConstant.DefaultDatabaseColumn.Choice3, 20);
                Expectations.Choice3.Copyable = false;
                Expectations.Choice3.DefaultValue = " ";
                Expectations.Choice3.Label = "NotCopyableChoice";
                Expectations.Choice3.List = " ";
                Expectations.Choice3.Tooltip = "Choice which can't be copied (rather than the default of copyable).";
                Expectations.Note3 = ControlExpectations.CreateNote(TestConstant.DefaultDatabaseColumn.Note3, 21);
                Expectations.Note3.Copyable = false;
                Expectations.Note3.DefaultValue = " ";
                Expectations.Note3.Label = "NotCopyableNote";
                Expectations.Note3.List = " ";
                Expectations.Note3.Tooltip = "Note which can't be copied (rather than the default of copyable).";
                Expectations.Flag3 = ControlExpectations.CreateFlag(TestConstant.DefaultDatabaseColumn.Flag3, 22);
                Expectations.Flag3.Copyable = false;
                Expectations.Flag3.Label = "NotCopyableFlag";
                Expectations.Flag3.List = " ";
                Expectations.Flag3.Tooltip = "Flag which can't be copied (rather than the default of copyable).";

                // images
                Expectations.DaylightBobcatImage = new ImageExpectations()
                {
                    DarkPixelFraction = 0.24222145485288338,
                    Date = "05-Aug-2015",
                    FileName = TestConstant.File.DaylightBobcatImage,
                    IsColor = true,
                    Quality = ImageQualityFilter.Ok,
                    Time = "08:06:23"
                };

                Expectations.DaylightCoyoteImage = new ImageExpectations()
                {
                    DarkPixelFraction = 0.60847930235235814,
                    Date = "21-Apr-2016",
                    FileName = TestConstant.File.DaylightCoyoteImage,
                    IsColor = true,
                    Quality = ImageQualityFilter.Ok,
                    RelativePath = TestConstant.File.CarnivoreDirectoryName,
                    Time = "06:31:13"
                };

                Expectations.DaylightMartenPairImage = new ImageExpectations()
                {
                    DarkPixelFraction = 0.70253739978510621,
                    Date = "28-Jan-2015",
                    FileName = TestConstant.File.DaylightMartenPairImage,
                    IsColor = true,
                    Quality = ImageQualityFilter.Ok,
                    RelativePath = TestConstant.File.CarnivoreDirectoryName,
                    Time = "11:17:34"
                };

                Expectations.InfraredMartenImage = new ImageExpectations()
                {
                    DarkPixelFraction = 0.0743353174106539,
                    Date = "24-Feb-2016",
                    FileName = TestConstant.File.InfraredMartenImage,
                    IsColor = false,
                    Quality = ImageQualityFilter.Ok,
                    Time = "04:59:46"
                };
            }
        }

        public static class File
        {
            // template databases for backwards compatibility testing
            // version is the editor version used for creation
            public const string CarnivoreDirectoryName = "CarnivoreTestImages";
            public const string CarnivoreTemplateDatabaseFileName = "CarnivoreTemplate 2.0.1.5.tdb";
            public const string DefaultTemplateDatabaseFileName2015 = "TimelapseTemplate 2.0.1.5.tdb";

            // image databases for backwards compatibility testing
            // version is the Timelapse version used for creation
            public const string DefaultImageDatabaseFileName2023 = "TimelapseData 2.0.2.3.ddb";

            // databases generated dynamically by tests
            // see also use of Constants.File.Default*DatabaseFileName
            public const string CarnivoreNewImageDatabaseFileName = "CarnivoreDatabaseTest.ddb";
            public const string DefaultNewTemplateDatabaseFileName = "TemplateDatabaseTest.tdb";

            public const string DaylightBobcatImage = "BushnellTrophyHD-119677C-20160805-926.JPG";
            public const string DaylightCoyoteImage = "BushnellTrophyHDAggressor-119777C-20160421-112.JPG";
            public const string DaylightMartenPairImage = "Reconyx-HC500-20150128-201.JPG";
            public const string InfraredMartenImage = "BushnellTrophyHD-119677C-20160224-056.JPG";
        }

        public static class ImageQuality
        {
            public const string LegacyListOfValues = "Ok| Dark| Corrupted | Missing";
        }
    }
}
