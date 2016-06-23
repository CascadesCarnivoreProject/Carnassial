using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Timelapse.Database;

namespace Timelapse
{
    /// <summary>
    /// Interaction logic for DialogCustomViewFilter.xaml
    /// </summary>
    public partial class DialogCustomViewFilter : Window
    {
        private ImageDatabase database;
        private CustomFilter customFilter;

        #region Constructors and Loading
        public DialogCustomViewFilter(ImageDatabase database, CustomFilter customFilter)
        {
            this.database = database;
            this.customFilter = customFilter;
            this.InitializeComponent();
        }

        // When the window is loaded, add all the controls to it dynamically
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // UI Housekeeping
            // Make sure the title bar of the dialog box is on the screen. For small screens it may default to being off the screen
            if (this.Left < 10 || this.Top < 10)
            {
                this.Left = this.Owner.Left + (this.Owner.Width - this.ActualWidth) / 2; // Center it horizontally
                this.Top = this.Owner.Top + 20; // Offset it from the windows'top by 20 pixels downwards
            }

            // And now the real work
            if (this.customFilter.LogicalOperator == CustomFilterOperator.And)
            {
                rbAnd.IsChecked = true;
                rbOr.IsChecked = false;
            }
            else
            {
                rbAnd.IsChecked = false;
                rbOr.IsChecked = true;
            }
            rbAnd.Checked += this.AndOrRadioButton_Checked;
            rbOr.Checked += this.AndOrRadioButton_Checked;

