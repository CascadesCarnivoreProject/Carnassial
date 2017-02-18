using System.Windows.Controls;

namespace Carnassial.Editor.Util
{
    internal static class DataGridCellExtensions
    {
        public static bool TryGetCheckBox(this DataGridCell cell, out CheckBox checkBox)
        {
            checkBox = cell.Content as CheckBox;
            return checkBox != null;
        }

        public static bool TryGetTextBox(this DataGridCell cell, out TextBox textBox)
        {
            textBox = cell.Content as TextBox;
            return textBox != null;
        }
    }
}
