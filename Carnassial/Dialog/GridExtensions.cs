using System.Windows;
using System.Windows.Controls;

namespace Carnassial.Dialog
{
    internal static class GridExtensions
    {
        public static void ReplaceOrAddChild(this Grid grid, int row, int column, UIElement newChild)
        {
            grid.TryRemoveChild(row, column);

            Grid.SetColumn(newChild, column);
            Grid.SetRow(newChild, row);
            grid.Children.Add(newChild);
        }

        public static bool TryRemoveChild(this Grid grid, int row, int column)
        {
            foreach (UIElement child in grid.Children)
            {
                if (Grid.GetRow(child) == row)
                {
                    if (Grid.GetColumn(child) == column)
                    {
                        grid.Children.Remove(child);
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
