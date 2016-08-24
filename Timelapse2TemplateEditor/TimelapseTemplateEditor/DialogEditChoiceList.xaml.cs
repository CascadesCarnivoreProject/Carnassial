using System;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Util;

namespace Timelapse.Editor
{
    public partial class DialogEditChoiceList : Window
    {
        private UIElement positionReference;

        public DialogEditChoiceList(UIElement positionReference, string choices)
        {
            this.InitializeComponent();
            Utilities.TryFitWindowInWorkingArea(this);

            this.ChoiceList.Text = choices;
            this.OkButton.IsEnabled = false;
            this.positionReference = positionReference;
        }

        /// <summary>
        /// Gets the modified text that can be accessed immediately after the dialog exits
        /// </summary>
        public string Choices
        {
            get { return this.ChoiceList.Text; }
        }

        // Position the window so it appears as a popup with its bottom aligned to the top of its owner button
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Point topLeft = this.positionReference.PointToScreen(new Point(0, 0));
            this.Top = topLeft.Y - this.ActualHeight;
            this.Left = topLeft.X;

            // On some older window systems, the above positioning doesn't work, where it seems to put it the the right of the main window
            // So we check to make sure it's in the main window, and if not, we try to position it there
            if (Application.Current != null)
            {
                double choiceRightSide = this.Left + this.ActualWidth;
                double mainWindowRightSide = Application.Current.MainWindow.Left + Application.Current.MainWindow.ActualWidth;
                if (choiceRightSide > mainWindowRightSide)
                {
                    this.Left = mainWindowRightSide - this.ActualWidth - 100;
                }
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        // Enable the Ok button for non-empty text
        private void ItemList_TextChanged(object sender, TextChangedEventArgs e)
        {
            this.OkButton.IsEnabled = !this.ChoiceList.Text.Trim().Equals(String.Empty);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
    }
}
