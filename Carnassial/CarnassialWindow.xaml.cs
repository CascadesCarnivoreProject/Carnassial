using Carnassial.Controls;
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
using System.Windows.Media;
using System.Windows.Threading;
using DialogResult = System.Windows.Forms.DialogResult;
using MessageBox = Carnassial.Dialog.MessageBox;
using SaveFileDialog = System.Windows.Forms.SaveFileDialog;

namespace Carnassial
{
    /// <summary>
    /// main window for Carnassial
    /// </summary>
    public partial class CarnassialWindow : Window, IDisposable
    {
        private DataEntryHandler dataHandler;
        private bool disposed;
        private List<MarkersForCounter> markersOnCurrentFile;

        // speech feedback
        private SpeechSynthesizer speechSynthesizer;

        // Status information concerning the state of the UI
        private CarnassialState state;

        // timer for flushing FileeNavigatorSlider drag events
        private DispatcherTimer timerFileNavigatorSlider;

        public CarnassialWindow()
        {
            AppDomain.CurrentDomain.UnhandledException += this.OnUnhandledException;
            this.InitializeComponent();

            this.MarkableCanvas.MouseEnter += new MouseEventHandler(this.MarkableCanvas_MouseEnter);
            this.MarkableCanvas.PreviewMouseDown += new MouseButtonEventHandler(this.MarkableCanvas_PreviewMouseDown);
            this.MarkableCanvas.MarkerEvent += new EventHandler<MarkerEventArgs>(this.MarkableCanvas_RaiseMarkerEvent);

            this.speechSynthesizer = new SpeechSynthesizer();
            this.state = new CarnassialState();
            this.Title = Constant.MainWindowBaseTitle;

            // recall user's state from prior sessions
            this.state.ReadFromRegistry();

            this.MenuOptionsAudioFeedback.IsChecked = this.state.AudioFeedback;
            this.MenuOptionsEnableCsvImportPrompt.IsChecked = !this.state.SuppressSpreadsheetImportPrompt;
            this.MenuOptionsOrderFilesByDateTime.IsChecked = this.state.OrderFilesByDateTime;
            this.MenuOptionsSkipDarkFileCheck.IsChecked = this.state.SkipDarkImagesCheck;

            // timer callback so the display will update to the current slider position when the user pauses whilst dragging the slider 
            this.timerFileNavigatorSlider = new DispatcherTimer();
            this.timerFileNavigatorSlider.Interval = TimeSpan.FromSeconds(1.0 / Constant.ThrottleValues.DesiredMaximumImageRendersPerSecondUpperBound);
            this.timerFileNavigatorSlider.Tick += this.FileNavigatorSlider_TimerTick;

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

            this.Top = this.state.CarnassialWindowPosition.Y;
            this.Left = this.state.CarnassialWindowPosition.X;
            this.Height = this.state.CarnassialWindowPosition.Height;
            this.Width = this.state.CarnassialWindowPosition.Width;
            Utilities.TryFitWindowInWorkingArea(this);
        }

        private string FolderPath
        {
            get
            {
                Debug.Assert(this.IsFileDatabaseAvailable(), "State management failure: attempt to obtain folder path when database is unavailable.");
                return this.dataHandler.FileDatabase.FolderPath;
            }
        }

        /// <summary>
        /// A new marker associated with a counter control has been created
        /// Increment the counter and add the marker to all data structures (including the database)
        /// </summary>
        private void AddMarkerToCounter(DataEntryCounter counter, Marker marker)
        {
            // increment the counter to reflect the new marker
            this.dataHandler.IncrementOrResetCounter(counter);

            // Find markers associated with this particular control
            MarkersForCounter markersForCounter;
            bool success = this.TryGetMarkersForCounter(counter, out markersForCounter);
            Debug.Assert(success, String.Format("No markers found for counter {0}.", counter.DataLabel));

            // fill in marker information
            marker.Brush = Brushes.Red;
            marker.DataLabel = counter.DataLabel;
            marker.ShowLabel = true; // show label on creation, cleared on next refresh
            marker.LabelShownPreviously = false;
            marker.Tooltip = counter.Label + Environment.NewLine + counter.DataLabel;
            markersForCounter.AddMarker(marker);

            // update this counter's list of points in the database
            this.dataHandler.FileDatabase.SetMarkerPositions(this.dataHandler.ImageCache.Current.ID, markersForCounter);

            this.MarkableCanvas.Markers = this.GetDisplayMarkers();
            this.Speak(counter.Content + " " + counter.Label); // Speak the current count
        }

        private void ApplyUndoRedoState(UndoRedoState state)
        {
            switch (state.Type)
            {
                case UndoRedoType.FileValues:
                    this.PasteValuesToCurrentFile(state.Values);
                    break;
                default:
                    throw new NotSupportedException(String.Format("Unhandled undo/redo state type {0}.", state.Type));
            }
        }

        private async Task CloseImageSetAsync()
        {
            if (this.IsFileDatabaseAvailable())
            {
                // persist image set properties if an image set has been opened
                if (this.dataHandler.FileDatabase.CurrentlySelectedFileCount > 0)
                {
                    // revert to custom selections to all 
                    if (this.dataHandler.FileDatabase.ImageSet.FileSelection == FileSelection.Custom)
                    {
                        this.dataHandler.FileDatabase.ImageSet.FileSelection = FileSelection.All;
                    }

                    // sync image set properties
                    if (this.MarkableCanvas != null)
                    {
                        this.dataHandler.FileDatabase.ImageSet.Options.SetFlag(ImageSetOptions.Magnifier, this.MarkableCanvas.MagnifyingGlassEnabled);
                    }

                    if (this.dataHandler.ImageCache != null && this.dataHandler.ImageCache.Current != null)
                    {
                        this.dataHandler.FileDatabase.ImageSet.MostRecentFileID = this.dataHandler.ImageCache.Current.ID;
                    }

                    // write image set properties to the database
                    this.dataHandler.FileDatabase.SyncImageSetToDatabase();

                    // ensure custom filter operator is synchronized in state for writing to user's registry
                    this.state.CustomSelectionTermCombiningOperator = this.dataHandler.FileDatabase.CustomSelection.TermCombiningOperator;
                }

                // discard the image set and reset UX for no open image set/no selected files
                this.dataHandler.Dispose();
                this.dataHandler = null;
                await this.EnableOrDisableMenusAndControlsAsync();
            }

            this.state.Reset();
            this.MenuEditRedo.IsEnabled = this.state.UndoRedoChain.CanRedo;
            this.MenuEditUndo.IsEnabled = this.state.UndoRedoChain.CanUndo;
        }

        /// <summary>
        /// This preview callback is used by all controls to reset the focus.
        /// Whenever the user hits enter over the control, set the focus back to the top-level
        /// </summary>
        private void ContentControl_PreviewKeyDown(object sender, KeyEventArgs eventArgs)
        {
            if (eventArgs.Key == Key.Enter)
            {
                this.TrySetKeyboardFocusToMarkableCanvas(false, eventArgs);
                eventArgs.Handled = true;
            }
        }

        /// <summary>Click callback: When the user selects a counter, refresh the markers, which will also readjust the colors and emphasis</summary>
        /// <param name="sender">the event source</param>
        /// <param name="e">event information</param>
        private void CounterControl_Click(object sender, RoutedEventArgs e)
        {
            this.MarkableCanvas_UpdateMarkers();
        }

        /// <summary>Highlight the markers associated with a counter when the mouse enters it</summary>
        private void CounterControl_MouseEnter(object sender, MouseEventArgs e)
        {
            Panel panel = (Panel)sender;
            this.state.MouseOverCounter = ((DataEntryCounter)panel.Tag).DataLabel;
            this.MarkableCanvas_UpdateMarkers();
        }

        /// <summary>Remove marker highlighting</summary>
        private void CounterControl_MouseLeave(object sender, MouseEventArgs e)
        {
            this.state.MouseOverCounter = null;
            this.MarkableCanvas_UpdateMarkers();
        }

        /// <summary>Ensures only numbers are entered for counters.</summary>
        private void CounterControl_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = (Utilities.IsDigits(e.Text) || String.IsNullOrWhiteSpace(e.Text)) ? false : true;
            this.OnPreviewTextInput(e);
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
                if (this.dataHandler != null)
                {
                    this.dataHandler.Dispose();
                }
                this.speechSynthesizer.Dispose();
            }

