using Carnassial.Control;
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
using SaveFileDialog = System.Windows.Forms.SaveFileDialog;

namespace Carnassial.Editor
{
    public partial class EditorWindow : WindowWithSystemMenu, IDisposable
    {
        // state tracking
        private bool controlDataGridBeingUpdatedByCode;
        private bool controlDataGridCellEditForcedByCode;
        private bool disposed;
        private EditorUserRegistrySettings userSettings;

        // database where the controls and image set defaults are stored
        private TemplateDatabase templateDatabase;

        public EditorWindow()
        {
            AppDomain.CurrentDomain.UnhandledException += this.OnUnhandledException;
            this.InitializeComponent();
            this.AddCounterButton.Tag = ControlType.Counter;
            this.AddFixedChoiceButton.Tag = ControlType.FixedChoice;
            this.AddFlagButton.Tag = ControlType.Flag;
            this.AddNoteButton.Tag = ControlType.Note;
            this.DataEntryControls.AllowDrop = true;
            this.Title = EditorConstant.MainWindowBaseTitle;
            Utilities.TryFitWindowInWorkingArea(this);

            // abort if some of the required dependencies are missing
            if (Dependencies.AreRequiredBinariesPresent(EditorConstant.ApplicationName, Assembly.GetExecutingAssembly()) == false)
            {
                Dependencies.ShowMissingBinariesDialog(EditorConstant.ApplicationName);
                Application.Current.Shutdown();
            }

            this.controlDataGridBeingUpdatedByCode = false;
            this.controlDataGridCellEditForcedByCode = false;
            this.disposed = false;

            this.MenuOptionsShowAllColumns_Click(this.MenuOptionsShowAllColumns, null);

            // recall state from prior sessions
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
            ControlType controlType = (ControlType)button.Tag;

            this.controlDataGridBeingUpdatedByCode = true;

            this.templateDatabase.AppendUserDefinedControl(controlType);
            this.ControlDataGrid.DataContext = this.templateDatabase.Controls;
            this.ControlDataGrid.ScrollIntoView(this.ControlDataGrid.Items[this.ControlDataGrid.Items.Count - 1]);

            this.RebuildControlPreview();
            this.SynchronizeSpreadsheetOrderPreview();

            this.controlDataGridBeingUpdatedByCode = false;
        }

