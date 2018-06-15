using Carnassial.Data;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Carnassial.Editor.Util
{
    internal static class DataGridCellExtensions
    {
        public static bool ShouldDisable(this DataGridCell cell, ControlRow control, string columnHeader)
        {
            // columns disabled in xaml: ID, Type, ControlOrder, SpreadsheetOrder
            // columns are always editable:
            //   Constant.Control.Label
            //   Constant.Control.Tooltip
            //   Constant.Control.Visible
            //   EditorConstant.ColumnHeader.MaxWidth
            if ((control.Type == ControlType.DateTime) ||
                String.Equals(control.DataLabel, Constant.DatabaseColumn.File, StringComparison.Ordinal))
            {
                // these standard controls have no editable properties other than width
                return columnHeader != EditorConstant.ColumnHeader.MaxWidth;
            }
            else if (String.Equals(control.DataLabel, Constant.DatabaseColumn.ImageQuality, StringComparison.Ordinal) ||
                     String.Equals(control.DataLabel, Constant.DatabaseColumn.RelativePath, StringComparison.Ordinal) ||
                     String.Equals(control.DataLabel, Constant.DatabaseColumn.UtcOffset, StringComparison.Ordinal))
            {
                // standard controls whose visible and width can be changed
                return (String.Equals(columnHeader, Constant.Control.Visible, StringComparison.Ordinal) == false) &&
                       (String.Equals(columnHeader, EditorConstant.ColumnHeader.MaxWidth, StringComparison.Ordinal) == false);
            }
            else if (String.Equals(columnHeader, EditorConstant.ColumnHeader.AnalysisLabel, StringComparison.Ordinal))
            {
                return control.Copyable == false;
            }
            else if (String.Equals(control.DataLabel, Constant.DatabaseColumn.DeleteFlag))
            {
                return (String.Equals(columnHeader, Constant.Control.Copyable, StringComparison.Ordinal) == false) &&
                       (String.Equals(columnHeader, Constant.Control.Visible, StringComparison.Ordinal) == false) &&
                       (String.Equals(columnHeader, EditorConstant.ColumnHeader.MaxWidth, StringComparison.Ordinal) == false);
            }
            else if ((control.Type == ControlType.Counter) ||
                     (control.Type == ControlType.Flag))
            {
                // all properties are editable except list
                return String.Equals(columnHeader, Constant.Control.List, StringComparison.Ordinal);
                // for notes and choices all properties including list are editable
            }

            return false;
        }

        public static bool TryGetControl<TControl>(this DataGridCell cell, out TControl control) where TControl : System.Windows.Controls.Control
        {
            control = cell.Content as TControl;
            return control != null;
        }
    }
}
