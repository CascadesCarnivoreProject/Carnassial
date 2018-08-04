using Carnassial.Command;
using Carnassial.Control;
using Carnassial.Data;
using Carnassial.Database;
using Carnassial.Dialog;
using Carnassial.Github;
using Carnassial.Images;
using Carnassial.Native;
using Carnassial.Util;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Speech.Synthesis;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using DialogResult = System.Windows.Forms.DialogResult;
using MessageBox = Carnassial.Dialog.MessageBox;
using SaveFileDialog = System.Windows.Forms.SaveFileDialog;

namespace Carnassial
{
    /// <summary>
    /// main window for Carnassial
    /// </summary>
    public partial class CarnassialWindow : WindowWithSystemMenu, IDisposable
    {
        private bool disposed;
        private SpeechSynthesizer speechSynthesizer;

        public DataEntryHandler DataHandler { get; private set; }
        public CarnassialState State { get; private set; }

        public CarnassialWindow()
        {
            AppDomain.CurrentDomain.UnhandledException += this.OnUnhandledException;
            this.InitializeComponent();

            this.speechSynthesizer = new SpeechSynthesizer();
            this.State = new CarnassialState();
            this.Title = Constant.MainWindowBaseTitle;

            // recall user's state from prior sessions
            this.State.ReadFromRegistry();

            this.MenuOptionsAudioFeedback.IsChecked = this.State.AudioFeedback;
            this.MenuOptionsEnableCsvImportPrompt.IsChecked = !this.State.SuppressSpreadsheetImportPrompt;
            this.MenuOptionsOrderFilesByDateTime.IsChecked = this.State.OrderFilesByDateTime;
            this.MenuOptionsSkipDarkFileCheck.IsChecked = this.State.SkipDarkImagesCheck;

            // timer callback so the display will update to the current slider position when the user pauses whilst dragging the slider 
            this.State.FileNavigatorSliderTimer.Tick += this.FileNavigatorSlider_TimerTick;
            this.State.Throttles.FilePlayTimer.Tick += this.FilePlay_TimerTick;

            // populate lists of menu items
            for (int analysisSlot = 0; analysisSlot < Constant.AnalysisSlots; ++analysisSlot)
            {
                int displaySlot = analysisSlot + 1;

                MenuItem copyToAnalysisSlot = new MenuItem();
                copyToAnalysisSlot.Click += this.MenuEditCopyValuesToAnalysis_Click;
                copyToAnalysisSlot.Header = String.Format("Analysis _{0}", displaySlot);
                copyToAnalysisSlot.Icon = new Image() { Source = Constant.Images.Copy.Value };
                copyToAnalysisSlot.InputGestureText = String.Format("Ctrl+{0}", displaySlot);
                copyToAnalysisSlot.Tag = analysisSlot;
                copyToAnalysisSlot.ToolTip = String.Format("Copy data entered for the current file analysis number {0}.", displaySlot);
                this.MenuEditCopyValuesToAnalysis.Items.Add(copyToAnalysisSlot);

                MenuItem pasteFromAnalysisSlot = new MenuItem();
                pasteFromAnalysisSlot.Click += this.MenuEditPasteFromAnalysis_Click;
                pasteFromAnalysisSlot.Icon = new Image() { Source = Constant.Images.Paste.Value };
                pasteFromAnalysisSlot.InputGestureText = String.Format("{0}", displaySlot);
                pasteFromAnalysisSlot.IsEnabled = false;
                pasteFromAnalysisSlot.Header = String.Format("Analysis _{0}", displaySlot);
                pasteFromAnalysisSlot.Tag = analysisSlot;
                pasteFromAnalysisSlot.ToolTip = String.Format("Apply data in analysis {0}.", displaySlot);
                this.MenuEditPasteValuesFromAnalysis.Items.Add(pasteFromAnalysisSlot);
            }
            this.MenuFileRecentImageSets_Refresh();

            this.Top = this.State.CarnassialWindowPosition.Y;
            this.Left = this.State.CarnassialWindowPosition.X;
            this.Height = this.State.CarnassialWindowPosition.Height;
            this.Width = this.State.CarnassialWindowPosition.Width;
            Utilities.TryFitWindowInWorkingArea(this);

            this.FileDisplay.Display(Constant.Images.NoSelectableFileMessage);
        }

        private string FolderPath
        {
            get
            {
                Debug.Assert(this.IsFileDatabaseAvailable(), "State management failure: attempt to obtain folder path when database is unavailable.");
                return this.DataHandler.FileDatabase.FolderPath;
            }
        }

        private void AddCommand(UndoableCommand<CarnassialWindow> command)
        {
            this.State.UndoRedoChain.AddCommand(command);
            this.MenuEditRedo.IsEnabled = this.State.UndoRedoChain.CanRedo;
            this.MenuEditUndo.IsEnabled = this.State.UndoRedoChain.CanUndo;
        }

        private void ClearStatusMessage()
        {
            this.MessageBar.Text = String.Empty;
        }

        private async Task CloseImageSetAsync()
        {
            if (this.IsFileDatabaseAvailable())
            {
                // persist image set properties if an image set has been opened
                if (this.DataHandler.FileDatabase.CurrentlySelectedFileCount > 0)
                {
                    // revert to custom selections to all 
                    if (this.DataHandler.FileDatabase.ImageSet.FileSelection == FileSelection.Custom)
                    {
                        this.DataHandler.FileDatabase.ImageSet.FileSelection = FileSelection.All;
                    }

                    // sync image set properties
                    if (this.FileDisplay != null)
                    {
                        this.DataHandler.FileDatabase.ImageSet.Options.SetFlag(ImageSetOptions.Magnifier, this.FileDisplay.MagnifyingGlassEnabled);
                    }

                    if (this.IsFileAvailable())
                    {
                        this.DataHandler.FileDatabase.ImageSet.MostRecentFileID = this.DataHandler.ImageCache.Current.ID;
                    }

                    // write image set properties to the database
                    this.DataHandler.FileDatabase.TrySyncImageSetToDatabase();

                    // ensure custom filter operator is synchronized in state for writing to user's registry
                    this.State.CustomSelectionTermCombiningOperator = this.DataHandler.FileDatabase.CustomSelection.TermCombiningOperator;
                }

                // discard the image set and reset UX for no open image set/no selected files
                // Controls are cleared after the call to ShowFileAsync() so that the show file can clear any data context set on the controls before they're
                // discarded.
                this.DataHandler.Dispose();
                this.DataHandler = null;
                this.EnableOrDisableMenusAndControls();
                await this.ShowFileAsync(Constant.Database.InvalidRow, false);
                this.DataEntryControls.Clear();

                // return to instuction tab
                this.MenuViewShowInstructions_Click(this, null);
            }

            this.State.ResetImageSetRelatedState();
            // reset undo/redo after reset of main state object as it touches some of the same state but also updates the enable state of undo/rendo menu items
            this.ResetUndoRedoState();
            this.SetStatusMessage("No image set is open.");
        }

        /// <summary>When the user selects a counter update the color and emphasis of its markers.</summary>
        private void DataEntryCounter_Click(object sender, RoutedEventArgs e)
        {
            this.RefreshDisplayedMarkers();
        }

        /// <summary>Highlight markers associated with a counter when the mouse enters the counter.</summary>
        private void DataEntryCounter_MouseEnter(object sender, MouseEventArgs e)
        {
            Panel panel = (Panel)sender;
            this.State.MouseOverCounter = ((DataEntryCounter)panel.Tag).DataLabel;
            this.RefreshDisplayedMarkers();
        }

        /// <summary>Remove marker highlighting when the mouse leaves a counter.</summary>
        private void DataEntryCounter_MouseLeave(object sender, MouseEventArgs e)
        {
            this.State.MouseOverCounter = null;
            this.RefreshDisplayedMarkers();
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
                if (this.DataHandler != null)
                {
                    this.DataHandler.Dispose();
                }
                this.speechSynthesizer.Dispose();
            }

            this.disposed = true;
        }

        private void EnableOrDisableMenusAndControls()
        {
            bool imageSetAvailable = this.IsFileDatabaseAvailable();
            bool filesSelected = false;
            if (imageSetAvailable)
            {
                filesSelected = this.DataHandler.FileDatabase.CurrentlySelectedFileCount > 0;
            }

            // enable/disable menus and menu items as appropriate depending upon whether files are selected
            // file menu
            this.MenuFileAddFilesToImageSet.IsEnabled = imageSetAvailable;
            this.MenuFileLoadImageSet.IsEnabled = !imageSetAvailable;
            this.MenuFileRecentImageSets.IsEnabled = !imageSetAvailable;
            this.MenuFileCloseImageSet.IsEnabled = imageSetAvailable;
            this.MenuFileCloneCurrent.IsEnabled = filesSelected;
            this.MenuFileExport.IsEnabled = filesSelected;
            this.MenuFileImport.IsEnabled = imageSetAvailable;
            this.MenuFileMoveFiles.IsEnabled = filesSelected;
            this.MenuFileRenameFileDatabase.IsEnabled = filesSelected;
            // edit menu
            this.MenuEdit.IsEnabled = filesSelected;
            this.MenuEditDeleteCurrentFile.IsEnabled = filesSelected;
            // view menu
            this.MenuView.IsEnabled = filesSelected;
            // select menu
            this.MenuSelect.IsEnabled = filesSelected;
            // options menu
            // always enable at top level when an image set exists so that image set advanced options are accessible
            this.MenuOptions.IsEnabled = imageSetAvailable;
            this.MenuOptionsAudioFeedback.IsEnabled = filesSelected;
            this.MenuOptionsDisplayMagnifier.IsChecked = imageSetAvailable && this.DataHandler.FileDatabase.ImageSet.Options.HasFlag(ImageSetOptions.Magnifier);
            this.MenuOptionsDialogsOnOrOff.IsEnabled = filesSelected;
            this.MenuOptionsAdvancedCarnassialOptions.IsEnabled = filesSelected;

            // other UI components
            // If no files are selected there's nothing for the user to do with data entry.
            this.AnalysisButtons.EnableOrDisable(filesSelected, this.State.Analysis);
            this.DataEntryControls.IsEnabled = filesSelected;
            this.FileNavigatorSlider.IsEnabled = filesSelected;
            this.FileDisplay.IsEnabled = filesSelected;
            this.FileDisplay.MagnifyingGlassEnabled = filesSelected && this.DataHandler.FileDatabase.ImageSet.Options.HasFlag(ImageSetOptions.Magnifier);

            if (filesSelected == false)
            {
                this.SetStatusMessage("Image set is empty.");
                this.SetFileCount(0);
            }
        }

        private async void FileNavigatorSlider_DragCompleted(object sender, DragCompletedEventArgs args)
        {
            this.State.FileNavigatorSliderDragging = false;
            await this.ShowFileAsync(this.FileNavigatorSlider);
            this.State.FileNavigatorSliderTimer.Stop();
        }

        private void FileNavigatorSlider_DragStarted(object sender, DragStartedEventArgs args)
        {
            this.State.FileNavigatorSliderTimer.Start(); // The timer forces an image display update to the current slider position if the user pauses longer than the timer's interval. 
            this.State.FileNavigatorSliderDragging = true;
        }

        private void FileNavigatorSlider_EnableOrDisableValueChangedCallback(bool enableCallback)
        {
            if (enableCallback)
            {
                this.FileNavigatorSlider.ValueChanged += this.FileNavigatorSlider_ValueChanged;
            }
            else
            {
                this.FileNavigatorSlider.ValueChanged -= this.FileNavigatorSlider_ValueChanged;
            }
        }

