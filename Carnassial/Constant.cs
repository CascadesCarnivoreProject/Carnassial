using Carnassial.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xceed.Wpf.Toolkit;

namespace Carnassial
{
    public static class Constant
    {
        public const string ApplicationName = "Carnassial";
        public const string MainWindowBaseTitle = "Carnassial: Simplifying Remote Camera Data";
        public const int NumberOfMostRecentDatabasesToTrack = 9;
        public const string StandardColour = "Gold";
        public const string SelectionColour = "MediumBlue";

        public static readonly TimeSpan CheckForUpdateInterval = TimeSpan.FromDays(1.25);

        public static class ApplicationSettings
        {
            public const string GithubOrganizationAndRepo = "githubOrganizationAndRepo";
        }

        public static class Control
        {
            // columns unique to the template table
            public const string ControlOrder = "ControlOrder";
            public const string Copyable = "Copyable";     // whether the content of this item should be copied from previous values
            public const string DataLabel = "DataLabel";   // if not empty, its used instead of the label as the header for the column when writing the spreadsheet
            public const string DefaultValue = "DefaultValue"; // a default value for that code
            public const string Label = "Label";           // a label used to describe that code
            public const string List = "List";             // a fixed list of choices
            public const string SpreadsheetOrder = "SpreadsheetOrder";
            public const string Tooltip = "Tooltip";       // the tooltip text that describes the code
            public const string Type = "Type";             // the data type
            public const string Visible = "Visible";       // whether an item should be visible (used by standard items)
            public const string Width = "Width";           // the width of the textbox

            // control types
            public const string Counter = "Counter";       // a counter
            public const string FixedChoice = "FixedChoice";  // a fixed choice
            public const string Flag = "Flag";             // A boolean
            public const string Note = "Note";             // A note

            // default data labels
            public const string Choice = "Choice";         // Label for a fixed choice

            // a minty green
            public static readonly SolidColorBrush CopyableFieldHighlightBrush = new SolidColorBrush(Color.FromArgb(255, 200, 251, 200));

            public static readonly ReadOnlyCollection<Type> KeyboardInputTypes = new List<Type>()
            {
                typeof(AutocompleteTextBox), // note controls
                typeof(Calendar),            // date time control
                typeof(CalendarDayButton),   // date time control
                typeof(CheckBox),            // flag controls
                typeof(ComboBox),            // choice controls
                typeof(ComboBoxItem),        // choice controls
                typeof(TextBox),             // counter controls
                typeof(WatermarkTextBox)     // date time control
            }.AsReadOnly();

            public static readonly ReadOnlyCollection<string> StandardTypes = new List<string>()
            {
                Constant.DatabaseColumn.DateTime,
                Constant.DatabaseColumn.DeleteFlag,
                Constant.DatabaseColumn.File,
                Constant.DatabaseColumn.ImageQuality,
                Constant.DatabaseColumn.RelativePath,
                Constant.DatabaseColumn.UtcOffset
            }.AsReadOnly();
        }

        // see also ControlLabelStyle and ControlContentStyle
        public static class ControlStyle
        {
            public const string ComboBoxCodeBar = "ComboBoxCodeBar";
            public const string StackPanelCodeBar = "StackPanelCodeBar";
        }

        public static class ControlDefault
        {
            // general defaults
            public const string Value = "";

            // user defined controls
            public const string CounterTooltip = "Click the counter button, then click on the image to count the entity. Or just type in a count";
            public const string CounterValue = "0";              // Default for: counters
            public const int CounterWidth = 80;
            public const string FixedChoiceTooltip = "Choose an item from the menu";
            public const int FixedChoiceWidth = 100;

            public const string FlagTooltip = "Toggle between true and false";
            public const string FlagValue = "False"; // can't use Boolean.FalseString as it's not const
            public const int FlagWidth = 20;
            public const string NoteTooltip = "Write a textual note";
            public const int NoteWidth = 100;

            // standard controls
            public const string DateTimeTooltip = "Date and time taken";
            public const string DateTimeWidth = "188";

            public const string FileTooltip = "The file name";
            public const string FileWidth = "221";
            public const string RelativePathTooltip = "Path from the folder containing the template and image data files to the file";
            public const string RelativePathWidth = "172";
            public const string FolderTooltip = "Name of the folder originally containing the template and image data files";
            public const string FolderWidth = "206";