            this.disposed = true;
        }

        private async Task EnableOrDisableMenusAndControlsAsync()
        {
            bool imageSetAvailable = this.IsFileDatabaseAvailable();
            bool filesSelected = false;
            if (imageSetAvailable)
            {
                filesSelected = this.dataHandler.FileDatabase.CurrentlySelectedFileCount > 0;
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
            this.MenuOptionsDisplayMagnifier.IsChecked = imageSetAvailable && this.dataHandler.FileDatabase.ImageSet.Options.HasFlag(ImageSetOptions.Magnifier);
            this.MenuOptionsDialogsOnOrOff.IsEnabled = filesSelected;
            this.MenuOptionsAdvancedCarnassialOptions.IsEnabled = filesSelected;

            // other UI components
            this.ControlsPanel.IsEnabled = filesSelected;  // no files are selected there's nothing for the user to do with data entry
            this.PastePreviousValues.IsEnabled = filesSelected;
            this.FileNavigatorSlider.IsEnabled = filesSelected;
            this.MarkableCanvas.IsEnabled = filesSelected;
            this.MarkableCanvas.MagnifyingGlassEnabled = filesSelected && this.dataHandler.FileDatabase.ImageSet.Options.HasFlag(ImageSetOptions.Magnifier);
            this.PasteAnalysis1.IsEnabled = filesSelected && (this.state.Analysis[0] != null);
            this.PasteAnalysis2.IsEnabled = filesSelected && (this.state.Analysis[1] != null);

            if (filesSelected == false)
            {
                await this.ShowFileAsync(Constant.Database.InvalidRow);
                this.statusBar.SetMessage("Image set is empty.");
                this.statusBar.SetCurrentFile(Constant.Database.InvalidRow);
                this.statusBar.SetFileCount(0);
            }
        }

        // lazily defer binding of data grid until user selects the data gind tab
        // This reduces startup and scrolling lag as filling and updating the grid is fairly expensive.
        private void FileDataPane_IsActiveChanged(object sender, EventArgs e)
        {
            if (this.IsFileDatabaseAvailable() == false)
            {
                return;
            }

            if (this.FileDataPane.IsActive)
            {
                this.dataHandler.FileDatabase.BindToDataGrid(this.DataGrid, null);
                if ((this.dataHandler.ImageCache != null) && (this.dataHandler.ImageCache.CurrentRow != Constant.Database.InvalidRow))
                {
                    // both UpdateLayout() calls are needed to get the data grid to highlight the selected row
                    // This seems related to initial population as the selection highlight updates without calling UpdateLayout() on subsequent calls
                    // to SelectAndScrollIntoView().
                    this.DataGrid.UpdateLayout();
                    this.DataGrid.SelectAndScrollIntoView(this.dataHandler.ImageCache.CurrentRow);
                    this.DataGrid.UpdateLayout();
                }
            }
        }

        private async void FileNavigatorSlider_DragCompleted(object sender, DragCompletedEventArgs args)
        {
            this.state.FileNavigatorSliderDragging = false;
            await this.ShowFileAsync(this.FileNavigatorSlider);
            this.timerFileNavigatorSlider.Stop();
        }

        private void FileNavigatorSlider_DragStarted(object sender, DragStartedEventArgs args)
        {
            this.timerFileNavigatorSlider.Start(); // The timer forces an image display update to the current slider position if the user pauses longer than the timer's interval. 
            this.state.FileNavigatorSliderDragging = true;
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

        private void FileNavigatorSlider_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // mark all key events as handled so that they're not processed
            // This works around a WPF async bug where pressing the up and down keys when focus is on the slider results in the key being handled by
            // both Window_PreviewKeyDown and the slider, triggering a slider value change event which moves to the next or previous image under the 
            // differenced view.  Without this block this WPF routing fail results in loss of image cache coherency and further problems.
            e.Handled = true;
        }

        // Timer callback that forces image update to the current slider position. Invoked as the user pauses dragging the image slider 
        private async void FileNavigatorSlider_TimerTick(object sender, EventArgs e)
        {
            await this.ShowFileAsync(this.FileNavigatorSlider);
            this.timerFileNavigatorSlider.Stop();
        }

        private async void FileNavigatorSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> args)
        {
            // since the minimum value is 1 there's a value change event during InitializeComponent() to ignore
            if (this.state == null)
            {
                args.Handled = true;
                return;
            }

            DateTime utcNow = DateTime.UtcNow;
            if ((this.state.FileNavigatorSliderDragging == false) || (utcNow - this.state.MostRecentDragEvent > this.timerFileNavigatorSlider.Interval))
            {
                await this.ShowFileAsync(this.FileNavigatorSlider);
                this.state.MostRecentDragEvent = utcNow;
                args.Handled = true;
            }
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
            if (this.markersOnCurrentFile == null)
            {
                return null;
            }

            List<Marker> markers = new List<Marker>();
            // if no counter is selected that just indicates no markers need to be highlighted at this time
            DataEntryCounter selectedCounter;
            this.TryGetSelectedCounter(out selectedCounter);
            for (int counter = 0; counter < this.markersOnCurrentFile.Count; ++counter)
            {
                MarkersForCounter markersForCounter = this.markersOnCurrentFile[counter];
                DataEntryControl control;
                if (this.DataEntryControls.ControlsByDataLabel.TryGetValue(markersForCounter.DataLabel, out control) == false)
                {
                    // if the counter can't be found it's likely because the control was made invisible in the template
                    // This means there is no user visible control associated with the marker.  For consistency, don't show those markers.
                    // If the control is later made visible in the template the markers will again be shown. 
                    continue;
                }

                // Update the emphasise for each tag to reflect how the user is interacting with tags
                DataEntryCounter currentCounter = (DataEntryCounter)this.DataEntryControls.ControlsByDataLabel[markersForCounter.DataLabel];
                bool emphasize = markersForCounter.DataLabel == this.state.MouseOverCounter;
                foreach (Marker marker in markersForCounter.Markers)
                {
                    // label markers when they're first created, don't show a label afterwards
                    if (marker.ShowLabel && !marker.LabelShownPreviously)
                    {
                        marker.ShowLabel = true;
                        marker.LabelShownPreviously = true;
                    }
                    else
                    {
                        marker.ShowLabel = false;
                    }

                    if (selectedCounter != null && currentCounter.DataLabel == selectedCounter.DataLabel)
                    {
                        marker.Brush = (SolidColorBrush)new BrushConverter().ConvertFromString(Constant.SelectionColour);
                    }
                    else
                    {
                        marker.Brush = (SolidColorBrush)new BrushConverter().ConvertFromString(Constant.StandardColour);
                    }

                    marker.Emphasise = emphasize;
                    marker.Tooltip = currentCounter.Label;
                    markers.Add(marker);
                }
            }
            return markers;
        }

        private async void Instructions_Drop(object sender, DragEventArgs dropEvent)
        {
            string templateDatabaseFilePath;
            if (Utilities.IsSingleTemplateFileDrag(dropEvent, out templateDatabaseFilePath))
            {
                dropEvent.Handled = await this.TryOpenTemplateAndBeginLoadFoldersAsync(templateDatabaseFilePath);
            }
        }

        private void Instructions_PreviewDrag(object sender, DragEventArgs dragEvent)
        {
            Utilities.OnInstructionsPreviewDrag(dragEvent);
        }

        private bool IsFileAvailable()
        {
            if (this.dataHandler == null ||
                this.dataHandler.ImageCache == null ||
                this.dataHandler.ImageCache.Current == null)
            {
                return false;
            }

            return true;
        }

        private bool IsFileDatabaseAvailable()
        {
            if (this.dataHandler == null ||
                this.dataHandler.FileDatabase == null)
            {
                return false;
            }

            return true;
        }

        private void MaybeShowFileCountsDialog(bool onFileLoading)
        {
            if (onFileLoading && this.state.SuppressFileCountOnImportDialog)
            {
                return;
            }

            Dictionary<FileSelection, int> counts = this.dataHandler.FileDatabase.GetFileCountsBySelection();
            FileCountsByQuality imageStats = new FileCountsByQuality(counts, this);
            if (onFileLoading)
            {
                imageStats.Message.Hint = "\u2022 " + imageStats.Message.Hint + Environment.NewLine + "\u2022 If you check don't show this message again this dialog can be turned back on via the Options menu.";
                imageStats.DontShowAgain.Visibility = Visibility.Visible;
            }
            Nullable<bool> result = imageStats.ShowDialog();
            if (onFileLoading && result.HasValue && result.Value && imageStats.DontShowAgain.IsChecked.HasValue)
            {
                this.state.SuppressFileCountOnImportDialog = imageStats.DontShowAgain.IsChecked.Value;
                this.MenuOptionsEnableFileCountOnImportDialog.IsChecked = !this.state.SuppressFileCountOnImportDialog;
            }
        }

        private void MarkableCanvas_MouseEnter(object sender, MouseEventArgs eventArgs)
        {
            // change focus to the canvas if the mouse enters the canvas and the user isn't in the midst of typing into a text field
            IInputElement focusedElement = FocusManager.GetFocusedElement(this);
            if ((focusedElement == null) || (focusedElement is TextBox == false))
            {
                this.TrySetKeyboardFocusToMarkableCanvas(true, eventArgs);
            }
        }

        // Whenever the user clicks on the image, reset the image focus to the image control 
        private void MarkableCanvas_PreviewMouseDown(object sender, MouseButtonEventArgs eventArgs)
        {
            this.TrySetKeyboardFocusToMarkableCanvas(true, eventArgs);
        }

        // Event handler: A marker, as defined in e.Marker, has been either added (if e.IsNew is true) or deleted (if it is false)
        // Depending on which it is, add or delete the tag from the current counter control's list of tags 
        // If its deleted, remove the tag from the current counter control's list of tags
        // Every addition / deletion requires us to:
        // - update the contents of the counter control 
        // - update the data held by the image
        // - update the list of markers held by that counter
        // - regenerate the list of markers used by the markableCanvas
        private void MarkableCanvas_RaiseMarkerEvent(object sender, MarkerEventArgs e)
        {
            DataEntryCounter selectedCounter;
            if (this.TryGetSelectedCounter(out selectedCounter) == false)
            {
                // mouse logic in MarkableCanvas sends marker create events based on mouse action and has no way of knowing if a counter is selected
                // If no counter's selected there's no marker to create and the event can be ignored.
                return;
            }

            // a new marker has been added
            if (e.IsNew)
            {
                Debug.Assert(e.Marker.DataLabel != null, "Markable canvas unexpectedly sent new marker with data label set.");
                this.AddMarkerToCounter(selectedCounter, e.Marker);
                return;
            }

            // an existing marker has been deleted
            if (this.dataHandler.TryDecrementOrResetCounter(selectedCounter))
            {
                this.Speak(selectedCounter.Content); // speak the current count
            }

            // remove the marker in memory and from the database
            MarkersForCounter markersForCounter;
            if (this.TryGetMarkersForCounter(selectedCounter, out markersForCounter) == false)
            {
                Debug.Fail(String.Format("Markers for counter {0} not found.", selectedCounter.DataLabel));
            }
            markersForCounter.RemoveMarker(e.Marker);
            this.dataHandler.FileDatabase.SetMarkerPositions(this.dataHandler.ImageCache.Current.ID, markersForCounter);

            this.MarkableCanvas_UpdateMarkers();
        }

        private void MarkableCanvas_UpdateMarkers()
        {
            this.MarkableCanvas.Markers = this.GetDisplayMarkers();
        }

        private void MenuEdit_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            if (this.IsFileAvailable())
            {
                this.MenuEditDeleteCurrentFile.IsEnabled = this.dataHandler.ImageCache.Current.ImageQuality != FileSelection.NoLongerAvailable;
                this.MenuEditDeleteCurrentFileAndData.IsEnabled = true;
            }
            else
            {
                this.MenuEditDeleteCurrentFile.IsEnabled = false;
                this.MenuEditDeleteCurrentFileAndData.IsEnabled = false;
            }

            int deletedImages = this.dataHandler.FileDatabase.GetFileCount(FileSelection.MarkedForDeletion);
            this.MenuEditDeleteFiles.IsEnabled = deletedImages > 0;
            this.MenuEditDeleteFilesAndData.IsEnabled = deletedImages > 0;
        }

        private void MenuEditCopy_Click(object sender, RoutedEventArgs e)
        {
            if (this.IsFileAvailable())
            {
                DataObject clipboardData = new DataObject(this.dataHandler.ImageCache.Current.AsDictionary());
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
            DateCorrectAmbiguous ambiguousDateCorrection = new DateCorrectAmbiguous(this.dataHandler.FileDatabase, this);
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

        private void MenuEditDarkImages_Click(object sender, RoutedEventArgs e)
        {
            using (DarkImagesThreshold darkThreshold = new DarkImagesThreshold(this.dataHandler.FileDatabase, this.dataHandler.ImageCache.CurrentRow, this.state, this))
            {
                darkThreshold.ShowDialog();
            }
        }

        /// <summary>Correct the date by specifying an offset.</summary>
        private async void MenuEditDateTimeFixedCorrection_Click(object sender, RoutedEventArgs e)
        {
            DateTimeFixedCorrection fixedDateCorrection = new DateTimeFixedCorrection(this.dataHandler.FileDatabase, this.dataHandler.ImageCache.Current, this);
            await this.ShowBulkFileEditDialogAsync(fixedDateCorrection);
        }

        /// <summary>Correct for drifting clock times. Correction applied only to selected files.</summary>
        private async void MenuEditDateTimeLinearCorrection_Click(object sender, RoutedEventArgs e)
        {
            DateTimeLinearCorrection linearDateCorrection = new DateTimeLinearCorrection(this.dataHandler.FileDatabase, this);
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
            if (this.dataHandler.ImageCache.Current.IsDisplayable() == false)
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

            DateDaylightSavingsTimeCorrection daylightSavingsCorrection = new DateDaylightSavingsTimeCorrection(this.dataHandler.FileDatabase, this.dataHandler.ImageCache, this);
            await this.ShowBulkFileEditDialogAsync(daylightSavingsCorrection);
        }

        /// <summary>Soft delete one or more files marked for deletion, and optionally the data associated with those files.</summary>
        private async void MenuEditDeleteFiles_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = sender as MenuItem;

            // this callback is invoked by DeleteCurrentFile and DeleteFiles
            // The logic therefore branches for removing a single file versus all selected files marked for deletion.
            List<ImageRow> imagesToDelete;
            bool deleteCurrentImageOnly;
            bool deleteFilesAndData;
            if (menuItem.Name.Equals(this.MenuEditDeleteFiles.Name) || menuItem.Name.Equals(this.MenuEditDeleteFilesAndData.Name))
            {
                deleteCurrentImageOnly = false;
                deleteFilesAndData = menuItem.Name.Equals(this.MenuEditDeleteFilesAndData.Name);
                // get files marked for deletion in the current seletion
                imagesToDelete = this.dataHandler.FileDatabase.GetFilesMarkedForDeletion().ToList();
                for (int index = imagesToDelete.Count - 1; index >= 0; index--)
                {
                    if (this.dataHandler.FileDatabase.Files.Find(imagesToDelete[index].ID) == null)
                    {
                        imagesToDelete.Remove(imagesToDelete[index]);
                    }
                }
            }
            else
            {
                // delete current file
                deleteCurrentImageOnly = true;
                deleteFilesAndData = menuItem.Name.Equals(this.MenuEditDeleteCurrentFileAndData.Name);
                imagesToDelete = new List<ImageRow>();
                if (this.dataHandler.ImageCache.Current != null)
                {
                    imagesToDelete.Add(this.dataHandler.ImageCache.Current);
                }
            }

            // notify the user if no files are selected for deletion
            // This should be unreachable as the invoking menu item should be disabled.
            if (imagesToDelete == null || imagesToDelete.Count < 1)
            {
                MessageBox messageBox = new MessageBox("No files are marked for deletion.", this);
                messageBox.Message.Problem = "You are trying to delete files marked for deletion, but no files have thier 'Delete?' box checked.";
                messageBox.Message.Hint = "If you have files that you think should be deleted, check thier Delete? box.";
                messageBox.Message.StatusImage = MessageBoxImage.Information;
                messageBox.ShowDialog();
                return;
            }

            DeleteFiles deleteImagesDialog = new DeleteFiles(this.dataHandler.FileDatabase, imagesToDelete, deleteFilesAndData, deleteCurrentImageOnly, this);
            bool? result = deleteImagesDialog.ShowDialog();
            if (result == true)
            {
                // cache the current ID as the current image may be invalidated
                long currentFileID = this.dataHandler.ImageCache.Current.ID;

                Mouse.OverrideCursor = Cursors.Wait;
                List<ColumnTuplesWithWhere> imagesToUpdate = new List<ColumnTuplesWithWhere>();
                List<long> imageIDsToDropFromDatabase = new List<long>();
                foreach (ImageRow image in imagesToDelete)
                {
                    // invalidate cache so FileNoLongerAvailable placeholder will be displayed
                    // release any handle open on the file so it can be moved
                    this.dataHandler.ImageCache.TryInvalidate(image.ID);
                    if (image.TryMoveFileToDeletedFilesFolder(this.FolderPath) == false)
                    {
                        // attempt to soft delete file failed so leave the image as marked for deletion
                        continue;
                    }

                    if (deleteFilesAndData)
                    {
                        // mark the image row for dropping
                        imageIDsToDropFromDatabase.Add(image.ID);
                    }
                    else
                    {
                        // as only the file was deleted, change image quality to FileNoLongerAvailable and clear the delete flag
                        image.DeleteFlag = false;
                        image.ImageQuality = FileSelection.NoLongerAvailable;
                        List<ColumnTuple> columnTuples = new List<ColumnTuple>()
                        {
                            new ColumnTuple(Constant.DatabaseColumn.DeleteFlag, Boolean.FalseString),
                            new ColumnTuple(Constant.DatabaseColumn.ImageQuality, FileSelection.NoLongerAvailable.ToString())
                        };
                        imagesToUpdate.Add(new ColumnTuplesWithWhere(columnTuples, image.ID));
                    }
                }

                if (deleteFilesAndData)
                {
                    // drop images
                    this.dataHandler.FileDatabase.DeleteFilesAndMarkers(imageIDsToDropFromDatabase);

                    // Reload the file data table. Then find and show the file closest to the last one shown
                    if (this.dataHandler.FileDatabase.CurrentlySelectedFileCount > 0)
                    {
                        await this.SelectFilesAndShowFileAsync(currentFileID, this.dataHandler.FileDatabase.ImageSet.FileSelection);
                    }
                    else
                    {
                        await this.EnableOrDisableMenusAndControlsAsync();
                    }
                }
                else
                {
                    // update image properties
                    this.dataHandler.FileDatabase.UpdateFiles(imagesToUpdate);

                    // display the updated properties on the current file or, if data for the current file was dropped, the next one
                    await this.ShowFileAsync(this.dataHandler.FileDatabase.GetFileOrNextFileIndex(currentFileID));
                }
                Mouse.OverrideCursor = null;
            }
        }

        /// <summary>Add some text to the image set log</summary>
        private void MenuEditLog_Click(object sender, RoutedEventArgs e)
        {
            EditLog editImageSetLog = new EditLog(this.dataHandler.FileDatabase.ImageSet.Log, this);
            bool? result = editImageSetLog.ShowDialog();
            if (result == true)
            {
                this.dataHandler.FileDatabase.ImageSet.Log = editImageSetLog.Log.Text;
                this.dataHandler.FileDatabase.SyncImageSetToDatabase();
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

            this.PasteValuesToCurrentFileWithUndo(valuesFromClipboard);
        }

        private void MenuEditPasteFromAnalysis_Click(object sender, RoutedEventArgs e)
        {
            if (this.IsFileAvailable())
            {
                this.TryPasteValuesFromAnalysis((int)((MenuItem)sender).Tag);
            }
        }

        // Populate a data field from metadata (example metadata displayed from the currently selected image)
        private async void MenuEditPopulateFieldFromMetadata_Click(object sender, RoutedEventArgs e)
        {
            if (this.dataHandler.ImageCache.Current.IsDisplayable() == false)
            {
                int firstFileDisplayable = this.dataHandler.FileDatabase.GetCurrentOrNextDisplayableFile(this.dataHandler.ImageCache.CurrentRow);
                if (firstFileDisplayable == -1)
                {
                    // There are no displayable files and thus no metadata to choose from, so abort
                    MessageBox messageBox = new MessageBox("Can't populate a data field with image metadata.", this);
                    messageBox.Message.Problem = "Metadata is not available as no file in the image set can be read." + Environment.NewLine;
                    messageBox.Message.Reason += "Carnassial must have at least one valid file in order to get its metadata.  All files are either corrupted or removed.";
                    messageBox.Message.StatusImage = MessageBoxImage.Error;
                    messageBox.ShowDialog();
                    return;
                }
            }

            PopulateFieldWithMetadata populateField = new PopulateFieldWithMetadata(this.dataHandler.FileDatabase, this.dataHandler.ImageCache.Current.GetFilePath(this.FolderPath), this);
            await this.ShowBulkFileEditDialogAsync(populateField);
        }

        private void MenuEditRedo_Click(object sender, RoutedEventArgs e)
        {
            if (this.IsFileAvailable() == false)
            {
                return;
            }

            UndoRedoState stateToRedoTo;
            if (this.state.UndoRedoChain.TryGetRedo(out stateToRedoTo))
            {
                this.ApplyUndoRedoState(stateToRedoTo);
            }
            this.MenuEditRedo.IsEnabled = this.state.UndoRedoChain.CanRedo;
            this.MenuEditUndo.IsEnabled = this.state.UndoRedoChain.CanUndo;
        }

        private async void MenuEditRereadDateTimesFromFiles_Click(object sender, RoutedEventArgs e)
        {
            DateTimeRereadFromFiles rereadDates = new DateTimeRereadFromFiles(this.dataHandler.FileDatabase, this);
            await this.ShowBulkFileEditDialogAsync(rereadDates);
        }

        private void MenuEditResetValues_Click(object sender, RoutedEventArgs e)
        {
            if (this.IsFileAvailable() == false)
            {
                return;
            }

            this.state.UndoRedoChain.AddStateIfDifferent(this.dataHandler.ImageCache.Current);
            foreach (KeyValuePair<string, DataEntryControl> pair in this.DataEntryControls.ControlsByDataLabel)
            {
                DataEntryControl control = pair.Value;
                if (this.dataHandler.FileDatabase.IsControlCopyable(control.DataLabel))
                {
                    control.SetContentAndTooltip(control.DefaultValue);
                }
            }
            this.state.UndoRedoChain.AddStateIfDifferent(this.dataHandler.ImageCache.Current);

            this.MenuEditRedo.IsEnabled = this.state.UndoRedoChain.CanRedo;
            this.MenuEditUndo.IsEnabled = this.state.UndoRedoChain.CanUndo;
        }

        private async void MenuEditSetTimeZone_Click(object sender, RoutedEventArgs e)
        {
            DateTimeSetTimeZone setTimeZone = new DateTimeSetTimeZone(this.dataHandler.FileDatabase, this.dataHandler.ImageCache.Current, this);
            await this.ShowBulkFileEditDialogAsync(setTimeZone);
        }

        private async void MenuEditToggleCurrentFileDeleteFlag_Click(object sender, RoutedEventArgs e)
        {
            await this.ToggleCurrentFileDeleteFlagAsync();
        }

        private void MenuEditUndo_Click(object sender, RoutedEventArgs e)
        {
            if (this.IsFileAvailable() == false)
            {
                return;
            }

            UndoRedoState stateToUndoTo;
            if (this.state.UndoRedoChain.TryGetUndo(out stateToUndoTo))
            {
                this.ApplyUndoRedoState(stateToUndoTo);
            }
            this.MenuEditRedo.IsEnabled = this.state.UndoRedoChain.CanRedo;
            this.MenuEditUndo.IsEnabled = this.state.UndoRedoChain.CanUndo;
        }

        private void MenuFile_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            this.MenuFileRecentImageSets_Refresh();
        }

        private async void MenuFileAddFilesToImageSet_Click(object sender, RoutedEventArgs e)
        {
            IEnumerable<string> folderPaths;
            if (this.ShowFolderSelectionDialog(out folderPaths))
            {
                FolderLoad folderLoad = new FolderLoad();
                folderLoad.FolderPaths.AddRange(folderPaths);
                await this.TryBeginFolderLoadAsync(folderLoad);
            }
        }

        /// <summary>
        /// Make a copy of the current file in the folder selected by the user and provide feedback in the status.
        /// </summary>
        private void MenuFileCloneCurrent_Click(object sender, RoutedEventArgs e)
        {
            Debug.Assert(this.dataHandler != null && this.dataHandler.ImageCache.Current != null, "MenuFileCloneCurrent unexpectedly enabled.");
            if (!this.dataHandler.ImageCache.Current.IsDisplayable())
            {
                MessageBox messageBox = new MessageBox("Can't copy this file!", this);
                messageBox.Message.StatusImage = MessageBoxImage.Error;
                messageBox.Message.Problem = "Carnassial can't copy the current file.";
                messageBox.Message.Reason = "It is likely corrupted or missing.";
                messageBox.Message.Solution = "Make sure you have navigated to, and are displaying, a valid file before you try to export it.";
                messageBox.ShowDialog();
                return;
            }

            string sourceFileName = this.dataHandler.ImageCache.Current.FileName;

            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Title = "Make a copy of the currently displayed file";
            dialog.Filter = String.Format("*{0}|*{0}", Path.GetExtension(this.dataHandler.ImageCache.Current.FileName));
            dialog.FileName = sourceFileName;
            dialog.OverwritePrompt = true;

            DialogResult result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                // Set the source and destination file names, including the complete path
                string sourcePath = this.dataHandler.ImageCache.Current.GetFilePath(this.FolderPath);
                string destinationPath = dialog.FileName;

                // Try to copy the source file to the destination, overwriting the destination file if it already exists.
                // And giving some feedback about its success (or failure) 
                try
                {
                    File.Copy(sourcePath, destinationPath, true);
                    this.statusBar.SetMessage(sourceFileName + " copied to " + destinationPath);
                }
                catch (Exception exception)
                {
                    Debug.Fail(String.Format("Copy of '{0}' to '{1}' failed.", sourceFileName, destinationPath), exception.ToString());
                    this.statusBar.SetMessage("Copy failed with {0}.", exception.GetType().Name);
                }
            }
        }

        private async void MenuFileCloseImageSet_Click(object sender, RoutedEventArgs e)
        {
            await this.CloseImageSetAsync();
            this.InstructionPane.IsActive = true;
        }

        private async void MenuFileLoadImageSet_Click(object sender, RoutedEventArgs e)
        {
            string templateDatabaseFilePath;
            if (this.TryGetTemplatePath(out templateDatabaseFilePath))
            {
                await this.TryOpenTemplateAndBeginLoadFoldersAsync(templateDatabaseFilePath);
            }     
        }

        private async void MenuFileMoveFiles_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog folderSelectionDialog = new CommonOpenFileDialog();
            folderSelectionDialog.Title = "Select the folder to move files to...";
            folderSelectionDialog.DefaultDirectory = this.FolderPath;
            folderSelectionDialog.InitialDirectory = folderSelectionDialog.DefaultDirectory;
            folderSelectionDialog.IsFolderPicker = true;
            folderSelectionDialog.FolderChanging += this.FolderSelectionDialog_FolderChanging;
            if (folderSelectionDialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                // move files
                List<string> immovableFiles = this.dataHandler.FileDatabase.MoveFilesToFolder(folderSelectionDialog.FileName);
                this.statusBar.SetMessage("Moved {0} of {1} files to {2}.", this.dataHandler.FileDatabase.CurrentlySelectedFileCount - immovableFiles.Count, this.dataHandler.FileDatabase.CurrentlySelectedFileCount, Path.GetFileName(folderSelectionDialog.FileName));
                if (immovableFiles.Count > 0)
                {
                    MessageBox messageBox = new MessageBox("Not all files could be moved.", this);
                    messageBox.Message.Title = "Conflicts prevented some files from being moved.";
                    messageBox.Message.What = String.Format("{0} of {1} files were moved.", this.dataHandler.FileDatabase.CurrentlySelectedFileCount - immovableFiles.Count, this.dataHandler.FileDatabase.CurrentlySelectedFileCount);
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
                await this.ShowFileAsync(this.dataHandler.ImageCache.CurrentRow);
            }
        }

        /// <summary>Write the .csv or .xlsx file and maybe send an open command to the system</summary>
        private void MenuFileExportSpreadsheet_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = (MenuItem)sender;
            bool exportXlsx = (sender == this.MenuFileExportXlsxAndOpen) || (sender == this.MenuFileExportXlsx);
            bool openFile = (sender == this.MenuFileExportXlsxAndOpen) || (sender == this.MenuFileExportCsvAndOpen);

            // backup any existing file as it's overwritten on export
            string spreadsheetFileExtension = exportXlsx ? Constant.File.ExcelFileExtension : Constant.File.CsvFileExtension;
            string spreadsheetFileName = Path.GetFileNameWithoutExtension(this.dataHandler.FileDatabase.FileName) + spreadsheetFileExtension;
            string spreadsheetFilePath = Path.Combine(this.FolderPath, spreadsheetFileName);
            if (FileBackup.TryCreateBackup(this.FolderPath, spreadsheetFileName))
            {
                this.statusBar.SetMessage("Backup of {0} made.", spreadsheetFileName);
            }

            SpreadsheetReaderWriter spreadsheetWriter = new SpreadsheetReaderWriter();
            try
            {
                if (exportXlsx)
                {
                    spreadsheetWriter.ExportFileDataToXlsx(this.dataHandler.FileDatabase, spreadsheetFilePath);
                }
                else
                {
                    spreadsheetWriter.ExportFileDataToCsv(this.dataHandler.FileDatabase, spreadsheetFilePath);
                }
                this.statusBar.SetMessage("Data exported to " + spreadsheetFileName);

                if (openFile)
                {
                    // show the exported file in whatever program is associated with its extension
                    Process process = new Process();
                    process.StartInfo.UseShellExecute = true;
                    process.StartInfo.RedirectStandardOutput = false;
                    process.StartInfo.FileName = spreadsheetFilePath;
                    process.Start();
                }
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
        }

        private async void MenuFileImportSpreadsheet_Click(object sender, RoutedEventArgs e)
        {
            if (this.state.SuppressSpreadsheetImportPrompt == false)
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

                bool? proceeed = messageBox.ShowDialog();
                if (proceeed != true)
                {
                    return;
                }

                if (messageBox.DontShowAgain.IsChecked.HasValue)
                {
                    this.state.SuppressSpreadsheetImportPrompt = messageBox.DontShowAgain.IsChecked.Value;
                    this.MenuOptionsEnableCsvImportPrompt.IsChecked = !this.state.SuppressSpreadsheetImportPrompt;
                }
            }

            string defaultSpreadsheetFileName = Path.GetFileNameWithoutExtension(this.dataHandler.FileDatabase.FileName) + Constant.File.ExcelFileExtension;
            string spreadsheetFilePath;
            if (Utilities.TryGetFileFromUser("Select a file to merge into the current image set",
                                             Path.Combine(this.dataHandler.FileDatabase.FolderPath, defaultSpreadsheetFileName),
                                             String.Format("Spreadsheet files (*{0};*{1})|*{0};*{1}", Constant.File.CsvFileExtension, Constant.File.ExcelFileExtension),
                                             out spreadsheetFilePath) == false)
            {
                return;
            }

            // Create a backup database file
            if (FileBackup.TryCreateBackup(this.dataHandler.FileDatabase.FilePath))
            {
                this.statusBar.SetMessage("Backup of data file made.");
            }
            else
            {
                this.statusBar.SetMessage("No data file backup was made.");
            }

            SpreadsheetReaderWriter spreadsheetReader = new SpreadsheetReaderWriter();
            try
            {
                List<string> importErrors;
                bool importSuccededFully;
                if (String.Equals(Path.GetExtension(spreadsheetFilePath), Constant.File.ExcelFileExtension, StringComparison.OrdinalIgnoreCase))
                {
                    importSuccededFully = spreadsheetReader.TryImportFileDataFromXlsx(spreadsheetFilePath, this.dataHandler.FileDatabase, out importErrors);
                }
                else
                {
                    importSuccededFully = spreadsheetReader.TryImportFileDataFromCsv(spreadsheetFilePath, this.dataHandler.FileDatabase, out importErrors);
                }
                if (importSuccededFully == false)
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
                    foreach (string importError in importErrors)
                    {
                        messageBox.Message.Hint += "\u2022 " + importError;
                    }
                    messageBox.ShowDialog();
                }
            }
            catch (Exception exception)
            {
                MessageBox messageBox = new MessageBox("Can't import the .csv file.", this);
                messageBox.Message.StatusImage = MessageBoxImage.Error;
                messageBox.Message.Problem = String.Format("The file {0} could not be opened.", spreadsheetFilePath);
                messageBox.Message.Reason = "Most likely the file is open in another program.";
                messageBox.Message.Solution = "If the file is open in another program, close it.";
                messageBox.Message.Result = String.Format("{0}: {1}", exception.GetType().FullName, exception.Message);
                messageBox.Message.Hint = "Is the file open in Excel?";
                messageBox.ShowDialog();
            }

            // reload the file data table and update the enable/disable state of the user interface to match
            await this.SelectFilesAndShowFileAsync();
            await this.EnableOrDisableMenusAndControlsAsync();
            this.statusBar.SetMessage(".csv file imported.");
        }

        private async void MenuFileRecentImageSet_Click(object sender, RoutedEventArgs e)
        {
            await this.TryOpenTemplateAndBeginLoadFoldersAsync((string)((MenuItem)sender).ToolTip);
        }

        /// <summary>
        /// Update the list of recent databases displayed under File -> Recent Databases.
        /// </summary>
        private void MenuFileRecentImageSets_Refresh()
        {
            // remove image sets which are no longer present from the most recently used list
            // probably overkill to perform this check on every refresh rather than once at application launch, but it's not particularly expensive
            List<string> invalidPaths = new List<string>();
            foreach (string recentImageSetPath in this.state.MostRecentImageSets)
            {
                if (File.Exists(recentImageSetPath) == false)
                {
                    invalidPaths.Add(recentImageSetPath);
                }
            }

            foreach (string path in invalidPaths)
            {
                bool result = this.state.MostRecentImageSets.TryRemove(path);
                Debug.Assert(result, String.Format("Removal of image set '{0}' no longer present on disk unexpectedly failed.", path));
            }

            // Enable recent image sets only if there are recent sets and the parent menu is also enabled (indicating no image set has been loaded)
            this.MenuFileRecentImageSets.IsEnabled = this.MenuFileLoadImageSet.IsEnabled && this.state.MostRecentImageSets.Count > 0;
            this.MenuFileRecentImageSets.Items.Clear();

            // add menu items most recently used image sets
            int index = 1;
            foreach (string recentImageSetPath in this.state.MostRecentImageSets)
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
            RenameFileDatabaseFile renameFileDatabase = new RenameFileDatabaseFile(this.dataHandler.FileDatabase.FileName, this);
            bool? result = renameFileDatabase.ShowDialog();
            if (result == true)
            {
                this.dataHandler.FileDatabase.RenameFile(renameFileDatabase.NewFileName);
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
                this.state.MostRecentCheckForUpdates = about.MostRecentCheckForUpdate.Value;
            }
        }

        /// <summary>Show advanced Carnassial options</summary>
        private void MenuOptionsAdvancedCarnassialOptions_Click(object sender, RoutedEventArgs e)
        {
            AdvancedCarnassialOptions advancedCarnassialOptions = new AdvancedCarnassialOptions(this.state, this.MarkableCanvas, this);
            if (advancedCarnassialOptions.ShowDialog() == true)
            {
                // throttle may have changed; update rendering rate
                this.timerFileNavigatorSlider.Interval = this.state.Throttles.DesiredIntervalBetweenRenders;
            }
        }

        /// <summary>Show advanced image set options</summary>
        private void MenuOptionsAdvancedImageSetOptions_Click(object sender, RoutedEventArgs e)
        {
            AdvancedImageSetOptions advancedImageSetOptions = new AdvancedImageSetOptions(this.dataHandler.FileDatabase, this);
            advancedImageSetOptions.ShowDialog();
        }

        private void MenuOptionsAmbiguousDatesDialog_Click(object sender, RoutedEventArgs e)
        {
            this.state.SuppressAmbiguousDatesDialog = !this.state.SuppressAmbiguousDatesDialog;
            this.MenuOptionsEnableAmbiguousDatesDialog.IsChecked = !this.state.SuppressAmbiguousDatesDialog;
        }

        /// <summary>Toggle audio feedback on and off</summary>
        private void MenuOptionsAudioFeedback_Click(object sender, RoutedEventArgs e)
        {
            this.state.AudioFeedback = !this.state.AudioFeedback;
            this.MenuOptionsAudioFeedback.IsChecked = this.state.AudioFeedback;
        }

        private void MenuOptionsEnableCsvImportPrompt_Click(object sender, RoutedEventArgs e)
        {
            this.state.SuppressSpreadsheetImportPrompt = !this.state.SuppressSpreadsheetImportPrompt;
            this.MenuOptionsEnableCsvImportPrompt.IsChecked = !this.state.SuppressSpreadsheetImportPrompt;
        }

        private void MenuOptionsEnableFileCountOnImportDialog_Click(object sender, RoutedEventArgs e)
        {
            this.state.SuppressFileCountOnImportDialog = !this.state.SuppressFileCountOnImportDialog;
            this.MenuOptionsEnableFileCountOnImportDialog.IsChecked = !this.state.SuppressFileCountOnImportDialog;
        }

        private async void MenuOptionsOrderFilesByDateTime_Click(object sender, RoutedEventArgs e)
        {
            this.state.OrderFilesByDateTime = !this.state.OrderFilesByDateTime;
            if (this.dataHandler != null && this.dataHandler.FileDatabase != null)
            {
                this.dataHandler.FileDatabase.OrderFilesByDateTime = this.state.OrderFilesByDateTime;
            }
            this.MenuOptionsOrderFilesByDateTime.IsChecked = this.state.OrderFilesByDateTime;
            await this.SelectFilesAndShowFileAsync();
        }

        private void MenuOptionsSkipDarkFileCheck_Click(object sender, RoutedEventArgs e)
        {
            this.state.SkipDarkImagesCheck = !this.state.SkipDarkImagesCheck;
            this.MenuOptionsSkipDarkFileCheck.IsChecked = this.state.SkipDarkImagesCheck;
        }

        private async void MenuSelectCustom_Click(object sender, RoutedEventArgs e)
        {
            // the first time the custom selection dialog is launched update the DateTime and UtcOffset search terms to the time of the current image
            SearchTerm firstDateTimeSearchTerm = this.dataHandler.FileDatabase.CustomSelection.SearchTerms.First(searchTerm => searchTerm.DataLabel == Constant.DatabaseColumn.DateTime);
            if (firstDateTimeSearchTerm.GetDateTime() == Constant.ControlDefault.DateTimeValue.DateTime)
            {
                DateTimeOffset defaultDate = this.dataHandler.ImageCache.Current.GetDateTime();
                this.dataHandler.FileDatabase.CustomSelection.SetDateTimesAndOffset(defaultDate);
            }

            // show the dialog and process the resuls
            Dialog.CustomSelection customSelection = new Dialog.CustomSelection(this.dataHandler.FileDatabase, this);
            bool? changeToCustomSelection = customSelection.ShowDialog();
            if (changeToCustomSelection == true)
            {
                await this.SelectFilesAndShowFileAsync(FileSelection.Custom);
            }
            else
            {
                // if needed, uncheck the custom selection menu item
                // It's checked automatically on click by WPF but, as the user cancelled out of custom selection, this isn't correct in cases where the
                // selection isn't already custom.
                this.MenuSelectCustom.IsChecked = this.dataHandler.FileDatabase.ImageSet.FileSelection == FileSelection.Custom;
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
            else if (sender == this.MenuSelectOkFiles)
            {
                selection = FileSelection.Ok;
            }
            else if (sender == this.MenuSelectCorruptedFiles)
            {
                selection = FileSelection.Corrupt;
            }
            else if (sender == this.MenuSelectDarkFiles)
            {
                selection = FileSelection.Dark;
            }
            else if (sender == this.MenuSelectFilesNoLongerAvailable)
            {
                selection = FileSelection.NoLongerAvailable;
            }
            else if (sender == this.MenuSelectFilesMarkedForDeletion)
            {
                selection = FileSelection.MarkedForDeletion;
            }
            else
            {
                throw new ArgumentOutOfRangeException("sender", String.Format("Unknown sender {0}.", sender));
            }

            // Go to the first result (i.e., index 0) in the selection
            await this.SelectFilesAndShowFileAsync(selection);
        }

        private void MenuSelect_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            Dictionary<FileSelection, int> counts = this.dataHandler.FileDatabase.GetFileCountsBySelection();

            this.MenuSelectOkFiles.IsEnabled = counts[FileSelection.Ok] > 0;
            this.MenuSelectDarkFiles.IsEnabled = counts[FileSelection.Dark] > 0;
            this.MenuSelectCorruptedFiles.IsEnabled = counts[FileSelection.Corrupt] > 0;
            this.MenuSelectFilesNoLongerAvailable.IsEnabled = counts[FileSelection.NoLongerAvailable] > 0;
            this.MenuSelectFilesMarkedForDeletion.IsEnabled = this.dataHandler.FileDatabase.GetFileCount(FileSelection.MarkedForDeletion) > 0;
        }

        private void MenuViewApplyBookmark_Click(object sender, RoutedEventArgs e)
        {
            this.MarkableCanvas.ApplyBookmark();
        }

        /// <summary>Toggle the magnifier on and off</summary>
        private void MenuViewDisplayMagnifier_Click(object sender, RoutedEventArgs e)
        {
            bool displayMagnifier = this.dataHandler.FileDatabase.ImageSet.Options.HasFlag(ImageSetOptions.Magnifier);
            displayMagnifier = !displayMagnifier;
            this.dataHandler.FileDatabase.ImageSet.Options = this.dataHandler.FileDatabase.ImageSet.Options.SetFlag(ImageSetOptions.Magnifier, displayMagnifier);
            this.MenuOptionsDisplayMagnifier.IsChecked = displayMagnifier;
            this.MarkableCanvas.MagnifyingGlassEnabled = displayMagnifier;
        }

        /// <summary>View the combined image differences</summary>
        private async void MenuViewDifferencesCombined_Click(object sender, RoutedEventArgs e)
        {
            await this.TryViewCombinedDifferenceAsync();
        }

        /// <summary>Increase the magnification of the magnifying glass by several keyboard steps.</summary>
        private void MenuViewMagnifierIncrease_Click(object sender, RoutedEventArgs e)
        {
            this.MarkableCanvas.MagnifierZoomIn();
            this.MarkableCanvas.MagnifierZoomIn();
            this.MarkableCanvas.MagnifierZoomIn();
        }

        private async void MenuViewGotoFile_Click(object sender, RoutedEventArgs e)
        {
            if (this.IsFileDatabaseAvailable() == false)
            {
                return;
            }

            GoToFile goToFile = new GoToFile(this.dataHandler.ImageCache.CurrentRow, this.dataHandler.FileDatabase.CurrentlySelectedFileCount, this);
            if (goToFile.ShowDialog() == true)
            {
                await this.ShowFileWithoutSliderCallbackAsync(goToFile.FileIndex);
            }
        }

        /// <summary>Decrease the magnification of the magnifying glass by several keyboard steps.</summary>
        private void MenuViewMagnifierDecrease_Click(object sender, RoutedEventArgs e)
        {
            this.MarkableCanvas.MagnifierZoomOut();
            this.MarkableCanvas.MagnifierZoomOut();
            this.MarkableCanvas.MagnifierZoomOut();
        }

        private void MenuViewPlayVideo_Click(object sender, RoutedEventArgs e)
        {
            this.MarkableCanvas.TryPlayOrPauseVideo();
        }

        /// <summary>Cycle through next and previous image differences</summary>
        private async void MenuViewPreviousOrNextDifference_Click(object sender, RoutedEventArgs e)
        {
            await this.TryViewPreviousOrNextDifferenceAsync();
        }

        private void MenuViewSetBookmark_Click(object sender, RoutedEventArgs e)
        {
            this.MarkableCanvas.SetBookmark();
        }

        private void MenuViewShowDataGrid_Click(object sender, RoutedEventArgs e)
        {
            this.FileDataPane.IsActive = true;
        }

        private async void MenuViewShowFirstFile_Click(object sender, RoutedEventArgs e)
        {
            await this.ShowFileWithoutSliderCallbackAsync(0);
        }

        private void MenuViewShowImageSet_Click(object sender, RoutedEventArgs e)
        {
            this.FileViewPane.IsActive = true;
        }

        private void MenuViewShowInstructions_Click(object sender, RoutedEventArgs e)
        {
            this.InstructionPane.IsActive = true;
        }

        private async void MenuViewShowLastFile_Click(object sender, RoutedEventArgs e)
        {
            if (this.IsFileDatabaseAvailable())
            {
                await this.ShowFileWithoutSliderCallbackAsync(this.dataHandler.FileDatabase.CurrentlySelectedFileCount - 1);
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
                int increment = (int)(Constant.PageUpDownNavigationFraction * this.dataHandler.FileDatabase.CurrentlySelectedFileCount);
                await this.ShowFileWithoutSliderCallbackAsync(this.dataHandler.ImageCache.CurrentRow + increment, increment);
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
                int increment = -(int)(Constant.PageUpDownNavigationFraction * this.dataHandler.FileDatabase.CurrentlySelectedFileCount);
                await this.ShowFileWithoutSliderCallbackAsync(this.dataHandler.ImageCache.CurrentRow + increment, increment);
            }
        }

        private void MenuViewZoomIn_Click(object sender, RoutedEventArgs e)
        {
            Point mousePosition = Mouse.GetPosition(this.MarkableCanvas.ImageToDisplay);
            this.MarkableCanvas.ZoomIn();
        }

        private void MenuViewZoomOut_Click(object sender, RoutedEventArgs e)
        {
            Point mousePosition = Mouse.GetPosition(this.MarkableCanvas.ImageToDisplay);
            this.MarkableCanvas.ZoomOut();
        }

        private void MenuViewZoomToFit_Click(object sender, RoutedEventArgs e)
        {
            this.MarkableCanvas.ZoomToFit();
        }

        private void MoveFocusToNextOrPreviousControlOrImageSlider(bool moveToPreviousControl)
        {
            // identify the currently selected control
            // if focus is currently set to the canvas this defaults to the first or last control, as appropriate
            int currentControl = moveToPreviousControl ? this.DataEntryControls.Controls.Count : -1;

            IInputElement focusedElement = FocusManager.GetFocusedElement(this);
            if (focusedElement != null)
            {
                Type type = focusedElement.GetType();
                if (Constant.Control.KeyboardInputTypes.Contains(type))
                {
                    DataEntryControl focusedControl;
                    if (DataEntryHandler.TryFindFocusedControl(focusedElement, out focusedControl))
                    {
                        int index = 0;
                        foreach (DataEntryControl control in this.DataEntryControls.Controls)
                        {
                            if (Object.ReferenceEquals(focusedControl, control))
                            {
                                currentControl = index;
                            }
                            ++index;
                        }
                    }
                }
            }

            // move to the next or previous control as available
            Func<int, int> incrementOrDecrement;
            if (moveToPreviousControl)
            {
                incrementOrDecrement = (int index) => { return --index; };
            }
            else
            {
                incrementOrDecrement = (int index) => { return ++index; };
            }

            for (currentControl = incrementOrDecrement(currentControl);
                 currentControl > -1 && currentControl < this.DataEntryControls.Controls.Count;
                 currentControl = incrementOrDecrement(currentControl))
            {
                DataEntryControl control = this.DataEntryControls.Controls[currentControl];
                if (control.ContentReadOnly == false)
                {
                    control.Focus(this);
                    return;
                }
            }

            // no control was found so set focus to the slider
            // this has also the desirable side effect of binding the controls into both next and previous loops so that keys can be used to cycle
            // continuously through them
            this.FileNavigatorSlider.Focus();
        }

        /// <summary>
        /// When folder loading has completed add callbacks, prepare the UI, set up the image set, and show the file.
        /// </summary>
        private async Task OnFolderLoadingCompleteAsync(bool filesJustAdded)
        {
            // Show the image, hide the load button, and make the feedback panels visible
            this.FileViewPane.IsActive = true;
            // Set focus to the markable canvas by default so it can interpret keys. 
            this.MarkableCanvas.Focus();

            // if this is completion of an existing .ddb open set the current selection and the image index to the ones from the previous session with the image set
            // also if this is completion of import to a new .ddb
            long mostRecentFileID = this.dataHandler.FileDatabase.ImageSet.MostRecentFileID;
            FileSelection fileSelection = this.dataHandler.FileDatabase.ImageSet.FileSelection;
            if (filesJustAdded && this.dataHandler.ImageCache.Current != null)
            {
                // if this is completion of an add to an existing image set stay on the file, ideally, shown before the import
                mostRecentFileID = this.dataHandler.ImageCache.Current.ID;
                // however, the cache doesn't know file loading changed the display image so invalidate to force a redraw
                // This is heavier weight than desirable, but it's a one off.
                this.dataHandler.ImageCache.TryInvalidate(mostRecentFileID);
            }
            await this.SelectFilesAndShowFileAsync(mostRecentFileID, fileSelection);

            // match UX availability to file availability
            await this.EnableOrDisableMenusAndControlsAsync();
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Utilities.ShowExceptionReportingDialog("Carnassial needs to close.", e, this);
        }

        private void PasteAnalysis1_Click(object sender, RoutedEventArgs e)
        {
            this.TryPasteValuesFromAnalysis(0);
        }

        private void PasteAnalysis2_Click(object sender, RoutedEventArgs e)
        {
            this.TryPasteValuesFromAnalysis(1);
        }

        /// <summary>
        /// When the mouse enters a paste button highlight the controls that are copyable.
        /// </summary>
        private void PasteButton_MouseEnter(object sender, MouseEventArgs e)
        {
            this.PastePreviousValues.Background = Constant.Control.CopyableFieldHighlightBrush;

            foreach (KeyValuePair<string, DataEntryControl> pair in this.DataEntryControls.ControlsByDataLabel)
            {
                DataEntryControl control = (DataEntryControl)pair.Value;
                if (control.Copyable)
                {
                    control.Container.Background = Constant.Control.CopyableFieldHighlightBrush;
                }
            }
        }

        /// <summary>
        ///  When the mouse leaves a paste button highlight the controls that are copyable.
        /// </summary>
        private void PasteButton_MouseLeave(object sender, MouseEventArgs e)
        {
            this.PastePreviousValues.ClearValue(Control.BackgroundProperty);
            foreach (KeyValuePair<string, DataEntryControl> pair in this.DataEntryControls.ControlsByDataLabel)
            {
                DataEntryControl control = (DataEntryControl)pair.Value;
                control.Container.ClearValue(Control.BackgroundProperty);
            }
        }

        private void PastePreviousValues_Click(object sender, RoutedEventArgs e)
        {
            int previousIndex = this.dataHandler.ImageCache.CurrentRow - 1;
            if (previousIndex < 0)
            {
                // at first image, so nothing to copy
                return;
            }

            ImageRow previousFile = this.dataHandler.FileDatabase.Files[previousIndex];
            this.PasteValuesToCurrentFileWithUndo(previousFile.AsDictionary());
        }

        private void PasteValuesToCurrentFile(Dictionary<string, object> values)
        {
            foreach (KeyValuePair<string, DataEntryControl> pair in this.DataEntryControls.ControlsByDataLabel)
            {
                DataEntryControl control = pair.Value;
                object value;
                if (this.dataHandler.FileDatabase.IsControlCopyable(control.DataLabel) && values.TryGetValue(control.DataLabel, out value))
                {
                    control.SetValue(value);
                }
            }
        }

        private void PasteValuesToCurrentFileWithUndo(Dictionary<string, object> values)
        {
            this.state.UndoRedoChain.AddStateIfDifferent(this.dataHandler.ImageCache.Current);
            this.PasteValuesToCurrentFile(values);
            this.state.UndoRedoChain.AddStateIfDifferent(this.dataHandler.ImageCache.Current);

            this.MenuEditRedo.IsEnabled = this.state.UndoRedoChain.CanRedo;
            this.MenuEditUndo.IsEnabled = this.state.UndoRedoChain.CanUndo;
        }

        private async Task SelectFilesAndShowFileAsync()
        {
            Debug.Assert(this.dataHandler != null && this.dataHandler.FileDatabase != null, "Expected a file database to be available.");
            await this.SelectFilesAndShowFileAsync(this.dataHandler.FileDatabase.ImageSet.FileSelection);
        }

        private async Task SelectFilesAndShowFileAsync(FileSelection selection)
        {
            long fileID = Constant.Database.DefaultFileID;
            if (this.dataHandler != null && this.dataHandler.ImageCache != null && this.dataHandler.ImageCache.Current != null)
            {
                fileID = this.dataHandler.ImageCache.Current.ID;
            }
            await this.SelectFilesAndShowFileAsync(fileID, selection);
        }

        private async Task SelectFilesAndShowFileAsync(long fileID, FileSelection selection)
        {
            // change selection
            // if the data grid is bound the file database automatically updates its contents on SelectFiles()
            Debug.Assert(this.dataHandler != null, "SelectFilesAndShowFile() should not be reachable with a null data handler.  Is a menu item wrongly enabled?");
            Debug.Assert(this.dataHandler.FileDatabase != null, "SelectFilesAndShowFile() should not be reachable with a null database.  Is a menu item wrongly enabled?");
            this.dataHandler.FileDatabase.SelectFiles(selection);

            // explain to user if their selection has gone empty and change to all files
            if ((this.dataHandler.FileDatabase.CurrentlySelectedFileCount < 1) && (selection != FileSelection.All))
            {
                // These cases are reached when 
                // 1) datetime modifications result in no files matching a custom selection
                // 2) all files which match the selection get deleted
                this.statusBar.SetMessage("Resetting selection to 'All files'.");

                MessageBox messageBox = new MessageBox("Resetting selection to 'All files' (no files match the current selection)", this);
                messageBox.Message.StatusImage = MessageBoxImage.Information;
                switch (selection)
                {
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
                    case FileSelection.Ok:
                        messageBox.Message.Problem = "Ok files were previously selected but no files are currently OK so nothing can be shown.";
                        messageBox.Message.Reason = "No files have their ImageQuality field set to Ok.";
                        messageBox.Message.Hint = "If you have files you think should be marked as Ok, set their ImageQuality field to Ok and then reselect Ok files.";
                        break;
                    default:
                        throw new NotSupportedException(String.Format("Unhandled selection {0}.", selection));
                }
                messageBox.Message.Result = "The 'All files' selection will be applied, where all files in your image set are displayed.";
                messageBox.ShowDialog();

                selection = FileSelection.All;
                this.dataHandler.FileDatabase.SelectFiles(selection);
            }

            // update status and menu state to reflect what the user selected
            string status;
            switch (selection)
            {
                case FileSelection.All:
                    status = "(all files selected)";
                    break;
                case FileSelection.Corrupt:
                    status = "corrupted files";
                    break;
                case FileSelection.Custom:
                    status = "files matching your custom selection";
                    break;
                case FileSelection.Dark:
                    status = "dark files";
                    break;
                case FileSelection.MarkedForDeletion:
                    status = "files marked for deletion";
                    break;
                case FileSelection.NoLongerAvailable:
                    status = "files no longer available";
                    break;
                case FileSelection.Ok:
                    status = "Ok files";
                    break;
                default:
                    throw new NotSupportedException(String.Format("Unhandled file selection {0}.", selection));
            }

            this.statusBar.SetView(status);

            this.MenuSelectAllFiles.IsChecked = selection == FileSelection.All;
            this.MenuSelectCorruptedFiles.IsChecked = selection == FileSelection.Corrupt;
            this.MenuSelectDarkFiles.IsChecked = selection == FileSelection.Dark;
            this.MenuSelectOkFiles.IsChecked = selection == FileSelection.Ok;
            this.MenuSelectFilesNoLongerAvailable.IsChecked = selection == FileSelection.NoLongerAvailable;
            this.MenuSelectFilesMarkedForDeletion.IsChecked = selection == FileSelection.MarkedForDeletion;
            this.MenuSelectCustom.IsChecked = selection == FileSelection.Custom;

            // after a selection change update the file navigatior slider's range and tick space
            this.FileNavigatorSlider_EnableOrDisableValueChangedCallback(false);
            this.FileNavigatorSlider.Maximum = this.dataHandler.FileDatabase.CurrentlySelectedFileCount;  // slider is one based so no - 1 on the count
            Utilities.ConfigureNavigatorSliderTick(this.FileNavigatorSlider);

            // Display the specified file or, if it's no longer selected, the next closest one
            // Showfile() handles empty image sets, so those don't need to be checked for here.
            await this.ShowFileAsync(this.dataHandler.FileDatabase.GetFileOrNextFileIndex(fileID));

            // Update the status bar accordingly
            this.statusBar.SetCurrentFile(this.dataHandler.ImageCache.CurrentRow);
            this.statusBar.SetFileCount(this.dataHandler.FileDatabase.CurrentlySelectedFileCount);
            this.FileNavigatorSlider_EnableOrDisableValueChangedCallback(true);
        }

        private bool SendKeyToDataEntryControlOrMenu(KeyEventArgs eventData)
        {
            // check if a menu is open
            // it is sufficient to check one always visible item from each top level menu (file, edit, etc.)
            // NOTE: this must be kept in sync with the menu definitions in XAML
            if (this.MenuFileExit.IsVisible || // file menu
                this.MenuEditCopy.IsVisible || // edit menu
                this.MenuOptionsSkipDarkFileCheck.IsVisible || // options menu
                this.MenuViewShowNextFile.IsVisible || // view menu
                this.MenuSelectAllFiles.IsVisible || // select menu, and then the help menu...
                this.MenuHelpAbout.IsVisible)
            {
                return true;
            }

            // by default focus will be on the MarkableCanvas
            // opening a menu doesn't change the focus
            IInputElement focusedElement = FocusManager.GetFocusedElement(this);
            if (focusedElement == null)
            {
                return false;
            }

            // check if focus is on a control
            // NOTE: this list must be kept in sync with the System.Windows classes used by the classes in Carnassial\Util\DataEntry*.cs
            Type type = focusedElement.GetType();
            if (Constant.Control.KeyboardInputTypes.Contains(type))
            {
                // send all keys to controls by default except
                // - escape as that's a natural way to back out of a control (the user can also hit enter)
                // - tab as that's the Windows keyboard navigation standard for moving between controls
                return eventData.Key != Key.Escape && eventData.Key != Key.Tab;
            }

            return false;
        }

        /// <summary>
        /// Add user interface event handler callbacks for (possibly invisible) controls
        /// </summary>
        private void SetUserInterfaceCallbacks()
        {
            // Add data entry callbacks to all editable controls. When the user changes an image's attribute using a particular control,
            // the callback updates the matching field for that image in the database.
            foreach (KeyValuePair<string, DataEntryControl> pair in this.DataEntryControls.ControlsByDataLabel)
            {
                string controlType = this.dataHandler.FileDatabase.FileTableColumnsByDataLabel[pair.Key].ControlType;
                switch (controlType)
                {
                    case Constant.Control.Counter:
                        DataEntryCounter counter = (DataEntryCounter)pair.Value;
                        counter.ContentControl.PreviewKeyDown += this.ContentControl_PreviewKeyDown;
                        counter.ContentControl.PreviewTextInput += this.CounterControl_PreviewTextInput;
                        counter.Container.MouseEnter += this.CounterControl_MouseEnter;
                        counter.Container.MouseLeave += this.CounterControl_MouseLeave;
                        counter.LabelControl.Click += this.CounterControl_Click;
                        break;
                    case Constant.Control.Flag:
                    case Constant.DatabaseColumn.DeleteFlag:
                        DataEntryFlag flag = (DataEntryFlag)pair.Value;
                        flag.ContentControl.PreviewKeyDown += this.ContentControl_PreviewKeyDown;
                        break;
                    case Constant.Control.FixedChoice:
                    case Constant.DatabaseColumn.ImageQuality:
                        DataEntryChoice choice = (DataEntryChoice)pair.Value;
                        choice.ContentControl.PreviewKeyDown += this.ContentControl_PreviewKeyDown;
                        break;
                    case Constant.Control.Note:
                    case Constant.DatabaseColumn.File:
                    case Constant.DatabaseColumn.RelativePath:
                        DataEntryNote note = (DataEntryNote)pair.Value;
                        note.ContentControl.PreviewKeyDown += this.ContentControl_PreviewKeyDown;
                        break;
                    case Constant.DatabaseColumn.DateTime:
                        DataEntryDateTime dateTime = (DataEntryDateTime)pair.Value;
                        dateTime.ContentControl.PreviewKeyDown += this.ContentControl_PreviewKeyDown;
                        break;
                    case Constant.DatabaseColumn.UtcOffset:
                        DataEntryUtcOffset utcOffset = (DataEntryUtcOffset)pair.Value;
                        utcOffset.ContentControl.PreviewKeyDown += this.ContentControl_PreviewKeyDown;
                        break;
                    default:
                        Debug.Fail(String.Format("Unhandled control type '{0}'.", controlType));
                        break;
                }
            }
        }

        // Various dialogs perform a bulk edit, after which the current file's data needs to be refreshed.
        private async Task ShowBulkFileEditDialogAsync(Window dialog)
        {
            Debug.Assert((dialog.GetType() == typeof(PopulateFieldWithMetadata)) ||
                         (dialog.GetType() == typeof(DateTimeFixedCorrection)) ||
                         (dialog.GetType() == typeof(DateTimeLinearCorrection)) ||
                         (dialog.GetType() == typeof(DateDaylightSavingsTimeCorrection)) ||
                         (dialog.GetType() == typeof(DateCorrectAmbiguous)) ||
                         (dialog.GetType() == typeof(DateTimeSetTimeZone)) ||
                         (dialog.GetType() == typeof(DateTimeRereadFromFiles)),
                         String.Format("Unexpected dialog {0}.", dialog.GetType()));

            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                // load the changes made through the current dialog
                long currentFileID = this.dataHandler.ImageCache.Current.ID;
                this.dataHandler.FileDatabase.SelectFiles(this.dataHandler.FileDatabase.ImageSet.FileSelection);

                // show updated data for file
                // Delete isn't considered a bulk edit so none of the bulk edit dialogs can result in a change in the image which needs to be displayed.
                // Hence the image cache doesn't need to be invalidated.  However, SelectFiles() may mean the currently displayed file is no longer selected.
                await this.ShowFileAsync(this.dataHandler.FileDatabase.GetFileOrNextFileIndex(currentFileID));
            }
        }

        private async Task ShowFileAsync(int fileIndex)
        {
            // if there is no file to show, then show an image indicating no image set or an empty image set
            if ((this.IsFileDatabaseAvailable() == false) || (this.dataHandler.FileDatabase.CurrentlySelectedFileCount < 1))
            {
                this.MarkableCanvas.SetNewImage(Constant.Images.NoSelectableFile.Value, null);
                this.markersOnCurrentFile = null;
                this.MarkableCanvas_UpdateMarkers();
                return;
            }

            // for the image caching logic below to work this should be the only place where code in CarnassialWindow moves the file enumerator
            int prefetchStride = 1;
            if (this.state.FileNavigatorSliderDragging)
            {
                prefetchStride = 0;
            }
            MoveToFileResult moveToFile = await this.dataHandler.ImageCache.TryMoveToFileAsync(fileIndex, prefetchStride);
            if (moveToFile.Succeeded == false)
            {
                throw new ArgumentOutOfRangeException("fileIndex", String.Format("{0} is not a valid index in the file table.", fileIndex));
            }

            // update each control with the data for the now current file
            // This is always done as it's assumed either the file being displayed changed or that a control refresh is required due to database changes
            // the call to TryMoveToFile() above refreshes the data stored under this.dataHandler.ImageCache.Current.
            this.dataHandler.IsProgrammaticControlUpdate = true;
            foreach (KeyValuePair<string, DataEntryControl> control in this.DataEntryControls.ControlsByDataLabel)
            {
                // update value
                string controlType = this.dataHandler.FileDatabase.FileTableColumnsByDataLabel[control.Key].ControlType;
                control.Value.SetValue(this.dataHandler.ImageCache.Current.GetValue(control.Value.DataLabel));

                // for note controls, update the autocomplete list if an edit occurred
                if (controlType == Constant.Control.Note)
                {
                    DataEntryNote noteControl = (DataEntryNote)control.Value;
                    if (noteControl.ContentChanged)
                    {
                        noteControl.SetAutocompletions(this.dataHandler.FileDatabase.GetDistinctValuesInFileDataColumn(control.Value.DataLabel));
                        noteControl.ContentChanged = false;
                    }
                }
            }
            this.dataHandler.IsProgrammaticControlUpdate = false;

            // update status bar
            // The file count is always refreshed as it's not known if ShowFile() is being called due to a seletion change.
            this.statusBar.SetCurrentFile(fileIndex);
            this.statusBar.SetFileCount(this.dataHandler.FileDatabase.CurrentlySelectedFileCount);
            this.statusBar.ClearMessage();

            // update nav slider thumb's position to the current file
            this.FileNavigatorSlider.Value = Utilities.ToDisplayIndex(fileIndex);

            // display new file and update menu item enables if the file changed
            // This avoids unnecessary image reloads and refreshes in cases where ShowFile() is just being called to refresh controls.
            if (moveToFile.NewFileToDisplay)
            {
                // show the file
                this.markersOnCurrentFile = this.dataHandler.FileDatabase.GetMarkersOnFile(this.dataHandler.ImageCache.Current.ID);
                List<Marker> displayMarkers = this.GetDisplayMarkers();

                bool isVideo = this.dataHandler.ImageCache.Current.IsVideo;
                if (isVideo)
                {
                    this.MarkableCanvas.SetNewVideo(this.dataHandler.ImageCache.Current.GetFileInfo(this.FolderPath), displayMarkers);
                }
                else
                {
                    this.MarkableCanvas.SetNewImage(this.dataHandler.ImageCache.GetCurrentImage(), displayMarkers);
                }

                // enable or disable menu items whose availability depends on whether the file's an image or video
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

                // draw markers for this file
                this.MarkableCanvas_UpdateMarkers();

                // clear any undo buffer as it no longer applies and disable the associated menu item
                this.MenuEditRedo.IsEnabled = false;
                this.MenuEditUndo.IsEnabled = false;
                this.state.UndoRedoChain.Clear();
            }

            // if the data grid has been bound, set the selected row to the current file and scroll so it's visible
            if (this.DataGrid.Items != null &&
                this.DataGrid.Items.Count > fileIndex &&
                this.DataGrid.SelectedIndex != fileIndex)
            {
                this.DataGrid.SelectAndScrollIntoView(fileIndex);
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
            int newFileIndex = this.dataHandler.ImageCache.CurrentRow + increment;

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
            if (newFileIndex == this.dataHandler.ImageCache.CurrentRow)
            {
                return;
            }

            // clamp to the maximum or minimum row available
            if (newFileIndex >= this.dataHandler.FileDatabase.CurrentlySelectedFileCount)
            {
                newFileIndex = this.dataHandler.FileDatabase.CurrentlySelectedFileCount - 1;
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
            CommonOpenFileDialog folderSelectionDialog = new CommonOpenFileDialog();
            folderSelectionDialog.Title = "Select one or more folders...";
            folderSelectionDialog.DefaultDirectory = this.state.MostRecentFileAddFolderPath;
            folderSelectionDialog.InitialDirectory = folderSelectionDialog.DefaultDirectory;
            folderSelectionDialog.IsFolderPicker = true;
            folderSelectionDialog.Multiselect = true;
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
                    this.state.MostRecentFileAddFolderPath = parentFolderPath;
                }
                return true;
            }

            folderPaths = null;
            return false;
        }

        private void Speak(string text)
        {
            if (this.state.AudioFeedback)
            {
                // cancel any speech in progress and say the given text
                this.speechSynthesizer.SpeakAsyncCancelAll();
                this.speechSynthesizer.SpeakAsync(text);
            }
        }

        private async Task ToggleCurrentFileDeleteFlagAsync()
        {
            DataEntryFlag deleteFlag = (DataEntryFlag)this.DataEntryControls.ControlsByDataLabel[Constant.DatabaseColumn.DeleteFlag];
            deleteFlag.ContentControl.IsChecked = !deleteFlag.ContentControl.IsChecked;

            // if the current file was just marked for deletion presumably the user is done with it and ready to move to the next
            // This autoadvance saves the user having to keep backing out of data entry and hitting the next arrow, so offers substantial savings when
            // working through large numbers of wind triggers or such but may not be desirable in all cases.  If needed an option can be added to disable
            // the behavior.
            if (deleteFlag.ContentControl.IsChecked == true)
            {
                await this.ShowFileWithoutSliderCallbackAsync(true, ModifierKeys.None);
            }
        }

        // out parameters can't be used in anonymous methods, so a separate pointer to backgroundWorker is required for return to the caller
        private async Task<bool> TryBeginFolderLoadAsync(FolderLoad folderLoad)
        {
            List<FileInfo> filesToAdd = folderLoad.GetFiles();
            if (filesToAdd.Count == 0)
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

                IEnumerable<string> folderPaths;
                if (this.ShowFolderSelectionDialog(out folderPaths))
                {
                    folderLoad.FolderPaths.Clear();
                    folderLoad.FolderPaths.AddRange(folderPaths);
                    return await this.TryBeginFolderLoadAsync(folderLoad);
                }

                // exit if user changed their mind about trying again
                return false;
            }

            // update UI for import (visibility is inverse of RunWorkerCompleted)
            this.FeedbackControl.Visibility = Visibility.Visible;
            this.FileNavigatorSlider.Visibility = Visibility.Collapsed;
            this.MenuOptions.IsEnabled = true;
            IProgress<FolderLoadProgress> folderLoadStatus = new Progress<FolderLoadProgress>(this.UpdateFolderLoadProgress);
            FolderLoadProgress folderLoadProgress = new FolderLoadProgress(filesToAdd.Count, (int)this.Width);
            folderLoadStatus.Report(folderLoadProgress);
            if (this.state.SkipDarkImagesCheck)
            {
                this.statusBar.SetMessage("Loading folders...");
            }
            else
            {
                this.statusBar.SetMessage("Loading folders (if this is slower than you like and dark image detection isn't needed you can select Skip dark check in the Options menu right now)...");
            }
            this.FileViewPane.IsActive = true;

            // ensure all files are selected
            // This prevents files which are in the DB but not selected from being added a second time.
            FileSelection originalSelection = this.dataHandler.FileDatabase.ImageSet.FileSelection;
            if (originalSelection != FileSelection.All)
            {
                this.dataHandler.FileDatabase.SelectFiles(FileSelection.All);
            }

            // Load all files found
            // First pass: Examine files to extract their basic properties and build a list of files not already in the database
            //
            // With 8MP images and dark calculations enabled:
            // Profiling of a 1000 image load on quad core, single 80+MB/s capable SSD with full size decoding finds:
            //   two threads:   55s, 66% CPU, 33MB/s disk
            //   three threads: 56s, 90% CPU, 22MB/s disk - worse!
            // CPU utilization is 90% jpeg decode and 10% IsDark(C++/CLI scalar).
            // 
            // Profiling of a 1000 image load on quad core, single 80+MB/s capable SSD with half size decoding finds:
            //   two threads:   34s, 60% CPU, 56MB/s disk
            //   three threads: 34s, 91% CPU, 60MB/s disk
            // So threads for half the available cores are dispatched by by default.
            //
            // With dark calculations disabled:
            // The bottleneck's the SQL insert though using more than four threads (or possibly more threads than the number of physical processors as the 
            // test machine was quad core) results in slow progress on the first 20 files or so, making the optimum number of loading threads complex as it
            // depends on amortizing startup lag across faster progress on the remaining import.  As this is comparatively minor relative to SQL (at least
            // for O(10,000) files for now just default to four threads in the disabled case.
            //
            // Note: the UI thread is free during loading.  So if loading's going slow the user can switch off dark checking asynchronously to speed up 
            // loading.
            //
            // A sequential partitioner is used as this keeps the preview images displayed to the user in pretty much the same order as they're named,
            // which is less confusing than TPL's default partitioning where the displayed image jumps back and forth through the image set.  Pulling files
            // nearly sequentially may also offer some minor disk performance benefit.
            List<ImageRow> filesToInsert = new List<ImageRow>();
            TimeZoneInfo imageSetTimeZone = this.dataHandler.FileDatabase.ImageSet.GetTimeZone();
            await Task.Run(() => Parallel.ForEach(
                new SequentialPartitioner<FileInfo>(filesToAdd),
                Utilities.GetParallelOptions(this.state.SkipDarkImagesCheck ? Environment.ProcessorCount : Environment.ProcessorCount / 2),
                (FileInfo fileInfo) =>
                {
                    ImageRow file;
                    if (this.dataHandler.FileDatabase.GetOrCreateFile(fileInfo, imageSetTimeZone, out file))
                    {
                        // the database already has an entry for this file so skip it
                        // if needed, a separate list of files to update could be generated
                        return;
                    }

                    MemoryImage memoryImage = null;
                    try
                    {
                        if (this.state.SkipDarkImagesCheck)
                        {
                            file.ImageQuality = FileSelection.Ok;
                        }
                        else
                        {
                            // load image and determine its quality
                            // As noted above, folder loading is jpeg decoding bound.  Lowering the resolution it's loaded at lowers CPU requirements and is
                            // acceptable here because dark calculation is an estimate based on using a subset of the pixels, the image is displayed only 
                            // briefly as a status update, and the user has no opportunity to zoom into it.  Downsizing too much results in a poor quality 
                            // preview and downsizing below the markable canvas's display size is somewhat self defeating but dark estimation is not too
                            // sensitive.  So image load size here is driven by the size of the window with a floor applied in FolderLoadProgress..ctor() so
                            // that it doesn't become too small.
                            // Parallel doesn't await async bodies so awaiting the load results in Parallel prematurely concluding the body task completed and
                            // dispatching another, resulting in system overload.  The simplest solution is to block this worker thread, which is OK as there
                            // are many workers and they're decoupled from the UI thread.
                            memoryImage = file.LoadAsync(this.FolderPath, folderLoadProgress.ImageRenderWidth).GetAwaiter().GetResult();
                            if (memoryImage == Constant.Images.CorruptFile.Value)
                            {
                                file.ImageQuality = FileSelection.Corrupt;
                            }
                            else
                            {
                                file.ImageQuality = memoryImage.IsDark(this.state.DarkPixelThreshold, this.state.DarkPixelRatioThreshold) ? FileSelection.Dark : FileSelection.Ok;
                            }
                        }

                        // see if the datetime can be updated from the metadata
                        file.TryReadDateTimeOriginalFromMetadata(this.FolderPath, imageSetTimeZone);
                    }
                    catch (Exception exception)
                    {
                        Debug.Fail(String.Format("Load of {0} failed as it's likely corrupted.", file.FileName), exception.ToString());
                        memoryImage = Constant.Images.CorruptFile.Value;
                        file.ImageQuality = FileSelection.Corrupt;
                    }

                    lock (filesToInsert)
                    {
                        filesToInsert.Add(file);
                    }

                    DateTime utcNow = DateTime.UtcNow;
                    if (utcNow - folderLoadProgress.MostRecentStatusDispatch > this.state.Throttles.DesiredIntervalBetweenRenders)
                    {
                        lock (folderLoadProgress)
                        {
                            if (utcNow - folderLoadProgress.MostRecentStatusDispatch > this.state.Throttles.DesiredIntervalBetweenRenders)
                            {
                                // if file was already loaded for dark checking use the resulting image
                                // otherwise, load the file for display
                                if (memoryImage != null)
                                {
                                    folderLoadProgress.Image = memoryImage;
                                }
                                else
                                {
                                    folderLoadProgress.Image = null;
                                }
                                folderLoadProgress.CurrentFile = file;
                                folderLoadProgress.CurrentFileIndex = filesToInsert.Count;
                                folderLoadProgress.DisplayImage = true;
                                folderLoadProgress.MostRecentStatusDispatch = utcNow;
                                folderLoadStatus.Report(folderLoadProgress);
                            }
                        }
                    }
                }));

            // Second pass: Update database
            // Parallel execution above produces out of order results.  Put them back in order so the user sees images in file name order when
            // reviewing the image set.
            folderLoadProgress.DatabaseInsert = true;
            await Task.Run(() =>
                {
                    filesToInsert = filesToInsert.OrderBy(file => Path.Combine(file.RelativePath, file.FileName)).ToList();
                    this.dataHandler.FileDatabase.AddFiles(filesToInsert, (ImageRow file, int fileIndex) =>
                    {
                        // skip reloading images to display as the user's already seen them import
                        folderLoadProgress.Image = null;
                        folderLoadProgress.CurrentFile = file;
                        folderLoadProgress.CurrentFileIndex = fileIndex;
                        folderLoadProgress.DisplayImage = false;
                        folderLoadStatus.Report(folderLoadProgress);
                    });
                });

            // if needed, revert to original selection
            if (originalSelection != this.dataHandler.FileDatabase.ImageSet.FileSelection)
            {
                this.dataHandler.FileDatabase.SelectFiles(originalSelection);
            }

            // hide the feedback bar, show the file slider
            this.FeedbackControl.Visibility = Visibility.Collapsed;
            this.FileNavigatorSlider.Visibility = Visibility.Visible;

            await this.OnFolderLoadingCompleteAsync(true);

            // tell the user how many files were loaded
            this.MaybeShowFileCountsDialog(true);
            return true;
        }

        private bool TryCopyValuesToAnalysis(int analysisSlot)
        {
            if (this.IsFileAvailable() == false)
            {
                return false;
            }
            this.state.Analysis[analysisSlot] = this.dataHandler.ImageCache.Current.AsDictionary();

            ((MenuItem)this.MenuEditPasteValuesFromAnalysis.Items[analysisSlot]).IsEnabled = true;
            switch (analysisSlot)
            {
                case 0:
                    this.PasteAnalysis1.IsEnabled = true;
                    break;
                case 1:
                    this.PasteAnalysis2.IsEnabled = true;
                    break;
                default:
                    // do nothing as there's no button associated with analysis 3 and higher
                    break;
            }
            return true;
        }

        private bool TryGetMarkersForCounter(DataEntryCounter counter, out MarkersForCounter markersForCounter)
        {
            markersForCounter = null;
            if (this.markersOnCurrentFile != null)
            {
                foreach (MarkersForCounter markers in this.markersOnCurrentFile)
                {
                    if (markers.DataLabel == counter.DataLabel)
                    {
                        markersForCounter = markers;
                        break;
                    }
                }
            }

            return markersForCounter != null;
        }

        private bool TryGetSelectedCounter(out DataEntryCounter selectedCounter)
        {
            foreach (DataEntryControl control in this.DataEntryControls.Controls)
            {
                if (control is DataEntryCounter)
                {
                    DataEntryCounter counter = (DataEntryCounter)control;
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
            string defaultTemplateDatabasePath;
            this.state.MostRecentImageSets.TryGetMostRecent(out defaultTemplateDatabasePath);
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
        /// <returns>true only if both the template and image database file are loaded (regardless of whether any images were loaded), false otherwise</returns>
        /// <remarks>This method doesn't particularly need to be public. But making it private imposes substantial complexity in invoking it via PrivateObject
        /// in unit tests.</remarks>
        public async Task<bool> TryOpenTemplateAndBeginLoadFoldersAsync(string templateDatabasePath)
        {
            // Try to create or open the template database
            TemplateDatabase templateDatabase;
            if (TemplateDatabase.TryCreateOrOpen(templateDatabasePath, out templateDatabase) == false)
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

                this.state.MostRecentImageSets.TryRemove(templateDatabasePath);
                this.MenuFileRecentImageSets_Refresh();
                return false;
            }

            // Try to get the file database file path
            // importImages will be true if it's a new image database file (meaning the user will be prompted import some images)
            string fileDatabaseFilePath;
            bool addFiles;
            if (this.TrySelectDatabaseFile(templateDatabasePath, out fileDatabaseFilePath, out addFiles) == false)
            {
                // No image database file was selected
                templateDatabase.Dispose();
                return false;
            }

            // Before running from an existing file database, check the controls in the template database are compatible with those
            // of the file database.
            FileDatabase fileDatabase;
            if (FileDatabase.TryCreateOrOpen(fileDatabaseFilePath, templateDatabase, this.state.OrderFilesByDateTime, this.state.CustomSelectionTermCombiningOperator, out fileDatabase) == false)
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
            templateDatabase.Dispose();

            // valid template and file database loaded
            // generate and render the data entry controls regardless of whether there are actually any files in the file database.
            this.dataHandler = new DataEntryHandler(fileDatabase);
            this.DataEntryControls.CreateControls(fileDatabase, this.dataHandler);
            this.SetUserInterfaceCallbacks();

            this.MenuFileRecentImageSets_Refresh();
            this.state.MostRecentFileAddFolderPath = fileDatabase.FolderPath;
            this.state.MostRecentImageSets.SetMostRecent(templateDatabasePath);
            this.Title = Path.GetFileName(fileDatabase.FilePath) + " - " + Constant.MainWindowBaseTitle;

            // If this is a new file database, try to load files (if any) from the folder...  
            if (addFiles)
            {
                FolderLoad folderLoad = new FolderLoad();
                folderLoad.FolderPaths.Add(this.FolderPath);
                await this.TryBeginFolderLoadAsync(folderLoad);
            }

            await this.OnFolderLoadingCompleteAsync(false);
            return true;
        }

        private bool TryPasteValuesFromAnalysis(int analysisSlot)
        {
            Dictionary<string, object> valuesFromAnalysis = this.state.Analysis[analysisSlot];
            if (valuesFromAnalysis == null)
            {
                // nothing to copy
                return false;
            }

            this.PasteValuesToCurrentFileWithUndo(valuesFromAnalysis);
            return true;
        }

        // Given the location path of the template, return:
        // - true if a database file was specified
        // - databaseFilePath: the path to the data database file (or null if none was specified).
        // - importImages: true when the database file has just been created, which means images still have to be imported.
        private bool TrySelectDatabaseFile(string templateDatabasePath, out string databaseFilePath, out bool addFiles)
        {
            addFiles = false;

            string databaseFileName;
            string directoryPath = Path.GetDirectoryName(templateDatabasePath);
            string[] fileDatabasePaths = Directory.GetFiles(directoryPath, "*.ddb");
            if (fileDatabasePaths.Length == 1)
            {
                databaseFileName = Path.GetFileName(fileDatabasePaths[0]); // Get the file name, excluding the path
            }
            else if (fileDatabasePaths.Length > 1)
            {
                ChooseFileDatabase chooseDatabaseFile = new ChooseFileDatabase(fileDatabasePaths, templateDatabasePath, this);
                bool? result = chooseDatabaseFile.ShowDialog();
                if (result == true)
                {
                    databaseFileName = chooseDatabaseFile.SelectedFile;
                }
                else
                {
                    // User cancelled .ddb selection
                    databaseFilePath = null;
                    return false;
                }
            }
            else
            {
                // There are no existing .ddb files
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

        private bool TrySetKeyboardFocusToMarkableCanvas(bool checkForControlFocus, InputEventArgs eventArgs)
        {
            // if a data entry control has focus typically focus should remain on the control
            // However, there are a few instances (mainly after enter or escape) where focus should go back to the markable canvas.  Among other things,
            // this lets arrow keys be used to move to the next file after data entry's completed.
            if (checkForControlFocus && eventArgs is KeyEventArgs)
            {
                if (this.SendKeyToDataEntryControlOrMenu((KeyEventArgs)eventArgs))
                {
                    return false;
                }
            }

            // don't raise the control on receiving keyboard focus
            Keyboard.DefaultRestoreFocusMode = RestoreFocusMode.None;
            Keyboard.Focus(this.MarkableCanvas);
            return true;
        }

        private async Task TryViewCombinedDifferenceAsync()
        {
            if ((this.IsFileAvailable() == false) || this.dataHandler.ImageCache.Current.IsVideo)
            {
                return;
            }

            this.dataHandler.ImageCache.MoveToNextStateInCombinedDifferenceCycle();
            if (this.dataHandler.ImageCache.CurrentDifferenceState == ImageDifference.Unaltered)
            {
                // unaltered image should be cached
                MemoryImage unalteredImage = this.dataHandler.ImageCache.GetCurrentImage();
                Debug.Assert(unalteredImage != null, "Unaltered image not available from image cache.");
                this.MarkableCanvas.SetDisplayImage(unalteredImage);
                this.statusBar.ClearMessage();
                return;
            }

            // generate and cache difference image if needed
            if (this.dataHandler.ImageCache.GetCurrentImage() == null)
            {
                ImageDifferenceResult result = await this.dataHandler.ImageCache.TryCalculateCombinedDifferenceAsync(this.state.DifferenceThreshold);
                switch (result)
                {
                    case ImageDifferenceResult.CurrentImageNotAvailable:
                        this.statusBar.SetMessage("Combined differences can't be shown since the current file is not a loadable image (typically it's a video, missing, or corrupt).");
                        return;
                    case ImageDifferenceResult.NextImageNotAvailable:
                        this.statusBar.SetMessage("Combined differences can't be shown since the next file is not available.");
                        return;
                    case ImageDifferenceResult.NotCalculable:
                        this.statusBar.SetMessage("Previous or next file is not compatible with {0}, most likely because it's a different size.", this.dataHandler.ImageCache.Current.FileName);
                        return;
                    case ImageDifferenceResult.PreviousImageNotAvailable:
                        this.statusBar.SetMessage("Combined differences can't be shown since the next file is not available.");
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
            this.MarkableCanvas.SetDisplayImage(this.dataHandler.ImageCache.GetCurrentImage());
            this.statusBar.SetMessage("Viewing differences from both next and previous files.");
        }

        // Cycle through difference images in the order current, then previous and next differenced images.
        // Create and cache the differenced images.
        private async Task TryViewPreviousOrNextDifferenceAsync()
        {
            if ((this.IsFileAvailable() == false) || this.dataHandler.ImageCache.Current.IsVideo)
            {
                return;
            }

            this.dataHandler.ImageCache.MoveToNextStateInPreviousNextDifferenceCycle();
            if (this.dataHandler.ImageCache.CurrentDifferenceState == ImageDifference.Unaltered)
            {
                // unaltered image should be cached
                MemoryImage unaltered = this.dataHandler.ImageCache.GetCurrentImage();
                Debug.Assert(unaltered != null, "Unaltered image not available from image cache.");
                this.MarkableCanvas.SetDisplayImage(unaltered);
                this.statusBar.ClearMessage();
                return;
            }

            // generate and cache difference image if needed
            if (this.dataHandler.ImageCache.GetCurrentImage() == null)
            {
                ImageDifferenceResult result = await this.dataHandler.ImageCache.TryCalculateDifferenceAsync(this.state.DifferenceThreshold);
                switch (result)
                {
                    case ImageDifferenceResult.CurrentImageNotAvailable:
                        this.statusBar.SetMessage("Difference can't be shown as the current file is not a displayable image (typically it's a video, missing, or corrupt).");
                        return;
                    case ImageDifferenceResult.NextImageNotAvailable:
                    case ImageDifferenceResult.PreviousImageNotAvailable:
                        this.statusBar.SetMessage("View of difference from {0} file unavailable as it is not a displayable image (typically it's a video, missing, or corrupt).", this.dataHandler.ImageCache.CurrentDifferenceState == ImageDifference.Previous ? "previous" : "next");
                        return;
                    case ImageDifferenceResult.NotCalculable:
                        this.statusBar.SetMessage("{0} file is not compatible with {1}, most likely because it's a different size.", this.dataHandler.ImageCache.CurrentDifferenceState == ImageDifference.Previous ? "Previous" : "Next", this.dataHandler.ImageCache.Current.FileName);
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
            this.MarkableCanvas.SetDisplayImage(this.dataHandler.ImageCache.GetCurrentImage());
            this.statusBar.SetMessage("Viewing difference from {0} file.", this.dataHandler.ImageCache.CurrentDifferenceState == ImageDifference.Previous ? "previous" : "next");
        }

        private async void UpdateFolderLoadProgress(FolderLoadProgress progress)
        {
            this.FeedbackControl.Message.Content = progress.GetMessage();
            this.FeedbackControl.ProgressBar.Value = progress.GetPercentage();
            if (progress.DisplayImage)
            {
                MemoryImage image;
                if (progress.Image != null)
                {
                    image = progress.Image;
                }
                else
                {
                    image = await progress.CurrentFile.LoadAsync(this.FolderPath, progress.ImageRenderWidth);
                }
                this.MarkableCanvas.SetNewImage(image, null);
            }
        }

        private async void Window_Closing(object sender, CancelEventArgs e)
        {
            await this.CloseImageSetAsync();

            // persist user specific state to the registry
            if (this.Top > -10 && this.Left > -10)
            {
                this.state.CarnassialWindowPosition = new Rect(new Point(this.Left, this.Top), new Size(this.Width, this.Height));
            }
            this.state.WriteToRegistry();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
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
            if (DateTime.UtcNow - this.state.MostRecentCheckForUpdates > Constant.CheckForUpdateInterval)
            {
                Uri latestVersionAddress = CarnassialConfigurationSettings.GetLatestReleaseApiAddress();
                if (latestVersionAddress == null)
                {
                    return;
                }

                GithubReleaseClient updater = new GithubReleaseClient(Constant.ApplicationName, latestVersionAddress);
                updater.TryGetAndParseRelease(false);
                this.state.MostRecentCheckForUpdates = DateTime.UtcNow;
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
                    await this.TryOpenTemplateAndBeginLoadFoldersAsync(filePath);
                }
                else if (String.Equals(fileExtension, Constant.File.FileDatabaseFileExtension, StringComparison.OrdinalIgnoreCase))
                {
                    string[] templatePaths = Directory.GetFiles(Path.GetDirectoryName(filePath), "*" + Constant.File.TemplateFileExtension);
                    if (templatePaths != null && templatePaths.Length == 1)
                    {
                        await this.TryOpenTemplateAndBeginLoadFoldersAsync(templatePaths[0]);
                    }
                }
            }
        }

        private async void Window_PreviewKeyDown(object sender, KeyEventArgs currentKey)
        {
            if (this.IsFileAvailable() == false)
            {
                // no file loaded so nothing to do
                return;
            }

            // if the focus is on a control in the control grid send keys to the control
            if (this.SendKeyToDataEntryControlOrMenu(currentKey))
            {
                return;
            }

            // check if input key or chord is a shortcut key and dispatch appropriately if so
            // When dispatch to an async method occurs the key typically needs to be marked handled before the async call.  Otherwise the UI thread continues
            // event processing on the reasonable assumption the key's unhandled.  This is frequently benign but can have undesirable side effects, such as
            // navigation keys changing which control has focus in addition to their intended effect.
            int keyRepeatCount = this.state.GetKeyRepeatCount(currentKey);
            switch (currentKey.Key)
            {
                case Key.B:
                    if (Keyboard.Modifiers == ModifierKeys.None)
                    {
                        // apply the current bookmark
                        this.MarkableCanvas.ApplyBookmark();
                        currentKey.Handled = true;
                    }
                    else if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        // bookmark (save) the current pan / zoom of the display image
                        this.MarkableCanvas.SetBookmark();
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
                    this.MarkableCanvas.MagnifierZoomOut();
                    currentKey.Handled = true;
                    break;
                // return to full view of display image
                case Key.D0:
                case Key.NumPad0:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.MarkableCanvas.ZoomToFit();
                        currentKey.Handled = true;
                    }
                    break;
                case Key.OemMinus:
                    this.MarkableCanvas.ZoomOut();
                    currentKey.Handled = true;
                    break;
                case Key.OemPlus:
                    this.MarkableCanvas.ZoomIn();
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
                case Key.Escape:            // exit current control, if any
                    this.TrySetKeyboardFocusToMarkableCanvas(false, currentKey);
                    currentKey.Handled = true;
                    break;
                case Key.G:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.MenuViewGotoFile_Click(this, currentKey);
                        currentKey.Handled = true;
                    }
                    break;
                case Key.M:                 // toggle the magnifying glass on and off
                    this.MenuViewDisplayMagnifier_Click(this, currentKey);
                    currentKey.Handled = true;
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
                    if (this.MarkableCanvas.TryPlayOrPauseVideo() == false)
                    {
                        currentKey.Handled = true;
                        return;
                    }
                    break;
                case Key.U:
                    this.MarkableCanvas.MagnifierZoomIn();
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
                        await this.ShowFileWithoutSliderCallbackAsync(this.dataHandler.FileDatabase.CurrentlySelectedFileCount - 1);
                    }
                    break;
                case Key.Left:              // previous image
                    currentKey.Handled = true;
                    if (keyRepeatCount % this.state.Throttles.RepeatedKeyAcceptanceInterval == 0)
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
                        await this.ShowFileWithoutSliderCallbackAsync(this.dataHandler.ImageCache.CurrentRow + (int)(Constant.PageUpDownNavigationFraction * this.dataHandler.FileDatabase.CurrentlySelectedFileCount));
                    }
                    break;
                case Key.PageUp:
                    if (this.IsFileDatabaseAvailable())
                    {
                        currentKey.Handled = true;
                        await this.ShowFileWithoutSliderCallbackAsync(this.dataHandler.ImageCache.CurrentRow - (int)(Constant.PageUpDownNavigationFraction * this.dataHandler.FileDatabase.CurrentlySelectedFileCount));
                    }
                    break;
                case Key.Right:             // next image
                    currentKey.Handled = true;
                    if (keyRepeatCount % this.state.Throttles.RepeatedKeyAcceptanceInterval == 0)
                    {
                        await this.ShowFileWithoutSliderCallbackAsync(true, Keyboard.Modifiers);
                    }
                    break;
                case Key.Tab:               // next or previous control
                    this.MoveFocusToNextOrPreviousControlOrImageSlider(Keyboard.Modifiers == ModifierKeys.Shift);
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
    }
}
