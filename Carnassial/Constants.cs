using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Imaging;
using Xceed.Wpf.Toolkit;

namespace Carnassial
{
    // Keep all constants in one place. 
    // This helps ensure we are not setting values differently across multiple files, etc.
    public static class Constants
    {
        // Default Settings
        public const string ApplicationName = "Carnassial";
        public const int DefaultImageRowIndex = 0;
        public const int NumberOfMostRecentDatabasesToTrack = 9;
        public const string StandardColour = "Gold";
        public const string SelectionColour = "MediumBlue";

        public static class ApplicationSettings
        {
            public const string LatestVersionAddress = "latestVersionAddress";
            public const string VersionChangesAddress = "versionChangesAddress";
        }

        // Boolean.TrueString and FalseString are "True" and "False" and are preferred, but 
        public static class Boolean
        {
            public const string True = "true";
            public const string False = "false";
        }

        public static class Control
        {
            // columns unique to the template table
            public const string ControlOrder = "ControlOrder";
            public const string Copyable = "Copyable";     // whether the content of this item should be copied from previous values
            public const string DataLabel = "DataLabel";   // if not empty, its used instead of the label as the header for the column when writing the spreadsheet
            public const string DefaultValue = "DefaultValue"; // a default value for that code
            public const string Label = "Label";           // a label used to describe that code
            public const string SpreadsheetOrder = "SpreadsheetOrder";
            public const string Tooltip = "Tooltip";       // the tooltip text that describes the code
            public const string Type = "Type";             // the data type
            public const string Visible = "Visible";       // whether an item should be visible (used by standard items)
            public const string Width = "Width";           // the width of the textbox

            // control types
            public const string Counter = "Counter";       // a counter
            public const string FixedChoice = "FixedChoice";  // a fixed choice
            public const string Flag = "Flag";             // A boolean
            public const string List = "List";             // indicates a list of items
            public const string Note = "Note";             // A note

            // default data labels
            public const string Choice = "Choice";         // Label for: a choice

            public static readonly ReadOnlyCollection<Type> KeyboardInputTypes = new List<Type>()
            {
                typeof(Calendar),          // date time control
                typeof(CalendarDayButton), // date time control
                typeof(CheckBox),          // flag controls
                typeof(ComboBox),          // choice controls
                typeof(ComboBoxItem),      // choice controls
                typeof(TextBox),           // note and counter controls
                typeof(WatermarkTextBox)   // date time control
            }.AsReadOnly();

            public static readonly ReadOnlyCollection<string> StandardTypes = new List<string>()
            {
                Constants.DatabaseColumn.DateTime,
                Constants.DatabaseColumn.DeleteFlag,
                Constants.DatabaseColumn.File,
                Constants.DatabaseColumn.Folder,
                Constants.DatabaseColumn.ImageQuality,
                Constants.DatabaseColumn.RelativePath,
                Constants.DatabaseColumn.UtcOffset
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
            public const string FlagValue = Constants.Boolean.False;             // Default for: flags
            public const int FlagWidth = 20;
            public const string NoteTooltip = "Write a textual note";
            public const int NoteWidth = 100;

            // standard controls
            public const string DateTimeTooltip = "Date and time taken";
            public const string DateTimeWidth = "170";

            public const string FileTooltip = "The file name";
            public const string FileWidth = "100";
            public const string RelativePathTooltip = "Path from the folder containing the template and image data files to the file";
            public const string RelativePathWidth = "100";
            public const string FolderTooltip = "Name of the folder originally containing the template and image data files";
            public const string FolderWidth = "100";

            public const string ImageQualityTooltip = "System-determined image quality: Ok, dark if mostly black, corrupted if it can not be read, missing if the image/video file is missing";
            public const string ImageQualityWidth = "85";

            public const string DeleteFlagLabel = "Delete?";    // a flag data type for marking deletion
            public const string DeleteFlagTooltip = "Mark a file as one to be deleted. You can then confirm deletion through the Edit Menu";

            public const string UtcOffsetTooltip = "Universal Time offset of the time zone for date and time taken";
            public const string UtcOffsetWidth = "60";

            public static readonly DateTimeOffset DateTimeValue = new DateTimeOffset(1900, 1, 1, 12, 0, 0, 0, TimeSpan.Zero);
        }

        public static class Database
        {
            // database table names and related strings
            public const string CreationStringInteger = "Id integer primary key";
            public const string CreationStringPrimaryKey = "INTEGER PRIMARY KEY AUTOINCREMENT";
            public const string ImageDataTable = "DataTable";         // the table containing the image data
            public const string ImageSetTable = "ImageSetTable"; // the table containing information common to the entire image set
            public const string MarkersTable = "MarkersTable";         // the table containing the marker data
            public const string TemplateTable = "TemplateTable"; // the table containing the template data

