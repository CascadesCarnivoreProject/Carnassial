using Carnassial.Database;
using Carnassial.Editor.Dialog;
using Carnassial.Editor.Util;
using Carnassial.Github;
using Carnassial.Util;
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
using MessageBox = Carnassial.Dialog.MessageBox;

namespace Carnassial.Editor
{
    public partial class EditorWindow : Window
    {
        // database where the template is stored
        private TemplateDatabase templateDatabase;

        // state tracking
        private EditorControls controls;
        private bool dataGridBeingUpdatedByCode;
        private EditorUserRegistrySettings userSettings;

        // These variables support the drag/drop of controls
        private bool isMouseDown;
        private bool isMouseDragging;
        private Point mouseDownStartPosition;
        private UIElement realMouseDragSource;
        private UIElement dummyMouseDragSource;

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

            this.controls = new EditorControls();
            this.dummyMouseDragSource = new UIElement();
            this.dataGridBeingUpdatedByCode = false;

            this.ShowAllColumnsMenuItem_Click(this.ShowAllColumns, null);

            // Recall state from prior sessions
            this.userSettings = new EditorUserRegistrySettings();

            // populate the most recent databases list
            this.MenuItemRecentTemplates_Refresh();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Uri latestVersionAddress = CarnassialConfigurationSettings.GetLatestReleaseAddress();
            if (latestVersionAddress == null)
            {
                return;
            }

            GithubReleaseClient updater = new GithubReleaseClient(Constants.ApplicationName, latestVersionAddress);
            // TODO: remove temporary disable
            // updater.TryGetAndParseRelease(false);
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // apply any pending edits
            this.TemplateDataGrid.CommitEdit();
            // persist state to registry
            this.userSettings.WriteToRegistry();
        }

        /// <summary>
        /// Creates a new database file of a user chosen name in a user chosen location.
        /// </summary>
        private void NewFileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            this.TemplateDataGrid.CommitEdit(); // to apply edits that the enter key was not pressed

            // Configure save file dialog box
            SaveFileDialog newTemplateFilePathDialog = new SaveFileDialog();
            newTemplateFilePathDialog.FileName = Path.GetFileNameWithoutExtension(Constants.File.DefaultTemplateDatabaseFileName); // Default file name without the extension
            newTemplateFilePathDialog.DefaultExt = Constants.File.TemplateDatabaseFileExtension; // Default file extension
            newTemplateFilePathDialog.Filter = "Database Files (" + Constants.File.TemplateDatabaseFileExtension + ")|*" + Constants.File.TemplateDatabaseFileExtension; // Filter files by extension 
            newTemplateFilePathDialog.Title = "Select Location to Save New Template File";

            // Show save file dialog box
            Nullable<bool> result = newTemplateFilePathDialog.ShowDialog();

