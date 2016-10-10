using System;
using System.ComponentModel;
using System.Windows.Controls;

namespace Carnassial.Util
{
    public static class DataGridExtensions
    {
        public static void SelectAndScrollIntoView(this DataGrid dataGrid, int rowIndex)
        {
            bool indexIncreasing = rowIndex > dataGrid.SelectedIndex;
            dataGrid.SelectedIndex = rowIndex;

            // try to scroll so at least 5 rows are visible beyond the selected row
            int scrollIndex;
            if (indexIncreasing)
            {
                scrollIndex = Math.Min(rowIndex + 5, dataGrid.Items.Count - 1);
            }
            else
            {
                scrollIndex = Math.Max(rowIndex - 5, 0);
            }
            dataGrid.ScrollIntoView(dataGrid.Items[scrollIndex]);
        }

        public static void SortByFirstColumnAscending(this DataGrid dataGrid)
        {
            // Clear current sort descriptions
            dataGrid.Items.SortDescriptions.Clear();

            // Add the new sort description
            DataGridColumn firstColumn = dataGrid.Columns[0];
            ListSortDirection sortDirection = ListSortDirection.Ascending;
            dataGrid.Items.SortDescriptions.Add(new SortDescription(firstColumn.SortMemberPath, sortDirection));

            // Apply sort
            foreach (DataGridColumn column in dataGrid.Columns)
            {
                column.SortDirection = null;
            }
            firstColumn.SortDirection = sortDirection;

            // Refresh items to display sort
            dataGrid.Items.Refresh();
        }
    }
}
