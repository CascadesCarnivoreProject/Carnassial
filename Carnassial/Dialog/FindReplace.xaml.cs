using Carnassial.Control;
using Carnassial.Data;
using Carnassial.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using WpfControl = System.Windows.Controls.Control;

namespace Carnassial.Dialog
{
    public partial class FindReplace : WindowWithSystemMenu
    {
        private const int ReplaceRow = 2;
        private const int ReplaceValueTabIndex = 16;
        private const int Term1Row = 0;
        private const int Term2Row = 1;
        private const int ValueColumn = 3;

        private static double MostRecentLeft;
        private static double MostRecentTop;

        private readonly CarnassialWindow carnassial;

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

            this.ReplaceTerm1Label.ItemsSource = this.carnassial.DataHandler.FindReplace.FindTerm1Labels;
            this.ReplaceTerm1Label.Margin = Constant.UserInterface.FindCellMargin;
            this.ReplaceTerm1Label.SelectedItem = this.carnassial.DataHandler.FindReplace.FindTerm1.DataLabel;
            this.ReplaceTerm1Operator.Margin = Constant.UserInterface.FindCellMargin;

            this.FindTerm2Label.ItemsSource = this.carnassial.DataHandler.FindReplace.FindTerm2Labels;
            this.FindTerm2Label.Margin = Constant.UserInterface.FindCellMargin;
            this.FindTerm2Operator.Margin = Constant.UserInterface.FindCellMargin;

            this.ReplaceTerm2Label.ItemsSource = this.carnassial.DataHandler.FindReplace.FindTerm2Labels;
            this.ReplaceTerm2Label.Margin = Constant.UserInterface.FindCellMargin;
            this.ReplaceTerm2Operator.Margin = Constant.UserInterface.FindCellMargin;

            if (this.carnassial.DataHandler.FindReplace.FindTerm2 != null)
            {
                this.FindTerm2Label.SelectedItem = this.carnassial.DataHandler.FindReplace.FindTerm2.DataLabel;
                this.ReplaceTerm2Label.SelectedItem = this.carnassial.DataHandler.FindReplace.FindTerm2.DataLabel;
            }
            else
            {
                this.FindTerm2Label.SelectedItem = Constant.UserInterface.NoFindValue;
                this.ReplaceTerm2Label.SelectedItem = Constant.UserInterface.NoFindValue;
                // nothing to set for FindTerm2Operator.SelectedItem and ReplaceTerm2Operator.SelectedItem
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private SearchTerm EnsureAndBindSearchTerm(SearchTerm existingTerm, int row, Grid changingGrid, ComboBox changedLabel, ComboBox changingOperator, Grid synchronizingGrid, ComboBox synchronizingLabel, ComboBox synchronizingOperator)
        {
            SearchTerm searchTerm = null;
            List<string> operators = null;
            if (String.Equals((string)changedLabel.SelectedItem, Constant.UserInterface.NoFindValue, StringComparison.Ordinal) == false)
            {
                if ((existingTerm != null) && String.Equals((string)changedLabel.SelectedItem, existingTerm.Label, StringComparison.Ordinal))
                {
                    searchTerm = existingTerm;
                }
                else
                {
                    ControlRow selectedControl = this.carnassial.DataHandler.FileDatabase.Controls.Single(control => String.Equals((string)changedLabel.SelectedItem, control.Label, StringComparison.Ordinal));
                    searchTerm = selectedControl.CreateSearchTerm();
                    searchTerm.UseForSearching = true;
                }

                // for now, glob is not supported
                // Update FileFindReplace.MatchString() if this changes.            
                operators = searchTerm.GetOperators();
                operators.Remove(Constant.SearchTermOperator.Glob);
            }

            // update labels
            synchronizingLabel.SelectedItem = changedLabel.SelectedItem;

            // update operators
            changingOperator.DataContext = searchTerm;
            changingOperator.ItemsSource = operators;
            synchronizingOperator.DataContext = searchTerm;
            synchronizingOperator.ItemsSource = operators;

            // update values
            if (searchTerm != null)
            {
                WpfControl changingValue = searchTerm.CreateValueControl(this.carnassial.DataHandler.FileDatabase.AutocompletionCache);
                changingValue.TabIndex = changingOperator.TabIndex + 1;
                changingGrid.ReplaceOrAddChild(row, FindReplace.ValueColumn, changingValue);

                WpfControl synchronizingValue = searchTerm.CreateValueControl(this.carnassial.DataHandler.FileDatabase.AutocompletionCache);
                synchronizingValue.TabIndex = synchronizingOperator.TabIndex + 1;
                synchronizingGrid.ReplaceOrAddChild(row, FindReplace.ValueColumn, synchronizingValue);

                if (row == FindReplace.Term1Row)
                {
                    SearchTerm replaceTerm = this.carnassial.DataHandler.FindReplace.ReplaceTerm;
                    if ((replaceTerm == null) || (String.Equals((string)changedLabel.SelectedItem, replaceTerm.Label, StringComparison.Ordinal) == false))
                    {
                        replaceTerm = searchTerm.Clone();
                        this.carnassial.DataHandler.FindReplace.ReplaceTerm = replaceTerm;
                        this.ReplaceLabel.DataContext = replaceTerm;
                    }

                    WpfControl replaceValue = replaceTerm.CreateValueControl(this.carnassial.DataHandler.FileDatabase.AutocompletionCache);
                    replaceValue.TabIndex = FindReplace.ReplaceValueTabIndex;
                    this.ReplaceGrid.ReplaceOrAddChild(FindReplace.ReplaceRow, FindReplace.ValueColumn, replaceValue);
                }
                else if (row == FindReplace.Term2Row)
                {
                    this.FindFieldCombiningLabel.Visibility = Visibility.Visible;
                    this.ReplaceFieldCombiningLabel.Visibility = Visibility.Visible;
                }
            }
            else
            {
                changingGrid.TryRemoveChild(row, FindReplace.ValueColumn);
                synchronizingGrid.TryRemoveChild(row, FindReplace.ValueColumn);

                if (row == FindReplace.Term2Row)
                {
                    this.FindFieldCombiningLabel.Visibility = Visibility.Hidden;
                    this.ReplaceFieldCombiningLabel.Visibility = Visibility.Hidden;
                }
            }

            return searchTerm;
        }

        private void FindTerm1Label_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (String.Equals((string)this.FindTerm1Label.SelectedItem, (string)this.ReplaceTerm1Label.SelectedItem, StringComparison.Ordinal) == false)
            {
                SearchTerm term1 = this.EnsureAndBindSearchTerm(this.carnassial.DataHandler.FindReplace.FindTerm1, FindReplace.Term1Row, this.FindGrid, this.FindTerm1Label, this.FindTerm1Operator, this.ReplaceGrid, this.ReplaceTerm1Label, this.ReplaceTerm1Operator);
                this.carnassial.DataHandler.FindReplace.FindTerm1 = term1;
            }
        }