            // Process save file dialog box results 
            if (result == true)
            {
                // Overwrite the file if it exists
                if (File.Exists(newTemplateFilePathDialog.FileName))
                {
                    FileBackup.TryCreateBackup(newTemplateFilePathDialog.FileName);
                    File.Delete(newTemplateFilePathDialog.FileName);
                }

                // Open document 
                this.InitializeDataGrid(newTemplateFilePathDialog.FileName);
            }
        }

        /// <summary>
        /// Opens an existing database file.
        /// </summary>
        private void OpenFileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            this.TemplateDataGrid.CommitEdit(); // to save any edits that the enter key was not pressed

            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.FileName = Path.GetFileNameWithoutExtension(Constants.File.DefaultTemplateDatabaseFileName); // Default file name without the extension
            openFileDialog.DefaultExt = Constants.File.TemplateDatabaseFileExtension; // Default file extension
            openFileDialog.Filter = "Database Files (" + Constants.File.TemplateDatabaseFileExtension + ")|*" + Constants.File.TemplateDatabaseFileExtension; // Filter files by extension 
            openFileDialog.Title = "Select an Existing Template File to Open";

            // Show open file dialog box
            Nullable<bool> result = openFileDialog.ShowDialog();

            // Process open file dialog box results 
            if (result == true)
            {
                // Open document 
                this.InitializeDataGrid(openFileDialog.FileName);
            }
        }

        // Opern a recently used template
        private void MenuItemRecentTemplate_Click(object sender, RoutedEventArgs e)
        {
            string recentTemplatePath = (string)((MenuItem)sender).ToolTip;
            this.InitializeDataGrid(recentTemplatePath);
        }

        /// <summary>
        /// Exits the application.
        /// </summary>
        private void ExitFileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            this.TemplateDataGrid.CommitEdit(); // to save any edits that the enter key was not pressed
            Application.Current.Shutdown();
        }

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

            Visibility visibility = mi.IsChecked ? Visibility.Visible : Visibility.Collapsed;
            foreach (DataGridColumn column in this.TemplateDataGrid.Columns)
            {
                if (column.Header.Equals(EditorConstant.ColumnHeader.ID) || 
                    column.Header.Equals(EditorConstant.ColumnHeader.ControlOrder) || 
                    column.Header.Equals(EditorConstant.ColumnHeader.SpreadsheetOrder))
                {
                    column.Visibility = visibility;
                }
            }
        }

        /// <summary>
        /// Show the dialog that allows a user to inspect image metadata
        /// </summary>
        private void MenuItemInspectImageMetadata_Click(object sender, RoutedEventArgs e)
        {
            InspectMetadata inspectMetadata = new InspectMetadata(this.templateDatabase.FilePath, this);
            inspectMetadata.ShowDialog();
        }

        private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            AboutEditor about = new AboutEditor(this);
            about.ShowDialog();
        }

        /// <summary>
        /// Given a database file path,create a new DB file if one does not exist, or load a DB file if there is one.
        /// After a DB file is loaded, the table is extracted and loaded a DataTable for binding to the DataGrid.
        /// Some listeners are added to the DataTable, and the DataTable is bound. The add row buttons are enabled.
        /// </summary>
        /// <param name="templateDatabaseFilePath">The path of the DB file created or loaded</param>
        private void InitializeDataGrid(string templateDatabaseFilePath)
        {
            // Create a new DB file if one does not exist, or load a DB file if there is one.
            this.templateDatabase = TemplateDatabase.CreateOrOpen(templateDatabaseFilePath);

            // Map the data table to the data grid, and create a callback executed whenever the datatable row changes
            this.templateDatabase.BindToEditorDataGrid(this.TemplateDataGrid, this.TemplateDataTable_RowChanged);

            // Update the user interface specified by the contents of the table
            this.controls.Generate(this, this.ControlsPanel, this.templateDatabase.Controls);
            this.GenerateSpreadsheet();

            // update UI for having a .tdb loaded
            this.AddCounterButton.IsEnabled = true;
            this.AddFixedChoiceButton.IsEnabled = true;
            this.AddNoteButton.IsEnabled = true;
            this.AddFlagButton.IsEnabled = true;

            this.NewFileMenuItem.IsEnabled = false;
            this.OpenFileMenuItem.IsEnabled = false;
            this.ViewMenu.IsEnabled = true;

            this.TemplatePane.IsActive = true;
            this.Title = Path.GetFileName(this.templateDatabase.FilePath) + " - " + EditorConstant.MainWindowBaseTitle;

            // update state
            // disable the recent templates list rather than call this.MenuItemRecentTemplates_Refresh
            this.userSettings.MostRecentTemplates.SetMostRecent(templateDatabaseFilePath);
            this.MenuItemRecentTemplates.IsEnabled = false;
        }

        /// <summary>
        /// Updates a given control in the database with the current state of the DataGrid. 
        /// </summary>
        private void SyncControlToDatabase(ControlRow control)
        {
            this.dataGridBeingUpdatedByCode = true;

            this.templateDatabase.SyncControlToDatabase(control);
            this.controls.Generate(this, this.ControlsPanel, this.templateDatabase.Controls);
            this.GenerateSpreadsheet();

            this.dataGridBeingUpdatedByCode = false;
        }

        /// <summary>
        /// Whenever a row changes save the database, which also updates the grid colors.
        /// </summary>
        private void TemplateDataTable_RowChanged(object sender, DataRowChangeEventArgs e)
        {
            if (!this.dataGridBeingUpdatedByCode)
            {
                this.SyncControlToDatabase(new ControlRow(e.Row));
            }
        }

        /// <summary>
        /// enable or disable the remove control button as appropriate
        /// </summary>
        private void TemplateDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DataRowView selectedRowView = this.TemplateDataGrid.SelectedItem as DataRowView;
            if (selectedRowView == null)
            {
                this.RemoveControlButton.IsEnabled = false;
                return;
            }

            ControlRow control = new ControlRow(selectedRowView.Row);
            this.RemoveControlButton.IsEnabled = !Constants.Control.StandardTypes.Contains(control.Type);
        }

        /// <summary>
        /// Adds a row to the table. The row type is decided by the button tags.
        /// Default values are set for the added row, differing depending on type.
        /// </summary>
        private void AddControlButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            string controlType = button.Tag.ToString();

            this.dataGridBeingUpdatedByCode = true;

            this.templateDatabase.AddUserDefinedControl(controlType);
            this.TemplateDataGrid.DataContext = this.templateDatabase.Controls;
            this.TemplateDataGrid.ScrollIntoView(this.TemplateDataGrid.Items[this.TemplateDataGrid.Items.Count - 1]);

            this.controls.Generate(this, this.ControlsPanel, this.templateDatabase.Controls);
            this.GenerateSpreadsheet();
            this.OnControlOrderChanged();

            this.dataGridBeingUpdatedByCode = false;
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

            this.dataGridBeingUpdatedByCode = true;
            this.templateDatabase.RemoveUserDefinedControl(new ControlRow(selectedRowView.Row));

            // update the control panel so it reflects the current values in the database
            this.controls.Generate(this, this.ControlsPanel, this.templateDatabase.Controls);
            this.GenerateSpreadsheet();

            this.dataGridBeingUpdatedByCode = false;
        }

        // When the  choice list button is clicked, raise a dialog box that lets the user edit the list of choices
        private void ChoiceListButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;

            // The button tag holds the Control Order of the row the button is in, not the ID.
            // So we have to search through the rows to find the one with the correct control order
            // and retrieve / set the ItemList menu in that row.
            ControlRow choiceControl = this.templateDatabase.Controls.FirstOrDefault(control => control.ControlOrder.ToString().Equals(button.Tag.ToString()));
            Debug.Assert(choiceControl != null, String.Format("Control named {0} not found.", button.Tag));

            EditChoiceList choiceListDialog = new EditChoiceList(button, choiceControl.GetChoices(), this);
            bool? result = choiceListDialog.ShowDialog();
            if (result == true)
            {
                choiceControl.SetChoices(choiceListDialog.Choices);
                this.SyncControlToDatabase(choiceControl);
            }
        }

        private void TemplateDataGrid_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            DataGridCell currentCell;
            DataGridRow currentRow;
            if ((this.TryGetCurrentCell(out currentCell, out currentRow) == false) || currentCell.Background.Equals(EditorConstant.NotEditableCellColor))
            {
                e.Handled = true;
                return;
            }

            switch ((string)this.TemplateDataGrid.CurrentColumn.Header)
            {
                // EditorConstant.Control.ControlOrder is not editable
                case EditorConstant.ColumnHeader.DataLabel:
                    if (String.IsNullOrEmpty(e.Text) == false)
                    {
                        // one key is provided by the event at a time during typing, multiple keys can be pasted in a single event
                        // it's not known where in the data label the input occurs, so full validation is postponed to ValidateDataLabel()
                        if (this.IsValidDataLabelCharacters(e.Text) == false)
                        {
                            this.ShowDataLabelRequirementsDialog();
                            e.Handled = true;
                        }
                    }
                    break;
                case EditorConstant.ColumnHeader.DefaultValue:
                    ControlRow control = new ControlRow((currentRow.Item as DataRowView).Row);
                    switch (control.Type)
                    {
                        case Constants.Control.Counter:
                            e.Handled = !Utilities.IsDigits(e.Text);
                            break;
                        case Constants.Control.Flag:
                            // Only allow t/f and translate to true/false
                            if (e.Text == "t" || e.Text == "T")
                            {
                                control.DefaultValue = Constants.Boolean.True;
                                this.SyncControlToDatabase(control);
                            }
                            else if (e.Text == "f" || e.Text == "F")
                            {
                                control.DefaultValue = Constants.Boolean.False;
                                this.SyncControlToDatabase(control);
                            }
                            e.Handled = true;
                            break;
                        case Constants.Control.FixedChoice:
                            // no restrictions for now
                            // the default value should be limited to one of the choices defined, however
                            break;
                        default:
                            // no restrictions on note controls
                            break;
                    }
                    break;
                // EditorConstant.Control.ID is not editable
                // EditorConstant.Control.SpreadsheetOrder is not editable
                // Type is not editable
                case EditorConstant.ColumnHeader.Width:
                    // only allow digits in widths as they must be parseable as integers
                    e.Handled = !Utilities.IsDigits(e.Text);
                    break;
                default:
                    // no restrictions on copyable, label, tooltip, or visibile columns
                    break;
            }
        }

        private bool IsValidDataLabel(string dataLabel)
        {
            if ((dataLabel == null) || (dataLabel.Length < 1))
            {
                return false;
            }
            if (Char.IsLetter(dataLabel[0]) == false)
            {
                return false;
            }

            if (dataLabel.Length > 1)
            {
                return this.IsValidDataLabelCharacters(dataLabel.Substring(1));
            }
            return true;
        }

        private bool IsValidDataLabelCharacters(string dataLabelFragment)
        {
            if (String.IsNullOrEmpty(dataLabelFragment))
            {
                return false;
            }

            foreach (char character in dataLabelFragment)
            {
                if ((Char.IsLetterOrDigit(character) == false) && (character != '_'))
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
        private void TemplateDataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            DataGridCell currentCell;
            DataGridRow currentRow;
            if (this.TryGetCurrentCell(out currentCell, out currentRow) == false)
            {
                return;
            }

            if (currentCell.Background.Equals(EditorConstant.NotEditableCellColor))
            {
                currentCell.IsEnabled = false;
                this.TemplateDataGrid.CancelEdit();
            }
        }

        /// <summary>
        /// After cell editing ends (prematurely or no), re-enable disabled cells.
        /// See TemplateDataGrid_BeginningEdit for full explanation.
        /// </summary>
        private void TemplateDataGrid_CurrentCellChanged(object sender, EventArgs e)
        {
            DataGridCell currentCell;
            DataGridRow currentRow;
            if (this.TryGetCurrentCell(out currentCell, out currentRow) == false)
            {
                return;
            }

            if (currentCell.Background.Equals(EditorConstant.NotEditableCellColor))
            {
                currentCell.IsEnabled = true;
            }
        }

        private void TemplateDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.Column.Header.Equals(EditorConstant.ColumnHeader.DataLabel))
            {
                this.ValidateDataLabel(e);
            }
        }

        /// <summary>
        /// Updates colors when the Layout changes.
        /// </summary>
        private void TemplateDataGrid_LayoutUpdated(object sender, EventArgs e)
        {
            // Greys out cells as defined by logic. 
            // This is to show the user uneditable cells, and informs events about whether a cell can be edited.
            // This is called after row are added/moved/deleted to update the colors. 
            // This also disables checkboxes which cannot be edited. Disabling checkboxes does not affect row interactions.
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

                // grid cells are editable by default
                // disable cells which should not be editable
                DataGridCellsPresenter presenter = GetVisualChild<DataGridCellsPresenter>(row);
                for (int column = 0; column < this.TemplateDataGrid.Columns.Count; column++)
                {
                    DataGridCell cell = (DataGridCell)presenter.ItemContainerGenerator.ContainerFromIndex(column);
                    if (cell == null)
                    {
                        // cell will be null for columns with Visibility = Hidden
                        continue;
                    }

                    ControlRow control = new ControlRow(((DataRowView)this.TemplateDataGrid.Items[rowIndex]).Row);
                    string controlType = control.Type;

                    string columnHeader = (string)this.TemplateDataGrid.Columns[column].Header;
                    if ((columnHeader == Constants.Control.Label) ||
                        (columnHeader == Constants.Control.Tooltip) ||
                        (columnHeader == Constants.Control.Visible) ||
                        (columnHeader == EditorConstant.ColumnHeader.Width))
                    {
                        continue;
                    }

                    // The following attributes should NOT be editable.
                    ContentPresenter cellContent = cell.Content as ContentPresenter;
                    string sortMemberPath = this.TemplateDataGrid.Columns[column].SortMemberPath;
                    if (String.Equals(sortMemberPath, Constants.DatabaseColumn.ID, StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(sortMemberPath, Constants.Control.ControlOrder, StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(sortMemberPath, Constants.Control.SpreadsheetOrder, StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(sortMemberPath, Constants.Control.Type, StringComparison.OrdinalIgnoreCase) ||
                        (controlType == Constants.DatabaseColumn.DateTime) ||
                        (controlType == Constants.DatabaseColumn.DeleteFlag) ||
                        (controlType == Constants.DatabaseColumn.File) ||
                        (controlType == Constants.DatabaseColumn.Folder) ||
                        ((controlType == Constants.DatabaseColumn.ImageQuality) && (columnHeader == Constants.Control.Copyable)) ||
                        ((controlType == Constants.DatabaseColumn.ImageQuality) && (columnHeader == EditorConstant.ColumnHeader.DataLabel)) ||
                        ((controlType == Constants.DatabaseColumn.ImageQuality) && (columnHeader == Constants.Control.List)) ||
                        ((controlType == Constants.DatabaseColumn.ImageQuality) && (sortMemberPath == Constants.Control.DefaultValue)) ||
                        (controlType == Constants.DatabaseColumn.RelativePath) ||
                        (controlType == Constants.DatabaseColumn.UtcOffset) ||
                        ((controlType == Constants.Control.Counter) && (columnHeader == Constants.Control.List)) ||
                        ((controlType == Constants.Control.Flag) && (columnHeader == Constants.Control.List)) ||
                        ((controlType == Constants.Control.Note) && (columnHeader == Constants.Control.List)))
                    {
                        cell.Background = EditorConstant.NotEditableCellColor;
                        cell.Foreground = Brushes.Gray;

                        // if cell has a checkbox, also disable it.
                        if (cellContent != null)
                        {
                            CheckBox checkbox = cellContent.ContentTemplate.FindName("CheckBox", cellContent) as CheckBox;
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

        /// <summary>
        /// Update the list of recent databases displayed under File -> Recent Databases.
        /// </summary>
        private void MenuItemRecentTemplates_Refresh()
        {
            this.MenuItemRecentTemplates.IsEnabled = this.userSettings.MostRecentTemplates.Count > 0;
            this.MenuItemRecentTemplates.Items.Clear();

            int index = 1;
            foreach (string recentTemplatePath in this.userSettings.MostRecentTemplates)
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
            MessageBox messageBox = new MessageBox("Data Labels can only contain letters, numbers and '_'", this);
            messageBox.Message.Icon = MessageBoxImage.Warning;
            messageBox.Message.Problem = "Data labels must begin with a letter, followed only by letters, numbers, and '_'.";
            messageBox.Message.Result = "We will automatically ignore other characters, including spaces";
            messageBox.Message.Hint = "Start your label with a letter. Then use any combination of letters, numbers, and '_'.";
            messageBox.ShowDialog();
        }

        private void GenerateSpreadsheet()
        {
            List<ControlRow> controlsInSpreadsheetOrder = this.templateDatabase.Controls.OrderBy(control => control.SpreadsheetOrder).ToList();
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

        private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.Source != this.ControlsPanel)
            {
                this.isMouseDown = true;
                this.mouseDownStartPosition = e.GetPosition(this.ControlsPanel);
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
                Point currentMousePosition = e.GetPosition(this.ControlsPanel);
                if ((this.isMouseDragging == false) && 
                    ((Math.Abs(currentMousePosition.X - this.mouseDownStartPosition.X) > SystemParameters.MinimumHorizontalDragDistance) || 
                     (Math.Abs(currentMousePosition.Y - this.mouseDownStartPosition.Y) > SystemParameters.MinimumVerticalDragDistance)))
                {
                    this.isMouseDragging = true;
                    this.realMouseDragSource = e.Source as UIElement;
                    this.realMouseDragSource.CaptureMouse();
                    DragDrop.DoDragDrop(this.dummyMouseDragSource, new DataObject("UIElement", e.Source, true), DragDropEffects.Move);
                }
            }
        }

        private void ControlsPanel_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("UIElement"))
            {
                e.Effects = DragDropEffects.Move;
            }
        }

        private void ControlsPanel_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("UIElement"))
            {
                UIElement dropTarget = e.Source as UIElement;
                int control = 0;
                int dropTargetIndex = -1;
                foreach (UIElement element in this.ControlsPanel.Children)
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
                    this.ControlsPanel.Children.Remove(this.realMouseDragSource);
                    this.ControlsPanel.Children.Insert(dropTargetIndex, this.realMouseDragSource);
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
            foreach (UIElement element in this.ControlsPanel.Children)
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
            this.controls.Generate(this, this.ControlsPanel, this.templateDatabase.Controls); // A contorted to make sure the controls panel updates itself
        }

        private void HelpDocument_Drop(object sender, DragEventArgs dropEvent)
        {
            string templateDatabaseFilePath;
            if (Utilities.IsSingleTemplateFileDrag(dropEvent, out templateDatabaseFilePath))
            {
                this.InitializeDataGrid(templateDatabaseFilePath);
            }
        }

        private void HelpDocument_PreviewDrag(object sender, DragEventArgs dragEvent)
        {
            Utilities.OnHelpDocumentPreviewDrag(dragEvent);
        }

        private bool TryGetCurrentCell(out DataGridCell currentCell, out DataGridRow currentRow)
        {
            if ((this.TemplateDataGrid.SelectedIndex == -1) || (this.TemplateDataGrid.CurrentColumn == null))
            {
                currentCell = null;
                currentRow = null;
                return false;
            }

            currentRow = (DataGridRow)this.TemplateDataGrid.ItemContainerGenerator.ContainerFromIndex(this.TemplateDataGrid.SelectedIndex);
            DataGridCellsPresenter presenter = EditorWindow.GetVisualChild<DataGridCellsPresenter>(currentRow);
            currentCell = (DataGridCell)presenter.ItemContainerGenerator.ContainerFromIndex(this.TemplateDataGrid.CurrentColumn.DisplayIndex);
            return currentCell != null;
        }

        private void ValidateDataLabel(DataGridCellEditEndingEventArgs e)
        {
            // Check to see if the data label entered is a reserved word or if its a non-unique label
            TextBox textBox = e.EditingElement as TextBox;
            string dataLabel = textBox.Text;

            // Check to see if the data label is empty. If it is, generate a unique data label and warn the user
            if (this.IsValidDataLabel(dataLabel) == false)
            {
                MessageBox messageBox = new MessageBox("Data label isn't valid.", this);
                messageBox.Message.Icon = MessageBoxImage.Warning;
                messageBox.Message.Problem = "Data labels must begin with a letter, followed only by letters, numbers, and '_'.  They cannot be empty.";
                messageBox.Message.Result = "We will automatically create a uniquely named data label for you.";
                messageBox.Message.Hint = "You can create your own name for this data label. Start your label with a letter. Then use any combination of letters, numbers, and '_'.";
                messageBox.ShowDialog();
                textBox.Text = this.templateDatabase.GetNextUniqueDataLabel("DataLabel");
            }

            // Check to see if the data label is unique. If not, generate a unique data label and warn the user
            for (int row = 0; row < this.templateDatabase.Controls.RowCount; row++)
            {
                ControlRow control = this.templateDatabase.Controls[row];
                if (dataLabel.Equals(control.DataLabel))
                {
                    if (this.TemplateDataGrid.SelectedIndex == row)
                    {
                        continue; // Its the same row, so its the same key, so skip it
                    }

                    MessageBox messageBox = new MessageBox("Data Labels must be unique", this);
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

                    MessageBox messageBox = new MessageBox("'" + textBox.Text + "' is not a valid data label.", this);
                    messageBox.Message.Icon = MessageBoxImage.Warning;
                    messageBox.Message.Problem = "Data labels must begin with a letter, followed only by letters, numbers, and '_'.";
                    messageBox.Message.Result = "We replaced all dissallowed characters with an 'X': " + replacementDataLabel;
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
                    MessageBox messageBox = new MessageBox("'" + textBox.Text + "' is not a valid data label.", this);
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
    }
}