using Carnassial.Data;
using Carnassial.Database;
using Carnassial.Images;
using Carnassial.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MessageBox = Carnassial.Dialog.MessageBox;

namespace Carnassial.Control
{
    /// <summary>
    /// Callbacks for data entry or and context menus for propagation.
    /// </summary>
    public class DataEntryHandler : IDisposable
    {
        private bool disposed;

        public event EventHandler BulkEdit;
        public FileDatabase FileDatabase { get; private set; }
        public ImageCache ImageCache { get; private set; }
        public bool IsProgrammaticUpdate { get; set; }

        public DataEntryHandler(FileDatabase fileDatabase)
        {
            this.disposed = false;
            this.ImageCache = new ImageCache(fileDatabase);
            this.FileDatabase = fileDatabase;
            this.IsProgrammaticUpdate = false;
        }

        public void DeleteFiles(IEnumerable<ImageRow> filesToDelete, bool deleteFilesAndData)
        {
            this.IsProgrammaticUpdate = true;

            FileTuplesWithID filesToUpdate = new FileTuplesWithID(Constant.DatabaseColumn.DeleteFlag, Constant.DatabaseColumn.ImageQuality);
            List<long> fileIDsToDropFromDatabase = new List<long>();
            using (Recycler fileOperation = new Recycler())
            {
                foreach (ImageRow file in filesToDelete)
                {
                    string filePath = file.GetFilePath(this.FileDatabase.FolderPath);
                    if (File.Exists(filePath))
                    {
                        fileOperation.MoveToRecycleBin(filePath);
                    }

                    // if this file is cached, invalidate it so FileNoLongerAvailable placeholder will be displayed instead of the
                    // cached image
                    this.ImageCache.TryInvalidate(file.ID);

                    if (deleteFilesAndData)
                    {
                        // mark the file row for dropping
                        fileIDsToDropFromDatabase.Add(file.ID);
                    }
                    else
                    {
                        // as only the file was deleted, change image quality to FileNoLongerAvailable and clear the delete flag
                        file.DeleteFlag = false;
                        file.ImageQuality = FileSelection.NoLongerAvailable;
                        filesToUpdate.Add(file.ID, Boolean.FalseString, FileSelection.NoLongerAvailable.ToString());
                    }
                }
            }

            if (deleteFilesAndData)
            {
                // drop files
                Debug.Assert(fileIDsToDropFromDatabase.Count > 0, "No files are being deleted.");
                Debug.Assert(filesToUpdate.RowCount == 0, "Files to update unexpectedly present.");
                this.FileDatabase.DeleteFiles(fileIDsToDropFromDatabase);
            }
            else
            {
                // update file properties
                Debug.Assert(fileIDsToDropFromDatabase.Count > 0, "Files to drop from database unexpectedly present.");
                Debug.Assert(filesToUpdate.RowCount > 0, "No files are being to be deleted.");
                this.FileDatabase.UpdateFiles(filesToUpdate);
            }

            this.BulkEdit?.Invoke(this, null);
            this.IsProgrammaticUpdate = false;
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

            if (disposing)
            {
                this.TrySyncCurrentFileToDatabase();
                this.FileDatabase.Dispose();
                this.ImageCache.Dispose();
            }

            this.disposed = true;
        }

        public Dictionary<string, object> GetCopyableFields(ImageRow file, List<DataEntryControl> controls)
        {
            Dictionary<string, object> copyableFields = new Dictionary<string, object>();
            foreach (DataEntryControl control in controls)
            {
                if (control.Copyable)
                {
                    copyableFields.Add(control.PropertyName, file[control.PropertyName]);
                }
            }
            return copyableFields;
        }

        public Dictionary<string, object> GetCopyableFieldsFromCurrentFile(List<DataEntryControl> controls)
        {
            Debug.Assert(this.ImageCache.IsFileAvailable, "Attempt to copy from nonexistent file");
            return this.GetCopyableFields(this.ImageCache.Current, controls);
        }

