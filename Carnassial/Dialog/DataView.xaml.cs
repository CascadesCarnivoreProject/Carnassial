using Carnassial.Database;
using Carnassial.Images;
using Carnassial.Util;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;

namespace Carnassial.Dialog
{
    /// <summary>
    /// Display a dialog box showing the current contents of the dataTable in the database
    /// Also highlight the current row representing the currently displayed file.
    /// </summary>
    public partial class DataView : Window
    {
        private static int lastIndex = -1; // Keep track of the last index we had selected. Don't autoscroll if it hasn't changed.

        private ImageDatabase database;
        private ImageCache imageCache;
        private DispatcherTimer dispatcherTimer;

        internal DataView(ImageDatabase database, ImageCache imageCache)
        {
            this.InitializeComponent();

            this.database = database;
            this.imageCache = imageCache;
        }

        /// <summary>
        /// Refresh the link to the dataTable being monitored
        /// </summary>
        public void RefreshDataTable()
        {
            this.dataGrid.Items.Refresh();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            this.database.BindToDataGrid(null, null);
        }

        // This timer monitors the value of the current row. 
        // If the current row changes, it resets the selected index to it.
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Utilities.SetDefaultDialogPosition(this);
            Utilities.TryFitWindowInWorkingArea(this);

            this.database.BindToDataGrid(this.dataGrid, null);
            this.RefreshDataTable();

            this.dispatcherTimer = new DispatcherTimer();
            this.dispatcherTimer.Tick += new EventHandler(this.DispatcherTimer_Tick);
            this.dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            this.dispatcherTimer.Start();
        }

        /// <summary>Ensure that the the highlighted row is the current row </summary>
        private void DispatcherTimer_Tick(object sender, EventArgs e)
        {
            // Set the selected index to the current row that represents the image being viewed
            this.dataGrid.SelectedIndex = this.imageCache.CurrentRow; 

            // A workaround to autoscroll the currently selected items, where the item always appears at the top of the window.
            // We check the last index and only autoscroll if it hasn't changed since then.
            // This workaround means that the user can manually scroll to a new spot, where it won't jump back unless the image number has changed.
            if (lastIndex != this.dataGrid.SelectedIndex)
            { 
                this.dataGrid.ScrollIntoView(dataGrid.Items[this.dataGrid.Items.Count - 1]);
                this.dataGrid.UpdateLayout();
                // Try to autoscroll so at least 5 rows are visible (if possible) before the selected row
                int rowToShow = (this.dataGrid.SelectedIndex > 5) ? this.dataGrid.SelectedIndex - 5 : 0;
                this.dataGrid.ScrollIntoView(this.dataGrid.Items[rowToShow]);
                lastIndex = this.dataGrid.SelectedIndex;
            }
        }

        /// <summary>The user clicked ok. Stop the dispatcher, and return</summary>
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.dispatcherTimer.Stop();
            this.dispatcherTimer.Tick -= new EventHandler(this.DispatcherTimer_Tick);
            this.Close();
            return;
        }
    }
}
