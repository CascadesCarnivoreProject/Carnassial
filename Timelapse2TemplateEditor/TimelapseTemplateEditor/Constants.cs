namespace TimelapseTemplateEditor
{
    static class Constants
    {
        // Update Information, for checking for updates in the timelapse xml file stored on the web site
        public const string URL_CONTAINING_LATEST_VERSION_INFO = "http://saul.cpsc.ucalgary.ca/timelapse/uploads/Installs/timelapse_template_version.xml";
        public const string APPLICATION_NAME = "timelapse";

        // About this version (used to construct the About message box)
        public const string ABOUT_VERSION = "Version: ";
        public const string ABOUT_AUTHORS = "Authors: Saul Greenberg and Matthew Dunlap";
        public const string ABOUT_DATE = "Release Date: June 27, 2015";
        public const string ABOUT_CAPTION = "About Timeplapse 2 Template Editor";

        // Code Template file name
        public const string CT_ROOTFILENAME = "CodeTemplate";
        public const string CT_FILENAMEEXTENSION = ".xml";

        // Database file name
        public const string DB_ROOTFILENAME = "TimelapseTemplate";
        public const string DB_FILENAMEEXTENSION = ".tdb";

        // Database table name
        public const string TABLENAME = "TemplateTable";                // The name of the primary table in the data template
        public const string TABLECREATIONSTRING = "Id integer primary key autoincrement, ControlOrder int, Type text, DefaultValue text, Label text, DataLabel text, Tooltip text, TXTBOXWIDTH text, Copyable text, Visible text, List text";


        // Database fields
        public const string TYPE = "Type";                  // the data type
        public const string ID = "Id";                      // the unique id of the table row
        public const string DEFAULT = "DefaultValue";       // a default value for that code
        public const string LABEL = "Label";                // UI: the label associated with that data
        public const string DATALABEL = "DataLabel";        // if not empty, this label used instead of the label as the header for the column when writing the spreadsheet
        public const string TOOLTIP = "Tooltip";            // UI: tooltip text that describes the code
        public const string TXTBOXWIDTH = "TXTBOXWIDTH";    // UI: the width of the textbox 
        public const string COPYABLE = "Copyable";          // whether the content of this item should be copied from previous values
        public const string VISIBLE = "Visible";            // UI: whether an item should be visible in the user interface
        public const string LIST = "List";                  // indicates a list of items, used in building fixed choices
        public const string CONTROLORDER = "ControlOrder";        // UI: the order position of items to be used in laying out the controls in the interface
        public const string SPREADSHEETORDER = "SpreadsheetOrder";        // UI: the order position of items to be used in laying out the controls in the interface


        // Defines the allowable types 
        public const string FILE = "File";                  // TYPE: the file name
        public const string FOLDER = "Folder";              // TYPE: the folder path
        public const string DATE = "Date";                  // TYPE: date image was taken
        public const string TIME = "Time";                  // TYPE: time image was taken
        public const string IMAGEQUALITY = "ImageQuality";  // TYPE: a special fixed choice pre-filled with image quality items
        public const string DELETEFLAG = "DeleteFlag";    // TYPE: a flag data type for marking deletion
        public const string NOTE = "Note";                  // TYPE: A text item
        public const string COUNTER = "Counter";            // TYPE: A counter item
        public const string CHOICE = "FixedChoice";         // TYPE: a fixed choice data type (i.e., a drop-down menu of items)
        public const string FLAG = "Flag";         // TYPE: a fixed choice data type (i.e., a drop-down menu of items)

        // Database default Label Values for above fields
        public const string LABEL_FILE = "File";                  // Label for: the file name
        public const string LABEL_FOLDER = "Folder";              // Label for: the folder path
        public const string LABEL_DATE = "Date";                  // Label for: date image was taken
        public const string LABEL_TIME = "Time";                  // Label for: time image was taken
        public const string LABEL_IMAGEQUALITY = "ImageQuality"; // Label for: the Image Quality
        public const string LABEL_DELETEFLAG = "Delete?";        // Label for: the Deletion
        public const string DATALABEL_DELETEFLAG = "MarkForDeletion";     // Data Label for: the Deletion
        public const string LABEL_COUNTER = "Counter";           // Label for: a counter
        public const string LABEL_NOTE = "Note";                 // Label for: a note
        public const string LABEL_CHOICE = "Choice";            // Label for: a choice
        public const string LABEL_FLAG = "Flag";                // Label for: a flag

        // Default contents for all
        public const string DEFAULT_FILE = "";                  // Default for: the file name
        public const string DEFAULT_FOLDER = "";                // Default for: the folder path
        public const string DEFAULT_DATE = "";                  // Default for: date image was taken
        public const string DEFAULT_TIME = "";                  // Default for: time image was taken
        public const string DEFAULT_IMAGEQUALITY = "";          // Default for: time image was taken
        public const string DEFAULT_COUNTER = "0";              // Default for: counters
        public const string DEFAULT_NOTE = "";                  // Default for: notes
        public const string DEFAULT_CHOICE = "";
        public const string DEFAULT_LIST = "";                  // Default for: list
        public const string DEFAULT_FLAG = "false";             // Default for: flags

        // Default widths for various text boxes
        public const string TXTBOXWIDTH_FILE = "100";
        public const string TXTBOXWIDTH_FOLDER = "100";
        public const string TXTBOXWIDTH_DATE = "100";
        public const string TXTBOXWIDTH_TIME = "100";
        public const string TXTBOXWIDTH_IMAGEQUALITY = "80";
        public const string CHKBOXWIDTH_FLAG = "20";
        public const string TXTBOXWIDTH_COUNTER = "80";
        public const string TXTBOXWIDTH_NOTE = "100";
        public const string TXTBOXWIDTH_CHOICE = "100";

        public const string LIST_IMAGEQUALITY = "Ok| Dark| Corrupted | Missing";

        // Tooltip strings
        public const string TOOLTIP_FILE = "The image file name";
        public const string TOOLTIP_FOLDER = "Name of the folder containing the images";
        public const string TOOLTIP_DATE = "Date the image was taken";
        public const string TOOLTIP_TIME = "Time the image was taken";
        public const string TOOLTIP_IMAGEQUALITY = "System-determined image quality: Ok, dark if mostly black, corrupted if it can not be read";
        public const string TOOLTIP_DELETEFLAG = "Mark an image as one to be deleted. You can then confirm deletion through the Edit Menu";
        public const string TOOLTIP_COUNTER = "Click the counter button, then click on the image to count the entity. Or just type in a count";
        public const string TOOLTIP_NOTE = "Write a textual note";
        public const string TOOLTIP_CHOICE = "Choose an item from the menu";
        public const string TOOLTIP_FLAG = "Toggle between true and false";
    }
}
