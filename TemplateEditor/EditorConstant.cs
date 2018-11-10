using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Media;

namespace Carnassial.Editor
{
    internal static class EditorConstant
    {
        public const string ApplicationName = "CarnassialTemplateEditor";
        public const string MainWindowBaseTitle = "Carnassial Template Editor";  // The initial title shown in the window title bar

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

        public static class Resources
        {
            public const string DefaultValueChoiceComboBox = "DefaultValueChoiceComboBox";
            public const string DefaultValueFlagComboBox = "DefaultValueFlagComboBox";
            public const string DefaultValueTextBox = "DefaultValueTextBox";

            public const string DisplayFalseString = "False (unchecked)";
            public const string DisplayTrueString = "True (checked)";
        }

        public static class ResourceKey
        {
            public const string AboutEditorTermsOfUse = "AboutEditor.TermsOfUse";
            public const string EditorWindowTemplateLoadFailed = "EditorWindow.TemplateLoad.Failed";
            public const string EditorWindowDataLabelEmpty = "EditorWindow.DataLabel.Empty";
            public const string EditorWindowDataLabelNotUnique = "EditorWindow.DataLabel.NotUnique";
            public const string EditorWindowException = "EditorWindow.Exception";
            public const string EditorWindowTemplateFileOpenExisting = "EditorWindow.TemplateFile.OpenExisting";
            public const string EditorWindowTemplateFileOpenExistingFilter = "EditorWindow.TemplateFile.OpenExistingFilter";
            public const string EditorWindowTemplateFileSaveNew = "EditorWindow.TemplateFile.SaveNew";
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