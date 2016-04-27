using System;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Media.Imaging;

namespace Timelapse
{
    // Keep all constants in one place. 
    // This helps ensure we are not setting values differently across multiple files, etc.
    internal static class Constants
    {
        // Default Settings
        public const int BitmapCacheSize = 9;
        public const double DarkPixelRatioThresholdDefault = 0.9; // The default threshold where the ratio of pixels below a given darkness in an image is used to determine whether the image is classified as 'dark'
        public const int DarkPixelThresholdDefault = 60; // The default threshold where a pixel color should be considered as 'dark' when checking image darkness. The Range is 0  (black) - 255 (white)
        public const int DefaultImageRowIndex = 0;
        public const int DifferenceThresholdDefault = 20; // The threshold to determine differences between images
        public const byte DifferenceThresholdMax = 255;
        public const byte DifferenceThresholdMin = 0;
        public const int FolderScanProgressUpdateFrequency = 1;
        public const int NumberOfMostRecentDatabasesToTrack = 9;
        public const string StandardColour = "Gold";
        public const string SelectionColour = "MediumBlue";

        // Update Information, for checking for updates in the timelapse xml file stored on the web site
        public const string ApplicationName = "timelapse";
        public static readonly Uri LatestVersionAddress = new Uri("http://saul.cpsc.ucalgary.ca/timelapse/uploads/Installs/timelapse_version.xml");
        public static readonly Uri VersionChangesAddress = new Uri("http://saul.cpsc.ucalgary.ca/timelapse/pmwiki.php?n=Main.TimelapseVersions#Timelapse");

        // Generic Codes
        // These are generic to all controls that want to use them
        public static class Control
        {
            public const string Copyable = "Copyable";     // whether the content of this item should be copied from previous values
            public const string DataLabel = "DataLabel";   // if not empty, its used instead of the label as the header for the column when writing the spreadsheet
            public const string DefaultValue = "DefaultValue";  // a default value for that code
            public const string List = "List";             // indicates a list of items
            public const string Label = "Label";           // a label used to describe that code
            public const string TextBoxWidth = "TXTBOXWIDTH";      // the width of the textbox
            public const string Tooltop = "Tooltip";       // the tooltip text that describes the code
            public const string Visible = "Visible";        // whether an item should be visible (used by standard items)
        }

        public static class Database
        {
            // db query phrases
            public const string ControlOrder = "ControlOrder";
            public const string SelectStarFrom = "SELECT * FROM ";
            public const string SpreadsheetOrder = "SpreadsheetOrder";

            // db table names and related strings
            public const string CreationStringInteger = "Id integer primary key";
            public const string CreationStringPrimaryKey = "INTEGER PRIMARY KEY AUTOINCREMENT";
            public const string ImageDataTable = "DataTable";         // the table containing the image data
            public const string ID = "Id";                       // the unique id of the table row
            public const string ImageSetTable = "ImageSetTable"; // the table containing information commont to the entire image set
            public const string MarkersTable = "MarkersTable";         // the table containing the marker data
            public const string TemplateTable = "TemplateTable"; // the data containing the template data
            public const string Type = "Type";                   // the data type

            // Special characters
            public const char MarkerBar = '|';              // Separator used to separate marker points in the database i.e. "2.3,5.6 | 7.1, 3.3"
        }

        // Names of standard database columns, always included but not always made visible in the user controls
        public static class DatabaseColumn
        {
            public const string Counter = "Counter";           // a counter
            public const string Data = "Data";                 // the data describing the attributes of that control
            public const string Date = "Date";
            public const string DeleteFlag = "DeleteFlag";    // a flag data type for marking deletion
            public const string File = "File";
            public const string FixedChoice = "FixedChoice";   // a fixed choice
            public const string Flag = "Flag";                 // A boolean
            public const string Folder = "Folder";
            public const string ID = "Id";
            public const string Image = "Image";               // A single image and its associated data
            public const string ImageQuality = "ImageQuality";
            public const string Images = "Images";             // There are multiple images
            public const string Note = "Note";                 // A note
            public const string Point = "Point";               // a single point
            public const string Time = "Time";
            public const string X = "X";                       // Every point has an X and Y
            public const string Y = "Y";

            // Keys defining columns in our ImageSetTable
            public const string Filter = "Filter";         // string holding the currently selected filter
            public const string Log = "Log";                   // String holding a user-created text log
            public const string Magnifier = "Magnifier";          // string holding the true/false state of the magnifying glass (on or off)
            public const string Row = "Row";                    // string holding the currently selected row
            public const string WhiteSpaceTrimmed = "WhiteSpaceTrimmed";          // string holding the true/false state of whether the white space has been trimmed from the data.

            // Paths to standard elements, always included but not always made visible
            [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1309:FieldNamesMustNotBeginWithUnderscore", Justification = "Accuracy")]
            public const string _Date = "_Date";
            [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1309:FieldNamesMustNotBeginWithUnderscore", Justification = "Accuracy")]
            public const string _File = "_File";
            [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1309:FieldNamesMustNotBeginWithUnderscore", Justification = "Accuracy")]
            public const string _Time = "_Time";
            public const string Slash = "/";
        }

        public static class File
        {
            public const string BackupFolder = "Backups"; // Sub-folder that will contain file backups and deleted images 
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
            public const string GreaterThanOrEqual = "\u2267";
            public const string LessThan = "\u003C";
            public const string LessThanOrEqual = "\u2264";
            public const string NotEqual = "\u2260";
        }

        public static class ImageQuality
        {
            public const string Corrupted = "Corrupted";
            public const string Dark = "Dark";
            public const string Missing = "Missing";
            public const string Ok = "Ok";
        }

        public static class Images
        {
            public static readonly BitmapFrame Corrupt;
            public static readonly BitmapFrame Missing;

            static Images()
            {
                Images.Corrupt = BitmapFrame.Create(new Uri("pack://application:,,/Resources/corrupted.jpg"), BitmapCreateOptions.None, BitmapCacheOption.OnDemand);
                Images.Corrupt.Freeze();
                Images.Missing = BitmapFrame.Create(new Uri("pack://application:,,/Resources/missing.jpg"), BitmapCreateOptions.None, BitmapCacheOption.OnDemand);
                Images.Missing.Freeze();
            }
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
                // Key containing the list of most recently used data files
                public const string MostRecentlyUsedDataFiles = "MostRecentlyUsedDataFiles";
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
            public const string WhereIDIn = Where + "Id IN ";

            public const string Null = "NULL";
            public const string NullAs = Null + " " + As;

            public const string Comma = ", ";
            public const string OpenParenthesis = " ( ";
            public const string CloseParenthesis = " ) ";
            public const string Semicolon = " ; ";
        }
    }
}