            public const string ImageQualityTooltip = "System-determined image quality: Ok, dark if mostly black, corrupted if it can not be read, missing if the image/video file is missing";
            public const string ImageQualityWidth = "166";

            public const string DeleteFlagLabel = "Delete?";    // a flag data type for marking deletion
            public const string DeleteFlagTooltip = "Mark a file as one to be deleted. You can then confirm deletion through the Edit Menu";

            public const string UtcOffsetTooltip = "Universal Time offset of the time zone for date and time taken";
            public const string UtcOffsetWidth = "188";

            public static readonly DateTimeOffset DateTimeValue = new DateTimeOffset(1900, 1, 1, 12, 0, 0, 0, TimeSpan.Zero);
        }

        public static class Database
        {
            // default values
            public const long DefaultFileID = 1;
            public const string ImageSetDefaultLog = "Add text here";
            public const long ImageSetRowID = 1;
            public const int InvalidRow = -1;
            public const int RowsPerInsert = 100;

            // Special characters
            public const char MarkerBar = '|';              // Separator used to separate marker points in the database i.e. "2.3,5.6 | 7.1, 3.3"
        }

        // Names of standard database columns, always included but not always made visible in the user controls
        public static class DatabaseColumn
        {
            public const string ID = "Id";

            // columns in FileData
            public const string DateTime = "DateTime";
            public const string File = "File";
            public const string ImageQuality = "ImageQuality";
            public const string DeleteFlag = "DeleteFlag";
            public const string RelativePath = "RelativePath";
            public const string UtcOffset = "UtcOffset";

            // columns in ImageSet
            public const string FileSelection = "FileSelection";
            public const string InitialFolderName = "InitialFolderName";
            public const string Log = "Log";
            public const string Magnifier = "Magnifier";
            public const string MostRecentFileID = "MostRecentFileID";
            public const string TimeZone = "TimeZone";
        }

        public static class DatabaseTable
        {
            public const string Controls = "Controls"; // table containing controls
            public const string FileData = "FileData";     // table containing image and video data
            public const string ImageSet = "ImageSet"; // table containing information common to the entire image set
            public const string Markers = "Markers";   // table containing counter markers
        }

        public static class File
        {
            public const string AviFileExtension = ".avi";
            public const string BackupFolder = "Backups"; // Sub-folder that will contain database and csv file backups  
            public const int NumberOfBackupFilesToKeep = 9; // Maximum number of backup files to keep
            public const string CsvFileExtension = ".csv";
            public const string DefaultFileDatabaseFileName = "CarnassialData.ddb";
            public const string DefaultTemplateDatabaseFileName = "CarnassialTemplate.tdb";
            public const string DeletedFilesFolder = "DeletedFiles"; // Sub-folder that will contain backups of deleted files
            public const string FileDatabaseFileExtension = ".ddb";
            public const string JpgFileExtension = ".jpg";
            public const string Mp4FileExtension = ".mp4";
            public const string TemplateDatabaseFileExtension = ".tdb";

            public static readonly TimeSpan BackupInterval = TimeSpan.FromMinutes(10);
        }

        public static class GitHub
        {
            public static readonly Uri ApiBaseAddress = new Uri("https://api.github.com/repos/");
            public static readonly Uri BaseAddress = new Uri("https://github.com/");
        }

        // shorthands for FileSelection.<value>.ToString()
        public static class ImageQuality
        {
            public const string Dark = "Dark";
            public const string Ok = "Ok";

            public const string ListOfValues = "Ok|Dark|Corrupt|NoLongerAvailable";
        }

        public static class Images
        {
            public const int BitmapCacheSize = 9;

            // The default threshold where the ratio of pixels below a given darkness in an image is used to determine whether the image is classified as 'dark'
            public const double DarkPixelRatioThresholdDefault = 0.9;
            public const int DarkPixelSampleStrideDefault = 20;
            // The default threshold where a pixel color should be considered as 'dark' when checking image darkness. The Range is 0  (black) - 255 (white)
            public const int DarkPixelThresholdDefault = 60;

            // The threshold to determine differences between images
            public const int DifferenceThresholdDefault = 20;
            public const byte DifferenceThresholdMax = 255;
            public const byte DifferenceThresholdMin = 0;

            // A greyscale image (given the above slop) will typically have about 90% of its pixels as grey scale
            public const double GreyScaleImageThreshold = 0.9;
            // A greyscale pixel has r = g = b. But we will allow some slop in here just in case a bit of color creeps in
            public const int GreyScalePixelThreshold = 40;

