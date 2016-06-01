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
        public static readonly ReadOnlyCollection<string> ReservedSqlKeywords = new List<string>()
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

            // labels for the few cases where the default label isn't the same as the data label
            public const string MarkForDeletionLabel = "Delete?";        // Label for: the Deletion
        }
    }
}
