using System;
using System.Windows;
using System.Windows.Controls;

namespace Carnassial.Util
{
    internal static class GridExtensions
    {
        public static TElement GetChild<TElement>(this Grid grid, int row, int column) where TElement : UIElement
        {
            for (int index = 0; index < grid.Children.Count; ++index)
            {
                UIElement child = grid.Children[index];
                if (Grid.GetRow(child) == row)
                {
                    if (Grid.GetColumn(child) == column)
                    {
                        return (TElement)child;
                    }
                }
            }

            throw new ArgumentException(nameof(row) + " or " + nameof(column), "Grid has no child at row " + row + " and column " + column + ".");
        }

        public static void ReplaceOrAddChild(this Grid grid, int row, int column, UIElement newChild)
        {
            Grid.SetColumn(newChild, column);
            Grid.SetRow(newChild, row);

            if (grid.TryRemoveChild(row, column, out int insertIndex) == false)
            {
                for (int index = 0; index < grid.Children.Count; ++index)
                {
                    UIElement child = grid.Children[index];
                    if (Grid.GetRow(child) == row)
                    {
                        if (Grid.GetColumn(child) > column)
                        {
                            insertIndex = index;
                            break;
                        }
                    }
                    else if (Grid.GetRow(child) > row)
                    {
                        insertIndex = index;
                        break;
                    }
                }
            }

            if (insertIndex < 0)
            {
                grid.Children.Add(newChild);
            }
            else
            {
                grid.Children.Insert(insertIndex, newChild);
            }
        }

        public static bool TryRemoveChild(this Grid grid, int row, int column)
        {
            return grid.TryRemoveChild(row, column, out int _);
        }

        public static bool TryRemoveChild(this Grid grid, int row, int column, out int existingIndex)
        {
            for (int index = 0; index < grid.Children.Count; ++index)
            {
                UIElement child = grid.Children[index];
                if (Grid.GetRow(child) == row)
                {
                    if (Grid.GetColumn(child) == column)
                    {
                        existingIndex = index;
                        grid.Children.Remove(child);
                        return true;
                    }
                }
            }

            existingIndex = -1;
            return false;
        }
    }
}