            public const int LargeNumberOfDeletedImages = 30;

            public const int ThumbnailWidth = 300;

            public static readonly BitmapImage CorruptFile;
            public static readonly BitmapImage FileNoLongerAvailable;
            public static readonly BitmapImage NoSelectableFile;

            static Images()
            {
                Images.CorruptFile = new BitmapImage();
                Images.CorruptFile.BeginInit();
                Images.CorruptFile.CacheOption = BitmapCacheOption.None;
                Images.CorruptFile.UriSource = new Uri("pack://application:,,/Resources/CorruptFile.jpg");
                Images.CorruptFile.EndInit();
                Images.CorruptFile.Freeze();

                Images.FileNoLongerAvailable = new BitmapImage();
                Images.FileNoLongerAvailable.BeginInit();
                Images.FileNoLongerAvailable.CacheOption = BitmapCacheOption.None;
                Images.FileNoLongerAvailable.UriSource = new Uri("pack://application:,,/Resources/FileNoLongerAvailable.jpg");
                Images.FileNoLongerAvailable.EndInit();
                Images.FileNoLongerAvailable.Freeze();

                Images.NoSelectableFile = new BitmapImage();
                Images.NoSelectableFile.BeginInit();
                Images.NoSelectableFile.CacheOption = BitmapCacheOption.None;
                Images.NoSelectableFile.UriSource = new Uri("pack://application:,,/Resources/NoSelectableFile.jpg");
                Images.NoSelectableFile.EndInit();
                Images.NoSelectableFile.Freeze();
            }
        }

        public static class MarkableCanvas
        {
            public const double MagnifyingGlassDefaultZoom = 60;
            public const double MagnifyingGlassMaximumZoom = 100;
            public const double MagnifyingGlassMinimumZoom = 15;
            public const double MagnifyingGlassZoomIncrement = 1.2;

            public const int MagnifyingGlassDiameter = 250;
            public const int MagnifyingGlassHandleStart = 200;
            public const int MagnifyingGlassHandleEnd = 250;

            public const int MarkerDiameter = 10;
            public const int MarkerGlowDiameterIncrease = 14;
            public const int MarkerStrokeThickness = 2;
            public const double MarkerGlowOpacity = 0.35;
            public const int MarkerGlowStrokeThickness = 7;

            public const double ZoomAbsoluteMaximum = 50; // the highest zoom a user can configure for a display image
            public const double ZoomMaximum = 10;         // user configurable maximum amount of zoom in a display image
            public const double ZoomMinimum = 1;          // minimum amount of zoom
        }

        public static class Registry
        {
            public static class CarnassialKey
            {
                public const string AudioFeedback = "AudioFeedback";
                public const string CarnassialWindowPosition = "CarnassialWindowPosition";

                // most recently used operator for custom selections
                public const string CustomSelectionTermCombiningOperator = "CustomSelectionTermCombiningOperator";
                // the DarkPixelThreshold
                public const string DarkPixelThreshold = "DarkPixelThreshold";
                // the DarkPixelRatio
                public const string DarkPixelRatio = "DarkPixelRatio";
                // the value for rendering
                public const string DesiredImageRendersPerSecond = "DesiredImageRendersPerSecond";
                public const string MostRecentCheckForUpdates = "MostRecentCheckForUpdates";
                // key containing the list of most recently image sets opened by Carnassial
                public const string MostRecentlyUsedImageSets = "MostRecentlyUsedImageSets";

                public const string OrderFilesByDateTime = "OrderFilesByDateTime";
                public const string SkipDarkImagesCheck = "SkipDarkImagesCheck";

                // dialog opt outs
                public const string SuppressAmbiguousDatesDialog = "SuppressAmbiguousDatesDialog";
                public const string SuppressCsvExportDialog = "SuppressCsvExportDialog";
                public const string SuppressCsvImportPrompt = "SuppressCsvImportPrompt";
                public const string SuppressFileCountOnImportDialog = "SuppressFileCountOnImportDialog";
                public const string SuppressSelectedAmbiguousDatesPrompt = "SuppressSelectedAmbiguousDatesPrompt";
                public const string SuppressSelectedCsvExportPrompt = "SuppressSelectedCsvExportPrompt";
                public const string SuppressSelectedDarkThresholdPrompt = "SuppressSelectedDarkThresholdPrompt";
                public const string SuppressSelectedDateTimeFixedCorrectionPrompt = "SuppressSelectedDateTimeFixedCorrectionPrompt";
                public const string SuppressSelectedDateTimeLinearCorrectionPrompt = "SuppressSelectedDateTimeLinearCorrectionPrompt";
                public const string SuppressSelectedDaylightSavingsCorrectionPrompt = "SuppressSelectedDaylightSavingsCorrectionPrompt";
                public const string SuppressSelectedPopulateFieldFromMetadataPrompt = "SuppressSelectedPopulateFieldFromMetadataPrompt";
                public const string SuppressSelectedRereadDatesFromFilesPrompt = "SuppressSelectedRereadDatesFromFilesPrompt";
                public const string SuppressSelectedSetTimeZonePrompt = "SuppressSelectedSetTimeZonePrompt";
            }

