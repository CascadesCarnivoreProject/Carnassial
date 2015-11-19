using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Timelapse
{
    /// <summary>
    /// Display a dialog box showing the current contents of the dataTable in the database
    /// Also highlight the current row representing the currently displayed image.
    /// </summary>
    public partial class DlgDataView : Window
    {
        private DBData dbData;
        private int last_row = -1;
        private DispatcherTimer dispatcherTimer;

        /// <summary>Constructor </summary>
        /// <param name="db"></param>
        public DlgDataView(DBData dbData)
        {
            InitializeComponent();
            this.dbData = dbData;
            RefreshDataTable ();
        }

        /// <summary>
        /// Refresh the link to the dataTable being monitored
        /// </summary>
        public void RefreshDataTable()
        {
            this.datagrid.ItemsSource = dbData.dataTable.DefaultView;
        }

        #region Callbacks
        // This timer monitors the value of the current row. 
        // If the current row changes, it resets the selected index to it.
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            dispatcherTimer = new DispatcherTimer();
            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimer.Start();
        }

        /// <summary> Ensure that the the highlit row is the current row </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        static int last_index = -1; // Keep track of the last index we had selected. Don't autoscroll if it hasn't changed.
        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            if (last_row != dbData.CurrentRow)
                this.datagrid.SelectedIndex = dbData.CurrentRow;
            // A workaround to autoscroll the currently selected items, where the item always appears at the top of the window.
            // We check the last index and only autoscroll if it hasn't changed since then.
            // This workaround means that the user can manually scroll to a new spot, where it won't jump back unless the image number has changed.
            if (last_index != this.datagrid.SelectedIndex)
            {
                this.datagrid.ScrollIntoView(datagrid.Items[datagrid.Items.Count - 1]);
                this.datagrid.UpdateLayout();
                this.datagrid.ScrollIntoView(this.datagrid.Items[datagrid.SelectedIndex]);
                last_index = this.datagrid.SelectedIndex;
            }
        }

        /// <summary> The user clicked ok. Stop the dispatcher, and return</summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            dispatcherTimer.Stop(); 
            dispatcherTimer.Tick -= new EventHandler(dispatcherTimer_Tick);
            this.Close();
            return;
        }
        #endregion
    }
}
