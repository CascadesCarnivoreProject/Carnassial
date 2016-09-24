using Carnassial.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Carnassial.Editor.Dialog
{
    public partial class EditChoiceList : Window
    {
        private static readonly string[] NewLineDelimiter = { Environment.NewLine };

        private UIElement positionReference;

        public List<string> Choices { get; private set; }

        public EditChoiceList(UIElement positionReference, List<string> choices, Window owner)
        {
            this.InitializeComponent();

            this.ChoiceList.Text = String.Join(Environment.NewLine, choices);
            this.Choices = choices;
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

            Utilities.TryFitWindowInWorkingArea(this);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void ItemList_TextChanged(object sender, TextChangedEventArgs e)
        {
            this.Choices = new List<string>(this.ChoiceList.Text.Split(EditChoiceList.NewLineDelimiter, StringSplitOptions.None));
            List<string> uniqueChoices = this.Choices.Distinct().ToList();
            this.OkButton.IsEnabled = this.Choices.Count > 0 && (this.Choices.Count == uniqueChoices.Count);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
    }
}