            // default values
            public const int DateTimePosition = 4;
            public const string ImageSetDefaultLog = "Add text here";
            public const long ImageSetRowID = 1;
            public const long InvalidID = -1;
            public const int InvalidRow = -1;
            public const int RelativePathPosition = 2;
            public const int RowsPerInsert = 100;
            public const int UtcOffsetPosition = 5;

            // Special characters
            public const char MarkerBar = '|';              // Separator used to separate marker points in the database i.e. "2.3,5.6 | 7.1, 3.3"
        }

        // Names of standard database columns, always included but not always made visible in the user controls
        public static class DatabaseColumn
        {
            public const string Data = "Data";                 // the data describing the attributes of that control
            public const string DateTime = "DateTime";
            public const string File = "File";
            public const string Folder = "Folder";
            public const string ID = "Id";
            public const string Image = "Image";               // A single image and its associated data
            public const string ImageQuality = "ImageQuality";
            public const string DeleteFlag = "DeleteFlag";
            public const string Point = "Point";               // a single point
            public const string RelativePath = "RelativePath";
            public const string TimeZone = "TimeZone";
            public const string UtcOffset = "UtcOffset";
            public const string X = "X";                       // Every point has an X and Y
            public const string Y = "Y";

            // columns in ImageSetTable
            public const string Selection = "Selection";       // string holding the current selection
            public const string Log = "Log";                   // string holding a user-created text log
            public const string Magnifier = "Magnifier";       // string holding the true/false state of the magnifying glass (on or off)
            public const string Row = "Row";                   // string holding the currently selected row
        }

        public static class File
        {
            public const string AviFileExtension = ".avi";
            public const string BackupFolder = "Backups"; // Sub-folder that will contain database and csv file backups  
            public const int NumberOfBackupFilesToKeep = 8; // Maximum number of backup files to keep
            public const string DeletedImagesFolder = "DeletedImages"; // Sub-folder that will contain backups of deleted images 
            public const string CsvFileExtension = ".csv";
            public const string DefaultImageDatabaseFileName = "CarnassialData.ddb";
            public const string DefaultTemplateDatabaseFileName = "CarnassialTemplate.tdb";
            public const string ImageDatabaseFileExtension = ".ddb";
            public const string JpgFileExtension = ".jpg";
            public const string Mp4FileExtension = ".mp4";
            public const string TemplateDatabaseFileExtension = ".tdb";
            public const string XmlTemplateFileName = "CodeTemplate.xml";
            public const string XmlDataFileName = "ImageData.xml";
        }

        // shorthands for ImageSelection.<value>.ToString()
        public static class ImageQuality
        {
            public const string Corrupted = "Corrupted";
            public const string Dark = "Dark";
            public const string Missing = "Missing";
            public const string Ok = "Ok";

            public const string ListOfValues = "Ok|Dark|Corrupted|Missing";
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
            // Check only every few pixels as otherwise dark frame detection is expensive operation
            // A grey scale pixel has r = g = b. But we will allow some slop in here just in case a bit of color creeps in
            public const int GreyScalePixelThreshold = 40;

            // Various thumbnail sizes
            public const int ThumbnailSmall = 300;
            public const int ThumbnailMedium = 512;
            public const int ThumbnailLarge = 1024;
            public const int ThumbnailNone = 0;

            public static readonly BitmapFrame Corrupt;
            public static readonly BitmapFrame CorruptThumbnail;  
            public static readonly BitmapFrame Missing;
            public static readonly BitmapFrame MissingThumbnail;
            public static readonly BitmapFrame EmptyImageSet;

            static Images()
            {
                // Create a variety of images.
                Images.Corrupt = BitmapFrame.Create(new Uri("pack://application:,,/Resources/corrupted.jpg"), BitmapCreateOptions.None, BitmapCacheOption.OnDemand);
                Images.Corrupt.Freeze();
                Images.Missing = BitmapFrame.Create(new Uri("pack://application:,,/Resources/missing.jpg"), BitmapCreateOptions.None, BitmapCacheOption.OnDemand);
                Images.Missing.Freeze();
                Images.EmptyImageSet = BitmapFrame.Create(new Uri("pack://application:,,/Resources/empty_imageset.jpg"), BitmapCreateOptions.None, BitmapCacheOption.OnDemand);
                Images.Missing.Freeze();
                Images.CorruptThumbnail = BitmapFrame.Create(new Uri("pack://application:,,/Resources/corrupted_thumbnail.jpg"), BitmapCreateOptions.None, BitmapCacheOption.OnDemand);
                Images.CorruptThumbnail.Freeze();
                Images.MissingThumbnail = BitmapFrame.Create(new Uri("pack://application:,,/Resources/missing_thumbnail.jpg"), BitmapCreateOptions.None, BitmapCacheOption.OnDemand);
                Images.MissingThumbnail.Freeze();
            }
        }

