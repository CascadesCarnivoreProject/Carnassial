namespace Timelapse
{
    // Keep all constants in one place. 
    // This helps ensure we are not setting values differently across multiple files, etc.
    internal static class Constants
    {
        // Update Information, for checking for updates in the timelapse xml file stored on the web site
        public const string URL_CONTAINING_LATEST_VERSION_INFO = "http://saul.cpsc.ucalgary.ca/timelapse/uploads/Installs/timelapse_version.xml";
        public const string APPLICATION_NAME = "timelapse";

        // File names
        public const string DBTEMPLATEFILENAME = "TimelapseTemplate.tdb";    // Template
        public const string XMLTEMPLATEFILENAME = "CodeTemplate.xml";
        public const string XMLDATAFILENAME = "ImageData.xml";

        public const string DBIMAGEDATAFILENAME = "TimelapseData.ddb";               // Image database name
        public const string DBIMAGEDATABACKUPFILENAME = "TimelapseData.BACKUP.ddb";  // Image database - backup name
        public const string DBIMAGEDATASUFFIX = ".ddb";
        public const string CSVIMAGEDATAFILENAME = "TimelapseData.csv";                 // CSV file name
        public const string CSVIMAGEDATABACKUPFILENAME = "TimelapseData.BACKUP.csv";    // CSV file - backup name
        public const string CSVSUFFIX = ".csv";

        public const string BACKUPFOLDER = "Backups"; // Sub-folder that will contain file backups and deleted images 

        // Generic Codes
        // These are generic to all codes that want to use them
        public const string DATA = "Data";             // the data describing the attributes of that code
        public const string DEFAULT = "DefaultValue";  // a default value for that code
        public const string LIST = "List";             // indicates a list of items
        public const string ITEM = "Item";             // and item in a list
        public const string LABEL = "Label";           // a label used to describe that code
        public const string TOOLTIP = "Tooltip";       // the tooltip text that describes the code
        public const string COPYABLE = "Copyable";     // whether the content of this item should be copied from previous values
        public const string DATALABEL = "DataLabel";   // if not empty, its used instead of the label as the header for the column when writing the spreadsheet
        public const string VISIBLE = "Visible";        // whether an item should be visible (used by standard items)
        public const string TXTBOXWIDTH = "TXTBOXWIDTH";      // the width of the textbox

        public const string IMAGEQUALITY_OK = "Ok";
        public const string IMAGEQUALITY_DARK = "Dark";
        public const string IMAGEQUALITY_CORRUPTED = "Corrupted";
        public const string IMAGEQUALITY_MISSING = "Missing";

        // Default Settings
        public const byte DifferenceThresholdMax = 255;
        public const byte DifferenceThresholdMin = 0;

        public const int DEFAULT_DIFFERENCE_THRESHOLD = 20; // The threshold to determine differences between images

        // Special characters
        public const string SLASH = "/";
        public const char MARKERBAR = '|';              // Separator used to separate marker points in the database i.e. "2.3,5.6 | 7.1, 3.3"

        // Template Database things:
        // db query phrases
        public const string SELECTSTAR = "SELECT * FROM ";
        public const string ORDERBY = " ORDER BY ";
        public const string KEYPREFIX = "K";  // The key used as a tag is the KeyPrefix immediately followed by the template row ID, eg, Key5.
        public const string CONTROLORDER = "ControlOrder";
        public const string SPREADSHEETORDER = "SpreadsheetOrder";

        // db table names and related strings
        public const string TABLETEMPLATE = "TemplateTable"; // the data containing the template data
        public const string TABLEDATA = "DataTable";         // the table containing the image data
        public const string TABLEIMAGESET = "ImageSetTable"; // the table containing information commont to the entire image set
        public const string TABLEMARKERS = "MarkersTable";         // the table containing the marker data
        public const string TABLE_CREATIONSTRING_WITHPRIMARYKEY = "INTEGER PRIMARY KEY AUTOINCREMENT";
        public const string TABLE_CREATIONSTRING_WITHINTEGER = "Id integer primary key";
        public const string TYPE = "Type";                   // the data type
        public const string ID = "Id";                       // the unique id of the table row

        // To keep track of image counts and the order of dates
        public enum DateOrder : int { DayMonth = 0, MonthDay = 1, Unknown = 2 }

        // XML Labels(Code Template File) - XXXX NOTE, COULD PROBABLY PREFIX THIS WITH 'XML' for great clarity
        public const string IMAGES = "Images";             // There are multiple images

        public const string IMAGE = "Image";               // A single image and its associated data

        public const string MARKER_STANDARDCOLOR = "Gold";
        public const string MARKER_SELECTIONCOLOR = "MediumBlue";
        public const string MARKER_EMPHASISCOLOR = "Red";

        // Names of standard elements, always included but not always made visible
        public const string FILE = "File";
        public const string FOLDER = "Folder";
        public const string DATE = "Date";
        public const string TIME = "Time";
        public const string IMAGEQUALITY = "ImageQuality";
        public const string DELETEFLAG = "DeleteFlag";    // a flag data type for marking deletion
        public const string NOTE = "Note";                 // A note
        public const string FIXEDCHOICE = "FixedChoice";   // a fixed choice
        public const string FLAG = "Flag";                 // A note
        public const string COUNTER = "Counter";           // a counter
        public const string POINTS = "Points";             // There may be multiple points per counter
        public const string POINT = "Point";               // a single point
        public const string X = "X";                       // Every point has an X and Y
        public const string Y = "Y";

        // Keys defining columns in our ImageSetTable
        public const string LOG = "Log";                   // String holding a user-created text log
        public const string STATE_MAGNIFIER = "Magnifier";          // string holding the true/false state of the magnifying glass (on or off)
        public const string STATE_FILTER = "Filter";         // string holding the currently selected filter
        public const string STATE_ROW = "Row";                    // string holding the currently selected row
        public const string STATE_WHITESPACE_TRIMMED = "WhiteSpaceTrimmed";          // string holding the true/false state of whether the white space has been trimmed from the data.

        // XML STRING PATHS to standard elements, always included but not always made visible
        // Essentially, its just the standard elements prefixed by CODES/<element>
        public const string CODES = "Codes";
        public const string FILEPATH = Constants.CODES + Constants.SLASH + Constants.FILE;
        public const string FOLDERPATH = Constants.CODES + Constants.SLASH + Constants.FOLDER;
        public const string DATEPATH = Constants.CODES + Constants.SLASH + Constants.DATE;
        public const string TIMEPATH = Constants.CODES + Constants.SLASH + Constants.TIME;
        public const string IMAGEQUALITYPATH = Constants.CODES + Constants.SLASH + Constants.IMAGEQUALITY;
        public const string NOTEPATH = Constants.CODES + Constants.SLASH + Constants.NOTE;
        public const string COUNTERPATH = Constants.CODES + Constants.SLASH + Constants.COUNTER;
        public const string FIXEDCHOICEPATH = Constants.CODES + Constants.SLASH + Constants.FIXEDCHOICE;

        public const int DEFAULT_DARK_PIXEL_THRESHOLD = 60; // The default threshold where a pixel color should be considered as 'dark' when checking image darkness. The Range is 0  (black) - 255 (white)
        public const double DEFAULT_DARK_PIXEL_RATIO_THRESHOLD = 0.9; // The default threshold where the ratio of pixels below a given darkness in an image is used to determine whether the image is classified as 'dark'

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
                // The last image folder on exit (a full path)
                public const string LastDatabaseFolderPath = "LastDatabaseFolderPath";
                // The last image template file name on exit (just the name)
                public const string LastDatabaseTemplateName = "LastDatabaseTemplateName";
                // Whether to show the CSV dialog window
                public const string ShowCsvDialog = "ShowCsvDialog";
            }

            public const string RootKey = @"Software\Greenberg Consulting\Timelapse\2.0";   // Defines the KEY path under HKEY_CURRENT_USER
        }
    }
}
