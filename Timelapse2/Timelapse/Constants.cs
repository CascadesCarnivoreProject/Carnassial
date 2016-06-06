using System;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Timelapse
{
    // Keep all constants in one place. 
    // This helps ensure we are not setting values differently across multiple files, etc.
    public static class Constants
    {
        // Default Settings
        public const int DefaultImageRowIndex = 0;
        public const int FolderScanProgressUpdateFrequency = 1;
        public const int NumberOfMostRecentDatabasesToTrack = 9;
        public const string StandardColour = "Gold";
        public const string SelectionColour = "MediumBlue";

        // Update Information, for checking for updates in the timelapse xml file stored on the web site
        public const string ApplicationName = "Timelapse";
        public static readonly Uri LatestVersionAddress = new Uri("http://saul.cpsc.ucalgary.ca/timelapse/uploads/Installs/timelapse_version.xml");
        public static readonly Uri VersionChangesAddress = new Uri("http://saul.cpsc.ucalgary.ca/timelapse/pmwiki.php?n=Main.TimelapseVersions#Timelapse");

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
            public const string TextBoxWidth = "TXTBOXWIDTH";  // the width of the textbox
            public const string Tooltip = "Tooltip";       // the tooltip text that describes the code
            public const string Type = "Type";             // the data type
            public const string Visible = "Visible";       // whether an item should be visible (used by standard items)

            // control types
            public const string Counter = "Counter";           // a counter
            public const string FixedChoice = "FixedChoice";   // a fixed choice
            public const string Flag = "Flag";                 // A boolean
            public const string List = "List";             // indicates a list of items
            public const string Note = "Note";                 // A note

            // default data labels
            public const string Choice = "Choice";            // Label for: a choice

            // things which should be data labels but are used in the TemplateTable's Type column
            public const string DeleteFlag = "DeleteFlag";    // a flag data type for marking deletion
        }

        // see also ControlLabelStyle and ControlContentStyle
        public static class ControlStyle
        {
            public const string ComboBoxCodeBar = "ComboBoxCodeBar";
            public const string StackPanelCodeBar = "StackPanelCodeBar";
        }

        public static class ControlDefault
        {
            // standard controls
            public const string CounterTooltip = "Click the counter button, then click on the image to count the entity. Or just type in a count";
            public const string CounterValue = "0";              // Default for: counters
            public const string CounterWidth = "80";
            public const string FixedChoiceTooltip = "Choose an item from the menu";
            public const string FixedChoiceValue = "";
            public const string FixedChoiceWidth = "100";

            public const string FlagTooltip = "Toggle between true and false";
            public const string FlagValue = Constants.Boolean.False;             // Default for: flags
            public const string FlagWidth = "20";
            public const string NoteTooltip = "Write a textual note";
            public const string NoteValue = "";                  // Default for: notes
            public const string NoteWidth = "100";

            public const string ListValue = "";                  // Default for: list

            // standard columns
            public const string DateTooltip = "Date the image was taken";
            public const string DateValue = "";                  // Default for: date image was taken
            public const string DateWidth = "100";
            public const string FileTooltip = "The image file name";
            public const string FileValue = "";                  // Default for: the file name
            public const string FileWidth = "100";

            public const string FolderTooltip = "Name of the folder containing the images";
            public const string FolderValue = "";                // Default for: the folder path
            public const string FolderWidth = "100";
            public const string ImageQualityTooltip = "System-determined image quality: Ok, dark if mostly black, corrupted if it can not be read";
            public const string ImageQualityValue = "";          // Default for: time image was taken
            public const string ImageQualityWidth = "80";

            public const string MarkForDeletionTooltip = "Mark an image as one to be deleted. You can then confirm deletion through the Edit Menu";
            public const string TimeTooltip = "Time the image was taken";
            public const string TimeValue = "";                  // Default for: time image was taken
            public const string TimeWidth = "100";
        }

        public static class Database
        {
            // db table names and related strings
            public const string CreationStringInteger = "Id integer primary key";
            public const string CreationStringPrimaryKey = "INTEGER PRIMARY KEY AUTOINCREMENT";
            public const string ImageDataTable = "DataTable";         // the table containing the image data
            public const string ImageSetTable = "ImageSetTable"; // the table containing information commont to the entire image set
            public const string MarkersTable = "MarkersTable";         // the table containing the marker data
            public const string TemplateTable = "TemplateTable"; // the data containing the template data

            // default values
            public const string ImageSetDefaultLog = "Add text here";

            // Special characters
            public const char MarkerBar = '|';              // Separator used to separate marker points in the database i.e. "2.3,5.6 | 7.1, 3.3"
        }

        // Names of standard database columns, always included but not always made visible in the user controls
        public static class DatabaseColumn
        {
            public const string Data = "Data";                 // the data describing the attributes of that control
            public const string Date = "Date";
            public const string File = "File";
            public const string Folder = "Folder";
            public const string ID = "Id";
            public const string Image = "Image";               // A single image and its associated data
            public const string ImageQuality = "ImageQuality";
            public const string Point = "Point";               // a single point
            public const string Time = "Time";
            public const string X = "X";                       // Every point has an X and Y
            public const string Y = "Y";

            // columns in ImageSetTable
            public const string Filter = "Filter";         // string holding the currently selected filter
            public const string Log = "Log";                   // String holding a user-created text log
            public const string Magnifier = "Magnifier";          // string holding the true/false state of the magnifying glass (on or off)
            public const string Row = "Row";                    // string holding the currently selected row
            public const string WhiteSpaceTrimmed = "WhiteSpaceTrimmed";          // string holding the true/false state of whether the white space has been trimmed from the data.
        }

        public static class File
        {
            public const string BackupFolder = "Backups"; // Sub-folder that will contain database and csv file backups  
            public const string DeletedImagesFolder = "DeletedImages"; // Sub-folder that will contain backups of deleted images 
            public const string CsvFileExtension = ".csv";
            public const string DefaultImageDatabaseFileName = "TimelapseData.ddb";
            public const string DefaultTemplateDatabaseFileName = "TimelapseTemplate.tdb";
            public const string ImageDatabaseFileExtension = ".ddb";
            public const string TemplateDatabaseFileExtension = ".tdb";
            public const string XmlTemplateFileName = "CodeTemplate.xml";
            public const string XmlDataFileName = "ImageData.xml";
        }

        public static class Filter
        {
            public const string Equal = "\u003D";
            public const string Glob = " GLOB ";
            public const string GreaterThan = "\u003E";
            public const string GreaterThanOrEqual = "\u2265";
            public const string LessThan = "\u003C";
            public const string LessThanOrEqual = "\u2264";
            public const string NotEqual = "\u2260";
        }

        // shorthands for ImageQualityFilter.<value>.ToString()
        public static class ImageQuality
        {
            public const string Corrupted = "Corrupted";
            public const string Dark = "Dark";
            public const string Missing = "Missing";
            public const string Ok = "Ok";

            public const string ListOfValues = "Ok| Dark| Corrupted | Missing";
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

            public static readonly BitmapFrame Corrupt;
            public static readonly BitmapFrame CorruptThumbnail;  
            public static readonly BitmapFrame Missing;
            public static readonly BitmapFrame MissingThumbnail;

            static Images()
            {
                // Create a variety of images.
                // SAULTODO: Thumbnail access had occassionaly introduces a threading violation, but I need to replicate it before I can fix it.
                Images.Corrupt = BitmapFrame.Create(new Uri("pack://application:,,/Resources/corrupted.jpg"), BitmapCreateOptions.None, BitmapCacheOption.OnDemand);
                Images.Corrupt.Freeze();
                Images.Missing = BitmapFrame.Create(new Uri("pack://application:,,/Resources/missing.jpg"), BitmapCreateOptions.None, BitmapCacheOption.OnDemand);
                Images.Missing.Freeze();

                // Create thumbnails of both the above images
                BitmapImage bmMissingThumbnail = new BitmapImage();
                bmMissingThumbnail.BeginInit();
                bmMissingThumbnail.DecodePixelWidth = 400;
                bmMissingThumbnail.CacheOption = BitmapCacheOption.OnLoad;
                bmMissingThumbnail.UriSource = new Uri("pack://application:,,/Resources/missing.jpg");
                bmMissingThumbnail.EndInit();

                Images.MissingThumbnail = BitmapFrame.Create(bmMissingThumbnail);
                BitmapImage bmCorruptThumbnail = new BitmapImage();
                bmCorruptThumbnail.BeginInit();
                bmCorruptThumbnail.DecodePixelWidth = 400;
                bmCorruptThumbnail.CacheOption = BitmapCacheOption.OnLoad;
                bmCorruptThumbnail.UriSource = new Uri("pack://application:,,/Resources/corrupted.jpg");
                bmCorruptThumbnail.EndInit();
                Images.CorruptThumbnail = BitmapFrame.Create(bmCorruptThumbnail);
            }
        }

        public static class ImageXml
        {
            // standard elements, always included but not always made visible
            public const string Date = "_Date";
            public const string File = "_File";
            public const string Time = "_Time";

            // paths to standard elements, always included but not always made visible
            public const string DatePath = "Codes/_Date";
            public const string FilePath = "Codes/_File";
            public const string FolderPath = "Codes/_Folder";
            public const string ImageQualityPath = "Codes/_ImageQuality";
            public const string TimePath = "Codes/_Time";

            // elements
            public const string Codes = "Codes";
            public const string Data = "Data";             // the data describing the attributes of that code
            public const string Images = "Images";
            public const string Item = "Item";             // and item in a list
            public const string Slash = "/";

            // paths to notes, counters, and fixed choices
            public const string CounterPath = ImageXml.Codes + ImageXml.Slash + Constants.Control.Counter;
            public const string FixedChoicePath = ImageXml.Codes + ImageXml.Slash + Constants.Control.FixedChoice;
            public const string NotePath = ImageXml.Codes + ImageXml.Slash + Constants.Control.Note;
        }

        public static class Registry
        {
            public static class Key
            {
                // The image filter used on exit
                public const string AudioFeedback = "AudioFeedback";
                // Whether the controls are in a separate window (true) or in the Timelapse Window (false)
                public const string ControlsInSeparateWindow = "ControlWindowSeparate";
                // The width of the controlWindow
                public const string ControlWindowHeight = "ControlWindowHeight";
                // The width of the controlWindow
                public const string ControlWindowWidth = "ControlWindowWidth";
                // The DarkPixelThreshold
                public const string DarkPixelThreshold = "DarkPixelThreshold";
                // The DarkPixelRatio
                public const string DarkPixelRatio = "DarkPixelRatio";
                // Key containing the list of most recently used template databases
                public const string MostRecentlyUsedImageSets = "MostRecentlyUsedImageSets";
                // Whether to show the CSV dialog window
                public const string ShowCsvDialog = "ShowCsvDialog";
            }

            public const string RootKey = @"Software\Greenberg Consulting\Timelapse\2.0";   // Defines the KEY path under HKEY_CURRENT_USER
        }

        public static class Sql
        {
            public const string DataSource = "Data Source=";
            public const string CreateTable = "CREATE TABLE ";
            public const string InsertInto = "INSERT INTO ";
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
            public const string WhereIDIn = Where + "Id IN ";

            public const string Null = "NULL";
            public const string NullAs = Null + " " + As;

            public const string Comma = ", ";
            public const string OpenParenthesis = " ( ";
            public const string CloseParenthesis = " ) ";
            public const string Semicolon = " ; ";
        }

        public static class Time
        {
            // The standard date format, e.g., 05-Apr-2011
            public const string DateFormat = "dd-MMM-yyyy";
            public const string DateTimeFormat = "dd-MMM-yyyy HH:mm:ss";
            public const string TimeFormatForDatabase = "HH:mm:ss";
            public const string TimeFormatForUser = "hh:mm tt";
        }

        public static class VersionXml
        {
            public const string Changes = "changes";
            public const string Timelapse = "timelapse";
            public const string Url = "url";
            public const string Version = "version";
        }
    }
}
