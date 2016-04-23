using System;
using System.Windows;
using System.Windows.Threading;
using Timelapse.Database;

namespace Timelapse
{
    /// <summary>
    /// Display a dialog box showing the current contents of the dataTable in the database
    /// Also highlight the current row representing the currently displayed image.
    /// </summary>
    public partial class DialogDataView : Window
    {
        private static int lastIndex = -1; // Keep track of the last index we had selected. Don't autoscroll if it hasn't changed.

        private ImageDatabase database;
        private int defaultImageRow;
        private DispatcherTimer dispatcherTimer;
        private int lastRow = -1;

        public DialogDataView(ImageDatabase database, int defaultImageRow)
        {
            this.InitializeComponent();
            this.database = database;
            this.defaultImageRow = defaultImageRow;
            this.RefreshDataTable();
        }

        /// <summary>
        /// Refresh the link to the dataTable being monitored
        /// </summary>
        public void RefreshDataTable()
        {
            this.datagrid.ItemsSource = this.database.ImageDataTable.DefaultView;
        }

        #region Callbacks
        // This timer monitors the value of the current row. 
        // If the current row changes, it resets the selected index to it.
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.dispatcherTimer = new DispatcherTimer();
            this.dispatcherTimer.Tick += new EventHandler(this.DispatcherTimer_Tick);
            this.dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            this.dispatcherTimer.Start();
        }

        /// <summary>Ensure that the the highlighted row is the current row </summary>
        private void DispatcherTimer_Tick(object sender, EventArgs e)
        {
            if (this.lastRow != this.defaultImageRow)
            {
                this.datagrid.SelectedIndex = this.defaultImageRow;
            }

            // A workaround to autoscroll the currently selected items, where the item always appears at the top of the window.
            // We check the last index and only autoscroll if it hasn't changed since then.
            // This workaround means that the user can manually scroll to a new spot, where it won't jump back unless the image number has changed.
            if (lastIndex != this.datagrid.SelectedIndex)
            {
                this.datagrid.ScrollIntoView(datagrid.Items[this.datagrid.Items.Count - 1]);
                this.datagrid.UpdateLayout();
                this.datagrid.ScrollIntoView(this.datagrid.Items[this.datagrid.SelectedIndex]);
                lastIndex = this.datagrid.SelectedIndex;
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
        #endregion
    }
}