        public bool IsCopyForwardPossible(DataEntryControl control)
        {
            if (this.ImageCache.Current == null || control.Copyable == false)
            {
                return false;
            }

            int filesAffected = this.FileDatabase.CurrentlySelectedFileCount - this.ImageCache.CurrentRow - 1;
            return (filesAffected > 0) ? true : false;
        }

        // return true if there is a non-empty value available
        public bool IsCopyFromLastNonEmptyValuePossible(DataEntryControl control)
        {
            Debug.Assert(control.Type != ControlType.DateTime, "Propagate context menu unexpectedly enabled on DateTime control.");
            Debug.Assert(control.Type != ControlType.UtcOffset, "Propagate context menu unexpectedly enabled on UTC offset control.");
            bool isCounter = control.Type == ControlType.Counter;
            bool isFlag = control.Type == ControlType.Flag;

            for (int fileIndex = this.ImageCache.CurrentRow - 1; fileIndex >= 0; --fileIndex)
            {
                // search for a file with a value assigned for this field, starting from the previous file
                object valueToTest = this.FileDatabase.Files[fileIndex][control.PropertyName];
                if (this.IsNonEmpty(valueToTest, isCounter, isFlag))
                {
                    return true;
                }
            }
            return false;
        }

        private bool IsNonEmpty(object value, bool isCounter, bool isFlag)
        {
            if (isFlag && (value is bool))
            {
                // standard control flags
                return (bool)value;
            }

            string valueAsString = (string)value;
            if (String.IsNullOrEmpty(valueAsString))
            {
                return false;
            }

            if (isCounter)
            {
                return String.Equals(valueAsString, "0", StringComparison.Ordinal) == false;
            }
            else if (isFlag)
            {
                // user defined flags
                return String.Equals(valueAsString, Boolean.TrueString, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                return true;
            }
        }

        /// <summary>
        /// Add data event handler callbacks for (possibly invisible) controls
        /// </summary>
        public void SetDataEntryCallbacks(List<DataEntryControl> dataEntryControls)
        {
            // Adds
            // - copy file path option to file and relative path controls
            // - data entry callbacks to editable controls
            // - propagate context menu to copyable controls
            foreach (DataEntryControl dataEntryControl in dataEntryControls)
            {
                ControlRow control = this.FileDatabase.ControlsByDataLabel[dataEntryControl.DataLabel];
                if (control.IsFilePathComponent())
                {
                    Debug.Assert(dataEntryControl.ContentReadOnly, "File name and relative path are expected to be read only fields.");
                    Debug.Assert(dataEntryControl.Copyable == false, "File name and relative path are not expected to be copyable fields.");
                    MenuItem filePathItem = new MenuItem() { Header = "Copy file's path" };
                    filePathItem.Click += this.MenuContextFilePath_Click;
                    dataEntryControl.AppendToContextMenu(filePathItem);
                }

                // add propagate context menu to copyable fields
                if (dataEntryControl.Copyable)
                {
                    Debug.Assert(control.Type != ControlType.DateTime, "Propagate context menu unexpectedly enabled on DateTime control.");
                    Debug.Assert(control.Type != ControlType.UtcOffset, "Propagate context menu unexpectedly enabled on UTC offset control.");

                    MenuItem menuItemPropagateFromLastValue = new MenuItem()
                    {
                        Header = "Propagate from the _last non-empty value to here...",
                        Tag = DataEntryControlContextMenuItemType.PropagateFromLastValue
                    };
                    menuItemPropagateFromLastValue.Click += this.MenuContextPropagateFromLastValue_Click;
                    if (dataEntryControl is DataEntryCounter)
                    {
                        menuItemPropagateFromLastValue.Header = "Propagate from the _last non-zero value to here...";
                    }

                    MenuItem menuItemCopyForward = new MenuItem()
                    {
                        Header = "Copy forward to _end...",
                        ToolTip = "The value of this field will be copied forward from this file to the last file in this set.",
                        Tag = DataEntryControlContextMenuItemType.CopyForward
                    };
                    menuItemCopyForward.Click += this.MenuContextPropagateForward_Click;

                    MenuItem menuItemCopyToAll = new MenuItem()
                    {
                        Header = "Copy to _all...",
                        ToolTip = "Copy the value of this field to all selected files.",
                        Tag = DataEntryControlContextMenuItemType.CopyToAll
                    };
                    menuItemCopyToAll.Click += this.MenuContextCopyToAll_Click;

                    dataEntryControl.AppendToContextMenu(menuItemPropagateFromLastValue, menuItemCopyForward, menuItemCopyToAll);
                }
            }
        }

        public bool TrySyncCurrentFileToDatabase()
        {
            if (this.ImageCache.Current == null)
            {
                return false;
            }
            if (this.ImageCache.Current.HasChanges == false)
            {
                // database is already in sync 
                return true;
            }

            // update row in file table
            this.FileDatabase.SyncFileToDatabase(this.ImageCache.Current);
            this.ImageCache.Current.AcceptChanges();

            return true;
        }

        public static bool TryFindFocusedControl(IInputElement focusedElement, out DataEntryControl focusedControl)
        {
            if (focusedElement is FrameworkElement focusedFrameworkElement)
            {
                focusedControl = (DataEntryControl)focusedFrameworkElement.Tag;
                if (focusedControl != null)
                {
                    return true;
                }

                // for complex controls which dynamic generate child controls, such as date time pickers, the tag of the focused element can't be set
                // so try to locate a parent of the focused element with a tag indicating the control
                FrameworkElement parent = null;
                if (focusedFrameworkElement.Parent != null && focusedFrameworkElement.Parent is FrameworkElement)
                {
                    parent = (FrameworkElement)focusedFrameworkElement.Parent;
                }
                else if (focusedFrameworkElement.TemplatedParent != null && focusedFrameworkElement.TemplatedParent is FrameworkElement)
                {
                    parent = (FrameworkElement)focusedFrameworkElement.TemplatedParent;
                }

                if (parent != null)
                {
                    return DataEntryHandler.TryFindFocusedControl(parent, out focusedControl);
                }
            }

            focusedControl = null;
            return false;
        }

        // ask the user to confirm value propagation from the last value
        private bool? ConfirmCopyForward(string value, int filesAffected, bool checkForZero)
        {
            MessageBox messageBox = new MessageBox("Please confirm copy forward for this field...", Application.Current.MainWindow, MessageBoxButton.YesNo);
            messageBox.Message.StatusImage = MessageBoxImage.Question;
            messageBox.Message.What = "Copy forward is not undoable and can overwrite existing values.";
            messageBox.Message.Result = "If you select yes, this operation will:" + Environment.NewLine;
            if (!checkForZero && value.Equals(String.Empty))
            {
                messageBox.Message.Result += "\u2022 copy the (empty) value '" + value + "' in this field from here to the last of the selected files.";
            }
            else
            {
                messageBox.Message.Result += "\u2022 copy the value '" + value + "' in this field from here to the last of the selected files.";
            }
            messageBox.Message.Result += Environment.NewLine + "\u2022 over-write any existing data values in those fields";
            messageBox.Message.Result += Environment.NewLine + "\u2022 will affect " + filesAffected.ToString() + " files.";
            return messageBox.ShowDialog();
        }

        // ask the user to confirm propagation to all selected files
        private bool? ConfirmCopyCurrentValueToAll(string value, int filesAffected, bool checkForZero)
        {
            MessageBox messageBox = new MessageBox("Please confirm copy to all for this field...", Application.Current.MainWindow, MessageBoxButton.YesNo);
            messageBox.Message.StatusImage = MessageBoxImage.Question;
            messageBox.Message.What = "Copy to all is not undoable and can overwrite existing values.";
            messageBox.Message.Result = "If you select yes, this operation will:" + Environment.NewLine;
            if (!checkForZero && value.Equals(String.Empty))
            {
                messageBox.Message.Result += "\u2022 clear this field across all " + filesAffected.ToString() + " selected files.";
            }
            else
            {
                messageBox.Message.Result += "\u2022 set this field to '" + value + "' across all " + filesAffected.ToString() + " selected files.";
            }
            messageBox.Message.Result += Environment.NewLine + "\u2022 over-write any existing data values in those fields";
            return messageBox.ShowDialog();
        }

        // ask the user to confirm value propagation from the last value
        private bool? ConfirmCopyFromLastNonEmptyValue(string value, int filesAffected)
        {
            MessageBox messageBox = new MessageBox("Please confirm 'Propagate to Here' for this field.", Application.Current.MainWindow, MessageBoxButton.YesNo);
            messageBox.Message.StatusImage = MessageBoxImage.Question;
            messageBox.Message.What = "Propagate to here is not undoable and can overwrite existing values.";
            messageBox.Message.Reason = "\u2022 The last non-empty value '" + value + "' was seen " + filesAffected.ToString() + " files back." + Environment.NewLine;
            messageBox.Message.Reason += "\u2022 That field's value will be copied to all files between that file and this one in the selection";
            messageBox.Message.Result = "If you select yes: " + Environment.NewLine;
            messageBox.Message.Result = "\u2022 " + filesAffected.ToString() + " files will be affected.";
            return messageBox.ShowDialog();
        }

        // enable or disable context menu items
        protected virtual void Container_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            DataEntryControl control = (DataEntryControl)((FrameworkElement)sender).Tag;
            ContextMenu contextMenu = control.ContextMenu;
            if (contextMenu != null && contextMenu.Items != null)
            {
                foreach (object menuObject in contextMenu.Items)
                {
                    if (menuObject is MenuItem == false)
                    {
                        continue;
                    }

                    MenuItem menuItem = (MenuItem)menuObject;
                    DataEntryControlContextMenuItemType menuItemType = (DataEntryControlContextMenuItemType)menuItem.Tag;
                    switch (menuItemType)
                    {
                        case DataEntryControlContextMenuItemType.CopyForward:
                            menuItem.IsEnabled = this.IsCopyForwardPossible(control);
                            break;
                        case DataEntryControlContextMenuItemType.CopyToAll:
                            // nothing to do
                            break;
                        case DataEntryControlContextMenuItemType.PropagateFromLastValue:
                            menuItem.IsEnabled = this.IsCopyFromLastNonEmptyValuePossible(control);
                            break;
                        default:
                            throw new NotSupportedException(String.Format("Unhandled context menu item type {0}.", menuItemType));
                    }
                }
            }
        }

