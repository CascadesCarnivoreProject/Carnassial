using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Timelapse
{
    /// <summary>
    /// Interaction logic for DlgCustomViewFilter.xaml
    /// </summary>
    public partial class DlgCustomViewFilter : Window
    {
        #region Variables and Constants
        static bool HideExplanation = false; // Whether to show or hide the explanation (state remembered across all these dialog boxes

        private DBData dbData;
        private CustomFilter customFilter;

        const string CH_EQUALS = "\u003D";
        const string CH_NOT_EQUALS = "\u2260";
        const string CH_LESS_THAN = "\u003C";
        const string CH_GREATER_THAN = "\u003E";
        const string CH_LESS_THAN_EQUALS = "\u2264";
        const string CH_GREATER_THAN_EQUALS = "\u2267";
        const string CH_GLOB = " GLOB ";
        #endregion

        #region Constructors and Loading
        public DlgCustomViewFilter(DBData db_data, CustomFilter custom_filter)
        {
            this.dbData = db_data;
            this.customFilter = custom_filter;
            InitializeComponent();
        }


        // When the window is loaded, add all the controls to it dynamically
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // UI Housekeeping
            // Make sure the title bar of the dialog box is on the screen. For small screens it may default to being off the screen
            if (this.Left < 10 || this.Top < 10)
            {
                this.Left = this.Owner.Left + (this.Owner.Width - this.ActualWidth) / 2; //Center it horizontally
                this.Top = this.Owner.Top + 20; // Offset it from the windows'top by 20 pixels downwards
            }

            // Show or hide the explanation depending on the saved state of the static variable
            btnHideText.IsChecked = DlgCustomViewFilter.HideExplanation;

            // And now the real work
            // The expressions allowed to compare numbers vs. plain text
            string[] number_expressions = { CH_EQUALS, CH_NOT_EQUALS, CH_LESS_THAN, CH_GREATER_THAN, CH_LESS_THAN_EQUALS, CH_GREATER_THAN_EQUALS, CH_GLOB };
            //string[] text_expressions = { CH_EQUALS, CH_NOT_EQUALS, CH_GLOB, CH_GREATER_THAN };
            string[] text_expressions = { CH_EQUALS, CH_NOT_EQUALS, CH_LESS_THAN, CH_GREATER_THAN, CH_LESS_THAN_EQUALS, CH_GREATER_THAN_EQUALS, CH_GLOB };

            if (this.customFilter.LogicalOperator == CustomFilter.LogicalOperators.And)
            {
                rbAnd.IsChecked = true;
                rbOr.IsChecked = false;

            }
            else
            {
                rbAnd.IsChecked = false;
                rbOr.IsChecked = true;
            }
            rbAnd.Checked += rbAndOr_Checked;
            rbOr.Checked += rbAndOr_Checked;
            // We start at 1 as there is already a header row
            for (int row_count = 1; row_count <= customFilter.SearchTermList.Count; row_count++)
            {
                // Get the values for each control

                string type = customFilter.SearchTermList[row_count].Type;
                Thickness thickness = new Thickness(5, 2, 5, 2);

                // Create a new row, where each row specifies a particular control and how it can be searched
                RowDefinition gridRow = new RowDefinition();
                gridRow.Height = GridLength.Auto;
                grid.RowDefinitions.Add(gridRow);

                // USE Column: A checkbox to indicate whether the current search row should be used as part of the search
                CheckBox cb = new CheckBox();
                cb.Margin = thickness;
                cb.VerticalAlignment = VerticalAlignment.Center;
                cb.HorizontalAlignment = HorizontalAlignment.Center;
                cb.IsChecked = customFilter.SearchTermList[row_count].UseForSearching;
                cb.Checked += Cb_CheckHandler;
                cb.Unchecked += Cb_CheckHandler;
                Grid.SetRow(cb, row_count);
                Grid.SetColumn(cb, 0);
                grid.Children.Add(cb);

                // LABEL column: The label associated with the control (Note: not the data label)
                TextBlock tbLabel = new TextBlock();
                tbLabel.Margin = thickness;
                tbLabel.Text = customFilter.SearchTermList[row_count].Label;
                tbLabel.Margin = new Thickness(5);
                Grid.SetRow(tbLabel, row_count);
                Grid.SetColumn(tbLabel, 1);
                grid.Children.Add(tbLabel);

                // EXPRESSION : creates a combo box where its contents of various expression values depends on the type
                ComboBox comboboxExpressions = new ComboBox();
                comboboxExpressions.SelectedValue = customFilter.SearchTermList[row_count].Expression; // Default: equals sign
                comboboxExpressions.Width = 60;
                comboboxExpressions.Margin = thickness;
                comboboxExpressions.IsEnabled = customFilter.SearchTermList[row_count].UseForSearching; ;
                comboboxExpressions.ItemsSource = (type == Constants.DatabaseElement.Counter) ? number_expressions : text_expressions;
                comboboxExpressions.SelectionChanged += CbExpressions_SelectionChanged; // Create the callback that is invoked whenever the user changes the expresison

                Grid.SetRow(comboboxExpressions, row_count);
                Grid.SetColumn(comboboxExpressions, 2);
                grid.Children.Add(comboboxExpressions);

                // Value column: The value used for comparison in the search
                // Notes and Counters both uses a text field, so they can be constructed as a textbox
                // However, Counters textboxes are modified to only allow integer input (both direct typing or pasting are checked)
                if (type == Constants.DatabaseElement.Note || type == Constants.DatabaseElement.Counter)
                {
                    TextBox tboxValue = new TextBox();
                    tboxValue.Text = customFilter.SearchTermList[row_count].Value;
                    tboxValue.Width = 150;
                    tboxValue.Height = 22;
                    tboxValue.TextWrapping = TextWrapping.NoWrap;
                    tboxValue.VerticalAlignment = VerticalAlignment.Center;
                    tboxValue.VerticalContentAlignment = VerticalAlignment.Center;
                    tboxValue.IsEnabled = customFilter.SearchTermList[row_count].UseForSearching; ;
                    tboxValue.Margin = thickness;

                    // The following is specific only to Counters
                    if (type == Constants.DatabaseElement.Counter)
                    {
                        tboxValue.PreviewTextInput += TxtboxCounterValue_PreviewTextInput;
                        DataObject.AddPastingHandler(tboxValue, tboxCounterValueOnPaste);
                    }
                    tboxValue.TextChanged += Txtbox_TextChanged;

                    Grid.SetRow(tboxValue, row_count);
                    Grid.SetColumn(tboxValue, 3);
                    grid.Children.Add(tboxValue);
                }
                // FixedChoice and ImageQuality both present combo boxes, so they can be constructed the same way
                else if (type == Constants.DatabaseElement.FixedChoice || type == Constants.DatabaseElement.ImageQuality)
                {
                    ComboBox comboBoxValue = new ComboBox();

                    comboBoxValue.Width = 150;
                    comboBoxValue.Margin = thickness;
                    comboBoxValue.IsEnabled = customFilter.SearchTermList[row_count].UseForSearching; ;

                    // Create the dropdown menu 
                    string list = customFilter.SearchTermList[row_count].List;
                    list += " | "; // Add an empty field so it can be reset to empty by the user
                    string[] choices = list.Split(new char[] { '|' });
                    comboBoxValue.ItemsSource = choices;
                    comboBoxValue.SelectedItem = customFilter.SearchTermList[row_count].Value;
                    comboBoxValue.SelectionChanged += ComboBoxValue_SelectionChanged;
                    Grid.SetRow(comboBoxValue, row_count);
                    Grid.SetColumn(comboBoxValue, 3);
                    grid.Children.Add(comboBoxValue);
                }
                else if (type == Constants.DatabaseElement.Flag)
                {
                    // Flags present checkboxes
                    CheckBox cbFlag = new CheckBox();
                    cbFlag.Margin = thickness;
                    cbFlag.IsEnabled = customFilter.SearchTermList[row_count].UseForSearching; ;
                    cbFlag.VerticalAlignment = VerticalAlignment.Center;
                    cbFlag.HorizontalAlignment = HorizontalAlignment.Left;

                    cbFlag.IsChecked = (customFilter.SearchTermList[row_count].Value.ToLower() == "false") ? false : true;
                    cbFlag.Checked += CbFlag_CheckHandler;
                    cbFlag.Unchecked += CbFlag_CheckHandler;
                    Grid.SetRow(cbFlag, row_count);
                    Grid.SetColumn(cbFlag, 3);
                    grid.Children.Add(cbFlag);
                }

                // Search Criteria Column: initially as an empty textblock
                TextBlock tbSearchcriteria = new TextBlock();
                tbSearchcriteria.Width = 150;
                tbSearchcriteria.Margin = thickness;
                tbSearchcriteria.IsEnabled = true;
                tbSearchcriteria.VerticalAlignment = VerticalAlignment.Center;

                Grid.SetRow(tbSearchcriteria, row_count);
                Grid.SetColumn(tbSearchcriteria, 4);
                grid.Children.Add(tbSearchcriteria);
            }
            UpdateSearchCriteriaFeedback();
        }
        #endregion 

        #region Search Selection Handlers 
        // Select: When the use checks or unchecks the checkbox for a row
        // - activate or deactivate the search criteria for that row
        // - update the searchterms to reflect the new status 
        // - update the UI to activate or deactivate (or show or hide) its various search terms
        private void Cb_CheckHandler(object sender, RoutedEventArgs e)
        {
            CheckBox cb = sender as CheckBox;
            int row = Grid.GetRow(cb);  // And you have the row number...
            bool state = cb.IsChecked.Value;

            SearchTerm searchterms = this.customFilter.SearchTermList[row];
            searchterms.UseForSearching = cb.IsChecked.Value;

            TextBlock tbLabel = (TextBlock)grid.Children.Cast<UIElement>().First(ex => Grid.GetRow(ex) == row && Grid.GetColumn(ex) == 1);
            ComboBox cbExpression = (ComboBox)grid.Children.Cast<UIElement>().First(ex => Grid.GetRow(ex) == row && Grid.GetColumn(ex) == 2);

            UIElement uieValue = (UIElement)grid.Children.Cast<UIElement>().First(ex => Grid.GetRow(ex) == row && Grid.GetColumn(ex) == 3);

            tbLabel.FontWeight = (cb.IsChecked.Value) ? FontWeights.Bold : FontWeights.Normal;
            cbExpression.IsEnabled = cb.IsChecked.Value;
            uieValue.IsEnabled = cb.IsChecked.Value;

            UpdateSearchCriteriaFeedback();
        }

        // Expression: The user has selected a new expression
        // - set its corresponding search term in the searchList data structure
        // - update the UI to show the search criteria 
        private void CbExpressions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox cb = sender as ComboBox;
            int row = Grid.GetRow(cb);  // Get the row number...
            this.customFilter.SearchTermList[row].Expression = cb.SelectedValue.ToString(); // Set the corresponding expression to the current selection
            UpdateSearchCriteriaFeedback();
        }

        // Value (Counters and Notes): The user has selected a new value
        // - set its corresponding search term in the searchList data structure
        // - update the UI to show the search criteria 
        private void Txtbox_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox tb = sender as TextBox;
            int row = Grid.GetRow(tb);  // Get the row number...
            this.customFilter.SearchTermList[row].Value = tb.Text; // Set the corresponding value to the current selection
            UpdateSearchCriteriaFeedback();
        }

        // Value (Counter) Helper function: textbox accept only typed numbers 
        private void TxtboxCounterValue_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = IsNumbersOnly(e.Text);
        }

        // Value (Counter) Helper function:  textbox accept only pasted numbers 
        private void tboxCounterValueOnPaste(object sender, DataObjectPastingEventArgs e)
        {
            bool isText = e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, true);
            if (!isText) e.CancelCommand();

            string text = e.SourceDataObject.GetData(DataFormats.UnicodeText) as string;
            if (IsNumbersOnly(text)) e.CancelCommand();
        }

        // Value(Counter) Helper function: checks if the text contains only numbers
        private static bool IsNumbersOnly(string text)
        {
            Regex regex = new Regex("[^0-9.-]+"); //regex that matches allowed text
            return regex.IsMatch(text);
        }

        // Value (for FixedChoices): The user has selected a new value 
        // - set its corresponding search term in the searchList data structure
        // - update the UI to show the search criteria 
        private void ComboBoxValue_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox cb = sender as ComboBox;
            int row = Grid.GetRow(cb);  // Get the row number...
            this.customFilter.SearchTermList[row].Value = cb.SelectedValue.ToString(); // Set the corresponding value to the current selection
            UpdateSearchCriteriaFeedback();
        }

        // Value (for Flags): The user has checked or unchecked a new value 
        // - set its corresponding search term in the searchList data structure
        // - update the UI to show the search criteria 
        private void CbFlag_CheckHandler(object sender, RoutedEventArgs e)
        {
            CheckBox cb = sender as CheckBox;
            int row = Grid.GetRow(cb);  // Get the row number...
            this.customFilter.SearchTermList[row].Value = cb.IsChecked.ToString().ToLower(); // Set the corresponding value to the current selection
            UpdateSearchCriteriaFeedback();
        }
        #endregion

        #region Update the UI
        // Updates the search criteria shown across all rows to reflect the contents of the search list
        // i show or hides the search term feedback for that row.
        private void UpdateSearchCriteriaFeedback()
        {
            SearchTerm st;
            int row;
            bool last_expression = true;
            bool search_terms_exists = false;
            // We go backwards, as we don't want to print the AND or OR on the last expression
            for (int i = this.customFilter.SearchTermList.Count - 1; i >= 0; i--)
            {
                row = i + 1; // we offset the row by 1 as row 0 is the header
                st = this.customFilter.SearchTermList.Values.ElementAt(i);
                TextBlock tb_search_feedback = (TextBlock)grid.Children.Cast<UIElement>().First(ex => Grid.GetRow(ex) == row && Grid.GetColumn(ex) == 4);

                if (st.UseForSearching)  // We are only interested in showing feedback for rows where the search expression is active
                {
                    search_terms_exists = true;
                    // Construct the search term 
                    string string_to_display = st.DataLabel + " " + st.Expression + " "; // So far, we have "Data Label = 

                    string value = st.Value.Trim();    // the Value, but if its 
                    if (value.Length == 0) value = "\"\"";  // an empty string, display it as ""
                    string_to_display += value;

                    // If its not the last expression and if there are multiple queries (i.e., search terms) then show the And or Or at its end.
                    if (!last_expression && this.customFilter.IsQueryHasMultipleSelectedSearchTerms)
                    {
                        if (this.customFilter.IsQueryHasMultipleSelectedSearchTerms) string_to_display += " " + this.customFilter.LogicalOperator.ToString();
                    }

                    tb_search_feedback.Text = string_to_display;
                    last_expression = false;
                }
                else // The search term is not used for searching, so clear the feedback field
                {
                    tb_search_feedback.Text = "";
                }
            }

            int count = this.customFilter.QueryResultCount;
            this.OkButton.IsEnabled = (count > 0) ? true : false;
            this.btnShowAll.IsEnabled = search_terms_exists;
            textBlockQueryMatches.Text = this.customFilter.QueryResultCount.ToString();
        }
        #endregion

        #region Button callbacks
        // Toggle the visibility of the explanation panel
        private void btnHideText_StateChange(object sender, RoutedEventArgs e)
        {
            DlgCustomViewFilter.HideExplanation = ((bool)btnHideText.IsChecked);
            gridExplanation.Visibility = DlgCustomViewFilter.HideExplanation ? Visibility.Collapsed : Visibility.Visible;
        }

        // Radio buttons for determing if we use And or Or
        private void rbAndOr_Checked(object sender, RoutedEventArgs e)
        {
            RadioButton rb = sender as RadioButton;
            this.customFilter.LogicalOperator = (rb == this.rbAnd) ? CustomFilter.LogicalOperators.And : CustomFilter.LogicalOperators.Or;
            UpdateSearchCriteriaFeedback();
        }
        // Apply the filter if the Ok button is clicked
        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            this.customFilter.RunQuery();
            this.DialogResult = true;
        }

        // Cancel - exit the dialog without doing anythikng.
        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
        #endregion

        // When this button is pressed, all the search terms checkboxes are cleared, which is equivalent to showing all images
        private void btnShowAll_Click(object sender, RoutedEventArgs e)
        {
            // We go backwards, as we don't want to print the AND or OR on the last expression
            for (int row=1; row <= this.customFilter.SearchTermList.Count ; row++)
            {
                CheckBox chbox = (CheckBox)grid.Children.Cast<UIElement>().First(ex => Grid.GetRow(ex) == row && Grid.GetColumn(ex) == 0);
                chbox.IsChecked = false;
            }
        }
    }
}
