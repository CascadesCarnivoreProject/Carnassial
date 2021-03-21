using Carnassial.Data;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Controls;
using WpfControl = System.Windows.Controls.Control;

namespace Carnassial.Editor.Util
{
    internal static class DataGridCellExtensions
    {
        public static bool ShouldDisableCell(ControlRow control, string columnHeader)
        {
            // columns disabled in xaml: ID, Type, ControlOrder, SpreadsheetOrder
            // These columns are always editable:
            //   Constant.ControlColumn.Label
            //   Constant.ControlColumn.Tooltip
            //   Constant.ControlColumn.Visible
            //   EditorConstant.ColumnHeader.MaxWidth
            if ((control.ControlType == ControlType.DateTime) ||
                String.Equals(control.DataLabel, Constant.FileColumn.File, StringComparison.Ordinal))
            {
                // these standard controls have no editable properties other than width
                return columnHeader != EditorConstant.ColumnHeader.MaxWidth;
            }
            else if (String.Equals(control.DataLabel, Constant.FileColumn.Classification, StringComparison.Ordinal) ||
                     String.Equals(control.DataLabel, Constant.FileColumn.RelativePath, StringComparison.Ordinal) ||
                     String.Equals(control.DataLabel, Constant.FileColumn.UtcOffset, StringComparison.Ordinal))
            {
                // standard controls whose visible and width can be changed
                return (String.Equals(columnHeader, Constant.ControlColumn.Visible, StringComparison.Ordinal) == false) &&
                       (String.Equals(columnHeader, EditorConstant.ColumnHeader.MaxWidth, StringComparison.Ordinal) == false);
            }
            else if (String.Equals(columnHeader, EditorConstant.ColumnHeader.AnalysisLabel, StringComparison.Ordinal))
            {
                return control.Copyable == false;
            }
            else if (String.Equals(control.DataLabel, Constant.FileColumn.DeleteFlag, StringComparison.Ordinal))
            {
                return (String.Equals(columnHeader, Constant.ControlColumn.Copyable, StringComparison.Ordinal) == false) &&
                       (String.Equals(columnHeader, Constant.ControlColumn.Visible, StringComparison.Ordinal) == false) &&
                       (String.Equals(columnHeader, EditorConstant.ColumnHeader.MaxWidth, StringComparison.Ordinal) == false);
            }
            else if ((control.ControlType == ControlType.Counter) ||
                     (control.ControlType == ControlType.Flag))
            {
                // all properties are editable except list
                return String.Equals(columnHeader, EditorConstant.ColumnHeader.WellKnownValues, StringComparison.Ordinal);
                // for notes and choices all properties including list are editable
            }

            return false;
        }

        public static bool TryGetControl<TControl>(this DataGridCell cell, [NotNullWhen(true)] out TControl? control) where TControl : WpfControl
        {
            control = cell.Content as TControl;
            return control != null;
        }
    }
}