        private void FindTerm2Label_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (String.Equals((string)this.FindTerm2Label.SelectedItem, (string)this.ReplaceTerm2Label.SelectedItem, StringComparison.Ordinal) == false)
            {
                SearchTerm term2 = this.EnsureAndBindSearchTerm(this.carnassial.DataHandler.FindReplace.FindTerm2, FindReplace.Term2Row, this.FindGrid, this.FindTerm2Label, this.FindTerm2Operator, this.ReplaceGrid, this.ReplaceTerm2Label, this.ReplaceTerm2Operator);
                this.carnassial.DataHandler.FindReplace.FindTerm2 = term2;
            }
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
                    await this.carnassial.ShowFileAsync(fileIndex).ConfigureAwait(true);
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
            await this.TryFindNext().ConfigureAwait(true);
        }

        private async void Replace_Click(object sender, RoutedEventArgs e)
        {
            ImageRow currentFile = this.carnassial.DataHandler.ImageCache.Current;
            if (this.carnassial.DataHandler.FindReplace.Matches(currentFile))
            {
                Debug.Assert(this.carnassial.DataHandler.FindReplace.ReplaceTerm != null, "A replacement database value must be available.");
                this.carnassial.DataHandler.FindReplace.TryReplace(currentFile);
            }

            await this.TryFindNext().ConfigureAwait(true);
        }

        private void ReplaceAll_Click(object sender, RoutedEventArgs e)
        {
            this.carnassial.DataHandler.ReplaceAll();
        }

        private void ReplaceTerm1Label_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (String.Equals((string)this.ReplaceTerm1Label.SelectedItem, (string)this.FindTerm1Label.SelectedItem, StringComparison.Ordinal) == false)
            {
                SearchTerm term1 = this.EnsureAndBindSearchTerm(this.carnassial.DataHandler.FindReplace.FindTerm1, FindReplace.Term1Row, this.ReplaceGrid, this.ReplaceTerm1Label, this.ReplaceTerm1Operator, this.FindGrid, this.FindTerm1Label, this.FindTerm1Operator);
            }
        }

        private void ReplaceTerm2Label_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (String.Equals((string)this.ReplaceTerm2Label.SelectedItem, (string)this.FindTerm2Label.SelectedItem, StringComparison.Ordinal) == false)
            {
                SearchTerm term2 = this.EnsureAndBindSearchTerm(this.carnassial.DataHandler.FindReplace.FindTerm2, FindReplace.Term2Row, this.ReplaceGrid, this.ReplaceTerm2Label, this.ReplaceTerm2Operator, this.FindGrid, this.FindTerm2Label, this.FindTerm2Operator);
                this.carnassial.DataHandler.FindReplace.FindTerm2 = term2;
            }
        }

        private async Task<bool> TryFindNext()
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
                    await this.carnassial.ShowFileAsync(fileIndex).ConfigureAwait(true);
                    this.MessageBar.Text = null;
                }
                return true;
            }
            else
            {
                this.MessageBar.Text = "No matching file found.";
                return false;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            FindReplace.MostRecentLeft = this.Left;
            FindReplace.MostRecentTop = this.Top;
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

            Grid selectedGrid = this.FindTab.IsSelected ? this.FindGrid : this.ReplaceGrid;
            FrameworkElement findTermValue = selectedGrid.GetChild<FrameworkElement>(FindReplace.Term1Row, FindReplace.ValueColumn);
            findTermValue.Focus();
        }
    }
}