        private void ControlDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.Column.Header.Equals(EditorConstant.ColumnHeader.DataLabel))
            {
                this.ValidateDataLabel(e);
            }
            if ((e.EditAction == DataGridEditAction.Commit) && (this.controlDataGridCellEditForcedByCode == false))
            {
                // flush changes in each cell as user exits it to database and trigger a redraw to update control display
                // Generating a call to ControlDataGrid_RowEditEnding() isn't the most elegant approach but it avoids separate commit 
                // pathways for cell and row edits.  Since this is an ending event (rather than an ended devent, which DataGrid lacks)
                // first comitting the cell edit is necessary to flush the change to the ControlRow so that it's visible to the row 
                // edit ending handler.
                this.controlDataGridCellEditForcedByCode = true;
                this.ControlDataGrid.CommitEdit(DataGridEditingUnit.Cell, false);
                this.ControlDataGrid.CommitEdit(DataGridEditingUnit.Row, false);
                this.controlDataGridCellEditForcedByCode = false;
            }
        }

        private void ControlDataGridCopyable_Changed(object sender, RoutedEventArgs e)
        {
            CheckBox checkBox = (CheckBox)sender;
            int rowIndex = (int)checkBox.Tag;
            if (checkBox.IsChecked.HasValue)
            {
                int analysisLabelIndex = -1;
                for (int column = 0; column < this.ControlDataGrid.Columns.Count; ++column)
                {
                    string columnHeader = (string)this.ControlDataGrid.Columns[column].Header;
                    if (String.Equals(columnHeader, EditorConstant.ColumnHeader.AnalysisLabel, StringComparison.Ordinal))
                    {
                        analysisLabelIndex = column;
                        break;
                    }
                }
                Debug.Assert(analysisLabelIndex != -1, "Failed to find analysis label column.");

                DataGridRow row = (DataGridRow)this.ControlDataGrid.ItemContainerGenerator.ContainerFromIndex(rowIndex);
                DataGridCellsPresenter presenter = EditorWindow.GetVisualChild<DataGridCellsPresenter>(row);
                DataGridCell analysisLabelCell = (DataGridCell)presenter.ItemContainerGenerator.ContainerFromIndex(analysisLabelIndex);

                // immediately update enable/disable state of analysis label cell
                ControlRow control = this.templateDatabase.Controls[rowIndex];
                if (checkBox.IsChecked.Value)
                {
                    analysisLabelCell.IsEnabled = true;
                }
                else
                {
                    analysisLabelCell.IsEnabled = false;
                    control.AnalysisLabel = false;
                }
            }
        }

        /// <summary>
        /// Sets cell enable/disable and colors when rows are added, moved, or deleted.
        /// </summary>
        private void ControlDataGrid_LayoutUpdated(object sender, EventArgs e)
        {
            for (int rowIndex = 0; rowIndex < this.ControlDataGrid.Items.Count; rowIndex++)
            {
                // for ItemContainerGenerator to work the DataGrid must have VirtualizingStackPanel.IsVirtualizing="False"
                // the following may be more efficient for large grids but are not used as more than dozen or so controls is unlikely
                // this.ControlDataGrid.UpdateLayout();
                // this.ControlDataGrid.ScrollIntoView(rowIndex + 1);
                DataGridRow row = (DataGridRow)this.ControlDataGrid.ItemContainerGenerator.ContainerFromIndex(rowIndex);
                if (row == null)
                {
                    continue;
                }

                // grid cells are editable by default
                // disable cells which should not be editable
                ControlRow control = this.templateDatabase.Controls[rowIndex];
                DataGridCellsPresenter presenter = EditorWindow.GetVisualChild<DataGridCellsPresenter>(row);
                for (int column = 0; column < this.ControlDataGrid.Columns.Count; column++)
                {
                    DataGridCell cell = (DataGridCell)presenter.ItemContainerGenerator.ContainerFromIndex(column);
                    if (cell == null)
                    {
                        // cell will be null for columns with Visibility = Hidden
                        continue;
                    }

                    string columnHeader = (string)this.ControlDataGrid.Columns[column].Header;
                    bool disableCell = cell.ShouldDisable(control, columnHeader);
                    if (disableCell)
                    {
                        cell.IsEnabled = false;
                    }
                    else
                    {
                        cell.IsEnabled = true;

                        if (String.Equals(columnHeader, EditorConstant.ColumnHeader.MaxWidth, StringComparison.Ordinal))
                        {
                            // set up text boxes in the width column for immediate data binding so user sees the control preview 
                            // update promptly
                            // A guard is required as above.  When the DataGrid is first instantiated TextBlocks are used for the 
                            // cell content; these are changed to TextBoxes when the user initiates an edit. Therefore, it's OK if
                            // TryGetControl() returns false.
                            if (cell.TryGetControl(out TextBox textBox))
                            {
                                if (textBox.Tag == null)
                                {
                                    textBox.TextChanged += this.ControlDataGrid_WidthChanged;
                                    textBox.Tag = rowIndex;
                                }
                            }
                        }
                        else if (String.Equals(columnHeader, Constant.ControlColumn.Visible, StringComparison.Ordinal))
                        {
                            // set up check boxes in the visible column for immediate data binding so user sees the control preview
                            // update promptly
                            // The LayoutUpdated event fires many times so a guard is required to set the callbacks only once.
                            if (cell.TryGetControl(out CheckBox checkBox))
                            {
                                if (checkBox.Tag == null)
                                {
                                    checkBox.Checked += this.ControlDataGrid_VisibleChanged;
                                    checkBox.Unchecked += this.ControlDataGrid_VisibleChanged;
                                    checkBox.Tag = rowIndex;
                                }
                            }
                            else
                            {
                                Debug.Fail("Could not find check box associated with Visible column for immediate data binding.");
                            }
                        }
                    }

                    if (String.Equals(columnHeader, Constant.ControlColumn.Copyable, StringComparison.Ordinal))
                    {
                        if (cell.TryGetControl(out CheckBox checkBox))
                        {
                            if (checkBox.Tag == null)
                            {
                                checkBox.Checked += this.ControlDataGridCopyable_Changed;
                                checkBox.Unchecked += this.ControlDataGridCopyable_Changed;
                                checkBox.Tag = rowIndex;
                            }
                        }
                        else
                        {
                            Debug.Fail("Could not find check box associated with Copyable column for immediate data binding.");
                        }
                    }
                }
            }
        }

        private void ControlDataGrid_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            switch ((string)this.ControlDataGrid.CurrentColumn.Header)
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
                    if (this.ControlDataGrid.SelectedIndex < 0)
                    {
                        e.Handled = true;
                        return;
                    }
                    DataGridRow currentRow = (DataGridRow)this.ControlDataGrid.ItemContainerGenerator.ContainerFromIndex(this.ControlDataGrid.SelectedIndex);
                    ControlRow control = (ControlRow)currentRow.Item;
                    switch (control.Type)
                    {
                        case ControlType.Counter:
                            e.Handled = !Utilities.IsDigits(e.Text);
                            break;
                        case ControlType.Flag:
                            if (String.Equals(e.Text, Constant.Sql.FalseString, StringComparison.Ordinal) ||
                                String.Equals(e.Text, Constant.Sql.TrueString, StringComparison.Ordinal))
                            {
                                control.DefaultValue = e.Text;
                                this.SyncControlToDatabaseAndPreviews(control);
                            }
                            e.Handled = true;
                            break;
                        case ControlType.FixedChoice:
                            // no restrictions for now
                            // The default value should eventially be limited to one of the well known values defined, however.
                            break;
                        case ControlType.DateTime:
                        case ControlType.Note:
                        case ControlType.UtcOffset:
                            // no restrictions on note or time controls
                            break;
                        default:
                            throw new NotSupportedException(String.Format("Unhandled control type {0}.", control.Type));
                    }
                    break;
                // EditorConstant.Control.ID is not editable
                // EditorConstant.Control.SpreadsheetOrder is not editable
                // Type is not editable
                case EditorConstant.ColumnHeader.MaxWidth:
                    // only allow digits in widths as they must be parseable as integers
                    e.Handled = !Utilities.IsDigits(e.Text);
                    break;
                default:
                    // no restrictions on analysis label, copyable, label, tooltip, or visible columns
                    break;
            }
        }

        private void ControlDataGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            if (!this.controlDataGridBeingUpdatedByCode)
            {
                this.SyncControlToDatabaseAndPreviews((ControlRow)e.Row.Item);
            }
        }

        /// <summary>
        /// enable or disable the remove control button as appropriate
        /// </summary>
        private void ControlDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ControlRow control = (ControlRow)this.ControlDataGrid.SelectedItem;
            if (control == null)
            {
                this.RemoveControlButton.IsEnabled = false;
                return;
            }

            this.RemoveControlButton.IsEnabled = !Constant.Control.StandardControls.Contains(control.DataLabel, StringComparer.Ordinal);
        }

        private void ControlDataGrid_VisibleChanged(object sender, RoutedEventArgs e)
        {
            CheckBox checkBox = (CheckBox)sender;
            int rowIndex = (int)checkBox.Tag;
            if (checkBox.IsChecked.HasValue)
            {
                // immediately propagate change in check to underlying data table so user sees control appear or disappear
                // Data grids don't particularly support immediate data binding (though they can be coerced into it by providing 
                // unknown template cell types at the expense of other state management issues) so a change in cell focus is,
                // within their model, needed to trigger an update of the control layout preview for Width and Visible at the data 
                // grid level.
                ControlRow control = this.templateDatabase.Controls[rowIndex];
                control.Visible = checkBox.IsChecked.Value;
                this.SyncControlToDatabaseAndPreviews(control);
            }
        }

        private void ControlDataGrid_WidthChanged(object sender, RoutedEventArgs e)
        {
            TextBox textBox = (TextBox)sender;
            int rowIndex = (int)textBox.Tag;
            if (Int32.TryParse(textBox.Text, out int newWidth))
            {
                // immediately propagate change in width to underlying data table so user sees control width adjust as they type
                ControlRow control = this.templateDatabase.Controls[rowIndex];
                control.MaxWidth = newWidth;
                this.SyncControlToDatabaseAndPreviews(control);
            }
        }

        private void DataEntryControls_ControlOrderChangedByDragDrop(DataEntryControl controlBeingDragged, DataEntryControl dropTarget)
        {
            Dictionary<string, int> newControlOrderByDataLabel = new Dictionary<string, int>(StringComparer.Ordinal);
            int controlOrder = 1;
            foreach (ControlRow control in this.templateDatabase.Controls)
            {
                if (String.Equals(control.DataLabel, controlBeingDragged.DataLabel, StringComparison.Ordinal))
                {
                    continue;
                }

                newControlOrderByDataLabel.Add(control.DataLabel, controlOrder);
                ++controlOrder;
                if (String.Equals(control.DataLabel, dropTarget.DataLabel, StringComparison.Ordinal))
                {
                    newControlOrderByDataLabel.Add(controlBeingDragged.DataLabel, controlOrder);
                    ++controlOrder;
                }
            }

            this.templateDatabase.UpdateDisplayOrder(Constant.ControlColumn.ControlOrder, newControlOrderByDataLabel);
            this.RebuildControlPreview();
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing && (this.templateDatabase != null))
            {
                this.templateDatabase.Dispose();
            }
            this.disposed = true;
        }

        // raise a dialog box that lets the user edit the list of choices or note control default autocompletions
        private void EditWellKnownValues_Click(object sender, RoutedEventArgs e)
        {
            // the button's tag is bound to the ControlOrder of the ControlRow the button is associated with in xaml; find the 
            // control with the same control order
            Button button = (Button)sender;
            ControlRow choiceOrNote = this.templateDatabase.Controls.FirstOrDefault(control => control.ControlOrder == (int)button.Tag);
            Debug.Assert(choiceOrNote != null, String.Format("Control with tag {0} not found.", button.Tag));

            EditWellKnownValues wellKnownValuesDialog = new EditWellKnownValues(button, choiceOrNote.GetWellKnownValues(), this);
            if (wellKnownValuesDialog.ShowDialog() == true)
            {
                choiceOrNote.SetWellKnownValues(wellKnownValuesDialog.Values);
                this.SyncControlToDatabaseAndPreviews(choiceOrNote);
            }
        }

        private void EnableOrDisableMenusAndControls(bool templateLoaded)
        {
            this.AddCounterButton.IsEnabled = templateLoaded;
            this.AddFixedChoiceButton.IsEnabled = templateLoaded;
            this.AddNoteButton.IsEnabled = templateLoaded;
            this.AddFlagButton.IsEnabled = templateLoaded;

            this.MenuFileCloseTemplate.IsEnabled = templateLoaded;
            this.MenuFileNewTemplate.IsEnabled = templateLoaded;
            this.MenuFileOpenTemplate.IsEnabled = !templateLoaded;
            this.MenuFileRecentTemplates.IsEnabled = !templateLoaded;
            this.MenuOptions.IsEnabled = templateLoaded;
            this.MenuView.IsEnabled = templateLoaded;

            if (templateLoaded)
            {
                this.ControlDataGrid.ItemsSource = this.templateDatabase.Controls;

                this.Tabs.SelectedIndex = 1;
                this.Title = Path.GetFileName(this.templateDatabase.FilePath) + " - " + EditorConstant.MainWindowBaseTitle;
                this.userSettings.MostRecentTemplates.SetMostRecent(this.templateDatabase.FilePath);
                this.MenuFileRecentTemplates_Refresh();

                // populate controls interface in UX
                this.RebuildControlPreview();
                this.SynchronizeSpreadsheetOrderPreview();
            }
            else
            {
                this.ControlDataGrid.ItemsSource = null;
                this.DataEntryControls.Clear();
                this.SpreadsheetOrderPreview.Columns.Clear();

                this.Tabs.SelectedIndex = 0;
                this.Title = EditorConstant.MainWindowBaseTitle;
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
                    child = EditorWindow.GetVisualChild<T>(v);
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
            // flush any pending edit which might exist
            this.ControlDataGrid.CommitEdit();

            // create a new template file if one does not exist or load a DB file if there is one.
            bool templateLoaded = TemplateDatabase.TryCreateOrOpen(templateDatabasePath, out this.templateDatabase);
            if (templateLoaded == false)
            {
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

            // update UI
            this.EnableOrDisableMenusAndControls(templateLoaded);
        }

        private void Instructions_Drop(object sender, DragEventArgs dropEvent)
        {
            if (Utilities.IsSingleTemplateFileDrag(dropEvent, out string templateDatabaseFilePath))
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

        private void MenuFileCloseTemplate_Click(object sender, RoutedEventArgs e)
        {
            // flush any pending edits to the currently selected row
            this.ControlDataGrid.CommitEdit();

            this.EnableOrDisableMenusAndControls(false);
        }

        /// <summary>
        /// Exits the application.
        /// </summary>
        private void MenuFileExit_Click(object sender, RoutedEventArgs e)
        {
            this.MenuFileCloseTemplate_Click(sender, e);
            Application.Current.Shutdown();
        }

        /// <summary>
        /// Creates a new template of a user chosen name in a user chosen location.
        /// </summary>
        private void MenuFileNewTemplate_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog()
            {
                AddExtension = true,
                AutoUpgradeEnabled = true,
                CheckPathExists = true,
                CreatePrompt = false,
                DefaultExt = Constant.File.TemplateFileExtension,
                FileName = Path.GetFileNameWithoutExtension(Constant.File.DefaultTemplateDatabaseFileName),
                Filter = Constant.File.TemplateFileFilter,
                OverwritePrompt = true,
                Title = "Save new template file",
            };

            if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                this.InitializeDataGrid(saveFileDialog.FileName);
            }
        }

        /// <summary>
        /// Open an existing template.
        /// </summary>
        private void MenuFileOpenTemplate_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog()
            {
                FileName = Path.GetFileNameWithoutExtension(Constant.File.DefaultTemplateDatabaseFileName), // Default file name without the extension
                DefaultExt = Constant.File.TemplateFileExtension, // Default file extension
                Filter = "Database Files (" + Constant.File.TemplateFileExtension + ")|*" + Constant.File.TemplateFileExtension, // Filter files by extension 
                Title = "Select an Existing Template File to Open"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                this.InitializeDataGrid(openFileDialog.FileName);
            }
        }

        // open a recently used template
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
            Visibility visibility = this.MenuOptionsShowAllColumns.IsChecked ? Visibility.Visible : Visibility.Collapsed;
            foreach (DataGridColumn column in this.ControlDataGrid.Columns)
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
            Dictionary<string, int> spreadsheetOrderByDataLabel = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int control = 0; control < dataGrid.Columns.Count; control++)
            {
                string dataLabelFromColumnHeader = dataGrid.Columns[control].Header.ToString();
                int newSpreadsheetOrder = dataGrid.Columns[control].DisplayIndex + 1;
                spreadsheetOrderByDataLabel.Add(dataLabelFromColumnHeader, newSpreadsheetOrder);
            }

            this.templateDatabase.UpdateDisplayOrder(Constant.ControlColumn.SpreadsheetOrder, spreadsheetOrderByDataLabel);
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Utilities.ShowExceptionReportingDialog("The template editor needs to close.", e, this);
        }

        private void RebuildControlPreview()
        {
            this.DataEntryControls.CreateControls(this.templateDatabase, null, (string dataLabel) => { return this.templateDatabase.Controls[dataLabel].GetWellKnownValues(); });
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
            this.controlDataGridBeingUpdatedByCode = true;
            this.templateDatabase.TrySyncControlToDatabase(control);
            this.controlDataGridBeingUpdatedByCode = false;

            if ((this.DataEntryControls.ControlsByDataLabel.TryGetValue(control.DataLabel, out DataEntryControl controlPreview) == false) ||
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
                // changes to control.AnalysisLabel have no effect on the preview
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
                List<string> currentValues = control.GetWellKnownValues();
                List<string> previewValues = controlPreview.GetWellKnownValues();
                bool wellKnonwValuesNeedSynchronization = currentValues.Count != previewValues.Count;
                if (wellKnonwValuesNeedSynchronization == false)
                {
                    List<string> allValues = currentValues.Union(previewValues).ToList();
                    wellKnonwValuesNeedSynchronization = allValues.Count != previewValues.Count;
                }
                if (wellKnonwValuesNeedSynchronization)
                {
                    controlPreview.SetWellKnownValues(currentValues);
                }
                // control.SpreadsheetOrder is handled below
                if (control.Tooltip != controlPreview.LabelTooltip)
                {
                    controlPreview.LabelTooltip = control.Tooltip;
                }
                // control.Type is immutable
                // control.Visible changes are handled above
                if (control.MaxWidth != controlPreview.ContentMaxWidth)
                {
                    controlPreview.ContentMaxWidth = control.MaxWidth;
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
        /// Standard controls can't be deleted.
        /// </summary>
        private void RemoveControlButton_Click(object sender, RoutedEventArgs e)
        {
            ControlRow control = (ControlRow)this.ControlDataGrid.SelectedItem;
            if (control == null)
            {
                // nothing to do
                return;
            }

            if (Constant.Control.StandardControls.Contains(control.DataLabel, StringComparer.Ordinal))
            {
                // standard controls cannot be removed
                return;
            }

            this.controlDataGridBeingUpdatedByCode = true;
            this.templateDatabase.RemoveUserDefinedControl(control);

            // update the control panel so it reflects the current values in the database
            this.RebuildControlPreview();
            this.SynchronizeSpreadsheetOrderPreview();

            this.controlDataGridBeingUpdatedByCode = false;
        }

        private void TutorialLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            if (e.Uri != null)
            {
                Process.Start(e.Uri.AbsoluteUri);
                e.Handled = true;
            }
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
                    if (this.ControlDataGrid.SelectedIndex == row)
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
                updater.TryGetAndParseRelease(false, out Version publiclyAvailableVersion);
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
            this.ControlDataGrid.CommitEdit();
            // persist state to registry
            this.userSettings.WriteToRegistry();
        }
    }
}