            int gridRowIndex = 0;
            foreach (int key in this.customFilter.SearchTermList.Keys)
            {
                SearchTerm searchTerm = this.customFilter.SearchTermList[key];
                    
                // Create a new row, where each row specifies a particular control and how it can be searched
                // start at 1 as there is already a header row
                ++gridRowIndex;
                RowDefinition gridRow = new RowDefinition();
                gridRow.Height = GridLength.Auto;
                grid.RowDefinitions.Add(gridRow);

                // USE Column: A checkbox to indicate whether the current search row should be used as part of the search
                Thickness thickness = new Thickness(5, 2, 5, 2);
                CheckBox useCurrentRow = new CheckBox();
                useCurrentRow.Margin = thickness;
                useCurrentRow.VerticalAlignment = VerticalAlignment.Center;
                useCurrentRow.HorizontalAlignment = HorizontalAlignment.Center;
                useCurrentRow.IsChecked = searchTerm.UseForSearching;
                useCurrentRow.Checked += this.ComboBox_CheckHandler;
                useCurrentRow.Unchecked += this.ComboBox_CheckHandler;
                Grid.SetRow(useCurrentRow, gridRowIndex);
                Grid.SetColumn(useCurrentRow, 0);
                grid.Children.Add(useCurrentRow);

                // LABEL column: The label associated with the control (Note: not the data label)
                TextBlock controlLabel = new TextBlock();
                controlLabel.Margin = thickness;
                controlLabel.Text = searchTerm.Label;
                controlLabel.Margin = new Thickness(5);
                Grid.SetRow(controlLabel, gridRowIndex);
                Grid.SetColumn(controlLabel, 1);
                grid.Children.Add(controlLabel);

                // EXPRESSION : creates a combo box where its contents of various expression values depends on the type
                ComboBox comboboxExpressions = new ComboBox();
                comboboxExpressions.SelectedValue = searchTerm.Expression; // Default: equals sign
                comboboxExpressions.Width = 60;
                comboboxExpressions.Margin = thickness;
                comboboxExpressions.IsEnabled = searchTerm.UseForSearching;

                // The expressions allowed to compare numbers vs. plain text
                string controlType = searchTerm.Type;
                string[] expressions;
                if (controlType == Constants.Control.Counter)
                {
                    // No globs in Counters: since that text field only allows numbers, we can't enter the special characters Glob required
                    expressions = new string[]
                    {
                        Constants.Filter.Equal,
                        Constants.Filter.NotEqual,
                        Constants.Filter.LessThan,
                        Constants.Filter.GreaterThan,
                        Constants.Filter.LessThanOrEqual,
                        Constants.Filter.GreaterThanOrEqual
                    };
                }
                else if (controlType == Constants.Control.Flag)
                {
                    // Only equals and not equals in Flags, as other options don't make sense for booleans
                    expressions = new string[]
                    {
                        Constants.Filter.Equal,
                        Constants.Filter.NotEqual
                    };
                }
                else
                {
                    expressions = new string[]
                    {
                        Constants.Filter.Equal,
                        Constants.Filter.NotEqual,
                        Constants.Filter.LessThan,
                        Constants.Filter.GreaterThan,
                        Constants.Filter.LessThanOrEqual,
                        Constants.Filter.GreaterThanOrEqual,
                        Constants.Filter.Glob
                    };
                }

                comboboxExpressions.ItemsSource = expressions;
                comboboxExpressions.SelectionChanged += this.CbExpressions_SelectionChanged; // Create the callback that is invoked whenever the user changes the expresison

                Grid.SetRow(comboboxExpressions, gridRowIndex);
                Grid.SetColumn(comboboxExpressions, 2);
                grid.Children.Add(comboboxExpressions);

                // Value column: The value used for comparison in the search
                // Notes and Counters both uses a text field, so they can be constructed as a textbox
                // However, Counters textboxes are modified to only allow integer input (both direct typing or pasting are checked)
                if (controlType == Constants.Control.Note || controlType == Constants.Control.Counter || controlType == Constants.DatabaseColumn.RelativePath)
                {
                    TextBox tboxValue = new TextBox();
                    tboxValue.Text = searchTerm.Value;
                    tboxValue.Width = 150;
                    tboxValue.Height = 22;
                    tboxValue.TextWrapping = TextWrapping.NoWrap;
                    tboxValue.VerticalAlignment = VerticalAlignment.Center;
                    tboxValue.VerticalContentAlignment = VerticalAlignment.Center;
                    tboxValue.IsEnabled = searchTerm.UseForSearching;
                    tboxValue.Margin = thickness;

                    // The following is specific only to Counters
                    if (controlType == Constants.Control.Counter)
                    {
                        tboxValue.PreviewTextInput += this.TxtboxCounterValue_PreviewTextInput;
                        DataObject.AddPastingHandler(tboxValue, this.CounterValueText_Paste);
                    }
                    tboxValue.TextChanged += this.Txtbox_TextChanged;

                    Grid.SetRow(tboxValue, gridRowIndex);
                    Grid.SetColumn(tboxValue, 3);
                    grid.Children.Add(tboxValue);
                }
                else if (controlType == Constants.Control.FixedChoice || controlType == Constants.DatabaseColumn.ImageQuality)
                {
                    // FixedChoice and ImageQuality both present combo boxes, so they can be constructed the same way
                    ComboBox comboBoxValue = new ComboBox();

                    comboBoxValue.Width = 150;
                    comboBoxValue.Margin = thickness;
                    comboBoxValue.IsEnabled = searchTerm.UseForSearching;

                    // Create the dropdown menu 
                    string list = searchTerm.List;
                    list += " | "; // Add an empty field so it can be reset to empty by the user
                    string[] choices = list.Split(new char[] { '|' });
                    comboBoxValue.ItemsSource = choices;
                    comboBoxValue.SelectedItem = searchTerm.Value;
                    comboBoxValue.SelectionChanged += this.ComboBoxValue_SelectionChanged;
                    Grid.SetRow(comboBoxValue, gridRowIndex);
                    Grid.SetColumn(comboBoxValue, 3);
                    grid.Children.Add(comboBoxValue);
                }
                else if (controlType == Constants.Control.Flag)
                {
                    // Flags present checkboxes
                    CheckBox flagCheckBox = new CheckBox();
                    flagCheckBox.Margin = thickness;
                    flagCheckBox.IsEnabled = searchTerm.UseForSearching;
                    flagCheckBox.VerticalAlignment = VerticalAlignment.Center;
                    flagCheckBox.HorizontalAlignment = HorizontalAlignment.Left;

                    flagCheckBox.IsChecked = (searchTerm.Value.ToLower() == Constants.Boolean.False) ? false : true;
                    flagCheckBox.Checked += this.FlagBox_Check;
                    flagCheckBox.Unchecked += this.FlagBox_Check;
                    Grid.SetRow(flagCheckBox, gridRowIndex);
                    Grid.SetColumn(flagCheckBox, 3);
                    grid.Children.Add(flagCheckBox);
                }

                // Search Criteria Column: initially as an empty textblock
                TextBlock searchCriteria = new TextBlock();
                searchCriteria.Width = 150;
                searchCriteria.Margin = thickness;
                searchCriteria.IsEnabled = true;
                searchCriteria.VerticalAlignment = VerticalAlignment.Center;

                Grid.SetRow(searchCriteria, gridRowIndex);
                Grid.SetColumn(searchCriteria, 4);
                grid.Children.Add(searchCriteria);
            }
            this.UpdateSearchCriteriaFeedback();
        }
        #endregion 

