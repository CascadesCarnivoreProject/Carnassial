using System.ComponentModel;
using System.Windows.Controls;

namespace Timelapse.Util
{
    public static class DataGridExtensions
    {
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