        private void MenuContextFilePath_Click(object sender, RoutedEventArgs e)
        {
            Debug.Assert(this.ImageCache.Current != null, "Context menu unexpectedly reached without a file displayed.");
            string filePath = this.ImageCache.Current.GetFilePath(this.FileDatabase.FolderPath);
            Clipboard.SetText(filePath);
        }

        // copy the current value of this control to all files
        private void MenuContextCopyToAll_Click(object sender, RoutedEventArgs e)
        {
            DataEntryControl control = (DataEntryControl)((ContextMenu)((MenuItem)sender).Parent).Tag;
            this.TryCopyToAll(control);
        }

        // propagate the current value of this control forward from this point across the current selection
        private void MenuContextPropagateForward_Click(object sender, RoutedEventArgs e)
        {
            DataEntryControl control = (DataEntryControl)((ContextMenu)((MenuItem)sender).Parent).Tag;
            this.TryCopyForward(control, control is DataEntryCounter);
        }

        private void MenuContextPropagateFromLastValue_Click(object sender, RoutedEventArgs e)
        {
            DataEntryControl control = (DataEntryControl)((ContextMenu)((MenuItem)sender).Parent).Tag;
            if (this.TryCopyFromLastNonEmptyValue(control, out object valueToCopy))
            {
                this.ImageCache.Current[control.PropertyName] = valueToCopy;
            }
        }

