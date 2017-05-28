using Carnassial.Controls;
using Carnassial.Data;
using Carnassial.Database;
using Carnassial.Util;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Carnassial.Dialog
{
    /// <summary>
    /// A dialog allowing a user to create a custom selection by setting conditions on data fields.
    /// </summary>
    public partial class CustomSelection : Window
    {
        private const int DefaultControlWidth = 200;
        private const double DefaultSearchCriteriaWidth = Double.NaN; // Same as xaml Width = "Auto"
        private const int ValueTextBoxHeight = 22;

        private const int LabelColumn = 0;
        private const int OperatorColumn = 1;
        private const int ValueColumn = 2;
        private const int SearchCriteriaColumn = 3;
        private const int DuplicateColumn = 4;

        private static readonly Thickness GridCellMargin = new Thickness(5, 2, 5, 2);

        private FileDatabase fileDatabase;
        private TimeZoneInfo imageSetTimeZone;

        public CustomSelection(FileDatabase database, Window owner)
        {
            this.InitializeComponent();

            this.fileDatabase = database;
            this.imageSetTimeZone = this.fileDatabase.ImageSet.GetTimeZone();
            this.Owner = owner;
            this.TermCombiningAnd.IsChecked = this.fileDatabase.CustomSelection.TermCombiningOperator == LogicalOperator.And;
            this.TermCombiningOr.IsChecked = !this.TermCombiningAnd.IsChecked;
            this.TermCombiningAnd.Checked += this.AndOrRadioButton_Checked;
            this.TermCombiningOr.Checked += this.AndOrRadioButton_Checked;

            // create a new row for each search term. 
            // Each row specifies a particular control and how it can be searched
            for (int searchTermIndex = 0; searchTermIndex < this.fileDatabase.CustomSelection.SearchTerms.Count; ++searchTermIndex)
            {
                this.AddSearchTermToGrid(this.fileDatabase.CustomSelection.SearchTerms[searchTermIndex], searchTermIndex);
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
            CheckBox controlLabel = new CheckBox();
            controlLabel.Content = "_" + searchTerm.Label;
            controlLabel.ContextMenu = new ContextMenu();
            MenuItem menuItemDuplicateSearchTerm = new MenuItem();
            menuItemDuplicateSearchTerm.Click += this.MenuItemDuplicateSearchTerm_Click;
            menuItemDuplicateSearchTerm.Header = "_Duplicate this search term";
            menuItemDuplicateSearchTerm.Tag = searchTerm;
            menuItemDuplicateSearchTerm.ToolTip = "Add a copy of this search term to the custom filter to allow more complex filtering.";
            controlLabel.ContextMenu.Items.Add(menuItemDuplicateSearchTerm);
            controlLabel.Margin = CustomSelection.GridCellMargin;
            controlLabel.VerticalAlignment = VerticalAlignment.Center;
            controlLabel.HorizontalAlignment = HorizontalAlignment.Left;
            controlLabel.IsChecked = searchTerm.UseForSearching;
            controlLabel.Checked += this.Select_CheckedOrUnchecked;
            controlLabel.Unchecked += this.Select_CheckedOrUnchecked;
            Grid.SetRow(controlLabel, gridRowIndex);
            Grid.SetColumn(controlLabel, CustomSelection.LabelColumn);
            this.SearchTerms.Children.Add(controlLabel);

            // operators allowed for search term
            string[] termOperators;
            switch (searchTerm.ControlType)
            {
                case ControlType.Counter:
                case ControlType.DateTime:
                case ControlType.FixedChoice:
                    // no globs in Counters they use only numbers
                    // no globs in Dates the date entries are constrained by the date picker
                    // no globs in Fixed Choices as choice entries are constrained by menu selection
                    termOperators = new string[]
                    {
                            Constant.SearchTermOperator.Equal,
                            Constant.SearchTermOperator.NotEqual,
                            Constant.SearchTermOperator.LessThan,
                            Constant.SearchTermOperator.GreaterThan,
                            Constant.SearchTermOperator.LessThanOrEqual,
                            Constant.SearchTermOperator.GreaterThanOrEqual
                    };
                    break;
                case ControlType.Flag:
                    termOperators = new string[]
                    {
                            Constant.SearchTermOperator.Equal,
                            Constant.SearchTermOperator.NotEqual
                    };
                    break;
                default:
                    termOperators = new string[]
                    {
                            Constant.SearchTermOperator.Equal,
                            Constant.SearchTermOperator.NotEqual,
                            Constant.SearchTermOperator.LessThan,
                            Constant.SearchTermOperator.GreaterThan,
                            Constant.SearchTermOperator.LessThanOrEqual,
                            Constant.SearchTermOperator.GreaterThanOrEqual,
                            Constant.SearchTermOperator.Glob
                    };
                    break;
            }

            // term operator combo box
            ComboBox operatorsComboBox = new ComboBox();
            operatorsComboBox.IsEnabled = searchTerm.UseForSearching;
            operatorsComboBox.ItemsSource = termOperators;
            operatorsComboBox.Margin = CustomSelection.GridCellMargin;
            operatorsComboBox.SelectedValue = searchTerm.Operator;
            operatorsComboBox.SelectionChanged += this.Operator_SelectionChanged; // Create the callback that is invoked whenever the user changes the expresison
            operatorsComboBox.Width = 60;

            Grid.SetRow(operatorsComboBox, gridRowIndex);
            Grid.SetColumn(operatorsComboBox, CustomSelection.OperatorColumn);
            this.SearchTerms.Children.Add(operatorsComboBox);

            // value column: The value used for comparison in the search
            ControlType controlType = searchTerm.ControlType;
            switch (controlType)
            {
                case ControlType.Counter:
                case ControlType.Note:
                    AutocompleteTextBox textBoxValue = new AutocompleteTextBox();
                    textBoxValue.AllowLeadingWhitespace = true;
                    textBoxValue.Autocompletions = this.fileDatabase.GetDistinctValuesInFileDataColumn(searchTerm.DataLabel);
                    textBoxValue.IsEnabled = searchTerm.UseForSearching;
                    textBoxValue.Text = searchTerm.DatabaseValue;
                    textBoxValue.Margin = CustomSelection.GridCellMargin;
                    textBoxValue.Width = CustomSelection.DefaultControlWidth;
                    textBoxValue.Height = CustomSelection.ValueTextBoxHeight;
                    textBoxValue.TextWrapping = TextWrapping.NoWrap;
                    textBoxValue.VerticalAlignment = VerticalAlignment.Center;
                    textBoxValue.VerticalContentAlignment = VerticalAlignment.Center;

                    textBoxValue.TextAutocompleted += this.SearchTermDatabaseValue_TextAutocompleted;
                    if (controlType == ControlType.Counter)
                    {
                        // accept only numbers in counter text boxes
                        textBoxValue.PreviewTextInput += this.Counter_PreviewTextInput;
                        DataObject.AddPastingHandler(textBoxValue, this.Counter_Paste);
                    }

                    Grid.SetRow(textBoxValue, gridRowIndex);
                    Grid.SetColumn(textBoxValue, CustomSelection.ValueColumn);
                    this.SearchTerms.Children.Add(textBoxValue);
                    break;
                case ControlType.DateTime:
                    DateTimeOffset dateTime = this.fileDatabase.CustomSelection.GetDateTime(gridRowIndex - 1, this.imageSetTimeZone);

                    DateTimeOffsetPicker dateValue = new DateTimeOffsetPicker();
                    dateValue.IsEnabled = searchTerm.UseForSearching;
                    dateValue.Value = dateTime;
                    dateValue.ValueChanged += this.DateTime_ValueChanged;
                    dateValue.Width = CustomSelection.DefaultControlWidth;

                    Grid.SetRow(dateValue, gridRowIndex);
                    Grid.SetColumn(dateValue, CustomSelection.ValueColumn);
                    this.SearchTerms.Children.Add(dateValue);
                    break;
                case ControlType.FixedChoice:
                    ComboBox comboBoxValue = new ComboBox();
                    comboBoxValue.IsEnabled = searchTerm.UseForSearching;
                    comboBoxValue.Width = CustomSelection.DefaultControlWidth;
                    comboBoxValue.Margin = CustomSelection.GridCellMargin;

                    // create the dropdown menu 
                    comboBoxValue.ItemsSource = searchTerm.List;
                    comboBoxValue.SelectedItem = searchTerm.DatabaseValue;
                    comboBoxValue.SelectionChanged += this.FixedChoice_SelectionChanged;
                    Grid.SetRow(comboBoxValue, gridRowIndex);
                    Grid.SetColumn(comboBoxValue, CustomSelection.ValueColumn);
                    this.SearchTerms.Children.Add(comboBoxValue);
                    break;
                case ControlType.Flag:
                    CheckBox flagCheckBox = new CheckBox();
                    flagCheckBox.Margin = CustomSelection.GridCellMargin;
                    flagCheckBox.VerticalAlignment = VerticalAlignment.Center;
                    flagCheckBox.HorizontalAlignment = HorizontalAlignment.Left;
                    flagCheckBox.IsChecked = String.Equals(searchTerm.DatabaseValue, Boolean.FalseString, StringComparison.OrdinalIgnoreCase) ? false : true;
                    flagCheckBox.IsEnabled = searchTerm.UseForSearching;
                    flagCheckBox.Checked += this.Flag_CheckedOrUnchecked;
                    flagCheckBox.Unchecked += this.Flag_CheckedOrUnchecked;

                    searchTerm.DatabaseValue = flagCheckBox.IsChecked.Value ? Boolean.TrueString : Boolean.FalseString;

                    Grid.SetRow(flagCheckBox, gridRowIndex);
                    Grid.SetColumn(flagCheckBox, CustomSelection.ValueColumn);
                    this.SearchTerms.Children.Add(flagCheckBox);
                    break;
                case ControlType.UtcOffset:
                    UtcOffsetPicker utcOffsetValue = new UtcOffsetPicker();
                    utcOffsetValue.IsEnabled = searchTerm.UseForSearching;
                    utcOffsetValue.IsTabStop = true;
                    utcOffsetValue.Value = searchTerm.GetUtcOffset();
                    utcOffsetValue.ValueChanged += this.UtcOffset_ValueChanged;
                    utcOffsetValue.Width = CustomSelection.DefaultControlWidth;

                    Grid.SetRow(utcOffsetValue, gridRowIndex);
                    Grid.SetColumn(utcOffsetValue, CustomSelection.ValueColumn);
                    this.SearchTerms.Children.Add(utcOffsetValue);
                    break;
                default:
                    throw new NotSupportedException(String.Format("Unhandled control type '{0}'.", controlType));
            }

            // search criteria
            // Initially as an empty textblock. Indicates the constructed query expression for this row
            TextBlock searchCriteria = new TextBlock();
            searchCriteria.HorizontalAlignment = HorizontalAlignment.Left;
            searchCriteria.Margin = CustomSelection.GridCellMargin;
            searchCriteria.VerticalAlignment = VerticalAlignment.Center;
            searchCriteria.Width = CustomSelection.DefaultSearchCriteriaWidth;

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

        // accept only numbers in counter textboxes
        private void Counter_Paste(object sender, DataObjectPastingEventArgs args)
        {
            bool isText = args.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, true);
            if (!isText)
            {
                args.CancelCommand();
            }

            string text = args.SourceDataObject.GetData(DataFormats.UnicodeText) as string;
            if (Utilities.IsDigits(text) == false)
            {
                args.CancelCommand();
            }
        }

        private void Counter_PreviewTextInput(object sender, TextCompositionEventArgs args)
        {
            // counters accept only numbers
            args.Handled = !Utilities.IsDigits(args.Text);
        }

        private void DateTime_ValueChanged(DateTimeOffsetPicker datePicker, DateTimeOffset newDateTime)
        {
            int row = Grid.GetRow(datePicker);
            this.fileDatabase.CustomSelection.SetDateTime(row - 1, datePicker.Value, this.imageSetTimeZone);
            this.UpdateSearchCriteriaFeedback();
        }

        private void Duplicate_Click(object sender, RoutedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void FixedChoice_SelectionChanged(object sender, SelectionChangedEventArgs args)
        {
            ComboBox comboBox = sender as ComboBox;
            int row = Grid.GetRow(comboBox);
            this.fileDatabase.CustomSelection.SearchTerms[row - 1].DatabaseValue = comboBox.SelectedValue.ToString();
            this.UpdateSearchCriteriaFeedback();
        }

        private void Flag_CheckedOrUnchecked(object sender, RoutedEventArgs e)
        {
            CheckBox checkBox = sender as CheckBox;
            int row = Grid.GetRow(checkBox);
            this.fileDatabase.CustomSelection.SearchTerms[row - 1].DatabaseValue = checkBox.IsChecked.ToString().ToLower();
            this.UpdateSearchCriteriaFeedback();
        }

        // get the corresponding grid element from a given column, row
        private TElement GetGridElement<TElement>(int column, int row) where TElement : UIElement
        {
            return (TElement)this.SearchTerms.Children.Cast<UIElement>().First(control => Grid.GetRow(control) == row && Grid.GetColumn(control) == column);
        }

        private void MenuItemDuplicateSearchTerm_Click(object sender, RoutedEventArgs e)
        {
            // duplicate search term
            SearchTerm searchTerm = (SearchTerm)((Control)sender).Tag;
            int insertionIndex = this.fileDatabase.CustomSelection.SearchTerms.IndexOf(searchTerm) + 1;
            SearchTerm termClone = new SearchTerm(searchTerm);
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
                    UIElement gridElement = this.GetGridElement<UIElement>(column, searchTermIndex + 1); // +1 for header row in grid
                    this.SearchTerms.Children.Remove(gridElement);
                }
                this.AddSearchTermToGrid(this.fileDatabase.CustomSelection.SearchTerms[searchTermIndex], searchTermIndex);
            }
            int lastSearchTermIndex = this.fileDatabase.CustomSelection.SearchTerms.Count - 1;
            this.AddSearchTermToGrid(this.fileDatabase.CustomSelection.SearchTerms[lastSearchTermIndex], lastSearchTermIndex);
        }

        private void OkButton_Click(object sender, RoutedEventArgs args)
        {
            this.fileDatabase.SelectFiles(FileSelection.Custom);
            this.DialogResult = true;
        }

        // Select: When the use checks or unchecks the checkbox for a row
        // - activate or deactivate the search criteria for that row
        // - update the searchterms to reflect the new status 
        // - update the UI to activate or deactivate (or show or hide) its various search terms
        private void Select_CheckedOrUnchecked(object sender, RoutedEventArgs args)
        {
            CheckBox select = sender as CheckBox;
            int row = Grid.GetRow(select);  // And you have the row number...
            bool state = select.IsChecked.Value;

            SearchTerm searchTerm = this.fileDatabase.CustomSelection.SearchTerms[row - 1];
            searchTerm.UseForSearching = select.IsChecked.Value;

            CheckBox label = this.GetGridElement<CheckBox>(CustomSelection.LabelColumn, row);
            ComboBox expression = this.GetGridElement<ComboBox>(CustomSelection.OperatorColumn, row);
            UIElement value = this.GetGridElement<UIElement>(CustomSelection.ValueColumn, row);

            label.FontWeight = select.IsChecked.Value ? FontWeights.Bold : FontWeights.Normal;
            expression.IsEnabled = select.IsChecked.Value;
            value.IsEnabled = select.IsChecked.Value;

            this.UpdateSearchCriteriaFeedback();
        }

        // Operator: The user has selected a new expression
        // - set its corresponding search term in the searchList data structure
        // - update the UI to show the search criteria 
        private void Operator_SelectionChanged(object sender, SelectionChangedEventArgs args)
        {
            ComboBox comboBox = sender as ComboBox;
            int row = Grid.GetRow(comboBox);
            this.fileDatabase.CustomSelection.SearchTerms[row - 1].Operator = comboBox.SelectedValue.ToString(); // Set the corresponding expression to the current selection
            this.UpdateSearchCriteriaFeedback();
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            // disable all search terms
            for (int row = 1; row <= this.fileDatabase.CustomSelection.SearchTerms.Count; row++)
            {
                CheckBox label = this.GetGridElement<CheckBox>(CustomSelection.LabelColumn, row);
                label.IsChecked = false;
            }
        }

        // Value: The user has selected a new value
        // - set its corresponding search term in the searchList data structure
        // - update the UI to show the search criteria 
        private void SearchTermDatabaseValue_TextAutocompleted(object sender, TextChangedEventArgs args)
        {
            TextBox textBox = sender as TextBox;
            int row = Grid.GetRow(textBox);
            this.fileDatabase.CustomSelection.SearchTerms[row - 1].DatabaseValue = textBox.Text;
            this.UpdateSearchCriteriaFeedback();
        }

        private void UtcOffset_ValueChanged(TimeSpanPicker utcOffsetPicker, TimeSpan newTimeSpan)
        {
            int row = Grid.GetRow(utcOffsetPicker);
            this.fileDatabase.CustomSelection.SearchTerms[row - 1].SetDatabaseValue(utcOffsetPicker.Value);
            this.UpdateSearchCriteriaFeedback();
        }

        // Updates the search criteria shown across all rows to reflect the contents of the search list,
        // which also show or hides the search term feedback for that row.
        private void UpdateSearchCriteriaFeedback()
        {
            // loop runs backwards for final term combining operator check
            bool lastExpression = true;
            for (int index = this.fileDatabase.CustomSelection.SearchTerms.Count - 1; index >= 0; index--)
            {
                int row = index + 1; // row 0 in the data grid is the header
                SearchTerm searchTerm = this.fileDatabase.CustomSelection.SearchTerms[index];
                TextBlock searchCriteria = this.GetGridElement<TextBlock>(CustomSelection.SearchCriteriaColumn, row);

                if (searchTerm.UseForSearching == false)
                {
                    // The search term is not used for searching, so clear the feedback field
                    searchCriteria.Text = String.Empty;
                    continue;
                }

                // construct the search term 
                string searchCriteriaText = searchTerm.DataLabel + " " + searchTerm.Operator + " ";
                string value = searchTerm.DatabaseValue.Trim();
                if (value.Length == 0)
                {
                    value = "\"\"";  // an empty string, display it as ""
                }
                searchCriteriaText += value;

                // include term combining operator
                if (!lastExpression)
                {
                    searchCriteriaText += " " + this.fileDatabase.CustomSelection.TermCombiningOperator.ToString();
                }

                searchCriteria.Text = searchCriteriaText;
                lastExpression = false;
            }

            int count = this.fileDatabase.GetFileCount(FileSelection.Custom);
            this.OkButton.IsEnabled = count > 0 ? true : false;
            this.QueryMatches.Text = count > 0 ? count.ToString() : "0";

            this.Reset.IsEnabled = lastExpression == false;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Utilities.SetDefaultDialogPosition(this);
            Utilities.TryFitWindowInWorkingArea(this);
        }
    }
}