        #region Search Selection Handlers 
        // Value(Counter) Helper function: checks if the text contains only numbers
        private static bool IsNumbersOnly(string text)
        {
            Regex regex = new Regex("[^0-9.-]+"); // regex that matches allowed text
            return regex.IsMatch(text);
        }

        // Select: When the use checks or unchecks the checkbox for a row
        // - activate or deactivate the search criteria for that row
        // - update the searchterms to reflect the new status 
        // - update the UI to activate or deactivate (or show or hide) its various search terms
        private void ComboBox_CheckHandler(object sender, RoutedEventArgs e)
        {
            CheckBox cb = sender as CheckBox;
            int row = Grid.GetRow(cb);  // And you have the row number...
            bool state = cb.IsChecked.Value;

            SearchTerm searchterms = this.customFilter.SearchTermList.Values.ElementAt(row - 1);
            searchterms.UseForSearching = cb.IsChecked.Value;

            TextBlock label = (TextBlock)grid.Children.Cast<UIElement>().First(ex => Grid.GetRow(ex) == row && Grid.GetColumn(ex) == 1);
            ComboBox expressionBox = (ComboBox)grid.Children.Cast<UIElement>().First(ex => Grid.GetRow(ex) == row && Grid.GetColumn(ex) == 2);

            UIElement uieValue = (UIElement)grid.Children.Cast<UIElement>().First(ex => Grid.GetRow(ex) == row && Grid.GetColumn(ex) == 3);

            label.FontWeight = cb.IsChecked.Value ? FontWeights.Bold : FontWeights.Normal;
            expressionBox.IsEnabled = cb.IsChecked.Value;
            uieValue.IsEnabled = cb.IsChecked.Value;

            this.UpdateSearchCriteriaFeedback();
        }

