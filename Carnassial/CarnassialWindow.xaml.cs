using Carnassial.Controls;
using Carnassial.Database;
using Carnassial.Dialog;
using Carnassial.Github;
using Carnassial.Images;
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
using System.Windows.Media.Imaging;
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
        private string mostRecentFileAddFolderPath;

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

            // Recall user's state from prior sessions
            this.state.ReadFromRegistry();

            this.MenuOptionsAudioFeedback.IsChecked = this.state.AudioFeedback;
            this.MenuOptionsEnableCsvImportPrompt.IsChecked = !this.state.SuppressCsvImportPrompt;
            this.MenuOptionsOrderFilesByDateTime.IsChecked = this.state.OrderFilesByDateTime;
            this.MenuOptionsSkipDarkFileCheck.IsChecked = this.state.SkipDarkImagesCheck;

            // Timer callback so the display will update to the current slider position when the user pauses whilst dragging the slider 
            this.timerFileNavigatorSlider = new DispatcherTimer();
            this.timerFileNavigatorSlider.Interval = this.state.Throttles.DesiredIntervalBetweenRenders;
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
                pasteFromAnalysisSlot.InputGestureText = String.Format("Alt+{0}", displaySlot);
                pasteFromAnalysisSlot.IsEnabled = false;
                pasteFromAnalysisSlot.Header = String.Format("Analysis _{0}", displaySlot);
                pasteFromAnalysisSlot.Tag = analysisSlot;
                pasteFromAnalysisSlot.ToolTip = String.Format("Apply data in analysis {0}.", displaySlot);
                this.MenuEditPasteValuesFromAnalysis.Items.Add(pasteFromAnalysisSlot);

                this.state.Analysis.Add(null);
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
            get { return this.dataHandler.FileDatabase.FolderPath; }
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

        /// <summary>Ensures only numbers are entered for counters.</summary>
        private void CounterControl_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = (Utilities.IsDigits(e.Text) || String.IsNullOrWhiteSpace(e.Text)) ? false : true;
            this.OnPreviewTextInput(e);
        }

        // lazily defer binding of data grid until user selects the data gind tab
        // This reduces startup and scrolling lag as filling and updating the grid is fairly expensive.
        private void DataGridPane_IsActiveChanged(object sender, EventArgs e)
        {
            if (this.dataHandler == null || this.dataHandler.FileDatabase == null)
            {
                return;
            }

            if (this.DataGridPane.IsActive)
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

        private void EnableOrDisableMenusAndControls()
        {
            bool imageSetAvailable = (this.dataHandler != null) && (this.dataHandler.FileDatabase != null);
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
            this.MenuFileExportCsvAndView.IsEnabled = filesSelected;
            this.MenuFileExportCsv.IsEnabled = filesSelected;
            this.MenuFileImportCsv.IsEnabled = imageSetAvailable;
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
                this.ShowFile(Constant.Database.InvalidRow);
                this.statusBar.SetMessage("Image set is empty.");
                this.statusBar.SetCurrentFile(Constant.Database.InvalidRow);
                this.statusBar.SetFileCount(0);
            }
        }

        private void FileNavigatorSlider_DragCompleted(object sender, DragCompletedEventArgs args)
        {
            this.state.FileNavigatorSliderDragging = false;
            this.ShowFile(this.FileNavigatorSlider);
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

        // Timer callback that forces image update to the current slider position. Invoked as the user pauses dragging the image slider 
        private void FileNavigatorSlider_TimerTick(object sender, EventArgs e)
        {
            this.ShowFile(this.FileNavigatorSlider);
            this.timerFileNavigatorSlider.Stop();
        }

        private void FileNavigatorSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> args)
        {
            // since the minimum value is 1 there's a value change event during InitializeComponent() to ignore
            if (this.state == null)
            {
                return;
            }

            DateTime utcNow = DateTime.UtcNow;
            if ((this.state.FileNavigatorSliderDragging == false) || (utcNow - this.state.MostRecentDragEvent > this.timerFileNavigatorSlider.Interval))
            {
                this.ShowFile(this.FileNavigatorSlider);
                this.state.MostRecentDragEvent = utcNow;
            }
        }

        // Returns the currently active counter control, otherwise null
        private DataEntryCounter FindSelectedCounter()
        {
            foreach (DataEntryControl control in this.DataEntryControls.Controls)
            {
                if (control is DataEntryCounter)
                {
                    DataEntryCounter counter = (DataEntryCounter)control;
                    if (counter.IsSelected)
                    {
                        return counter;
                    }
                }
            }
            return null;
        }

        private List<Marker> GetDisplayMarkers(bool showAnnotations)
        {
            if (this.markersOnCurrentFile == null)
            {
                return null;
            }

            List<Marker> markers = new List<Marker>();
            DataEntryCounter selectedCounter = this.FindSelectedCounter();
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

        private void Instructions_Drop(object sender, DragEventArgs dropEvent)
        {
            string templateDatabaseFilePath;
            if (Utilities.IsSingleTemplateFileDrag(dropEvent, out templateDatabaseFilePath))
            {
                BackgroundWorker ignored;
                if (this.TryOpenTemplateAndBeginLoadFoldersAsync(templateDatabaseFilePath, out ignored) == false)
                {
                    this.state.MostRecentImageSets.TryRemove(templateDatabaseFilePath);
                    this.MenuFileRecentImageSets_Refresh();
                }
                dropEvent.Handled = true;
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

        /// <summary>
        /// A new marker associated with a counter control has been created
        /// Increment the counter and add the marker to all data structures (including the database)
        /// </summary>
        private void MarkableCanvas_AddMarker(DataEntryCounter counter, Marker marker)
        {
            // increment the counter to reflect the new marker
            int count;
            if (Int32.TryParse(counter.Content, out count) == false)
            {
                // if the current value's not parseable assume that 
                // 1) the default value is set to a non-integer in the template
                // 2) or it's a space
                // In either case, revert to zero.
                count = 0;
            }
            ++count;

            string counterContent = count.ToString();
            this.dataHandler.IsProgrammaticControlUpdate = true;
            this.dataHandler.FileDatabase.UpdateFile(this.dataHandler.ImageCache.Current.ID, counter.DataLabel, counterContent);
            counter.SetContentAndTooltip(counterContent);
            this.dataHandler.IsProgrammaticControlUpdate = false;

            // Find markers associated with this particular control
            MarkersForCounter markersForCounter = null;
            foreach (MarkersForCounter markers in this.markersOnCurrentFile)
            {
                if (markers.DataLabel == counter.DataLabel)
                {
                    markersForCounter = markers;
                    break;
                }
            }

            // fill in marker information
            marker.ShowLabel = true; // show label on creation, cleared on next refresh
            marker.LabelShownPreviously = false;
            marker.Brush = Brushes.Red;               // Make it Red (for now)
            marker.DataLabel = counter.DataLabel;
            marker.Tooltip = counter.Label;   // The tooltip will be the counter label plus its data label
            marker.Tooltip += "\n" + counter.DataLabel;
            markersForCounter.AddMarker(marker);

            // update this counter's list of points in the database
            this.dataHandler.FileDatabase.SetMarkerPositions(this.dataHandler.ImageCache.Current.ID, markersForCounter);

            this.MarkableCanvas.Markers = this.GetDisplayMarkers(true);
            this.Speak(counter.Content + " " + counter.Label); // Speak the current count
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
            // A marker has been added
            if (e.IsNew)
            {
                DataEntryCounter currentCounter = this.FindSelectedCounter(); // No counters are selected, so don't mark anything
                if (currentCounter == null)
                {
                    return;
                }
                this.MarkableCanvas_AddMarker(currentCounter, e.Marker);
                return;
            }

            // An existing marker has been deleted.
            DataEntryCounter counter = (DataEntryCounter)this.DataEntryControls.ControlsByDataLabel[e.Marker.DataLabel];

            // Decrement the counter only if there is a number in it
            string oldCounterData = counter.Content;
            string newCounterData = String.Empty;
            if (oldCounterData != String.Empty) 
            {
                int count = Convert.ToInt32(oldCounterData);
                count = (count == 0) ? 0 : count - 1;           // Make sure its never negative, which could happen if a person manually enters the count 
                newCounterData = count.ToString();
            }
            if (!newCounterData.Equals(oldCounterData))
            {
                // Don't bother updating if the value hasn't changed (i.e., already at a 0 count)
                // Update the datatable and database with the new counter values
                this.dataHandler.IsProgrammaticControlUpdate = true;
                counter.SetContentAndTooltip(newCounterData);
                this.dataHandler.IsProgrammaticControlUpdate = false;
                this.dataHandler.FileDatabase.UpdateFile(this.dataHandler.ImageCache.Current.ID, counter.DataLabel, newCounterData);
            }

            // Remove the marker in memory and from the database
            MarkersForCounter markersForCounter = null;
            foreach (MarkersForCounter markers in this.markersOnCurrentFile)
            {
                if (markers.Markers.Count == 0)
                {
                    continue;
                }

                if (markers.Markers[0].DataLabel == markers.DataLabel)
                {
                    markersForCounter = markers;
                    break;
                }
            }

            if (markersForCounter != null)
            {
                markersForCounter.RemoveMarker(e.Marker);
                this.Speak(counter.Content); // Speak the current count
                this.dataHandler.FileDatabase.SetMarkerPositions(this.dataHandler.ImageCache.Current.ID, markersForCounter);
            }

            this.MarkableCanvas_UpdateMarkers();
        }

        private void MarkableCanvas_UpdateMarkers()
        {
            // by default, don't show markers' labels
            this.MarkableCanvas.Markers = this.GetDisplayMarkers(false);
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
                DataObject clipboardData = new DataObject(this.dataHandler.ImageCache.Current.AsDisplayDictionary());
                Clipboard.SetDataObject(clipboardData);
            }
        }

        private void MenuEditCopyValuesToAnalysis_Click(object sender, RoutedEventArgs e)
        {
            this.TryCopyValuesToAnalysis((int)((MenuItem)sender).Tag);
        }

        // Correct ambiguous dates dialog (i.e. dates that could be read as either month/day or day/month
        private void MenuEditCorrectAmbiguousDates_Click(object sender, RoutedEventArgs e)
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
            this.ShowBulkFileEditDialog(ambiguousDateCorrection);
        }

        private void MenuEditDarkImages_Click(object sender, RoutedEventArgs e)
        {
            using (DarkImagesThreshold darkThreshold = new DarkImagesThreshold(this.dataHandler.FileDatabase, this.dataHandler.ImageCache.CurrentRow, this.state, this))
            {
                darkThreshold.ShowDialog();
            }
        }

        /// <summary>Correct the date by specifying an offset.</summary>
        private void MenuEditDateTimeFixedCorrection_Click(object sender, RoutedEventArgs e)
        {
            DateTimeFixedCorrection fixedDateCorrection = new DateTimeFixedCorrection(this.dataHandler.FileDatabase, this.dataHandler.ImageCache.Current, this);
            this.ShowBulkFileEditDialog(fixedDateCorrection);
        }

        /// <summary>Correct for drifting clock times. Correction applied only to selected files.</summary>
        private void MenuEditDateTimeLinearCorrection_Click(object sender, RoutedEventArgs e)
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
            this.ShowBulkFileEditDialog(linearDateCorrection);
        }

        /// <summary>Correct for daylight savings time</summary>
        private void MenuEditDaylightSavingsTimeCorrection_Click(object sender, RoutedEventArgs e)
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
            this.ShowBulkFileEditDialog(daylightSavingsCorrection);
        }

        /// <summary>Soft delete one or more files marked for deletion, and optionally the data associated with those files.</summary>
        private void MenuEditDeleteFiles_Click(object sender, RoutedEventArgs e)
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

            DeleteImages deleteImagesDialog = new DeleteImages(this.dataHandler.FileDatabase, imagesToDelete, deleteFilesAndData, deleteCurrentImageOnly, this);
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
                    if (image.TryMoveFileToDeletedFilesFolder(this.dataHandler.FileDatabase.FolderPath) == false)
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
                        this.SelectFilesAndShowFile(currentFileID, this.dataHandler.FileDatabase.ImageSet.FileSelection);
                    }
                    else
                    {
                        this.EnableOrDisableMenusAndControls();
                    }
                }
                else
                {
                    // update image properties
                    this.dataHandler.FileDatabase.UpdateFiles(imagesToUpdate);

                    // display the updated properties on the current file or, if data for the current file was dropped, the next one
                    this.ShowFile(this.dataHandler.FileDatabase.GetFileOrNextFileIndex(currentFileID));
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
            if (clipboardData == null || clipboardData.GetDataPresent(typeof(Dictionary<string, string>)) == false)
            {
                return;
            }
            Dictionary<string, string> sourceFile = (Dictionary<string, string>)clipboardData.GetData(typeof(Dictionary<string, string>));
            if (sourceFile == null)
            {
                return;
            }

            this.PasteValuesToCurrentFile(sourceFile);
        }

        private void MenuEditPasteFromAnalysis_Click(object sender, RoutedEventArgs e)
        {
            if (this.IsFileAvailable())
            {
                this.TryPasteValuesFromAnalysis((int)((MenuItem)sender).Tag);
            }
        }

        // Populate a data field from metadata (example metadata displayed from the currently selected image)
        private void MenuEditPopulateFieldFromMetadata_Click(object sender, RoutedEventArgs e)
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
            this.ShowBulkFileEditDialog(populateField);
        }

        private void MenuEditRereadDateTimesFromFiles_Click(object sender, RoutedEventArgs e)
        {
            DateTimeRereadFromFiles rereadDates = new DateTimeRereadFromFiles(this.dataHandler.FileDatabase, this);
            this.ShowBulkFileEditDialog(rereadDates);
        }

        private void MenuEditSetTimeZone_Click(object sender, RoutedEventArgs e)
        {
            DateTimeSetTimeZone setTimeZone = new DateTimeSetTimeZone(this.dataHandler.FileDatabase, this.dataHandler.ImageCache.Current, this);
            this.ShowBulkFileEditDialog(setTimeZone);
        }

        private void MenuEditToggleCurrentFileDeleteFlag_Click(object sender, RoutedEventArgs e)
        {
            this.ToggleCurrentFileDeleteFlag();
        }

        private void MenuEditUndo_Click(object sender, RoutedEventArgs e)
        {
            if (this.IsFileAvailable() && this.state.UndoBuffer != null)
            {
                this.PasteValuesToCurrentFile(this.state.UndoBuffer);
            }
        }

        private void MenuFile_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            this.MenuFileRecentImageSets_Refresh();
        }

        private void MenuFileAddFilesToImageSet_Click(object sender, RoutedEventArgs e)
        {
            IEnumerable<string> folderPaths;
            if (this.ShowFolderSelectionDialog(out folderPaths))
            {
                BackgroundWorker backgroundWorker;
                this.TryBeginFolderLoadAsync(folderPaths, out backgroundWorker);
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
                    this.statusBar.SetMessage(String.Format("Copy failed with {0}.", exception.GetType().Name));
                }
            }
        }

        private void MenuFileCloseImageSet_Click(object sender, RoutedEventArgs e)
        {
            if (this.TryCloseImageSet())
            {
                // switch back to the instructions pane
                this.InstructionPane.IsActive = true;
            }
        }

        private void MenuFileLoadImageSet_Click(object sender, RoutedEventArgs e)
        {
            string templateDatabasePath;
            if (this.TryGetTemplatePath(out templateDatabasePath))
            {
                BackgroundWorker ignored;
                this.TryOpenTemplateAndBeginLoadFoldersAsync(templateDatabasePath, out ignored);
            }     
        }

        private void MenuFileMoveFiles_Click(object sender, RoutedEventArgs e)
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
                if (immovableFiles.Count > 0)
                {
                    MessageBox messageBox = new MessageBox("Not all files could be moved.", this);
                    messageBox.Message.What = String.Format("{0} of {1} files were moved.", this.dataHandler.FileDatabase.CurrentlySelectedFileCount - immovableFiles.Count, this.dataHandler.FileDatabase.CurrentlySelectedFileCount);
                    messageBox.Message.Reason = "This occurs when the selection 1) contains multiple files with the same name and 2) files with the same name as files already in the destination folder.";
                    messageBox.Message.Solution = "Remove or rename the conflicting files and apply the move command again to move the remaining files.";
                    messageBox.Message.Result = "Carnassial moved the files which could be moved.  The remaining files were left in place.";
                    messageBox.Message.Hint = String.Format("The {0} files which could not be moved are{1}", immovableFiles.Count, Environment.NewLine);
                    foreach (string fileName in immovableFiles)
                    {
                        messageBox.Message.Hint += String.Format("\u2022 {0}", fileName);
                    }
                    messageBox.ShowDialog();
                }

                // refresh the current file to show its new relative path field 
                this.ShowFile(this.dataHandler.ImageCache.CurrentRow);
            }
        }

        /// <summary>Write the .csv file and preview it in Excel</summary>
        private void MenuFileExportCsv_Click(object sender, RoutedEventArgs e)
        {
            // backup any existing .csv file as it's overwritten on export
            string csvFileName = Path.GetFileNameWithoutExtension(this.dataHandler.FileDatabase.FileName) + ".csv";
            string csvFilePath = Path.Combine(this.FolderPath, csvFileName);
            if (FileBackup.TryCreateBackup(this.FolderPath, csvFileName))
            {
                this.statusBar.SetMessage("Backup of .csv file made.");
            }

            CsvReaderWriter csvWriter = new CsvReaderWriter();
            try
            {
                csvWriter.ExportToCsv(this.dataHandler.FileDatabase, csvFilePath);
                this.statusBar.SetMessage("Data exported to " + csvFileName);

                MenuItem menuItem = (MenuItem)sender;
                if (menuItem == this.MenuFileExportCsvAndView)
                {
                    // show the file in whatever program is associated with the .csv extension
                    Process process = new Process();
                    process.StartInfo.UseShellExecute = true;
                    process.StartInfo.RedirectStandardOutput = false;
                    process.StartInfo.FileName = csvFilePath;
                    process.Start();
                }
            }
            catch (IOException exception)
            {
                MessageBox messageBox = new MessageBox("Can't write the spreadsheet file.", this);
                messageBox.Message.StatusImage = MessageBoxImage.Error;
                messageBox.Message.Problem = "The following file can't be written: " + csvFilePath;
                messageBox.Message.Reason = "You may already have it open in Excel or another application.";
                messageBox.Message.Solution = "If the file is open in another application, close it and try again.";
                messageBox.Message.Hint = String.Format("{0}: {1}", exception.GetType().FullName, exception.Message);
                messageBox.ShowDialog();
                return;
            }
        }

        private void MenuFileImportCsv_Click(object sender, RoutedEventArgs e)
        {
            if (this.state.SuppressCsvImportPrompt == false)
            {
                MessageBox messageBox = new MessageBox("How import of .csv data works.", this, MessageBoxButton.OKCancel);
                messageBox.Message.What = "Importing data from a .csv (comma separated value) file follows the rules below.";
                messageBox.Message.Reason = "Carnassial requires the .csv file follow a specific format and processes its data in a specific way.";
                messageBox.Message.Solution = "Modifying and importing a .csv file is supported only if the .csv file is exported from and then back into Carnassial image sets with the same template." + Environment.NewLine;
                messageBox.Message.Solution += "A limited set of modifications is allowed:" + Environment.NewLine;
                messageBox.Message.Solution += "\u2022 Adding columns, removing columns, or changing column names is not supported." + Environment.NewLine;
                messageBox.Message.Solution += "\u2022 FileName and RelativePath identify the file updates are applied to.  Changing them causes a different file to be updated or a new file to be added." + Environment.NewLine;
                messageBox.Message.Solution += "\u2022 RelativePath is interpreted relative to the .csv file.  Make sure it's in the right place!" + Environment.NewLine;
                messageBox.Message.Solution += String.Format("\u2022 DateTime must be in '{0}' format.{1}", Constant.Time.DateTimeDatabaseFormat, Environment.NewLine);
                messageBox.Message.Solution += String.Format("\u2022 UtcOffset must be a floating point number between {0} and {1}, inclusive.{2}", DateTimeHandler.ToDatabaseUtcOffsetString(Constant.Time.MinimumUtcOffset), DateTimeHandler.ToDatabaseUtcOffsetString(Constant.Time.MinimumUtcOffset), Environment.NewLine);
                messageBox.Message.Solution += "\u2022 Counter data must be zero or a positive integer." + Environment.NewLine;
                messageBox.Message.Solution += "\u2022 Flag data must be 'true' or 'false', case insensitive." + Environment.NewLine;
                messageBox.Message.Solution += "\u2022 FixedChoice data must be a string that exactly matches one of the FixedChoice menu options or the field's default value." + Environment.NewLine;
                messageBox.Message.Result = String.Format("Carnassial will create a backup .ddb file in the {0} folder and then import as much data as it can.  If data can't be imported you'll get a dialog listing the problems.", Constant.File.BackupFolder);
                messageBox.Message.Hint = "\u2022 After you import, check your data. If it is not what you expect, restore your data by using that backup file." + Environment.NewLine;
                messageBox.Message.Hint += String.Format("\u2022 Usually the .csv file should be in the same folder as the data file ({0}) it was exported from.{1}", Constant.File.FileDatabaseFileExtension, Environment.NewLine);
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
                    this.state.SuppressCsvImportPrompt = messageBox.DontShowAgain.IsChecked.Value;
                    this.MenuOptionsEnableCsvImportPrompt.IsChecked = !this.state.SuppressCsvImportPrompt;
                }
            }

            string csvFileName = Path.GetFileNameWithoutExtension(this.dataHandler.FileDatabase.FileName) + Constant.File.CsvFileExtension;
            string csvFilePath;
            if (Utilities.TryGetFileFromUser("Select a .csv file to merge into the current image set",
                                             Path.Combine(this.dataHandler.FileDatabase.FolderPath, csvFileName),
                                             String.Format("Comma separated value files (*{0})|*{0}", Constant.File.CsvFileExtension),
                                             out csvFilePath) == false)
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

            CsvReaderWriter csvReader = new CsvReaderWriter();
            try
            {
                List<string> importErrors;
                if (csvReader.TryImportFromCsv(csvFilePath, this.dataHandler.FileDatabase, out importErrors) == false)
                {
                    MessageBox messageBox = new MessageBox("Can't import the .csv file.", this);
                    messageBox.Message.StatusImage = MessageBoxImage.Error;
                    messageBox.Message.Problem = String.Format("The file {0} could not be read.", csvFilePath);
                    messageBox.Message.Reason = "The .csv file is not compatible with the current image set.";
                    messageBox.Message.Solution = "Check that:" + Environment.NewLine;
                    messageBox.Message.Solution += "\u2022 The first row of the .csv file is a header line." + Environment.NewLine;
                    messageBox.Message.Solution += "\u2022 The column names in the header line match the database." + Environment.NewLine; 
                    messageBox.Message.Solution += "\u2022 Choice values use the correct case." + Environment.NewLine;
                    messageBox.Message.Solution += "\u2022 Counter values are numbers." + Environment.NewLine;
                    messageBox.Message.Solution += "\u2022 Flag values are either 'true' or 'false'.";
                    messageBox.Message.Result = "Either no data was imported or invalid parts of the .csv were skipped.";
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
                messageBox.Message.Problem = String.Format("The file {0} could not be opened.", csvFilePath);
                messageBox.Message.Reason = "Most likely the file is open in another program.";
                messageBox.Message.Solution = "If the file is open in another program, close it.";
                messageBox.Message.Result = String.Format("{0}: {1}", exception.GetType().FullName, exception.Message);
                messageBox.Message.Hint = "Is the file open in Excel?";
                messageBox.ShowDialog();
            }

            // reload the file data table and update the enable/disable state of the user interface to match
            this.SelectFilesAndShowFile();
            this.EnableOrDisableMenusAndControls();
            this.statusBar.SetMessage(".csv file imported.");
        }

        private void MenuFileRecentImageSet_Click(object sender, RoutedEventArgs e)
        {
            string recentDatabasePath = (string)((MenuItem)sender).ToolTip;
            BackgroundWorker backgroundWorker;
            if (this.TryOpenTemplateAndBeginLoadFoldersAsync(recentDatabasePath, out backgroundWorker) == false)
            {
                this.state.MostRecentImageSets.TryRemove(recentDatabasePath);
                this.MenuFileRecentImageSets_Refresh();
            }
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
            this.state.SuppressCsvImportPrompt = !this.state.SuppressCsvImportPrompt;
            this.MenuOptionsEnableCsvImportPrompt.IsChecked = !this.state.SuppressCsvImportPrompt;
        }

        private void MenuOptionsEnableFileCountOnImportDialog_Click(object sender, RoutedEventArgs e)
        {
            this.state.SuppressFileCountOnImportDialog = !this.state.SuppressFileCountOnImportDialog;
            this.MenuOptionsEnableFileCountOnImportDialog.IsChecked = !this.state.SuppressFileCountOnImportDialog;
        }

        private void MenuOptionsOrderFilesByDateTime_Click(object sender, RoutedEventArgs e)
        {
            this.state.OrderFilesByDateTime = !this.state.OrderFilesByDateTime;
            if (this.dataHandler != null && this.dataHandler.FileDatabase != null)
            {
                this.dataHandler.FileDatabase.OrderFilesByDateTime = this.state.OrderFilesByDateTime;
            }
            this.MenuOptionsOrderFilesByDateTime.IsChecked = this.state.OrderFilesByDateTime;
            this.SelectFilesAndShowFile();
        }

        private void MenuOptionsSkipDarkFileCheck_Click(object sender, RoutedEventArgs e)
        {
            this.state.SkipDarkImagesCheck = !this.state.SkipDarkImagesCheck;
            this.MenuOptionsSkipDarkFileCheck.IsChecked = this.state.SkipDarkImagesCheck;
        }

        private void MenuSelectCustom_Click(object sender, RoutedEventArgs e)
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
                this.SelectFilesAndShowFile(FileSelection.Custom);
            }
        }

        /// <summary>Show a dialog box telling the user how many images were loaded, etc.</summary>
        public void MenuSelectFileCounts_Click(object sender, RoutedEventArgs e)
        {
            this.MaybeShowFileCountsDialog(false);
        }

        /// <summary>Get the non-custom selection and update the view.</summary>
        private void MenuSelectFiles_Click(object sender, RoutedEventArgs e)
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
            this.SelectFilesAndShowFile(selection);
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
        private void MenuViewDifferencesCombined_Click(object sender, RoutedEventArgs e)
        {
            this.TryViewCombinedDifference();
        }

        /// <summary>Increase the magnification of the magnifying glass by several keyboard steps.</summary>
        private void MenuViewMagnifierIncrease_Click(object sender, RoutedEventArgs e)
        {
            this.MarkableCanvas.MagnifierZoomIn();
            this.MarkableCanvas.MagnifierZoomIn();
            this.MarkableCanvas.MagnifierZoomIn();
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
        private void MenuViewPreviousOrNextDifference_Click(object sender, RoutedEventArgs e)
        {
            this.TryViewPreviousOrNextDifference();
        }

        private void MenuViewSetBookmark_Click(object sender, RoutedEventArgs e)
        {
            this.MarkableCanvas.SetBookmark();
        }

        /// <summary>Navigate to the next file in this image set</summary>
        private void MenuViewShowNextFile_Click(object sender, RoutedEventArgs e)
        {
            this.ShowFileWithoutSliderCallback(true, ModifierKeys.None);
        }

        /// <summary>Navigate to the previous file in this image set</summary>
        private void MenuViewShowPreviousFile_Click(object sender, RoutedEventArgs e)
        {
            this.ShowFileWithoutSliderCallback(false, ModifierKeys.None);
        }

        private void MenuViewZoomIn_Click(object sender, RoutedEventArgs e)
        {
            Point mousePosition = Mouse.GetPosition(this.MarkableCanvas.ImageToDisplay);
            this.MarkableCanvas.ScaleImage(mousePosition, true);
        }

        private void MenuViewZoomOut_Click(object sender, RoutedEventArgs e)
        {
            Point mousePosition = Mouse.GetPosition(this.MarkableCanvas.ImageToDisplay);
            this.MarkableCanvas.ScaleImage(mousePosition, false);
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
        private void OnFolderLoadingComplete(bool filesJustAdded)
        {
            // Show the image, hide the load button, and make the feedback panels visible
            this.ImageSetPane.IsActive = true;
            this.FileNavigatorSlider_EnableOrDisableValueChangedCallback(false);
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
            this.SelectFilesAndShowFile(mostRecentFileID, fileSelection);

            // match UX availability to file availability
            this.EnableOrDisableMenusAndControls();
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
            this.PasteValuesToCurrentFile(previousFile);
        }

        private void PasteValuesToCurrentFile(Dictionary<string, string> propertyBag)
        {
            this.state.UndoBuffer = this.dataHandler.ImageCache.Current.AsDisplayDictionary();
            this.MenuEditUndo.IsEnabled = true;

            foreach (KeyValuePair<string, DataEntryControl> pair in this.DataEntryControls.ControlsByDataLabel)
            {
                DataEntryControl control = pair.Value;
                string value;
                if (this.dataHandler.FileDatabase.IsControlCopyable(control.DataLabel) && propertyBag.TryGetValue(control.DataLabel, out value))
                {
                    control.SetContentAndTooltip(value);
                }
            }
        }

        private void PasteValuesToCurrentFile(ImageRow sourceFile)
        {
            this.PasteValuesToCurrentFile(sourceFile.AsDisplayDictionary());
        }

        private void SelectFilesAndShowFile()
        {
            Debug.Assert(this.dataHandler != null && this.dataHandler.FileDatabase != null, "Expected a file database to be available.");
            this.SelectFilesAndShowFile(this.dataHandler.FileDatabase.ImageSet.FileSelection);
        }

        private void SelectFilesAndShowFile(FileSelection selection)
        {
            long fileID = Constant.Database.DefaultFileID;
            if (this.dataHandler != null && this.dataHandler.ImageCache != null && this.dataHandler.ImageCache.Current != null)
            {
                fileID = this.dataHandler.ImageCache.Current.ID;
            }
            this.SelectFilesAndShowFile(fileID, selection);
        }

        private void SelectFilesAndShowFile(long fileID, FileSelection selection)
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
            if (this.FileNavigatorSlider.Maximum <= 50)
            {
                this.FileNavigatorSlider.IsSnapToTickEnabled = true;
                this.FileNavigatorSlider.TickFrequency = 1.0;
            }
            else
            {
                this.FileNavigatorSlider.IsSnapToTickEnabled = false;
                this.FileNavigatorSlider.TickFrequency = 0.02 * this.FileNavigatorSlider.Maximum;
            }

            // Display the specified file or, if it's no longer selected, the next closest one
            // Showfile() handles empty image sets, so those don't need to be checked for here.
            this.ShowFile(this.dataHandler.FileDatabase.GetFileOrNextFileIndex(fileID));

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
        private void ShowBulkFileEditDialog(Window dialog)
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
                this.ShowFile(this.dataHandler.FileDatabase.GetFileOrNextFileIndex(currentFileID));
            }
        }

        private void ShowFile(int fileIndex)
        {
            // if there is no file to show, then show an image indicating no image set or an empty image set
            if ((this.dataHandler == null) || (this.dataHandler.FileDatabase == null) || (this.dataHandler.FileDatabase.CurrentlySelectedFileCount < 1))
            {
                this.MarkableCanvas.SetNewImage(Constant.Images.NoSelectableFile.Value, null);
                this.markersOnCurrentFile = null;
                this.MarkableCanvas_UpdateMarkers();
                return;
            }

            // for the bitmap caching logic below to work this should be the only place where code in CarnassialWindow moves the file enumerator
            bool newFileToDisplay;
            if (this.dataHandler.ImageCache.TryMoveToFile(fileIndex, out newFileToDisplay) == false)
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
                control.Value.SetContentAndTooltip(this.dataHandler.ImageCache.Current.GetValueDisplayString(control.Value.DataLabel));

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
            if (newFileToDisplay)
            {
                // show the file and enable or disable menu items whose availability depends on whether the file's an image or video
                this.markersOnCurrentFile = this.dataHandler.FileDatabase.GetMarkersOnFile(this.dataHandler.ImageCache.Current.ID);
                List<Marker> displayMarkers = this.GetDisplayMarkers(false);

                if (this.dataHandler.ImageCache.Current.IsVideo)
                {
                    this.MarkableCanvas.SetNewVideo(this.dataHandler.ImageCache.Current.GetFileInfo(this.dataHandler.FileDatabase.FolderPath), displayMarkers);

                    this.MenuOptionsDisplayMagnifier.IsEnabled = false;
                    this.MenuViewApplyBookmark.IsEnabled = false;
                    this.MenuViewDifferencesCombined.IsEnabled = false;
                    this.MenuViewMagnifierZoomIncrease.IsEnabled = false;
                    this.MenuViewMagnifierZoomDecrease.IsEnabled = false;
                    this.MenuViewNextOrPreviousDifference.IsEnabled = false;
                    this.MenuViewPlayVideo.IsEnabled = true;
                    this.MenuViewSetBookmark.IsEnabled = false;
                    this.MenuViewZoomIn.IsEnabled = false;
                    this.MenuViewZoomOut.IsEnabled = false;
                    this.MenuViewZoomToFit.IsEnabled = false;
                }
                else
                {
                    this.MarkableCanvas.SetNewImage(this.dataHandler.ImageCache.GetCurrentImage(), displayMarkers);

                    this.MenuOptionsDisplayMagnifier.IsEnabled = true;
                    this.MenuViewApplyBookmark.IsEnabled = true;
                    this.MenuViewDifferencesCombined.IsEnabled = true;
                    this.MenuViewMagnifierZoomIncrease.IsEnabled = true;
                    this.MenuViewMagnifierZoomDecrease.IsEnabled = true;
                    this.MenuViewNextOrPreviousDifference.IsEnabled = true;
                    this.MenuViewPlayVideo.IsEnabled = false;
                    this.MenuViewSetBookmark.IsEnabled = true;
                    this.MenuViewZoomIn.IsEnabled = true;
                    this.MenuViewZoomOut.IsEnabled = true;
                    this.MenuViewZoomToFit.IsEnabled = true;
                }

                // draw markers for this file
                this.MarkableCanvas_UpdateMarkers();

                // clear any undo buffer as it no longer applies and disable the undo menu item
                this.MenuEditUndo.IsEnabled = false;
                this.state.UndoBuffer = null;
            }

            // if the data grid has been bound, set the selected row to the current file and scroll so it's visible
            if (this.DataGrid.Items != null &&
                this.DataGrid.Items.Count > fileIndex &&
                this.DataGrid.SelectedIndex != fileIndex)
            {
                this.DataGrid.SelectAndScrollIntoView(fileIndex);
            }
        }

        private void ShowFile(Slider fileNavigatorSlider)
        {
            this.ShowFile((int)fileNavigatorSlider.Value - 1);
        }

        private void ShowFileWithoutSliderCallback(bool forward, ModifierKeys modifiers)
        {
            // determine how far to move and in which direction
            int increment = forward ? 1 : -1;
            if (modifiers.HasFlag(ModifierKeys.Shift))
            {
                increment *= 5;
            }
            if (modifiers.HasFlag(ModifierKeys.Control))
            {
                increment *= 10;
            }

            // clamp to the maximum or minimum row available
            int newFileRow = this.dataHandler.ImageCache.CurrentRow + increment;
            if (newFileRow >= this.dataHandler.FileDatabase.CurrentlySelectedFileCount)
            {
                newFileRow = this.dataHandler.FileDatabase.CurrentlySelectedFileCount - 1;
            }
            else if (newFileRow < 0)
            {
                newFileRow = 0;
            }

            // if no change the file is already being displayed
            // For example, the end of the image set has been reached but key repeat means right arrow events are still coming in as the user hasn't
            // reacted yet.
            if (newFileRow == this.dataHandler.ImageCache.CurrentRow)
            {
                return;
            }

            // show the new file
            this.FileNavigatorSlider_EnableOrDisableValueChangedCallback(false);
            this.ShowFile(newFileRow);
            this.FileNavigatorSlider_EnableOrDisableValueChangedCallback(true);
        }

        private bool ShowFolderSelectionDialog(out IEnumerable<string> folderPaths)
        {
            CommonOpenFileDialog folderSelectionDialog = new CommonOpenFileDialog();
            folderSelectionDialog.Title = "Select one or more folders...";
            folderSelectionDialog.DefaultDirectory = this.mostRecentFileAddFolderPath == null ? this.FolderPath : this.mostRecentFileAddFolderPath;
            folderSelectionDialog.InitialDirectory = folderSelectionDialog.DefaultDirectory;
            folderSelectionDialog.IsFolderPicker = true;
            folderSelectionDialog.Multiselect = true;
            folderSelectionDialog.FolderChanging += this.FolderSelectionDialog_FolderChanging;
            if (folderSelectionDialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                folderPaths = folderSelectionDialog.FileNames;

                // remember the parent of the selected folder path to save the user clicks and scrolling in case images from additional 
                // directories are added
                this.mostRecentFileAddFolderPath = Path.GetDirectoryName(folderPaths.First());
                return true;
            }

            folderPaths = null;
            return false;
        }

        private void FolderSelectionDialog_FolderChanging(object sender, CommonFileDialogFolderChangeEventArgs e)
        {
            // require folders to be loaded be either the same folder as the .tdb and .ddb or subfolders of it
            if (e.Folder.StartsWith(this.FolderPath, StringComparison.OrdinalIgnoreCase) == false)
            {
                e.Cancel = true;
            }
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

        private void ToggleCurrentFileDeleteFlag()
        {
            DataEntryFlag deleteFlag = (DataEntryFlag)this.DataEntryControls.ControlsByDataLabel[Constant.DatabaseColumn.DeleteFlag];
            deleteFlag.ContentControl.IsChecked = !deleteFlag.ContentControl.IsChecked;

            // if the current file was just marked for deletion presumably the user is done with it and ready to move to the next
            // This autoadvance saves the user having to keep backing out of data entry and hitting the next arrow, so offers substantial savings when
            // working through large numbers of wind triggers or such but may not be desirable in all cases.  If needed an option can be added to disable
            // the behavior.
            if (deleteFlag.ContentControl.IsChecked == true)
            {
                this.ShowFileWithoutSliderCallback(true, ModifierKeys.None);
            }
        }

        // out parameters can't be used in anonymous methods, so a separate pointer to backgroundWorker is required for return to the caller
        private bool TryBeginFolderLoadAsync(IEnumerable<string> folderPaths, out BackgroundWorker externallyVisibleWorker)
        {
            List<FileInfo> filesToAdd = new List<FileInfo>();
            foreach (string folderPath in folderPaths)
            {
                DirectoryInfo folder = new DirectoryInfo(folderPath);
                foreach (string extension in new List<string>() { Constant.File.AviFileExtension, Constant.File.Mp4FileExtension, Constant.File.JpgFileExtension })
                {
                    filesToAdd.AddRange(folder.GetFiles("*" + extension));
                }
            }
            filesToAdd = filesToAdd.OrderBy(file => file.FullName).ToList();

            if (filesToAdd.Count == 0)
            {
                externallyVisibleWorker = null;

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
                    return this.TryBeginFolderLoadAsync(folderPaths, out externallyVisibleWorker);
                }

                // exit if user changed their mind about trying again
                return false;
            }

            // Load all files found
            // Show previews to the user as loading proceeds.
            BackgroundWorker backgroundWorker = new BackgroundWorker()
            {
                WorkerReportsProgress = true
            };

            FolderLoadProgress folderLoadProgress = new FolderLoadProgress(filesToAdd.Count);
            int renderWidthBestEstimate = this.MarkableCanvas.Width > 0 ? (int)this.MarkableCanvas.Width : (int)this.Width;
            backgroundWorker.DoWork += (ow, ea) =>
            {
                // First pass: Examine files to extract their basic properties and build a list of files not already in the database
                //
                // With dark calculations enabled:
                // Profiling of a 1000 image load on quad core, single 80+MB/s capable SSD shows the following:
                // - one thread:   100% normalized execution time, 35% CPU, 16MB/s disk (100% normalized time = 1 minute 58 seconds)
                // - two threads:   55% normalized execution time, 50% CPU, 17MB/s disk (6.3% normalized time with dark checking skipped)
                // - three threads: 46% normalized execution time, 70% CPU, 20MB/s disk
                // This suggests memory bound operation due to image quality calculation.  The overhead of displaying preview images is fairly low; 
                // normalized time is about 5% with both dark checking and previewing skipped.
                //
                // For now, try to get at least two threads as that captures most of the benefit from parallel operation.  Video loading may be more CPU bound 
                // due to initial frame rendering and benefit from additional threads.  This requires further investigation.  It may also be desirable to reduce 
                // the pixel stride in image quality calculation, which would increase CPU load.
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
                DateTime previousImageRender = DateTime.UtcNow - this.state.Throttles.DesiredIntervalBetweenRenders;

                Parallel.ForEach(new SequentialPartitioner<FileInfo>(filesToAdd), Utilities.GetParallelOptions(this.state.SkipDarkImagesCheck ? 4 : 2), (FileInfo fileInfo) =>
                {
                    ImageRow file;
                    if (this.dataHandler.FileDatabase.GetOrCreateFile(fileInfo, imageSetTimeZone, out file))
                    {
                        // the database already has an entry for this file so skip it
                        // if needed, a separate list of files to update could be generated
                        return;
                    }

                    BitmapSource bitmapSource = null;
                    try
                    {
                        if (this.state.SkipDarkImagesCheck)
                        {
                            file.ImageQuality = FileSelection.Ok;
                        }
                        else
                        {
                            // Create the bitmap and determine its quality
                            // For good display quality the render size is ideally the markable canvas width.  However, its width isn't known until layout of the
                            // ImageSetPane completes, which occurs asynchronously on the UI thread from background worker thread execution.  Therefore, start with
                            // a naive guess of the width and refine it as layout information becomes available.  Profiling shows no difference in import speed
                            // for renders up to at least 1000 pixels wide or so, suggesting there's little reason to degrade the quality of the preview/progress 
                            // image the user sees.
                            bitmapSource = file.LoadBitmap(this.FolderPath, renderWidthBestEstimate);

                            // Set the ImageQuality to corrupt if the returned bitmap is the corrupt image, otherwise set it to its Ok/Dark setting
                            if (bitmapSource == Constant.Images.CorruptFile.Value)
                            {
                                file.ImageQuality = FileSelection.Corrupt;
                            }
                            else
                            {
                                file.ImageQuality = bitmapSource.AsWriteable().IsDark(this.state.DarkPixelThreshold, this.state.DarkPixelRatioThreshold);
                            }
                        }

                        // see if the datetime can be updated from the metadata
                        file.TryReadDateTimeOriginalFromMetadata(this.FolderPath, imageSetTimeZone);
                    }
                    catch (Exception exception)
                    {
                        Debug.Fail(String.Format("Load of {0} failed as it's likely corrupted.", file.FileName), exception.ToString());
                        bitmapSource = Constant.Images.CorruptFile.Value;
                        file.ImageQuality = FileSelection.Corrupt;
                    }

                    lock (filesToInsert)
                    {
                        filesToInsert.Add(file);
                    }

                    DateTime utcNow = DateTime.UtcNow;
                    if (utcNow - previousImageRender > this.state.Throttles.DesiredIntervalBetweenRenders)
                    {
                        lock (folderLoadProgress)
                        {
                            if (utcNow - previousImageRender > this.state.Throttles.DesiredIntervalBetweenRenders)
                            {
                                // if file was already loaded for dark checking use the resulting bitmap
                                // otherwise, load the file for display
                                if (bitmapSource != null)
                                {
                                    folderLoadProgress.BitmapSource = bitmapSource;
                                }
                                else
                                {
                                    folderLoadProgress.BitmapSource = file.LoadBitmap(this.FolderPath, renderWidthBestEstimate);
                                }
                                folderLoadProgress.CurrentFile = filesToInsert.Count;
                                folderLoadProgress.CurrentFileName = file.FileName;

                                int percentProgress = (int)(100.0 * filesToInsert.Count / (double)filesToAdd.Count);
                                backgroundWorker.ReportProgress(percentProgress, folderLoadProgress);
                                previousImageRender = utcNow;
                            }
                        }
                    }
                });

                // Second pass: Update database
                // Parallel execution above produces out of order results.  Put them back in order so the user sees images in file name order when
                // reviewing the image set.
                filesToInsert = filesToInsert.OrderBy(file => Path.Combine(file.RelativePath, file.FileName)).ToList();
                this.dataHandler.FileDatabase.AddFiles(filesToInsert, (ImageRow file, int fileIndex) =>
                {
                    // skip reloading images to display as the user's already seen them import
                    folderLoadProgress.BitmapSource = null;
                    folderLoadProgress.CurrentFile = fileIndex;
                    folderLoadProgress.CurrentFileName = file.FileName;
                    int percentProgress = (int)(100.0 * fileIndex / (double)filesToInsert.Count);
                    backgroundWorker.ReportProgress(percentProgress, folderLoadProgress);
                });
            };
            backgroundWorker.ProgressChanged += (o, ea) =>
            {
                // this gets called on the UI thread
                this.UpdateFolderLoadProgress(folderLoadProgress.BitmapSource, ea.ProgressPercentage, folderLoadProgress.GetMessage());
                if (this.MarkableCanvas.Width > 0)
                {
                    renderWidthBestEstimate = (int)this.MarkableCanvas.Width;
                }
            };
            backgroundWorker.RunWorkerCompleted += (o, ea) =>
            {
                // BackgroundWorker aborts execution on an exception and transfers it to completion for handling
                // If something went wrong rethrow the error so the user knows there's a problem.  Otherwise what would happen is either 
                //  1) some or all of the folder load file scan progress displays but no files get added to the database as the insert is skipped
                //  2) only some of the files get inserted and the rest are silently dropped
                // Both of these outcomes result in quite poor user experience and are best avoided.
                if (ea.Error != null)
                {
                    throw new FileLoadException("Folder loading failed unexpectedly.  See inner exception for details.", ea.Error);
                }

                // hide the feedback bar, show the file slider
                this.FeedbackControl.Visibility = Visibility.Collapsed;
                this.FileNavigatorSlider.Visibility = Visibility.Visible;

                this.OnFolderLoadingComplete(true);

                // Finally, tell the user how many images were loaded, etc.
                this.MaybeShowFileCountsDialog(true);
            };

            // update UI for import (visibility is inverse of RunWorkerCompleted)
            this.FeedbackControl.Visibility = Visibility.Visible;
            this.FileNavigatorSlider.Visibility = Visibility.Collapsed;
            this.UpdateFolderLoadProgress(null, 0, "Folder loading beginning...");
            this.statusBar.SetMessage("Loading folders...");
            this.ImageSetPane.IsActive = true;

            // start import and return
            backgroundWorker.RunWorkerAsync();
            externallyVisibleWorker = backgroundWorker;
            return true;
        }

        private bool TryCloseImageSet()
        {
            if ((this.dataHandler == null) ||
                (this.dataHandler.FileDatabase == null))
            {
                // no image set to close
                return false;
            }

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
            this.EnableOrDisableMenusAndControls();
            return true;
        }

        private bool TryCopyValuesToAnalysis(int analysisSlot)
        {
            if (this.IsFileAvailable() == false)
            {
                return false;
            }
            this.state.Analysis[analysisSlot] = this.dataHandler.ImageCache.Current;

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

        private bool TryGetTemplatePath(out string templateDatabasePath)
        {
            // prompt user to select a template
            // default the template selection dialog to the most recently opened database
            string defaultTemplateDatabasePath;
            this.state.MostRecentImageSets.TryGetMostRecent(out defaultTemplateDatabasePath);
            if (Utilities.TryGetFileFromUser("Select a template file, which should be located in the root folder containing your images and videos",
                                             defaultTemplateDatabasePath,
                                             String.Format("Template files (*{0})|*{0}", Constant.File.TemplateDatabaseFileExtension),
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
        public bool TryOpenTemplateAndBeginLoadFoldersAsync(string templateDatabasePath, out BackgroundWorker backgroundWorker)
        {
            backgroundWorker = null;

            // Try to create or open the template database
            TemplateDatabase templateDatabase;
            if (!TemplateDatabase.TryCreateOrOpen(templateDatabasePath, out templateDatabase))
            {
                // notify the user the template couldn't be loaded rather than silently doing nothing
                MessageBox messageBox = new MessageBox("Carnassial could not load the template.", this);
                messageBox.Message.Problem = "Carnassial could not load the Template File:" + Environment.NewLine;
                messageBox.Message.Problem += "\u2022 " + templateDatabasePath;
                messageBox.Message.Reason = "The template may be corrupted or somehow otherwise invalid. ";
                messageBox.Message.Solution = "You may have to recreate the template, or use another copy of it (if you have one).";
                messageBox.Message.Result = "Carnassial won't do anything. You can try to select another template file.";
                messageBox.Message.Hint = "See if you can examine the template file in the Carnassial Template Editor.";
                messageBox.Message.Hint += "If you can't, there is likley something wrong with it and you will have to recreate it.";
                messageBox.Message.StatusImage = MessageBoxImage.Error;
                messageBox.ShowDialog();
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
            FileDatabase fileDatabase = FileDatabase.CreateOrOpen(fileDatabaseFilePath, templateDatabase, this.state.OrderFilesByDateTime, this.state.CustomSelectionTermCombiningOperator);
            templateDatabase.Dispose();

            if (fileDatabase.ControlSynchronizationIssues.Count > 0)
            {
                TemplateSynchronization templatesNotCompatibleDialog = new TemplateSynchronization(fileDatabase.ControlSynchronizationIssues, this);
                bool? result = templatesNotCompatibleDialog.ShowDialog();
                if (result == true)
                {
                    // user indicated not to update to the current template so exit.
                    Application.Current.Shutdown();
                    return false;
                }
                // user indicated to run with the stale copy of the template found in the image database
            }

            // valid template and file database loaded
            // generate and render the data entry controls regardless of whether there are actually any files in the file database.
            this.dataHandler = new DataEntryHandler(fileDatabase);
            this.DataEntryControls.CreateControls(fileDatabase, this.dataHandler);
            this.SetUserInterfaceCallbacks();

            this.Title = Path.GetFileName(fileDatabase.FilePath) + " - " + Constant.MainWindowBaseTitle;
            this.state.MostRecentImageSets.SetMostRecent(templateDatabasePath);
            this.MenuFileRecentImageSets_Refresh();

            // If this is a new file database, try to load files (if any) from the folder...  
            if (addFiles)
            {
                this.TryBeginFolderLoadAsync(new List<string>() { this.FolderPath }, out backgroundWorker);
            }

            this.OnFolderLoadingComplete(false);
            return true;
        }

        private bool TryPasteValuesFromAnalysis(int analysisSlot)
        {
            ImageRow sourceFile = this.state.Analysis[analysisSlot];
            if (sourceFile == null)
            {
                // nothing to copy
                return false;
            }

            this.PasteValuesToCurrentFile(sourceFile);
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

        private void TryViewCombinedDifference()
        {
            if (this.dataHandler == null ||
                this.dataHandler.ImageCache == null ||
                this.dataHandler.ImageCache.Current == null ||
                this.dataHandler.ImageCache.Current.IsVideo)
            {
                return;
            }

            this.dataHandler.ImageCache.MoveToNextStateInCombinedDifferenceCycle();
            if (this.dataHandler.ImageCache.CurrentDifferenceState == ImageDifference.Unaltered)
            {
                // unaltered image should be cached
                BitmapSource unalteredImage = this.dataHandler.ImageCache.GetCurrentImage();
                Debug.Assert(unalteredImage != null, "Unaltered image not available from image cache.");
                this.MarkableCanvas.SetDisplayImage(unalteredImage);
                this.statusBar.ClearMessage();
                return;
            }

            // generate and cache difference image if needed
            if (this.dataHandler.ImageCache.GetCurrentImage() == null)
            {
                ImageDifferenceResult result = this.dataHandler.ImageCache.TryCalculateCombinedDifference(this.state.DifferenceThreshold);
                switch (result)
                {
                    case ImageDifferenceResult.CurrentImageNotAvailable:
                        this.statusBar.SetMessage("Combined differences can't be shown since the current file is not a loadable image (typically it's a video, missing, or corrupt).");
                        return;
                    case ImageDifferenceResult.NextImageNotAvailable:
                        this.statusBar.SetMessage("Combined differences can't be shown since the next file is not a loadable image (typically it's a video, missing, or corrupt).");
                        return;
                    case ImageDifferenceResult.NotCalculable:
                        this.statusBar.SetMessage(String.Format("Previous or next file is not compatible with {0}, most likely because it's a different size.", this.dataHandler.ImageCache.Current.FileName));
                        return;
                    case ImageDifferenceResult.PreviousImageNotAvailable:
                        this.statusBar.SetMessage("Combined differences can't be shown since the previous file is not a loadable image (typically it's a video, missing, or corrupt).");
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
        private void TryViewPreviousOrNextDifference()
        {
            if (this.dataHandler == null || 
                this.dataHandler.ImageCache == null || 
                this.dataHandler.ImageCache.Current == null ||
                this.dataHandler.ImageCache.Current.IsVideo)
            {
                return;
            }

            this.dataHandler.ImageCache.MoveToNextStateInPreviousNextDifferenceCycle();
            if (this.dataHandler.ImageCache.CurrentDifferenceState == ImageDifference.Unaltered)
            {
                // unaltered image should be cached
                BitmapSource unalteredImage = this.dataHandler.ImageCache.GetCurrentImage();
                Debug.Assert(unalteredImage != null, "Unaltered image not available from image cache.");
                this.MarkableCanvas.SetDisplayImage(unalteredImage);
                this.statusBar.ClearMessage();
                return;
            }

            // generate and cache difference image if needed
            if (this.dataHandler.ImageCache.GetCurrentImage() == null)
            {
                ImageDifferenceResult result = this.dataHandler.ImageCache.TryCalculateDifference();
                switch (result)
                {
                    case ImageDifferenceResult.CurrentImageNotAvailable:
                        this.statusBar.SetMessage("Difference can't be shown as the current file is not a displayable image (typically it's a video, missing, or corrupt).");
                        return;
                    case ImageDifferenceResult.NextImageNotAvailable:
                    case ImageDifferenceResult.PreviousImageNotAvailable:
                        this.statusBar.SetMessage(String.Format("View of difference from {0} file unavailable as it is not a displayable image (typically it's a video, missing, or corrupt).", this.dataHandler.ImageCache.CurrentDifferenceState == ImageDifference.Previous ? "previous" : "next"));
                        return;
                    case ImageDifferenceResult.NotCalculable:
                        this.statusBar.SetMessage(String.Format("{0} file is not compatible with {1}, most likely because it's a different size.", this.dataHandler.ImageCache.CurrentDifferenceState == ImageDifference.Previous ? "Previous" : "Next", this.dataHandler.ImageCache.Current.FileName));
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
            this.statusBar.SetMessage(String.Format("Viewing difference from {0} file.", this.dataHandler.ImageCache.CurrentDifferenceState == ImageDifference.Previous ? "previous" : "next"));
        }

        private void UpdateFolderLoadProgress(BitmapSource bitmap, int percent, string message)
        {
            if (bitmap != null)
            {
                this.MarkableCanvas.SetNewImage(bitmap, null);
            }
            this.FeedbackControl.Message.Content = message;
            this.FeedbackControl.ProgressBar.Value = percent;
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            this.TryCloseImageSet();

            // persist user specific state to the registry
            if (this.Top > -10 && this.Left > -10)
            {
                this.state.CarnassialWindowPosition = new Rect(new Point(this.Left, this.Top), new Size(this.Width, this.Height));
            }
            this.state.WriteToRegistry();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // abort if required dependencies are missing
            if (Dependencies.AreRequiredBinariesPresent(Constant.ApplicationName, Assembly.GetExecutingAssembly()) == false)
            {
                Dependencies.ShowMissingBinariesDialog(Constant.ApplicationName);
                Application.Current.Shutdown();
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
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs currentKey)
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
            int keyRepeatCount = this.state.GetKeyRepeatCount(currentKey);
            switch (currentKey.Key)
            {
                // copy whatever data fields are copyable from the previous file
                case Key.C:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.MenuEditCopy_Click(null, null);
                    }
                    break;
                // toggle the file's delete flag and, if set, move to the next file
                case Key.Delete:
                    this.ToggleCurrentFileDeleteFlag();
                    break;
                // Alt+Key.D1 and D2 are handled by routine keyboard shortcuts for the analysis 1 and 2 buttons
                case Key.D1:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.TryCopyValuesToAnalysis(0);
                    }
                    break;
                case Key.D2:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.TryCopyValuesToAnalysis(1);
                    }
                    break;
                case Key.D3:
                    // see Key.System case for ModifierKeys.Alt
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.TryCopyValuesToAnalysis(2);
                    }
                    break;
                case Key.D4:
                    // see Key.System case for ModifierKeys.Alt
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.TryCopyValuesToAnalysis(3);
                    }
                    break;
                case Key.D5:
                    // see Key.System case for ModifierKeys.Alt
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.TryCopyValuesToAnalysis(4);
                    }
                    break;
                case Key.D6:
                    // see Key.System case for ModifierKeys.Alt
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.TryCopyValuesToAnalysis(5);
                    }
                    break;
                case Key.Escape:            // exit current control, if any
                    this.TrySetKeyboardFocusToMarkableCanvas(false, currentKey);
                    break;
                case Key.M:                 // toggle the magnifying glass on and off
                    this.MenuViewDisplayMagnifier_Click(this, null);
                    break;
                case Key.V:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.MenuEditPaste_Click(null, null);
                    }
                    break;
                case Key.Z:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.MenuEditUndo_Click(null, null);
                    }
                    break;
                case Key.Left:              // previous image
                    if (keyRepeatCount % this.state.Throttles.RepeatedKeyAcceptanceInterval == 0)
                    {
                        this.ShowFileWithoutSliderCallback(false, Keyboard.Modifiers);
                    }
                    break;
                case Key.Right:             // next image
                    if (keyRepeatCount % this.state.Throttles.RepeatedKeyAcceptanceInterval == 0)
                    {
                        this.ShowFileWithoutSliderCallback(true, Keyboard.Modifiers);
                    }
                    break;
                case Key.System:            // most commonly reached when Keyboard.Modifiers includes ModifierKeys.Alt
                    if (Keyboard.Modifiers == ModifierKeys.Alt)
                    {
                        switch (currentKey.SystemKey)
                        {
                            case Key.D3:
                                this.TryPasteValuesFromAnalysis(2);
                                break;
                            case Key.D4:
                                this.TryPasteValuesFromAnalysis(3);
                                break;
                            case Key.D5:
                                this.TryPasteValuesFromAnalysis(4);
                                break;
                            case Key.D6:
                                this.TryPasteValuesFromAnalysis(5);
                                break;
                            default:
                                return;
                        }
                    }
                    break;
                case Key.Tab:               // next or previous control
                    this.MoveFocusToNextOrPreviousControlOrImageSlider(Keyboard.Modifiers == ModifierKeys.Shift);
                    break;
                case Key.Up:                // show visual difference to next image
                    this.TryViewPreviousOrNextDifference();
                    break;
                case Key.Down:              // show visual difference to previous image
                    this.TryViewCombinedDifference();
                    break;
                default:
                    return;
            }
            currentKey.Handled = true;
        }
    }
}
