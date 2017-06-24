using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Carnassial.Editor.Util
{
    internal static class DataGridCellExtensions
    {
        public static bool TryGetControl<TControl>(this DataGridCell cell, out TControl control) where TControl : System.Windows.Controls.Control
        {
            control = cell.Content as TControl;
            return control != null;
        }
    }
}
