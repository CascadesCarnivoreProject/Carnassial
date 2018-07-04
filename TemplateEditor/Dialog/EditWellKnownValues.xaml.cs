using Carnassial.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Carnassial.Editor.Dialog
{
    public partial class EditWellKnownValues : Window
    {
        private static readonly string[] NewLineDelimiter = { Environment.NewLine };

        private UIElement positionReference;

        public List<string> Values { get; private set; }

        public EditWellKnownValues(UIElement positionReference, List<string> wellKnownValues, Window owner)
        {
            this.InitializeComponent();

            this.ValuesList.Text = String.Join(Environment.NewLine, wellKnownValues);
            this.Values = wellKnownValues;
            this.OkButton.IsEnabled = false;
            this.Owner = owner;
            this.positionReference = positionReference;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Point topLeft = this.positionReference.PointToScreen(new Point(0, 0));
            this.Top = topLeft.Y - this.ActualHeight;
            if (this.Top < 0)
            {
                this.Top = 0;
            }
            this.Left = topLeft.X;

            // on some older Windows versions the above positioning doesn't work as the list ends up to the right of the main window
            // Check to make sure the popup dialog is over the main window, and if not, try to position it there.
            if (Application.Current != null)
            {
                double choiceRightSide = this.Left + this.ActualWidth;
                double mainWindowRightSide = Application.Current.MainWindow.Left + Application.Current.MainWindow.ActualWidth;
                if (choiceRightSide > mainWindowRightSide)
                {
                    this.Left = mainWindowRightSide - this.ActualWidth - 100;
                }
            }

            Utilities.TryFitWindowInWorkingArea(this);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void ItemList_TextChanged(object sender, TextChangedEventArgs e)
        {
            this.Values = new List<string>(this.ValuesList.Text.Split(EditWellKnownValues.NewLineDelimiter, StringSplitOptions.None));
            List<string> uniqueValues = this.Values.Distinct().ToList();
            this.OkButton.IsEnabled = this.Values.Count > 0 && (this.Values.Count == uniqueValues.Count);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
    }
}
