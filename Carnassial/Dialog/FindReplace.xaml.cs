using Carnassial.Data;
using Carnassial.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Carnassial.Dialog
{
    public partial class FindReplace : FindDialog
    {
        private const int ReplaceRow = 2;
        private const int Term1Row = 0;
        private const int Term2Row = 1;
        private const int ValueColumn = 3;

        private static double MostRecentLeft;
        private static double MostRecentTop;

        private CarnassialWindow carnassial;

        static FindReplace()
        {
            FindReplace.MostRecentLeft = -10.0;
            FindReplace.MostRecentTop = -10.0;
        }

        public FindReplace(CarnassialWindow carnassial)
        {
            this.InitializeComponent();
            this.carnassial = carnassial;
            this.Owner = carnassial;
            
            this.FindTerm1Label.ItemsSource = this.carnassial.DataHandler.FindReplace.FindTerm1Labels;
            this.FindTerm1Label.SelectedItem = this.carnassial.DataHandler.FindReplace.FindTerm1.DataLabel;
            this.FindTerm1Label.Margin = Constant.UserInterface.FindCellMargin;
            this.FindTerm1Operator.Margin = Constant.UserInterface.FindCellMargin;
            this.FindTerm2Label.ItemsSource = this.carnassial.DataHandler.FindReplace.FindTerm2Labels;
            this.FindTerm2Label.Margin = Constant.UserInterface.FindCellMargin;
            this.FindTerm2Label.SelectedItem = Constant.UserInterface.NoFindValue;
            this.FindTerm2Operator.Margin = Constant.UserInterface.FindCellMargin;

            this.ReplaceTerm1Label.ItemsSource = this.carnassial.DataHandler.FindReplace.FindTerm1Labels;
            this.ReplaceTerm1Label.Margin = Constant.UserInterface.FindCellMargin;
            this.ReplaceTerm1Label.SelectedItem = this.carnassial.DataHandler.FindReplace.FindTerm1.DataLabel;
            this.ReplaceTerm1Operator.Margin = Constant.UserInterface.FindCellMargin;
            this.ReplaceTerm2Label.ItemsSource = this.carnassial.DataHandler.FindReplace.FindTerm2Labels;
            this.ReplaceTerm2Label.Margin = Constant.UserInterface.FindCellMargin;
            this.ReplaceTerm2Label.SelectedItem = Constant.UserInterface.NoFindValue;
            this.ReplaceTerm2Operator.Margin = Constant.UserInterface.FindCellMargin;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void FindField1Label_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.carnassial.DataHandler.FindReplace.FindTerm1 = this.RebuildFindField(this.FindTerm1Label, FindReplace.Term1Row, this.FindTerm1Operator, this.FindGrid);
        }

        private void FindField2Label_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SearchTerm term2 = this.RebuildFindField(this.FindTerm2Label, FindReplace.Term2Row, this.FindTerm2Operator, this.FindGrid);
            this.carnassial.DataHandler.FindReplace.FindTerm2 = term2;
            this.FindFieldCombiningLabel.Visibility = term2 != null ? Visibility.Visible : Visibility.Hidden;
        }

        private async void FindNext_Click(object sender, RoutedEventArgs e)
        {
            if (this.carnassial.DataHandler.TryFindNext(out int fileIndex))
            {
                int currentIndex = this.carnassial.DataHandler.ImageCache.CurrentRow;
                if (currentIndex == fileIndex)
                {
                    this.MessageBar.Text = "Only one matching file found.  Search is complete.";
                }
                else
                {
                    await this.carnassial.ShowFileAsync(fileIndex);
                    this.MessageBar.Text = null;
                }
            }
            else
            {
                this.MessageBar.Text = "No matching file found.";
            }
        }

        private async void FindPrevious_Click(object sender, RoutedEventArgs e)
        {
            if (this.carnassial.DataHandler.TryFindPrevious(out int fileIndex))
            {
                int currentIndex = this.carnassial.DataHandler.ImageCache.CurrentRow;
                if (currentIndex == fileIndex)
                {
                    this.MessageBar.Text = "Only one matching file found.  Search is complete.";
                }
                else
                {
                    await this.carnassial.ShowFileAsync(fileIndex);
                    this.MessageBar.Text = null;
                }
            }
            else
            {
                this.MessageBar.Text = "No matching file found.";
            }
        }

        private void FindReplaceTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Object.ReferenceEquals(e.OriginalSource, this.FindReplaceTabs) == false)
            {
                // ignore selection changed events which bubble up from combo boxes (or potentially other controls within the 
                // tab items)
                return;
            }

            if (String.Equals((string)this.FindTerm1Label.SelectedItem, (string)this.ReplaceTerm1Label.SelectedItem, StringComparison.Ordinal) == false)
            {
                if (this.FindTab.IsSelected)
                {
                    this.FindTerm1Label.SelectedItem = this.ReplaceTerm1Label.SelectedItem;
                }
                else
                {
                    Debug.Assert(this.ReplaceTab.IsSelected, "Expected replace tab to be selected.");
                    this.ReplaceTerm1Label.SelectedItem = this.FindTerm1Label.SelectedItem;
                }
            }

            if (String.Equals((string)this.FindTerm2Label.SelectedItem, (string)this.ReplaceTerm2Label.SelectedItem, StringComparison.Ordinal) == false)
            {
                if (this.FindTab.IsSelected)
                {
                    this.FindTerm2Label.SelectedItem = this.ReplaceTerm2Label.SelectedItem;
                }
                else
                {
                    Debug.Assert(this.ReplaceTab.IsSelected, "Expected replace tab to be selected.");
                    this.ReplaceTerm2Label.SelectedItem = this.FindTerm2Label.SelectedItem;
                }
            }
        }

        private SearchTerm RebuildFindField(ComboBox findLabel, int row, ComboBox findOperator, Grid grid)
        {
            if (String.Equals((string)findLabel.SelectedItem, Constant.UserInterface.NoFindValue, StringComparison.Ordinal))
            {
                findOperator.SelectedItem = null;
                grid.TryRemoveChild(row, FindReplace.ValueColumn);
                return null;
            }

            ControlRow selectedControl = this.carnassial.DataHandler.FileDatabase.Controls.Single(control => String.Equals((string)findLabel.SelectedItem, control.Label, StringComparison.Ordinal));
            SearchTerm findTerm = selectedControl.CreateSearchTerm();
            findTerm.UseForSearching = true;

            FrameworkElement findValue = this.CreateValueControl(findTerm, this.carnassial.DataHandler.FileDatabase);
            findLabel.Tag = findTerm;
            findOperator.Tag = findTerm;
            findValue.Tag = findTerm;
            grid.ReplaceOrAddChild(row, FindReplace.ValueColumn, findValue);

            // for now, glob is not supported
            // Update FileFindReplace.MatchString() if this changes.
            List<string> operators = this.GetOperators(findTerm);
            operators.Remove(Constant.SearchTermOperator.Glob);
            findOperator.ItemsSource = operators;
            findOperator.SelectedItem = findTerm.Operator;

            return findTerm;
        }

        private void ReplaceAll_Click(object sender, RoutedEventArgs e)
        {
            this.carnassial.DataHandler.ReplaceAll();
        }

        private void ReplaceField1Label_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SearchTerm findTerm1 = this.RebuildFindField(this.ReplaceTerm1Label, FindReplace.Term1Row, this.ReplaceTerm1Operator, this.ReplaceGrid);
            this.carnassial.DataHandler.FindReplace.FindTerm1 = findTerm1; 
            this.ReplaceLabel.Content = (string)this.ReplaceTerm1Label.SelectedItem;

            this.carnassial.DataHandler.FindReplace.ReplaceTerm = findTerm1.Clone();
            UIElement replaceValue = this.CreateValueControl(this.carnassial.DataHandler.FindReplace.ReplaceTerm, this.carnassial.DataHandler.FileDatabase);
            this.ReplaceGrid.ReplaceOrAddChild(FindReplace.ReplaceRow, FindReplace.ValueColumn, replaceValue);
        }

        private void ReplaceField2Label_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SearchTerm term2 = this.RebuildFindField(this.ReplaceTerm2Label, FindReplace.Term2Row, this.ReplaceTerm2Operator, this.ReplaceGrid);
            this.carnassial.DataHandler.FindReplace.FindTerm2 = term2;
            this.ReplaceFieldCombiningLabel.Visibility = term2 != null ? Visibility.Visible : Visibility.Hidden;
        }

        private async void ReplaceNext_Click(object sender, RoutedEventArgs e)
        {
            ImageRow currentFile = this.carnassial.DataHandler.ImageCache.Current;
            if (this.carnassial.DataHandler.FindReplace.Matches(currentFile))
            {
                this.carnassial.DataHandler.FindReplace.TryReplace(currentFile);
            }

            if (this.carnassial.DataHandler.TryFindNext(out int fileIndex))
            {
                Debug.Assert(this.carnassial.DataHandler.FindReplace.ReplaceTerm != null, "A replacement database value must be available.");
                await this.carnassial.ShowFileAsync(fileIndex);
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (FindReplace.MostRecentLeft >= 0.0)
            {
                this.Left = FindReplace.MostRecentLeft;
                this.Top = FindReplace.MostRecentTop;
            }
            else
            {
                CommonUserInterface.SetDefaultDialogPosition(this);
                CommonUserInterface.TryFitWindowInWorkingArea(this);
            }
        }
    }
}
