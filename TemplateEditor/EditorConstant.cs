using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Media;

namespace Carnassial.Editor
{
    internal static class EditorConstant
    {
        public const string ApplicationName = "CarnassialTemplateEditor";
        public const string MainWindowBaseTitle = "Carnassial Template Editor";  // The initial title shown in the window title bar

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

        public static class ColumnHeader
        {
            // data grid column headers
            // these are human friendly forms of data labels
            // these constants are duplicated in EditorWindow.xaml and must be kept in sync
            public const string AnalysisLabel = "Analysis\nLabel";
            public const string ControlOrder = "Control\nOrder";
            public const string DataLabel = "Data Label";
            public const string DefaultValue = "Default Value";
            public const string ID = "ID";
            public const string MaxWidth = "Max\nWidth";
            public const string SpreadsheetOrder = "Spreadsheet\nOrder";
            public const string WellKnownValues = "Well Known\nValues";
        }

        public static class Registry
        {
            public static class EditorKey
            {
                // key containing the list of most recent template databases opened by the editor
                public const string MostRecentlyUsedTemplates = "MostRecentlyUsedTemplates";
            }
        }

        // keys in EditorWindowStyle.xaml
        public static class StyleResources
        {
            public const string ControlGridDisabledCellBackground = "ControlGridDisabledCellBackground";
            public const string ControlGridDisabledCellForeground = "ControlGridDisabledCellForeground";
            public const string ControlGridEnabledCellBackground = "ControlGridEnabledCellBackground";
            public const string ControlGridEnabledCellForeground = "ControlGridEnabledCellForeground";
        }
    }
}