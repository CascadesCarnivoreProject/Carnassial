using Carnassial.Data;
using Carnassial.Images;
using Carnassial.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MessageBox = Carnassial.Dialog.MessageBox;

namespace Carnassial.Controls
{
    /// <summary>
    /// Callbacks for data entry or the context menu for propagation.
    /// </summary>
    public class DataEntryHandler : IDisposable
    {
        private bool disposed;

        public FileDatabase FileDatabase { get; private set; }
        public ImageCache ImageCache { get; private set; }
        public bool IsProgrammaticControlUpdate { get; set; }

        public DataEntryHandler(FileDatabase fileDatabase)
        {
            this.disposed = false;
            this.ImageCache = new ImageCache(fileDatabase);
            this.FileDatabase = fileDatabase;
            this.IsProgrammaticControlUpdate = false;
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

        public void IncrementOrResetCounter(DataEntryCounter counter)
        {
            counter.IncrementOrReset();
            this.ImageCache.Current.SetValueFromDatabaseString(counter.DataLabel, counter.Content);
        }

        public bool IsCopyForwardPossible(DataEntryControl control)
        {
            if (this.ImageCache.Current == null || control.Copyable == false)
            {
                return false;
            }

            int imagesAffected = this.FileDatabase.CurrentlySelectedFileCount - this.ImageCache.CurrentRow - 1;
            return (imagesAffected > 0) ? true : false;
        }

        // Return true if there is a non-empty value available
        public bool IsCopyFromLastNonEmptyValuePossible(DataEntryControl control)
        {
            bool checkForZero = control is DataEntryCounter;
            int nearestRowWithCopyableValue = -1;
            for (int fileIndex = this.ImageCache.CurrentRow - 1; fileIndex >= 0; --fileIndex)
            {
                // search for a file with a value assigned for this field, starting from the previous file
                string valueToTest = this.FileDatabase.Files[fileIndex].GetValueDatabaseString(control.DataLabel);
                if (String.IsNullOrWhiteSpace(valueToTest) == false)
                {
                    if ((checkForZero && !valueToTest.Equals("0")) || !checkForZero)
                    {
                        nearestRowWithCopyableValue = fileIndex;    // found a non-empty value
                        break;
                    }
                }
            }
            return (nearestRowWithCopyableValue >= 0) ? true : false;
        }

        /// <summary>
        /// Add data event handler callbacks for (possibly invisible) controls
        /// </summary>
        public void SetDataEntryCallbacks(List<DataEntryControl> dataEntryControl)
        {
            // Adds
            // - copy file path option to file and relative path controls
            // - data entry callbacks to editable controls
            // - propagate context menu to copyable controls
            foreach (DataEntryControl control in dataEntryControl)
            {
                string controlType = this.FileDatabase.ControlsByDataLabel[control.DataLabel].Type;
                switch (controlType)
                {
                    case Constant.Control.Note:
                        DataEntryNote note = (DataEntryNote)control;
                        note.ContentControl.TextAutocompleted += this.NoteControl_TextAutocompleted;
                        break;
                    case Constant.DatabaseColumn.File:
                    case Constant.DatabaseColumn.RelativePath:
                        note = (DataEntryNote)control;
                        Debug.Assert(note.ContentReadOnly, "File name and relative path are expected to be read only fields.");
                        Debug.Assert(note.Copyable == false, "File name and relative path are not expected to be copyable fields.");
                        MenuItem filePathItem = new MenuItem() { Header = "Copy file's path" };
                        filePathItem.Click += this.MenuContextFilePath_Click;
                        note.AppendToContextMenu(filePathItem);
                        break;
                    case Constant.DatabaseColumn.DateTime:
                        DataEntryDateTime dateTime = (DataEntryDateTime)control;
                        dateTime.ContentControl.ValueChanged += this.DateTimeControl_ValueChanged;
                        break;
                    case Constant.DatabaseColumn.UtcOffset:
                        DataEntryUtcOffset utcOffset = (DataEntryUtcOffset)control;
                        utcOffset.ContentControl.ValueChanged += this.UtcOffsetControl_ValueChanged;
                        break;
                    case Constant.DatabaseColumn.DeleteFlag:
                    case Constant.Control.Flag:
                        DataEntryFlag flag = (DataEntryFlag)control;
                        flag.ContentControl.Checked += this.FlagControl_CheckedChanged;
                        flag.ContentControl.Unchecked += this.FlagControl_CheckedChanged;
                        break;
                    case Constant.DatabaseColumn.ImageQuality:
                    case Constant.Control.FixedChoice:
                        DataEntryChoice choice = (DataEntryChoice)control;
                        choice.ContentControl.SelectionChanged += this.ChoiceControl_SelectionChanged;
                        break;
                    case Constant.Control.Counter:
                        DataEntryCounter counter = (DataEntryCounter)control;
                        counter.ContentControl.TextChanged += this.CounterControl_TextChanged;
                        break;
                    default:
                        throw new NotSupportedException(String.Format("Unhandled control type '{0}'.", controlType));
                }

                // add the propagate context menu to copyable fields
                if (control.Copyable)
                {
                    MenuItem menuItemPropagateFromLastValue = new MenuItem();
                    menuItemPropagateFromLastValue.Header = "Propagate from the _last non-empty value to here...";
                    if (control is DataEntryCounter)
                    {
                        menuItemPropagateFromLastValue.Header = "Propagate from the _last non-zero value to here...";
                    }
                    menuItemPropagateFromLastValue.Click += this.MenuContextPropagateFromLastValue_Click;
                    menuItemPropagateFromLastValue.Tag = DataEntryControlContextMenuItemType.PropagateFromLastValue;

                    MenuItem menuItemCopyForward = new MenuItem();
                    menuItemCopyForward.Header = "Copy forward to _end...";
                    menuItemCopyForward.ToolTip = "The value of this field will be copied forward from this file to the last file in this set.";
                    menuItemCopyForward.Click += this.MenuContextPropagateForward_Click;
                    menuItemCopyForward.Tag = DataEntryControlContextMenuItemType.CopyForward;

                    MenuItem menuItemCopyToAll = new MenuItem();
                    menuItemCopyToAll.Header = "Copy to _all...";
                    menuItemCopyToAll.ToolTip = "Copy the value of this field to all selected files.";
                    menuItemCopyToAll.Click += this.MenuContextCopyToAll_Click;
                    menuItemCopyToAll.Tag = DataEntryControlContextMenuItemType.CopyToAll;

                    control.AppendToContextMenu(menuItemPropagateFromLastValue, menuItemCopyForward, menuItemCopyToAll);
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

            this.FileDatabase.UpdateFile(this.ImageCache.Current);
            this.ImageCache.Current.AcceptChanges();
            return true;
        }

        public static bool TryFindFocusedControl(IInputElement focusedElement, out DataEntryControl focusedControl)
        {
            if (focusedElement is FrameworkElement)
            {
                FrameworkElement focusedFrameworkElement = (FrameworkElement)focusedElement;
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
        private bool? ConfirmCopyForward(string text, int imagesAffected, bool checkForZero)
        {
            text = text.Trim();

            MessageBox messageBox = new MessageBox("Please confirm copy forward for this field...", Application.Current.MainWindow, MessageBoxButton.YesNo);
            messageBox.Message.StatusImage = MessageBoxImage.Question;
            messageBox.Message.What = "Copy forward is not undoable and can overwrite existing values.";
            messageBox.Message.Result = "If you select yes, this operation will:" + Environment.NewLine;
            if (!checkForZero && text.Equals(String.Empty))
            {
                messageBox.Message.Result += "\u2022 copy the (empty) value '" + text + "' in this field from here to the last of the selected files.";
            }
            else
            {
                messageBox.Message.Result += "\u2022 copy the value '" + text + "' in this field from here to the last of the selected files.";
            }
            messageBox.Message.Result += Environment.NewLine + "\u2022 over-write any existing data values in those fields";
            messageBox.Message.Result += Environment.NewLine + "\u2022 will affect " + imagesAffected.ToString() + " files.";
            return messageBox.ShowDialog();
        }

        // ask the user to confirm propagation to all selected files
        private bool? ConfirmCopyCurrentValueToAll(String text, int filesAffected, bool checkForZero)
        {
            text = text.Trim();

            MessageBox messageBox = new MessageBox("Please confirm copy to all for this field...", Application.Current.MainWindow, MessageBoxButton.YesNo);
            messageBox.Message.StatusImage = MessageBoxImage.Question;
            messageBox.Message.What = "Copy to all is not undoable and can overwrite existing values.";
            messageBox.Message.Result = "If you select yes, this operation will:" + Environment.NewLine;
            if (!checkForZero && text.Equals(String.Empty))
            {
                messageBox.Message.Result += "\u2022 clear this field across all " + filesAffected.ToString() + " selected files.";
            }
            else
            {
                messageBox.Message.Result += "\u2022 set this field to '" + text + "' across all " + filesAffected.ToString() + " selected files.";
            }
            messageBox.Message.Result += Environment.NewLine + "\u2022 over-write any existing data values in those fields";
            return messageBox.ShowDialog();
        }

        // ask the user to confirm value propagation from the last value
        private bool? ConfirmCopyFromLastNonEmptyValue(String text, int filesAffected)
        {
            text = text.Trim();
            MessageBox messageBox = new MessageBox("Please confirm 'Propagate to Here' for this field.", Application.Current.MainWindow, MessageBoxButton.YesNo);
            messageBox.Message.StatusImage = MessageBoxImage.Question;
            messageBox.Message.What = "Propagate to here is not undoable and can overwrite existing values.";
            messageBox.Message.Reason = "\u2022 The last non-empty value '" + text + "' was seen " + filesAffected.ToString() + " files back." + Environment.NewLine;
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

        // when the number in a counter changes, update the counter's field in the database
        private void CounterControl_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (this.IsProgrammaticControlUpdate)
            {
                return;
            }

            TextBox textBox = (TextBox)sender;
            DataEntryControl control = (DataEntryControl)textBox.Tag;
            control.SetValue(textBox.Text);
            this.ImageCache.Current.SetValueFromDatabaseString(control.DataLabel, control.Content);
        }

        // when a choice changes, update the choice's field in the database
        private void ChoiceControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.IsProgrammaticControlUpdate)
            {
                return;
            }

            ComboBox comboBox = (ComboBox)sender;
            if (comboBox.SelectedItem == null)
            {
                // no item selected (probably the user cancelled)
                return;
            }

            DataEntryControl control = (DataEntryControl)comboBox.Tag;
            control.SetValue(comboBox.SelectedItem.ToString());
            this.ImageCache.Current.SetValueFromDatabaseString(control.DataLabel, control.Content);
        }

        private void DateTimeControl_ValueChanged(DateTimeOffsetPicker dateTimePicker, DateTimeOffset newDateTime)
        {
            if (this.IsProgrammaticControlUpdate)
            {
                return;
            }

            this.ImageCache.Current.SetDateTimeOffset(newDateTime);
            dateTimePicker.ToolTip = newDateTime.ToString(dateTimePicker.Format);
        }

        // when a flag changes checked state, update the flag's field in the database
        private void FlagControl_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (this.IsProgrammaticControlUpdate)
            {
                return;
            }

            CheckBox checkBox = (CheckBox)sender;
            string value = ((bool)checkBox.IsChecked) ? Boolean.TrueString : Boolean.FalseString;
            DataEntryControl control = (DataEntryControl)checkBox.Tag;
            control.SetValue(value);
            this.ImageCache.Current.SetValueFromDatabaseString(control.DataLabel, control.Content);
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
            string valueToCopy;
            if (this.TryCopyFromLastNonEmptyValue(control, out valueToCopy))
            {
                control.SetValue(valueToCopy);
            }
        }

        // update database whenever text in a note control changes and mark the field for autocomplete recalculation
        private void NoteControl_TextAutocompleted(object sender, TextChangedEventArgs e)
        {
            if (this.IsProgrammaticControlUpdate)
            {
                return;
            }

            DataEntryNote control = (DataEntryNote)((TextBox)sender).Tag;
            control.ContentChanged = true;
            this.ImageCache.Current.SetValueFromDatabaseString(control.DataLabel, control.Content);
        }

        /// <summary>Propagate the current value of this control forward from this point across the current selection.</summary>
        private bool TryCopyForward(DataEntryControl control, bool checkForZeroValue)
        {
            int filesAffected = this.FileDatabase.CurrentlySelectedFileCount - this.ImageCache.CurrentRow - 1;
            if (filesAffected == 0)
            {
                // should be unreachable as the menu shouldn't be be enabled on the last file
                MessageBox messageBox = new MessageBox("Nothing to copy forward.", Application.Current.MainWindow);
                messageBox.Message.Reason = "As you are on the last file, there are no files after this.";
                messageBox.ShowDialog();
                return false;
            }

            string displayValueForConfirm = this.ImageCache.Current.GetValueDisplayString(control);
            if (this.ConfirmCopyForward(displayValueForConfirm, filesAffected, checkForZeroValue) != true)
            {
                return false;
            }

            // update starts on the next row since copying from the current row
            this.FileDatabase.UpdateFiles(this.ImageCache.Current, control, this.ImageCache.CurrentRow + 1, this.FileDatabase.CurrentlySelectedFileCount - 1);
            return true;
        }

        /// <summary>
        /// Copy the closest, non-empty value in a file preceding this one to all intervening files plus the current file
        /// </summary>
        private bool TryCopyFromLastNonEmptyValue(DataEntryControl control, out string valueToCopy)
        {
            bool isCounter = control is DataEntryCounter;
            bool isFlag = control is DataEntryFlag;

            // search for a previous file with a value assigned for this field, starting from the previous row
            ImageRow fileWithLastNonEmptyValue = null;
            int indexToCopyFrom = Constant.Database.InvalidRow;
            valueToCopy = isCounter ? "0" : String.Empty;
            for (int previousIndex = this.ImageCache.CurrentRow - 1; previousIndex >= 0; previousIndex--)
            {
                ImageRow file = this.FileDatabase.Files[previousIndex];
                valueToCopy = file.GetValueDatabaseString(control.DataLabel);
                if (valueToCopy == null)
                {
                    continue;
                }

                valueToCopy = valueToCopy.Trim();
                if (valueToCopy.Length > 0)
                {
                    // skip zero values for counters and false values for flags
                    if ((isCounter && !valueToCopy.Equals("0")) ||
                        (isFlag && !valueToCopy.Equals(Boolean.FalseString, StringComparison.OrdinalIgnoreCase)) ||
                        (!isCounter && !isFlag))
                    {
                        indexToCopyFrom = previousIndex;
                        fileWithLastNonEmptyValue = file;
                        break;
                    }
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

            int imagesAffected = this.ImageCache.CurrentRow - indexToCopyFrom;
            if (this.ConfirmCopyFromLastNonEmptyValue(valueToCopy, imagesAffected) != true)
            {
                return false;
            }

            this.FileDatabase.UpdateFiles(fileWithLastNonEmptyValue, control, indexToCopyFrom + 1, this.ImageCache.CurrentRow);
            return true;
        }

        /// <summary>Copy the current value of this control to all images</summary>
        public bool TryCopyToAll(DataEntryControl control)
        {
            bool checkForZero = control is DataEntryCounter;
            int imagesAffected = this.FileDatabase.CurrentlySelectedFileCount;
            string displayValueForConfirm = this.ImageCache.Current.GetValueDisplayString(control);
            if (this.ConfirmCopyCurrentValueToAll(displayValueForConfirm, imagesAffected, checkForZero) != true)
            {
                return false;
            }
            this.FileDatabase.UpdateFiles(this.ImageCache.Current, control);
            return true;
        }

        public bool TryDecrementOrResetCounter(DataEntryCounter counter)
        {
            bool counterValueChanged = counter.TryDecrementOrReset();
            if (counterValueChanged)
            {
                this.ImageCache.Current.SetValueFromDatabaseString(counter.DataLabel, counter.Content);
            }
            return counterValueChanged;
        }

        private void UtcOffsetControl_ValueChanged(TimeSpanPicker utcOffsetPicker, TimeSpan newTimeSpan)
        {
            if (this.IsProgrammaticControlUpdate)
            {
                return;
            }

            DateTimeOffset currentImageDateTime = this.ImageCache.Current.GetDateTime();
            DateTimeOffset newImageDateTime = currentImageDateTime.SetOffset(utcOffsetPicker.Value);
            this.ImageCache.Current.SetDateTimeOffset(newImageDateTime);
            utcOffsetPicker.ToolTip = DateTimeHandler.ToDisplayUtcOffsetString(utcOffsetPicker.Value);
        }
    }
}
