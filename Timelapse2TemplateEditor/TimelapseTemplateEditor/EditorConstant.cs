using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Media;
using Timelapse;

namespace TimelapseTemplateEditor
{
    internal static class EditorConstant
    {
        // If the DataGrid columns change this needs to be adjusted to index correctly.
        // Could not find a way to reference the DataGrid items by name.
        public const int DataGridTypeColumnIndex = 3;

        public const string MainWindowBaseTitle = "Timelapse Template Editor";  // The initial title shown in the window title bar

        public static readonly SolidColorBrush NotEditableCellColor = Brushes.LightGray; // Color of non-editable data grid items 

        // reserved words that cannot be used as a data label
        public static readonly ReadOnlyCollection<string> ReservedWords = new List<string>()
        {
            "ABORT", "ACTION", "ADD", "AFTER", "ALL", "ALTER", "ANALYZE", "AND", "AS", "ASC", "ATTACH", "AUTOINCREMENT", "BEFORE", "BEGIN", "BETWEEN",
            "BY", "CASCADE", "CASE", "CAST", "CHECK", "COLLATE", "COLUMN", "COMMIT", "CONFLICT", "CONSTRAINT", "CREATE", "CROSS", "CURRENT_DATE",
            "CURRENT_TIME", "CURRENT_TIMESTAMP", "DATABASE", "DEFAULT", "DEFERRABLE", "DEFERRED", "DELETE", "DESC", "DETACH", "DISTINCT", "DROP",
            "EACH", "ELSE", "END", "ESCAPE", "EXCEPT", "EXCLUSIVE", "EXISTS", "EXPLAIN", "FAIL", "FOR", "FOREIGN", "FROM", "FULL", "GLOB", "GROUP",
            "HAVING", "ID", "IF", "IGNORE", "IMMEDIATE", "IN", "INDEX", "INDEXED", "INITIALLY", "INNER", "INSERT", "INSTEAD", "INTERSECT", "INTO",
            "IS", "ISNULL", "JOIN", "KEY", "LEFT", "LIKE", "LIMIT", "MATCH", "NATURAL", "NO", "NOT", "NOTNULL", "NULL", "OF", "OFFSET", "ON", "OR",
            "ORDER", "OUTER", "PLAN", "PRAGMA", "PRIMARY", "QUERY", "RAISE", "RECURSIVE", "REFERENCES", "REGEXP", "REINDEX", "RELEASE", "RENAME",
            "REPLACE", "RESTRICT", "RIGHT", "ROLLBACK", "ROW", "SAVEPOINT", "SELECT", "SET", "TABLE", "TEMP", "TEMPORARY", "THEN", "TO", "TRANSACTION",
            "TRIGGER", "UNION", "UNIQUE", "UPDATE", "USING", "VACUUM", "VALUES", "VIEW", "VIRTUAL", "WHEN", "WHERE", "WITH", "WITHOUT"
        }.AsReadOnly();

        // a few control values not needed in Constants.Control
        public static class Control
        {
            // columns
            public const string MarkForDeletion = "MarkForDeletion";     // Data Label for: the Deletion

            // default data labels
            public const string Choice = "Choice";            // Label for: a choice

            // labels for the few cases where the default label isn't the same as the data label
            public const string MarkForDeletionLabel = "Delete?";        // Label for: the Deletion
        }

        // default values for controls
        public static class DefaultValue
        {
            public const string Choice = "";
            public const string Counter = "0";              // Default for: counters
            public const string Date = "";                  // Default for: date image was taken
            public const string File = "";                  // Default for: the file name
            public const string Flag = Constants.Boolean.False;             // Default for: flags
            public const string Folder = "";                // Default for: the folder path
            public const string ImageQuality = "";          // Default for: time image was taken
            public const string List = "";                  // Default for: list
            public const string Note = "";                  // Default for: notes
            public const string Time = "";                  // Default for: time image was taken
        }

        // default widths for various text boxes
        public static class DefaultWidth
        {
            public const string Choice = "100";
            public const string Counter = "80";
            public const string Date = "100";
            public const string File = "100";
            public const string Flag = "20";
            public const string Folder = "100";
            public const string ImageQuality = "80";
            public const string Note = "100";
            public const string Time = "100";
        }

        // default tooltips for controls
        public static class DefaultTooltip
        {
            public const string Counter = "Click the counter button, then click on the image to count the entity. Or just type in a count";
            public const string Flag = "Toggle between true and false";
            public const string Note = "Write a textual note";
            public const string Choice = "Choose an item from the menu";
        }

        public static class Sql
        {
            // database query phrases
            public const string ByControlSortOrder = " ORDER BY " + Constants.Control.ControlOrder;
        }

        // tooltips for well known controls/columns
        public static class Tooltip
        {
            public const string Date = "Date the image was taken";
            public const string File = "The image file name";
            public const string Folder = "Name of the folder containing the images";
            public const string ImageQuality = "System-determined image quality: Ok, dark if mostly black, corrupted if it can not be read";
            public const string MarkForDeletion = "Mark an image as one to be deleted. You can then confirm deletion through the Edit Menu";
            public const string Time = "Time the image was taken";
        }
    }
}