        public static class MarkableCanvas
        {
            public const double MagnifierDefaultZoom = 60;
            public const double MagnifierMaxZoom = 15;  // Max is a smaller number
            public const double MagnifierMinZoom = 100; // Min is the larger number
            public const double MagnifierZoomStep = 2;

            public const int MarkDiameter = 10;
            public const int MarkStrokeThickness = 2;
            public const int MarkGlowDiameterIncrease = 15;
            public const int MarkGlowStrokeThickness = 7;
            public const double MarkGlowOpacity = 0.35;

            public const double ZoomMaximum = 10;   // Maximum amount of zoom
            public const double ZoomMaximumUpperBound = 50;   // Maximum amount of zoom
            public const double ZoomMinimum = 1;   // Minimum amount of zoom
            public const double ZoomStep = 1.2;   // Amount to scale on each increment
        }

        public static class Registry
        {
            public static class CarnassialKey
            {
                public const string AudioFeedback = "AudioFeedback";

                public const string CarnassialWindowLocation = "CarnassialWindowLocation";
                public const string CarnassialWindowSize = "CarnassialWindowSize";

                // most recently used operator for custom selections
                public const string CustomSelectionTermCombiningOperator = "CustomSelectionTermCombiningOperator";
                // the DarkPixelThreshold
                public const string DarkPixelThreshold = "DarkPixelThreshold";
                // the DarkPixelRatio
                public const string DarkPixelRatio = "DarkPixelRatio";
                // the value for rendering
                public const string DesiredImageRendersPerSecond = "DesiredImageRendersPerSecond";
                // key containing the list of most recently image sets opened by Carnassial
                public const string MostRecentlyUsedImageSets = "MostRecentlyUsedImageSets";

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
            public const string DataSource = "Data Source=";
            public const string CreateTable = "CREATE TABLE ";
            public const string InsertInto = "INSERT INTO ";
            public const string Integer = "INTEGER";
            public const string Values = " VALUES ";
            public const string Select = " SELECT ";
            public const string UnionAll = " UNION ALL";
            public const string As = " AS ";
            public const string DeleteFrom = " DELETE FROM ";
            public const string Where = " WHERE ";
            public const string NameFromSqliteMaster = " NAME FROM SQLITE_MASTER ";
            public const string TypeEqualsTable = " TYPE='table' ";
            public const string OrderBy = " ORDER BY ";
            public const string Name = " NAME ";
            public const string Update = " UPDATE ";
            public const string Set = " SET ";
            public const string When = " WHEN ";
            public const string Then = " THEN ";
            public const string Begin = " BEGIN ";
            public const string End = " END ";
            public const string EqualsCaseID = " = CASE Id";
            public const string SelectStarFrom = "SELECT * FROM ";
            public const string Text = "TEXT";
            public const string WhereIDIn = Where + "Id IN ";

            public const string Null = "NULL";
            public const string NullAs = Null + " " + As;

            public const string Comma = ", ";
            public const string OpenParenthesis = " ( ";
            public const string CloseParenthesis = " ) ";
            public const string Semicolon = " ; ";
        }

        public static class ThrottleValues
        {
            public const double DesiredMaximumImageRendersPerSecondLowerBound = 2.0;
            public const double DesiredMaximumImageRendersPerSecondDefault = 6.0;
            public const double DesiredMaximumImageRendersPerSecondUpperBound = 12.0;
            public const int MaximumRenderAttempts = 100;
            public const int SleepForImageRenderInterval = 100;

            public static readonly TimeSpan PollIntervalForVideoLoad = TimeSpan.FromMilliseconds(1.0);
            public static readonly TimeSpan RenderingBackoffTime = TimeSpan.FromMilliseconds(25);
        }

        public static class Time
        {
            // The standard date format, e.g., 05-Apr-2011
            public const string DateFormat = "dd-MMM-yyyy";
            public const string DateTimeDatabaseFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
            public const string DateTimeDisplayFormat = "dd-MMM-yyyy HH:mm:ss";
            // must be kept in sync with ExifToolWrapper.Arguments 
            public const string DateTimeExifToolFormat = "yyyy:MM:dd HH:mm:ss";
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

        public static class VersionXml
        {
            public const string Changes = "changes";
            public const string Carnassial = "carnassial";
            public const string Url = "url";
            public const string Version = "version";
        }
    }
}
