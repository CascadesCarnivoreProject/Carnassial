using Carnassial.Controls;
using Carnassial.Data;
using Carnassial.Database;
using Carnassial.Dialog;
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
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using MessageBox = Carnassial.Dialog.MessageBox;

namespace Carnassial.Editor
{
    public partial class EditorWindow : Window
    {
        // state tracking
        private bool templateDataGridBeingUpdatedByCode;
        private bool templateDataGridCellEditForcedByCode;
        private EditorUserRegistrySettings userSettings;

        // These variables support the drag/drop of controls
        private UIElement dummyMouseDragSource;
        private bool isMouseDown;
        private bool isMouseDragging;
        private Point mouseDownStartPosition;

        // database where the template is stored
        private TemplateDatabase templateDatabase;

        /// <summary>
        /// Starts the UI.
        /// </summary>
        public EditorWindow()
        {
            AppDomain.CurrentDomain.UnhandledException += this.OnUnhandledException;
            this.InitializeComponent();
            this.Title = EditorConstant.MainWindowBaseTitle;
            Utilities.TryFitWindowInWorkingArea(this);

            // Abort if some of the required dependencies are missing
            if (Dependencies.AreRequiredBinariesPresent(EditorConstant.ApplicationName, Assembly.GetExecutingAssembly()) == false)
            {
                Dependencies.ShowMissingBinariesDialog(EditorConstant.ApplicationName);
                Application.Current.Shutdown();
            }

            this.dummyMouseDragSource = new UIElement();
            this.templateDataGridBeingUpdatedByCode = false;
            this.templateDataGridCellEditForcedByCode = false;

            this.MenuOptionsShowAllColumns_Click(this.MenuOptionsShowAllColumns, null);

            // Recall state from prior sessions
            this.userSettings = new EditorUserRegistrySettings();

            // populate the most recent databases list
            this.MenuFileRecentTemplates_Refresh();

            this.TutorialLink.NavigateUri = CarnassialConfigurationSettings.GetTutorialBrowserAddress();
            this.TutorialLink.ToolTip = this.TutorialLink.NavigateUri.AbsoluteUri;
        }

        /// <summary>
        /// Adds a row to the table. The row type is decided by the button tags.
        /// Default values are set for the added row, differing depending on type.
        /// </summary>
        private void AddControlButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            string controlType = button.Tag.ToString();

            this.templateDataGridBeingUpdatedByCode = true;

            this.templateDatabase.AddUserDefinedControl(controlType);
            this.TemplateDataGrid.DataContext = this.templateDatabase.Controls;
            this.TemplateDataGrid.ScrollIntoView(this.TemplateDataGrid.Items[this.TemplateDataGrid.Items.Count - 1]);

            this.RebuildControlPreview();
            this.SynchronizeSpreadsheetOrderPreview();

            this.templateDataGridBeingUpdatedByCode = false;
        }

        private void ControlsPanel_DragDrop(object sender, DragEventArgs e)
        {
            DataEntryControl controlBeingDragged;
            if (this.DataEntryControls.TryFindDataEntryControl(this.mouseDownStartPosition, out controlBeingDragged))
            {
                DataEntryControl dropTarget;
                if (this.DataEntryControls.TryFindDataEntryControl(e.GetPosition(this.DataEntryControls), out dropTarget))
                {
                    Dictionary<string, long> newControlOrderByDataLabel = new Dictionary<string, long>();
                    long controlOrder = 1;
                    foreach (ControlRow control in this.templateDatabase.Controls)
                    {
                        if (control.DataLabel == controlBeingDragged.DataLabel)
                        {
                            continue;
                        }
                        if (control.DataLabel == dropTarget.DataLabel)
                        {
                            newControlOrderByDataLabel.Add(controlBeingDragged.DataLabel, controlOrder);
                            ++controlOrder;
                        }
                        newControlOrderByDataLabel.Add(control.DataLabel, controlOrder);
                        ++controlOrder;
                    }

                    this.templateDatabase.UpdateDisplayOrder(Constant.Control.ControlOrder, newControlOrderByDataLabel);
                    this.RebuildControlPreview();
                }
            }

            this.isMouseDown = false;
            this.isMouseDragging = false;
            this.ControlsPanel.ReleaseMouseCapture();
        }