            public const string RootKey = @"Software\Cascades Carnivore Project\Carnassial\2.0";
        }

        public static class SearchTermOperator
        {
            public const string Equal = "\u003D";
            public const string Glob = " GLOB ";
            public const string GreaterThan = "\u003E";
            public const string GreaterThanOrEqual = "\u2265";
            public const string LessThan = "\u003C";
            public const string LessThanOrEqual = "\u2264";
            public const string NotEqual = "\u2260";
        }

        public static class Sql
        {
            public const string CreationStringPrimaryKey = "INTEGER PRIMARY KEY AUTOINCREMENT";
            public const string Integer = "INTEGER";
            public const string DeleteFrom = "DELETE FROM ";
            public const string Where = " WHERE ";
            public const string End = "END";
            public const string Text = "TEXT";

            public const string Comma = ", ";
        }

        public static class ThrottleValues
        {
            public const double DesiredMaximumImageRendersPerSecondLowerBound = 2.0;
            public const double DesiredMaximumImageRendersPerSecondDefault = 6.0;
            public const double DesiredMaximumImageRendersPerSecondUpperBound = 12.0;
            public const int MaximumRenderAttempts = 100;
            public const int SleepForImageRenderInterval = 100;

            public static readonly TimeSpan PollIntervalForVideoLoad = TimeSpan.FromMilliseconds(1.0);
            public static readonly TimeSpan RenderingBackoffTime = TimeSpan.FromMilliseconds(25.0);
        }

        public static class Time
        {
            // The standard date format, e.g., 05-Apr-2011
            public const string DateFormat = "dd-MMM-yyyy";
            public const string DateTimeDatabaseFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
            public const string DateTimeDisplayFormat = "dd-MMM-yyyy HH:mm:ss";
            public const int MonthsInYear = 12;
            public const string TimeFormat = "HH:mm:ss";
            public const string TimeSpanDisplayFormat = @"hh\:mm\:ss";
            public const string UtcOffsetDatabaseFormat = "0.00";
            public const string UtcOffsetDisplayFormat = @"hh\:mm";

            public static readonly TimeSpan DateTimeDatabaseResolution = TimeSpan.FromMilliseconds(1.0);
            public static readonly TimeSpan MaximumUtcOffset = TimeSpan.FromHours(14.0);
            public static readonly TimeSpan MinimumUtcOffset = TimeSpan.FromHours(-12.0);
            public static readonly TimeSpan UtcOffsetGranularity = TimeSpan.FromTicks(9000000000); // 15 minutes

            public static readonly string[] DateTimeMetadataFormats =
            {
                // known formats supported by Metadata Extractor
                "yyyy:MM:dd HH:mm:ss.fff",
                "yyyy:MM:dd HH:mm:ss",
                "yyyy:MM:dd HH:mm",
                "yyyy-MM-dd HH:mm:ss",
                "yyyy-MM-dd HH:mm",
                "yyyy.MM.dd HH:mm:ss",
                "yyyy.MM.dd HH:mm",
                "yyyy-MM-ddTHH:mm:ss.fff",
                "yyyy-MM-ddTHH:mm:ss.ff",
                "yyyy-MM-ddTHH:mm:ss.f",
                "yyyy-MM-ddTHH:mm:ss",
                "yyyy-MM-ddTHH:mm.fff",
                "yyyy-MM-ddTHH:mm.ff",
                "yyyy-MM-ddTHH:mm.f",
                "yyyy-MM-ddTHH:mm",
                "yyyy:MM:dd",
                "yyyy-MM-dd",
                "yyyy-MM",
                "yyyy",

                // File.File Modified Date
                "ddd MMM dd HH:mm:ss K yyyy"
            };
        }
    }
}