        private void FileNavigatorSlider_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            this.FocusFileDisplay();
        }

        // timer callback that forces image update to the current slider position. Invoked as the user pauses dragging the image slider 
        private async void FileNavigatorSlider_TimerTick(object sender, EventArgs e)
        {
            await this.ShowFileAsync(this.FileNavigatorSlider);
            this.State.FileNavigatorSliderTimer.Stop();
        }

        private async void FileNavigatorSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> args)
        {
            // since the minimum value is 1 there's a value change event during InitializeComponent() to ignore
            if (this.State == null)
            {
                args.Handled = true;
                return;
            }

            DateTime utcNow = DateTime.UtcNow;
            if ((this.State.FileNavigatorSliderDragging == false) || (utcNow - this.State.MostRecentRender > this.State.FileNavigatorSliderTimer.Interval))
            {
                await this.ShowFileAsync(this.FileNavigatorSlider);
                this.State.MostRecentRender = utcNow;
                args.Handled = true;
            }
        }

        private async void FilePlay_TimerTick(object sender, EventArgs e)
        {
            await this.ShowFileWithoutSliderCallbackAsync(true, Keyboard.Modifiers);
            if (this.DataHandler.ImageCache.CurrentRow == (this.DataHandler.FileDatabase.Files.RowCount - 1))
            {
                this.State.FileNavigatorSliderTimer.Stop();
            }
        }

        private void FocusFileDisplay()
        {
            Debug.Assert(this.FileDisplay.FileDisplay.Dock.Focusable, "FileDisplay isn't focusable.");
            this.FileDisplay.FileDisplay.Dock.Focus();
        }

        private void FolderSelectionDialog_FolderChanging(object sender, CommonFileDialogFolderChangeEventArgs e)
        {
            // require folders to be loaded be either the same folder as the .tdb and .ddb or subfolders of it
            if ((e.Folder == null) || (e.Folder.StartsWith(this.FolderPath, StringComparison.OrdinalIgnoreCase) == false))
            {
                // rather than cancel the event, override the selected path
                // This produces a better experience in cases where the user selects a folder that's above the databases as there's a visible response
                // when the current folder is a subfolder.
                e.Folder = this.FolderPath;
            }
        }

        private List<Marker> GetDisplayMarkers()
        {
            List<Marker> markers = new List<Marker>();
            if (this.IsFileAvailable() == false)
            {
                return markers;
            }

            // if no counter is selected that just indicates no markers need to be highlighted at this time
            this.TryGetSelectedCounter(out DataEntryCounter selectedCounter);
            foreach (DataEntryControl control in this.DataEntryControls.ControlsByDataLabel.Values)
            {
                if (control.Type != ControlType.Counter)
                {
                    continue;
                }
                MarkersForCounter markersForCounter = this.DataHandler.ImageCache.Current.GetMarkersForCounter(control.DataLabel);

                // on mouse hover over a counter, emphasize markers associated with it
                DataEntryCounter currentCounter = (DataEntryCounter)control;
                bool emphasize = String.Equals(markersForCounter.DataLabel, this.State.MouseOverCounter, StringComparison.Ordinal);
                bool highlight = (selectedCounter != null) && String.Equals(currentCounter.DataLabel, selectedCounter.DataLabel, StringComparison.Ordinal);
                foreach (Marker marker in markersForCounter.Markers)
                {
                    // label markers when they're first created, don't show a label afterwards
                    if (marker.ShowLabel && (marker.LabelShownPreviously == false))
                    {
                        marker.ShowLabel = true;
                        marker.LabelShownPreviously = true;
                    }
                    else
                    {
                        marker.ShowLabel = false;
                    }

                    marker.Emphasize = emphasize;
                    marker.Highlight = highlight;
                    marker.Tooltip = currentCounter.Label.Replace("_", String.Empty);
                    markers.Add(marker);
                }
            }
            return markers;
        }

        private void HideLongRunningOperationFeedback()
        {
            this.LongRunningFeedback.Visibility = Visibility.Collapsed;
            this.FileNavigationGrid.Visibility = Visibility.Visible;
        }

        private async void Instructions_Drop(object sender, DragEventArgs dropEvent)
        {
            if (Utilities.IsSingleTemplateFileDrag(dropEvent, out string templateDatabaseFilePath))
            {
                dropEvent.Handled = await this.TryOpenTemplateAndFileDatabaseAsync(templateDatabaseFilePath);
            }
        }

        private void Instructions_PreviewDrag(object sender, DragEventArgs dragEvent)
        {
            Utilities.OnInstructionsPreviewDrag(dragEvent);
        }

        public bool IsFileAvailable()
        {
            if ((this.DataHandler == null) || (this.DataHandler.ImageCache == null))
            {
                return false;
            }

            return this.DataHandler.ImageCache.IsFileAvailable;
        }

        private bool IsFileDatabaseAvailable()
        {
            if ((this.DataHandler == null) || (this.DataHandler.FileDatabase == null))
            {
                return false;
            }

            return true;
        }

        private void MaybeExecuteMultipleFieldEdit(FileMultipleFieldChange multipleChange)
        {
            if (multipleChange.Changes > 0)
            {
                if (multipleChange.Changes == 1)
                {
                    // in the case where the multiple change reduced to a single field convert to a single change for clarity 
                    FileSingleFieldChange singleChange = multipleChange.AsSingleChange();
                    singleChange.Execute(this);
                    this.AddCommand(singleChange);
                }
                else
                {
                    multipleChange.Execute(this);
                    this.AddCommand(multipleChange);
                }
            }
        }

        private void MaybeShowFileCountsDialog(bool onFileLoading)
        {
            if (onFileLoading && this.State.SuppressFileCountOnImportDialog)
            {
                return;
            }

            Dictionary<FileClassification, int> counts = this.DataHandler.FileDatabase.GetFileCountsByClassification();
            FileCountsByClassification imageStats = new FileCountsByClassification(counts, this);
            if (onFileLoading)
            {
                imageStats.Message.Hint = "\u2022 " + imageStats.Message.Hint + Environment.NewLine + "\u2022 If you check don't show this message again this dialog can be turned back on via the Options menu.";
                imageStats.DontShowAgain.Visibility = Visibility.Visible;
            }
            Nullable<bool> result = imageStats.ShowDialog();
            if (onFileLoading && (result == true) && imageStats.DontShowAgain.IsChecked.HasValue)
            {
                this.State.SuppressFileCountOnImportDialog = imageStats.DontShowAgain.IsChecked.Value;
                this.MenuOptionsEnableFileCountOnImportDialog.IsChecked = !this.State.SuppressFileCountOnImportDialog;
            }
        }

        private void MaybeSpeak(string text)
        {
            if (this.State.AudioFeedback)
            {
                // cancel any speech in progress and say the given text
                this.speechSynthesizer.SpeakAsyncCancelAll();
                this.speechSynthesizer.SpeakAsync(text);
            }
        }

        // Event handler: A marker, as defined in e.Marker, has been either added (if e.IsNew is true) or deleted (if it is false)
        // Depending on which it is, add or delete the tag from the current counter control's list of tags 
        // If its deleted, remove the tag from the current counter control's list of tags
        // Every addition / deletion requires us to:
        // - update the contents of the counter control 
        // - update the data held by the image
        // - update the list of markers held by that counter
        // - regenerate the list of markers used by the markableCanvas
        private void MarkableCanvas_MarkerCreatedOrDeleted(object sender, MarkerCreatedOrDeletedEventArgs e)
        {
            if (this.TryGetSelectedCounter(out DataEntryCounter selectedCounter) == false)
            {
                // mouse logic in MarkableCanvas sends marker create events based on mouse action and has no way of knowing if a counter is selected
                // If no counter's selected there's no marker to create and the event can be ignored.
                return;
            }

            // if this is a newly created marker, fill in marker information not populated at creation time because it's unavailable to MarkableCanvas
            // The counter's label typically contains an underscore to set a hotkey, which should not be included in the marker's tooltip.
            if (e.IsCreation)
            {
                Debug.Assert(e.Marker.DataLabel == null, "Markable canvas unexpectedly sent new marker with data label set.");
                e.Marker.DataLabel = selectedCounter.DataLabel;
                e.Marker.Tooltip = selectedCounter.Label.Replace("_", String.Empty);
            }

            // add or remove the marker
            Debug.Assert(this.IsFileAvailable(), "Marker creation when no file is available.  Is the DataEntryCounter unexpectedly enabled?");
            FileMarkerChange markerChange = new FileMarkerChange(this.DataHandler.ImageCache.Current.ID, e);
            markerChange.Execute(this);
            this.AddCommand(markerChange);

            this.RefreshDisplayedMarkers();
            if (e.IsCreation)
            {
                this.MaybeSpeak(selectedCounter.Label);
            }
        }

        public void RefreshDisplayedMarkers()
        {
            this.FileDisplay.Markers = this.GetDisplayMarkers();
        }

        private void MenuEdit_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            if (this.IsFileAvailable())
            {
                this.MenuEditDeleteCurrentFile.IsEnabled = this.DataHandler.ImageCache.Current.Classification != FileClassification.NoLongerAvailable;
                this.MenuEditDeleteCurrentFileAndData.IsEnabled = true;
            }
            else
            {
                this.MenuEditDeleteCurrentFile.IsEnabled = false;
                this.MenuEditDeleteCurrentFileAndData.IsEnabled = false;
            }

            int deletedFiles = this.DataHandler.FileDatabase.Files.Count(file => file.DeleteFlag == true);
            this.MenuEditDeleteFiles.IsEnabled = deletedFiles > 0;
            this.MenuEditDeleteFilesAndData.IsEnabled = deletedFiles > 0;
        }

        private void MenuEditCopy_Click(object sender, RoutedEventArgs e)
        {
            if (this.IsFileAvailable())
            {
                DataObject clipboardData = new DataObject(this.DataHandler.GetCopyableFieldsFromCurrentFile(this.DataEntryControls.Controls));
                Clipboard.SetDataObject(clipboardData);
            }
        }

        private void MenuEditCopyValuesToAnalysis_Click(object sender, RoutedEventArgs e)
        {
            this.TryCopyValuesToAnalysis((int)((MenuItem)sender).Tag);
        }

        // Correct ambiguous dates dialog (i.e. dates that could be read as either month/day or day/month
        private async void MenuEditCorrectAmbiguousDates_Click(object sender, RoutedEventArgs e)
        {
            DateCorrectAmbiguous ambiguousDateCorrection = new DateCorrectAmbiguous(this.DataHandler.FileDatabase, this);
            if (ambiguousDateCorrection.Abort)
            {
                MessageBox messageBox = new MessageBox("No ambiguous dates found.", this);
                messageBox.Message.Reason = "All of the selected images have unambguous date fields." + Environment.NewLine;
                messageBox.Message.Result = "No corrections needed, and no changes have been made." + Environment.NewLine;
                messageBox.Message.StatusImage = MessageBoxImage.Information;
                messageBox.ShowDialog();
                messageBox.Close();
                return;
            }
            await this.ShowBulkFileEditDialogAsync(ambiguousDateCorrection);
        }

        private async void MenuEditDarkImages_Click(object sender, RoutedEventArgs e)
        {
            using (DarkImagesThreshold darkThreshold = new DarkImagesThreshold(this.DataHandler.FileDatabase, this.DataHandler.ImageCache, this.State, this))
            {
                await this.ShowBulkFileEditDialogAsync(darkThreshold);
            }
        }

        /// <summary>Correct the date by specifying an offset.</summary>
        private async void MenuEditDateTimeFixedCorrection_Click(object sender, RoutedEventArgs e)
        {
            DateTimeFixedCorrection fixedDateCorrection = new DateTimeFixedCorrection(this.DataHandler.FileDatabase, this.DataHandler.ImageCache, this);
            await this.ShowBulkFileEditDialogAsync(fixedDateCorrection);
        }

        /// <summary>Correct for drifting clock times. Correction applied only to selected files.</summary>
        private async void MenuEditDateTimeLinearCorrection_Click(object sender, RoutedEventArgs e)
        {
            DateTimeLinearCorrection linearDateCorrection = new DateTimeLinearCorrection(this.DataHandler.FileDatabase, this);
            if (linearDateCorrection.Abort)
            {
                MessageBox messageBox = new MessageBox("Can't correct for clock drift.", this);
                messageBox.Message.Problem = "Can't correct for clock drift.";
                messageBox.Message.Reason = "All of the files selected have date/time fields whose contents are not recognizable as dates or times." + Environment.NewLine;
                messageBox.Message.Reason += "\u2022 dates should look like dd-MMM-yyyy e.g., 16-Jan-2016" + Environment.NewLine;
                messageBox.Message.Reason += "\u2022 times should look like HH:mm:ss using 24 hour time e.g., 01:05:30 or 13:30:00";
                messageBox.Message.Result = "Date correction will be aborted and nothing will be changed.";
                messageBox.Message.Hint = "Check the format of your dates and times. You may also want to change your selection if you're not viewing All files.";
                messageBox.Message.StatusImage = MessageBoxImage.Error;
                messageBox.ShowDialog();
                return;
            }
            await this.ShowBulkFileEditDialogAsync(linearDateCorrection);
        }

        /// <summary>Correct for daylight savings time</summary>
        private async void MenuEditDaylightSavingsTimeCorrection_Click(object sender, RoutedEventArgs e)
        {
            if (this.DataHandler.ImageCache.Current.IsDisplayable() == false)
            {
                // Just a corrupted image
                MessageBox messageBox = new MessageBox("Can't correct for daylight savings time.", this);
                messageBox.Message.Problem = "This is a corrupted file.";
                messageBox.Message.Solution = "To correct for daylight savings time, you need to:" + Environment.NewLine;
                messageBox.Message.Solution += "\u2022 be displaying a file with a valid date ";
                messageBox.Message.Solution += "\u2022 where that file should be the one at the daylight savings time threshold.";
                messageBox.ShowDialog();
                return;
            }

            DateDaylightSavingsTimeCorrection daylightSavingsCorrection = new DateDaylightSavingsTimeCorrection(this.DataHandler.FileDatabase, this.DataHandler.ImageCache, this);
            await this.ShowBulkFileEditDialogAsync(daylightSavingsCorrection);
        }

        /// <summary>Soft delete one or more files marked for deletion, and optionally the data associated with those files.</summary>
        private async void MenuEditDeleteFiles_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = (MenuItem)sender;

            // this callback is invoked by DeleteCurrentFile and DeleteFiles
            // The logic therefore branches for removing a single file versus all selected files marked for deletion.
            List<ImageRow> filesToDelete = new List<ImageRow>();
            bool deleteCurrentFileOnly;
            bool deleteFilesAndData;
            if (menuItem.Name.Equals(this.MenuEditDeleteFiles.Name, StringComparison.Ordinal) || menuItem.Name.Equals(this.MenuEditDeleteFilesAndData.Name, StringComparison.Ordinal))
            {
                deleteCurrentFileOnly = false;
                deleteFilesAndData = menuItem.Name.Equals(this.MenuEditDeleteFilesAndData.Name, StringComparison.Ordinal);
                // get files marked for deletion in the current seletion
                filesToDelete.AddRange(this.DataHandler.FileDatabase.Files.Where(file => file.DeleteFlag == true));
            }
            else
            {
                // delete current file
                deleteCurrentFileOnly = true;
                deleteFilesAndData = menuItem.Name.Equals(this.MenuEditDeleteCurrentFileAndData.Name, StringComparison.Ordinal);
                if (this.IsFileAvailable())
                {
                    filesToDelete.Add(this.DataHandler.ImageCache.Current);
                }
            }

            // notify the user if no files are selected for deletion
            // This should be unreachable as the invoking menu item should be disabled.
            if (filesToDelete == null || filesToDelete.Count < 1)
            {
                MessageBox messageBox = new MessageBox("No files are marked for deletion.", this);
                messageBox.Message.Problem = "You are trying to delete files marked for deletion, but no files have their 'Delete?' box checked.";
                messageBox.Message.Hint = "If you have files that you think should be deleted, check their Delete? box.";
                messageBox.Message.StatusImage = MessageBoxImage.Information;
                messageBox.ShowDialog();
                return;
            }

            DeleteFiles deleteFilesDialog = new DeleteFiles(this.DataHandler.FileDatabase, filesToDelete, deleteFilesAndData, deleteCurrentFileOnly, this);
            if (deleteFilesDialog.ShowDialog() == true)
            {
                // cache the current ID and sync the current file to database as it may be invalidated
                long currentFileID = this.DataHandler.ImageCache.Current.ID;
                this.DataHandler.TrySyncCurrentFileToDatabase();
                // the current file might or might not be on the delete list but unlink it from change notification regardless
                // If it's not unlinked and it is deleted it remains on change tracking, resulting in OnFileFieldEdit() potentially receiving unexpected events 
                // during application shutdown as the controls panel is removed.  If the current file is on the delete list invalidating it from the cache sets
                // .Current to null, meaning event unregistry should be done before the TryInvalidate() call below.  C# ignores attempts to remove unregistered 
                // event callbacks, so there's no impact if the current file's not on the delete list; ShowFile() will repeat the remove without any effect and 
                // then restore the handler.
                this.DataHandler.ImageCache.Current.PropertyChanged -= this.OnFileFieldChanged;

                Mouse.OverrideCursor = Cursors.Wait;
                this.DataHandler.DeleteFiles(filesToDelete, deleteFilesAndData);
                if (deleteFilesAndData)
                {
                    // reload the file data table and find and show the file closest to the last one shown
                    await this.SelectFilesAndShowFileAsync(currentFileID, this.DataHandler.FileDatabase.ImageSet.FileSelection);
                }
                else
                {
                    // display the updated properties on the current file or, if data for the current file was dropped, the next one
                    await this.ShowFileAsync(this.DataHandler.FileDatabase.GetFileOrNextFileIndex(currentFileID));
                }
                Mouse.OverrideCursor = null;
            }
        }

        /// <summary>Edit text in the image set log</summary>
        private void MenuEditLog_Click(object sender, RoutedEventArgs e)
        {
            EditLog editImageSetLog = new EditLog(this.DataHandler.FileDatabase.ImageSet.Log, this);
            if (editImageSetLog.ShowDialog() == true)
            {
                this.DataHandler.FileDatabase.ImageSet.Log = editImageSetLog.Log.Text;
                this.DataHandler.FileDatabase.TrySyncImageSetToDatabase();
            }
        }

        private void MenuEditPaste_Click(object sender, RoutedEventArgs e)
        {
            if (this.IsFileAvailable() == false)
            {
                return;
            }

            IDataObject clipboardData = Clipboard.GetDataObject();
            if (clipboardData == null || clipboardData.GetDataPresent(typeof(Dictionary<string, object>)) == false)
            {
                return;
            }
            Dictionary<string, object> valuesFromClipboard = (Dictionary<string, object>)clipboardData.GetData(typeof(Dictionary<string, object>));
            if (valuesFromClipboard == null)
            {
                return;
            }

            this.PasteValuesToCurrentFileWithUndo(new Dictionary<string, object>(valuesFromClipboard, StringComparer.Ordinal));
        }

        private void MenuEditPasteFromAnalysis_Click(object sender, RoutedEventArgs e)
        {
            if (this.IsFileAvailable())
            {
                this.TryPasteValuesFromAnalysis((int)((MenuItem)sender).Tag);
            }
        }

        // populate a data field from metadata (example metadata displayed from the currently selected file)
        private async void MenuEditPopulateFieldFromMetadata_Click(object sender, RoutedEventArgs e)
        {
            if (this.DataHandler.ImageCache.Current.IsDisplayable() == false)
            {
                int firstFileDisplayable = this.DataHandler.FileDatabase.GetCurrentOrNextDisplayableFile(this.DataHandler.ImageCache.CurrentRow);
                if (firstFileDisplayable == -1)
                {
                    // there are no displayable files and thus no metadata to choose, so abort
                    MessageBox messageBox = new MessageBox("Can't populate a data field with image metadata.", this);
                    messageBox.Message.Problem = "Metadata is not available as no file in the image set can be read." + Environment.NewLine;
                    messageBox.Message.Reason += "Carnassial must have at least one valid file in order to get its metadata.  All files are either corrupted or removed.";
                    messageBox.Message.StatusImage = MessageBoxImage.Error;
                    messageBox.ShowDialog();
                    return;
                }
            }

            PopulateFieldWithMetadata populateField = new PopulateFieldWithMetadata(this.DataHandler.FileDatabase, this.DataHandler.ImageCache.Current.GetFilePath(this.FolderPath), this.State.Throttles.GetDesiredProgressUpdateInterval(), this);
            await this.ShowBulkFileEditDialogAsync(populateField);
        }

        internal async void MenuEditRedo_Click(object sender, RoutedEventArgs e)
        {
            if (this.IsFileAvailable() == false)
            {
                return;
            }

            if (this.State.UndoRedoChain.TryMoveToNextRedo(out UndoableCommand<CarnassialWindow> stateToRedo))
            {
                if (stateToRedo.CanExecute(this) == false)
                {
                    throw new InvalidOperationException(String.Format("Cannot redo {0}.", stateToRedo));
                }

                if (stateToRedo.IsAsync)
                {
                    UndoableCommandAsync<CarnassialWindow> stateToUndoAsync = (UndoableCommandAsync<CarnassialWindow>)stateToRedo;
                    await stateToUndoAsync.ExecuteAsync(this);
                }
                else
                {
                    stateToRedo.Execute(this);
                }

                this.MenuEditRedo.IsEnabled = this.State.UndoRedoChain.CanRedo;
                this.MenuEditUndo.IsEnabled = this.State.UndoRedoChain.CanUndo;
                this.SetStatusMessage("Redid " + stateToRedo.ToString() + ".");
            }
        }

        private async void MenuEditRereadDateTimesFromFiles_Click(object sender, RoutedEventArgs e)
        {
            DateTimeRereadFromFiles rereadDates = new DateTimeRereadFromFiles(this.DataHandler.FileDatabase, this.State.Throttles.GetDesiredProgressUpdateInterval(), this);
            await this.ShowBulkFileEditDialogAsync(rereadDates);
        }

        private void MenuEditResetValues_Click(object sender, RoutedEventArgs e)
        {
            if (this.IsFileAvailable() == false)
            {
                return;
            }

            Dictionary<string, object> defaultValues = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (ControlRow control in this.DataHandler.FileDatabase.Controls)
            {
                if (control.Copyable)
                {
                    defaultValues.Add(control.DataLabel, control.DefaultValue);
                }
            }
            this.MaybeExecuteMultipleFieldEdit(new FileMultipleFieldChange(this.DataHandler.ImageCache, defaultValues));
        }

        private async void MenuEditSetTimeZone_Click(object sender, RoutedEventArgs e)
        {
            DateTimeSetTimeZone setTimeZone = new DateTimeSetTimeZone(this.DataHandler.FileDatabase, this.DataHandler.ImageCache, this);
            await this.ShowBulkFileEditDialogAsync(setTimeZone);
        }

        private async void MenuEditToggleCurrentFileDeleteFlag_Click(object sender, RoutedEventArgs e)
        {
            await this.ToggleCurrentFileDeleteFlagAsync();
        }

        internal async void MenuEditUndo_Click(object sender, RoutedEventArgs e)
        {
            if (this.IsFileAvailable() == false)
            {
                return;
            }

            if (this.State.UndoRedoChain.TryMoveToNextUndo(out UndoableCommand<CarnassialWindow> stateToUndo))
            {
                if (stateToUndo.CanUndo(this) == false)
                {
                    throw new InvalidOperationException(String.Format("Cannot undo {0}.", stateToUndo));
                }

                if (stateToUndo.IsAsync)
                {
                    UndoableCommandAsync<CarnassialWindow> stateToUndoAsync = (UndoableCommandAsync<CarnassialWindow>)stateToUndo;
                    await stateToUndoAsync.UndoAsync(this);
                }
                else
                {
                    stateToUndo.Undo(this);
                }

                this.MenuEditRedo.IsEnabled = this.State.UndoRedoChain.CanRedo;
                this.MenuEditUndo.IsEnabled = this.State.UndoRedoChain.CanUndo;
                this.SetStatusMessage("Undid " + stateToUndo.ToString() + ".");
            }
        }

        private void MenuFile_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            this.MenuFileRecentImageSets_Refresh();
        }

        private async void MenuFileAddFilesToImageSet_Click(object sender, RoutedEventArgs e)
        {
            if (this.ShowFolderSelectionDialog(out IEnumerable<string> folderPaths))
            {
                await this.TryAddFilesAsync(folderPaths);
            }
        }

        /// <summary>
        /// Make a copy of the current file in the folder selected by the user and provide feedback in the status.
        /// </summary>
        private void MenuFileCloneCurrent_Click(object sender, RoutedEventArgs e)
        {
            string sourcePath = this.DataHandler.ImageCache.Current.GetFilePath(this.FolderPath);
            if (File.Exists(sourcePath) == false)
            {
                MessageBox messageBox = new MessageBox("Unable to copy this file.", this);
                messageBox.Message.StatusImage = MessageBoxImage.Error;
                messageBox.Message.Problem = "Carnassial can't copy the current file.";
                messageBox.Message.Reason = String.Format("The file '{0}' was not found.", sourcePath);
                messageBox.Message.Solution = "Make sure you have navigated to a file which is present on disk before you try to export it.";
                messageBox.ShowDialog();
                return;
            }

            string sourceFileName = this.DataHandler.ImageCache.Current.FileName;

            SaveFileDialog dialog = new SaveFileDialog()
            {
                Title = "Make a copy of the currently displayed file",
                Filter = String.Format("*{0}|*{0}", Path.GetExtension(this.DataHandler.ImageCache.Current.FileName)),
                FileName = sourceFileName,
                OverwritePrompt = true
            };

            DialogResult result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                // Set the source and destination file names, including the complete path
                string destinationPath = dialog.FileName;

                // Try to copy the source file to the destination, overwriting the destination file if it already exists.
                // And giving some feedback about its success (or failure) 
                try
                {
                    File.Copy(sourcePath, destinationPath, true);
                    this.SetStatusMessage(sourceFileName + " copied to " + destinationPath);
                }
                catch (Exception exception)
                {
                    Debug.Fail(String.Format("Copy of '{0}' to '{1}' failed.", sourceFileName, destinationPath), exception.ToString());
                    this.SetStatusMessage("Copy failed with {0}.", exception.GetType().Name);
                }
            }
        }

        private async void MenuFileCloseImageSet_Click(object sender, RoutedEventArgs e)
        {
            await this.CloseImageSetAsync();
            this.MenuViewShowInstructions_Click(this, null);
        }

        private async void MenuFileLoadImageSet_Click(object sender, RoutedEventArgs e)
        {
            if (this.TryGetTemplatePath(out string templateDatabaseFilePath))
            {
                await this.TryOpenTemplateAndFileDatabaseAsync(templateDatabaseFilePath);
            }
        }

        private async void MenuFileMoveFiles_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog folderSelectionDialog = new CommonOpenFileDialog()
            {
                Title = "Select the folder to move files to...",
                DefaultDirectory = this.FolderPath,
                InitialDirectory = this.FolderPath,
                IsFolderPicker = true
            };
            folderSelectionDialog.FolderChanging += this.FolderSelectionDialog_FolderChanging;

            if (folderSelectionDialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                // flush any pending changes so MoveFilesToFolder() has clean state
                this.DataHandler.TrySyncCurrentFileToDatabase();

                // move files
                List<string> immovableFiles = this.DataHandler.FileDatabase.MoveSelectedFilesToFolder(folderSelectionDialog.FileName);
                this.SetStatusMessage("Moved {0} of {1} files to {2}.", this.DataHandler.FileDatabase.CurrentlySelectedFileCount - immovableFiles.Count, this.DataHandler.FileDatabase.CurrentlySelectedFileCount, Path.GetFileName(folderSelectionDialog.FileName));
                if (immovableFiles.Count > 0)
                {
                    MessageBox messageBox = new MessageBox("Not all files could be moved.", this);
                    messageBox.Message.Title = "Conflicts prevented some files from being moved.";
                    messageBox.Message.What = String.Format("{0} of {1} files were moved.", this.DataHandler.FileDatabase.CurrentlySelectedFileCount - immovableFiles.Count, this.DataHandler.FileDatabase.CurrentlySelectedFileCount);
                    messageBox.Message.Reason = "This occurs when the selection contains multiple files with the same name or files with the same name are already present in the destination folder.";
                    messageBox.Message.Solution = "Remove or rename the conflicting files and apply the move command again to move the remaining files.";
                    messageBox.Message.Result = "Carnassial moved the files which could be moved.  The remaining files were left in place.";
                    messageBox.Message.Hint = String.Format("The {0} files which could not be moved are{1}", immovableFiles.Count, Environment.NewLine);
                    foreach (string fileName in immovableFiles)
                    {
                        messageBox.Message.Hint += String.Format("\u2022 {0}{1}", fileName, Environment.NewLine);
                    }
                    messageBox.ShowDialog();
                }

                // refresh the current file to show its new relative path field 
                await this.ShowFileAsync(this.DataHandler.ImageCache.CurrentRow, false);

                // clear undo/redo state as bulk edits aren't undoable
                this.OnBulkEdit(this, null);
            }
        }

        /// <summary>Write the .csv or .xlsx file and maybe send an open command to the system</summary>
        private async void MenuFileExportSpreadsheet_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = (MenuItem)sender;
            bool exportXlsx = (sender == this.MenuFileExportXlsxAndOpen) || (sender == this.MenuFileExportXlsx);
            bool openFile = (sender == this.MenuFileExportXlsxAndOpen) || (sender == this.MenuFileExportCsvAndOpen);

            string spreadsheetFileExtension = exportXlsx ? Constant.File.ExcelFileExtension : Constant.File.CsvFileExtension;
            string spreadsheetFileName = Path.GetFileNameWithoutExtension(this.DataHandler.FileDatabase.FileName) + spreadsheetFileExtension;
            string spreadsheetFilePath = Path.Combine(this.FolderPath, spreadsheetFileName);

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            this.ShowLongRunningOperationFeedback();
            this.SetStatusMessage("Exporting spreadsheet...");
            SpreadsheetReaderWriter spreadsheetWriter = new SpreadsheetReaderWriter(this.UpdateSpreadsheetProgress, this.State.Throttles.GetDesiredProgressUpdateInterval());
            try
            {
                await Task.Run(() =>
                {
                    // backup any existing file as it's overwritten on export
                    FileBackup.TryCreateBackup(this.FolderPath, spreadsheetFileName);

                    if (exportXlsx)
                    {
                        spreadsheetWriter.ExportFileDataToXlsx(this.DataHandler.FileDatabase, spreadsheetFilePath);
                    }
                    else
                    {
                        spreadsheetWriter.ExportFileDataToCsv(this.DataHandler.FileDatabase, spreadsheetFilePath);
                    }
                    stopwatch.Stop();

                    if (openFile)
                    {
                        // show the exported file in whatever program is associated with its extension
                        Process process = new Process();
                        process.StartInfo.UseShellExecute = true;
                        process.StartInfo.RedirectStandardOutput = false;
                        process.StartInfo.FileName = spreadsheetFilePath;
                        process.Start();
                    }
                });
            }
            catch (IOException exception)
            {
                MessageBox messageBox = new MessageBox("Can't write the spreadsheet file.", this);
                messageBox.Message.StatusImage = MessageBoxImage.Error;
                messageBox.Message.Problem = "The following file can't be written: " + spreadsheetFilePath;
                messageBox.Message.Reason = "You may already have it open in Excel or another application.";
                messageBox.Message.Solution = "If the file is open in another application, close it and try again.";
                messageBox.Message.Hint = String.Format("{0}: {1}", exception.GetType().FullName, exception.Message);
                messageBox.ShowDialog();
                return;
            }
            finally
            {
                this.HideLongRunningOperationFeedback();
            }

            this.SetStatusMessage("Data exported to {0} in {1:0.000}s ({2:0} files/second).", spreadsheetFileName, stopwatch.Elapsed.TotalSeconds, this.DataHandler.FileDatabase.CurrentlySelectedFileCount / stopwatch.Elapsed.TotalSeconds);
        }

        private async void MenuFileImportSpreadsheet_Click(object sender, RoutedEventArgs e)
        {
            if (this.State.SuppressSpreadsheetImportPrompt == false)
            {
                MessageBox messageBox = new MessageBox("How importing spreadsheet data works.", this, MessageBoxButton.OKCancel);
                messageBox.Message.What = "Importing data from .csv (comma separated value) and .xslx (Excel) files follows the rules below.";
                messageBox.Message.Reason = "Carnassial requires the file follow a specific format and processes its data in a specific way.";
                messageBox.Message.Solution = "Modifying and importing a spreadsheet is supported only if the file is exported from and then back into image set with the same template." + Environment.NewLine;
                messageBox.Message.Solution += "A limited set of modifications is allowed:" + Environment.NewLine;
                messageBox.Message.Solution += "\u2022 Counter data must be zero or a positive integer." + Environment.NewLine;
                messageBox.Message.Solution += "\u2022 Flag data must be 'true' or 'false', case insensitive." + Environment.NewLine;
                messageBox.Message.Solution += "\u2022 FixedChoice data must be a string that exactly matches one of the FixedChoice menu options or the field's default value." + Environment.NewLine;
                messageBox.Message.Solution += String.Format("\u2022 DateTime must be in '{0}' format.{1}", Constant.Time.DateTimeDatabaseFormat, Environment.NewLine);
                messageBox.Message.Solution += String.Format("\u2022 UtcOffset must be a floating point number between {0} and {1}, inclusive.{2}", DateTimeHandler.ToDatabaseUtcOffsetString(Constant.Time.MinimumUtcOffset), DateTimeHandler.ToDatabaseUtcOffsetString(Constant.Time.MinimumUtcOffset), Environment.NewLine);
                messageBox.Message.Solution += "Changing these things either doesn't work or is best done with care:" + Environment.NewLine;
                messageBox.Message.Solution += "\u2022 FileName and RelativePath identify the file updates are applied to.  Changing them causes a different file to be updated or a new file to be added." + Environment.NewLine;
                messageBox.Message.Solution += "\u2022 RelativePath is interpreted relative to the spreadsheet file.  Make sure it's in the right place!" + Environment.NewLine;
                messageBox.Message.Solution += "\u2022 Column names can be swapped to assign data to different fields." + Environment.NewLine;
                messageBox.Message.Solution += "\u2022 Adding, removing, or otherwise changing columns is not supported." + Environment.NewLine;
                messageBox.Message.Solution += String.Format("\u2022 Using a worksheet with a name other than '{0}' in .xlsx files is not supported.{1}", Constant.Excel.FileDataWorksheetName, Environment.NewLine);
                messageBox.Message.Result = String.Format("Carnassial will create a backup .ddb file in the {0} folder and then import as much data as it can.  If data can't be imported you'll get a dialog listing the problems.", Constant.File.BackupFolder);
                messageBox.Message.Hint = "\u2022 After you import, check your data. If it is not what you expect, restore your data by using that backup file." + Environment.NewLine;
                messageBox.Message.Hint += String.Format("\u2022 Usually the spreadsheet should be in the same folder as the data file ({0}) it was exported from.{1}", Constant.File.FileDatabaseFileExtension, Environment.NewLine);
                messageBox.Message.Hint += "\u2022 If you check 'Don't show this message' this dialog can be turned back on via the Options menu.";
                messageBox.Message.StatusImage = MessageBoxImage.Information;
                messageBox.DontShowAgain.Visibility = Visibility.Visible;

                if (messageBox.ShowDialog() != true)
                {
                    return;
                }

                if (messageBox.DontShowAgain.IsChecked.HasValue)
                {
                    this.State.SuppressSpreadsheetImportPrompt = messageBox.DontShowAgain.IsChecked.Value;
                    this.MenuOptionsEnableCsvImportPrompt.IsChecked = !this.State.SuppressSpreadsheetImportPrompt;
                }
            }

            string defaultSpreadsheetFileName = Path.GetFileNameWithoutExtension(this.DataHandler.FileDatabase.FileName) + Constant.File.ExcelFileExtension;
            if (Utilities.TryGetFileFromUser("Select a file to merge into the current image set",
                                             Path.Combine(this.DataHandler.FileDatabase.FolderPath, defaultSpreadsheetFileName),
                                             String.Format("Spreadsheet files (*{0};*{1})|*{0};*{1}", Constant.File.CsvFileExtension, Constant.File.ExcelFileExtension),
                                             out string spreadsheetFilePath) == false)
            {
                return;
            }

            // create a backup database file
            if (FileBackup.TryCreateBackup(this.DataHandler.FileDatabase.FilePath))
            {
                this.SetStatusMessage("Backup of data file made.");
            }
            else
            {
                this.SetStatusMessage("No data file backup was made.");
            }

            // ensure all files are selected
            // This prevents files which are in the database but not selected from being added a second time.
            FileSelection originalSelection = this.DataHandler.FileDatabase.ImageSet.FileSelection;
            if (originalSelection != FileSelection.All)
            {
                this.DataHandler.FileDatabase.SelectFiles(FileSelection.All);
            }

            Stopwatch stopwatch = new Stopwatch();
            FileImportResult importResult;
            try
            {
                stopwatch.Start();
                this.ShowLongRunningOperationFeedback();
                this.SetStatusMessage("Importing spreadsheet...");

                SpreadsheetReaderWriter spreadsheetReader = new SpreadsheetReaderWriter(this.UpdateSpreadsheetProgress, this.State.Throttles.GetDesiredProgressUpdateInterval());
                importResult = await Task.Run(() =>
                {
                    if (spreadsheetFilePath.EndsWith(Constant.File.ExcelFileExtension, StringComparison.OrdinalIgnoreCase))
                    {
                        return spreadsheetReader.TryImportFileDataFromXlsx(spreadsheetFilePath, this.DataHandler.FileDatabase);
                    }
                    else
                    {
                        return spreadsheetReader.TryImportFileDataFromCsv(spreadsheetFilePath, this.DataHandler.FileDatabase);
                    }
                });
                stopwatch.Stop();

                if (importResult.Errors.Count > 0)
                {
                    MessageBox messageBox = new MessageBox("Spreadsheet import incomplete.", this);
                    messageBox.Message.StatusImage = MessageBoxImage.Error;
                    messageBox.Message.Problem = String.Format("The file {0} could not be fully read.", spreadsheetFilePath);
                    messageBox.Message.Reason = "The spreadsheet is not fully compatible with the current image set.";
                    messageBox.Message.Solution = "Check that:" + Environment.NewLine;
                    messageBox.Message.Solution += "\u2022 The first row of the file is a header line." + Environment.NewLine;
                    messageBox.Message.Solution += "\u2022 The column names in the header line match the database." + Environment.NewLine;
                    messageBox.Message.Solution += "\u2022 Choice values use the correct case." + Environment.NewLine;
                    messageBox.Message.Solution += "\u2022 Counter values are numbers." + Environment.NewLine;
                    messageBox.Message.Solution += "\u2022 Flag values are either 'true' or 'false'.";
                    messageBox.Message.Result = "Either no data was imported or invalid parts of the spreadsheet were skipped.";
                    messageBox.Message.Hint = "The errors encountered were:";
                    foreach (string importError in importResult.Errors)
                    {
                        messageBox.Message.Hint += "\u2022 " + importError;
                    }
                    messageBox.ShowDialog();
                }
            }
            catch (IOException ioException)
            {
                this.SetStatusMessage("Couldn't import spreadsheet.");

                MessageBox messageBox = new MessageBox("Can't import spreadsheet.", this);
                messageBox.Message.StatusImage = MessageBoxImage.Error;
                messageBox.Message.Problem = String.Format("The file {0} either could not be opened or could not be read.", spreadsheetFilePath);
                messageBox.Message.Reason = "Most likely the file is open in another program.";
                messageBox.Message.Solution = "If the file is open in another program, close it.";
                messageBox.Message.Result = String.Format("{0}: {1}", ioException.GetType().FullName, ioException.Message);
                messageBox.Message.Hint = "Is the file open in Excel?";
                messageBox.ShowDialog();
                return;
            }
            finally
            {
                this.HideLongRunningOperationFeedback();
            }

            // reload the in memory file table to pick up newly added files
            // Also triggers an update to the enable/disable state of the user interface to match.
            stopwatch.Start();
            await this.SelectFilesAndShowFileAsync(originalSelection);

            // clear undo/redo state as bulk edits aren't undoable
            this.OnBulkEdit(this, null);
            stopwatch.Stop();

            this.SetStatusMessage("{0} imported in {1:0.000}s: {2} files added, {3} files updated ({4:0} files/second).", Path.GetFileName(spreadsheetFilePath), stopwatch.Elapsed.TotalSeconds, importResult.FilesAdded, importResult.FilesUpdated, (importResult.FilesAdded + importResult.FilesUpdated) / stopwatch.Elapsed.TotalSeconds);
        }

        private async void MenuFileRecentImageSet_Click(object sender, RoutedEventArgs e)
        {
            await this.TryOpenTemplateAndFileDatabaseAsync((string)((MenuItem)sender).ToolTip);
        }

        /// <summary>
        /// Update the list of recent databases displayed under File -> Recent Databases.
        /// </summary>
        private void MenuFileRecentImageSets_Refresh()
        {
            // remove image sets which are no longer present from the most recently used list
            // probably overkill to perform this check on every refresh rather than once at application launch, but it's not particularly expensive
            List<string> invalidPaths = new List<string>();
            foreach (string recentImageSetPath in this.State.MostRecentImageSets)
            {
                if (File.Exists(recentImageSetPath) == false)
                {
                    invalidPaths.Add(recentImageSetPath);
                }
            }

            foreach (string path in invalidPaths)
            {
                bool result = this.State.MostRecentImageSets.TryRemove(path);
                Debug.Assert(result, String.Format("Removal of image set '{0}' no longer present on disk unexpectedly failed.", path));
            }

            // Enable recent image sets only if there are recent sets and the parent menu is also enabled (indicating no image set has been loaded)
            this.MenuFileRecentImageSets.IsEnabled = this.MenuFileLoadImageSet.IsEnabled && this.State.MostRecentImageSets.Count > 0;
            this.MenuFileRecentImageSets.Items.Clear();

            // add menu items most recently used image sets
            int index = 1;
            foreach (string recentImageSetPath in this.State.MostRecentImageSets)
            {
                // Create a menu item for each path
                MenuItem recentImageSetItem = new MenuItem();
                recentImageSetItem.Click += this.MenuFileRecentImageSet_Click;
                recentImageSetItem.Header = String.Format("_{0} {1}", index++, recentImageSetPath);
                recentImageSetItem.ToolTip = recentImageSetPath;
                this.MenuFileRecentImageSets.Items.Add(recentImageSetItem);
            }
        }

        private void MenuFileRenameFileDatabase_Click(object sender, RoutedEventArgs e)
        {
            RenameFileDatabaseFile renameFileDatabase = new RenameFileDatabaseFile(this.DataHandler.FileDatabase.FileName, this);
            if (renameFileDatabase.ShowDialog() == true)
            {
                this.DataHandler.FileDatabase.RenameFile(renameFileDatabase.NewFileName);
            }
        }

        private void MenuFileExit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
            Application.Current.Shutdown();
        }

        /// <summary>Display a message describing the version, etc.</summary> 
        private void MenuHelpAbout_Click(object sender, RoutedEventArgs e)
        {
            About about = new About(this);
            if ((about.ShowDialog() == true) && about.MostRecentCheckForUpdate.HasValue)
            {
                this.State.MostRecentCheckForUpdates = about.MostRecentCheckForUpdate.Value;
            }
        }

        /// <summary>Show advanced Carnassial options</summary>
        private void MenuOptionsAdvancedCarnassialOptions_Click(object sender, RoutedEventArgs e)
        {
            AdvancedCarnassialOptions advancedCarnassialOptions = new AdvancedCarnassialOptions(this.State, this.FileDisplay, this);
            if (advancedCarnassialOptions.ShowDialog() == true)
            {
                // throttle may have changed; update rendering rate
                this.State.FileNavigatorSliderTimer.Interval = this.State.Throttles.DesiredIntervalBetweenRenders;
            }
        }

        /// <summary>Show advanced image set options</summary>
        private void MenuOptionsAdvancedImageSetOptions_Click(object sender, RoutedEventArgs e)
        {
            AdvancedImageSetOptions advancedImageSetOptions = new AdvancedImageSetOptions(this.DataHandler.FileDatabase, this);
            advancedImageSetOptions.ShowDialog();
        }

        private void MenuOptionsAmbiguousDatesDialog_Click(object sender, RoutedEventArgs e)
        {
            this.State.SuppressAmbiguousDatesDialog = !this.State.SuppressAmbiguousDatesDialog;
            this.MenuOptionsEnableAmbiguousDatesDialog.IsChecked = !this.State.SuppressAmbiguousDatesDialog;
        }

        /// <summary>Toggle audio feedback on and off</summary>
        private void MenuOptionsAudioFeedback_Click(object sender, RoutedEventArgs e)
        {
            this.State.AudioFeedback = !this.State.AudioFeedback;
            this.MenuOptionsAudioFeedback.IsChecked = this.State.AudioFeedback;
        }

        private void MenuOptionsEnableCsvImportPrompt_Click(object sender, RoutedEventArgs e)
        {
            this.State.SuppressSpreadsheetImportPrompt = !this.State.SuppressSpreadsheetImportPrompt;
            this.MenuOptionsEnableCsvImportPrompt.IsChecked = !this.State.SuppressSpreadsheetImportPrompt;
        }

        private void MenuOptionsEnableFileCountOnImportDialog_Click(object sender, RoutedEventArgs e)
        {
            this.State.SuppressFileCountOnImportDialog = !this.State.SuppressFileCountOnImportDialog;
            this.MenuOptionsEnableFileCountOnImportDialog.IsChecked = !this.State.SuppressFileCountOnImportDialog;
        }

        internal async void MenuOptionsOrderFilesByDateTime_Click(object sender, RoutedEventArgs e)
        {
            if (this.IsFileDatabaseAvailable() == false)
            {
                return;
            }

            FileOrdering orderingCommand = new FileOrdering(this.DataHandler.ImageCache);
            await orderingCommand.ExecuteAsync(this);
            this.State.UndoRedoChain.AddCommand(orderingCommand);
        }

        private void MenuOptionsSkipDarkFileCheck_Click(object sender, RoutedEventArgs e)
        {
            this.State.SkipDarkImagesCheck = !this.State.SkipDarkImagesCheck;
            this.MenuOptionsSkipDarkFileCheck.IsChecked = this.State.SkipDarkImagesCheck;
        }

        private async void MenuSelectCustom_Click(object sender, RoutedEventArgs e)
        {
            // the first time the custom selection dialog is launched update the DateTime and UtcOffset search terms to the time of the current file
            // Don't need to check CustomSelectionChange.HasChanges() as a change is guaranteed.
            SearchTerm firstDateTimeSearchTerm = this.DataHandler.FileDatabase.CustomSelection.SearchTerms.First(searchTerm => String.Equals(searchTerm.DataLabel, Constant.FileColumn.DateTime, StringComparison.Ordinal));
            if ((DateTimeOffset)firstDateTimeSearchTerm.DatabaseValue == Constant.ControlDefault.DateTimeValue)
            {
                Data.CustomSelection customSelectionInitialSnapshot = new Data.CustomSelection(this.DataHandler.FileDatabase.CustomSelection);
                DateTimeOffset defaultDate = this.DataHandler.ImageCache.Current.DateTimeOffset;
                this.DataHandler.FileDatabase.CustomSelection.SetDateTimesAndOffset(defaultDate);
                this.AddCommand(new CustomSelectionChange(customSelectionInitialSnapshot, this.DataHandler.FileDatabase.CustomSelection));
            }

            // show the dialog and process the results
            Data.CustomSelection customSelectionSnapshot = new Data.CustomSelection(this.DataHandler.FileDatabase.CustomSelection);
            Dialog.CustomSelection customSelectionDialog = new Dialog.CustomSelection(this.DataHandler.FileDatabase, this);
            if (customSelectionDialog.ShowDialog() == true)
            {
                CustomSelectionChange customSelectionChange = new CustomSelectionChange(customSelectionSnapshot, this.DataHandler.FileDatabase.CustomSelection);
                if (customSelectionChange.HasChanges())
                {
                    this.AddCommand(customSelectionChange);
                }
                await this.SelectFilesAndShowFileAsync(FileSelection.Custom);
            }
            else
            {
                // if needed, uncheck the custom selection menu item
                // It's checked automatically by WPF but, as the user cancelled out of custom selection, this isn't correct in cases where the selection 
                // wasn't already custom.
                this.MenuSelectCustom.IsChecked = this.DataHandler.FileDatabase.ImageSet.FileSelection == FileSelection.Custom;
            }
        }

        /// <summary>Show a dialog box telling the user how many images were loaded, etc.</summary>
        public void MenuSelectFileCounts_Click(object sender, RoutedEventArgs e)
        {
            this.MaybeShowFileCountsDialog(false);
        }

        /// <summary>Get the non-custom selection and update the view.</summary>
        private async void MenuSelectFiles_Click(object sender, RoutedEventArgs e)
        {
            // get selection 
            FileSelection selection;
            if (sender == this.MenuSelectAllFiles)
            {
                selection = FileSelection.All;
            }
            else if (sender == this.MenuSelectColorFiles)
            {
                selection = FileSelection.Color;
            }
            else if (sender == this.MenuSelectCorruptedFiles)
            {
                selection = FileSelection.Corrupt;
            }
            else if (sender == this.MenuSelectDarkFiles)
            {
                selection = FileSelection.Dark;
            }
            else if (sender == this.MenuSelectFilesMarkedForDeletion)
            {
                selection = FileSelection.MarkedForDeletion;
            }
            else if (sender == this.MenuSelectFilesNoLongerAvailable)
            {
                selection = FileSelection.NoLongerAvailable;
            }
            else if (sender == this.MenuSelectGreyscaleFiles)
            {
                selection = FileSelection.Greyscale;
            }
            else if (sender == this.MenuSelectVideoFiles)
            {
                selection = FileSelection.Video;
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(sender), String.Format("Unknown sender {0}.", sender));
            }

            await this.SelectFilesAndShowFileAsync(selection);
        }

        private void MenuSelect_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            Dictionary<FileClassification, int> counts = this.DataHandler.FileDatabase.GetFileCountsByClassification();

            this.MenuSelectColorFiles.IsEnabled = counts[FileClassification.Color] > 0;
            this.MenuSelectCorruptedFiles.IsEnabled = counts[FileClassification.Corrupt] > 0;
            this.MenuSelectDarkFiles.IsEnabled = counts[FileClassification.Dark] > 0;
            this.MenuSelectGreyscaleFiles.IsEnabled = counts[FileClassification.Greyscale] > 0;
            this.MenuSelectFilesNoLongerAvailable.IsEnabled = counts[FileClassification.NoLongerAvailable] > 0;
            this.MenuSelectFilesMarkedForDeletion.IsEnabled = this.DataHandler.FileDatabase.GetFileCount(FileSelection.MarkedForDeletion) > 0;
            this.MenuSelectVideoFiles.IsEnabled = counts[FileClassification.Video] > 0;
        }

        private void MenuViewApplyBookmark_Click(object sender, RoutedEventArgs e)
        {
            this.FileDisplay.ApplyBookmark();
        }

        /// <summary>Toggle the magnifier on and off</summary>
        private void MenuViewDisplayMagnifier_Click(object sender, RoutedEventArgs e)
        {
            bool displayMagnifier = this.DataHandler.FileDatabase.ImageSet.Options.HasFlag(ImageSetOptions.Magnifier);
            displayMagnifier = !displayMagnifier;
            this.DataHandler.FileDatabase.ImageSet.Options = this.DataHandler.FileDatabase.ImageSet.Options.SetFlag(ImageSetOptions.Magnifier, displayMagnifier);
            this.MenuOptionsDisplayMagnifier.IsChecked = displayMagnifier;
            this.FileDisplay.MagnifyingGlassEnabled = displayMagnifier;
        }

        /// <summary>View the combined image differences</summary>
        private async void MenuViewDifferencesCombined_Click(object sender, RoutedEventArgs e)
        {
            await this.TryViewCombinedDifferenceAsync();
        }

        /// <summary>Increase the magnification of the magnifying glass by several keyboard steps.</summary>
        private void MenuViewMagnifierIncrease_Click(object sender, RoutedEventArgs e)
        {
            this.FileDisplay.MagnifierZoomIn();
            this.FileDisplay.MagnifierZoomIn();
            this.FileDisplay.MagnifierZoomIn();
        }

        private async void MenuViewGotoFile_Click(object sender, RoutedEventArgs e)
        {
            if (this.IsFileDatabaseAvailable() == false)
            {
                return;
            }

            GoToFile goToFile = new GoToFile(this.DataHandler.ImageCache.CurrentRow, this.DataHandler.FileDatabase.CurrentlySelectedFileCount, this);
            if (goToFile.ShowDialog() == true)
            {
                await this.ShowFileWithoutSliderCallbackAsync(goToFile.FileIndex);
            }
        }

        /// <summary>Decrease the magnification of the magnifying glass by several keyboard steps.</summary>
        private void MenuViewMagnifierDecrease_Click(object sender, RoutedEventArgs e)
        {
            this.FileDisplay.MagnifierZoomOut();
            this.FileDisplay.MagnifierZoomOut();
            this.FileDisplay.MagnifierZoomOut();
        }

        private void MenuViewPlayFiles_Click(object sender, RoutedEventArgs e)
        {
            // if this event doesn't result from a button click, toggle the play files button's state
            if (sender != this.PlayFilesButton)
            {
                this.PlayFilesButton.IsChecked = !this.PlayFilesButton.IsChecked;
            }

            // switch from not playing files to playing files5 or vice versa
            if (this.PlayFilesButton.IsChecked == true)
            {
                this.State.Throttles.FilePlayTimer.Start();
            }
            else
            {
                this.State.Throttles.FilePlayTimer.Stop();
            }
        }

        private void MenuViewPlayVideo_Click(object sender, RoutedEventArgs e)
        {
            this.FileDisplay.TryPlayOrPauseVideo();
        }

        /// <summary>Cycle through next and previous image differences</summary>
        private async void MenuViewPreviousOrNextDifference_Click(object sender, RoutedEventArgs e)
        {
            await this.TryViewPreviousOrNextDifferenceAsync();
        }

        private void MenuViewSetBookmark_Click(object sender, RoutedEventArgs e)
        {
            this.FileDisplay.SetBookmark();
        }

        private async void MenuViewShowFirstFile_Click(object sender, RoutedEventArgs e)
        {
            await this.ShowFileWithoutSliderCallbackAsync(0);
        }

        private void MenuViewShowFiles_Click(object sender, RoutedEventArgs e)
        {
            this.Tabs.SelectedIndex = 1;
            this.FocusFileDisplay();
        }

        private void MenuViewShowInstructions_Click(object sender, RoutedEventArgs e)
        {
            this.Tabs.SelectedIndex = 0;
        }

        private async void MenuViewShowLastFile_Click(object sender, RoutedEventArgs e)
        {
            if (this.IsFileDatabaseAvailable())
            {
                await this.ShowFileWithoutSliderCallbackAsync(this.DataHandler.FileDatabase.CurrentlySelectedFileCount - 1);
            }
        }

        /// <summary>Navigate to the next file in this image set</summary>
        private async void MenuViewShowNextFile_Click(object sender, RoutedEventArgs e)
        {
            await this.ShowFileWithoutSliderCallbackAsync(true, ModifierKeys.None);
        }

        private async void MenuViewShowNextFileControl_Click(object sender, RoutedEventArgs e)
        {
            await this.ShowFileWithoutSliderCallbackAsync(true, ModifierKeys.Control);
        }

        private async void MenuViewShowNextFileControlShift_Click(object sender, RoutedEventArgs e)
        {
            await this.ShowFileWithoutSliderCallbackAsync(true, ModifierKeys.Control | ModifierKeys.Shift);
        }

        private async void MenuViewShowNextFileShift_Click(object sender, RoutedEventArgs e)
        {
            await this.ShowFileWithoutSliderCallbackAsync(true, ModifierKeys.Shift);
        }

        private async void MenuViewShowNextFilePageDown_Click(object sender, RoutedEventArgs e)
        {
            if (this.IsFileDatabaseAvailable())
            {
                int increment = (int)(Constant.PageUpDownNavigationFraction * this.DataHandler.FileDatabase.CurrentlySelectedFileCount);
                await this.ShowFileWithoutSliderCallbackAsync(this.DataHandler.ImageCache.CurrentRow + increment, increment);
            }
        }

        /// <summary>Navigate to the previous file in this image set</summary>
        private async void MenuViewShowPreviousFile_Click(object sender, RoutedEventArgs e)
        {
            await this.ShowFileWithoutSliderCallbackAsync(false, ModifierKeys.None);
        }

        private async void MenuViewShowPreviousFileControl_Click(object sender, RoutedEventArgs e)
        {
            await this.ShowFileWithoutSliderCallbackAsync(false, ModifierKeys.Control);
        }

        private async void MenuViewShowPreviousFileControlShift_Click(object sender, RoutedEventArgs e)
        {
            await this.ShowFileWithoutSliderCallbackAsync(false, ModifierKeys.Control | ModifierKeys.Shift);
        }

        private async void MenuViewShowPreviousFileShift_Click(object sender, RoutedEventArgs e)
        {
            await this.ShowFileWithoutSliderCallbackAsync(false, ModifierKeys.Shift);
        }

        private async void MenuViewShowPreviousFilePageUp_Click(object sender, RoutedEventArgs e)
        {
            if (this.IsFileDatabaseAvailable())
            {
                int increment = -(int)(Constant.PageUpDownNavigationFraction * this.DataHandler.FileDatabase.CurrentlySelectedFileCount);
                await this.ShowFileWithoutSliderCallbackAsync(this.DataHandler.ImageCache.CurrentRow + increment, increment);
            }
        }

        private void MenuViewZoomIn_Click(object sender, RoutedEventArgs e)
        {
            Point mousePosition = Mouse.GetPosition(this.FileDisplay.FileDisplay.Image);
            this.FileDisplay.ZoomIn();
        }

        private void MenuViewZoomOut_Click(object sender, RoutedEventArgs e)
        {
            Point mousePosition = Mouse.GetPosition(this.FileDisplay.FileDisplay.Image);
            this.FileDisplay.ZoomOut();
        }

        private void MenuViewZoomToFit_Click(object sender, RoutedEventArgs e)
        {
            this.FileDisplay.ZoomToFit();
        }

        private void MoveFocusToNextOrPreviousTabPosition(bool moveToPrevious)
        {
            // identify the currently selected control with the most recently selected control as a default if no control is selected
            // The defaulting causes tabbing to resume from the last control the user tabbed to.  This is desirable as pressing enter on a control
            // sets focus to the markable canvas, meaning if defaulting weren't used a user entering data and tabbing through controls would have
            // to tab through controls they've already entered for after each press of enter.  Defaulting lets tabbing pick up seamlessly instead.
            int currentControlIndex = this.State.MostRecentlyFocusedControlIndex;
            IInputElement focusedElement = Keyboard.FocusedElement;
            if (focusedElement != null)
            {
                if (DataEntryHandler.TryFindFocusedControl(focusedElement, out DataEntryControl focusedControl))
                {
                    currentControlIndex = this.DataEntryControls.Controls.IndexOf(focusedControl);
                }
            }

            // if no control is selected and no default from previous tabbing is available then set up the loop below to move to the last control
            if ((currentControlIndex == -1) && moveToPrevious)
            {
                currentControlIndex = this.DataEntryControls.Controls.Count;
            }

            // move to the next or previous control as available
            Func<int, int> incrementOrDecrement;
            if (moveToPrevious)
            {
                incrementOrDecrement = (int index) => { return --index; };
            }
            else
            {
                incrementOrDecrement = (int index) => { return ++index; };
            }

            for (currentControlIndex = incrementOrDecrement(currentControlIndex);
                 currentControlIndex > -1 && currentControlIndex < this.DataEntryControls.Controls.Count;
                 currentControlIndex = incrementOrDecrement(currentControlIndex))
            {
                DataEntryControl control = this.DataEntryControls.Controls[currentControlIndex];
                if (control.ContentReadOnly == false)
                {
                    control.Focus(this);
                    this.State.MostRecentlyFocusedControlIndex = currentControlIndex;
                    return;
                }
            }

            // no control was found so set focus to the slider
            // this has also the desirable side effect of binding the controls into both next and previous loops so that keys can be used to cycle
            // continuously through them
            this.FocusFileDisplay();
            this.State.MostRecentlyFocusedControlIndex = -1;
        }

        private void OnBulkEdit(object sender, EventArgs e)
        {
            // clear undo/redo state as bulk edits aren't undoable
            this.ResetUndoRedoState();
        }

        private async Task OnFileDatabaseOpenedOrFilesAddedAsync(bool filesJustAdded)
        {
            // set the file displayed to the one 
            // - from the previous session with the image set if the .ddb was just opened
            // - displayed prior to adding files to an image set
            long mostRecentFileID = this.DataHandler.FileDatabase.ImageSet.MostRecentFileID;
            if (filesJustAdded)
            {
                if (this.IsFileAvailable())
                {
                    // if this is completion of an add to an existing image set stay on the file, ideally, shown before the import
                    mostRecentFileID = this.DataHandler.ImageCache.Current.ID;
                    // however, the cache doesn't know file loading changed the display image so invalidate to force a redraw
                    // This is heavier weight than desirable, but it occurs infrequently.
                    this.DataHandler.ImageCache.TryInvalidate(mostRecentFileID);
                }

                // reload the in memory copy of the files table
                // Adding files appends them in memory, which is consistent with FileSelection.All and sort by insertion order, but
                // for any other selection and sort the table needs to be rebuilt. For now, always reload all. It may eventually be
                // desirable to move this logic into AddFilesTransaction.Commit().
                await this.SelectFilesAndShowFileAsync(mostRecentFileID, this.DataHandler.FileDatabase.ImageSet.FileSelection, false);
            }
            else
            {
                this.OnFileSelectionChanged();
                await this.ShowFileAsync(this.DataHandler.FileDatabase.GetFileOrNextFileIndex(mostRecentFileID), false);
            }

            // if needed, change to the image set tab
            this.MenuViewShowFiles_Click(this, null);

            // clear undo/redo chain as opening a .ddb or adding files is not an undoable operation
            this.OnBulkEdit(this, null);
        }

        private void OnFileFieldChanged(object sender, PropertyChangedEventArgs fileChange)
        {
            if ((this.DataHandler != null) && this.DataHandler.IsProgrammaticUpdate)
            {
                return;
            }

            string propertyName = fileChange.PropertyName;
            if (fileChange is IndexedPropertyChangedEventArgs<string> indexedChange)
            {
                propertyName = indexedChange.Index;
            }

            object previousValue = this.State.CurrentFileSnapshot[propertyName];
            object newValue = this.DataHandler.ImageCache.Current[propertyName];
            FileSingleFieldChange fileEdit = new FileSingleFieldChange(this.DataHandler.ImageCache.Current.ID, ImageRow.GetDataLabel(propertyName), propertyName, previousValue, newValue, true);
            Debug.Assert((this.State.CurrentFileSnapshot.ContainsKey(propertyName) == false) || (this.State.CurrentFileSnapshot[propertyName] == fileEdit.PreviousValue), String.Format("Change tracking failure: previous value in file snapshot '{0}' does not match the previous value of the edit '{1}'.", this.State.CurrentFileSnapshot[propertyName], fileEdit.PreviousValue));

            if (fileEdit.HasChange())
            {
                this.AddCommand(fileEdit);
                this.State.CurrentFileSnapshot[propertyName] = fileEdit.NewValue;

                DataEntryControl control = this.DataEntryControls.ControlsByDataLabel[fileEdit.DataLabel];
                if (control.Type == ControlType.Note)
                {
                    DataEntryNote noteControl = (DataEntryNote)control;
                    if (noteControl.ContentControl.Autocompletions.Contains((string)fileEdit.NewValue, StringComparer.Ordinal) == false)
                    {
                        // if needed, controls could be removed from the list in cases where a correction returns the field's value to one which is already
                        // a known autocomplete
                        // a closely related case is removal of the old value from the autocomplete list when this was the only file to use the value
                        this.State.NoteControlsWithNewValues.Add(noteControl);
                    }
                }
            }
        }

        private void OnFileSelectionChanged()
        {
            // update status and menu state to reflect what ended up being selected
            FileSelection selection = this.DataHandler.FileDatabase.ImageSet.FileSelection;
            string selectionDescription;
            switch (selection)
            {
                case FileSelection.All:
                    selectionDescription = "(all files)";
                    break;
                case FileSelection.Color:
                    selectionDescription = "color images";
                    break;
                case FileSelection.Corrupt:
                    selectionDescription = "corrupted files";
                    break;
                case FileSelection.Custom:
                    selectionDescription = "(custom selection)";
                    break;
                case FileSelection.Dark:
                    selectionDescription = "dark images";
                    break;
                case FileSelection.Greyscale:
                    selectionDescription = "greyscale images";
                    break;
                case FileSelection.MarkedForDeletion:
                    selectionDescription = "marked for deletion";
                    break;
                case FileSelection.NoLongerAvailable:
                    selectionDescription = "no longer available";
                    break;
                case FileSelection.Video:
                    selectionDescription = "videos";
                    break;
                default:
                    throw new NotSupportedException(String.Format("Unhandled file selection {0}.", selection));
            }

            this.SetFileCount(this.DataHandler.FileDatabase.CurrentlySelectedFileCount);
            this.SetSelection(selectionDescription);

            this.EnableOrDisableMenusAndControls();
            this.MenuSelectAllFiles.IsChecked = selection == FileSelection.All;
            this.MenuSelectColorFiles.IsChecked = selection == FileSelection.Color;
            this.MenuSelectCorruptedFiles.IsChecked = selection == FileSelection.Corrupt;
            this.MenuSelectCustom.IsChecked = selection == FileSelection.Custom;
            this.MenuSelectDarkFiles.IsChecked = selection == FileSelection.Dark;
            this.MenuSelectGreyscaleFiles.IsChecked = selection == FileSelection.Greyscale;
            this.MenuSelectFilesNoLongerAvailable.IsChecked = selection == FileSelection.NoLongerAvailable;
            this.MenuSelectFilesMarkedForDeletion.IsChecked = selection == FileSelection.MarkedForDeletion;
            this.MenuSelectVideoFiles.IsChecked = selection == FileSelection.Video;

            // after a selection change update the file navigatior slider's range and tick space
            this.FileNavigatorSlider_EnableOrDisableValueChangedCallback(false);
            this.FileNavigatorSlider.Maximum = this.DataHandler.FileDatabase.CurrentlySelectedFileCount;  // slider is one based so no - 1 on the count
            Utilities.ConfigureNavigatorSliderTick(this.FileNavigatorSlider);

            this.FileNavigatorSlider_EnableOrDisableValueChangedCallback(true);
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Utilities.ShowExceptionReportingDialog("Carnassial needs to close.", e, this);
        }

        private void PasteAnalysis_Click(object sender, int analsisSlot)
        {
            this.TryPasteValuesFromAnalysis(analsisSlot);
        }

        /// <summary>
        /// Highlight controls for copyable fields when the mouse enters the paste button.
        /// </summary>
        private void PasteButton_MouseEnter(object sender, MouseEventArgs e)
        {
            foreach (DataEntryControl control in this.DataEntryControls.Controls)
            {
                control.HighlightIfCopyable();
            }
        }

        /// <summary>
        /// Remove highlights when the mouse leaves the paste button.
        /// </summary>
        private void PasteButton_MouseLeave(object sender, MouseEventArgs e)
        {
            foreach (DataEntryControl control in this.DataEntryControls.Controls)
            {
                control.RemoveHighlightIfCopyable();
            }
        }

        private void PasteNextValues_Click(object sender, RoutedEventArgs e)
        {
            int nextIndex = this.DataHandler.ImageCache.CurrentRow + 1;
            if (this.DataHandler.FileDatabase.IsFileRowInRange(nextIndex) == false)
            {
                // at last file, so nothing to copy
                return;
            }

            ImageRow nextFile = this.DataHandler.FileDatabase.Files[nextIndex];
            this.PasteValuesToCurrentFileWithUndo(this.DataHandler.GetCopyableFields(nextFile, this.DataEntryControls.Controls));
        }

        private void PastePreviousValues_Click(object sender, RoutedEventArgs e)
        {
            int previousIndex = this.DataHandler.ImageCache.CurrentRow - 1;
            if (this.DataHandler.FileDatabase.IsFileRowInRange(previousIndex) == false)
            {
                // at first file, so nothing to copy
                return;
            }

            ImageRow previousFile = this.DataHandler.FileDatabase.Files[previousIndex];
            this.PasteValuesToCurrentFileWithUndo(this.DataHandler.GetCopyableFields(previousFile, this.DataEntryControls.Controls));
        }

        private void PasteValuesToCurrentFileWithUndo(Dictionary<string, object> values)
        {
            this.MaybeExecuteMultipleFieldEdit(new FileMultipleFieldChange(this.DataHandler.ImageCache, values));
        }

        private void ResetUndoRedoState()
        {
            if (this.IsFileAvailable())
            {
                this.State.CurrentFileSnapshot = this.DataHandler.ImageCache.Current.GetValues();
            }
            else
            {
                this.State.CurrentFileSnapshot.Clear();
            }
            this.State.NoteControlsWithNewValues.Clear();
            this.State.UndoRedoChain.Clear();
            this.MenuEditRedo.IsEnabled = this.State.UndoRedoChain.CanRedo;
            this.MenuEditUndo.IsEnabled = this.State.UndoRedoChain.CanUndo;
        }

        public async Task SelectFilesAndShowFileAsync()
        {
            Debug.Assert(this.IsFileDatabaseAvailable(), "Expected a file database to be available.");
            await this.SelectFilesAndShowFileAsync(this.DataHandler.FileDatabase.ImageSet.FileSelection);
        }

        public async Task SelectFilesAndShowFileAsync(FileSelection selection)
        {
            await this.SelectFilesAndShowFileAsync(this.DataHandler.ImageCache.GetCurrentFileID(), selection);
        }

        public async Task SelectFilesAndShowFileAsync(long fileID, FileSelection selection)
        {
            await this.SelectFilesAndShowFileAsync(fileID, selection, true);
        }

        public async Task SelectFilesAndShowFileAsync(long fileID, FileSelection selection, bool generateUndoRedoCommands)
        {
            // record current selection for eventual insertion in undo/redo chain and change selection
            Debug.Assert(this.IsFileDatabaseAvailable(), "SelectFilesAndShowFile() should not be reachable with a null data handler or database.  Is a menu item wrongly enabled?");
            long previousFileID = this.DataHandler.ImageCache.GetCurrentFileID();
            FileSelection previousSelection = this.DataHandler.FileDatabase.ImageSet.FileSelection;
            this.DataHandler.TrySyncCurrentFileToDatabase();
            this.DataHandler.FileDatabase.SelectFiles(selection);

            // explain to user if their selection has gone empty and revert to all files
            if ((this.DataHandler.FileDatabase.CurrentlySelectedFileCount < 1) && (selection != FileSelection.All))
            {
                // These cases are reached when 
                // 1) datetime modifications result in no files matching a custom selection
                // 2) all files which match the selection get deleted
                this.SetStatusMessage("Resetting selection to 'All files'.");

                MessageBox messageBox = new MessageBox("Resetting selection to 'All files' (no files match the current selection)", this);
                messageBox.Message.StatusImage = MessageBoxImage.Information;
                switch (selection)
                {
                    case FileSelection.Color:
                        messageBox.Message.Problem = "Color images were previously selected but no files are currently classified as color so nothing can be shown.";
                        messageBox.Message.Reason = "No files are classified as color images.";
                        messageBox.Message.Hint = "If you have files you think should be marked as color, set their ImageQuality field to Color and then reselect color files.";
                        break;
                    case FileSelection.Corrupt:
                        messageBox.Message.Problem = "Corrupted files were previously selected but no files are currently corrupted, so nothing can be shown.";
                        messageBox.Message.Reason = "No files have their ImageQuality set to Corrupted.";
                        messageBox.Message.Hint = "If you have files you think should be marked as Corrupted, set their ImageQuality to Corrupted and then reselect corrupted files.";
                        break;
                    case FileSelection.Custom:
                        messageBox.Message.Problem = "No files currently match the custom selection so nothing can be shown.";
                        messageBox.Message.Reason = "No files match the criteria set in the current Custom selection.";
                        messageBox.Message.Hint = "Create a different custom selection and apply it view the matching files.";
                        break;
                    case FileSelection.Dark:
                        messageBox.Message.Problem = "Dark files were previously selected but no files are currently dark so nothing can be shown.";
                        messageBox.Message.Reason = "No files have their ImageQuality set to Dark.";
                        messageBox.Message.Hint = "If you have files you think should be marked as Dark, set their ImageQuality to Dark and then reselect dark files.";
                        break;
                    case FileSelection.Greyscale:
                        messageBox.Message.Problem = "Greyscale images were previously selected but no files are currently classified as greyscale so nothing can be shown.";
                        messageBox.Message.Reason = "No files are classified as greyscale images.";
                        messageBox.Message.Hint = "If you have files you think should be marked as greyscale, set their ImageQuality field to Greyscale and then reselect greyscale files.";
                        break;
                    case FileSelection.NoLongerAvailable:
                        messageBox.Message.Problem = "Files no londer available were previously selected but all files are availale so nothing can be shown.";
                        messageBox.Message.Reason = "No files have their ImageQuality field set to FilesNoLongerAvailable.";
                        messageBox.Message.Hint = "If you have removed files set their ImageQuality field to FilesNoLongerAvailable and then reselect files no longer available.";
                        break;
                    case FileSelection.MarkedForDeletion:
                        messageBox.Message.Problem = "Files marked for deletion were previously selected but no files are currently marked so nothing can be shown.";
                        messageBox.Message.Reason = "No files have their Delete? box checked.";
                        messageBox.Message.Hint = "If you have files you think should be marked for deletion, check their Delete? box and then reselect files marked for deletion.";
                        break;
                    case FileSelection.Video:
                        messageBox.Message.Problem = "Videos were previously selected but no files are currently classified as videos so nothing can be shown.";
                        messageBox.Message.Reason = "No files are classified as videos.";
                        messageBox.Message.Hint = "If you have files you think should be marked as videos, set their ImageQuality field to Video and then reselect video files.";
                        break;
                    default:
                        throw new NotSupportedException(String.Format("Unhandled selection {0}.", selection));
                }
                messageBox.Message.Result = "The 'All files' selection will be applied, where all files in your image set are displayed.";
                messageBox.ShowDialog();

                selection = FileSelection.All;
                this.DataHandler.FileDatabase.SelectFiles(selection);
            }

            // update UI for current selection
            this.OnFileSelectionChanged();

            // display the specified file or, if it's no longer selected, the next closest one
            // ShowFileAsync() handles empty image sets, so those don't need to be checked for here.
            // Undo/redo is handled in this function so that navigation triggered by selection changes is part of the selection undo/redo step.  Therefore, 
            // undo/redo generation is always suppressed in ShowFileAsync().
            await this.ShowFileAsync(this.DataHandler.FileDatabase.GetFileOrNextFileIndex(fileID), false);
            if (generateUndoRedoCommands)
            {
                FileSelectionChange selectionChange = new FileSelectionChange(this.DataHandler, previousSelection, previousFileID);
                if (selectionChange.HasChange())
                {
                    this.AddCommand(selectionChange);
                }
            }
        }

        // set the current file index
        private void SetCurrentFile(int fileIndex)
        {
            StatusBarItem currentFile = (StatusBarItem)this.FileNavigation.Items[1];
            currentFile.Content = Utilities.ToDisplayIndex(fileIndex).ToString();
        }

        // set the total number of files
        private void SetFileCount(int selectedFileCount)
        {
            StatusBarItem numberOfFiles = (StatusBarItem)this.FileNavigation.Items[3];
            numberOfFiles.Content = selectedFileCount.ToString();
        }

        private void SetStatusMessage(string format, params object[] args)
        {
            this.MessageBar.Text = String.Format(format, args);
        }

        // display a view in the view portion of the status bar
        private void SetSelection(string selectionDescription)
        {
            StatusBarItem selection = (StatusBarItem)this.FileNavigation.Items[4];
            selection.Content = selectionDescription;
        }

        // various dialogs cam perform a bulk edit, after which the current file's data needs to be refreshed
        private async Task ShowBulkFileEditDialogAsync(Window dialog)
        {
            Debug.Assert((dialog.GetType() == typeof(DateCorrectAmbiguous)) ||
                         (dialog.GetType() == typeof(DateDaylightSavingsTimeCorrection)) ||
                         (dialog.GetType() == typeof(DateTimeFixedCorrection)) ||
                         (dialog.GetType() == typeof(DateTimeLinearCorrection)) ||
                         (dialog.GetType() == typeof(DateTimeRereadFromFiles)) ||
                         (dialog.GetType() == typeof(DateTimeSetTimeZone)) ||
                         (dialog.GetType() == typeof(DarkImagesThreshold)) ||
                         (dialog.GetType() == typeof(PopulateFieldWithMetadata)),
                         String.Format("Unexpected dialog {0}.", dialog.GetType()));

            this.DataHandler.TrySyncCurrentFileToDatabase();
            this.DataHandler.IsProgrammaticUpdate = true;
            if (dialog.ShowDialog() == true)
            {
                // load the changes made through the current dialog
                // Often this won't be needed but it's nontrivial to determine if a bulk edit would affect which files are selected
                // or change files' sort order.
                long currentFileID = this.DataHandler.ImageCache.Current.ID;
                this.DataHandler.FileDatabase.SelectFiles(this.DataHandler.FileDatabase.ImageSet.FileSelection);

                // show updated data for file
                // Delete doesn't go through this code path so none of the bulk edit dialogs can result in a change in the file which 
                // needs to be displayed.  Hence the image cache doesn't need to be invalidated.  However, the SelectFiles() call above 
                // might mean the currently displayed file is no longer part of the selection and hence GetFileOrNextFileIndex() needs 
                // to be called for a fully safe flow.
                await this.ShowFileAsync(this.DataHandler.FileDatabase.GetFileOrNextFileIndex(currentFileID));

                // clear undo/redo state as bulk edits aren't undoable
                this.OnBulkEdit(this, null);
            }
            this.DataHandler.IsProgrammaticUpdate = false;
        }

        private void ShowLongRunningOperationFeedback()
        {
            this.LongRunningFeedback.Visibility = Visibility.Visible;
            this.FileNavigationGrid.Visibility = Visibility.Collapsed;
        }

        private async Task ShowFileAsync(int fileIndex)
        {
            await this.ShowFileAsync(fileIndex, true);
        }

        public async Task ShowFileAsync(int fileIndex, bool generateUndoRedoCommands)
        {
            // if there is no file to show, then show an image indicating no image set or an empty image set
            if ((this.IsFileDatabaseAvailable() == false) || (this.DataHandler.FileDatabase.CurrentlySelectedFileCount < 1))
            {
                this.FileDisplay.Display(Constant.Images.NoSelectableFileMessage);
                this.RefreshDisplayedMarkers();
                this.SetCurrentFile(Constant.Database.InvalidRow);

                // clear data context
                this.DataEntryControls.SetDataContext(null);

                // clear tracking state
                this.State.CurrentFileSnapshot.Clear();
                this.State.NoteControlsWithNewValues.Clear();
                return;
            }

            // detach from current file, if any
            if (this.IsFileAvailable())
            {
                if (this.DataHandler.ImageCache.Current.HasChanges)
                {
                    // persist any changes to the current file in the database and, if needed, include them in the undo/redo chain
                    // Changes to the file should already be captured to the undo/redo chain through data binding (single field edits) or from command 
                    // generation at multiple edits, so no new undo/redo state should be needed.
                    Debug.Assert(new FileMultipleFieldChange(this.State.CurrentFileSnapshot, this.DataHandler.ImageCache).Changes == 0, "Current file has unexpected changes which may not have been included in undo/redo state.");
                    this.DataHandler.TrySyncCurrentFileToDatabase();

                    // for note controls, update the autocomplete list if a new value was used
                    foreach (DataEntryNote noteControl in this.State.NoteControlsWithNewValues)
                    {
                        noteControl.MergeAutocompletions(this.DataHandler.FileDatabase.GetDistinctValuesInFileDataColumn(noteControl.DataLabel));
                    }
                }

                // unlink current file from change tracking
                this.DataHandler.ImageCache.Current.PropertyChanged -= this.OnFileFieldChanged;
                this.State.NoteControlsWithNewValues.Clear();
            }

            // move to new file, though it might be the same file if this show call is just to sync fields from the database
            // This should be the only place where code in CarnassialWindow moves the file enumerator
            // - database synchronization logic above depends on this
            // - image caching logic below depends on this
            int prefetchStride = 1;
            if (this.State.FileNavigatorSliderDragging)
            {
                prefetchStride = 0;
            }
            int previousFileIndex = this.DataHandler.ImageCache.CurrentRow;

            MoveToFileResult moveToFile = await this.DataHandler.ImageCache.TryMoveToFileAsync(fileIndex, prefetchStride);
            if (moveToFile.Succeeded == false)
            {
                throw new ArgumentOutOfRangeException(nameof(fileIndex), String.Format("{0} is not a valid index in the file table.", fileIndex));
            }

            // update each control with the data for the now current file
            // This is always done as it's assumed either the file being displayed changed or that a control refresh is required due to database changes
            // the call to TryMoveToFile() above refreshes the data stored under this.dataHandler.ImageCache.Current.
            // Note: The refresh here covers only the file table as there's no scenario for edits to the markers table which don't route through
            // MarkableCanvas.MarkerCreatedOrDeleted.
            this.State.CurrentFileSnapshot = this.DataHandler.ImageCache.Current.GetValues();
            this.DataHandler.IsProgrammaticUpdate = true;
            this.DataEntryControls.SetDataContext(this.DataHandler.ImageCache.Current);
            this.DataHandler.IsProgrammaticUpdate = false;
            this.DataHandler.ImageCache.Current.PropertyChanged += this.OnFileFieldChanged;

            // update status bar
            this.SetCurrentFile(fileIndex);
            this.ClearStatusMessage();

            // update nav slider thumb's position to the current file
            this.FileNavigatorSlider.Value = Utilities.ToDisplayIndex(fileIndex);

            // display new file and update menu item enables if the file changed
            // This avoids unnecessary image reloads and refreshes in cases where ShowFile() is just being called to refresh controls.
            if (moveToFile.NewFileToDisplay)
            {
                // show the file
                this.FileDisplay.Display(this.DataHandler.FileDatabase.FolderPath, this.DataHandler.ImageCache, this.GetDisplayMarkers());

                // add move to this file to the undo/redo chain if it's not already present
                if (generateUndoRedoCommands)
                {
                    this.AddCommand(new FileNavigation(this.DataHandler.ImageCache, previousFileIndex));
                }

                // enable or disable menu items whose availability depends on whether the file's an image or video
                bool isVideo = this.DataHandler.ImageCache.Current.IsVideo;
                bool isImage = !isVideo;
                this.MenuOptionsDisplayMagnifier.IsEnabled = isImage;
                this.MenuViewApplyBookmark.IsEnabled = isImage;
                this.MenuViewDifferencesCombined.IsEnabled = isImage;
                this.MenuViewMagnifierZoomIncrease.IsEnabled = isImage;
                this.MenuViewMagnifierZoomDecrease.IsEnabled = isImage;
                this.MenuViewNextOrPreviousDifference.IsEnabled = isImage;
                this.MenuViewSetBookmark.IsEnabled = isImage;
                this.MenuViewZoomIn.IsEnabled = isImage;
                this.MenuViewZoomOut.IsEnabled = isImage;
                this.MenuViewZoomToFit.IsEnabled = isImage;

                this.MenuViewPlayVideo.IsEnabled = isVideo;
            }
        }

        private async Task ShowFileAsync(Slider fileNavigatorSlider)
        {
            await this.ShowFileAsync((int)fileNavigatorSlider.Value - 1);
        }

        internal async Task ShowFileWithoutSliderCallbackAsync(bool forward, ModifierKeys modifiers)
        {
            // determine how far to move and in which direction
            int increment = Utilities.GetIncrement(forward, modifiers);
            int newFileIndex = this.DataHandler.ImageCache.CurrentRow + increment;

            await this.ShowFileWithoutSliderCallbackAsync(newFileIndex, increment);
        }

        private async Task ShowFileWithoutSliderCallbackAsync(int newFileIndex)
        {
            await this.ShowFileWithoutSliderCallbackAsync(newFileIndex, 0);
        }

        private async Task ShowFileWithoutSliderCallbackAsync(int newFileIndex, int prefetchStride)
        {
            // if no change the file is already being displayed
            // For example, the end of the image set has been reached but key repeat means right arrow events are still coming in as the user hasn't
            // reacted yet.
            if (newFileIndex == this.DataHandler.ImageCache.CurrentRow)
            {
                return;
            }

            // clamp to the maximum or minimum row available
            if (newFileIndex >= this.DataHandler.FileDatabase.CurrentlySelectedFileCount)
            {
                newFileIndex = this.DataHandler.FileDatabase.CurrentlySelectedFileCount - 1;
            }
            else if (newFileIndex < 0)
            {
                newFileIndex = 0;
            }

            // show the new file
            this.FileNavigatorSlider_EnableOrDisableValueChangedCallback(false);
            await this.ShowFileAsync(newFileIndex);
            this.FileNavigatorSlider_EnableOrDisableValueChangedCallback(true);
        }

        private bool ShowFolderSelectionDialog(out IEnumerable<string> folderPaths)
        {
            CommonOpenFileDialog folderSelectionDialog = new CommonOpenFileDialog()
            {
                Title = "Select one or more folders...",
                DefaultDirectory = this.State.MostRecentFileAddFolderPath,
                InitialDirectory = this.State.MostRecentFileAddFolderPath,
                IsFolderPicker = true,
                Multiselect = true
            };
            folderSelectionDialog.FolderChanging += this.FolderSelectionDialog_FolderChanging;

            if (folderSelectionDialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                folderPaths = folderSelectionDialog.FileNames;

                // remember the parent of the selected folder path to save the user clicks and scrolling in case files from additional 
                // directories are added later
                // Moves above the location of the template file are disallowed, however.
                string parentFolderPath = Path.GetDirectoryName(folderPaths.First());
                if ((parentFolderPath != null) && parentFolderPath.StartsWith(this.FolderPath, StringComparison.OrdinalIgnoreCase))
                {
                    this.State.MostRecentFileAddFolderPath = parentFolderPath;
                }
                return true;
            }

            folderPaths = null;
            return false;
        }

        private async Task ToggleCurrentFileDeleteFlagAsync()
        {
            if (this.IsFileAvailable() == false)
            {
                return;
            }

            bool newDeleteValue = !this.DataHandler.ImageCache.Current.DeleteFlag;
            this.DataHandler.ImageCache.Current.DeleteFlag = newDeleteValue;

            // if the current file was just marked for deletion presumably the user is done with it and ready to move to the next
            // This autoadvance saves the user having to keep backing out of data entry and hitting the next arrow, so offers substantial savings when
            // working through large numbers of wind triggers or such but may not be desirable in all cases.  If needed an option can be added to disable
            // the behavior.
            if (newDeleteValue)
            {
                await this.ShowFileWithoutSliderCallbackAsync(true, ModifierKeys.None);
            }
        }

        // out parameters can't be used in anonymous methods, so a separate pointer to backgroundWorker is required for return to the caller
        private async Task<bool> TryAddFilesAsync(IEnumerable<string> folderPaths)
        {
            AddFilesIOComputeTransactionManager folderLoad = new AddFilesIOComputeTransactionManager(this.UpdateFolderLoadProgress, this.State.Throttles.GetDesiredProgressUpdateInterval());
            folderLoad.FolderPaths.AddRange(folderPaths);
            folderLoad.FindFilesToLoad(this.FolderPath);
            if (folderLoad.FilesToLoad == 0)
            {
                // no images were found in folder; see if user wants to try again
                MessageBox messageBox = new MessageBox("Select a folder containing images or videos?", this, MessageBoxButton.YesNo);
                messageBox.Message.Problem = "There aren't any images or videos in the folder '" + this.FolderPath + "' so your image set is currentl empty.";
                messageBox.Message.Reason = "\u2022 This folder has no images in it (files ending in .jpg)." + Environment.NewLine;
                messageBox.Message.Reason += "\u2022 This folder has no videos in it (files ending in .avi or .mp4).";
                messageBox.Message.Solution = "Choose Yes and select a folder containing images and/or videos or choose No and add files later via the File menu.";
                messageBox.Message.Hint = "\u2022 The files may be in a subfolder of this folder." + Environment.NewLine;
                messageBox.Message.Hint += "\u2022 If you need to set the image set's time zone before adding files choose No." + Environment.NewLine;
                messageBox.Message.StatusImage = MessageBoxImage.Question;
                if (messageBox.ShowDialog() == false)
                {
                    return false;
                }

                if (this.ShowFolderSelectionDialog(out folderPaths))
                {
                    return await this.TryAddFilesAsync(folderPaths);
                }

                // exit if user changed their mind about trying again
                return false;
            }

            // update UI for import
            this.ShowLongRunningOperationFeedback();
            this.MenuOptions.IsEnabled = true;
            folderLoad.ReportStatus();
            if (this.State.SkipDarkImagesCheck)
            {
                this.SetStatusMessage("Loading folders...");
            }
            else
            {
                this.SetStatusMessage("Loading folders (if this is slower than you like and dark image detection isn't needed you can select Skip dark check in the Options menu right now)...");
            }

            // change to the files tab
            this.MenuViewShowFiles_Click(this, null);

            // ensure all files are selected
            // This prevents files which are in the database but not selected from being added a second time.
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            FileSelection originalSelection = this.DataHandler.FileDatabase.ImageSet.FileSelection;
            if (originalSelection != FileSelection.All)
            {
                this.DataHandler.FileDatabase.SelectFiles(FileSelection.All);
            }

            // load all files found
            // Note: the UI thread is free during loading.  So if loading's going slow the user can switch off dark checking 
            // asynchronously in the middle of the load to speed it up.            
            int filesAddedToDatabase = await folderLoad.AddFilesAsync(this.DataHandler.FileDatabase, this.State, (int)this.Width);
            folderLoad.Dispose();

            // if needed, revert to original selection
            if (originalSelection != this.DataHandler.FileDatabase.ImageSet.FileSelection)
            {
                this.DataHandler.FileDatabase.SelectFiles(originalSelection);
            }

            // shift UI to normal, non-loading state
            // Stopwatch is stopped before OnFolderLoadingCompleteAsync() to exclude load and render time of the first image.
            // Status message is updated after OnFolderLoadingCompleteAsync() because loading an image clears the status message.
            this.HideLongRunningOperationFeedback();
            stopwatch.Stop();
            await this.OnFileDatabaseOpenedOrFilesAddedAsync(true);
            this.SetStatusMessage("{0} of {1} files added to image set in {2:0.000}s ({3:0} files/second, {4:0.000}s IO, {5:0.000}s compute, {6:0.000}s database).", filesAddedToDatabase, folderLoad.FilesToLoad, stopwatch.Elapsed.TotalSeconds, folderLoad.FilesToLoad / stopwatch.Elapsed.TotalSeconds, folderLoad.IODuration.TotalSeconds, folderLoad.ComputeDuration.TotalSeconds, folderLoad.DatabaseDuration.TotalSeconds);

            // update the user as to what files are in the database
            this.MaybeShowFileCountsDialog(true);
            return true;
        }

        private bool TryCopyValuesToAnalysis(int analysisSlot)
        {
            if (this.IsFileAvailable() == false)
            {
                return false;
            }
            Dictionary<string, object> analysisValuesByDataLabel = this.DataHandler.GetCopyableFieldsFromCurrentFile(this.DataEntryControls.Controls);
            this.State.Analysis[analysisSlot] = analysisValuesByDataLabel;
            ((MenuItem)this.MenuEditPasteValuesFromAnalysis.Items[analysisSlot]).IsEnabled = true;

            HashSet<string> analysisLabelsByDataLabel = new HashSet<string>(this.DataHandler.FileDatabase.Controls.Where(control => control.AnalysisLabel).Select(control => control.DataLabel));
            this.AnalysisButtons.SetAnalysis(analysisSlot, analysisValuesByDataLabel, analysisLabelsByDataLabel);
            return true;
        }

        private bool TryGetSelectedCounter(out DataEntryCounter selectedCounter)
        {
            foreach (DataEntryControl control in this.DataEntryControls.Controls)
            {
                if (control is DataEntryCounter counter)
                {
                    if (counter.IsSelected)
                    {
                        selectedCounter = counter;
                        return true;
                    }
                }
            }
            selectedCounter = null;
            return false;
        }

        private bool TryGetTemplatePath(out string templateDatabasePath)
        {
            // prompt user to select a template
            // default the template selection dialog to the most recently opened database
            this.State.MostRecentImageSets.TryGetMostRecent(out string defaultTemplateDatabasePath);
            if (Utilities.TryGetFileFromUser("Select a template file, which should be located in the root folder containing your images and videos",
                                             defaultTemplateDatabasePath,
                                             String.Format("Template files (*{0})|*{0}", Constant.File.TemplateFileExtension),
                                             out templateDatabasePath) == false)
            {
                return false;
            }

            string templateDatabaseDirectoryPath = Path.GetDirectoryName(templateDatabasePath);
            if (String.IsNullOrEmpty(templateDatabaseDirectoryPath))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Load the specified database template and then the associated file database.
        /// </summary>
        /// <param name="templateDatabasePath">Fully qualified path to the template database file.</param>
        /// <returns>true only if both the template and file database are loaded (regardless of whether any files were loaded), false otherwise</returns>
        /// <remarks>This method doesn't particularly need to be public. But making it private imposes substantial complexity in invoking it via PrivateObject
        /// in unit tests.</remarks>
        public async Task<bool> TryOpenTemplateAndFileDatabaseAsync(string templateDatabasePath)
        {
            Stopwatch imageSetLoadAndSetupTime = new Stopwatch();
            imageSetLoadAndSetupTime.Start();
            bool templateLoadedOrCreated = TemplateDatabase.TryCreateOrOpen(templateDatabasePath, out TemplateDatabase templateDatabase);
            imageSetLoadAndSetupTime.Stop();
            if (templateLoadedOrCreated == false)
            {
                // notify the user the template couldn't be loaded
                MessageBox messageBox = new MessageBox("Carnassial could not load the template.", this);
                messageBox.Message.Problem = "Carnassial could not load " + Path.GetFileName(templateDatabasePath) + Environment.NewLine;
                messageBox.Message.Reason = "\u2022 The template was created with the Timelapse template editor instead of the Carnassial editor." + Environment.NewLine;
                messageBox.Message.Reason = "\u2022 The template may be corrupted or somehow otherwise invalid.";
                messageBox.Message.Solution = String.Format("You may have to recreate the template, restore it from the {0} folder, or use another copy of it if you have one.", Constant.File.BackupFolder);
                messageBox.Message.Result = "Carnassial won't do anything.  You can try to select another template file.";
                messageBox.Message.Hint = "If the template can't be opened in a SQLite database editor the file is corrupt.";
                messageBox.Message.StatusImage = MessageBoxImage.Error;
                messageBox.ShowDialog();

                this.State.MostRecentImageSets.TryRemove(templateDatabasePath);
                this.MenuFileRecentImageSets_Refresh();
                return false;
            }

            // try to get the file database file path
            // addFiles will be true if it's a new file database (meaning the user will be prompted import some files)
            if (this.TrySelectDatabaseFile(templateDatabasePath, out string fileDatabaseFilePath, out bool tryAddFiles) == false)
            {
                // no file database was selected
                return false;
            }

            // before running from an existing file database, verify the controls in the template database are compatible with those
            // of the file database
            imageSetLoadAndSetupTime.Start();
            bool fileDatabaseCreatedOrOpened = FileDatabase.TryCreateOrOpen(fileDatabaseFilePath, templateDatabase, this.State.OrderFilesByDateTime, this.State.CustomSelectionTermCombiningOperator, out FileDatabase fileDatabase);
            imageSetLoadAndSetupTime.Stop();
            if (fileDatabaseCreatedOrOpened == false)
            {
                if (fileDatabase.ControlSynchronizationIssues.Count > 0)
                {
                    // notify user the template and database are out of sync
                    TemplateSynchronization templatesNotCompatibleDialog = new TemplateSynchronization(fileDatabase.ControlSynchronizationIssues, this);
                    if (templatesNotCompatibleDialog.ShowDialog() != true)
                    {
                        // user indicated not to update to the current template or cancelled out of the dialog
                        Application.Current.Shutdown();
                        return false;
                    }
                    // user indicated to run with the stale copy of the template found in the file database
                }
                else
                {
                    // notify user the database couldn't be loaded
                    MessageBox messageBox = new MessageBox("Carnassial could not load the database.", this);
                    messageBox.Message.Problem = "Carnassial could not load " + Path.GetFileName(fileDatabaseFilePath) + Environment.NewLine;
                    messageBox.Message.Reason = "\u2022 The database was created with Timelapse instead of Carnassial." + Environment.NewLine;
                    messageBox.Message.Reason = "\u2022 The database may be corrupted or somehow otherwise invalid.";
                    messageBox.Message.Solution = String.Format("You may have to recreate the database, restore it from the {0} folder, or use another copy of it if you have one.", Constant.File.BackupFolder);
                    messageBox.Message.Result = "Carnassial won't do anything.  You can try to select another template or database file.";
                    messageBox.Message.Hint = "If the database can't be opened in a SQLite database editor the file is corrupt.";
                    messageBox.Message.StatusImage = MessageBoxImage.Error;
                    messageBox.ShowDialog();
                    return false;
                }
            }

            // valid template and file database loaded
            // generate and render the data entry controls regardless of whether there are actually any files in the file database.
            imageSetLoadAndSetupTime.Start();
            this.DataHandler = new DataEntryHandler(fileDatabase);
            this.DataHandler.BulkEdit += this.OnBulkEdit;
            this.DataEntryControls.CreateControls(fileDatabase, this.DataHandler, (string dataLabel) => { return fileDatabase.GetDistinctValuesInFileDataColumn(dataLabel); });

            // add event handlers for marker effects which can't be handled by DataEntryHandler
            foreach (DataEntryControl control in this.DataEntryControls.Controls)
            {
                if (control is DataEntryCounter counter)
                {
                    counter.Container.MouseEnter += this.DataEntryCounter_MouseEnter;
                    counter.Container.MouseLeave += this.DataEntryCounter_MouseLeave;
                    counter.LabelControl.Click += this.DataEntryCounter_Click;
                }
            }

            this.MenuFileRecentImageSets_Refresh();
            this.State.MostRecentFileAddFolderPath = fileDatabase.FolderPath;
            this.State.MostRecentImageSets.SetMostRecent(templateDatabasePath);
            this.Title = Path.GetFileName(fileDatabase.FilePath) + " - " + Constant.MainWindowBaseTitle;
            imageSetLoadAndSetupTime.Stop();

            bool filesAdded = false;
            if (tryAddFiles)
            {
                // if this is a new file database, try to load files (if any) from the same folder
                filesAdded = await this.TryAddFilesAsync(new string[] { this.FolderPath });
            }

            await this.OnFileDatabaseOpenedOrFilesAddedAsync(filesAdded);
            if (filesAdded == false)
            {
                this.SetStatusMessage("Image set opened in {0:0.000}s ({1:0} files/second).", imageSetLoadAndSetupTime.Elapsed.TotalSeconds, this.DataHandler.FileDatabase.CurrentlySelectedFileCount / imageSetLoadAndSetupTime.Elapsed.TotalSeconds);
            }
            return true;
        }

        private bool TryPasteValuesFromAnalysis(int analysisSlot)
        {
            Dictionary<string, object> valuesFromAnalysis = this.State.Analysis[analysisSlot];
            if (valuesFromAnalysis == null)
            {
                // nothing to copy
                return false;
            }

            this.PasteValuesToCurrentFileWithUndo(new Dictionary<string, object>(valuesFromAnalysis, StringComparer.Ordinal));
            return true;
        }

        // Given the location path of the template, return:
        // - true if a database file was specified
        // - databaseFilePath: the path to the data database file (or null if none was specified).
        // - addFiles: true when the database file has just been created, which means images still have to be imported.
        private bool TrySelectDatabaseFile(string templateDatabasePath, out string databaseFilePath, out bool addFiles)
        {
            addFiles = false;

            string databaseFileName;
            string directoryPath = Path.GetDirectoryName(templateDatabasePath);
            string[] fileDatabasePaths = Directory.GetFiles(directoryPath, "*" + Constant.File.FileDatabaseFileExtension);
            if (fileDatabasePaths.Length == 1)
            {
                databaseFileName = Path.GetFileName(fileDatabasePaths[0]);
            }
            else if (fileDatabasePaths.Length > 1)
            {
                ChooseFileDatabase chooseDatabaseFile = new ChooseFileDatabase(fileDatabasePaths, templateDatabasePath, this);
                if (chooseDatabaseFile.ShowDialog() == true)
                {
                    databaseFileName = chooseDatabaseFile.SelectedFile;
                }
                else
                {
                    // user cancelled .ddb selection
                    databaseFilePath = null;
                    return false;
                }
            }
            else
            {
                // there are no existing .ddb files
                string templateDatabaseFileName = Path.GetFileName(templateDatabasePath);
                if (String.Equals(templateDatabaseFileName, Constant.File.DefaultTemplateDatabaseFileName, StringComparison.OrdinalIgnoreCase))
                {
                    databaseFileName = Constant.File.DefaultFileDatabaseFileName;
                }
                else
                {
                    databaseFileName = Path.GetFileNameWithoutExtension(templateDatabasePath) + Constant.File.FileDatabaseFileExtension;
                }
                addFiles = true;
            }

            databaseFilePath = Path.Combine(directoryPath, databaseFileName);
            return true;
        }

        private async Task TryViewCombinedDifferenceAsync()
        {
            if ((this.IsFileAvailable() == false) || this.DataHandler.ImageCache.Current.IsVideo)
            {
                return;
            }

            this.DataHandler.ImageCache.MoveToNextStateInCombinedDifferenceCycle();
            if (this.DataHandler.ImageCache.CurrentDifferenceState == ImageDifference.Unaltered)
            {
                // unaltered image should be cached
                MemoryImage unalteredImage = this.DataHandler.ImageCache.GetCurrentImage();
                Debug.Assert(unalteredImage != null, "Unaltered image not available from image cache.");
                this.FileDisplay.Display(unalteredImage);
                this.ClearStatusMessage();
                return;
            }

            // generate and cache difference image if needed
            if (this.DataHandler.ImageCache.GetCurrentImage() == null)
            {
                ImageDifferenceResult result = await this.DataHandler.ImageCache.TryCalculateCombinedDifferenceAsync(this.State.DifferenceThreshold);
                switch (result)
                {
                    case ImageDifferenceResult.CurrentImageNotAvailable:
                        this.SetStatusMessage("Combined differences can't be shown since the current file is not a loadable image (typically it's a video, missing, or corrupt).");
                        return;
                    case ImageDifferenceResult.NextImageNotAvailable:
                        this.SetStatusMessage("Combined differences can't be shown since the next file is not available.");
                        return;
                    case ImageDifferenceResult.NotCalculable:
                        this.SetStatusMessage("Previous or next file is not compatible with {0}, most likely because it's a different size.", this.DataHandler.ImageCache.Current.FileName);
                        return;
                    case ImageDifferenceResult.PreviousImageNotAvailable:
                        this.SetStatusMessage("Combined differences can't be shown since the next file is not available.");
                        return;
                    case ImageDifferenceResult.Success:
                        // set status below so that it's displayed when a cached difference is used
                        break;
                    default:
                        throw new NotSupportedException(String.Format("Unhandled combined difference result {0}.", result));
                }
            }

            // display differenced image
            // see above remarks about not modifying ImageToMagnify
            this.FileDisplay.Display(this.DataHandler.ImageCache.GetCurrentImage());
            this.SetStatusMessage("Viewing differences from both next and previous files ({0:0.0}ms).", 1000.0 * this.DataHandler.ImageCache.AverageCombinedDifferenceTimeInSeconds);
        }

        // Cycle through difference images in the order current, then previous and next differenced images.
        // Create and cache the differenced images.
        private async Task TryViewPreviousOrNextDifferenceAsync()
        {
            if ((this.IsFileAvailable() == false) || this.DataHandler.ImageCache.Current.IsVideo)
            {
                return;
            }

            this.DataHandler.ImageCache.MoveToNextStateInPreviousNextDifferenceCycle();
            if (this.DataHandler.ImageCache.CurrentDifferenceState == ImageDifference.Unaltered)
            {
                // unaltered image should be cached
                MemoryImage unaltered = this.DataHandler.ImageCache.GetCurrentImage();
                this.FileDisplay.Display(unaltered);
                if (unaltered != null)
                {
                    this.ClearStatusMessage();
                }
                else
                {
                    this.SetStatusMessage("Difference can't be shown as the current file is not a displayable image (typically it's a video, missing, or corrupt).");
                }
                return;
            }

            // generate and cache difference image if needed
            if (this.DataHandler.ImageCache.GetCurrentImage() == null)
            {
                ImageDifferenceResult result = await this.DataHandler.ImageCache.TryCalculateDifferenceAsync(this.State.DifferenceThreshold);
                switch (result)
                {
                    case ImageDifferenceResult.CurrentImageNotAvailable:
                        this.SetStatusMessage("Difference can't be shown as the current file is not a displayable image (typically it's a video, missing, or corrupt).");
                        return;
                    case ImageDifferenceResult.NextImageNotAvailable:
                    case ImageDifferenceResult.PreviousImageNotAvailable:
                        this.SetStatusMessage("View of difference from {0} file unavailable as it is not a displayable image (typically it's a video, missing, or corrupt).", this.DataHandler.ImageCache.CurrentDifferenceState == ImageDifference.Previous ? "previous" : "next");
                        return;
                    case ImageDifferenceResult.NotCalculable:
                        this.SetStatusMessage("{0} file is not compatible with {1}, most likely because it's a different size.", this.DataHandler.ImageCache.CurrentDifferenceState == ImageDifference.Previous ? "Previous" : "Next", this.DataHandler.ImageCache.Current.FileName);
                        return;
                    case ImageDifferenceResult.Success:
                        // set status below so that it's displayed when a cached difference is used
                        break;
                    default:
                        throw new NotSupportedException(String.Format("Unhandled difference result {0}.", result));
                }
            }

            // display the differenced image
            // the magnifying glass always displays the original non-diferenced image so ImageToDisplay is updated and ImageToMagnify left unchnaged
            // this allows the user to examine any particular differenced area and see what it really looks like in the non-differenced image. 
            this.FileDisplay.Display(this.DataHandler.ImageCache.GetCurrentImage());
            this.SetStatusMessage("Viewing difference from {0} file ({1:0.0}ms).", this.DataHandler.ImageCache.CurrentDifferenceState == ImageDifference.Previous ? "previous" : "next", 1000.0 * this.DataHandler.ImageCache.AverageDifferenceTimeInSeconds);
        }

        private void UpdateFolderLoadProgress(FileLoadStatus progress)
        {
            StatusBarItem statusMessage = (StatusBarItem)this.LongRunningFeedback.StatusMessage.Items[0];
            statusMessage.Content = progress.GetMessage();
            this.LongRunningFeedback.ProgressBar.Value = progress.GetPercentage();

            if (progress.TryDetachImage(out MemoryImage image))
            {
                progress.MaybeUpdateImageRenderWidth((int)this.Width);
                this.FileDisplay.Display(image, null);
                image.Dispose();
            }
        }

        private void UpdateSpreadsheetProgress(SpreadsheetReadWriteStatus progress)
        {
            StatusBarItem statusMessage = (StatusBarItem)this.LongRunningFeedback.StatusMessage.Items[0];
            statusMessage.Content = progress.GetMessage();
            this.LongRunningFeedback.ProgressBar.Value = progress.GetPercentage();
        }

        private async void Window_Closing(object sender, CancelEventArgs e)
        {
            await this.CloseImageSetAsync();

            HwndSource.FromHwnd((new WindowInteropHelper(this)).Handle).RemoveHook(new HwndSourceHook(this.WndProc));

            // persist user specific state to the registry
            if (this.Top > -10 && this.Left > -10)
            {
                this.State.CarnassialWindowPosition = new Rect(new Point(this.Left, this.Top), new Size(this.Width, this.Height));
            }
            this.State.WriteToRegistry();
        }

        private async void Window_ContentRendered(object sender, EventArgs e)
        {
            // abort if required dependencies are missing
            if (Dependencies.AreRequiredBinariesPresent(Constant.ApplicationName, Assembly.GetExecutingAssembly()) == false)
            {
                Dependencies.ShowMissingBinariesDialog(Constant.ApplicationName);
                if (Application.Current != null)
                {
                    Application.Current.Shutdown();
                }
            }

            // check for updates
            if (DateTime.UtcNow - this.State.MostRecentCheckForUpdates > Constant.CheckForUpdateInterval)
            {
                Uri latestVersionAddress = CarnassialConfigurationSettings.GetLatestReleaseApiAddress();
                if (latestVersionAddress == null)
                {
                    return;
                }

                GithubReleaseClient updater = new GithubReleaseClient(Constant.ApplicationName, latestVersionAddress);
                updater.TryGetAndParseRelease(false, out Version publiclyAvailableVersion);
                this.State.MostRecentCheckForUpdates = DateTime.UtcNow;
            }

            // if a file was passed on the command line, try to open it
            // args[0] is the .exe
            string[] args = Environment.GetCommandLineArgs();
            if (args != null && args.Length > 1)
            {
                string filePath = args[1];
                if (filePath.EndsWith(Constant.File.TemplateFileExtension, StringComparison.OrdinalIgnoreCase))
                {
                    await this.TryOpenTemplateAndFileDatabaseAsync(filePath);
                }
                else if (filePath.EndsWith(Constant.File.FileDatabaseFileExtension, StringComparison.OrdinalIgnoreCase))
                {
                    string[] templatePaths = Directory.GetFiles(Path.GetDirectoryName(filePath), "*" + Constant.File.TemplateFileExtension);
                    if (templatePaths != null && templatePaths.Length == 1)
                    {
                        await this.TryOpenTemplateAndFileDatabaseAsync(templatePaths[0]);
                    }
                }
            }
        }

        private async void Window_KeyDown(object sender, KeyEventArgs currentKey)
        {
            if (this.IsFileAvailable() == false)
            {
                // no file loaded so no special processing to do; let WPF drive menus as needed
                return;
            }

            // pass all keys to menus
            if (this.Menu.IsKeyboardFocusWithin)
            {
                return;
            }

            // pass all keys except keys which move focus off of data entry controls to data entry controls
            if (this.DataEntryControls.ControlsView.IsKeyboardFocusWithin)
            {
                // check if focus is actually within a control
                // This check is needed to prevent focus unexpectedly going to the data entry controls in the case where
                //   1) the controls, as is usually the case, don't fill the whole height of CarnassilWindow grid row they occupy
                //   2) the user switched input focus to another application
                //   3) the user clicks empty space near the controls to return input focus to Carnassial
                // Without this check, this sequence results in the controls view having keyboard focus even though no control has 
                // keyboard focus, which causes Carnassial to swallow keyboard input from the user and offer a confusing experience.
                // Numerous other solutions are possible but, in general, create auto layout difficulties as Grids do not resize
                // the height of auto rows or offer collapse priorities among star spacings.  Within Carnassial, probably the best
                // xaml based alternative is a row height multibinding, but this is substantially more complex than checking whether
                // a control is selected.
                if (this.DataEntryControls.ControlsView.SelectedItem != null)
                {
                    if ((currentKey.Key != Key.Escape) && (currentKey.Key != Key.Enter) && (currentKey.Key != Key.Tab))
                    {
                        return;
                    }
                }
            }

            // stop any file play in progress when any key is pressed
            bool wasPlayingFiles = false;
            if (this.PlayFilesButton.IsChecked == true)
            {
                wasPlayingFiles = true;
                this.MenuViewPlayFiles_Click(sender, null);
            }

            // check if input key or chord is a shortcut key and dispatch appropriately if so
            // When dispatch to an async method occurs the key typically needs to be marked handled before the async call.  Otherwise the UI thread continues
            // event processing on the reasonable assumption the key's unhandled.  This is frequently benign but can have undesirable side effects, such as
            // navigation keys changing which control has focus in addition to their intended effect.
            int keyRepeatCount = this.State.GetKeyRepeatCount(currentKey);
            switch (currentKey.Key)
            {
                case Key.B:
                    if (Keyboard.Modifiers == ModifierKeys.None)
                    {
                        // apply the current bookmark
                        this.FileDisplay.ApplyBookmark();
                        currentKey.Handled = true;
                    }
                    else if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        // bookmark (save) the current pan / zoom of the display image
                        this.FileDisplay.SetBookmark();
                        currentKey.Handled = true;
                    }
                    break;
                case Key.C:
                    // copy whatever data fields are copyable from the previous file
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.MenuEditCopy_Click(this, currentKey);
                        currentKey.Handled = true;
                    }
                    break;
                case Key.D:
                    // decrease the magnifing glass zoom
                    this.FileDisplay.MagnifierZoomOut();
                    currentKey.Handled = true;
                    break;
                // return to full view of display image
                case Key.D0:
                case Key.NumPad0:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.FileDisplay.ZoomToFit();
                        currentKey.Handled = true;
                    }
                    break;
                case Key.OemMinus:
                    this.FileDisplay.ZoomOut();
                    currentKey.Handled = true;
                    break;
                case Key.OemPlus:
                    this.FileDisplay.ZoomIn();
                    currentKey.Handled = true;
                    break;
                case Key.D1:
                case Key.D2:
                case Key.D3:
                case Key.D4:
                case Key.D5:
                case Key.D6:
                case Key.D7:
                case Key.D8:
                case Key.D9:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.TryCopyValuesToAnalysis(currentKey.Key - Key.D1);
                        currentKey.Handled = true;
                    }
                    else if (Keyboard.Modifiers == ModifierKeys.None)
                    {
                        this.TryPasteValuesFromAnalysis(currentKey.Key - Key.D1);
                        currentKey.Handled = true;
                    }
                    break;
                case Key.NumPad1:
                case Key.NumPad2:
                case Key.NumPad3:
                case Key.NumPad4:
                case Key.NumPad5:
                case Key.NumPad6:
                case Key.NumPad7:
                case Key.NumPad8:
                case Key.NumPad9:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.TryCopyValuesToAnalysis(currentKey.Key - Key.NumPad1);
                        currentKey.Handled = true;
                    }
                    else if (Keyboard.Modifiers == ModifierKeys.None)
                    {
                        this.TryPasteValuesFromAnalysis(currentKey.Key - Key.NumPad1);
                        currentKey.Handled = true;
                    }
                    break;
                // toggle the file's delete flag and, if set, move to the next file
                case Key.Delete:
                    currentKey.Handled = true;
                    await this.ToggleCurrentFileDeleteFlagAsync();
                    break;
                // exit current control, if any
                case Key.Enter:
                case Key.Escape:
                    this.FocusFileDisplay();
                    currentKey.Handled = true;
                    break;
                case Key.G:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.MenuViewGotoFile_Click(this, currentKey);
                        currentKey.Handled = true;
                    }
                    break;
                // toggle the magnifying glass on and off
                case Key.M:
                    this.MenuViewDisplayMagnifier_Click(this, currentKey);
                    currentKey.Handled = true;
                    break;
                case Key.N:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.PasteNextValues_Click(this, currentKey);
                        currentKey.Handled = true;
                    }
                    break;
                case Key.P:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.PastePreviousValues_Click(this, currentKey);
                        currentKey.Handled = true;
                    }
                    break;
                case Key.R:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.MenuEditResetValues_Click(this, currentKey);
                        currentKey.Handled = true;
                    }
                    break;
                case Key.Space:
                    // if the current file's a video allow the user to hit the space bar to start or stop playing the video
                    // This is desirable as the play or pause button doesn't necessarily have focus and it saves the user having to click the button with
                    // the mouse.
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        // since the check before this switch stops any current play, only respond to this key if it's a request to
                        // start a play
                        // Without this check, MenuViewPlayFiles_Click() would be called twice if the user uses ctrl+enter to stop a
                        // file play, resulting in the keystroke causing file play to continue.
                        if (wasPlayingFiles == false)
                        {
                            this.MenuViewPlayFiles_Click(this, null);
                        }
                    }
                    else if (this.FileDisplay.TryPlayOrPauseVideo() == false)
                    {
                        currentKey.Handled = true;
                        return;
                    }
                    break;
                case Key.U:
                    this.FileDisplay.MagnifierZoomIn();
                    currentKey.Handled = true;
                    break;
                case Key.V:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.MenuEditPaste_Click(this, currentKey);
                        currentKey.Handled = true;
                    }
                    break;
                case Key.Y:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.MenuEditRedo_Click(this, currentKey);
                        currentKey.Handled = true;
                    }
                    break;
                case Key.Z:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.MenuEditUndo_Click(this, currentKey);
                        currentKey.Handled = true;
                    }
                    break;
                case Key.End:
                    if (this.IsFileDatabaseAvailable())
                    {
                        currentKey.Handled = true;
                        await this.ShowFileWithoutSliderCallbackAsync(this.DataHandler.FileDatabase.CurrentlySelectedFileCount - 1);
                    }
                    break;
                case Key.Left:              // previous file
                    currentKey.Handled = true;
                    if (keyRepeatCount % this.State.Throttles.RepeatedKeyAcceptanceInterval == 0)
                    {
                        await this.ShowFileWithoutSliderCallbackAsync(false, Keyboard.Modifiers);
                    }
                    break;
                case Key.Home:
                    currentKey.Handled = true;
                    await this.ShowFileWithoutSliderCallbackAsync(0);
                    break;
                case Key.PageDown:
                    if (this.IsFileDatabaseAvailable())
                    {
                        currentKey.Handled = true;
                        await this.ShowFileWithoutSliderCallbackAsync(this.DataHandler.ImageCache.CurrentRow + (int)(Constant.PageUpDownNavigationFraction * this.DataHandler.FileDatabase.CurrentlySelectedFileCount));
                    }
                    break;
                case Key.PageUp:
                    if (this.IsFileDatabaseAvailable())
                    {
                        currentKey.Handled = true;
                        await this.ShowFileWithoutSliderCallbackAsync(this.DataHandler.ImageCache.CurrentRow - (int)(Constant.PageUpDownNavigationFraction * this.DataHandler.FileDatabase.CurrentlySelectedFileCount));
                    }
                    break;
                case Key.Right:             // next file
                    currentKey.Handled = true;
                    if (keyRepeatCount % this.State.Throttles.RepeatedKeyAcceptanceInterval == 0)
                    {
                        await this.ShowFileWithoutSliderCallbackAsync(true, Keyboard.Modifiers);
                    }
                    break;
                case Key.Tab:               // next or previous control
                    this.MoveFocusToNextOrPreviousTabPosition(Keyboard.Modifiers == ModifierKeys.Shift);
                    currentKey.Handled = true;
                    break;
                case Key.Up:                // show visual difference to next image
                    currentKey.Handled = true;
                    await this.TryViewPreviousOrNextDifferenceAsync();
                    break;
                case Key.Down:              // show visual difference to previous image
                    currentKey.Handled = true;
                    await this.TryViewCombinedDifferenceAsync();
                    break;
                default:
                    return;
            }
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            // hook touchpad swipes
            HwndSource.FromHwnd((new WindowInteropHelper(this)).Handle).AddHook(new HwndSourceHook(this.WndProc));
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // handle left and right swipes
            // The maximum size of the horizontal scrolling increment accepted is limited as, particularly when swiping left, wParam can carry large
            // positive increments rather than the negative increment which is correct for the motion.  As it sometimes jumps large for swipe rights
            // as well the simplest solution is to declare the event out of range and drop it.  Some combining of drag events is done (MouseHWheelStep)
            // as moving to a new file is a relatively chunky operation and actioning every increment makes the user interface rather hyper.
            if (msg == Constant.Win32Messages.WM_MOUSEHWHEEL && this.FileDisplay.IsFocused)
            {
                long wheelIncrement = wParam.ToInt64() >> 16;
                if (Math.Abs(wheelIncrement) < Constant.Gestures.MaximumMouseHWheelIncrement)
                {
                    this.State.MouseHorizontalScrollDelta += wheelIncrement;
                    if (Math.Abs(this.State.MouseHorizontalScrollDelta) >= Constant.Gestures.MouseHWheelStep)
                    {
                        // if enough swipe distance has accumulated reset the accumulator
                        // This resembles a slider drag in that the rendering needs not to be spammed with events but differs as the touchpad driver
                        // likely keeps firing intertial events for quite some time if the user is doing swipe and throw.  For best responsiveness,
                        // the accumulator is allowed to build until the next render and the number of files traversed incremented (or decremented)
                        // accordingly.
                        DateTime utcNow = DateTime.UtcNow;
                        if (utcNow - this.State.MostRecentRender > this.State.FileNavigatorSliderTimer.Interval)
                        {
                            // awaiting an async function within WndProc() bricks the UI, so fire it asynchronously
                            int increment = (int)(this.State.MouseHorizontalScrollDelta / Constant.Gestures.MouseHWheelStep);
                            int newFileIndex = this.DataHandler.ImageCache.CurrentRow + increment;
                            #pragma warning disable CS4014
                            this.ShowFileWithoutSliderCallbackAsync(newFileIndex, increment);
                            #pragma warning restore CS4014
                            this.State.MostRecentRender = utcNow;
                            this.State.MouseHorizontalScrollDelta = 0;
                        }
                    }
                }
                handled = true;
            }
            return IntPtr.Zero;
        }
    }
}