        private void ControlsPanel_OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (this.isMouseDown)
            {
                Point currentMousePosition = e.GetPosition(this.ControlsPanel);
                if ((this.isMouseDragging == false) &&
                    ((Math.Abs(currentMousePosition.X - this.mouseDownStartPosition.X) > SystemParameters.MinimumHorizontalDragDistance) ||
                     (Math.Abs(currentMousePosition.Y - this.mouseDownStartPosition.Y) > SystemParameters.MinimumVerticalDragDistance)))
                {
                    this.isMouseDragging = true;
                    this.ControlsPanel.CaptureMouse();
                    DragDrop.DoDragDrop(this.dummyMouseDragSource, new DataObject("UIElement", e.Source, true), DragDropEffects.Move);
                }
            }
        }

        private void ControlsPanel_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.isMouseDown = true;
            this.mouseDownStartPosition = e.GetPosition(this.ControlsPanel);
        }

        private void ControlsPanel_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            this.isMouseDown = false;
            this.isMouseDragging = false;
            this.ControlsPanel.ReleaseMouseCapture();
        }

        // raise a dialog box that lets the user edit the list of choices or note control default autocompletions
        private void DefineList_Click(object sender, RoutedEventArgs e)
        {
            // the button's tag is the ControlOrder of the row the button is in; find the control with the same control order
            Button button = (Button)sender;
            ControlRow choiceOrNote = this.templateDatabase.Controls.FirstOrDefault(control => control.ControlOrder.ToString().Equals(button.Tag.ToString()));
            Debug.Assert(choiceOrNote != null, String.Format("Control named {0} not found.", button.Tag));

            EditChoiceList choiceListDialog = new EditChoiceList(button, choiceOrNote.GetChoices(), this);
            bool? result = choiceListDialog.ShowDialog();
            if (result == true)
            {
                choiceOrNote.SetChoices(choiceListDialog.Choices);
                this.SyncControlToDatabaseAndPreviews(choiceOrNote);
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

        private void InitializeDataGrid(string templateDatabasePath)
        {
            // create a new template file if one does not exist or load a DB file if there is one.
            bool templateLoaded = TemplateDatabase.TryCreateOrOpen(templateDatabasePath, out this.templateDatabase);
            if (templateLoaded)
            {
                this.templateDatabase.BindToEditorDataGrid(this.TemplateDataGrid, this.TemplateDataGrid_RowChanged);

                // populate controls interface in UX
                this.RebuildControlPreview();
                this.SynchronizeSpreadsheetOrderPreview();
                this.Title = Path.GetFileName(this.templateDatabase.FilePath) + " - " + EditorConstant.MainWindowBaseTitle;

                this.userSettings.MostRecentTemplates.SetMostRecent(templateDatabasePath);
            }
            else
            {
                this.Title = EditorConstant.MainWindowBaseTitle;

                // notify the user the template couldn't be loaded
                MessageBox messageBox = new MessageBox("Carnassial could not load the template.", this);
                messageBox.Message.Problem = "Carnassial could not load " + Path.GetFileName(templateDatabasePath) + Environment.NewLine;
                messageBox.Message.Reason = "\u2022 The template was created with the Timelapse template editor instead of the Carnassial editor." + Environment.NewLine;
                messageBox.Message.Reason = "\u2022 The template may be corrupted or somehow otherwise invalid.";
                messageBox.Message.Solution = "You may have to recreate the template or use another copy of it (if you have one).";
                messageBox.Message.Result = "Carnassial won't do anything. You can try to select another template file.";
                messageBox.Message.Hint = "If the template can't be opened in a SQLite database editor the file is corrupt and you'll have to recreate it.";
                messageBox.Message.StatusImage = MessageBoxImage.Error;
                messageBox.ShowDialog();
            }

            // update UI states
            this.AddCounterButton.IsEnabled = templateLoaded;
            this.AddFixedChoiceButton.IsEnabled = templateLoaded;
            this.AddNoteButton.IsEnabled = templateLoaded;
            this.AddFlagButton.IsEnabled = templateLoaded;

            this.MenuFileNewTemplate.IsEnabled = templateLoaded;
            this.MenuFileOpenTemplate.IsEnabled = !templateLoaded;
            this.MenuFileRecentTemplates.IsEnabled = !templateLoaded;
            this.MenuOptions.IsEnabled = templateLoaded;
            this.MenuView.IsEnabled = templateLoaded;

            this.Tabs.SelectedIndex = templateLoaded ? 1 : 0;
        }

        private void Instructions_Drop(object sender, DragEventArgs dropEvent)
        {
            string templateDatabaseFilePath;
            if (Utilities.IsSingleTemplateFileDrag(dropEvent, out templateDatabaseFilePath))
            {
                this.InitializeDataGrid(templateDatabaseFilePath);
            }
        }

        private void Instructions_PreviewDrag(object sender, DragEventArgs dragEvent)
        {
            Utilities.OnInstructionsPreviewDrag(dragEvent);
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
        /// Exits the application.
        /// </summary>
        private void MenuFileExit_Click(object sender, RoutedEventArgs e)
        {
            // flush any pending edits to the currently selected row
            this.TemplateDataGrid.CommitEdit();
            Application.Current.Shutdown();
        }

        /// <summary>
        /// Creates a new template of a user chosen name in a user chosen location.
        /// </summary>
        private void MenuFileNewTemplate_Click(object sender, RoutedEventArgs e)
        {
            this.TemplateDataGrid.CommitEdit(); // to apply edits that the enter key was not pressed

            // Configure save file dialog box
            SaveFileDialog newTemplateFilePathDialog = new SaveFileDialog();
            newTemplateFilePathDialog.FileName = Path.GetFileNameWithoutExtension(Constant.File.DefaultTemplateDatabaseFileName); // Default file name without the extension
            newTemplateFilePathDialog.DefaultExt = Constant.File.TemplateFileExtension; // Default file extension
            newTemplateFilePathDialog.Filter = "Database Files (" + Constant.File.TemplateFileExtension + ")|*" + Constant.File.TemplateFileExtension; // Filter files by extension 
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
        /// Open an existing template.
        /// </summary>
        private void MenuFileOpenTemplate_Click(object sender, RoutedEventArgs e)
        {
            this.TemplateDataGrid.CommitEdit(); // to save any edits that the enter key was not pressed

            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.FileName = Path.GetFileNameWithoutExtension(Constant.File.DefaultTemplateDatabaseFileName); // Default file name without the extension
            openFileDialog.DefaultExt = Constant.File.TemplateFileExtension; // Default file extension
            openFileDialog.Filter = "Database Files (" + Constant.File.TemplateFileExtension + ")|*" + Constant.File.TemplateFileExtension; // Filter files by extension 
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
        private void MenuFileRecentTemplate_Click(object sender, RoutedEventArgs e)
        {
            string recentTemplatePath = (string)((MenuItem)sender).ToolTip;
            this.InitializeDataGrid(recentTemplatePath);
        }

        /// <summary>
        /// Update the list of recent databases displayed under File -> Recent Databases.
        /// </summary>
        private void MenuFileRecentTemplates_Refresh()
        {
            this.MenuFileRecentTemplates.IsEnabled = this.userSettings.MostRecentTemplates.Count > 0;
            this.MenuFileRecentTemplates.Items.Clear();

            int index = 1;
            foreach (string recentTemplatePath in this.userSettings.MostRecentTemplates)
            {
                MenuItem recentImageSetItem = new MenuItem();
                recentImageSetItem.Click += this.MenuFileRecentTemplate_Click;
                recentImageSetItem.Header = String.Format("_{0} {1}", index, recentTemplatePath);
                recentImageSetItem.ToolTip = recentTemplatePath;
                this.MenuFileRecentTemplates.Items.Add(recentImageSetItem);
                ++index;
            }
        }

        private void MenuHelpAbout_Click(object sender, RoutedEventArgs e)
        {
            AboutEditor about = new AboutEditor(this);
            if ((about.ShowDialog() == true) && about.MostRecentCheckForUpdate.HasValue)
            {
                this.userSettings.MostRecentCheckForUpdates = about.MostRecentCheckForUpdate.Value;
            }
        }

        private void MenuOptionsAdvancedImageSetOptions_Click(object sender, RoutedEventArgs e)
        {
            AdvancedImageSetOptions advancedImageSetOptions = new AdvancedImageSetOptions(this.templateDatabase, this);
            advancedImageSetOptions.ShowDialog();
        }

        /// <summary>
        /// Depending on the menu's checkbox state, show all columns or hide selected columns
        /// </summary>
        private void MenuOptionsShowAllColumns_Click(object sender, RoutedEventArgs e)
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
        private void MenuViewInspectMetadata_Click(object sender, RoutedEventArgs e)
        {
            InspectMetadata inspectMetadata = new InspectMetadata(this.templateDatabase.FilePath, this);
            inspectMetadata.ShowDialog();
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

            this.templateDatabase.UpdateDisplayOrder(Constant.Control.SpreadsheetOrder, spreadsheetOrderByDataLabel);
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Utilities.ShowExceptionReportingDialog("The template editor needs to close.", e, this);
        }

        private void RebuildControlPreview()
        {
            this.DataEntryControls.CreateControls(this.templateDatabase, null, (string dataLabel) => { return this.templateDatabase.FindControl(dataLabel).GetChoices(); });
        }

        private void ShowDataLabelRequirementsDialog()
        {
            MessageBox messageBox = new MessageBox("Data Labels can only contain letters, numbers and '_'.", this);
            messageBox.Message.StatusImage = MessageBoxImage.Warning;
            messageBox.Message.Problem = "Data labels must begin with a letter, followed only by letters, numbers, and '_'.";
            messageBox.Message.Result = "We will automatically ignore other characters, including spaces";
            messageBox.Message.Hint = "Start your label with a letter. Then use any combination of letters, numbers, and '_'.";
            messageBox.ShowDialog();
        }

        private void SyncControlToDatabaseAndPreviews(ControlRow control)
        {
            this.templateDataGridBeingUpdatedByCode = true;
            this.templateDatabase.SyncControlToDatabase(control);
            this.templateDataGridBeingUpdatedByCode = false;

            DataEntryControl controlPreview;
            if ((this.DataEntryControls.ControlsByDataLabel.TryGetValue(control.DataLabel, out controlPreview) == false) ||
                (control.Visible == false))
            {
                // rebuild the controls preview if data label or visibility changed
                // DataEntryControls does not create UI objects for controls which aren't visible, so the TryGetValue() check above triggers when a control is
                // becoming visible.  The Visible == false check handles the case where the control is becoming invisible.
                // Control order is not checked here as the basic comparison
                //   this.DataEntryControls.Controls.IndexOf(controlPreview) != control.ControlOrder
                // has to be adjusted for controls which aren't visible and for the DateTime and UtcOffset ControlRows often mapping to the same DataEntryDateTime.
                // This is complex and not needed as drop events call RebuildControlPreview() rather than this function.
                this.RebuildControlPreview();
            }
            else
            {
                // if the control's data label isn't changing just update preview for control
                // This is a UX responsiveness optimization for simple changes.
                // control.ControlOrder changes are handled elsewhere
                // changes to control.Copyable have no effect on the preview
                // control.DataLabel changes are handled above
                // control.DefaultValue is used to initialize the preview but changes not propagated as the user may have configured the preview differently
                // Default value changes are included in choice updates.
                // control.ID is immutable
                if (control.Label != controlPreview.Label)
                {
                    controlPreview.Label = control.Label;
                }
                // control.List is handled via choice comparison
                List<string> controlChoices = control.GetChoices();
                List<string> previewChoices = controlPreview.GetChoices();
                bool choicesNeedSynchronization = controlChoices.Count != previewChoices.Count;
                if (choicesNeedSynchronization == false)
                {
                    List<string> allChoices = controlChoices.Union(previewChoices).ToList();
                    choicesNeedSynchronization = allChoices.Count != previewChoices.Count;
                }
                if (choicesNeedSynchronization)
                {
                    controlPreview.SetChoices(controlChoices);
                }
                // control.SpreadsheetOrder is handled below
                if (control.Tooltip != controlPreview.LabelTooltip)
                {
                    controlPreview.LabelTooltip = control.Tooltip;
                }
                // control.Type is immutable
                // control.Visible changes are handled above
                if (control.Width != controlPreview.ContentWidth)
                {
                    controlPreview.ContentWidth = control.Width;
                }
            }

            this.SynchronizeSpreadsheetOrderPreview();
        }

        // incrementally update spreadsheet order preview
        // Incremental updates are used to limit WPF UI tree rebuilds and improve responsiveness.
        private void SynchronizeSpreadsheetOrderPreview()
        {
            List<ControlRow> controlsInSpreadsheetOrder = this.templateDatabase.Controls.OrderBy(control => control.SpreadsheetOrder).ToList();

            // synchronize number of preview columns if number of controls changed
            while (this.SpreadsheetOrderPreview.Columns.Count < controlsInSpreadsheetOrder.Count)
            {
                this.SpreadsheetOrderPreview.Columns.Add(new DataGridTextColumn());
            }
            while (this.SpreadsheetOrderPreview.Columns.Count > controlsInSpreadsheetOrder.Count)
            {
                this.SpreadsheetOrderPreview.Columns.RemoveAt(this.SpreadsheetOrderPreview.Columns.Count - 1);
            }

            // update existing column headers if needed
            for (int controlIndex = 0; controlIndex < controlsInSpreadsheetOrder.Count; ++controlIndex)
            {
                ControlRow control = controlsInSpreadsheetOrder[controlIndex];
                Debug.Assert(String.IsNullOrEmpty(control.DataLabel) == false, "Database constructors should guarantee data labels are not null.");

                DataGridColumn column = this.SpreadsheetOrderPreview.Columns[controlIndex];
                if ((column.Header == null) || (String.Equals((string)column.Header, control.DataLabel, StringComparison.Ordinal) == false))
                {
                    column.Header = control.DataLabel;
                }
            }
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
            if (Constant.Control.StandardControls.Contains(control.Type))
            {
                // standard controls cannot be removed
                return;
            }

            this.templateDataGridBeingUpdatedByCode = true;
            this.templateDatabase.RemoveUserDefinedControl(new ControlRow(selectedRowView.Row));

            // update the control panel so it reflects the current values in the database
            this.RebuildControlPreview();
            this.SynchronizeSpreadsheetOrderPreview();

            this.templateDataGridBeingUpdatedByCode = false;
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
            if ((e.EditAction == DataGridEditAction.Commit) && (this.templateDataGridCellEditForcedByCode == false))
            {
                // flush changes in each cell as user exits it to database and trigger a redraw to update control visibility
                // Generating a call to TemplateDataGrid_RowChanged() here isn't the most elegant approach but it avoids creating separate commit pathways
                // for cell and row edits.  Data grids don't particularly support immediate data binding (though they can be coerced into it by providing
                // unknown template cell types at the expense of other state management issues) so a change in cell focus is, within their model, needed to
                // trigger an update of the control layout preview for Width and Visible at the data grid level.
                this.templateDataGridCellEditForcedByCode = true;
                this.TemplateDataGrid.CommitEdit(DataGridEditingUnit.Row, true);
                this.templateDataGridCellEditForcedByCode = false;
            }
        }

        /// <summary>
        /// Updates colors when rows are added, moved, or deleted.
        /// </summary>
        private void TemplateDataGrid_LayoutUpdated(object sender, EventArgs e)
        {
            // Greys out cells as defined by logic and also disables checkboxes which cannot be edited.
            // This is to show the user uneditable cells.
            for (int rowIndex = 0; rowIndex < this.TemplateDataGrid.Items.Count; rowIndex++)
            {
                // for ItemContainerGenerator to work the DataGrid must have VirtualizingStackPanel.IsVirtualizing="False"
                // the following may be more efficient for large grids but is not used as more than dozen or so controls is unlikely
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

                    string columnHeader = (string)this.TemplateDataGrid.Columns[column].Header;
                    ControlRow control = new ControlRow(((DataRowView)this.TemplateDataGrid.Items[rowIndex]).Row);
                    string controlType = control.Type;
                    bool disableCell = false;
                    if ((columnHeader == Constant.DatabaseColumn.ID) ||
                        (columnHeader == Constant.Control.ControlOrder) ||
                        (columnHeader == Constant.Control.SpreadsheetOrder) ||
                        (columnHeader == Constant.Control.Type))
                    {
                        // these columns are never editable
                        disableCell = true;
                        // these columns are always editable
                        //   Constant.Control.Label
                        //   Constant.Control.Tooltip
                        //   Constant.Control.Visible
                        //   EditorConstant.ColumnHeader.Width
                    }
                    else if ((controlType == Constant.DatabaseColumn.DateTime) ||
                             (controlType == Constant.DatabaseColumn.File) ||
                             (controlType == Constant.DatabaseColumn.RelativePath))
                    {
                        // these standard controls have no editable properties other than width
                        disableCell = columnHeader != Constant.Control.Width;
                    }
                    else if ((controlType == Constant.DatabaseColumn.DeleteFlag) ||
                             (controlType == Constant.DatabaseColumn.ImageQuality) ||
                             (controlType == Constant.DatabaseColumn.UtcOffset))
                    {
                        // standard controls whose copyable, visible, and width can be changed
                        disableCell = (columnHeader != Constant.Control.Copyable) && 
                                      (columnHeader != Constant.Control.Visible) && 
                                      (columnHeader != Constant.Control.Width);
                    }
                    else if ((controlType == Constant.Control.Counter) ||
                             (controlType == Constant.Control.Flag))
                    {
                        // all properties are editable except list
                        disableCell = columnHeader == Constant.Control.List;
                        // for notes and choices all properties including list are editable
                    }

                    if (disableCell)
                    {
                        cell.Background = EditorConstant.NotEditableCellColor;
                        cell.Foreground = Brushes.Gray;
                        cell.IsEnabled = false;

                        // if cell has a checkbox disable it
                        CheckBox checkBox;
                        if (cell.TryGetCheckBox(out checkBox))
                        {
                            checkBox.IsEnabled = false;
                        }
                    }
                    else if (columnHeader == Constant.Control.Visible)
                    {
                        // set up check boxes in the visible column for immediate data binding so user sees the control preview update promptly
                        // The LayoutUpdated event fires many times so a guard is required to set the callbacks only once.
                        CheckBox checkBox;
                        if (cell.TryGetCheckBox(out checkBox))
                        {
                            if (checkBox.Tag == null)
                            {
                                checkBox.Checked += this.TemplateDataGridVisible_Changed;
                                checkBox.Unchecked += this.TemplateDataGridVisible_Changed;
                                checkBox.Tag = rowIndex;
                            }
                        }
                        else
                        {
                            Debug.Fail("Could not find check box associated with Visible column for immediate data binding.");
                        }
                    }
                    else if (columnHeader == Constant.Control.Width)
                    {
                        // set up text boxes in the width column for immediate data binding so user sees the control preview update promptly
                        // A guard is required as above.  When the DataGrid is first instantiated TextBlocks are used for the cell content; these are
                        // changed to TextBoxes when the user initiates an edit.
                        TextBox textBox;
                        if (cell.TryGetTextBox(out textBox))
                        {
                            if (textBox.Tag == null)
                            {
                                textBox.TextChanged += this.TemplateDataGridWidth_Changed;
                                textBox.Tag = rowIndex;
                            }
                        }
                    }
                }
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
                        case Constant.Control.Counter:
                            e.Handled = !Utilities.IsDigits(e.Text);
                            break;
                        case Constant.Control.Flag:
                            // only allow t/f and translate to true/false
                            if (e.Text == "t" || e.Text == "T")
                            {
                                control.DefaultValue = Boolean.TrueString;
                                this.SyncControlToDatabaseAndPreviews(control);
                            }
                            else if (e.Text == "f" || e.Text == "F")
                            {
                                control.DefaultValue = Boolean.FalseString;
                                this.SyncControlToDatabaseAndPreviews(control);
                            }
                            e.Handled = true;
                            break;
                        case Constant.Control.FixedChoice:
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

        /// <summary>
        /// Whenever a row changes save the database, which also updates the grid colors.
        /// </summary>
        private void TemplateDataGrid_RowChanged(object sender, DataRowChangeEventArgs e)
        {
            if (!this.templateDataGridBeingUpdatedByCode)
            {
                this.SyncControlToDatabaseAndPreviews(new ControlRow(e.Row));
            }
        }

        private void TemplateDataGridVisible_Changed(object sender, RoutedEventArgs e)
        {
            CheckBox checkBox = (CheckBox)sender;
            int rowIndex = (int)checkBox.Tag;
            if (checkBox.IsChecked.HasValue)
            {
                // immediately propagate change in check to underlying data table; this triggers a call to TemplateDataGrid_RowChanged() so the user
                // sees the control layout preview update in response to their click on the check box
                this.templateDatabase.Controls[rowIndex].Visible = checkBox.IsChecked.Value;
            }
        }

        private void TemplateDataGridWidth_Changed(object sender, RoutedEventArgs e)
        {
            TextBox textBox = (TextBox)sender;
            int rowIndex = (int)textBox.Tag;
            int newWidth;
            if (Int32.TryParse(textBox.Text, out newWidth))
            {
                this.templateDatabase.Controls[rowIndex].Width = newWidth;
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
            this.RemoveControlButton.IsEnabled = !Constant.Control.StandardControls.Contains(control.Type);
        }

        private void TutorialLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            if (e.Uri != null)
            {
                Process.Start(e.Uri.AbsoluteUri);
                e.Handled = true;
            }
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
                messageBox.Message.StatusImage = MessageBoxImage.Warning;
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

                    MessageBox messageBox = new MessageBox("Data Labels must be unique.", this);
                    messageBox.Message.StatusImage = MessageBoxImage.Warning;
                    messageBox.Message.Problem = "'" + textBox.Text + "' is not a valid Data Label, as you have already used it in another row.";
                    messageBox.Message.Result = "We will automatically create a unique Data Label for you by adding a number to its end.";
                    messageBox.Message.Hint = "You can create your own unique name for this Data Label. Start your label with a letter. Then use any combination of letters, numbers, and '_'.";
                    messageBox.ShowDialog();
                    textBox.Text = this.templateDatabase.GetNextUniqueDataLabel("DataLabel");
                    break;
                }
            }

            // Check to see if the label (if its not empty, which it shouldn't be) has any illegal characters.
            // Much of this is redundant characters are also checked as they are typed.  However, the first check has not been performed.
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
                    messageBox.Message.StatusImage = MessageBoxImage.Warning;
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
                    messageBox.Message.StatusImage = MessageBoxImage.Warning;
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

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // check for updates
            if (DateTime.UtcNow - this.userSettings.MostRecentCheckForUpdates > Constant.CheckForUpdateInterval)
            {
                Uri latestVersionAddress = CarnassialConfigurationSettings.GetLatestReleaseApiAddress();
                if (latestVersionAddress == null)
                {
                    return;
                }

                GithubReleaseClient updater = new GithubReleaseClient(EditorConstant.ApplicationName, latestVersionAddress);
                updater.TryGetAndParseRelease(false);
                this.userSettings.MostRecentCheckForUpdates = DateTime.UtcNow;
            }

            // if a file was passed on the command line, try to open it
            // args[0] is the .exe
            string[] args = Environment.GetCommandLineArgs();
            if (args != null && args.Length > 1)
            {
                string filePath = args[1];
                string fileExtension = Path.GetExtension(filePath);
                if (String.Equals(fileExtension, Constant.File.TemplateFileExtension, StringComparison.OrdinalIgnoreCase))
                {
                    this.InitializeDataGrid(filePath);
                }
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // apply any pending edits
            this.TemplateDataGrid.CommitEdit();
            // persist state to registry
            this.userSettings.WriteToRegistry();
        }
    }
}