        /// <summary>Propagate the current value of this control forward from this point across the current selection.</summary>
        private bool TryCopyForward(DataEntryControl control, bool checkForZeroValue)
        {
            int filesAffected = this.FileDatabase.CurrentlySelectedFileCount - this.ImageCache.CurrentRow - 1;
            if (filesAffected < 1)
            {
                // should be unreachable as the menu shouldn't be be enabled on the last file
                MessageBox messageBox = new MessageBox("Nothing to copy forward.", Application.Current.MainWindow);
                messageBox.Message.Reason = "As you are on the last file, there are no files after this.";
                messageBox.ShowDialog();
                return false;
            }

            string displayValueForConfirm = this.ImageCache.Current.GetDisplayString(control);
            if (this.ConfirmCopyForward(displayValueForConfirm, filesAffected, checkForZeroValue) != true)
            {
                return false;
            }

            // update starts on the next row since copying from the current row
            this.FileDatabase.UpdateFiles(this.ImageCache.Current, control, this.ImageCache.CurrentRow + 1, this.FileDatabase.CurrentlySelectedFileCount - 1);
            this.BulkEdit?.Invoke(this, null);
            return true;
        }

        /// <summary>
        /// Copy the closest, non-empty value in a file preceding this one to all intervening files plus the current file
        /// </summary>
        private bool TryCopyFromLastNonEmptyValue(DataEntryControl control, out object valueToCopy)
        {
            bool isCounter = control.Type == ControlType.Counter;
            bool isFlag = control.Type == ControlType.Flag;

            // search for a previous file with a value assigned for this field, starting from the previous row
            ImageRow fileWithLastNonEmptyValue = null;
            int indexToCopyFrom = Constant.Database.InvalidRow;
            string displayValueForConfirm = null;
            valueToCopy = isCounter ? "0" : String.Empty;
            for (int previousIndex = this.ImageCache.CurrentRow - 1; previousIndex >= 0; previousIndex--)
            {
                ImageRow file = this.FileDatabase.Files[previousIndex];
                valueToCopy = file[control.PropertyName];
                if (valueToCopy == null)
                {
                    continue;
                }

                // skip zero values for counters and false values for flags
                if (this.IsNonEmpty(valueToCopy, isCounter, isFlag))
                {
                    indexToCopyFrom = previousIndex;
                    fileWithLastNonEmptyValue = file;
                    displayValueForConfirm = this.ImageCache.Current.GetDisplayString(control);
                    break;
                }
            }

            if (indexToCopyFrom == Constant.Database.InvalidRow)
            {
                // Nothing to propagate.  If the menu item is deactivated as expected this shouldn't be reachable.
                MessageBox messageBox = new MessageBox("Nothing to propagate to here.", Application.Current.MainWindow);
                messageBox.Message.Reason = "None of the earlier files have a value specified for this field, so there is no value to propagate.";
                messageBox.ShowDialog();
                return false;
            }

            int filesAffected = this.ImageCache.CurrentRow - indexToCopyFrom;
            if (this.ConfirmCopyFromLastNonEmptyValue(displayValueForConfirm, filesAffected) != true)
            {
                return false;
            }

            this.FileDatabase.UpdateFiles(fileWithLastNonEmptyValue, control, indexToCopyFrom + 1, this.ImageCache.CurrentRow);
            this.BulkEdit?.Invoke(this, null);
            return true;
        }

        /// <summary>Copy the current value of this control to all files</summary>
        public bool TryCopyToAll(DataEntryControl control)
        {
            bool checkForZero = control is DataEntryCounter;
            int filesAffected = this.FileDatabase.CurrentlySelectedFileCount;
            string displayValueForConfirm = this.ImageCache.Current.GetDisplayString(control);
            if (this.ConfirmCopyCurrentValueToAll(displayValueForConfirm, filesAffected, checkForZero) != true)
            {
                return false;
            }

            this.TrySyncCurrentFileToDatabase();
            this.FileDatabase.UpdateFiles(this.ImageCache.Current, control);
            this.BulkEdit?.Invoke(this, null);
            return true;
        }
    }
}
