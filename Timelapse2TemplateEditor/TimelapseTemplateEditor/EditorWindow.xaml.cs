using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions; // For debugging
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Timelapse.Database;
using Timelapse.Editor.Util;
using Timelapse.Util;

namespace Timelapse.Editor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class EditorWindow : Window
    {
        #region Private Variables
        // database where the template is stored
        private TemplateDatabase templateDatabase;

        // state tracking
        private bool generateControlsAndSpreadsheet;
        private MostRecentlyUsedList<string> mostRecentTemplates;
        private bool rowsActionsOn;
        private bool tabWasPressed; // to make tab trigger row update.

        // These variables support the drag/drop of controls
        private bool isMouseDown;
        private bool isMouseDragging;
        private Point startPoint;
        private UIElement realMouseDragSource;
        private UIElement dummyMouseDragSource;
        #endregion

        #region Initialization, Window Loading and Closing
        /// <summary>
        /// Starts the UI.
        /// </summary>
        public EditorWindow()
        {
            this.InitializeComponent();
            Utilities.TryFitWindowInWorkingArea(this);

            // Abort if some of the required dependencies are missing
            if (Dependencies.AreRequiredBinariesPresent(EditorConstant.ApplicationName, Assembly.GetExecutingAssembly()) == false)
            {
                Dependencies.ShowMissingBinariesDialog(EditorConstant.ApplicationName);
                Application.Current.Shutdown();
            }

            this.dummyMouseDragSource = new UIElement();
            this.generateControlsAndSpreadsheet = true;
            this.rowsActionsOn = false;
            this.tabWasPressed = false;

            this.ShowAllColumnsMenuItem_Click(this.ShowAllColumns, null);

            // Recall state from prior sessions
            using (EditorRegistryUserSettings userSettings = new EditorRegistryUserSettings())
            {
                this.mostRecentTemplates = userSettings.ReadMostRecentTemplates();  // the last path opened by the user is stored in the registry
            }

            // populate the most recent databases list
            this.MenuItemRecentTemplates_Refresh();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            VersionClient updater = new VersionClient(Constants.ApplicationName, Constants.LatestVersionAddress);
            updater.TryGetAndParseVersion(false);
        }

        // When the main window closes, apply any pending edits.
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            this.TemplateDataGrid.CommitEdit();

            // Save the current filter set and the index of the current image being viewed in that set, and save it into the registry
            using (EditorRegistryUserSettings userSettings = new EditorRegistryUserSettings())
            {
                userSettings.WriteMostRecentTemplates(this.mostRecentTemplates);
            }
        }
        #endregion

        #region File Menu Callbacks
        /// <summary>
        /// Creates a new database file of a user chosen name in a user chosen location.
        /// </summary>
        private void NewFileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            this.TemplateDataGrid.CommitEdit(); // to apply edits that the enter key was not pressed

            // Configure save file dialog box
            SaveFileDialog dlg = new SaveFileDialog();
            dlg.FileName = Path.GetFileNameWithoutExtension(Constants.File.DefaultTemplateDatabaseFileName); // Default file name without the extension
            dlg.DefaultExt = Constants.File.TemplateDatabaseFileExtension; // Default file extension
            dlg.Filter = "Database Files (" + Constants.File.TemplateDatabaseFileExtension + ")|*" + Constants.File.TemplateDatabaseFileExtension; // Filter files by extension 
            dlg.Title = "Select Location to Save New Template File";

            // Show save file dialog box
            Nullable<bool> result = dlg.ShowDialog();

            // Process save file dialog box results 
            if (result == true)
            {
                this.TrySaveDatabaseBackupFile();

                // Overwrite the file if it exists
                if (File.Exists(dlg.FileName))
                {
                    File.Delete(dlg.FileName);
                }

                // Open document 
                this.InitializeDataGrid(dlg.FileName);
            }
        }

        /// <summary>
        /// Opens an existing database file.
        /// </summary>
        private void OpenFileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            this.TemplateDataGrid.CommitEdit(); // to save any edits that the enter key was not pressed

            OpenFileDialog dlg = new OpenFileDialog();
            dlg.FileName = Path.GetFileNameWithoutExtension(Constants.File.DefaultTemplateDatabaseFileName); // Default file name without the extension
            dlg.DefaultExt = Constants.File.TemplateDatabaseFileExtension; // Default file extension
            dlg.Filter = "Database Files (" + Constants.File.TemplateDatabaseFileExtension + ")|*" + Constants.File.TemplateDatabaseFileExtension; // Filter files by extension 
            dlg.Title = "Select an Existing Template File to Open";

            // Show open file dialog box
            Nullable<bool> result = dlg.ShowDialog();

            // Process open file dialog box results 
            if (result == true)
            {
                this.TrySaveDatabaseBackupFile();

                // Open document 
                this.InitializeDataGrid(dlg.FileName);
            }
        }

        // Opern a rencently used template
        private void MenuItemRecentTemplate_Click(object sender, RoutedEventArgs e)
        {
            string recentTemplatePath = (string)((MenuItem)sender).ToolTip;

            // Open document 
            this.TrySaveDatabaseBackupFile();
            this.InitializeDataGrid(recentTemplatePath);
        }

        /// <summary>
        /// Convert an old style xml template to the new ddb style
        /// </summary>
        private void ConvertCodeTemplateFileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            string codeTemplateFileName = String.Empty;  // The code template file name

            this.TemplateDataGrid.CommitEdit(); // to save any edits that the enter key was not pressed

            // Get the name of the Code Template file to open
            OpenFileDialog codeTemplateFile = new OpenFileDialog();
            codeTemplateFile.FileName = Path.GetFileName(Constants.File.XmlDataFileName); // Default file name
            string xmlDataFileExtension = Path.GetExtension(Constants.File.XmlDataFileName);
            codeTemplateFile.DefaultExt = xmlDataFileExtension; // Default file extension
            codeTemplateFile.Filter = "Code Template Files (" + xmlDataFileExtension + ")|*" + xmlDataFileExtension; // Filter files by extension 
            codeTemplateFile.Title = "Select Code Template File to convert...";

            Nullable<bool> result = codeTemplateFile.ShowDialog(); // Show the open file dialog box
            if (result == true)
            {
                codeTemplateFileName = codeTemplateFile.FileName;  // Process open file dialog box results 
            }
            else
            {
                return;
            }

            // Get the name of the new database file to create (over-writes it if it exists)
            SaveFileDialog templateDatabaseFile = new SaveFileDialog();
            templateDatabaseFile.Title = "Select Location to Save the Converted Template File";
            templateDatabaseFile.FileName = Path.GetFileNameWithoutExtension(Constants.File.DefaultTemplateDatabaseFileName); // Default file name
            templateDatabaseFile.DefaultExt = Constants.File.TemplateDatabaseFileExtension; // Default file extension
            templateDatabaseFile.Filter = "Database Files (" + Constants.File.TemplateDatabaseFileExtension + ")|*" + Constants.File.TemplateDatabaseFileExtension; // Filter files by extension 
            result = templateDatabaseFile.ShowDialog(); // Show open file dialog box

            // Process open file dialog box results 
            if (result == true)
            {
                this.TrySaveDatabaseBackupFile();

                // Overwrite the file if it exists
                if (File.Exists(templateDatabaseFile.FileName))
                {
                    File.Delete(templateDatabaseFile.FileName);
                }
            }

            // Start with the default layout of the data template
            this.InitializeDataGrid(templateDatabaseFile.FileName);

            // Now convert the code template file into a Data Template, overwriting values and adding rows as required
            Mouse.OverrideCursor = Cursors.Wait;

            this.generateControlsAndSpreadsheet = false;
            List<string> conversionErrors;
            CodeTemplateImporter importer = new CodeTemplateImporter();
            importer.Import(codeTemplateFileName, this.templateDatabase, out conversionErrors);
            this.generateControlsAndSpreadsheet = true;

            EditorControls.Generate(this, this.controlsPanel, this.templateDatabase.TemplateTable);
            this.GenerateSpreadsheet();
            this.ReInitializeDataGrid(this.templateDatabase.FilePath);
            Mouse.OverrideCursor = null;
            if (conversionErrors.Count > 0)
            {
                DialogMessageBox messageBox = new DialogMessageBox("One or more data labels were problematic", this);
                messageBox.Message.Icon = MessageBoxImage.Warning;
                messageBox.Message.Problem = conversionErrors.Count.ToString() + " of your Data Labels were problematic." + Environment.NewLine + Environment.NewLine +
                              "Data Labels:" + Environment.NewLine +
                              "\u2022 must be unique," + Environment.NewLine +
                              "\u2022 can only contain alphanumeric characters and '_'," + Environment.NewLine +
                              "\u2022 cannot match particular reserved words.";
                messageBox.Message.Result = "We will automatically repair these Data Labels:";
                foreach (string s in conversionErrors)
                {
                    messageBox.Message.Solution += Environment.NewLine + "\u2022 " + s;
                }
                messageBox.Message.Hint = "Check if these are the names you want. You can also rename these corrected Data Labels if you want";

                messageBox.ShowDialog();
            }
        }

        /// <summary>
        /// Exits the application.
        /// </summary>
        private void ExitFileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            this.TemplateDataGrid.CommitEdit(); // to save any edits that the enter key was not pressed
            Application.Current.Shutdown();
        }
        #endregion

        #region View Menu Callbacks
        /// <summary>
        /// Depending on the menu's checkbox state, show all columns or hide selected columns
        /// </summary>
        private void ShowAllColumnsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MenuItem mi = sender as MenuItem;
            if (mi == null)
            {
                return;
            }

            foreach (DataGridColumn column in this.TemplateDataGrid.Columns)
            {
                if (!mi.IsChecked &&
                    (column.Header.Equals(EditorConstant.Control.ID) || column.Header.Equals(EditorConstant.Control.ControlOrder) || column.Header.Equals(EditorConstant.Control.SpreadsheetOrder)))
                {
                    column.MinWidth = 0;
                    column.Width = new DataGridLength(0);
                }
                else
                {
                    column.MinWidth = 90;
                    if (column.Header.Equals(Constants.Control.Visible) || column.Header.Equals(Constants.Control.Copyable) || column.Header.Equals(EditorConstant.Control.Width))
                    {
                        column.MinWidth = 65;
                    }

                    if (column.Header.Equals(Constants.Control.Tooltip))
                    {
                        column.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
                    }
                    else
                    {
                        column.Width = new DataGridLength(1, DataGridLengthUnitType.SizeToCells);
                    }
                }
            }
        }

        /// <summary>
        /// Show the dialog that allows a user to inspect image metadata
        /// </summary>
        private void MenuItemInspectImageMetaData_Click(object sender, RoutedEventArgs e)
        {
            DialogInspectMetaData dlg = new DialogInspectMetaData(this.templateDatabase.FilePath);
            dlg.Owner = this;
            dlg.Show();
        }
        #endregion

        #region Help Menu Callbacks
        /// <summary>Display the Timelapse home page </summary> 
        private void MenuTimelapseWebPage_Click(object sender, RoutedEventArgs e)
        {
            Uri tutorialUri = new Uri("http://saul.cpsc.ucalgary.ca/timelapse/");
            Process.Start(new ProcessStartInfo(tutorialUri.AbsoluteUri));
        }

        /// <summary>Display the manual in a web browser </summary> 
        private void MenuTutorialManual_Click(object sender, RoutedEventArgs e)
        {
            Uri tutorialUri = new Uri("http://saul.cpsc.ucalgary.ca/timelapse/uploads/Installs/Timelapse2/Timelapse2Manual.pdf");
            Process.Start(new ProcessStartInfo(tutorialUri.AbsoluteUri));
        }

        /// <summary>Display the page in the web browser that lets you join the Timelapse mailing list</summary>
        private void MenuJoinTimelapseMailingList_Click(object sender, RoutedEventArgs e)
        {
            Uri tutorialUri = new Uri("http://mailman.ucalgary.ca/mailman/listinfo/timelapse-l");
            Process.Start(new ProcessStartInfo(tutorialUri.AbsoluteUri));
        }

        /// <summary>Download the sample images from a web browser</summary>
        private void MenuDownloadSampleImages_Click(object sender, RoutedEventArgs e)
        {
            Uri tutorialUri = new Uri("http://saul.cpsc.ucalgary.ca/timelapse/uploads/Main/TutorialImageSet.zip");
            Process.Start(new ProcessStartInfo(tutorialUri.AbsoluteUri));
        }

        /// <summary>Send mail to the timelapse mailing list</summary> 
        private void MenuMailToTimelapseMailingList_Click(object sender, RoutedEventArgs e)
        {
            Uri tutorialUri = new Uri("mailto:timelapse-l@mailman.ucalgary.ca");
            Process.Start(new ProcessStartInfo(tutorialUri.AbsoluteUri));
        }

        private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            DialogAboutTimelapseEditor about = new DialogAboutTimelapseEditor();
            about.Owner = this;
            about.ShowDialog();
        }
        #endregion

        #region DataGrid and New Database Initialization
        /// <summary>
        /// Given a database file path,create a new DB file if one does not exist, or load a DB file if there is one.
        /// After a DB file is loaded, the table is extracted and loaded a DataTable for binding to the DataGrid.
        /// Some listeners are added to the DataTable, and the DataTable is bound. The add row buttons are enabled.
        /// </summary>
        /// <param name="templateDatabaseFilePath">The path of the DB file created or loaded</param>
        private void InitializeDataGrid(string templateDatabaseFilePath)
        {
            MyTrace.MethodName("DG InitializeDataGrid");

            // Create a new DB file if one does not exist, or load a DB file if there is one.
            this.templateDatabase = TemplateDatabase.CreateOrOpen(templateDatabaseFilePath);

            // Have the window title include the database file name
            this.OnlyWindow.Title = EditorConstant.MainWindowBaseTitle + " | File: " + Path.GetFileName(this.templateDatabase.FilePath);

            // Map the data tableto the data grid, and create a callback executed whenever the datatable row changes
            this.templateDatabase.BindToEditorDataGrid(this.TemplateDataGrid, this.TemplateTable_RowChanged);

            // Now that there is a data table, enable the buttons that allows rows to be added.
            this.AddCounterButton.IsEnabled = true;
            this.AddFixedChoiceButton.IsEnabled = true;
            this.AddNoteButton.IsEnabled = true;
            this.AddFlagButton.IsEnabled = true;

            // Update the user interface specified by the contents of the table
            // Change the help text message
            EditorControls.Generate(this, this.controlsPanel, this.templateDatabase.TemplateTable);
            this.GenerateSpreadsheet();
            this.InitializeUI();

            // update state
            // unlike Timelapse, editor processes are currently 1:1 with opened template files
            // so disable the recent templates list rather than call this.MenuItemRecentTemplates_Refresh
            this.mostRecentTemplates.SetMostRecent(templateDatabaseFilePath);
            this.MenuItemRecentTemplates.IsEnabled = false;
        }

        private void InitializeUI()
        {
            MyTrace.MethodName("DG InitializeUI");
            this.HelpText.Text = "Click the white fields to edit their contents. Gray fields are not editable." + Environment.NewLine +
                "List items: Click 'Define List' to create or edit menu items, one per line.";
            this.HelpDocument.Visibility = Visibility.Collapsed;
            this.HelpText.Visibility = Visibility.Visible;
            this.TemplateDataGrid.Visibility = Visibility.Visible;
            this.RowControls.Visibility = Visibility.Visible;
            this.TextMessage1.Visibility = Visibility.Visible;
            this.OtherGrids.Visibility = Visibility.Visible;
            this.NewFileMenuItem.IsEnabled = false;
            this.OpenFileMenuItem.IsEnabled = false;
            this.ConvertFileMenuItem.IsEnabled = false;
            this.ViewMenu.IsEnabled = true;
        }

        // Reload a database into the grid. We do this as part of the convert, where we create the database, but then have to reinitialize the datagrid if we want to see the results.
        // So this is actually a reduced form of INitializeDataGrid
        private void ReInitializeDataGrid(string templateDatabaseFilePath)
        {
            MyTrace.MethodName("DG ReInitializeDataGrid");

            // Create a new DB file if one does not exist, or load a DB file if there is one.
            this.templateDatabase = TemplateDatabase.CreateOrOpen(templateDatabaseFilePath);

            // Have the window title include the database file name
            this.OnlyWindow.Title = EditorConstant.MainWindowBaseTitle + " | File: " + Path.GetFileName(this.templateDatabase.FilePath);

            // Map the data table to the data grid, and create a callback executed whenever the datatable row changes
            this.templateDatabase.BindToEditorDataGrid(this.TemplateDataGrid, this.TemplateTable_RowChanged);

            // Update the user interface specified by the contents of the table
            EditorControls.Generate(this, this.controlsPanel, this.templateDatabase.TemplateTable);
            this.GenerateSpreadsheet();
            this.InitializeUI();       
        }
        #endregion DataGrid and New Database Initialization

        #region Data Changed Listeners and Methods
        /// <summary>
        /// Updates a given control in the database with the current state of the DataGrid. 
        /// </summary>
        private void SyncControlToDatabase(ControlRow control)
        {
            MyTrace.MethodName("DB SyncControlToDatabase");

            this.templateDatabase.SyncControlToDatabase(control);
            if (this.generateControlsAndSpreadsheet)
            {
                EditorControls.Generate(this, this.controlsPanel, this.templateDatabase.TemplateTable);
                this.GenerateSpreadsheet();
            }
        }

        /// <summary>
        /// Whenever a row changes save the database, which also updates the grid colors.
        /// </summary>
        private void TemplateTable_RowChanged(object sender, DataRowChangeEventArgs e)
        {
            MyTrace.MethodName("DB TemplateTable_RowChanged");
            if (!this.rowsActionsOn)
            {
                this.SyncControlToDatabase(new ControlRow(e.Row));
            }
            DataGridRow selectedRow = (DataGridRow)this.TemplateDataGrid.ItemContainerGenerator.ContainerFromIndex(this.TemplateDataGrid.SelectedIndex);
        }
        #endregion Data Changed Listeners and Methods=

        #region Datagrid Row Modifiers listeners and methods
        /// <summary>
        /// Logic to enable/disable editing buttons depending on there being a row selection
        /// Also sets the text for the remove row button.
        /// </summary>
        private void TemplateDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            MyTrace.MethodName("DG TemplateDataGrid_SelectionChanged");
            DataRowView selectedRowView = this.TemplateDataGrid.SelectedItem as DataRowView;
            if (selectedRowView == null)
            {
                this.RemoveControlButton.IsEnabled = false;
                this.RemoveControlButton.Content = "Remove";
                return;
            }

            ControlRow control = new ControlRow(selectedRowView.Row);
            if (Constants.Control.StandardTypes.Contains(control.Type))
            {
                this.RemoveControlButton.IsEnabled = false;
                this.RemoveControlButton.Content = "Item cannot" + Environment.NewLine + "be removed";
            }
            else
            {
                this.RemoveControlButton.IsEnabled = true;
                this.RemoveControlButton.Content = "Remove";
            }
        }

        /// <summary>
        /// Adds a row to the table. The row type is decided by the button tags.
        /// Default values are set for the added row, differing depending on type.
        /// </summary>
        private void AddControlButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            string controlType = button.Tag.ToString();
            this.templateDatabase.AddUserDefinedControl(controlType);

            this.TemplateDataGrid.DataContext = this.templateDatabase.TemplateTable;
            this.TemplateDataGrid.ScrollIntoView(this.TemplateDataGrid.Items[this.TemplateDataGrid.Items.Count - 1]);
            EditorControls.Generate(this, this.controlsPanel, this.templateDatabase.TemplateTable);
            this.GenerateSpreadsheet();
            this.OnControlOrderChanged();
        }

        /// <summary>
        /// Removes a row from the table and shifts up the ids on the remaining rows.
        /// The required rows are unable to be deleted.
        /// </summary>
        private void RemoveControlButton_Click(object sender, RoutedEventArgs e)
        {
            DataRowView selectedRowView = this.TemplateDataGrid.SelectedItem as DataRowView;
            if (selectedRowView == null || selectedRowView.Row == null)
            {
                // nothing to do
                return;
            }

            ControlRow control = new ControlRow(selectedRowView.Row);
            if (EditorControls.IsStandardControlType(control.Type))
            {
                // standard controls cannot be removed
                return;
            }

            this.rowsActionsOn = true;
            this.templateDatabase.RemoveUserDefinedControl(new ControlRow(selectedRowView.Row));

            // update the control panel so it reflects the current values in the database
            EditorControls.Generate(this, this.controlsPanel, this.templateDatabase.TemplateTable);
            this.GenerateSpreadsheet();

            this.rowsActionsOn = false;
        }
        #endregion Datagrid Row Modifyiers listeners and methods

        #region Choice Edit Box Handlers
        // When the  choice list button is clicked, raise a dialog box that lets the user edit the list of choices
        private void ChoiceListButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;

            // The button tag holds the Control Order of the row the button is in, not the ID.
            // So we have to search through the rows to find the one with the correct control order
            // and retrieve / set the ItemList menu in that row.
            ControlRow choiceControl = this.templateDatabase.TemplateTable.FirstOrDefault(control => control.ControlOrder.ToString().Equals(button.Tag.ToString()));

            // a control should be found but, just in case...
            string choiceList = String.Empty;
            if (choiceControl != null)
            {
                choiceList = choiceControl.List;
            }

            choiceList = Utilities.ConvertBarsToLineBreaks(choiceList);
            DialogEditChoiceList choiceListDialog = new DialogEditChoiceList(button, choiceList);
            choiceListDialog.Owner = this;
            bool? result = choiceListDialog.ShowDialog();
            if (result == true)
            {
                if (choiceControl != null)
                {
                    choiceControl.List = Utilities.ConvertLineBreaksToBars(choiceListDialog.Choices);
                    this.templateDatabase.SyncControlToDatabase(choiceControl);
                }
                else
                {
                    // choiceControl should never be null, so shouldn't get here. But just in case this does happen, 
                    // I am setting the itemList to be the one in the ControlOrder row. This was the original buggy version that didn't work, but what the heck.
                    this.templateDatabase.TemplateTable[Convert.ToInt32(button.Tag) - 1].List = Utilities.ConvertLineBreaksToBars(choiceListDialog.Choices);
                }
            }
        }
        #endregion

        #region Cell Editing / Coloring Listeners and Methods
        /// <summary>
        /// Informs application tab was used, which allows it to more quickly visualize the grid values in the preview.
        /// Tab does not normal raise the row edited listener, which we are using to do the update.
        /// Note that by setting the e.handled to true, we ignore the tab navigation (as this was introducing problems)
        /// </summary>
        private void TemplateDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            MyTrace.MethodName("DG TemplateDataGrid_PreviewKeyDow");
            if (e.Key == Key.Tab)
            {
                this.tabWasPressed = true;
                e.Handled = true;
            }
            if (this.TemplateDataGrid.CurrentColumn.Header.Equals(EditorConstant.Control.DataLabel))
            {
                // No white space in a data label
                // TODOSAUL: check for other disallowed values such as -, =, etc.
                if (e.Key == Key.Space)
                {
                    this.ShowDataLabelRequirementsDialog();
                    e.Handled = true;
                }
            }
        }

        // Accept only numbers in counters, and t/f in flags
        // This could probably be done way simpler
        private void TemplateDataGrid_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            MyTrace.MethodName("DG TemplateDataGrid_PreviewTextInput");
            if (this.TemplateDataGrid.SelectedIndex != -1)
            {
                // this is how you can get an actual cell.
                DataGridRow selectedRow = (DataGridRow)this.TemplateDataGrid.ItemContainerGenerator.ContainerFromIndex(this.TemplateDataGrid.SelectedIndex);
                DataGridCellsPresenter presenter = GetVisualChild<DataGridCellsPresenter>(selectedRow);
                if (this.TemplateDataGrid.CurrentColumn != null)
                {
                    DataGridCell cell = (DataGridCell)presenter.ItemContainerGenerator.ContainerFromIndex(this.TemplateDataGrid.CurrentColumn.DisplayIndex);

                    // We only do something if its an editable row and if we are in the Default Value column
                    if (cell.Background.Equals(EditorConstant.NotEditableCellColor))
                    {
                        e.Handled = true;
                        return;
                    }
                    if (!cell.Background.Equals(EditorConstant.NotEditableCellColor))
                    {
                        if (this.TemplateDataGrid.CurrentColumn.Header.Equals(EditorConstant.Control.DataLabel))
                        {
                            // Only allow alphanumeric and '_' in data labels
                            if ((!this.IsAllValidAlphaNumericChars(e.Text)) && !e.Text.Equals("_"))
                            {
                                this.ShowDataLabelRequirementsDialog();
                                e.Handled = true;
                            }
                        }
                        else if (this.TemplateDataGrid.CurrentColumn.Header.Equals(EditorConstant.Control.DefaultValue))
                        {
                            ControlRow control = new ControlRow((selectedRow.Item as DataRowView).Row);
                            if (control.Type == Constants.Control.Counter)
                            {
                                // Its a counter. Only allow numbers
                                e.Handled = !this.IsAllValidNumericChars(e.Text);
                            }
                            else if (control.Type == Constants.Control.Flag)
                            {
                                // Its a flag. Only allow t/f and translate that to true / false
                                DataRowView dataRow = (DataRowView)this.TemplateDataGrid.SelectedItem;
                                int index = this.TemplateDataGrid.CurrentCell.Column.DisplayIndex;
                                if (e.Text == "t" || e.Text == "T")
                                {
                                    dataRow.Row[index] = Constants.Boolean.True;
                                    this.SyncControlToDatabase(new ControlRow(dataRow.Row));
                                }
                                else if (e.Text == "f" || e.Text == "F")
                                {
                                    dataRow.Row[index] = Constants.Boolean.False;
                                    this.SyncControlToDatabase(new ControlRow(dataRow.Row));
                                }
                                e.Handled = true;
                            }
                        }
                    }
                }
            }
        }

        private bool IsAllValidNumericChars(string str)
        {
            foreach (char c in str)
            {
                if (!Char.IsNumber(c))
                {
                    return false;
                }
            }
            return true;
        }

        private bool IsAllValidAlphaNumericChars(string str)
        {
            foreach (char c in str)
            {
                if (!Char.IsLetterOrDigit(c))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Before cell editing begins on a cell click, the cell is disabled if it is grey (meaning cannot be edited).
        /// Another method re-enables the cell immediately afterwards.
        /// The reason for this implementation is because disabled cells cannot be single clicked, which is needed for row actions.
        /// </summary>
        private void NewTemplateDataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            MyTrace.MethodName("DG NewTemplateDataGrid_BeginningEdit");
            if (this.TemplateDataGrid.SelectedIndex != -1)
            {
                // this is how you can get an actual cell.
                DataGridRow row = (DataGridRow)this.TemplateDataGrid.ItemContainerGenerator.ContainerFromIndex(this.TemplateDataGrid.SelectedIndex);
                DataGridCellsPresenter presenter = GetVisualChild<DataGridCellsPresenter>(row);
                if (this.TemplateDataGrid.CurrentColumn != null)
                {
                    DataGridCell cell = (DataGridCell)presenter.ItemContainerGenerator.ContainerFromIndex(this.TemplateDataGrid.CurrentColumn.DisplayIndex);

                    if (cell.Background.Equals(EditorConstant.NotEditableCellColor))
                    {
                        cell.IsEnabled = false;
                        this.TemplateDataGrid.CancelEdit();
                    }
                }
            }
        }

        /// <summary>
        /// After cell editing ends (prematurely or no), the cell is enabled.
        /// See TemplateDataGrid_BeginningEdit for full explanation.
        /// </summary>
        private void TemplateDataGrid_CurrentCellChanged(object sender, EventArgs e)
        {
            MyTrace.MethodName("DG TemplateDataGrid_CurrentCellChanged");
            if (this.TemplateDataGrid.SelectedIndex != -1)
            {
                // this is how you can get an actual cell.
                DataGridRow row = (DataGridRow)this.TemplateDataGrid.ItemContainerGenerator.ContainerFromIndex(this.TemplateDataGrid.SelectedIndex);
                DataGridCellsPresenter presenter = GetVisualChild<DataGridCellsPresenter>(row);
                if (this.TemplateDataGrid.CurrentColumn != null)
                {
                    DataGridCell cell = (DataGridCell)presenter.ItemContainerGenerator.ContainerFromIndex(this.TemplateDataGrid.CurrentColumn.DisplayIndex);
                    cell.IsEnabled = true;
                }
            }
            if (this.tabWasPressed)
            {
                this.tabWasPressed = false;
                DataRowView selectedRowView = this.TemplateDataGrid.SelectedItem as DataRowView; // current cell
                if (selectedRowView.Row != null)
                { 
                    this.SyncControlToDatabase(new ControlRow(selectedRowView.Row));
                }
            }
        }

        // Check to see if the data label entered is a reserved word or if its a non-unique label
        private void NewTemplateDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            MyTrace.MethodName("Main");
            // If the edited cell is not in the Data Label column, then just exit.
            if (!e.Column.Header.Equals(EditorConstant.Control.DataLabel))
            {
                return;
            }

            TextBox textBox = e.EditingElement as TextBox;
            string dataLabel = textBox.Text;

            // Check to see if the data label is empty. If it is, generate a unique data label and warn the user
            if (String.IsNullOrWhiteSpace(dataLabel))
            {
                DialogMessageBox messageBox = new DialogMessageBox("Data Labels cannot be empty", this);
                messageBox.Message.Icon = MessageBoxImage.Warning;
                messageBox.Message.Problem = "Data Labels cannot be empty. They must begin with a letter, followed only by letters, numbers, and '_'.";
                messageBox.Message.Result = "We will automatically create a uniquely named Data Label for you.";
                messageBox.Message.Hint = "You can create your own name for this Data Label. Start your label with a letter. Then use any combination of letters, numbers, and '_'.";
                messageBox.ShowDialog();
                textBox.Text = this.templateDatabase.GetNextUniqueDataLabel("DataLabel");
            }

            // Check to see if the data label is unique. If not, generate a unique data label and warn the user
            for (int row = 0; row < this.templateDatabase.TemplateTable.RowCount; row++)
            {
                ControlRow control = this.templateDatabase.TemplateTable[row];
                if (dataLabel.Equals(control.DataLabel))
                {
                    if (this.TemplateDataGrid.SelectedIndex == row)
                    {
                        continue; // Its the same row, so its the same key, so skip it
                    }

                    DialogMessageBox messageBox = new DialogMessageBox("Data Labels must be unique", this);
                    messageBox.Message.Icon = MessageBoxImage.Warning;
                    messageBox.Message.Problem = "'" + textBox.Text + "' is not a valid Data Label, as you have already used it in another row.";
                    messageBox.Message.Result = "We will automatically create a unique Data Label for you by adding a number to its end.";
                    messageBox.Message.Hint = "You can create your own unique name for this Data Label. Start your label with a letter. Then use any combination of letters, numbers, and '_'.";
                    messageBox.ShowDialog();
                    textBox.Text = this.templateDatabase.GetNextUniqueDataLabel("DataLabel");
                    break;
                }
            }

            // Check to see if the label (if its not empty, which it shouldn't be) has any illegal characters.
            // Note that most of this is redundant, as we have already checked for illegal characters as they are typed. However,
            // we have not checked to see if the first letter is alphabetic.
            if (dataLabel.Length > 0)
            {
                Regex alphanumdash = new Regex("^[a-zA-Z0-9_]*$");
                Regex alpha = new Regex("^[a-zA-Z]*$");

                string firstCharacter = dataLabel[0].ToString();

                if (!(alpha.IsMatch(firstCharacter) && alphanumdash.IsMatch(dataLabel)))
                {
                    string replacementDataLabel = dataLabel;

                    if (!alpha.IsMatch(firstCharacter))
                    {
                        replacementDataLabel = "X" + replacementDataLabel.Substring(1);
                    }
                    replacementDataLabel = Regex.Replace(replacementDataLabel, @"[^A-Za-z0-9_]+", "X");

                    // TODOSAUL: should the dialog show the user the replacement label which will be used?
                    DialogMessageBox messageBox = new DialogMessageBox("'" + textBox.Text + "' is not a valid data label.", this);
                    messageBox.Message.Icon = MessageBoxImage.Warning;
                    messageBox.Message.Problem = "Data labels must begin with a letter, followed only by letters, numbers, and '_'.";
                    messageBox.Message.Result = "We will replace all dissallowed characters with an 'X'.";
                    messageBox.Message.Hint = "Start your label with a letter. Then use any combination of letters, numbers, and '_'.";
                    messageBox.ShowDialog();

                    textBox.Text = replacementDataLabel;
                }
            }

            // Check to see if its a reserved word
            foreach (string sqlKeyword in EditorConstant.ReservedSqlKeywords)
            {
                if (String.Equals(sqlKeyword, dataLabel, StringComparison.OrdinalIgnoreCase))
                {
                    DialogMessageBox messageBox = new DialogMessageBox("'" + textBox.Text + "' is not a valid data label.", this);
                    messageBox.Message.Icon = MessageBoxImage.Warning;
                    messageBox.Message.Problem = "Data labels cannot match the reserved words.";
                    messageBox.Message.Result = "We will add an '_' suffix to this Data Label to make it differ from the reserved word";
                    messageBox.Message.Hint = "Avoid the reserved words listed below. Start your label with a letter. Then use any combination of letters, numbers, and '_'." + Environment.NewLine;
                    foreach (string keyword in EditorConstant.ReservedSqlKeywords)
                    {
                        messageBox.Message.Hint += keyword + " ";
                    }
                    messageBox.ShowDialog();

                    textBox.Text += "_";
                    break;
                }
            }
        }

        /// <summary>
        /// Greys out cells as defined by logic. 
        /// This is to visually show the user uneditable cells, and informs events about whether a cell can be edited.
        /// This is called after row are added/moved/deleted to update the colors. 
        /// This also disables checkboxes that cannot be edited. Disabling checkboxes does not effect row interactions.
        /// </summary>
        private void UpdateCellColors()
        {
            MyTrace.MethodName("UpdateCellColors");
            for (int rowIndex = 0; rowIndex < this.TemplateDataGrid.Items.Count; rowIndex++)
            {
                // In order for ItemContainerGenerator to work, we need to set the TemplateGrid in the XAML to VirtualizingStackPanel.IsVirtualizing="False"
                // Alternately, we could just do the following, which may be more efficient for large grids (which we normally don't have)
                // this.TemplateDataGrid.UpdateLayout();
                // this.TemplateDataGrid.ScrollIntoView(rowIndex + 1);
                DataGridRow row = (DataGridRow)this.TemplateDataGrid.ItemContainerGenerator.ContainerFromIndex(rowIndex);
                if (row == null)
                {
                    return;
                }

                DataGridCellsPresenter presenter = GetVisualChild<DataGridCellsPresenter>(row);
                for (int column = 0; column < this.TemplateDataGrid.Columns.Count; column++)
                {
                    // The following attributes should always be editable.
                    // Note that this is hardwired to the header names in the xaml file, so this could break if that ever changes.. should probably do this so it works no matter what the header text is of the table
                    string columnHeader = (string)this.TemplateDataGrid.Columns[column].Header;
                    if ((columnHeader == EditorConstant.Control.Width) || (columnHeader == Constants.Control.Visible) || (columnHeader == Constants.Control.Label) || (columnHeader == Constants.Control.Tooltip))
                    {
                        continue;
                    }

                    // The following attributes should NOT be editable.
                    DataGridCell cell = (DataGridCell)presenter.ItemContainerGenerator.ContainerFromIndex(column);
                    ControlRow control = new ControlRow(((DataRowView)this.TemplateDataGrid.Items[rowIndex]).Row);

                    // more constants to access checkbox columns and combobox columns.
                    // the sortmemberpath is include, not the sort name, so we are accessing by head, which may change.
                    string controlType = control.Type;
                    string sortMemberPath = this.TemplateDataGrid.Columns[column].SortMemberPath;
                    if (String.Equals(sortMemberPath, Constants.DatabaseColumn.ID, StringComparison.OrdinalIgnoreCase)  ||
                        String.Equals(sortMemberPath, Constants.Control.ControlOrder, StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(sortMemberPath, Constants.Control.SpreadsheetOrder, StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(sortMemberPath, Constants.Control.Type, StringComparison.OrdinalIgnoreCase) ||
                        (controlType == Constants.DatabaseColumn.Date) ||
                        (controlType == Constants.Control.DeleteFlag) ||
                        (controlType == Constants.DatabaseColumn.File) ||
                        (controlType == Constants.DatabaseColumn.Folder) ||
                        ((controlType == Constants.DatabaseColumn.ImageQuality) && (columnHeader == Constants.Control.Copyable)) ||
                        ((controlType == Constants.DatabaseColumn.ImageQuality) && (columnHeader == EditorConstant.Control.DataLabel)) ||
                        ((controlType == Constants.DatabaseColumn.ImageQuality) && (columnHeader == Constants.Control.List)) ||
                        ((controlType == Constants.DatabaseColumn.ImageQuality) && (sortMemberPath == Constants.Control.DefaultValue)) ||
                        (controlType == Constants.DatabaseColumn.RelativePath) ||
                        (controlType == Constants.DatabaseColumn.Time) ||
                        ((controlType == Constants.Control.Counter) && (columnHeader == Constants.Control.List)) ||
                        ((controlType == Constants.Control.Flag) && (columnHeader == Constants.Control.List)) ||
                        ((controlType == Constants.Control.Note) && (columnHeader == Constants.Control.List)))
                    {
                        cell.Background = EditorConstant.NotEditableCellColor;
                        cell.Foreground = Brushes.Gray;

                        // if cell has a checkbox, also disable it.
                        var cp = cell.Content as ContentPresenter;
                        if (cp != null)
                        {
                            var checkbox = cp.ContentTemplate.FindName("CheckBox", cp) as CheckBox;
                            if (checkbox != null)
                            {
                                checkbox.IsEnabled = false;
                            }
                            else if ((controlType == Constants.DatabaseColumn.ImageQuality) && TemplateDataGrid.Columns[column].Header.Equals("List"))
                            {
                                cell.IsEnabled = false; // Don't let users edit the ImageQuality menu
                            }
                        }
                    }
                    else
                    {
                        cell.ClearValue(DataGridCell.BackgroundProperty); // otherwise when scrolling cells offscreen get colored randomly
                        ContentPresenter cellContent = cell.Content as ContentPresenter;

                        // if cell has a checkbox, enable it.
                        if (cellContent != null)
                        {
                            CheckBox checkbox = cellContent.ContentTemplate.FindName("CheckBox", cellContent) as CheckBox;
                            if (checkbox != null)
                            {
                                checkbox.IsEnabled = true;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Updates colors when the Layout changes.
        /// </summary>
        private void TemplateDataGrid_LayoutUpdated(object sender, EventArgs e)
        {
            MyTrace.MethodName("TemplateDataGrid_LayoutUpdated");
            this.UpdateCellColors();
        }

        /// <summary>
        /// Used in this code to get the child of a DataGridRows, DataGridCellsPresenter. This can be used to get the DataGridCell.
        /// WPF does not make it easy to get to the actual cells.
        /// </summary>
        // Code from: http://techiethings.blogspot.com/2010/05/get-wpf-datagrid-row-and-cell.html
        private static T GetVisualChild<T>(Visual parent) where T : Visual
        {
            T child = default(T);
            int numVisuals = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < numVisuals; i++)
            {
                Visual v = (Visual)VisualTreeHelper.GetChild(parent, i);
                child = v as T;
                if (child == null)
                {
                    child = GetVisualChild<T>(v);
                }
                if (child != null)
                {
                    break;
                }
            }
            return child;
        }
        #endregion Cell Editing / Coloring Listeners and Methods

        #region Other menu related items
        /// <summary>
        /// Update the list of recent databases displayed under File -> Recent Databases.
        /// </summary>
        private void MenuItemRecentTemplates_Refresh()
        {
            this.MenuItemRecentTemplates.IsEnabled = this.mostRecentTemplates.Count > 0;
            this.MenuItemRecentTemplates.Items.Clear();

            int index = 1;
            foreach (string recentTemplatePath in this.mostRecentTemplates)
            {
                MenuItem recentImageSetItem = new MenuItem();
                recentImageSetItem.Click += this.MenuItemRecentTemplate_Click;
                recentImageSetItem.Header = String.Format("_{0} {1}", index, recentTemplatePath);
                recentImageSetItem.ToolTip = recentTemplatePath;
                this.MenuItemRecentTemplates.Items.Add(recentImageSetItem);
                ++index;
            }
        }

        private void ShowDataLabelRequirementsDialog()
        {
            DialogMessageBox messageBox = new DialogMessageBox("Data Labels can only contain letters, numbers and '_'", this);
            messageBox.Message.Icon = MessageBoxImage.Warning;
            messageBox.Message.Problem = "Data labels must begin with a letter, followed only by letters, numbers, and '_'.";
            messageBox.Message.Result = "We will automatically ignore other characters, including spaces";
            messageBox.Message.Hint = "Start your label with a letter. Then use any combination of letters, numbers, and '_'.";
            messageBox.ShowDialog();
        }
        #endregion

        #region SpreadsheetAppearance
        private void GenerateSpreadsheet()
        {
            List<ControlRow> controlsInSpreadsheetOrder = this.templateDatabase.TemplateTable.OrderBy(control => control.SpreadsheetOrder).ToList();
            this.dgSpreadsheet.Columns.Clear();
            foreach (ControlRow control in controlsInSpreadsheetOrder)
            {
                DataGridTextColumn column = new DataGridTextColumn();
                string dataLabel = control.DataLabel;
                if (String.IsNullOrEmpty(dataLabel))
                {
                    Debug.Assert(false, "Database constructors should guarantee data labels are not null.");
                }
                else
                {
                    column.Header = dataLabel;
                    this.dgSpreadsheet.Columns.Add(column);
                }
            }
        }

        private void OnSpreadsheetOrderChanged(object sender, DataGridColumnEventArgs e)
        {
            DataGrid dataGrid = (DataGrid)sender;
            Dictionary<string, long> spreadsheetOrderByDataLabel = new Dictionary<string, long>();
            for (int control = 0; control < dataGrid.Columns.Count; control++)
            {
                string dataLabelFromColumnHeader = dataGrid.Columns[control].Header.ToString();
                long newSpreadsheetOrder = dataGrid.Columns[control].DisplayIndex + 1;
                spreadsheetOrderByDataLabel.Add(dataLabelFromColumnHeader, newSpreadsheetOrder);
            }

            this.templateDatabase.UpdateDisplayOrder(Constants.Control.SpreadsheetOrder, spreadsheetOrderByDataLabel);
        }
        #endregion

        #region Dragging and Dropping of Controls to Reorder them
        private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.Source == this.controlsPanel)
            {
            }
            else
            {
                this.isMouseDown = true;
                this.startPoint = e.GetPosition(this.controlsPanel);
            }
        }

        private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                this.isMouseDown = false;
                this.isMouseDragging = false;
                if (!(this.realMouseDragSource == null))
                {
                    this.realMouseDragSource.ReleaseMouseCapture();
                }
            }
            catch
            {
            }
        }

        private void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (this.isMouseDown)
            {
                if ((this.isMouseDragging == false) && 
                    ((Math.Abs(e.GetPosition(this.controlsPanel).X - this.startPoint.X) > SystemParameters.MinimumHorizontalDragDistance) || 
                     (Math.Abs(e.GetPosition(this.controlsPanel).Y - this.startPoint.Y) > SystemParameters.MinimumVerticalDragDistance)))
                {
                    this.isMouseDragging = true;
                    this.realMouseDragSource = e.Source as UIElement;
                    this.realMouseDragSource.CaptureMouse();
                    DragDrop.DoDragDrop(this.dummyMouseDragSource, new DataObject("UIElement", e.Source, true), DragDropEffects.Move);
                }
            }
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("UIElement"))
            {
                e.Effects = DragDropEffects.Move;
            }
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("UIElement"))
            {
                UIElement dropTarget = e.Source as UIElement;
                int control = 0;
                int dropTargetIndex = -1;
                foreach (UIElement element in this.controlsPanel.Children)
                {
                    if (element.Equals(dropTarget))
                    {
                        dropTargetIndex = control;
                        break;
                    }
                    else
                    {
                        // Check if its a stack panel, and if so check to see if its children are the drop target
                        StackPanel stackPanel = element as StackPanel;
                        if (stackPanel != null)
                        {
                            // Check the children...
                            foreach (UIElement subelement in stackPanel.Children)
                            {
                                if (subelement.Equals(dropTarget))
                                {
                                    dropTargetIndex = control;
                                    break;
                                }
                            }
                        }
                    }
                    control++;
                }
                if (dropTargetIndex != -1)
                {
                    StackPanel tsp = this.realMouseDragSource as StackPanel;
                    if (tsp == null)
                    {
                        StackPanel parent = FindVisualParent<StackPanel>(this.realMouseDragSource);
                        this.realMouseDragSource = parent;
                    }
                    this.controlsPanel.Children.Remove(this.realMouseDragSource);
                    this.controlsPanel.Children.Insert(dropTargetIndex, this.realMouseDragSource);
                    this.OnControlOrderChanged();
                }

                this.isMouseDown = false;
                this.isMouseDragging = false;
                this.realMouseDragSource.ReleaseMouseCapture();
            }
        }

        private static T FindVisualParent<T>(UIElement element) where T : UIElement
        {
            UIElement parent = element;
            while (parent != null)
            {
                T correctlyTyped = parent as T;
                if (correctlyTyped != null)
                {
                    return correctlyTyped;
                }
                parent = VisualTreeHelper.GetParent(parent) as UIElement;
            }
            return null;
        }

        private void OnControlOrderChanged()
        {
            Dictionary<string, long> newControlOrderByDataLabel = new Dictionary<string, long>();
            long controlOrder = 1;
            foreach (UIElement element in this.controlsPanel.Children)
            {
                StackPanel stackPanel = element as StackPanel;
                if (stackPanel == null)
                {
                    continue;
                }
                newControlOrderByDataLabel.Add((string)stackPanel.Tag, controlOrder);
                controlOrder++;
            }

            this.templateDatabase.UpdateDisplayOrder(Constants.Control.ControlOrder, newControlOrderByDataLabel);
            EditorControls.Generate(this, this.controlsPanel, this.templateDatabase.TemplateTable); // A contorted to make sure the controls panel updates itself
        }
        #endregion

        #region Template Drag and Drop
        private void HelpDocument_Drop(object sender, DragEventArgs dropEvent)
        {
            string templateDatabaseFilePath;
            if (Utilities.IsSingleTemplateFileDrag(dropEvent, out templateDatabaseFilePath))
            {
                this.TrySaveDatabaseBackupFile();
                this.InitializeDataGrid(templateDatabaseFilePath);
            }
        }

        private void HelpDocument_PreviewDrag(object sender, DragEventArgs dragEvent)
        {
            Utilities.OnHelpDocumentPreviewDrag(dragEvent);
        }
        #endregion

        #region Backups
        /// <summary>
        /// Helper method that creates a database backup. Used when performing file menu options.
        /// </summary>
        private bool TrySaveDatabaseBackupFile()
        {
            if (this.templateDatabase == null)
            {
                return false;
            }

            return FileBackup.TryCreateBackups(Path.GetDirectoryName(this.templateDatabase.FilePath), Path.GetFileName(this.templateDatabase.FilePath));
        }
        #endregion 
    }
}