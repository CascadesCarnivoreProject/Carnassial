using System;
using System.Windows;
using System.Windows.Controls;

namespace TimelapseTemplateEditor
{
    /// <summary>
    /// Interaction logic for DlgEditChoiceList.xaml
    /// </summary>
    public partial class DlgEditChoiceList : Window
    {

        /// <summary>
        /// Contains the modified text that can be accessed immediately after the dialog exits
        /// </summary>
        public string ItemList { get; set; }
        private Button owner;
        public DlgEditChoiceList(Button owner, string list)
        {
            InitializeComponent();
            this.ItemList = list;
            this.tbItemList.Text = this.ItemList;
            this.OkButton.IsEnabled = false;
            this.owner = owner;
        }

        // Position the window  so it appears as a popup with its bottom aligned to the top of its owner button
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Point p = this.owner.PointToScreen(new Point(0, 0));
            this.Top = p.Y - this.ActualHeight ;
            this.Left = p.X ;

            // On some older window systems, the above positioning doesn't work, where it seems to put it the the right of the main window
            // So we check to make sure its in the main window, and if not, we try to position it there
            double choice_right_side = this.Left + this.ActualWidth;
            double main_window_right_side = Application.Current.MainWindow.Left + Application.Current.MainWindow.ActualWidth;
            if (choice_right_side > main_window_right_side)
            {
                this.Left = main_window_right_side - this.ActualWidth - 100;
            }
        }

        #region Private Methods
        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            this.ItemList = this.tbItemList.Text;
            this.DialogResult = true;
        }

        // Enable the Ok button for non-empty text
        private void tbLog_TextChanged(object sender, TextChangedEventArgs e)
        {
            this.OkButton.IsEnabled = (!this.tbItemList.Text.Trim().Equals(String.Empty));
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
        #endregion
    }
}
