using Carnassial.Data;
using Carnassial.Database;
using Carnassial.Util;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Carnassial.Dialog
{
    /// <summary>
    /// A dialog allowing a user to create a custom selection by setting conditions on data fields.
    /// </summary>
    public partial class CustomSelection : FindDialog
    {
        private const int LabelColumn = 0;
        private const int OperatorColumn = 1;
        private const int ValueColumn = 2;
        private const int SearchCriteriaColumn = 3;
        private const int DuplicateColumn = 4;

        private FileDatabase fileDatabase;
        private TimeZoneInfo imageSetTimeZone;

        public CustomSelection(FileDatabase database, Window owner)
        {
            this.InitializeComponent();

            this.fileDatabase = database;
            this.imageSetTimeZone = this.fileDatabase.ImageSet.GetTimeZoneInfo();
            this.Owner = owner;
            this.TermCombiningAnd.IsChecked = this.fileDatabase.CustomSelection.TermCombiningOperator == LogicalOperator.And;
            this.TermCombiningOr.IsChecked = !this.TermCombiningAnd.IsChecked;
            this.TermCombiningAnd.Checked += this.AndOrRadioButton_Checked;
            this.TermCombiningOr.Checked += this.AndOrRadioButton_Checked;

            // create a new row for each search term. 
            // Each row specifies a particular control and how it can be searched
            for (int index = 0; index < this.fileDatabase.CustomSelection.SearchTerms.Count; ++index)
            {
                SearchTerm searchTerm = this.fileDatabase.CustomSelection.SearchTerms[index];
                this.AddSearchTermToGrid(searchTerm, index);

                // creating UI controls for the search term triggers data binding calls which fire property change events
                // Therefore, for efficiency, hook PropertyChanged after the term's UI objects are instantiated and call
                // UpdateSearchCriteriaFeedback() once after terms are populated.  This also simplifies the feedback logic as
                // it doesn't have to handle partially populated term UI.
                searchTerm.PropertyChanged += this.UpdateSearchCriteriaFeedback;
            }
            this.UpdateSearchCriteriaFeedback();
        }

        private void AddSearchTermToGrid(SearchTerm searchTerm, int searchTermIndex)
        {
            // grid has an extra header row
            int gridRowIndex = searchTermIndex + 1;
            if (this.SearchTerms.RowDefinitions.Count <= gridRowIndex)
            {
                this.SearchTerms.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
            }

            // label
            // A checkbox to indicate whether the current search row should be used as part of the search
            CheckBox controlLabel = new CheckBox()
            {
                Content = "_" + searchTerm.Label,
                ContextMenu = new ContextMenu(),
                Margin = Constant.UserInterface.FindCellMargin,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                IsChecked = searchTerm.UseForSearching
            };
            controlLabel.Checked += this.UseForSearching_CheckedOrUnchecked;
            controlLabel.Unchecked += this.UseForSearching_CheckedOrUnchecked;

            MenuItem menuItemDuplicateSearchTerm = new MenuItem();
            menuItemDuplicateSearchTerm.Click += this.MenuItemDuplicateSearchTerm_Click;
            menuItemDuplicateSearchTerm.Header = "_Duplicate this search term";
            menuItemDuplicateSearchTerm.Tag = searchTerm;
            menuItemDuplicateSearchTerm.ToolTip = "Add a copy of this search term to the custom filter to allow more complex filtering.";
            controlLabel.ContextMenu.Items.Add(menuItemDuplicateSearchTerm);

            Grid.SetRow(controlLabel, gridRowIndex);
            Grid.SetColumn(controlLabel, CustomSelection.LabelColumn);
            this.SearchTerms.Children.Add(controlLabel);

            // term operator combo box
            ComboBox operatorsComboBox = new ComboBox()
            {
                DataContext = searchTerm,
                IsEnabled = searchTerm.UseForSearching,
                ItemsSource = this.GetOperators(searchTerm),
                Margin = Constant.UserInterface.FindCellMargin,
                SelectedValue = searchTerm.Operator,
                Width = Constant.UserInterface.FindOperatorWidth
            };
            BindingOperations.SetBinding(operatorsComboBox, ComboBox.SelectedItemProperty, new Binding(nameof(searchTerm.Operator)));

            Grid.SetRow(operatorsComboBox, gridRowIndex);
            Grid.SetColumn(operatorsComboBox, CustomSelection.OperatorColumn);
            this.SearchTerms.Children.Add(operatorsComboBox);

            // value column: The value used for comparison in the search
            UIElement valueControl = this.CreateValueControl(searchTerm, this.fileDatabase);
            Grid.SetRow(valueControl, gridRowIndex);
            Grid.SetColumn(valueControl, CustomSelection.ValueColumn);
            this.SearchTerms.Children.Add(valueControl);

            // search criteria
            // Indicates the query expression for this term.
            TextBlock searchCriteria = new TextBlock()
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = Constant.UserInterface.FindCellMargin,
                VerticalAlignment = VerticalAlignment.Center,
            };

            Grid.SetRow(searchCriteria, gridRowIndex);
            Grid.SetColumn(searchCriteria, CustomSelection.SearchCriteriaColumn);
            this.SearchTerms.Children.Add(searchCriteria);
        }

        // radio buttons for search term combining operator
        private void AndOrRadioButton_Checked(object sender, RoutedEventArgs args)
        {
            RadioButton radioButton = sender as RadioButton;
            this.fileDatabase.CustomSelection.TermCombiningOperator = (radioButton == this.TermCombiningAnd) ? LogicalOperator.And : LogicalOperator.Or;
            this.UpdateSearchCriteriaFeedback();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void MenuItemDuplicateSearchTerm_Click(object sender, RoutedEventArgs e)
        {
            // duplicate search term
            SearchTerm searchTerm = (SearchTerm)((FrameworkElement)sender).Tag;
            int insertionIndex = this.fileDatabase.CustomSelection.SearchTerms.IndexOf(searchTerm) + 1;
            SearchTerm termClone = searchTerm.Clone();
            this.fileDatabase.CustomSelection.SearchTerms.Insert(insertionIndex, termClone);

            // work around WPF by rebuilding FrameworkElements in search term grid
            // Grid doesn't respond to changes in Grid.SetRow() even when UpdateLayout() is called explicitly, so simply moving existing content down one
            // row in the grid results in the new search term getting in a Z fight with the content already present in the row.  Additionally, WPF fails to
            // correctly manage ownership of the adorners on date time pickers, so simply detaching and reattaching the existing UI objects results in
            // ownership exceptions.
            for (int searchTermIndex = insertionIndex; searchTermIndex < this.fileDatabase.CustomSelection.SearchTerms.Count - 1; ++searchTermIndex)
            {
                for (int column = 0; column < this.SearchTerms.ColumnDefinitions.Count; ++column)
                {
                    this.SearchTerms.TryRemoveChild(searchTermIndex + 1, column); // +1 for header row in grid
                }
                this.AddSearchTermToGrid(this.fileDatabase.CustomSelection.SearchTerms[searchTermIndex], searchTermIndex);
            }
            int lastSearchTermIndex = this.fileDatabase.CustomSelection.SearchTerms.Count - 1;
            this.AddSearchTermToGrid(this.fileDatabase.CustomSelection.SearchTerms[lastSearchTermIndex], lastSearchTermIndex);
        }

        private void OkButton_Click(object sender, RoutedEventArgs args)
        {
            this.DialogResult = true;
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            // disable all search terms
            for (int row = 1; row <= this.fileDatabase.CustomSelection.SearchTerms.Count; row++)
            {
                CheckBox label = this.SearchTerms.GetChild<CheckBox>(row, CustomSelection.LabelColumn);
                label.IsChecked = false;
            }
        }

        // Updates the search criteria shown across all rows to reflect the contents of the search list,
        // which also shows or hides the search term feedback for that row.
        private void UpdateSearchCriteriaFeedback()
        {
            // loop runs backwards for final term combining operator check
            bool lastExpression = true;
            for (int index = this.fileDatabase.CustomSelection.SearchTerms.Count - 1; index >= 0; index--)
            {
                int row = index + 1; // row 0 in the data grid is the header
                SearchTerm searchTerm = this.fileDatabase.CustomSelection.SearchTerms[index];
                TextBlock searchCriteria = this.SearchTerms.GetChild<TextBlock>(row, CustomSelection.SearchCriteriaColumn);

                if (searchTerm.UseForSearching == false)
                {
                    // search term is not used for searching, so clear the feedback field
                    searchCriteria.Text = String.Empty;
                    continue;
                }

                // display search term's contribution to the query
                string searchCriteriaText = searchTerm.ToString();
                if (!lastExpression)
                {
                    searchCriteriaText += " " + this.fileDatabase.CustomSelection.TermCombiningOperator.ToString();
                }

                searchCriteria.Text = searchCriteriaText;
                lastExpression = false;
            }

            int count = this.fileDatabase.GetFileCount(FileSelection.Custom);
            this.OkButton.IsEnabled = count > 0 ? true : false;
            this.QueryMatches.Text = count > 0 ? count.ToString(CultureInfo.CurrentCulture) : "0";

            this.Reset.IsEnabled = lastExpression == false;
        }

        private void UpdateSearchCriteriaFeedback(object sender, PropertyChangedEventArgs e)
        {
            this.UpdateSearchCriteriaFeedback();
        }

        // When the user checks or unchecks the checkbox for a row
        // - activate or deactivate the search criteria for that row
        // - update the searchterms to reflect the new status 
        // - update the UI to activate or deactivate (or show or hide) its various search terms
        private void UseForSearching_CheckedOrUnchecked(object sender, RoutedEventArgs args)
        {
            CheckBox select = sender as CheckBox;
            int row = Grid.GetRow(select);

            SearchTerm searchTerm = this.fileDatabase.CustomSelection.SearchTerms[row - 1];
            searchTerm.UseForSearching = select.IsChecked.Value;

            CheckBox label = this.SearchTerms.GetChild<CheckBox>(row, CustomSelection.LabelColumn);
            ComboBox expression = this.SearchTerms.GetChild<ComboBox>(row, CustomSelection.OperatorColumn);
            UIElement value = this.SearchTerms.GetChild<UIElement>(row, CustomSelection.ValueColumn);

            label.FontWeight = select.IsChecked.Value ? FontWeights.Bold : FontWeights.Normal;
            expression.IsEnabled = select.IsChecked.Value;
            value.IsEnabled = select.IsChecked.Value;

            this.UpdateSearchCriteriaFeedback();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            // search terms are durable so unhook their property changed events
            for (int index = 0; index < this.fileDatabase.CustomSelection.SearchTerms.Count; ++index)
            {
                SearchTerm searchTerm = this.fileDatabase.CustomSelection.SearchTerms[index];
                searchTerm.PropertyChanged -= this.UpdateSearchCriteriaFeedback;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CommonUserInterface.SetDefaultDialogPosition(this);
            CommonUserInterface.TryFitWindowInWorkingArea(this);
        }
    }
}
