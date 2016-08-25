using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Timelapse.Database;
using Timelapse.Util;

namespace Timelapse.UnitTests
{
    internal static class TestConstant
    {
        public const double DarkPixelFractionTolerance = 0.00000001;
        public const string DataHandlerFieldName = "dataHandler";
        public const string DateTimeWithOffsetFormat = "yyyy-MM-ddTHH:mm:ss.fffK";
        public const string FileCountsAutomationID = "FileCountsByQuality";
        public const string InitializeDataGridMethodName = "InitializeDataGrid";
        public const string MessageBoxAutomationID = "TimelapseMessageBox";
        public const string OkButtonAutomationID = "OkButton";
        public const string TimelapseAutomationID = "Timelapse";
        public const string TrySaveDatabaseBackupFileMethodName = "TrySaveDatabaseBackupFile";
        public const string TryShowImageWithoutSliderCallbackMethodName = "TryShowImageWithoutSliderCallback";

        public static readonly TimeSpan UIElementSearchTimeout = TimeSpan.FromSeconds(15.0);
        public static readonly Version Version2104 = new Version(2, 1, 0, 4);

        public static readonly ReadOnlyCollection<string> DefaultImageTableColumns = new List<string>()
        {
            Constants.DatabaseColumn.ID,
            Constants.DatabaseColumn.File,
            Constants.DatabaseColumn.RelativePath,
            Constants.DatabaseColumn.Folder,
            Constants.DatabaseColumn.DateTime,
            Constants.DatabaseColumn.UtcOffset,
            Constants.DatabaseColumn.Date,
            Constants.DatabaseColumn.Time,
            Constants.DatabaseColumn.ImageQuality,
            Constants.DatabaseColumn.DeleteFlag,
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

        public static class Exif
        {
            public const string DateTimeOriginal = "Date/Time Original";
            public const string ExposureTime = "Exposure Time";
            public const string ShutterSpeed = "Shutter Speed";

            public static class Bushnell
            {
                public const string CreateDate = "Create Date";
                public const string ModifyDate = "Modify Date";
                public const string Software = "Software";
            }

            public static class Reconyx
            {
                public const string AmbientTemperature = "Ambient Temperature";
                public const string FirmwareVersion = "Firmware Version";
                public const string InfraredIlluminator = "Infrared Illuminator";
                public const string Sequence = "Sequence";
                public const string SerialNumber = "Serial Number";
                public const string TriggerMode = "Trigger Mode";
                public const string UserLabel = "User Label";
            }
        }

        public static class File
        {
            // template databases for backwards compatibility testing
            // version is the editor version used for creation
            public const string CarnivoreDirectoryName = "CarnivoreTestImages";
            public const string CarnivoreTemplateDatabaseFileName = "CarnivoreTemplate 2.0.1.5.tdb";
            public const string DefaultTemplateDatabaseFileName2015 = "TimelapseTemplate 2.0.1.5.tdb";
            public const string DefaultTemplateDatabaseFileName2104 = "TimelapseTemplate 2.1.0.4.tdb";
            public const string HybridVideoDirectoryName = "HybridVideo";

            // image databases for backwards compatibility testing
            // version is the Timelapse version used for creation
            public const string DefaultImageDatabaseFileName2023 = "TimelapseData 2.0.2.3.ddb";
            public const string DefaultImageDatabaseFileName2104 = "TimelapseData 2.1.0.4.ddb";

            // databases generated dynamically by tests
            // see also use of Constants.File.Default*DatabaseFileName
            public const string CarnivoreNewImageDatabaseFileName = "CarnivoreDatabaseTest.ddb";
            public const string CarnivoreNewImageDatabaseFileName2104 = "CarnivoreDatabaseTest2104.ddb";
            public const string DefaultNewImageDatabaseFileName = "DefaultUnitTest.ddb";
            public const string DefaultNewTemplateDatabaseFileName = "DefaultUnitTest.tdb";
        }

        public static class ImageExpectation
        {
            public static readonly ImageExpectations DaylightBobcat;
            public static readonly ImageExpectations DaylightCoyote;
            public static readonly ImageExpectations DaylightMartenPair;
            public static readonly ImageExpectations InfraredMarten;

            static ImageExpectation()
            {
                TimeZoneInfo pacificTime = TimeZoneInfo.FindSystemTimeZoneById(TestConstant.TimeZone.Pacific);

                ImageExpectation.DaylightBobcat = new ImageExpectations(pacificTime)
                {
                    DarkPixelFraction = 0.242364344315876,
                    DateTime = ImageExpectations.ParseDateTimeOffsetString("2015-08-05T08:06:23.000-07:00"),
                    FileName = "BushnellTrophyHD-119677C-20160805-926.JPG",
                    IsColor = true,
                    Quality = ImageFilter.Ok
                };

                ImageExpectation.DaylightCoyote = new ImageExpectations(pacificTime)
                {
                    DarkPixelFraction = 0.610071236552411,
                    DateTime = ImageExpectations.ParseDateTimeOffsetString("2016-04-21T06:31:13.000-07:00"),
                    FileName = "BushnellTrophyHDAggressor-119777C-20160421-112.JPG",
                    IsColor = true,
                    Quality = ImageFilter.Ok,
                    RelativePath = TestConstant.File.CarnivoreDirectoryName
                };

                ImageExpectation.DaylightMartenPair = new ImageExpectations(pacificTime)
                {
                    DarkPixelFraction = 0.705627292783256,
                    DateTime = ImageExpectations.ParseDateTimeOffsetString("2015-01-28T11:17:34.000-08:00"),
                    FileName = "Reconyx-HC500-20150128-201.JPG",
                    IsColor = true,
                    Quality = ImageFilter.Ok,
                    RelativePath = TestConstant.File.CarnivoreDirectoryName,
                    SkipDateTimeVerification = true
                };

                ImageExpectation.InfraredMarten = new ImageExpectations(pacificTime)
                {
                    DarkPixelFraction = 0.077128711384332,
                    DateTime = ImageExpectations.ParseDateTimeOffsetString("2016-02-24T04:59:46.000-08:00"),
                    FileName = "BushnellTrophyHD-119677C-20160224-056.JPG",
                    IsColor = false,
                    Quality = ImageFilter.Ok
                };
            }
        }
        
        public static class TimeZone
        {
            public const string Alaska = "Alaskan Standard Time"; // UTC-9
            public const string Arizona = "US Mountain Standard Time"; // UTC-7
            public const string CapeVerde = "Cape Verde Standard Time"; // UTC-1
            public const string Dateline = "Dateline Standard Time"; // UTC-12
            public const string Gmt = "GMT Standard Time"; // UTC+0
            public const string LineIslands = "Line Islands Standard Time"; // UTC+14
            public const string Mountain = "Mountain Standard Time"; // UTC-7
            public const string Pacific = "Pacific Standard Time"; // UTC-8
            public const string Utc = "UTC";
            public const string WestCentralAfrica = "W. Central Africa Standard Time"; // UTC+1
        }
    }
}
