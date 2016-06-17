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

        public static class CarnivoreDatabaseColumn
        {
            public const string Pelage = "Pelage";
        }

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

        public static class DefaultExpectation
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

            static DefaultExpectation()
            {
                // standard controls
                DefaultExpectation.File = ControlExpectations.CreateNote(Constants.DatabaseColumn.File, 1);
                DefaultExpectation.File.Copyable = false;
                DefaultExpectation.File.DefaultValue = " ";
                DefaultExpectation.File.List = " ";
                DefaultExpectation.File.TextBoxWidth = Constants.ControlDefault.FileWidth;
                DefaultExpectation.File.Tooltip = Constants.ControlDefault.FileTooltip;
                DefaultExpectation.File.Type = Constants.DatabaseColumn.File;
                DefaultExpectation.RelativePath = ControlExpectations.CreateNote(Constants.DatabaseColumn.RelativePath, 2);
                DefaultExpectation.RelativePath.Copyable = false;
                DefaultExpectation.RelativePath.DefaultValue = Constants.ControlDefault.Value;
                DefaultExpectation.RelativePath.List = Constants.ControlDefault.Value;
                DefaultExpectation.RelativePath.TextBoxWidth = Constants.ControlDefault.RelativePathWidth;
                DefaultExpectation.RelativePath.Tooltip = Constants.ControlDefault.RelativePathTooltip;
                DefaultExpectation.RelativePath.Type = Constants.DatabaseColumn.RelativePath;
                DefaultExpectation.RelativePath.Visible = false;
                DefaultExpectation.Folder = ControlExpectations.CreateNote(Constants.DatabaseColumn.Folder, 3);
                DefaultExpectation.Folder.Copyable = false;
                DefaultExpectation.Folder.DefaultValue = " ";
                DefaultExpectation.Folder.List = " ";
                DefaultExpectation.Folder.TextBoxWidth = Constants.ControlDefault.FolderWidth;
                DefaultExpectation.Folder.Tooltip = Constants.ControlDefault.FolderTooltip;
                DefaultExpectation.Folder.Type = Constants.DatabaseColumn.Folder;
                DefaultExpectation.Date = ControlExpectations.CreateNote(Constants.DatabaseColumn.Date, 4);
                DefaultExpectation.Date.Copyable = false;
                DefaultExpectation.Date.DefaultValue = " ";
                DefaultExpectation.Date.List = " ";
                DefaultExpectation.Date.TextBoxWidth = Constants.ControlDefault.DateWidth;
                DefaultExpectation.Date.Tooltip = Constants.ControlDefault.DateTooltip;
                DefaultExpectation.Date.Type = Constants.DatabaseColumn.Date;
                DefaultExpectation.Time = ControlExpectations.CreateNote(Constants.DatabaseColumn.Time, 5);
                DefaultExpectation.Time.Copyable = false;
                DefaultExpectation.Time.DefaultValue = " ";
                DefaultExpectation.Time.List = " ";
                DefaultExpectation.Time.TextBoxWidth = Constants.ControlDefault.TimeWidth;
                DefaultExpectation.Time.Tooltip = Constants.ControlDefault.TimeTooltip;
                DefaultExpectation.Time.Type = Constants.DatabaseColumn.Time;
                DefaultExpectation.ImageQuality = ControlExpectations.CreateFlag(Constants.DatabaseColumn.ImageQuality, 6);
                DefaultExpectation.ImageQuality.Copyable = false;
                DefaultExpectation.ImageQuality.DefaultValue = " ";
                DefaultExpectation.ImageQuality.List = TestConstant.ImageQuality.LegacyListOfValues;
                DefaultExpectation.ImageQuality.TextBoxWidth = Constants.ControlDefault.ImageQualityWidth;
                DefaultExpectation.ImageQuality.Tooltip = Constants.ControlDefault.ImageQualityTooltip;
                DefaultExpectation.ImageQuality.Type = Constants.DatabaseColumn.ImageQuality;
                DefaultExpectation.MarkForDeletion = ControlExpectations.CreateFlag(EditorConstant.Control.MarkForDeletion, 7);
                DefaultExpectation.MarkForDeletion.Copyable = false;
                DefaultExpectation.MarkForDeletion.Label = EditorConstant.Control.MarkForDeletionLabel;
                DefaultExpectation.MarkForDeletion.List = " ";
                DefaultExpectation.MarkForDeletion.Tooltip = Constants.ControlDefault.MarkForDeletionTooltip;
                DefaultExpectation.MarkForDeletion.Type = Constants.Control.DeleteFlag;

                // controls
                DefaultExpectation.Counter0 = ControlExpectations.CreateCounter(TestConstant.DefaultDatabaseColumn.Counter0, 8);
                DefaultExpectation.Counter0.List = " ";
                DefaultExpectation.Choice0 = ControlExpectations.CreateChoice(TestConstant.DefaultDatabaseColumn.Choice0, 9);
                DefaultExpectation.Choice0.DefaultValue = " ";
                DefaultExpectation.Choice0.List = "choice a|choice b|choice c";
                DefaultExpectation.Note0 = ControlExpectations.CreateNote(TestConstant.DefaultDatabaseColumn.Note0, 10);
                DefaultExpectation.Note0.DefaultValue = " ";
                DefaultExpectation.Note0.List = " ";
                DefaultExpectation.Flag0 = ControlExpectations.CreateFlag(TestConstant.DefaultDatabaseColumn.Flag0, 11);
                DefaultExpectation.Flag0.List = " ";
                DefaultExpectation.CounterWithCustomDataLabel = ControlExpectations.CreateCounter(TestConstant.DefaultDatabaseColumn.CounterWithCustomDataLabel, 12);
                DefaultExpectation.CounterWithCustomDataLabel.DefaultValue = "100";
                DefaultExpectation.CounterWithCustomDataLabel.Label = "CounterWithCustomLabel";
                DefaultExpectation.CounterWithCustomDataLabel.List = " ";
                DefaultExpectation.CounterWithCustomDataLabel.TextBoxWidth = "75";
                DefaultExpectation.CounterWithCustomDataLabel.Tooltip = "Counter with custom label.";
                DefaultExpectation.ChoiceWithCustomDataLabel = ControlExpectations.CreateChoice(TestConstant.DefaultDatabaseColumn.ChoiceWithCustomDataLabel, 13);
                DefaultExpectation.ChoiceWithCustomDataLabel.DefaultValue = " ";
                DefaultExpectation.ChoiceWithCustomDataLabel.Label = "ChoiceWithCustomLabel";
                DefaultExpectation.ChoiceWithCustomDataLabel.List = " ";
                DefaultExpectation.ChoiceWithCustomDataLabel.List = "value|Genus species|Genus species subspecies|with , comma|with ' apostrophe";
                DefaultExpectation.ChoiceWithCustomDataLabel.TextBoxWidth = "90";
                DefaultExpectation.ChoiceWithCustomDataLabel.Tooltip = "Choice with custom label and some values of interest.";
                DefaultExpectation.NoteWithCustomDataLabel = ControlExpectations.CreateNote(TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel, 14);
                DefaultExpectation.NoteWithCustomDataLabel.DefaultValue = " ";
                DefaultExpectation.NoteWithCustomDataLabel.Label = "NoteWithCustomLabel";
                DefaultExpectation.NoteWithCustomDataLabel.List = " ";
                DefaultExpectation.NoteWithCustomDataLabel.TextBoxWidth = "200";
                DefaultExpectation.NoteWithCustomDataLabel.Tooltip = "Note with custom label.";
                DefaultExpectation.FlagWithCustomDataLabel = ControlExpectations.CreateFlag(TestConstant.DefaultDatabaseColumn.FlagWithCustomDataLabel, 15);
                DefaultExpectation.FlagWithCustomDataLabel.DefaultValue = Constants.Boolean.True;
                DefaultExpectation.FlagWithCustomDataLabel.Label = "FlagWithCustomLabel";
                DefaultExpectation.FlagWithCustomDataLabel.List = " ";
                DefaultExpectation.FlagWithCustomDataLabel.TextBoxWidth = "30";
                DefaultExpectation.FlagWithCustomDataLabel.Tooltip = "Flag with custom label.";

                DefaultExpectation.CounterNotVisible = ControlExpectations.CreateCounter(TestConstant.DefaultDatabaseColumn.CounterNotVisible, 16);
                DefaultExpectation.CounterNotVisible.DefaultValue = "3";
                DefaultExpectation.CounterNotVisible.Label = "InvisibleCounter";
                DefaultExpectation.CounterNotVisible.List = " ";
                DefaultExpectation.CounterNotVisible.TextBoxWidth = "111";
                DefaultExpectation.CounterNotVisible.Tooltip = "Counter which can't be seen.";
                DefaultExpectation.CounterNotVisible.Visible = false;
                DefaultExpectation.ChoiceNotVisible = ControlExpectations.CreateChoice(TestConstant.DefaultDatabaseColumn.ChoiceNotVisible, 17);
                DefaultExpectation.ChoiceNotVisible.DefaultValue = " ";
                DefaultExpectation.ChoiceNotVisible.Label = "InvisibleChoice";
                DefaultExpectation.ChoiceNotVisible.List = "you can't see me";
                DefaultExpectation.ChoiceNotVisible.TextBoxWidth = "150";
                DefaultExpectation.ChoiceNotVisible.Tooltip = "Choice which can't be seen.";
                DefaultExpectation.ChoiceNotVisible.Visible = false;
                DefaultExpectation.NoteNotVisible = ControlExpectations.CreateNote(TestConstant.DefaultDatabaseColumn.NoteNotVisible, 18);
                DefaultExpectation.NoteNotVisible.DefaultValue = " ";
                DefaultExpectation.NoteNotVisible.Label = "InvisibleNote";
                DefaultExpectation.NoteNotVisible.List = " ";
                DefaultExpectation.NoteNotVisible.TextBoxWidth = "32";
                DefaultExpectation.NoteNotVisible.Tooltip = "Note which can't be seen.";
                DefaultExpectation.NoteNotVisible.Visible = false;
                DefaultExpectation.FlagNotVisible = ControlExpectations.CreateFlag(TestConstant.DefaultDatabaseColumn.FlagNotVisible, 19);
                DefaultExpectation.FlagNotVisible.Label = "InvisibleFlag";
                DefaultExpectation.FlagNotVisible.List = " ";
                DefaultExpectation.FlagNotVisible.TextBoxWidth = "17";
                DefaultExpectation.FlagNotVisible.Tooltip = "Flag which can't be seen.";
                DefaultExpectation.FlagNotVisible.Visible = false;

                DefaultExpectation.Counter3 = ControlExpectations.CreateCounter(TestConstant.DefaultDatabaseColumn.Counter3, 20);
                DefaultExpectation.Counter3.Copyable = true;
                DefaultExpectation.Counter3.Label = "CopyableCounter";
                DefaultExpectation.Counter3.List = " ";
                DefaultExpectation.Counter3.Tooltip = "Counter which can be copied (rather than the default of not being copyable).";
                DefaultExpectation.Choice3 = ControlExpectations.CreateChoice(TestConstant.DefaultDatabaseColumn.Choice3, 21);
                DefaultExpectation.Choice3.Copyable = false;
                DefaultExpectation.Choice3.DefaultValue = " ";
                DefaultExpectation.Choice3.Label = "NotCopyableChoice";
                DefaultExpectation.Choice3.List = " ";
                DefaultExpectation.Choice3.Tooltip = "Choice which can't be copied (rather than the default of copyable).";
                DefaultExpectation.Note3 = ControlExpectations.CreateNote(TestConstant.DefaultDatabaseColumn.Note3, 22);
                DefaultExpectation.Note3.Copyable = false;
                DefaultExpectation.Note3.DefaultValue = " ";
                DefaultExpectation.Note3.Label = "NotCopyableNote";
                DefaultExpectation.Note3.List = " ";
                DefaultExpectation.Note3.Tooltip = "Note which can't be copied (rather than the default of copyable).";
                DefaultExpectation.Flag3 = ControlExpectations.CreateFlag(TestConstant.DefaultDatabaseColumn.Flag3, 23);
                DefaultExpectation.Flag3.Copyable = false;
                DefaultExpectation.Flag3.Label = "NotCopyableFlag";
                DefaultExpectation.Flag3.List = " ";
                DefaultExpectation.Flag3.Tooltip = "Flag which can't be copied (rather than the default of copyable).";

                // images
                DefaultExpectation.DaylightBobcatImage = new ImageExpectations()
                {
                    DarkPixelFraction = 0.24222145485288338,
                    Date = "05-Aug-2015",
                    FileName = TestConstant.File.DaylightBobcatImage,
                    IsColor = true,
                    Quality = ImageQualityFilter.Ok,
                    Time = "08:06:23"
                };

                DefaultExpectation.DaylightCoyoteImage = new ImageExpectations()
                {
                    DarkPixelFraction = 0.60847930235235814,
                    Date = "21-Apr-2016",
                    FileName = TestConstant.File.DaylightCoyoteImage,
                    IsColor = true,
                    Quality = ImageQualityFilter.Ok,
                    RelativePath = TestConstant.File.CarnivoreDirectoryName,
                    Time = "06:31:13"
                };

                DefaultExpectation.DaylightMartenPairImage = new ImageExpectations()
                {
                    DarkPixelFraction = 0.70253739978510621,
                    Date = "28-Jan-2015",
                    FileName = TestConstant.File.DaylightMartenPairImage,
                    IsColor = true,
                    Quality = ImageQualityFilter.Ok,
                    RelativePath = TestConstant.File.CarnivoreDirectoryName,
                    SkipDateTimeVerification = true,
                    Time = "11:17:34"
                };

                DefaultExpectation.InfraredMartenImage = new ImageExpectations()
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
            public const string DefaultNewImageDatabaseFileName = "DefaultUnitTest.ddb";
            public const string DefaultNewTemplateDatabaseFileName = "DefaultUnitTest.tdb";

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
