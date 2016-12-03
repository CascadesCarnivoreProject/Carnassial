using Carnassial.Database;
using Carnassial.Images;
using Carnassial.Util;
using System;
using System.Collections.Generic;
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
        private const int CopyForwardMenuIndex = 1;
        private const int PropagateFromLastValueMenuIndex = 0;

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

        /// <summary>Propagate the current value of this control forward from this point across the current selection.</summary>
        public void CopyForward(DataEntryControl control, bool checkForZeroValue)
        {
            int filesAffected = this.FileDatabase.CurrentlySelectedFileCount - this.ImageCache.CurrentRow - 1;
            if (filesAffected == 0)
            {
                // should be unreachable as the menu shouldn't be be enabled on the last file
                MessageBox messageBox = new MessageBox("Nothing to copy forward.", Application.Current.MainWindow);
                messageBox.Message.Reason = "As you are on the last file, there are no files after this.";
                messageBox.ShowDialog();
                return;
            }

            string valueToCopy = this.ImageCache.Current.GetValueDisplayString(control);
            if (this.ConfirmCopyForward(valueToCopy, filesAffected, checkForZeroValue) != true)
            {
                return;
            }

            // update starts on the next row since copying from the current row
            this.FileDatabase.UpdateFiles(this.ImageCache.Current, control, this.ImageCache.CurrentRow + 1, this.FileDatabase.CurrentlySelectedFileCount - 1);
        }

        /// <summary>
        /// Copy the last non-empty value in this control preceding this file up to the current file
        /// </summary>
        public string CopyFromLastNonEmptyValue(DataEntryControl control)
        {
            bool isCounter = control is DataEntryCounter;
            bool isFlag = control is DataEntryFlag;

            ImageRow fileWithLastNonEmptyValue = null;
            int indexToCopyFrom = Constant.Database.InvalidRow;
            string valueToCopy = isCounter ? "0" : String.Empty;
            for (int previousIndex = this.ImageCache.CurrentRow - 1; previousIndex >= 0; previousIndex--)
            {
                // Search for the row with some value in it, starting from the previous row
                ImageRow file = this.FileDatabase.Files[previousIndex];
                valueToCopy = file.GetValueDatabaseString(control.DataLabel);
                if (valueToCopy == null)
                {
                    continue;
                }

                valueToCopy = valueToCopy.Trim();
                if (valueToCopy.Length > 0)
                {
                    if ((isCounter && !valueToCopy.Equals("0")) ||                                               // skip zero values for counters
                        (isFlag && !valueToCopy.Equals(Boolean.FalseString, StringComparison.OrdinalIgnoreCase)) || // false values for flags are considered empty
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
                messageBox.Message.Reason = "None of the earlier files have anything in this field, so there are no values to propagate.";
                messageBox.ShowDialog();
                return this.FileDatabase.Files[this.ImageCache.CurrentRow].GetValueDisplayString(control); // No change, so return the current value
            }

            int imagesAffected = this.ImageCache.CurrentRow - indexToCopyFrom;
            if (this.ConfirmPropagateFromLastValue(valueToCopy, imagesAffected) != true)
            {
                return this.FileDatabase.Files[this.ImageCache.CurrentRow].GetValueDisplayString(control); // No change, so return the current value
            }

            this.FileDatabase.UpdateFiles(fileWithLastNonEmptyValue, control, indexToCopyFrom + 1, this.ImageCache.CurrentRow);
            return valueToCopy;
        }

        /// <summary>Copy the current value of this control to all images</summary>
        public void CopyToAll(DataEntryControl control)
        {
            bool checkForZero = control is DataEntryCounter;
            int imagesAffected = this.FileDatabase.CurrentlySelectedFileCount;
            string displayValueToCopy = this.ImageCache.Current.GetValueDisplayString(control);
            if (this.ConfirmCopyCurrentValueToAll(displayValueToCopy, imagesAffected, checkForZero) != true)
            {
                return;
            }
            this.FileDatabase.UpdateFiles(this.ImageCache.Current, control);
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
                if (this.FileDatabase != null)
                {
                    this.FileDatabase.Dispose();
                }
            }

            this.disposed = true;
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
                // Search for the row with some value in it, starting from the previous row
                string valueToCopy = this.FileDatabase.Files[fileIndex].GetValueDatabaseString(control.DataLabel);
                if (String.IsNullOrWhiteSpace(valueToCopy) == false)
                {
                    if ((checkForZero && !valueToCopy.Equals("0")) || !checkForZero)
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
        public void SetDataEntryCallbacks(Dictionary<string, DataEntryControl> controlsByDataLabel)
        {
            // Add data entry callbacks to all editable controls. When the user changes a file's attribute using a particular control,
            // the callback updates the matching field for that file in the database.
            foreach (KeyValuePair<string, DataEntryControl> pair in controlsByDataLabel)
            {
                if (pair.Value.ContentReadOnly)
                {
                    continue;
                }

                string controlType = this.FileDatabase.FileTableColumnsByDataLabel[pair.Key].ControlType;
                switch (controlType)
                {
                    case Constant.Control.Note:
                    case Constant.DatabaseColumn.File:
                    case Constant.DatabaseColumn.RelativePath:
                        DataEntryNote note = (DataEntryNote)pair.Value;
                        note.ContentControl.TextAutocompleted += this.NoteControl_TextAutocompleted;
                        break;
                    case Constant.DatabaseColumn.DateTime:
                        DataEntryDateTime dateTime = (DataEntryDateTime)pair.Value;
                        dateTime.ContentControl.ValueChanged += this.DateTimeControl_ValueChanged;
                        break;
                    case Constant.DatabaseColumn.UtcOffset:
                        DataEntryUtcOffset utcOffset = (DataEntryUtcOffset)pair.Value;
                        utcOffset.ContentControl.ValueChanged += this.UtcOffsetControl_ValueChanged;
                        break;
                    case Constant.DatabaseColumn.DeleteFlag:
                    case Constant.Control.Flag:
                        DataEntryFlag flag = (DataEntryFlag)pair.Value;
                        flag.ContentControl.Checked += this.FlagControl_CheckedChanged;
                        flag.ContentControl.Unchecked += this.FlagControl_CheckedChanged;
                        break;
                    case Constant.DatabaseColumn.ImageQuality:
                    case Constant.Control.FixedChoice:
                        DataEntryChoice choice = (DataEntryChoice)pair.Value;
                        choice.ContentControl.SelectionChanged += this.ChoiceControl_SelectionChanged;
                        break;
                    case Constant.Control.Counter:
                        DataEntryCounter counter = (DataEntryCounter)pair.Value;
                        counter.ContentControl.TextChanged += this.CounterControl_TextChanged;
                        break;
                    default:
                        throw new NotSupportedException(String.Format("Unhandled control type '{0}'.", controlType));
                }

                if (pair.Value.Copyable)
                {
                    this.SetPropagateContextMenu(pair.Value);
                }
            }
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
        private bool? ConfirmPropagateFromLastValue(String text, int imagesAffected)
        {
            text = text.Trim();
            MessageBox messageBox = new MessageBox("Please confirm 'Propagate to Here' for this field.", Application.Current.MainWindow, MessageBoxButton.YesNo);
            messageBox.Message.StatusImage = MessageBoxImage.Question;
            messageBox.Message.What = "Propagate to here is not undoabl, and can overwrite existing values.";
            messageBox.Message.Reason = "\u2022 The last non-empty value '" + text + "' was seen " + imagesAffected.ToString() + " files back." + Environment.NewLine;
            messageBox.Message.Reason += "\u2022 That field's value will be copied across all files between that file and this one in the selection";
            messageBox.Message.Result = "If you select yes: " + Environment.NewLine;
            messageBox.Message.Result = "\u2022 " + imagesAffected.ToString() + " files will be affected.";
            return messageBox.ShowDialog();
        }

        // enable or disable context menu items
        protected virtual void Container_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            StackPanel stackPanel = (StackPanel)sender;
            DataEntryControl control = (DataEntryControl)stackPanel.Tag;

            MenuItem menuItemCopyForward = (MenuItem)stackPanel.ContextMenu.Items[DataEntryHandler.CopyForwardMenuIndex];
            menuItemCopyForward.IsEnabled = this.IsCopyForwardPossible(control);
            MenuItem menuItemPropagateFromLastValue = (MenuItem)stackPanel.ContextMenu.Items[DataEntryHandler.PropagateFromLastValueMenuIndex];
            menuItemPropagateFromLastValue.IsEnabled = this.IsCopyFromLastNonEmptyValuePossible(control);
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
            control.SetContentAndTooltip(textBox.Text);
            this.FileDatabase.UpdateFile(this.ImageCache.Current.ID, control.DataLabel, control.Content);
            return;
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
            control.SetContentAndTooltip(comboBox.SelectedItem.ToString());
            this.FileDatabase.UpdateFile(this.ImageCache.Current.ID, control.DataLabel, control.Content);
        }

        private void DateTimeControl_ValueChanged(DateTimeOffsetPicker dateTimePicker, DateTimeOffset newDateTime)
        {
            if (this.IsProgrammaticControlUpdate)
            {
                return;
            }

            // update file data table and write the new DateTime to the database
            this.ImageCache.Current.SetDateTimeOffset(newDateTime);
            dateTimePicker.ToolTip = newDateTime.ToString(dateTimePicker.Format);

            List<ColumnTuplesWithWhere> imageToUpdate = new List<ColumnTuplesWithWhere>() { this.ImageCache.Current.GetDateTimeColumnTuples() };
            this.FileDatabase.UpdateFiles(imageToUpdate);
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
            control.SetContentAndTooltip(value);
            this.FileDatabase.UpdateFile(this.ImageCache.Current.ID, control.DataLabel, control.Content);
            return;
        }

        // Menu selections for propagating or copying the current value of this control to all images
        protected virtual void MenuItemPropagateFromLastValue_Click(object sender, RoutedEventArgs e)
        {
            DataEntryControl control = (DataEntryControl)((MenuItem)sender).Tag;
            control.SetContentAndTooltip(this.CopyFromLastNonEmptyValue(control));
        }

        // Copy the current value of this control to all images
        protected virtual void MenuItemCopyCurrentValue_Click(object sender, RoutedEventArgs e)
        {
            DataEntryControl control = (DataEntryControl)((MenuItem)sender).Tag;
            this.CopyToAll(control);
        }

        // Propagate the current value of this control forward from this point across the current selection
        protected virtual void MenuItemPropagateForward_Click(object sender, RoutedEventArgs e)
        {
            DataEntryControl control = (DataEntryControl)((MenuItem)sender).Tag;
            this.CopyForward(control, control is DataEntryCounter);
        }

        // Whenever the text in a particular note box changes, update the particular note field in the database 
        private void NoteControl_TextAutocompleted(object sender, TextChangedEventArgs e)
        {
            if (this.IsProgrammaticControlUpdate)
            {
                return;
            }

            // update control state write current value to the database
            DataEntryNote control = (DataEntryNote)((TextBox)sender).Tag;
            control.ContentChanged = true;

            this.FileDatabase.UpdateFile(this.ImageCache.Current.ID, control.DataLabel, control.Content);
            this.IsProgrammaticControlUpdate = false;
        }

        private void SetPropagateContextMenu(DataEntryControl control)
        {
            MenuItem menuItemPropagateFromLastValue = new MenuItem();
            menuItemPropagateFromLastValue.IsCheckable = false;
            menuItemPropagateFromLastValue.Header = "Propagate from the _last non-empty value to here...";
            if (control is DataEntryCounter)
            {
                menuItemPropagateFromLastValue.Header = "Propagate from the _last non-zero value to here...";
            }
            menuItemPropagateFromLastValue.Click += this.MenuItemPropagateFromLastValue_Click;
            menuItemPropagateFromLastValue.Tag = control;

            MenuItem menuItemCopyForward = new MenuItem();
            menuItemCopyForward.IsCheckable = false;
            menuItemCopyForward.Header = "Copy forward to _end...";
            menuItemCopyForward.ToolTip = "The value of this field will be copied forward from this file to the last file in this set";
            menuItemCopyForward.Click += this.MenuItemPropagateForward_Click;
            menuItemCopyForward.Tag = control;

            MenuItem menuItemCopyCurrentValue = new MenuItem();
            menuItemCopyCurrentValue.IsCheckable = false;
            menuItemCopyCurrentValue.Header = "Copy to _all...";
            menuItemCopyCurrentValue.Click += this.MenuItemCopyCurrentValue_Click;
            menuItemCopyCurrentValue.Tag = control;

            // DataEntrHandler.PropagateFromLastValueIndex and CopyForwardIndex must be kept in sync with the add order here
            ContextMenu menu = new ContextMenu();
            menu.Items.Add(menuItemPropagateFromLastValue);
            menu.Items.Add(menuItemCopyForward);
            menu.Items.Add(menuItemCopyCurrentValue);

            control.Container.ContextMenu = menu;
            control.Container.PreviewMouseRightButtonDown += this.Container_PreviewMouseRightButtonDown;

            if (control is DataEntryCounter)
            {
                DataEntryCounter counter = (DataEntryCounter)control;
                counter.ContentControl.ContextMenu = menu;
            }
            else if (control is DataEntryNote)
            {
                DataEntryNote note = (DataEntryNote)control;
                note.ContentControl.ContextMenu = menu;
            }
            else if (control is DataEntryChoice)
            {
                DataEntryChoice choice = (DataEntryChoice)control;
                choice.ContentControl.ContextMenu = menu;
            }
            else if (control is DataEntryFlag)
            {
                DataEntryFlag flag = (DataEntryFlag)control;
                flag.ContentControl.ContextMenu = menu;
            }
            else
            {
                throw new NotSupportedException(String.Format("Unhandled control type {0}.", control.GetType().Name));
            }
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

            List<ColumnTuplesWithWhere> imageToUpdate = new List<ColumnTuplesWithWhere>() { this.ImageCache.Current.GetDateTimeColumnTuples() };
            this.FileDatabase.UpdateFiles(imageToUpdate);  // write the new UtcOffset to the database
        }
    }
}