        // Expression: The user has selected a new expression
        // - set its corresponding search term in the searchList data structure
        // - update the UI to show the search criteria 
        private void CbExpressions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox cb = sender as ComboBox;
            int row = Grid.GetRow(cb);  // Get the row number...
            this.customFilter.SearchTermList.Values.ElementAt(row - 1).Expression = cb.SelectedValue.ToString(); // Set the corresponding expression to the current selection
            this.UpdateSearchCriteriaFeedback();
        }

        // Value (Counters and Notes): The user has selected a new value
        // - set its corresponding search term in the searchList data structure
        // - update the UI to show the search criteria 
        private void Txtbox_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox tb = sender as TextBox;
            int row = Grid.GetRow(tb);  // Get the row number...
            this.customFilter.SearchTermList.Values.ElementAt(row - 1).Value = tb.Text;
            this.UpdateSearchCriteriaFeedback();
        }

        // Value (Counter) Helper function: textbox accept only typed numbers 
        private void TxtboxCounterValue_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = IsNumbersOnly(e.Text);
        }

        // Value (Counter) Helper function:  textbox accept only pasted numbers 
        private void CounterValueText_Paste(object sender, DataObjectPastingEventArgs e)
        {
            bool isText = e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, true);
            if (!isText)
            {
                e.CancelCommand();
            }

            string text = e.SourceDataObject.GetData(DataFormats.UnicodeText) as string;
            if (IsNumbersOnly(text))
            {
                e.CancelCommand();
            }
        }

        // Value (for FixedChoices): The user has selected a new value 
        // - set its corresponding search term in the searchList data structure
        // - update the UI to show the search criteria 
        private void ComboBoxValue_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox cb = sender as ComboBox;
            int row = Grid.GetRow(cb);  // Get the row number...
            this.customFilter.SearchTermList.Values.ElementAt(row - 1).Value = cb.SelectedValue.ToString(); // Set the corresponding value to the current selection
            this.UpdateSearchCriteriaFeedback();
        }

        // Value (for Flags): The user has checked or unchecked a new value 
        // - set its corresponding search term in the searchList data structure
        // - update the UI to show the search criteria 
        private void FlagBox_Check(object sender, RoutedEventArgs e)
        {
            CheckBox cb = sender as CheckBox;
            int row = Grid.GetRow(cb);  // Get the row number...
            this.customFilter.SearchTermList.Values.ElementAt(row - 1).Value = cb.IsChecked.ToString().ToLower(); // Set the corresponding value to the current selection
            this.UpdateSearchCriteriaFeedback();
        }
        #endregion

        #region Update the UI
        // Updates the search criteria shown across all rows to reflect the contents of the search list,
        // which also show or hides the search term feedback for that row.
        private void UpdateSearchCriteriaFeedback()
        {
            bool lastExpression = true;
            bool searchTermsExist = false;

            // We go backwards, as we don't want to print the AND or OR on the last expression
            for (int index = this.customFilter.SearchTermList.Count - 1; index >= 0; index--)
            {
                int row = index + 1; // we offset the row by 1 as row 0 is the header
                SearchTerm searchTerm = this.customFilter.SearchTermList.Values.ElementAt(index);
                TextBlock tb_search_feedback = (TextBlock)grid.Children.Cast<UIElement>().First(ex => Grid.GetRow(ex) == row && Grid.GetColumn(ex) == 4);

                if (searchTerm.UseForSearching)
                {
                    // We are only interested in showing feedback for rows where the search expression is active
                    searchTermsExist = true;
                    // Construct the search term 
                    string stringToDisplay = searchTerm.DataLabel + " " + searchTerm.Expression + " "; // So far, we have "Data Label = 

                    string value = searchTerm.Value.Trim();    // the Value, but if its 
                    if (value.Length == 0)
                    {
                        value = "\"\"";  // an empty string, display it as ""
                    }
                    stringToDisplay += value;

                    // If its not the last expression and if there are multiple queries (i.e., search terms) then show the And or Or at its end.
                    bool queryHasMultipleSelectedSearchTerms = this.customFilter.QueryHasMultipleSelectedSearchTerms();
                    if (!lastExpression && queryHasMultipleSelectedSearchTerms)
                    {
                        if (queryHasMultipleSelectedSearchTerms)
                        {
                            stringToDisplay += " " + this.customFilter.LogicalOperator.ToString();
                        }
                    }

                    tb_search_feedback.Text = stringToDisplay;
                    lastExpression = false;
                }
                else
                {
                    // The search term is not used for searching, so clear the feedback field
                    tb_search_feedback.Text = String.Empty;
                }
            }

            int count = this.customFilter.GetImageCount();
            this.OkButton.IsEnabled = count > 0 ? true : false;
            this.btnShowAll.IsEnabled = searchTermsExist;
            textBlockQueryMatches.Text = count.ToString();
        }
        #endregion

        #region Button callbacks
        // Radio buttons for determing if we use And or Or
        private void AndOrRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            RadioButton rb = sender as RadioButton;
            this.customFilter.LogicalOperator = (rb == this.rbAnd) ? CustomFilterOperator.And : CustomFilterOperator.Or;
            this.UpdateSearchCriteriaFeedback();
        }

        // Apply the filter if the Ok button is clicked
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.database.SelectDataTableImages(this.customFilter.GetImagesWhere());
            this.DialogResult = true;
        }

        // Cancel - exit the dialog without doing anythikng.
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
        #endregion

        // When this button is pressed, all the search terms checkboxes are cleared, which is equivalent to showing all images
        private void ShowAllButton_Click(object sender, RoutedEventArgs e)
        {
            // We go backwards, as we don't want to print the AND or OR on the last expression
            for (int row = 1; row <= this.customFilter.SearchTermList.Count; row++)
            {
                CheckBox chbox = (CheckBox)grid.Children.Cast<UIElement>().First(ex => Grid.GetRow(ex) == row && Grid.GetColumn(ex) == 0);
                chbox.IsChecked = false;
            }
        }
    }
}
