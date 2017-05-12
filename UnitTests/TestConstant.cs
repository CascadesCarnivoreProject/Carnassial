using Carnassial.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Carnassial.UnitTests
{
    internal static class TestConstant
    {
        public const string CarnassialAutomationID = "Carnassial";
        public const string CarnassialTemplateEditorAutomationID = "CarnassialTemplateEditor";
        public const double DarkPixelFractionTolerance = 0.00000001;
        public const string DataHandlerFieldName = "dataHandler";
        public const string DateTimeWithOffsetFormat = "yyyy-MM-ddTHH:mm:ss.fffK";
        public const string FileCountsAutomationID = "FileCountsByQuality";
        public const string InitializeDataGridMethodName = "InitializeDataGrid";
        public const string MessageBoxAutomationID = "CarnassialMessageBox";
        public const string OkButtonAutomationID = "OkButton";
        public const string PerformanceIntervalFormat = @"s\.fff";
        public const string TemplatePaneAutomationID = "TemplatePane";

        public static readonly TimeSpan UIElementSearchTimeout = TimeSpan.FromSeconds(15.0);

        public static readonly ReadOnlyCollection<string> ControlsColumns = new List<string>()
            {
                Constant.Control.ControlOrder,
                Constant.Control.SpreadsheetOrder,
                Constant.Control.DefaultValue,
                Constant.Control.Label,
                Constant.Control.DataLabel,
                Constant.Control.Tooltip,
                Constant.Control.Width,
                Constant.Control.Copyable,
                Constant.Control.Visible,
                Constant.Control.List,
                Constant.DatabaseColumn.ID,
                Constant.Control.Type
            }.AsReadOnly();

        public static readonly ReadOnlyCollection<string> DefaultFileDataColumns = new List<string>()
        {
            Constant.DatabaseColumn.ID,
            Constant.DatabaseColumn.File,
            Constant.DatabaseColumn.RelativePath,
            Constant.DatabaseColumn.DateTime,
            Constant.DatabaseColumn.UtcOffset,
            Constant.DatabaseColumn.ImageQuality,
            Constant.DatabaseColumn.DeleteFlag,
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

        public static readonly ReadOnlyCollection<string> DefaultMarkerColumns = new List<string>()
        {
            Constant.DatabaseColumn.ID,
            TestConstant.DefaultDatabaseColumn.Counter0,
            TestConstant.DefaultDatabaseColumn.CounterWithCustomDataLabel,
            TestConstant.DefaultDatabaseColumn.CounterNotVisible,
            TestConstant.DefaultDatabaseColumn.Counter3
        }.AsReadOnly();

        public static readonly ReadOnlyCollection<string> ImageSetColumns = new List<string>()
        {
            Constant.DatabaseColumn.ID,
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
            public const string DateTime = "Exif IFD0.Date/Time";
            public const string DateTimeDigitized = "Exif SubIFD.Date/Time Digitized";
            public const string DateTimeFormat = "yyyy:MM:dd HH:mm:ss";
            public const string DateTimeOriginal = "Exif SubIFD.Date/Time Original";
            public const string ExposureTime = "Exif SubIFD.Exposure Time";
            public const string ShutterSpeed = "Exif SubIFD.Shutter Speed Value";
            public const string Software = "Exif IFD0.Software";

            public static class Reconyx
            {
                public const string AmbientTemperature = "Reconyx Makernote.Ambient Temperature";
                public const string AmbientTemperatureFarenheit = "Reconyx Makernote.Ambient Temperature Farenheit";
                public const string BatteryVoltage = "Reconyx Makernote.Battery Voltage";
                public const string Brightness = "Reconyx Makernote.Brightness";
                public const string Contrast = "Reconyx Makernote.Contrast";
                public const string DateTimeOriginal = "Reconyx Makernote.Date/Time Original";
                public const string FirmwareVersion = "Reconyx Makernote.Firmware Version";
                public const string InfraredIlluminator = "Reconyx Makernote.Infrared Illuminator";
                public const string MoonPhase = "Reconyx Makernote.Moon Phase";
                public const string MotionSensitivity = "Reconyx Makernote.Motion Sensitivity";
                public const string Saturation = "Reconyx Makernote.Saturation";
                public const string Sequence = "Reconyx Makernote.Sequence";
                public const string SerialNumber = "Reconyx Makernote.Serial Number";
                public const string Sharpness = "Reconyx Makernote.Sharpness";
                public const string TriggerMode = "Reconyx Makernote.Trigger Mode";
                public const string UserLabel = "Reconyx Makernote.UserLabel";

                // pending more information from Reconyx
                // public const string EventNumber = "Reconyx Makernote.Event Number";
                // public const string FirmwareDate = "Reconyx Makernote.Firmware Date";
                // public const string MakernoteVersion = "Reconyx Makernote.Makernote Version";
            }
        }

        public static class File
        {
            public const string CarnivoreDirectoryName = "CarnivoreTestImages";
            public const string HybridVideoDirectoryName = "HybridVideo";

            // file databases for backwards compatibility testing
            // version is the Carnassial version used for creation
            public const string DefaultFileDatabaseFileName = "DefaultData 2.2.0.0.ddb";

            // template databases for backwards compatibility testing
            // version is the editor version used for creation
            public const string DefaultTemplateDatabaseFileName = "DefaultTemplate 2.2.0.0.tdb";

            // databases generated dynamically by tests
            // see also use of Constants.File.Default*DatabaseFileName
            public const string DefaultNewFileDatabaseFileName = "DefaultUnitTest.ddb";
            public const string DefaultNewTemplateDatabaseFileName = "DefaultUnitTest.tdb";
        }

        public static class FileExpectation
        {
            public static readonly FileExpectations CorruptFieldScan;
            public static readonly FileExpectations DaylightBobcat;
            public static readonly FileExpectations DaylightCoyote;
            public static readonly FileExpectations DaylightMartenPair;
            public static readonly FileExpectations InfraredMarten;

            public static readonly DateTime HybridVideoFileDate = new DateTime(2016, 06, 26);

            static FileExpectation()
            {
                TimeZoneInfo pacificTime = TimeZoneInfo.FindSystemTimeZoneById(TestConstant.TimeZone.Pacific);

                FileExpectation.CorruptFieldScan = new FileExpectations(pacificTime)
                {
                    FileName = "BushnellTrophyHD-119677C-20170403-179.JPG",
                    IsColor = false,
                    Quality = FileSelection.Corrupt
                };

                FileExpectation.DaylightBobcat = new FileExpectations(pacificTime)
                {
                    DarkPixelFraction = 0.243754514928039,
                    DateTime = FileExpectations.ParseDateTimeOffsetString("2015-08-05T08:06:23.000-07:00"),
                    FileName = "BushnellTrophyHD-119677C-20160805-926.JPG",
                    IsColor = true,
                    Quality = FileSelection.Ok
                };

                FileExpectation.DaylightCoyote = new FileExpectations(pacificTime)
                {
                    DarkPixelFraction = 0.608317365996393,
                    DateTime = FileExpectations.ParseDateTimeOffsetString("2016-04-21T06:31:13.000-07:00"),
                    FileName = "BushnellTrophyHDAggressor-119777C-20160421-112.JPG",
                    IsColor = true,
                    Quality = FileSelection.Ok,
                    RelativePath = TestConstant.File.CarnivoreDirectoryName
                };

                FileExpectation.DaylightMartenPair = new FileExpectations(pacificTime)
                {
                    DarkPixelFraction = 0.702360667828033,
                    DateTime = FileExpectations.ParseDateTimeOffsetString("2015-01-28T11:17:34.000-08:00"),
                    FileName = "Reconyx-HC500-20150128-201.JPG",
                    IsColor = true,
                    Quality = FileSelection.Ok,
                    RelativePath = TestConstant.File.CarnivoreDirectoryName,
                };

                FileExpectation.InfraredMarten = new FileExpectations(pacificTime)
                {
                    DarkPixelFraction = 0.0742653252017767,
                    DateTime = FileExpectations.ParseDateTimeOffsetString("2016-02-24T04:59:46.000-08:00"),
                    FileName = "BushnellTrophyHD-119677C-20160224-056.JPG",
                    IsColor = false,
                    Quality = FileSelection.Ok
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
