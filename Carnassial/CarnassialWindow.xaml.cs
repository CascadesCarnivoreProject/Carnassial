using Carnassial.Controls;
using Carnassial.Database;
using Carnassial.Dialog;
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
using MessageBox = Carnassial.Dialog.MessageBox;

namespace Carnassial
{
    /// <summary>
    /// main window for Carnassial
    /// </summary>
    public partial class CarnassialWindow : Window, IDisposable
    {
        private DataEntryHandler dataHandler;
        private bool disposed;
        private List<MarkersForCounter> markersOnCurrentImage;
        private string mostRecentImageAddFolderPath;

        // Speech feedback
        private SpeechSynthesizer speechSynthesizer;

        // Status information concerning the state of the UI
        private CarnassialState state;

        // Timers for periodically updating the data grid and current file during ImageNavigator slider drag
        private DispatcherTimer timerDataGrid;
        private DispatcherTimer timerImageNavigator;

        // Non-modal dialogs
        private WindowVideoPlayer videoPlayer;

        public CarnassialWindow()
        {
            this.InitializeComponent();

            this.MarkableCanvas.MouseEnter += new MouseEventHandler(this.MarkableCanvas_MouseEnter);
            this.MarkableCanvas.PreviewMouseDown += new MouseButtonEventHandler(this.MarkableCanvas_PreviewMouseDown);
            this.MarkableCanvas.RaiseMarkerEvent += new EventHandler<MarkerEventArgs>(this.MarkableCanvas_RaiseMarkerEvent);

            this.speechSynthesizer = new SpeechSynthesizer();
            this.state = new CarnassialState();

            // Recall user's state from prior sessions
            this.state.ReadFromRegistry();

            this.MenuItemAudioFeedback.IsChecked = this.state.AudioFeedback;
            this.MenuItemEnableCsvExportDialog.IsChecked = !this.state.SuppressCsvExportDialog;
            this.MenuItemEnableCsvImportPrompt.IsChecked = !this.state.SuppressCsvImportPrompt;
            this.MenuItemEnableSelectedAmbiguousDatesPrompt.IsChecked = !this.state.SuppressSelectedAmbiguousDatesPrompt;
            this.MenuItemEnableSelectedCsvExportPrompt.IsChecked = !this.state.SuppressSelectedCsvExportPrompt;
            this.MenuItemEnableSelectedDarkThresholdPrompt.IsChecked = !this.state.SuppressSelectedDarkThresholdPrompt;
            this.MenuItemEnableSelectedDateTimeFixedCorrectionPrompt.IsChecked = !this.state.SuppressSelectedDateTimeFixedCorrectionPrompt;
            this.MenuItemEnableSelectedDateTimeLinearCorrectionPrompt.IsChecked = !this.state.SuppressSelectedDateTimeLinearCorrectionPrompt;
            this.MenuItemEnableSelectedDaylightSavingsCorrectionPrompt.IsChecked = !this.state.SuppressSelectedDaylightSavingsCorrectionPrompt;
            this.MenuItemEnableSelectedPopulateFieldFromMetadataPrompt.IsChecked = !this.state.SuppressSelectedPopulateFieldFromMetadataPrompt;
            this.MenuItemEnableSelectedRereadDatesFromFilesPrompt.IsChecked = !this.state.SuppressSelectedRereadDatesFromFilesPrompt;

            // Timer callback to keep the data grid in sync with the current image index
            this.timerDataGrid = new DispatcherTimer();
            this.timerDataGrid.Interval = TimeSpan.FromSeconds(0.5);
            this.timerDataGrid.Tick += this.DataGridTimer_Tick;

            // Timer callback so the image will update to the current slider position when the user pauses whilst dragging the slider 
            this.timerImageNavigator = new DispatcherTimer();
            this.timerImageNavigator.Interval = this.state.Throttles.DesiredIntervalBetweenRenders;
            this.timerImageNavigator.Tick += this.TimerImageNavigator_Tick;

            // populate the most recent image set list
            this.MenuItemRecentImageSets_Refresh();

            this.Top = this.state.CarnassialWindowLocation.Y;
            this.Left = this.state.CarnassialWindowLocation.X;
            this.Height = this.state.CarnassialWindowSize.Height;
            this.Width = this.state.CarnassialWindowSize.Width;
            Utilities.TryFitWindowInWorkingArea(this);
        }

        private string FolderPath
        {
            get { return this.dataHandler.ImageDatabase.FolderPath; }
        }

        private void DataGrid_IsActiveChanged(object sender, EventArgs e)
        {
            if (this.dataHandler == null || this.dataHandler.ImageDatabase == null)
            {
                return;
            }

            if (this.DataGridPane.IsActive)
            {
                this.dataHandler.ImageDatabase.BindToDataGrid(this.DataGrid, null);
                this.timerDataGrid.Start();
            }
            else
            {
                this.dataHandler.ImageDatabase.BindToDataGrid(null, null);
                this.timerDataGrid.Stop();
            }
        }

        /// <summary>Ensure that the the highlighted row is the current row </summary>
        private void DataGridTimer_Tick(object sender, EventArgs e)
        {
            // Set the selected index to the current row that represents the image being viewed
            int lastIndex = this.DataGrid.SelectedIndex;
            this.DataGrid.SelectedIndex = this.dataHandler.ImageCache.CurrentRow;

            // A workaround to autoscroll the currently selected items, where the item always appears at the top of the window.
            // We check the last index and only autoscroll if it hasn't changed since then.
            // This workaround means that the user can manually scroll to a new spot, where it won't jump back unless the image number has changed.
            if (lastIndex != this.DataGrid.SelectedIndex)
            {
                this.DataGrid.ScrollIntoView(this.DataGrid.Items[this.DataGrid.Items.Count - 1]);
                this.DataGrid.UpdateLayout();
                // Try to autoscroll so at least 5 rows are visible (if possible) before the selected row
                int rowToShow = (this.DataGrid.SelectedIndex > 5) ? this.DataGrid.SelectedIndex - 5 : 0;
                this.DataGrid.ScrollIntoView(this.DataGrid.Items[rowToShow]);
                lastIndex = this.DataGrid.SelectedIndex;
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

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // abort if required dependencies are missing
            if (Dependencies.AreRequiredBinariesPresent(Constants.ApplicationName, Assembly.GetExecutingAssembly()) == false)
            {
                Dependencies.ShowMissingBinariesDialog(Constants.ApplicationName);
                Application.Current.Shutdown();
            }

            // check for updates
            Uri latestVersionAddress = CarnassialConfigurationSettings.GetLatestVersionAddress();
            if (latestVersionAddress == null)
            {
                return;
            }

            VersionClient updater = new VersionClient(Constants.ApplicationName, latestVersionAddress);
            updater.TryGetAndParseVersion(false);
        }

        // On exiting, save various attributes so we can use recover them later
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if ((this.dataHandler != null) &&
                (this.dataHandler.ImageDatabase != null) &&
                (this.dataHandler.ImageDatabase.CurrentlySelectedImageCount > 0))
            {
                // save image set properties to the database
                if (this.dataHandler.ImageDatabase.ImageSet.ImageSelection == ImageSelection.Custom)
                {
                    // don't save custom selections, revert to All 
                    this.dataHandler.ImageDatabase.ImageSet.ImageSelection = ImageSelection.All;
                }

                if (this.dataHandler.ImageCache != null)
                {
                    this.dataHandler.ImageDatabase.ImageSet.ImageRowIndex = this.dataHandler.ImageCache.CurrentRow;
                }

                if (this.MarkableCanvas != null)
                {
                    this.dataHandler.ImageDatabase.ImageSet.MagnifierEnabled = this.MarkableCanvas.IsMagnifyingGlassVisible;
                }

                this.dataHandler.ImageDatabase.SyncImageSetToDatabase();

                // ensure custom filter operator is synchronized in state for writing to user's registry
                this.state.CustomSelectionTermCombiningOperator = this.dataHandler.ImageDatabase.CustomSelection.TermCombiningOperator;
            }

            // persist user specific state to the registry
            if (this.Top > -10 && this.Left > -10)
            {
                this.state.CarnassialWindowLocation = new Point(this.Left, this.Top);
            }
            this.state.CarnassialWindowSize = new Size(this.Width, this.Height);
            this.state.WriteToRegistry();

            // Close the various non-modal windows if they are opened
            if (this.videoPlayer != null)
            {
                this.videoPlayer.Close();
            }
        }

        private bool TryGetTemplatePath(out string templateDatabasePath)
        {
            // prompt user to select a template
            // default the template selection dialog to the most recently opened database
            string defaultTemplateDatabasePath;
            this.state.MostRecentImageSets.TryGetMostRecent(out defaultTemplateDatabasePath);
            if (Utilities.TryGetFileFromUser("Select a CarnassialTemplate.tdb file, which should be located in the root folder containing your images and videos",
                                             defaultTemplateDatabasePath,
                                             String.Format("Template files (*{0})|*{0}", Constants.File.TemplateDatabaseFileExtension),
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
        /// Load the specified database template and then the associated images.
        /// </summary>
        /// <param name="templateDatabasePath">Fully qualified path to the template database file.</param>
        /// <returns>true only if both the template and image database file are loaded (regardless of whether any images were loaded) , false otherwise</returns>
        /// <remarks>This method doesn't particularly need to be public. But making it private imposes substantial complexity in invoking it via PrivateObject
        /// in unit tests.</remarks>
        public bool TryOpenTemplateAndBeginLoadImagesAsync(string templateDatabasePath, out BackgroundWorker backgroundWorker)
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
                messageBox.Message.Icon = MessageBoxImage.Error;
                messageBox.ShowDialog();
                return false;
            }

            // Try to get the image database file path
            // importImages will be true if it's a new image database file (meaning the user will be prompted import some images)
            string imageDatabaseFilePath;
            bool importImages;
            if (this.TrySelectDatabaseFile(templateDatabasePath, out imageDatabaseFilePath, out importImages) == false)
            {
                // No image database file was selected
                templateDatabase.Dispose();
                return false;
            }

            // Before running from an existing image database, check the template table in the template database was compatible with the template table
            // of the image database.
            ImageDatabase imageDatabase = ImageDatabase.CreateOrOpen(imageDatabaseFilePath, templateDatabase, this.state.CustomSelectionTermCombiningOperator);
            templateDatabase.Dispose();

            if (imageDatabase.TemplateSynchronizationIssues.Count > 0)
            {
                TemplateSynchronization templatesNotCompatibleDialog = new TemplateSynchronization(imageDatabase.TemplateSynchronizationIssues, this);
                bool? result = templatesNotCompatibleDialog.ShowDialog();
                if (result == true)
                {
                    // user indicated not to update to the current template so exit.
                    Application.Current.Shutdown();
                    return false;
                }
                // user indicated to run with the stale copy of the template found in the image database
            }

            // At this point, we should have a valid template and image database loaded
            // Generate and render the data entry controls, regardless of whether there are actually any images in the image database.
            this.dataHandler = new DataEntryHandler(imageDatabase);
            this.DataEntryControls.CreateControls(imageDatabase, this.dataHandler);
            this.SetUserInterfaceCallbacks();

            this.state.MostRecentImageSets.SetMostRecent(templateDatabasePath);
            this.MenuItemRecentImageSets_Refresh();

            // If this is a new image database, try to load images (if any) from the folder...  
            if (importImages)
            {
                this.TryBeginImageFolderLoadAsync(new List<string>() { this.FolderPath }, out backgroundWorker);
            }
            else
            { 
                this.OnImageLoadingComplete(false);
            }

            return true;
        }

        // out parameters can't be used in anonymous methods, so a separate pointer to backgroundWorker is required for return to the caller
        private bool TryBeginImageFolderLoadAsync(IEnumerable<string> imageFolderPaths, out BackgroundWorker externallyVisibleWorker)
        {
            List<FileInfo> imageFiles = new List<FileInfo>();
            foreach (string imageFolderPath in imageFolderPaths)
            {
                DirectoryInfo imageFolder = new DirectoryInfo(imageFolderPath);
                foreach (string extension in new List<string>() { Constants.File.AviFileExtension, Constants.File.Mp4FileExtension, Constants.File.JpgFileExtension })
                {
                    imageFiles.AddRange(imageFolder.GetFiles("*" + extension));
                }
            }
            imageFiles = imageFiles.OrderBy(file => file.FullName).ToList();

            if (imageFiles.Count == 0)
            {
                externallyVisibleWorker = null;

                // no images were found in folder; see if user wants to try again
                MessageBox messageBox = new MessageBox("Select a folder containing images or videos", this, MessageBoxButton.YesNo);
                messageBox.Message.Problem = "Select a folder containing images or videos, as there aren't any images or videos in the folder:" + Environment.NewLine;
                messageBox.Message.Problem += "\u2022 " + this.FolderPath + Environment.NewLine;
                messageBox.Message.Reason = "\u2022 This folder has no JPG files in it (files ending in '.jpg'), and" + Environment.NewLine;
                messageBox.Message.Reason += "\u2022 This folder has no AVI files in it (files ending in '.avi'), and" + Environment.NewLine;
                messageBox.Message.Reason += "\u2022 This folder has no MP4 files in it (files ending in '.mp4'), or" + Environment.NewLine;
                messageBox.Message.Reason += "\u2022 The images / videos may be located in a subfolder to this one.";
                messageBox.Message.Solution = "Select a folder containing images (files with a '.jpg' suffix) and/or" + Environment.NewLine;
                messageBox.Message.Solution += "videos ('.avi' or '.mp4' files)." + Environment.NewLine;
                messageBox.Message.Icon = MessageBoxImage.Question;
                if (messageBox.ShowDialog() == false)
                {
                    return false;
                }

                IEnumerable<string> folderPaths;
                if (this.ShowFolderSelectionDialog(out folderPaths))
                {
                    return this.TryBeginImageFolderLoadAsync(folderPaths, out externallyVisibleWorker);
                }

                // exit if user changed their mind about trying again
                return false;
            }

            // Load all the jpg images found in the folder
            // We want to show previews of the frames to the user as they are individually loaded
            // Because WPF uses a scene graph, we have to do this by a background worker, as this forces the update
            BackgroundWorker backgroundWorker = new BackgroundWorker()
            {
                WorkerReportsProgress = true
            };

            ProgressState progressState = new ProgressState();
            bool unambiguousDayMonthOrder = true;
            int renderWidthBestEstimate = Constants.Images.DefaultPreviewWidth;
            backgroundWorker.DoWork += (ow, ea) =>
            {
                // First pass: Examine images to extract their basic properties and build a list of images not already in the database
                // Profiling of a 1000 image load on quad core, single 80+MB/s capable SSD shows the following:
                // - one thread:   100% normalized execution time, 35% CPU, 16MB/s disk (100% normalized time = 1 minute 58 seconds)
                // - two threads:   55% normalized execution time, 50% CPU, 17MB/s disk (6.3% normalized time with dark checking skipped)
                // - three threads: 46% normalized execution time, 70% CPU, 20MB/s disk
                // This suggests memory bound operation due to image quality calculation.  The overhead of displaying preview images is fairly low; 
                // normalized time is about 5% with both dark checking and previewing skipped.
                //
                // For now, use only two threads as that captures most of the benefit from parallel operation.  Video loading may be more CPU bound due to 
                // initial frame rendering and benefit from additional threads.  This requires further investigation.  It may also be desirable to reduce 
                // the pixel stride in image quality calculation, which would increase CPU load.
                //
                // A sequential partitioner is used as this keeps the preview images displayed to the user in pretty much the same order as they're named,
                // which is less confusing than TPL's default partitioning where the displayed image jumps back and forth through the image set.  Pulling files
                // nearly sequentially may also offer some minor disk performance benefit.
                List<ImageRow> imagesToInsert = new List<ImageRow>();
                TimeZoneInfo imageSetTimeZone = this.dataHandler.ImageDatabase.ImageSet.GetTimeZone();
                DateTime previousImageRender = DateTime.UtcNow - this.state.Throttles.DesiredIntervalBetweenRenders;

                ParallelOptions parallelOptions = new ParallelOptions();
                parallelOptions.MaxDegreeOfParallelism = Environment.ProcessorCount > 1 ? 2 : 1;
                Parallel.ForEach(new SequentialPartitioner<FileInfo>(imageFiles), parallelOptions, (FileInfo imageFile) =>
                {
                    ImageRow image;
                    if (this.dataHandler.ImageDatabase.GetOrCreateImage(imageFile, imageSetTimeZone, out image))
                    {
                        // the database already has an entry for this image so skip it
                        // if needed, a separate list of images to update could be generated
                        return;
                    }

                    BitmapSource bitmapSource = null;
                    try
                    {
                        // Create the bitmap and determine its ImageQuality
                        // For good display quality the render size is ideally the markable canvas width.  However, its width isn't known until layout of the
                        // ImageSetPane completes, which occurs asynchronously on the UI thread from background worker thread execution.  Therefore, start with
                        // a naive guess of the width and refine it as layout information becomes available.  Profiling shows no difference in import speed
                        // for renders up to at least 1000 pixels wide or so, suggesting there's little reason to degrade the quality of preview/progress image
                        // the user sees.
                        bitmapSource = image.LoadBitmap(this.FolderPath, renderWidthBestEstimate);

                        // Set the ImageQuality to corrupt if the returned bitmap is the corrupt image, otherwise set it to its Ok/Dark setting
                        if (bitmapSource == Constants.Images.CorruptFile)
                        {
                            image.ImageQuality = ImageSelection.CorruptFile;
                        }
                        else
                        {
                            image.ImageQuality = bitmapSource.AsWriteable().GetImageQuality(this.state.DarkPixelThreshold, this.state.DarkPixelRatioThreshold);
                        }

                        // see if the datetime can be updated from the metadata
                        DateTimeAdjustment imageTimeAdjustment = image.TryReadDateTimeOriginalFromMetadata(this.FolderPath, imageSetTimeZone);
                        if (imageTimeAdjustment == DateTimeAdjustment.MetadataDateAndTimeUsed ||
                            imageTimeAdjustment == DateTimeAdjustment.MetadataDateUsed)
                        {
                            DateTimeOffset imageTaken = image.GetDateTime();
                            if (imageTaken.Day <= Constants.Time.MonthsInYear)
                            {
                                unambiguousDayMonthOrder = false;
                            }
                        }
                    }
                    catch (Exception exception)
                    {
                        Debug.Fail(String.Format("Load of {0} failed as it's likely corrupted.", image.FileName), exception.ToString());
                        bitmapSource = Constants.Images.CorruptFile;
                        image.ImageQuality = ImageSelection.CorruptFile;
                    }

                    int imagesPendingInsert;
                    lock (imagesToInsert)
                    {
                        imagesToInsert.Add(image);
                        imagesPendingInsert = imagesToInsert.Count;
                    }

                    DateTime utcNow = DateTime.UtcNow;
                    if (utcNow - previousImageRender > this.state.Throttles.DesiredIntervalBetweenRenders)
                    {
                        lock (progressState)
                        {
                            if (utcNow - previousImageRender > this.state.Throttles.DesiredIntervalBetweenRenders)
                            {
                                progressState.Bmap = bitmapSource;
                                progressState.Message = String.Format("{0}/{1}: Examining {2}", imagesPendingInsert, imageFiles.Count, image.FileName);
                                int percentProgress = (int)(100.0 * imagesToInsert.Count / (double)imageFiles.Count);
                                backgroundWorker.ReportProgress(percentProgress, progressState);
                                previousImageRender = utcNow;
                            }
                        }
                    }
                });

                // Second pass: Update database
                // Parallel execution above produces out of order results.  Put them back in order so the user sees images in file name order when
                // reviewing the image set.
                imagesToInsert = imagesToInsert.OrderBy(image => Path.Combine(image.RelativePath, image.FileName)).ToList();
                this.dataHandler.ImageDatabase.AddImages(imagesToInsert, (ImageRow imageProperties, int imageIndex) =>
                {
                    // skip reloading images to display as the user's already seen them import
                    progressState.Bmap = null;
                    progressState.Message = String.Format("{0}/{1}: Adding {2}", imageIndex, imageFiles.Count, imageProperties.FileName);
                    int percentProgress = (int)(100.0 * imageIndex / (double)imagesToInsert.Count);
                    backgroundWorker.ReportProgress(percentProgress, progressState);
                });
            };
            backgroundWorker.ProgressChanged += (o, ea) =>
            {
                // this gets called on the UI thread
                this.Feedback(progressState.Bmap, ea.ProgressPercentage, progressState.Message);
                renderWidthBestEstimate = this.MarkableCanvas.Width > 0 ? (int)this.MarkableCanvas.Width : Constants.Images.DefaultPreviewWidth;
            };
            backgroundWorker.RunWorkerCompleted += (o, ea) =>
            {
                // this.dbData.GetImagesAll(); // Now load up the data table
                // Get rid of the feedback panel, and show the main interface
                this.FeedbackControl.Visibility = Visibility.Collapsed;
                this.FeedbackControl.ShowImage = null;

                this.ImageNavigatorSlider.Visibility = Visibility.Visible;
                this.MarkableCanvas.Visibility = Visibility.Visible;

                // warn the user if there are any ambiguous dates in terms of day/month or month/day order
                if (unambiguousDayMonthOrder == false && this.state.SuppressAmbiguousDatesDialog == false)
                {
                    MessageBox messageBox = new MessageBox("Carnassial was unsure about the month / day order of your file(s) dates.", this);
                    messageBox.Message.Problem = "Carnassial is extracting the dates from your files. However, File date formats can be ambiguous: is 2016/03/05 March 5 or May 3?";
                    messageBox.Message.Problem += "Carnassial tries its best by using the date format specified in the Windows Control Panel";
                    messageBox.Message.Solution = "If the month/day order is wrong, you can correct the dates by choosing" + Environment.NewLine;
                    messageBox.Message.Solution += "\u2022 Edit -> Date correction -> Correct ambiguous dates.";
                    messageBox.Message.Solution += "Alternately, go to your Windows Control Panel" + Environment.NewLine;
                    messageBox.Message.Solution += "\u2022 go to your Windows Control Panel" + Environment.NewLine;
                    messageBox.Message.Solution += "\u2022 change the short date format in the Windows Control Panel to match the correct date shown in the image e.g., to yyyy-MM-dd.";
                    messageBox.Message.Solution += "\u2022 then select 'Edit/Date correction/Reread dates from files...' and see if it fixed the problem.";
                    messageBox.Message.Hint = "If you are unsure about the correct date, try the following." + Environment.NewLine;
                    messageBox.Message.Hint += "\u2022 If your camera prints the date on the image or video, check that." + Environment.NewLine;
                    messageBox.Message.Hint += "\u2022 Look at the image / video to see what season it is (e.g., winter vs. summer)." + Environment.NewLine;
                    messageBox.Message.Hint += "\u2022 Check your own records." + Environment.NewLine;
                    messageBox.Message.Hint += "If you check don't show this message again this dialog can be turned back on via the Options menu.";
                    messageBox.Message.Icon = MessageBoxImage.Information;
                    messageBox.DontShowAgain.Visibility = Visibility.Visible;
                    Nullable<bool> result = messageBox.ShowDialog();
                    if (result.HasValue && result.Value && messageBox.DontShowAgain.IsChecked.HasValue)
                    {
                        this.state.SuppressAmbiguousDatesDialog = messageBox.DontShowAgain.IsChecked.Value;
                        this.MenuItemEnableAmbiguousDatesDialog.IsChecked = !this.state.SuppressAmbiguousDatesDialog;
                    }
                }
                this.OnImageLoadingComplete(true);

                // Finally, tell the user how many images were loaded, etc.
                this.ShowImageCountsDialog(true);
            };

            // update UI for import
            this.FeedbackControl.Visibility = Visibility.Visible;
            this.ImageNavigatorSlider.Visibility = Visibility.Collapsed;
            this.MarkableCanvas.Visibility = Visibility.Collapsed;
            this.ImageSetPane.IsActive = true;
            this.Feedback(null, 0, "Examining images...");

            // start import and return
            backgroundWorker.RunWorkerAsync();
            externallyVisibleWorker = backgroundWorker;
            return true;
        }

        // Given the location path of the template,  return:
        // - true if a database file was specified
        // - databaseFilePath: the path to the data database file (or null if none was specified).
        // - importImages: true when the database file has just been created, which means images still have to be imported.
        private bool TrySelectDatabaseFile(string templateDatabasePath, out string databaseFilePath, out bool importImages)
        {
            importImages = false;

            string databaseFileName;
            string directoryPath = Path.GetDirectoryName(templateDatabasePath);
            string[] files = Directory.GetFiles(directoryPath, "*.ddb");
            if (files.Length == 1)
            {
                databaseFileName = Path.GetFileName(files[0]); // Get the file name, excluding the path
            }
            else if (files.Length > 1)
            {
                ChooseDatabaseFile chooseDatabaseFile = new ChooseDatabaseFile(files, this);
                chooseDatabaseFile.Owner = this;
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
                if (String.Equals(templateDatabaseFileName, Constants.File.DefaultTemplateDatabaseFileName, StringComparison.OrdinalIgnoreCase))
                {
                    databaseFileName = Constants.File.DefaultImageDatabaseFileName;
                }
                else
                {
                    databaseFileName = Path.GetFileNameWithoutExtension(templateDatabasePath) + Constants.File.ImageDatabaseFileExtension;
                }
                importImages = true;
            }

            databaseFilePath = Path.Combine(directoryPath, databaseFileName);
            return true;
        }

        private void Feedback(BitmapSource bmap, int percent, string message)
        {
            this.FeedbackControl.ShowMessage = message;
            this.FeedbackControl.ShowProgress = percent;
            if (bmap != null)
            {
                this.FeedbackControl.ShowImage = bmap;
            }
        }

        /// <summary>
        /// When image loading has completed add callbacks, prepare the UI, set up the image set, and show the image.
        /// </summary>
        private void OnImageLoadingComplete(bool imagesJustImported)
        {
            // Set the magnifying glass status from the registry. 
            // Note that if it wasn't in the registry, the value returned will be true by default
            this.MarkableCanvas.IsMagnifyingGlassVisible = this.dataHandler.ImageDatabase.ImageSet.MagnifierEnabled;

            // Show the image, hide the load button, and make the feedback panels visible
            this.ImageSetPane.IsActive = true;
            this.ImageNavigatorSlider_EnableOrDisableValueChangedCallback(false);
            this.MarkableCanvas.Focus(); // We start with this having the focus so it can interpret keyboard shortcuts if needed. 

            // if this is completion of an existing .ddb open set the current selection and the image index to the ones from the previous session with the image set
            // also if this is completion of import to a new .ddb
            int imageRow = this.dataHandler.ImageDatabase.ImageSet.ImageRowIndex;
            ImageSelection imageSelection = this.dataHandler.ImageDatabase.ImageSet.ImageSelection;
            if (imagesJustImported && this.dataHandler.ImageCache.CurrentRow != Constants.Database.InvalidRow)
            {
                // if this is completion of an add to an existing image set stay on the image, ideally, shown before the import
                // TODO: add an API to read the current selection
                imageRow = this.dataHandler.ImageCache.CurrentRow;
            }
            this.SelectDataTableImagesAndShowImage(imageRow, imageSelection);

            // match UX availability to image availability
            this.EnableOrDisableMenusAndControls();
        }

        private void EnableOrDisableMenusAndControls()
        {
            bool filesSelected = this.dataHandler.ImageDatabase.CurrentlySelectedImageCount > 0;

            // Depending upon whether images exist in the data set,
            // enable / disable menus and menu items as needed
            // file menu
            this.MenuItemAddImagesToImageSet.IsEnabled = true;
            this.MenuItemLoadImages.IsEnabled = false;
            this.MenuItemRecentImageSets.IsEnabled = false;
            this.MenuItemExportThisImage.IsEnabled = filesSelected;
            this.MenuItemExportAsCsvAndPreview.IsEnabled = filesSelected;
            this.MenuItemExportAsCsv.IsEnabled = filesSelected;
            this.MenuItemImportFromCsv.IsEnabled = filesSelected;
            this.MenuItemRenameImageDatabaseFile.IsEnabled = filesSelected;
            // edit menu
            this.MenuItemEdit.IsEnabled = filesSelected;
            this.MenuItemDeleteCurrentFile.IsEnabled = filesSelected;
            // view menu
            this.MenuItemView.IsEnabled = filesSelected;
            // select menu
            this.MenuItemSelect.IsEnabled = filesSelected;
            // options menu
            // always enable at top level when an image set exists so that image set advanced options are accessible
            this.MenuItemOptions.IsEnabled = true;
            this.MenuItemAudioFeedback.IsEnabled = filesSelected;
            this.MenuItemMagnifyingGlass.IsEnabled = filesSelected;
            this.MenuItemDisplayMagnifier.IsChecked = this.dataHandler.ImageDatabase.ImageSet.MagnifierEnabled;
            this.MenuItemImageCounts.IsEnabled = filesSelected;
            this.MenuItemDialogsOnOrOff.IsEnabled = filesSelected;
            this.MenuItemAdvancedCarnassialOptions.IsEnabled = filesSelected;
            
            // other UI components
            this.ControlsPanel.IsEnabled = filesSelected;  // If images don't exist, the user shouldn't be allowed to interact with the control tray
            this.CopyPreviousValues.IsEnabled = filesSelected;
            this.ImageNavigatorSlider.IsEnabled = filesSelected;
            this.MarkableCanvas.IsEnabled = filesSelected;
            this.MarkableCanvas.IsMagnifyingGlassVisible = filesSelected && this.dataHandler.ImageDatabase.ImageSet.MagnifierEnabled;

            if (filesSelected == false)
            {
                this.ShowImage(Constants.DefaultImageRowIndex);
                this.statusBar.SetMessage("Image set is empty.");
                this.statusBar.SetCurrentImage(0);
                this.statusBar.SetCount(0);
            }
        }

        private void SelectDataTableImagesAndShowImage(int imageRow, ImageSelection selection)
        {
            this.dataHandler.ImageDatabase.SelectDataTableImages(selection);
            if (this.dataHandler.ImageDatabase.CurrentlySelectedImageCount > 0 || selection == ImageSelection.All)
            {
                // update status and menu state to reflect what the user selected
                string status;
                switch (selection)
                {
                    case ImageSelection.All:
                        status = "(all files selected)";
                        break;
                    case ImageSelection.CorruptFile:
                        status = "corrupted files";
                        break;
                    case ImageSelection.Custom:
                        status = "files matching your custom selection";
                        break;
                    case ImageSelection.Dark:
                        status = "dark files";
                        break;
                    case ImageSelection.MarkedForDeletion:
                        status = "files marked for deletion";
                        break;
                    case ImageSelection.FileNoLongerAvailable:
                        status = "files no longer available";
                        break;
                    case ImageSelection.Ok:
                        status = "light files";
                        break;
                    default:
                        throw new NotSupportedException(String.Format("Unhandled image selection {0}.", selection));
                }

                this.statusBar.SetView(status);

                this.MenuItemSelectAllImages.IsChecked = (selection == ImageSelection.All) ? true : false;
                this.MenuItemSelectCorruptedImages.IsChecked = (selection == ImageSelection.CorruptFile) ? true : false;
                this.MenuItemSelectDarkImages.IsChecked = (selection == ImageSelection.Dark) ? true : false;
                this.MenuItemSelectLightImages.IsChecked = (selection == ImageSelection.Ok) ? true : false;
                this.MenuItemSelectFilesNoLongerAvailable.IsChecked = (selection == ImageSelection.FileNoLongerAvailable) ? true : false;
                this.MenuItemSelectImagesMarkedForDeletion.IsChecked = (selection == ImageSelection.MarkedForDeletion) ? true : false;
                this.MenuItemSelectCustom.IsChecked = (selection == ImageSelection.Custom) ? true : false;

                // if it's displayed, update the window which shows the data table
                this.RefreshDataGrid();
            }
            else
            {
                // These cases are typically reached only when a user deletes all images which mach a selection.  
                string status;
                string title;
                string problem;
                string reason = null;
                string hint;
                status = "Resetting selection to All Files";
                title = "Resetting selection to All Files (no files currently match the current selection)";
                if (selection == ImageSelection.CorruptFile)
                {
                    problem = "Corrupted files were previously selected but no files are currently corrupted, so nothing can be shown.";
                    reason = "None of the files have their 'ImageQuality' field set to Corrupted.";
                    hint = "If you have files you think should be marked as 'Corrupted', set their 'ImageQuality' field to 'Corrupted' and then reselect corrupted files.";
                }
                else if (selection == ImageSelection.Custom)
                {
                    problem = "No files currently match the custom selection so nothing can be shown.";
                    reason = "None of the files match the criteria set in the current Custom Selection.";
                    hint = "Create a different custom selection and apply it view the matching files.";
                }
                else if (selection == ImageSelection.Dark)
                {
                    problem = "Dark files were previously selected but no files are currently dark so nothing can be shown.";
                    reason = "None of the files have their 'ImageQuality' field set to Dark.";
                    hint = "If you have files you think should be marked as 'Dark', set their 'ImageQuality' field to 'Dark' and then reselect dark files.";
                }
                else if (selection == ImageSelection.FileNoLongerAvailable)
                {
                    problem = "Files no londer available were previously selected but all files are availale so nothing can be shown.";
                    reason = "None of the files have their 'ImageQuality' field set to FilesNoLongerAvailable.";
                    hint = "If you have removed files set their 'ImageQuality' field to 'FilesNoLongerAvailable' and then reselect files no longer available.";
                }
                else if (selection == ImageSelection.MarkedForDeletion)
                {
                    problem = "Files marked for deletion were previously selected but no files are currently marked so nothing can be shown.";
                    reason = "None of the files have their 'Delete?' field checked.";
                    hint = "If you have files you think should be marked for deletion, check their 'Delete?' field and then reselect files marked for deletion.";
                }
                else if (selection == ImageSelection.Ok)
                {
                    problem = "Ok files were previously selected but no files are currently OK so nothing can be shown.";
                    reason = "None of the files have their 'ImageQuality' field set to Ol.";
                    hint = "If you have files you think should be marked as 'Ok', set their 'ImageQuality' field to 'Ok' and then reselect Ok files.";
                }
                else
                {
                    throw new NotSupportedException(String.Format("Unhandled selection {0}.", selection));
                }

                this.statusBar.SetMessage(status);

                MessageBox messageBox = new MessageBox(title, this);
                messageBox.Message.Icon = MessageBoxImage.Information;
                messageBox.Message.Problem = problem;
                if (reason != null)
                {
                    messageBox.Message.Reason = reason;
                }
                messageBox.Message.Hint = hint;
                messageBox.Message.Result = "The 'All Images' selection will be applied, where all images in your image set will be displayed.";
                messageBox.ShowDialog();

                this.SelectDataTableImagesAndShowImage(Constants.DefaultImageRowIndex, ImageSelection.All);
                return;
            }

            // Display the specified image under the new selection
            if (this.dataHandler.ImageDatabase.CurrentlySelectedImageCount > 0)
            {
                this.ShowImage(imageRow);
            }

            // After a selection change, set the slider to represent the index and the count of the selection
            this.ImageNavigatorSlider_EnableOrDisableValueChangedCallback(false);
            this.ImageNavigatorSlider.Maximum = this.dataHandler.ImageDatabase.CurrentlySelectedImageCount - 1;  // Reset the slider to the size of images in this set
            this.ImageNavigatorSlider.Value = this.dataHandler.ImageCache.CurrentRow;

            // Update the status bar accordingly
            this.statusBar.SetCurrentImage(this.dataHandler.ImageCache.CurrentRow + 1); // We add 1 because its a 0-based list
            this.statusBar.SetCount(this.dataHandler.ImageDatabase.CurrentlySelectedImageCount);
            this.ImageNavigatorSlider_EnableOrDisableValueChangedCallback(true);
            this.dataHandler.ImageDatabase.ImageSet.ImageSelection = selection; // persist the current selection
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
                string controlType = this.dataHandler.ImageDatabase.ImageDataColumnsByDataLabel[pair.Key].ControlType;
                switch (controlType)
                {
                    case Constants.Control.Counter:
                        DataEntryCounter counter = (DataEntryCounter)pair.Value;
                        counter.ContentControl.PreviewKeyDown += this.ContentCtl_PreviewKeyDown;
                        counter.ContentControl.PreviewTextInput += this.CounterCtl_PreviewTextInput;
                        counter.Container.MouseEnter += this.CounterControl_MouseEnter;
                        counter.Container.MouseLeave += this.CounterControl_MouseLeave;
                        counter.LabelControl.Click += this.CounterControl_Click;
                        break;
                    case Constants.Control.Flag:
                    case Constants.DatabaseColumn.DeleteFlag:
                        DataEntryFlag flag = (DataEntryFlag)pair.Value;
                        flag.ContentControl.PreviewKeyDown += this.ContentCtl_PreviewKeyDown;
                        break;
                    case Constants.Control.FixedChoice:
                    case Constants.DatabaseColumn.ImageQuality:
                        DataEntryChoice choice = (DataEntryChoice)pair.Value;
                        choice.ContentControl.PreviewKeyDown += this.ContentCtl_PreviewKeyDown;
                        break;
                    case Constants.Control.Note:
                    case Constants.DatabaseColumn.File:
                    case Constants.DatabaseColumn.Folder:
                    case Constants.DatabaseColumn.RelativePath:
                        DataEntryNote note = (DataEntryNote)pair.Value;
                        note.ContentControl.PreviewKeyDown += this.ContentCtl_PreviewKeyDown;
                        break;
                    case Constants.DatabaseColumn.DateTime:
                        DataEntryDateTime dateTime = (DataEntryDateTime)pair.Value;
                        dateTime.ContentControl.PreviewKeyDown += this.ContentCtl_PreviewKeyDown;
                        break;
                    case Constants.DatabaseColumn.UtcOffset:
                        DataEntryUtcOffset utcOffset = (DataEntryUtcOffset)pair.Value;
                        utcOffset.ContentControl.PreviewKeyDown += this.ContentCtl_PreviewKeyDown;
                        break;
                    default:
                        Debug.Fail(String.Format("Unhandled control type '{0}'.", controlType));
                        break;
                }
            }
        }

        /// <summary>
        /// This preview callback is used by all controls to reset the focus.
        /// Whenever the user hits enter over the control, set the focus back to the top-level
        /// </summary>
        /// <param name="sender">source of the event</param>
        /// <param name="eventArgs">event information</param>
        private void ContentCtl_PreviewKeyDown(object sender, KeyEventArgs eventArgs)
        {
            if (eventArgs.Key == Key.Enter)
            {
                this.SetTopLevelFocus(false, eventArgs);
                eventArgs.Handled = true;
            }
            // The 'empty else' means don't check to see if a textbox or control has the focus, as we want to reset the focus elsewhere
        }

        /// <summary>Preview callback for counters, to ensure ensure that we only accept numbers</summary>
        /// <param name="sender">the event source</param>
        /// <param name="e">event information</param>
        private void CounterCtl_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = (Utilities.IsDigits(e.Text) || String.IsNullOrWhiteSpace(e.Text)) ? false : true;
            this.OnPreviewTextInput(e);
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

        private void MoveFocusToNextOrPreviousControlOrImageSlider(bool moveToPreviousControl)
        {
            // identify the currently selected control
            // if focus is currently set to the canvas this defaults to the first or last control, as appropriate
            int currentControl = moveToPreviousControl ? this.DataEntryControls.Controls.Count : -1;

            IInputElement focusedElement = FocusManager.GetFocusedElement(this);
            if (focusedElement != null)
            {
                Type type = focusedElement.GetType();
                if (Constants.Control.KeyboardInputTypes.Contains(type))
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
            this.ImageNavigatorSlider.Focus();
        }

        /// <summary>
        /// When the mouse enters / leaves the copy button, the controls that are copyable will be highlighted. 
        /// </summary>
        private void CopyPreviousValues_MouseEnter(object sender, MouseEventArgs e)
        {
            this.CopyPreviousValues.Background = Constants.Control.CopyableFieldHighlightBrush;

            foreach (KeyValuePair<string, DataEntryControl> pair in this.DataEntryControls.ControlsByDataLabel)
            {
                DataEntryControl control = (DataEntryControl)pair.Value;
                if (control.Copyable)
                {
                    control.Container.Background = Constants.Control.CopyableFieldHighlightBrush;
                }
            }
        }

        /// <summary>
        ///  When the mouse enters / leaves the copy button, the controls that are copyable will be highlighted. 
        /// </summary>
        private void CopyPreviousValues_MouseLeave(object sender, MouseEventArgs e)
        {
            this.CopyPreviousValues.ClearValue(Control.BackgroundProperty);
            foreach (KeyValuePair<string, DataEntryControl> pair in this.DataEntryControls.ControlsByDataLabel)
            {
                DataEntryControl control = (DataEntryControl)pair.Value;
                control.Container.ClearValue(Control.BackgroundProperty);
            }
        }

        // Cycle through the image enhancements in the order current, then previous and next differenced images.
        // Create the differenced image if needed
        // For display efficiency, cache the differenced image.
        private void ViewPreviousOrNextDifference()
        {
            // Note:  No matter what image we are viewing, the source image will have already been cached before entering this function
            // Go to the next image in the cycle we want to show.
            this.dataHandler.ImageCache.MoveToNextStateInPreviousNextDifferenceCycle();

            // If we are supposed to display the unaltered image, do it and get out of here.
            // The unaltered image will always be cached at this point, so there is no need to check.
            if (this.dataHandler.ImageCache.CurrentDifferenceState == ImageDifference.Unaltered)
            {
                this.MarkableCanvas.ImageToMagnify.Source = this.dataHandler.ImageCache.GetCurrentImage();
                this.MarkableCanvas.ImageToDisplay.Source = this.MarkableCanvas.ImageToMagnify.Source;

                // Check if its a corrupted image
                if (!this.dataHandler.ImageCache.Current.IsDisplayable())
                {
                    // TO DO AS WE MAY HAVE TO GET THE INDEX OF THE NEXT IN CYCLE IMAGE???
                    this.statusBar.SetMessage(String.Format("Image is {0}.", this.dataHandler.ImageCache.Current.ImageQuality));
                }
                else
                {
                    this.statusBar.ClearMessage();
                }
                return;
            }

            // If we don't have the cached difference image, generate and cache it.
            if (this.dataHandler.ImageCache.GetCurrentImage() == null)
            {
                ImageDifferenceResult result = this.dataHandler.ImageCache.TryCalculateDifference();
                switch (result)
                {
                    case ImageDifferenceResult.CurrentImageNotAvailable:
                        this.statusBar.SetMessage("Differences can't be shown unless the current file be loaded");
                        return;
                    case ImageDifferenceResult.NextImageNotAvailable:
                    case ImageDifferenceResult.PreviousImageNotAvailable:
                        this.statusBar.SetMessage(String.Format("View of differences compared to {0} file not available", this.dataHandler.ImageCache.CurrentDifferenceState == ImageDifference.Previous ? "previous" : "next"));
                        return;
                    case ImageDifferenceResult.NotCalculable:
                        this.statusBar.SetMessage(String.Format("{0} file is not compatible with {1}", this.dataHandler.ImageCache.CurrentDifferenceState == ImageDifference.Previous ? "Previous" : "Next", this.dataHandler.ImageCache.Current.FileName));
                        return;
                    case ImageDifferenceResult.Success:
                        break;
                    default:
                        throw new NotSupportedException(String.Format("Unhandled difference result {0}.", result));
                }
            }

            // display the differenced image
            // the magnifying glass always displays the original non-diferenced image so ImageToDisplay is updated and ImageToMagnify left unchnaged
            // this allows the user to examine any particular differenced area and see what it really looks like in the non-differenced image. 
            this.MarkableCanvas.ImageToDisplay.Source = this.dataHandler.ImageCache.GetCurrentImage();
            this.statusBar.SetMessage("Viewing differences compared to " + (this.dataHandler.ImageCache.CurrentDifferenceState == ImageDifference.Previous ? "previous" : "next") + " file");
        }

        private void ViewCombinedDifference()
        {
            // If we are in any state other than the unaltered state, go to the unaltered state, otherwise the combined diff state
            this.dataHandler.ImageCache.MoveToNextStateInCombinedDifferenceCycle();
            if (this.dataHandler.ImageCache.CurrentDifferenceState != ImageDifference.Combined)
            {
                this.MarkableCanvas.ImageToDisplay.Source = this.dataHandler.ImageCache.GetCurrentImage();
                this.MarkableCanvas.ImageToMagnify.Source = this.MarkableCanvas.ImageToDisplay.Source;
                this.statusBar.ClearMessage();
                return;
            }

            // Generate the differenced image if it's not cached
            if (this.dataHandler.ImageCache.GetCurrentImage() == null)
            {
                ImageDifferenceResult result = this.dataHandler.ImageCache.TryCalculateCombinedDifference(this.state.DifferenceThreshold);
                switch (result)
                {
                    case ImageDifferenceResult.CurrentImageNotAvailable:
                        this.statusBar.SetMessage("Combined differences can't be shown unless the current file be loaded");
                        return;
                    case ImageDifferenceResult.NextImageNotAvailable:
                        this.statusBar.SetMessage("Combined differences can't be shown unless the next file can be loaded");
                        return;
                    case ImageDifferenceResult.NotCalculable:
                        this.statusBar.SetMessage(String.Format("Previous or next file is not compatible with {0}", this.dataHandler.ImageCache.Current.FileName));
                        return;
                    case ImageDifferenceResult.PreviousImageNotAvailable:
                        this.statusBar.SetMessage("Combined differences can't be shown unless the previous file can be loaded");
                        return;
                    case ImageDifferenceResult.Success:
                        break;
                    default:
                        throw new NotSupportedException(String.Format("Unhandled combined difference result {0}.", result));
                }
            }

            // display differenced image
            // see above remarks about not modifying ImageToMagnify
            this.MarkableCanvas.ImageToDisplay.Source = this.dataHandler.ImageCache.GetCurrentImage();
            this.statusBar.SetMessage("Viewing differences compared to both the next and previous files");
        }

        private void ImageNavigatorSlider_DragCompleted(object sender, DragCompletedEventArgs args)
        {
            this.state.ImageNavigatorSliderDragging = false;
            this.ShowImage((int)this.ImageNavigatorSlider.Value);
            this.timerImageNavigator.Stop(); 
        }

        private void ImageNavigatorSlider_DragStarted(object sender, DragStartedEventArgs args)
        {
            this.timerImageNavigator.Start(); // The timer forces an image display update to the current slider position if the user pauses longer than the timer's interval. 
            this.state.ImageNavigatorSliderDragging = true;
        }

        private void ImageNavigatorSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> args)
        {
            this.timerImageNavigator.Stop(); // Restart the timer 
            this.timerImageNavigator.Interval = this.state.Throttles.DesiredIntervalBetweenRenders; // Throttle values may have changed, so we reset it just in case.
            this.timerImageNavigator.Start();
            DateTime utcNow = DateTime.UtcNow;
            if ((this.state.ImageNavigatorSliderDragging == false) || (utcNow - this.state.MostRecentDragEvent > this.timerImageNavigator.Interval))
            {
                this.ShowImage((int)this.ImageNavigatorSlider.Value);
                this.state.MostRecentDragEvent = utcNow;
            }
        }

        private void ImageNavigatorSlider_EnableOrDisableValueChangedCallback(bool enableCallback)
        {
            if (enableCallback)
            {
                this.ImageNavigatorSlider.ValueChanged += new RoutedPropertyChangedEventHandler<double>(this.ImageNavigatorSlider_ValueChanged);
            }
            else
            {
                this.ImageNavigatorSlider.ValueChanged -= new RoutedPropertyChangedEventHandler<double>(this.ImageNavigatorSlider_ValueChanged);
            }
        }

        // Timer callback that forces image update to the current slider position. Invoked as the user pauses dragging the image slider 
        private void TimerImageNavigator_Tick(object sender, EventArgs e)
        {
            this.ShowImage((int)this.ImageNavigatorSlider.Value);
            this.timerImageNavigator.Stop(); 
        }

        // Various dialogs perform a bulk edit, after which various states have to be refreshed
        // This method shows the dialog and (if a bulk edit is done) refreshes those states.
        private void ShowBulkImageEditDialog(Window dialog)
        {
            dialog.Owner = this;
            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                this.dataHandler.ImageDatabase.SelectDataTableImages(this.dataHandler.ImageDatabase.ImageSet.ImageSelection);
                this.RefreshCurrentImageProperties();
                this.RefreshDataGrid();
            }
        }

        private void ShowImageCountsDialog(bool onImageLoading)
        {
            if (onImageLoading && this.state.SuppressFileCountOnImportDialog)
            {
                return;
            }

            Dictionary<ImageSelection, int> counts = this.dataHandler.ImageDatabase.GetImageCountsByQuality();
            FileCountsByQuality imageStats = new FileCountsByQuality(counts, this);
            if (onImageLoading)
            {
                imageStats.Message.Hint = "\u2022 " + imageStats.Message.Hint + Environment.NewLine + "\u2022 If you check don't show this message again this dialog can be turned back on via the Options menu.";
                imageStats.DontShowAgain.Visibility = Visibility.Visible;
            }
            Nullable<bool> result = imageStats.ShowDialog();
            if (onImageLoading && result.HasValue && result.Value && imageStats.DontShowAgain.IsChecked.HasValue)
            {
                this.state.SuppressFileCountOnImportDialog = imageStats.DontShowAgain.IsChecked.Value;
                this.MenuItemEnableFileCountOnImportDialog.IsChecked = !this.state.SuppressFileCountOnImportDialog;
            }
        }

        private void ShowFirstDisplayableImage(int firstRowInSearch)
        {
            int firstImageDisplayable = this.dataHandler.ImageDatabase.FindFirstDisplayableImage(firstRowInSearch);
            if (firstImageDisplayable != -1)
            {
                this.ShowImage(firstImageDisplayable);
            }
        }

        /// <summary>
        /// Reloads the current image's properties and redisplays them.  Doesn't invalidate the image's cached bitmap.
        /// </summary>
        private void RefreshCurrentImageProperties()
        {
            // caller is indicating image data has been updated, so move the image enumerator off current to force a refresh of the cached properties
            int currentRow = this.dataHandler.ImageCache.CurrentRow;
            this.dataHandler.ImageCache.Reset();
            // load the new property values
            this.ShowImage(currentRow);
        }

        // Show the image in the specified row
        private void ShowImage(int imageRow)
        {
            // If there is no image to show, then show an image indicating the empty image set.
            if (this.dataHandler.ImageDatabase.CurrentlySelectedImageCount < 1)
            {
                BitmapSource unalteredImage = Constants.Images.NoSelectableFile;
                this.MarkableCanvas.ImageToDisplay.Source = unalteredImage;
                this.MarkableCanvas.ImageToMagnify.Source = unalteredImage; // Probably not needed
                this.markersOnCurrentImage = null;
                this.MarkableCanvas_UpdateMarkers();

                // We could invalidate the cache here, but it will be reset anyways when images are loaded. 
                return;
            }

            // for the bitmap caching logic below to work this should be the only place where code in CarnassialWindow moves the image enumerator
            bool newImageToDisplay;
            if (this.dataHandler.ImageCache.TryMoveToImage(imageRow, out newImageToDisplay) == false)
            {
                throw new ArgumentOutOfRangeException("newImageRow", String.Format("{0} is not a valid row index in the image table.", imageRow));
            }

            // update each control with the data for the now current image
            // This is always done as it's assumed either the image changed or that a control refresh is required due to database changes
            // the call to TryMoveToImage() above refreshes the data stored under this.dataHandler.ImageCache.Current.
            this.dataHandler.IsProgrammaticControlUpdate = true;
            foreach (KeyValuePair<string, DataEntryControl> control in this.DataEntryControls.ControlsByDataLabel)
            {
                // update value
                string controlType = this.dataHandler.ImageDatabase.ImageDataColumnsByDataLabel[control.Key].ControlType;
                control.Value.SetContentAndTooltip(this.dataHandler.ImageCache.Current.GetValueDisplayString(control.Value.DataLabel));

                // for note controls, update the autocomplete list if an edit occurred
                if (controlType == Constants.Control.Note)
                {
                    DataEntryNote noteControl = (DataEntryNote)control.Value;
                    if (noteControl.ContentChanged)
                    {
                        noteControl.Autocompletions = this.dataHandler.ImageDatabase.GetDistinctValuesInImageColumn(control.Value.DataLabel);
                        noteControl.ContentChanged = false;
                    }
                }
            }
            this.dataHandler.IsProgrammaticControlUpdate = false;

            // update the status bar to show which image we are on out of the total displayed under the current selection
            // the total is always refreshed as it's not known if ShowImage() is being called due to a seletion change
            this.statusBar.SetCurrentImage(this.dataHandler.ImageCache.CurrentRow + 1); // Add one because indexes are 0-based
            this.statusBar.SetCount(this.dataHandler.ImageDatabase.CurrentlySelectedImageCount);
            this.statusBar.ClearMessage();

            this.ImageNavigatorSlider.Value = this.dataHandler.ImageCache.CurrentRow;

            // get and display the new image if the image changed
            // this avoids unnecessary image reloads and refreshes in cases where ShowImage() is just being called to refresh controls
            // the image row can't be tested against as its meaning changes when the selection is changed; use the image ID as that's both
            // unique and immutable
            if (newImageToDisplay)
            {
                BitmapSource unalteredImage = this.dataHandler.ImageCache.GetCurrentImage();
                this.MarkableCanvas.ImageToDisplay.Source = unalteredImage;

                // Set the image to magnify so the unaltered image will appear on the magnifying glass
                this.MarkableCanvas.ImageToMagnify.Source = unalteredImage;

                // Whenever we navigate to a new image, delete any markers that were displayed on the current image 
                // and then draw the markers assoicated with the new image
                this.markersOnCurrentImage = this.dataHandler.ImageDatabase.GetMarkersOnImage(this.dataHandler.ImageCache.Current.ID);
                this.MarkableCanvas_UpdateMarkers();
            }
            this.SetVideoPlayerToCurrentRow(); 
        }

        // If its an arrow key and the textbox doesn't have the focus,
        // navigate left/right image or up/down to look at differenced image
        private void Window_PreviewKeyDown(object sender, KeyEventArgs currentKey)
        {
            if (this.dataHandler == null ||
                this.dataHandler.ImageDatabase == null ||
                this.dataHandler.ImageDatabase.CurrentlySelectedImageCount == 0)
            {
                return; // No images are loaded, so don't try to interpret any keys
            }

            // Don't interpret keyboard shortcuts if the focus is on a control in the control grid, as the text entered may be directed
            // to the controls within it. That is, if a textbox or combo box has the focus, then take no as this is normal text input
            // and NOT a shortcut key.  Similarly, if a menu is displayed keys should be directed to the menu rather than interpreted as
            // shortcuts.
            if (this.SendKeyToDataEntryControlOrMenu(currentKey))
            {
                return;
            }
            // An alternate way of doing this, but not as good -> 
            // if ( this.ControlGrid.IsMouseOver) return;
            // if (!this.markableCanvas.IsMouseOver) return; // if its outside the window, return as well.

            // Interpret key as a possible shortcut key. 
            // Depending on the key, take the appropriate action
            int keyRepeatCount = this.state.GetKeyRepeatCount(currentKey);
            switch (currentKey.Key)
            {
                case Key.B:                 // Bookmark (Save) the current pan / zoom level of the image
                    this.MarkableCanvas.BookmarkSaveZoomPan();
                    break;
                case Key.Escape:
                    this.SetTopLevelFocus(false, currentKey);
                    break;
                case Key.OemPlus:           // Restore the zoom level / pan coordinates of the bookmark
                    this.MarkableCanvas.BookmarkSetZoomPan();
                    break;
                case Key.OemMinus:          // Restore the zoom level / pan coordinates of the bookmark
                    this.MarkableCanvas.BookmarkZoomOutAllTheWay();
                    break;
                case Key.M:                 // Toggle the magnifying glass on and off
                    this.MenuItemDisplayMagnifier_Click(this, null);
                    break;
                case Key.U:                 // Increase the magnifing glass zoom level
                    this.MarkableCanvas.MagnifierZoomIn();
                    break;
                case Key.D:                 // Decrease the magnifing glass zoom level
                    this.MarkableCanvas.MagnifierZoomOut();
                    break;
                case Key.Right:             // next image
                    if (keyRepeatCount % this.state.Throttles.RepeatedKeyAcceptanceInterval == 0)
                    {
                        this.TryShowImageWithoutSliderCallback(true, Keyboard.Modifiers);
                    }
                    break;
                case Key.Left:              // previous image
                    if (keyRepeatCount % this.state.Throttles.RepeatedKeyAcceptanceInterval == 0)
                    {
                        this.TryShowImageWithoutSliderCallback(false, Keyboard.Modifiers);
                    }
                    break;
                case Key.Up:                // show visual difference to next image
                    this.ViewPreviousOrNextDifference();
                    break;
                case Key.Down:              // show visual difference to previous image
                    this.ViewCombinedDifference();
                    break;
                case Key.C:
                    this.CopyPreviousValues_Click(null, null);
                    break;
                case Key.Tab:
                    this.MoveFocusToNextOrPreviousControlOrImageSlider(Keyboard.Modifiers == ModifierKeys.Shift);
                    break;
                default:
                    return;
            }
            currentKey.Handled = true;
        }

        // Because of shortcut keys, we want to reset the focus when appropriate to the 
        // image control. This is done from various places.

        // Whenever the user clicks on the image, reset the image focus to the image control 
        private void MarkableCanvas_PreviewMouseDown(object sender, MouseButtonEventArgs eventArgs)
        {
            this.SetTopLevelFocus(true, eventArgs);
        }

        // When we move over the canvas and the user isn't in the midst of typing into a text field, reset the top level focus
        private void MarkableCanvas_MouseEnter(object sender, MouseEventArgs eventArgs)
        {
            IInputElement focusedElement = FocusManager.GetFocusedElement(this);
            if ((focusedElement == null) || (focusedElement is TextBox == false))
            {
                this.SetTopLevelFocus(true, eventArgs);
            }
        }

        // Actually set the top level keyboard focus to the image control
        private void SetTopLevelFocus(bool checkForControlFocus, InputEventArgs eventArgs)
        {
            // If the text box or combobox has the focus, we usually don't want to reset the focus. 
            // However, there are a few instances (e.g., after enter has been pressed) where we no longer want it 
            // to have the focus, so we allow for that via this flag.
            if (checkForControlFocus && eventArgs is KeyEventArgs)
            {
                // If we are in a data control, don't reset the focus.
                if (this.SendKeyToDataEntryControlOrMenu((KeyEventArgs)eventArgs))
                {
                    return;
                }
            }

            // Don't raise the window just because we set the keyboard focus to it
            Keyboard.DefaultRestoreFocusMode = RestoreFocusMode.None;
            Keyboard.Focus(this.MarkableCanvas);
        }

        // Return true if the current focus is in a textbox or combobox data control
        private bool SendKeyToDataEntryControlOrMenu(KeyEventArgs eventData)
        {
            // check if a menu is open
            // it is sufficient to check one always visible item from each top level menu (file, edit, etc.)
            // NOTE: this must be kept in sync with 
            if (this.MenuItemExit.IsVisible || // file menu
                this.MenuItemCopyPreviousValues.IsVisible || // edit menu
                this.MenuItemMagnifyingGlass.IsVisible || // options menu
                this.MenuItemViewNextImage.IsVisible || // view menu
                this.MenuItemSelectAllImages.IsVisible || // select menu, and then the help menu...
                this.MenuItemAbout.IsVisible)
            {
                return true;
            }

            // by default focus will be on the MarkableImageCanvas
            // opening a menu doesn't change the focus
            IInputElement focusedElement = FocusManager.GetFocusedElement(this);
            if (focusedElement == null)
            {
                return false;
            }

            // check if focus is on a control
            // NOTE: this list must be kept in sync with the System.Windows classes used by the classes in Carnassial\Util\DataEntry*.cs
            Type type = focusedElement.GetType();
            if (Constants.Control.KeyboardInputTypes.Contains(type))
            {
                // send all keys to controls by default except
                // - escape as that's a natural way to back out of a control (the user can also hit enter)
                // - tab as that's the Windows keyboard navigation standard for moving between controls
                return eventData.Key != Key.Escape && eventData.Key != Key.Tab;
            }

            return false;
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
                this.dataHandler.ImageDatabase.UpdateImage(this.dataHandler.ImageCache.Current.ID, counter.DataLabel, newCounterData);
            }

            // Remove the marker in memory and from the database
            MarkersForCounter markersForCounter = null;
            foreach (MarkersForCounter markers in this.markersOnCurrentImage)
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
                this.dataHandler.ImageDatabase.SetMarkerPositions(this.dataHandler.ImageCache.Current.ID, markersForCounter);
            }

            this.MarkableCanvas_UpdateMarkers();
        }

        /// <summary>
        /// A new marker associated with a counter control has been created
        /// Increment the counter and add the marker to all data structures (including the database)
        /// </summary>
        private void MarkableCanvas_AddMarker(DataEntryCounter counter, Marker marker)
        {
            // Get the Counter Control's contents,  increment its value (as we have added a new marker) 
            // Then update the control's content as well as the database
            // If we can't convert it to an int, assume that someone set the default value to either a non-integer in the template, or that it's a space. In either case, revert it to zero.
            int count;
            if (Int32.TryParse(counter.Content, out count) == false)
            {
                count = 0; 
            }
            ++count;

            string counterContent = count.ToString();
            this.dataHandler.IsProgrammaticControlUpdate = true;
            this.dataHandler.ImageDatabase.UpdateImage(this.dataHandler.ImageCache.Current.ID, counter.DataLabel, counterContent);
            counter.SetContentAndTooltip(counterContent);
            this.dataHandler.IsProgrammaticControlUpdate = false;

            // Find markers associated with this particular control
            MarkersForCounter markersForCounter = null;
            foreach (MarkersForCounter markers in this.markersOnCurrentImage)
            {
                if (markers.DataLabel == counter.DataLabel)
                {
                    markersForCounter = markers;
                    break;
                }
            }

            // fill in marker information
            marker.Annotate = true; // Show the annotation as its created. We will clear it on the next refresh
            marker.AnnotationPreviouslyShown = false;
            marker.Brush = Brushes.Red;               // Make it Red (for now)
            marker.DataLabel = counter.DataLabel;
            marker.Tooltip = counter.Label;   // The tooltip will be the counter label plus its data label
            marker.Tooltip += "\n" + counter.DataLabel;
            markersForCounter.AddMarker(marker);

            // update this counter's list of points in the database
            this.dataHandler.ImageDatabase.SetMarkerPositions(this.dataHandler.ImageCache.Current.ID, markersForCounter);

            this.MarkableCanvas_UpdateMarkers(true);
            this.Speak(counter.Content + " " + counter.Label); // Speak the current count
        }

        private void MarkableCanvas_UpdateMarkers()
        {
            this.MarkableCanvas_UpdateMarkers(false); // By default, we don't show the annotation
        }

        private void MarkableCanvas_UpdateMarkers(bool showAnnotation)
        {
            List<Marker> markers = new List<Marker>();
            if (this.markersOnCurrentImage != null)
            {
                DataEntryCounter selectedCounter = this.FindSelectedCounter();
                for (int counter = 0; counter < this.markersOnCurrentImage.Count; ++counter)
                {
                    MarkersForCounter markersForCounter = this.markersOnCurrentImage[counter];
                    DataEntryControl control;
                    if (this.DataEntryControls.ControlsByDataLabel.TryGetValue(markersForCounter.DataLabel, out control) == false)
                    {
                        // If we can't find the counter, its likely because the control was made invisible in the template,
                        // which means that there is no control associated with the marker. So just don't create the 
                        // markers associated with this control. Note that if the control is later made visible in the template,
                        // the markers will then be shown. 
                        continue;
                    }

                    // Update the emphasise for each tag to reflect how the user is interacting with tags
                    DataEntryCounter currentCounter = (DataEntryCounter)this.DataEntryControls.ControlsByDataLabel[markersForCounter.DataLabel];
                    bool emphasize = markersForCounter.DataLabel == this.state.MouseOverCounter;
                    foreach (Marker marker in markersForCounter.Markers)
                    {
                        // the first time through, show an annotation. Otherwise we clear the flags to hide the annotation.
                        if (marker.Annotate && !marker.AnnotationPreviouslyShown)
                        {
                            marker.Annotate = true;
                            marker.AnnotationPreviouslyShown = true;
                        }
                        else
                        {
                            marker.Annotate = false;
                        }

                        if (selectedCounter != null && currentCounter.DataLabel == selectedCounter.DataLabel)
                        {
                            marker.Brush = (SolidColorBrush)new BrushConverter().ConvertFromString(Constants.SelectionColour);
                        }
                        else
                        {
                            marker.Brush = (SolidColorBrush)new BrushConverter().ConvertFromString(Constants.StandardColour);
                        }

                        marker.Emphasise = emphasize;
                        marker.Tooltip = currentCounter.Label;
                        markers.Add(marker); 
                    }
                }
            }
            this.MarkableCanvas.Markers = markers;
        }

        private void File_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            this.MenuItemRecentImageSets_Refresh();
        }

        private void MenuItemAddImagesToImageSet_Click(object sender, RoutedEventArgs e)
        {
            IEnumerable<string> folderPaths;
            if (this.ShowFolderSelectionDialog(out folderPaths))
            {
                BackgroundWorker backgroundWorker;
                this.TryBeginImageFolderLoadAsync(folderPaths, out backgroundWorker);
            }
        }

        /// <summary>Load the images from a folder.</summary>
        private void MenuItemLoadImages_Click(object sender, RoutedEventArgs e)
        {
            string templateDatabasePath;
            if (this.TryGetTemplatePath(out templateDatabasePath))
            {
                BackgroundWorker backgroundWorker;
                this.TryOpenTemplateAndBeginLoadImagesAsync(templateDatabasePath, out backgroundWorker);
            }     
        }

        /// <summary>Write the CSV file and preview it in excel.</summary>
        private void MenuItemExportCsv_Click(object sender, RoutedEventArgs e)
        {
            if (this.state.SuppressSelectedCsvExportPrompt == false &&
                this.dataHandler.ImageDatabase.ImageSet.ImageSelection != ImageSelection.All)
            {
                MessageBox messageBox = new MessageBox("Exporting a partial selection to a CSV file", this, MessageBoxButton.OKCancel);
                messageBox.Message.What = "Only a subset of your data will be exported to the CSV file.";
                messageBox.Message.Reason = "As your selection (in the Select menu) is not set to 'All', ";
                messageBox.Message.Reason += "only data for those files selected will be exported. ";
                messageBox.Message.Solution = "If you want to export just this subset, then " + Environment.NewLine;
                messageBox.Message.Solution += "\u2022 click Okay" + Environment.NewLine + Environment.NewLine;
                messageBox.Message.Solution += "If you want to export all your data for all your files, then " + Environment.NewLine;
                messageBox.Message.Solution += "\u2022 click Cancel," + Environment.NewLine;
                messageBox.Message.Solution += "\u2022 select 'All Files' in the Select menu, " + Environment.NewLine;
                messageBox.Message.Solution += "\u2022 retry exporting your data as a CSV file.";
                messageBox.Message.Hint = "If you check don't show this message this dialog can be turned back on via the Options menu.";
                messageBox.Message.Icon = MessageBoxImage.Warning;
                messageBox.DontShowAgain.Visibility = Visibility.Visible;

                bool? exportCsv = messageBox.ShowDialog();
                if (exportCsv != true)
                {
                    return;
                }

                if (messageBox.DontShowAgain.IsChecked.HasValue)
                {
                    this.state.SuppressSelectedCsvExportPrompt = messageBox.DontShowAgain.IsChecked.Value;
                    this.MenuItemEnableSelectedCsvExportPrompt.IsChecked = !this.state.SuppressSelectedCsvExportPrompt;
                }
            }

            // Generate the file names/path
            string csvFileName = Path.GetFileNameWithoutExtension(this.dataHandler.ImageDatabase.FileName) + ".csv";
            string csvFilePath = Path.Combine(this.FolderPath, csvFileName);

            // Backup the csv file if it exists, as the export will overwrite it. 
            if (FileBackup.TryCreateBackup(this.FolderPath, csvFileName))
            {
                this.statusBar.SetMessage("Backup of csv file made.");
            }
            else
            {
                this.statusBar.SetMessage("No csv file backup was made.");
            }

            CsvReaderWriter csvWriter = new CsvReaderWriter();
            try
            {
                csvWriter.ExportToCsv(this.dataHandler.ImageDatabase, csvFilePath);
            }
            catch (IOException exception)
            {
                // Can't write the spreadsheet file
                MessageBox messageBox = new MessageBox("Can't write the spreadsheet file.", this);
                messageBox.Message.Icon = MessageBoxImage.Error;
                messageBox.Message.Problem = "The following file can't be written: " + csvFilePath;
                messageBox.Message.Reason = "You may already have it open in Excel or another application.";
                messageBox.Message.Solution = "If the file is open in another application, close it and try again.";
                messageBox.Message.Hint = String.Format("{0}: {1}", exception.GetType().FullName, exception.Message);
                messageBox.ShowDialog();
                return;
            }

            MenuItem mi = (MenuItem)sender;
            if (mi == this.MenuItemExportAsCsvAndPreview)
            {
                // Show the file in excel
                // Create a process that will try to show the file
                Process process = new Process();

                process.StartInfo.UseShellExecute = true;
                process.StartInfo.RedirectStandardOutput = false;
                process.StartInfo.FileName = csvFilePath;
                process.Start();
            }
            else if (this.state.SuppressCsvExportDialog == false)
            {
                // Since we don't show the file, give the user some feedback about the export operation
                ExportCsv csvExportInformation = new ExportCsv(csvFileName, this);
                bool? result = csvExportInformation.ShowDialog();
                if (result.HasValue && result.Value && csvExportInformation.DontShowAgain.IsChecked.HasValue)
                {
                    this.state.SuppressCsvExportDialog = csvExportInformation.DontShowAgain.IsChecked.Value;
                    this.MenuItemEnableCsvExportDialog.IsChecked = !this.state.SuppressCsvExportDialog;
                }
            }
            this.statusBar.SetMessage("Data exported to " + csvFileName);
        }

        /// <summary>
        /// Export the current image to the folder selected by the user via a folder browser dialog.
        /// and provide feedback in the status bar if done.
        /// </summary>
        private void MenuItemExportThisImage_Click(object sender, RoutedEventArgs e)
        {
            if (!this.dataHandler.ImageCache.Current.IsDisplayable())
            {
                MessageBox messageBox = new MessageBox("Can't export this file!", this);
                messageBox.Message.Icon = MessageBoxImage.Error;
                messageBox.Message.Problem = "We can't export the currently displayed file.";
                messageBox.Message.Reason = "It is likely a corrupted or missing file.";
                messageBox.Message.Solution = "Make sure you have navigated to, and are displaying, a valid file before you try to export it.";
                messageBox.ShowDialog();
                return;
            }
            // Get the file name of the current image 
            string sourceFile = this.dataHandler.ImageCache.Current.FileName;

            // Set up a Folder Browser with some instructions
            System.Windows.Forms.SaveFileDialog dialog = new System.Windows.Forms.SaveFileDialog();
            dialog.Title = "Export a copy of the currently displayed file";
            dialog.Filter = String.Format("*{0}|*{0}", Path.GetExtension(this.dataHandler.ImageCache.Current.FileName));
            dialog.FileName = sourceFile;
            dialog.OverwritePrompt = true;

            // Display the Folder Browser dialog
            System.Windows.Forms.DialogResult result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                // Set the source and destination file names, including the complete path
                string sourceFileName = Path.Combine(this.FolderPath, sourceFile);
                string destFileName = dialog.FileName;

                // Try to copy the source file to the destination, overwriting the destination file if it already exists.
                // And giving some feedback about its success (or failure) 
                try
                {
                    File.Copy(sourceFileName, destFileName, true);
                    this.statusBar.SetMessage(sourceFile + " copied to " + destFileName);
                }
                catch (Exception exception)
                {
                    Debug.Fail(String.Format("Copy of '{0}' to '{1}' failed.", sourceFile, destFileName), exception.ToString());
                    this.statusBar.SetMessage(String.Format("Copy failed with {0}.", exception.GetType().Name));
                }
            }
        }

        private void MenuItemImportFromCsv_Click(object sender, RoutedEventArgs e)
        {
            if (this.state.SuppressCsvImportPrompt == false)
            {
                MessageBox messageBox = new MessageBox("How importing CSV data works", this, MessageBoxButton.OKCancel);
                messageBox.Message.What = "Importing data from a CSV (comma separated value) file follows the rules below.";
                messageBox.Message.Reason = "Carnassial requires the CSV file follow a specific format and processes its data a specific way.";
                messageBox.Message.Solution = "\u2022 Only modify and import a CSV file previously exported by Carnassial." + Environment.NewLine;
                messageBox.Message.Solution += "\u2022 Do not change Folder, RelativePath, or File as those fields uniquely identify a file" + Environment.NewLine;
                messageBox.Message.Solution += "\u2022 Do not change column names" + Environment.NewLine;
                messageBox.Message.Solution += "\u2022 Do not add or delete rows (those changes will be ignored)" + Environment.NewLine;
                messageBox.Message.Solution += "\u2022 Restrict modifications as follows:" + Environment.NewLine;
                messageBox.Message.Solution += String.Format("    \u2022 DateTime must be in '{0}' format{1}", Constants.Time.DateTimeDatabaseFormat, Environment.NewLine);
                messageBox.Message.Solution += String.Format("    \u2022 UtcOffset must be a floating point number between {0} and {1}, inclusive{2}", DateTimeHandler.ToDatabaseUtcOffsetString(Constants.Time.MinimumUtcOffset), DateTimeHandler.ToDatabaseUtcOffsetString(Constants.Time.MinimumUtcOffset), Environment.NewLine);
                messageBox.Message.Solution += "    \u2022 Counter data must be zero or a positive integer" + Environment.NewLine;
                messageBox.Message.Solution += "    \u2022 Flag data must be 'true' or 'false'" + Environment.NewLine;
                messageBox.Message.Solution += "    \u2022 FixedChoice data must be a string that exactly matches one of the FixedChoice menu options or the default value." + Environment.NewLine;
                messageBox.Message.Solution += "    \u2022 Note data can be any string, including empty.";
                messageBox.Message.Result = "Carnassial will create a backup .ddb file in the Backups folder and then try its best.";
                messageBox.Message.Hint = "\u2022 After you import, check your data. If it is not what you expect, restore your data by using that backup file." + Environment.NewLine;
                messageBox.Message.Hint += "\u2022 If you check don't show this message this dialog can be turned back on via the Options menu.";
                messageBox.Message.Icon = MessageBoxImage.Warning;
                messageBox.DontShowAgain.Visibility = Visibility.Visible;

                bool? proceeed = messageBox.ShowDialog();
                if (proceeed != true)
                {
                    return;
                }

                if (messageBox.DontShowAgain.IsChecked.HasValue)
                {
                    this.state.SuppressCsvImportPrompt = messageBox.DontShowAgain.IsChecked.Value;
                    this.MenuItemEnableCsvImportPrompt.IsChecked = !this.state.SuppressCsvImportPrompt;
                }
            }

            string csvFileName = Path.GetFileNameWithoutExtension(this.dataHandler.ImageDatabase.FileName) + Constants.File.CsvFileExtension;
            string csvFilePath;
            if (Utilities.TryGetFileFromUser("Select a .csv file to merge into the current image set",
                                             Path.Combine(this.dataHandler.ImageDatabase.FolderPath, csvFileName),
                                             String.Format("Comma separated value files (*{0})|*{0}", Constants.File.CsvFileExtension),
                                             out csvFilePath) == false)
            {
                return;
            }

            // Create a backup database file
            if (FileBackup.TryCreateBackup(this.dataHandler.ImageDatabase.FilePath))
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
                if (csvReader.TryImportFromCsv(csvFilePath, this.dataHandler.ImageDatabase, out importErrors) == false)
                {
                    MessageBox messageBox = new MessageBox("Can't import the CSV file.", this);
                    messageBox.Message.Icon = MessageBoxImage.Error;
                    messageBox.Message.Problem = String.Format("The file {0} could not be read.", csvFilePath);
                    messageBox.Message.Reason = "The CSV file is not compatible with the current image set.";
                    messageBox.Message.Solution = "Check that:" + Environment.NewLine;
                    messageBox.Message.Solution += "\u2022 The first row of the CSV file is a header line." + Environment.NewLine;
                    messageBox.Message.Solution += "\u2022 The column names in the header line match the database." + Environment.NewLine; 
                    messageBox.Message.Solution += "\u2022 Choice values use the correct case." + Environment.NewLine;
                    messageBox.Message.Solution += "\u2022 Counter values are numbers." + Environment.NewLine;
                    messageBox.Message.Solution += "\u2022 Flag values are either 'true' or 'false'.";
                    messageBox.Message.Result = "Either no data was imported or invalid parts of the CSV were skipped.";
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
                MessageBox messageBox = new MessageBox("Can't import the CSV file.", this);
                messageBox.Message.Icon = MessageBoxImage.Error;
                messageBox.Message.Problem = String.Format("The file {0} could not be opened.", csvFilePath);
                messageBox.Message.Reason = "Most likely the file is open in another program.";
                messageBox.Message.Solution = "If the file is open in another program, close it.";
                messageBox.Message.Result = String.Format("{0}: {1}", exception.GetType().FullName, exception.Message);
                messageBox.Message.Hint = "Is the file open in Excel?";
                messageBox.ShowDialog();
            }
            // Reload the data table
            this.SelectDataTableImagesAndShowImage(this.dataHandler.ImageCache.CurrentRow, this.dataHandler.ImageDatabase.ImageSet.ImageSelection);
            this.statusBar.SetMessage("CSV file imported.");
        }

        private void MenuItemRecentImageSet_Click(object sender, RoutedEventArgs e)
        {
            string recentDatabasePath = (string)((MenuItem)sender).ToolTip;
            BackgroundWorker backgroundWorker;
            if (this.TryOpenTemplateAndBeginLoadImagesAsync(recentDatabasePath, out backgroundWorker) == false)
            {
                this.state.MostRecentImageSets.TryRemove(recentDatabasePath);
                this.MenuItemRecentImageSets_Refresh();
            }
        }

        /// <summary>
        /// Update the list of recent databases displayed under File -> Recent Databases.
        /// </summary>
        private void MenuItemRecentImageSets_Refresh()
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

            // Enable the menu only when there are items in it and only if the load menu is also enabled (i.e., that we haven't loaded anything yet)
            this.MenuItemRecentImageSets.IsEnabled = this.state.MostRecentImageSets.Count > 0 && this.MenuItemLoadImages.IsEnabled;
            this.MenuItemRecentImageSets.Items.Clear();

            // add menu items most recently used image sets
            int index = 1;
            foreach (string recentImageSetPath in this.state.MostRecentImageSets)
            {
                // Create a menu item for each path
                MenuItem recentImageSetItem = new MenuItem();
                recentImageSetItem.Click += this.MenuItemRecentImageSet_Click;
                recentImageSetItem.Header = String.Format("_{0} {1}", index++, recentImageSetPath);
                recentImageSetItem.ToolTip = recentImageSetPath;
                this.MenuItemRecentImageSets.Items.Add(recentImageSetItem);
            }
        }

        private void MenuItemRenameImageDatabaseFile_Click(object sender, RoutedEventArgs e)
        {
            RenameImageDatabaseFile renameImageDatabase = new RenameImageDatabaseFile(this.dataHandler.ImageDatabase.FileName, this);
            renameImageDatabase.Owner = this;
            bool? result = renameImageDatabase.ShowDialog();
            if (result == true)
            {
                this.dataHandler.ImageDatabase.RenameFile(renameImageDatabase.NewFilename);
            }
        }

        /// <summary>
        /// Exit Carnassial
        /// </summary>
        private void MenuItemExit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
            Application.Current.Shutdown();
        }

        private bool ShowFolderSelectionDialog(out IEnumerable<string> folderPaths)
        {
            CommonOpenFileDialog folderSelectionDialog = new CommonOpenFileDialog();
            folderSelectionDialog.Title = "Select a folder ...";
            folderSelectionDialog.DefaultDirectory = this.mostRecentImageAddFolderPath == null ? this.FolderPath : this.mostRecentImageAddFolderPath;
            folderSelectionDialog.IsFolderPicker = true;
            folderSelectionDialog.Multiselect = true;
            if (folderSelectionDialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                folderPaths = folderSelectionDialog.FileNames;

                // remember the parent of the selected folder path to save the user clicks and scrolling in case images from additional 
                // directories are added
                this.mostRecentImageAddFolderPath = Path.GetDirectoryName(folderPaths.Last());
                return true;
            }

            folderPaths = null;
            return false;
        }

        // Populate a data field from metadata (example metadata displayed from the currently selected image)
        private void MenuItemPopulateFieldFromMetadata_Click(object sender, RoutedEventArgs e)
        {
            if (this.dataHandler.ImageCache.Current.IsDisplayable() == false)
            {
                int firstImageDisplayable = this.dataHandler.ImageDatabase.FindFirstDisplayableImage(Constants.DefaultImageRowIndex);
                if (firstImageDisplayable == -1)
                {
                    // There are no displayable images, and thus no metadata to choose from, so abort
                    MessageBox messageBox = new MessageBox("Can't populate a data field with image metadata.", this);
                    messageBox.Message.Problem = "Metadata is not available as no file in the image set can be read." + Environment.NewLine;
                    messageBox.Message.Reason += "Carnassial must have at least one valid file in order to get its metadata.  All files are either corrupted or removed.";
                    messageBox.Message.Icon = MessageBoxImage.Error;
                    messageBox.ShowDialog();
                    return;
                }
            }

            if (this.MaybePromptToApplyOperationIfPartialSelection(this.state.SuppressSelectedPopulateFieldFromMetadataPrompt, 
                                                               "'Populate a data field with image metadata of your choosing...'", 
                                                               (bool optOut) =>
                                                               {
                                                                   this.state.SuppressSelectedPopulateFieldFromMetadataPrompt = optOut;
                                                                   this.MenuItemEnableSelectedPopulateFieldFromMetadataPrompt.IsChecked = !optOut;
                                                               }))
            {
                PopulateFieldWithMetadata populateField = new PopulateFieldWithMetadata(this.dataHandler.ImageDatabase, this.dataHandler.ImageCache.Current.GetImagePath(this.FolderPath), this);
                this.ShowBulkImageEditDialog(populateField);
            }
        }

        /// <summary>Delete the current image by replacing it with a placeholder image, while still making a backup of it</summary>
        private void Delete_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            int deletedImages = this.dataHandler.ImageDatabase.GetImageCount(ImageSelection.MarkedForDeletion);
            this.MenuItemDeleteCurrentFile.IsEnabled = this.dataHandler.ImageCache.Current.IsDisplayable() || this.dataHandler.ImageCache.Current.ImageQuality == ImageSelection.CorruptFile;
            this.MenuItemDeleteCurrentFileAndData.IsEnabled = true;
            this.MenuItemDeleteFiles.IsEnabled = deletedImages > 0;
            this.MenuItemDeleteFilesAndData.IsEnabled = deletedImages > 0;
        }

        /// <summary>Delete all images marked for deletion, and optionally the data associated with those images.
        /// Deleted images are actually moved to a backup folder.</summary>
        private void MenuItemDeleteImages_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = sender as MenuItem;

            // This callback is invoked by DeleteImage (which deletes the current image) and DeleteImages (which deletes the images marked by the deletion flag)
            // Thus we need to use two different methods to construct a table containing all the images marked for deletion
            List<ImageRow> imagesToDelete;
            bool deleteCurrentImageOnly;
            bool deleteFilesAndData;
            if (menuItem.Name.Equals(this.MenuItemDeleteFiles.Name) || menuItem.Name.Equals(this.MenuItemDeleteFilesAndData.Name))
            {
                deleteCurrentImageOnly = false;
                deleteFilesAndData = menuItem.Name.Equals(this.MenuItemDeleteFilesAndData.Name);
                // get list of all images marked for deletion in the current seletion
                imagesToDelete = this.dataHandler.ImageDatabase.GetImagesMarkedForDeletion().ToList();
                for (int index = imagesToDelete.Count - 1; index >= 0;  index--)
                {
                    if (this.dataHandler.ImageDatabase.ImageDataTable.Find(imagesToDelete[index].ID) == null)
                    {
                        imagesToDelete.Remove(imagesToDelete[index]);
                    }
                }
            }
            else
            {
                // Delete current image case. Get the ID of the current image and construct a datatable that contains that image's datarow
                deleteCurrentImageOnly = true;
                deleteFilesAndData = menuItem.Name.Equals(this.MenuItemDeleteCurrentFileAndData.Name);
                imagesToDelete = new List<ImageRow>();
                if (this.dataHandler.ImageCache.Current != null)
                {
                    imagesToDelete.Add(this.dataHandler.ImageCache.Current);
                }
            }

            // If no images are selected for deletion. Warn the user.
            // Note that this should never happen, as the invoking menu item should be disabled (and thus not selectable)
            // if there aren't any images to delete. Still,...
            if (imagesToDelete == null || imagesToDelete.Count < 1)
            {
                MessageBox messageBox = new MessageBox("No files are marked for deletion", this);
                messageBox.Message.Problem = "You are trying to delete files marked for deletion, but none of the files have their 'Delete?' field checked.";
                messageBox.Message.Hint = "If you have files that you think should be deleted, check thier Delete? field.";
                messageBox.Message.Icon = MessageBoxImage.Information;
                messageBox.ShowDialog();
                return;
            }

            DeleteImages deleteImagesDialog = new DeleteImages(this.dataHandler.ImageDatabase, imagesToDelete, deleteFilesAndData, deleteCurrentImageOnly, this);
            bool? result = deleteImagesDialog.ShowDialog();
            if (result == true)
            {
                // cache the current ID as the current image may be invalidated
                long previousImageID = this.dataHandler.ImageCache.Current.ID;

                Mouse.OverrideCursor = Cursors.Wait;
                List<ColumnTuplesWithWhere> imagesToUpdate = new List<ColumnTuplesWithWhere>();
                List<long> imageIDsToDropFromDatabase = new List<long>();
                foreach (ImageRow image in imagesToDelete)
                {
                    // invalidate cache so FileNoLongerAvailable placeholder will be displayed
                    // release any handle open on the file so it can be moved
                    this.dataHandler.ImageCache.TryInvalidate(image.ID);
                    if (image.TryMoveFileToDeletedImagesFolder(this.dataHandler.ImageDatabase.FolderPath) == false)
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
                        image.ImageQuality = ImageSelection.FileNoLongerAvailable;
                        List<ColumnTuple> columnTuples = new List<ColumnTuple>()
                        {
                            new ColumnTuple(Constants.DatabaseColumn.DeleteFlag, Constants.Boolean.False),
                            new ColumnTuple(Constants.DatabaseColumn.ImageQuality, ImageSelection.FileNoLongerAvailable.ToString())
                        };
                        imagesToUpdate.Add(new ColumnTuplesWithWhere(columnTuples, image.ID));
                    }
                }

                if (deleteFilesAndData)
                {
                    // drop images
                    this.dataHandler.ImageDatabase.DeleteImages(imageIDsToDropFromDatabase);

                    // Reload the datatable. Then find and show the image closest to the last one shown
                    this.SelectDataTableImagesAndShowImage(Constants.DefaultImageRowIndex, this.dataHandler.ImageDatabase.ImageSet.ImageSelection);
                    if (this.dataHandler.ImageDatabase.CurrentlySelectedImageCount > 0)
                    {
                        int nextImageRow = this.dataHandler.ImageDatabase.FindClosestImageRow(previousImageID);
                        this.ShowImage(nextImageRow);
                    }
                    else
                    {
                        this.EnableOrDisableMenusAndControls();
                    }
                }
                else
                {
                    // update image properties
                    this.dataHandler.ImageDatabase.UpdateImages(imagesToUpdate);

                    // display the updated properties on the current image
                    int nextImageRow = this.dataHandler.ImageDatabase.FindClosestImageRow(previousImageID);
                    this.ShowImage(nextImageRow);
                }
                Mouse.OverrideCursor = null;
            }
        }

        /// <summary>Add some text to the image set log</summary>
        private void MenuItemLog_Click(object sender, RoutedEventArgs e)
        {
            EditLog editImageSetLog = new EditLog(this.dataHandler.ImageDatabase.ImageSet.Log, this);
            editImageSetLog.Owner = this;
            bool? result = editImageSetLog.ShowDialog();
            if (result == true)
            {
                this.dataHandler.ImageDatabase.ImageSet.Log = editImageSetLog.Log.Text;
                this.dataHandler.ImageDatabase.SyncImageSetToDatabase();
            }
        }

        private void CopyPreviousValues_Click(object sender, RoutedEventArgs e)
        {
            int previousRow = this.dataHandler.ImageCache.CurrentRow - 1;
            if (previousRow < 0)
            {
                return; // We are already on the first image, so there is nothing to copy
            }

            foreach (KeyValuePair<string, DataEntryControl> pair in this.DataEntryControls.ControlsByDataLabel)
            {
                DataEntryControl control = pair.Value;
                if (this.dataHandler.ImageDatabase.IsControlCopyable(control.DataLabel))
                {
                    control.SetContentAndTooltip(this.dataHandler.ImageDatabase.ImageDataTable[previousRow].GetValueDisplayString(control.DataLabel));
                }
            }
        }

        /// <summary>Show advanced image set options</summary>
        private void MenuItemAdvancedImageSetOptions_Click(object sender, RoutedEventArgs e)
        {
            AdvancedImageSetOptions advancedImageSetOptions = new AdvancedImageSetOptions(this.dataHandler.ImageDatabase, this);
            advancedImageSetOptions.ShowDialog();
        }

        /// <summary>Show advanced Carnassial options</summary>
        private void MenuItemAdvancedCarnassialOptions_Click(object sender, RoutedEventArgs e)
        {
            AdvancedCarnassialOptions advancedCarnassialOptions = new AdvancedCarnassialOptions(this.state, this.MarkableCanvas, this);
            advancedCarnassialOptions.ShowDialog();
        }

        /// <summary>Toggle the magnifier on and off</summary>
        private void MenuItemDisplayMagnifier_Click(object sender, RoutedEventArgs e)
        {
            this.dataHandler.ImageDatabase.ImageSet.MagnifierEnabled = !this.dataHandler.ImageDatabase.ImageSet.MagnifierEnabled;
            this.MarkableCanvas.IsMagnifyingGlassVisible = this.dataHandler.ImageDatabase.ImageSet.MagnifierEnabled;
            this.MenuItemDisplayMagnifier.IsChecked = this.dataHandler.ImageDatabase.ImageSet.MagnifierEnabled;
        }

        /// <summary>Increase the magnification of the magnifying glass. We do this several times to make
        /// the increase effect more visible through a menu option versus the keyboard equivalent</summary>
        private void MenuItemMagnifierIncrease_Click(object sender, RoutedEventArgs e)
        {
            this.MarkableCanvas.MagnifierZoomIn();
            this.MarkableCanvas.MagnifierZoomIn();
            this.MarkableCanvas.MagnifierZoomIn();
            this.MarkableCanvas.MagnifierZoomIn();
            this.MarkableCanvas.MagnifierZoomIn();
            this.MarkableCanvas.MagnifierZoomIn();
        }

        /// <summary> Decrease the magnification of the magnifying glass. We do this several times to make
        /// the increase effect more visible through a menu option versus the keyboard equivalent</summary>
        private void MenuItemMagnifierDecrease_Click(object sender, RoutedEventArgs e)
        {
            this.MarkableCanvas.MagnifierZoomOut();
            this.MarkableCanvas.MagnifierZoomOut();
            this.MarkableCanvas.MagnifierZoomOut();
            this.MarkableCanvas.MagnifierZoomOut();
            this.MarkableCanvas.MagnifierZoomOut();
            this.MarkableCanvas.MagnifierZoomOut();
        }

        private void MenuItemOptionsDarkImagesThreshold_Click(object sender, RoutedEventArgs e)
        {
            if (this.MaybePromptToApplyOperationIfPartialSelection(this.state.SuppressSelectedDarkThresholdPrompt,
                                                               "'Customize the threshold for determining dark files...'", 
                                                               (bool optOut) =>
                                                               {
                                                                   this.state.SuppressSelectedDarkThresholdPrompt = optOut;
                                                                   this.MenuItemEnableSelectedDarkThresholdPrompt.IsChecked = !optOut;
                                                               }))
            {
                using (DarkImagesThreshold darkThreshold = new DarkImagesThreshold(this.dataHandler.ImageDatabase, this.dataHandler.ImageCache.CurrentRow, this.state, this))
                {
                    darkThreshold.Owner = this;
                    darkThreshold.ShowDialog();
                }
            }
        }

        /// <summary>Correct the date by specifying an offset.</summary>
        private void MenuItemDateTimeFixedCorrection_Click(object sender, RoutedEventArgs e)
        {
            if (this.MaybePromptToApplyOperationIfPartialSelection(this.state.SuppressSelectedDateTimeFixedCorrectionPrompt, 
                                                               "'Add a fixed correction value to every date/time...'", 
                                                               (bool optOut) =>
                                                               {
                                                                   this.state.SuppressSelectedDateTimeFixedCorrectionPrompt = optOut;
                                                                   this.MenuItemEnableSelectedDateTimeFixedCorrectionPrompt.IsChecked = !optOut;
                                                               }))
            {
                DateTimeFixedCorrection fixedDateCorrection = new DateTimeFixedCorrection(this.dataHandler.ImageDatabase, this.dataHandler.ImageCache.Current, this);
                this.ShowBulkImageEditDialog(fixedDateCorrection);
            }
        }

        /// <summary>Correct for drifting clock times. Correction applied only to selected files.</summary>
        private void MenuItemDateTimeLinearCorrection_Click(object sender, RoutedEventArgs e)
        {
            if (this.MaybePromptToApplyOperationIfPartialSelection(this.state.SuppressSelectedDateTimeLinearCorrectionPrompt,
                                                               "'Correct for camera clock drift'",
                                                               (bool optOut) =>
                                                               {
                                                                   this.state.SuppressSelectedDateTimeLinearCorrectionPrompt = optOut;
                                                                   this.MenuItemEnableSelectedDateTimeLinearCorrectionPrompt.IsChecked = !optOut;
                                                               }))
            { 
                DateTimeLinearCorrection linearDateCorrection = new DateTimeLinearCorrection(this.dataHandler.ImageDatabase, this);
                if (linearDateCorrection.Abort)
                {
                    MessageBox messageBox = new MessageBox("Can't correct for clock drift", this);
                    messageBox.Message.Problem = "Can't correct for clock drift.";
                    messageBox.Message.Reason = "All of the files selected have date/time fields whose contents are not recognizable as dates or times." + Environment.NewLine;
                    messageBox.Message.Reason += "\u2022 dates should look like dd-MMM-yyyy e.g., 16-Jan-2016" + Environment.NewLine;
                    messageBox.Message.Reason += "\u2022 times should look like HH:mm:ss using 24 hour time e.g., 01:05:30 or 13:30:00";
                    messageBox.Message.Result = "Date correction will be aborted and nothing will be changed.";
                    messageBox.Message.Hint = "Check the format of your dates and times. You may also want to change your selection if you're not viewing All files.";
                    messageBox.Message.Icon = MessageBoxImage.Error;
                    messageBox.ShowDialog();
                    return;
                }
                this.ShowBulkImageEditDialog(linearDateCorrection);
            }
        }

        /// <summary>Correct for daylight savings time</summary>
        private void MenuItemDaylightSavingsTimeCorrection_Click(object sender, RoutedEventArgs e)
        {
            if (this.dataHandler.ImageCache.Current.IsDisplayable() == false)
            {
                // Just a corrupted image
                MessageBox messageBox = new MessageBox("Can't correct for daylight savings time.", this);
                messageBox.Message.Problem = "This is a corrupted file.";
                messageBox.Message.Solution = "To correct for daylight savings time, you need to:" + Environment.NewLine;
                messageBox.Message.Solution += "\u2022 be displaying a file with a valid date ";
                messageBox.Message.Solution += "\u2022 where that file should be the one at the daylight savings time threshold.";
                messageBox.Message.Icon = MessageBoxImage.Exclamation;
                messageBox.ShowDialog();
                return;
            }

            if (this.MaybePromptToApplyOperationIfPartialSelection(this.state.SuppressSelectedDaylightSavingsCorrectionPrompt, 
                                                               "'Correct for daylight savings time...'", 
                                                               (bool optOut) =>
                                                               {
                                                                   this.state.SuppressSelectedDaylightSavingsCorrectionPrompt = optOut;
                                                                   this.MenuItemEnableSelectedDaylightSavingsCorrectionPrompt.IsChecked = !optOut;
                                                               }))
            {
                DateDaylightSavingsTimeCorrection dateTimeChange = new DateDaylightSavingsTimeCorrection(this.dataHandler.ImageDatabase, this.dataHandler.ImageCache, this);
                this.ShowBulkImageEditDialog(dateTimeChange);
            }
        }

        // Correct ambiguous dates dialog (i.e. dates that could be read as either month/day or day/month
        private void MenuItemCorrectAmbiguousDates_Click(object sender, RoutedEventArgs e)
        {
            if (this.MaybePromptToApplyOperationIfPartialSelection(this.state.SuppressSelectedAmbiguousDatesPrompt, 
                                                               "'Correct ambiguous dates...'",
                                                               (bool optOut) =>
                                                               {
                                                                   this.state.SuppressSelectedAmbiguousDatesPrompt = optOut;
                                                                   this.MenuItemEnableSelectedAmbiguousDatesPrompt.IsChecked = !optOut;
                                                               }))
            {
                DateCorrectAmbiguous dateCorrection = new DateCorrectAmbiguous(this.dataHandler.ImageDatabase, this);
                if (dateCorrection.Abort)
                {
                    MessageBox messageBox = new MessageBox("No ambiguous dates found", this);
                    messageBox.Message.What = "No ambiguous dates found.";
                    messageBox.Message.Reason = "All of the selected images have unambguous date fields." + Environment.NewLine;
                    messageBox.Message.Result = "No corrections needed, and no changes have been made." + Environment.NewLine;
                    messageBox.Message.Icon = MessageBoxImage.Information;
                    messageBox.ShowDialog();
                    messageBox.Close();
                    return;
                 }
                 this.ShowBulkImageEditDialog(dateCorrection);
            }
        }

        private void MenuItemSetTimeZone_Click(object sender, RoutedEventArgs e)
        {
            if (this.MaybePromptToApplyOperationIfPartialSelection(this.state.SuppressSelectedSetTimeZonePrompt,
                                                               "'Set the time zone of every date/time...'",
                                                               (bool optOut) =>
                                                               {
                                                                   this.state.SuppressSelectedSetTimeZonePrompt = optOut;
                                                                   this.MenuItemEnableSelectedSetTimeZonePrompt.IsChecked = !optOut;
                                                               }))
            {
                DateTimeSetTimeZone fixedDateCorrection = new DateTimeSetTimeZone(this.dataHandler.ImageDatabase, this.dataHandler.ImageCache.Current, this);
                this.ShowBulkImageEditDialog(fixedDateCorrection);
            }
        }

        private void MenuItemAmbiguousDatesDialog_Click(object sender, RoutedEventArgs e)
        {
            this.state.SuppressAmbiguousDatesDialog = !this.state.SuppressAmbiguousDatesDialog;
            this.MenuItemEnableAmbiguousDatesDialog.IsChecked = !this.state.SuppressAmbiguousDatesDialog;
        }

        private void MenuItemEnableCsvExportDialog_Click(object sender, RoutedEventArgs e)
        {
            this.state.SuppressCsvExportDialog = !this.state.SuppressCsvExportDialog;
            this.MenuItemEnableCsvExportDialog.IsChecked = !this.state.SuppressCsvExportDialog;
        }

        private void MenuItemEnableCsvImportPrompt_Click(object sender, RoutedEventArgs e)
        {
            this.state.SuppressCsvImportPrompt = !this.state.SuppressCsvImportPrompt;
            this.MenuItemEnableCsvImportPrompt.IsChecked = !this.state.SuppressCsvImportPrompt;
        }

        private void MenuItemEnableFileCountOnImportDialog_Click(object sender, RoutedEventArgs e)
        {
            this.state.SuppressFileCountOnImportDialog = !this.state.SuppressFileCountOnImportDialog;
            this.MenuItemEnableFileCountOnImportDialog.IsChecked = !this.state.SuppressFileCountOnImportDialog;
        }

        private void MenuItemEnableSelectedAmbiguousDatesPrompt_Click(object sender, RoutedEventArgs e)
        {
            this.state.SuppressSelectedAmbiguousDatesPrompt = !this.state.SuppressSelectedAmbiguousDatesPrompt;
            this.MenuItemEnableSelectedAmbiguousDatesPrompt.IsChecked = !this.state.SuppressSelectedAmbiguousDatesPrompt;
        }

        private void MenuItemEnableSelectedCsvExportPrompt_Click(object sender, RoutedEventArgs e)
        {
            this.state.SuppressSelectedCsvExportPrompt = !this.state.SuppressSelectedCsvExportPrompt;
            this.MenuItemEnableSelectedCsvExportPrompt.IsChecked = !this.state.SuppressSelectedCsvExportPrompt;
        }

        private void MenuItemEnableSelectedDarkThresholdPrompt_Click(object sender, RoutedEventArgs e)
        {
            this.state.SuppressSelectedDarkThresholdPrompt = !this.state.SuppressSelectedDarkThresholdPrompt;
            this.MenuItemEnableSelectedDarkThresholdPrompt.IsChecked = !this.state.SuppressSelectedDarkThresholdPrompt;
        }

        private void MenuItemEnableSelectedDateTimeFixedCorrectionPrompt_Click(object sender, RoutedEventArgs e)
        {
            this.state.SuppressSelectedDateTimeFixedCorrectionPrompt = !this.state.SuppressSelectedDateTimeFixedCorrectionPrompt;
            this.MenuItemEnableSelectedDateTimeFixedCorrectionPrompt.IsChecked = !this.state.SuppressSelectedDateTimeFixedCorrectionPrompt;
        }

        private void MenuItemEnableSelectedDateTimeLinearCorrectionPrompt_Click(object sender, RoutedEventArgs e)
        {
            this.state.SuppressSelectedDateTimeLinearCorrectionPrompt = !this.state.SuppressSelectedDateTimeLinearCorrectionPrompt;
            this.MenuItemEnableSelectedDateTimeLinearCorrectionPrompt.IsChecked = !this.state.SuppressSelectedDateTimeLinearCorrectionPrompt;
        }

        private void MenuItemEnableSelectedDaylightSavingsCorrectionPrompt_Click(object sender, RoutedEventArgs e)
        {
            this.state.SuppressSelectedDaylightSavingsCorrectionPrompt = !this.state.SuppressSelectedDaylightSavingsCorrectionPrompt;
            this.MenuItemEnableSelectedDaylightSavingsCorrectionPrompt.IsChecked = !this.state.SuppressSelectedDaylightSavingsCorrectionPrompt;
        }

        private void MenuItemEnableSelectedPopulateFieldFromMetadataPrompt_Click(object sender, RoutedEventArgs e)
        {
            this.state.SuppressSelectedPopulateFieldFromMetadataPrompt = !this.state.SuppressSelectedPopulateFieldFromMetadataPrompt;
            this.MenuItemEnableSelectedPopulateFieldFromMetadataPrompt.IsChecked = !this.state.SuppressSelectedPopulateFieldFromMetadataPrompt;
        }

        private void MenuItemEnableSelectedRereadDatesFromFilesPrompt_Click(object sender, RoutedEventArgs e)
        {
            this.state.SuppressSelectedRereadDatesFromFilesPrompt = !this.state.SuppressSelectedRereadDatesFromFilesPrompt;
            this.MenuItemEnableSelectedRereadDatesFromFilesPrompt.IsChecked = !this.state.SuppressSelectedRereadDatesFromFilesPrompt;
        }

        private void MenuItemEnableSelectedSetTimeZonePrompt_Click(object sender, RoutedEventArgs e)
        {
            this.state.SuppressSelectedSetTimeZonePrompt = !this.state.SuppressSelectedSetTimeZonePrompt;
            this.MenuItemEnableSelectedSetTimeZonePrompt.IsChecked = !this.state.SuppressSelectedSetTimeZonePrompt;
        }

        private void MenuItemRereadDatesfromImages_Click(object sender, RoutedEventArgs e)
        {
            if (this.MaybePromptToApplyOperationIfPartialSelection(this.state.SuppressSelectedRereadDatesFromFilesPrompt,
                                                               "'Reread dates from files...'",
                                                               (bool optOut) =>
                                                               {
                                                                   this.state.SuppressSelectedRereadDatesFromFilesPrompt = optOut;
                                                                   this.MenuItemEnableSelectedRereadDatesFromFilesPrompt.IsChecked = !optOut;
                                                               }))
            {
                DateRereadFromFiles rereadDates = new DateRereadFromFiles(this.dataHandler.ImageDatabase, this);
                this.ShowBulkImageEditDialog(rereadDates);
            }
        }

        /// <summary> Toggle the audio feedback on and off</summary>
        private void MenuItemAudioFeedback_Click(object sender, RoutedEventArgs e)
        {
            // We don't have to do anything here...
            this.state.AudioFeedback = !this.state.AudioFeedback;
            this.MenuItemAudioFeedback.IsChecked = this.state.AudioFeedback;
        }

        private void MenuItemSelect_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            Dictionary<ImageSelection, int> counts = this.dataHandler.ImageDatabase.GetImageCountsByQuality();

            this.MenuItemSelectLightImages.IsEnabled = counts[ImageSelection.Ok] > 0;
            this.MenuItemSelectDarkImages.IsEnabled = counts[ImageSelection.Dark] > 0;
            this.MenuItemSelectCorruptedImages.IsEnabled = counts[ImageSelection.CorruptFile] > 0;
            this.MenuItemSelectFilesNoLongerAvailable.IsEnabled = counts[ImageSelection.FileNoLongerAvailable] > 0;
            this.MenuItemSelectImagesMarkedForDeletion.IsEnabled = this.dataHandler.ImageDatabase.GetImageCount(ImageSelection.MarkedForDeletion) > 0;
        }

        private void MenuItemZoomIn_Click(object sender, RoutedEventArgs e)
        {
            lock (this.MarkableCanvas.ImageToDisplay)
            {
                Point location = Mouse.GetPosition(this.MarkableCanvas.ImageToDisplay);
                if (location.X > this.MarkableCanvas.ImageToDisplay.ActualWidth || location.Y > this.MarkableCanvas.ImageToDisplay.ActualHeight)
                {
                    return; // Ignore points if mouse is off the image
                }
                this.MarkableCanvas.ScaleImage(location, true); // Zooming in if delta is positive, else zooming out
            }
        }

        private void MenuItemZoomOut_Click(object sender, RoutedEventArgs e)
        {
            lock (this.MarkableCanvas.ImageToDisplay)
            {
                Point location = Mouse.GetPosition(this.MarkableCanvas.ImageToDisplay);
                this.MarkableCanvas.ScaleImage(location, false); // Zooming in if delta is positive, else zooming out
            }
        }

        /// <summary>Navigate to the next image in this image set</summary>
        private void MenuItemViewNextImage_Click(object sender, RoutedEventArgs e)
        {
            this.TryShowImageWithoutSliderCallback(true, ModifierKeys.None);
        }

        /// <summary>Navigate to the previous image in this image set</summary>
        private void MenuItemViewPreviousImage_Click(object sender, RoutedEventArgs e)
        {
            this.TryShowImageWithoutSliderCallback(false, ModifierKeys.None);
        }

        /// <summary>Cycle through the image differences</summary>
        private void MenuItemViewDifferencesCycleThrough_Click(object sender, RoutedEventArgs e)
        {
            this.ViewPreviousOrNextDifference();
        }

        /// <summary>View the combined image differences</summary>
        private void MenuItemViewDifferencesCombined_Click(object sender, RoutedEventArgs e)
        {
            this.ViewCombinedDifference();
        }

        /// <summary>Show a dialog box telling the user how many images were loaded, etc.</summary>
        public void MenuItemImageCounts_Click(object sender, RoutedEventArgs e)
        {
            this.ShowImageCountsDialog(false);
        }

        // Display the Video Player Window
        private void MenuItemVideoViewer_Click(object sender, RoutedEventArgs e)
        {
            Uri uri = new System.Uri(Path.Combine(this.dataHandler.ImageDatabase.FolderPath, this.dataHandler.ImageCache.Current.FileName));

            // Check to see if we need to create the Video Player dialog window
            if (this.videoPlayer == null || this.videoPlayer.IsLoaded != true)
            {
                this.videoPlayer = new WindowVideoPlayer(this, this.FolderPath);
                this.videoPlayer.Owner = this;
            }

            // Initialize the video player to display the file held by the current row
            this.SetVideoPlayerToCurrentRow();

            // If the video player is already loaded, ensure that it is not minimized
            if (this.videoPlayer.IsLoaded)
            {
                this.videoPlayer.WindowState = WindowState.Normal;
            }
            else
            {
                this.videoPlayer.Show();
            }
        }

        // Set the video player to the current row, where it will try to display it (or provide appropriate feedback)
        private void SetVideoPlayerToCurrentRow()
        {
            if (this.videoPlayer == null)
            {
                return;
            }
            this.videoPlayer.CurrentRow = this.dataHandler.ImageCache.Current;
        }

        /// <summary>Get the selection and update the view</summary>
        private void MenuItemSelect_Click(object sender, RoutedEventArgs e)
        {
            // get selection 
            MenuItem item = (MenuItem)sender;
            ImageSelection selection;
            if (item == this.MenuItemSelectAllImages)
            {
                selection = ImageSelection.All;
            }
            else if (item == this.MenuItemSelectLightImages)
            {
                selection = ImageSelection.Ok;
            }
            else if (item == this.MenuItemSelectCorruptedImages)
            {
                selection = ImageSelection.CorruptFile;
            }
            else if (item == this.MenuItemSelectDarkImages)
            {
                selection = ImageSelection.Dark;
            }
            else if (item == this.MenuItemSelectFilesNoLongerAvailable)
            {
                selection = ImageSelection.FileNoLongerAvailable;
            }
            else if (item == this.MenuItemSelectImagesMarkedForDeletion)
            {
                selection = ImageSelection.MarkedForDeletion;
            }
            else
            {
                selection = ImageSelection.All;   // Just in case
            }

            // Treat the checked status as a radio button i.e., toggle their states so only the clicked menu item is checked.
            this.SelectDataTableImagesAndShowImage(Constants.DefaultImageRowIndex, selection);  // Go to the first result (i.e., index 0) in the selection
        }

        private void MenuItemSelectCustomSelection_Click(object sender, RoutedEventArgs e)
        {
            // the first time the custom selection dialog is launched update the DateTime and UtcOffset search terms to the time of the current image
            SearchTerm firstDateTimeSearchTerm = this.dataHandler.ImageDatabase.CustomSelection.SearchTerms.First(searchTerm => searchTerm.DataLabel == Constants.DatabaseColumn.DateTime);
            if (firstDateTimeSearchTerm.GetDateTime() == Constants.ControlDefault.DateTimeValue.DateTime)
            {
                DateTimeOffset defaultDate = this.dataHandler.ImageCache.Current.GetDateTime();
                this.dataHandler.ImageDatabase.CustomSelection.SetDateTimesAndOffset(defaultDate);
            }

            // show the dialog and process the resuls
            CustomViewSelection customSelection = new CustomViewSelection(this.dataHandler.ImageDatabase, this);
            bool? changeToCustomSelection = customSelection.ShowDialog();
            if (changeToCustomSelection == true)
            {
                this.SelectDataTableImagesAndShowImage(Constants.DefaultImageRowIndex, ImageSelection.Custom);
            }
        }

        /// <summary>Display a message describing the version, etc.</summary> 
        private void MenuItemAbout_Click(object sender, RoutedEventArgs e)
        {
            About about = new About(this);
            about.ShowDialog();
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

        // Say the given text
        public void Speak(string text)
        {
            if (this.state.AudioFeedback)
            {
                this.speechSynthesizer.SpeakAsyncCancelAll();
                this.speechSynthesizer.SpeakAsync(text);
            }
        }

        // If we are not showing all images, then warn the user and make sure they want to continue.
        private bool MaybePromptToApplyOperationIfPartialSelection(bool userOptedOutOfMessage, string operationDescription, Action<bool> persistOptOut)
        {
            // if showing all images then no need for showing the warning message
            if (userOptedOutOfMessage || this.dataHandler.ImageDatabase.ImageSet.ImageSelection == ImageSelection.All)
            {
                return true;
            }

            string title = "Apply " + operationDescription + " to this selection?";
            MessageBox messageBox = new MessageBox(title, this, MessageBoxButton.OKCancel);

            messageBox.Message.What = operationDescription + " will be applied only to the subset of images shown by the " + this.dataHandler.ImageDatabase.ImageSet.ImageSelection + " selection." + Environment.NewLine;
            messageBox.Message.What += "Is this what you want?";

            messageBox.Message.Reason = "You have the following selection on: " + this.dataHandler.ImageDatabase.ImageSet.ImageSelection + "." + Environment.NewLine;
            messageBox.Message.Reason += "Only data for those images available in this " + this.dataHandler.ImageDatabase.ImageSet.ImageSelection + " selection will be affected" + Environment.NewLine;
            messageBox.Message.Reason += "Data for images not shown in this " + this.dataHandler.ImageDatabase.ImageSet.ImageSelection + " selection will be unaffected." + Environment.NewLine;

            messageBox.Message.Solution = "Select " + Environment.NewLine;
            messageBox.Message.Solution += "\u2022 'Ok' for Carnassial to continue to " + operationDescription + Environment.NewLine;
            messageBox.Message.Solution += "\u2022 'Cancel' to abort";

            messageBox.Message.Hint = "This is not an error." + Environment.NewLine;
            messageBox.Message.Hint += "\u2022 We are asking just in case you forgot you had the " + this.dataHandler.ImageDatabase.ImageSet.ImageSelection + " on. " + Environment.NewLine;
            messageBox.Message.Hint += "\u2022 You can use the 'Select' menu to change to other views (including viewing All files)" + Environment.NewLine;
            messageBox.Message.Hint += "If you check don't show this message this dialog can be turned back on via the Options menu.";

            messageBox.Message.Icon = MessageBoxImage.Question;
            messageBox.DontShowAgain.Visibility = Visibility.Visible;

            bool proceedWithOperation = (bool)messageBox.ShowDialog();
            if (proceedWithOperation && messageBox.DontShowAgain.IsChecked.HasValue)
            {
                persistOptOut(messageBox.DontShowAgain.IsChecked.Value);
            }
            return proceedWithOperation;
        }

        private bool TryShowImageWithoutSliderCallback(bool forward, ModifierKeys modifiers)
        {
            // Check to see if there are any images to show, 
            if (this.dataHandler.ImageDatabase.CurrentlySelectedImageCount <= 0)
            {
                return false;
            }
            // determine how far to move and in which direction
            int increment = forward ? 1 : -1;
            if ((modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                increment *= 5;
            }
            if ((modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                increment *= 10;
            }

            int desiredRow = this.dataHandler.ImageCache.CurrentRow + increment;

            // Set the desiredRow to either the maximum or minimum row if it exceeds the bounds,
            if (desiredRow >= this.dataHandler.ImageDatabase.CurrentlySelectedImageCount)
            {
                desiredRow = this.dataHandler.ImageDatabase.CurrentlySelectedImageCount - 1;
            }
            else if (desiredRow < 0)
            {
                desiredRow = 0;
            }

            // If the desired row is the same as the current row, the image us already being displayed
            if (desiredRow != this.dataHandler.ImageCache.CurrentRow)
            {
                // Move to the desired row
                this.ImageNavigatorSlider_EnableOrDisableValueChangedCallback(false);
                this.ShowImage(desiredRow);
                this.ImageNavigatorSlider_EnableOrDisableValueChangedCallback(true);
            }
            return true;
        }

        // Bookmark (Save) the current pan / zoom level of the image
        private void MenuItem_BookmarkSavePanZoom(object sender, RoutedEventArgs e)
        {
            this.MarkableCanvas.BookmarkSaveZoomPan();
        }

        // Restore the zoom level / pan coordinates of the bookmark
        private void MenuItem_BookmarkSetPanZoom(object sender, RoutedEventArgs e)
        {
            this.MarkableCanvas.BookmarkSetZoomPan();
        }

        // Restore the zoomed out / pan coordinates 
        private void MenuItem_BookmarkDefaultPanZoom(object sender, RoutedEventArgs e)
        {
            this.MarkableCanvas.BookmarkZoomOutAllTheWay();
        }

        private void RefreshDataGrid()
        {
            if (this.DataGridPane.IsActive)
            {
                this.DataGrid.Items.Refresh();
            }
        }

        // A class that tracks our progress as we load the images
        internal class ProgressState
        {
            public BitmapSource Bmap { get; set; }
            public string Message { get; set; }

            public ProgressState()
            {
                this.Bmap = null;
                this.Message = String.Empty;
            }
        }

        private void HelpDocument_Drop(object sender, DragEventArgs dropEvent)
        {
            string templateDatabaseFilePath;
            if (Utilities.IsSingleTemplateFileDrag(dropEvent, out templateDatabaseFilePath))
            {
                BackgroundWorker backgroundWorker;
                if (this.TryOpenTemplateAndBeginLoadImagesAsync(templateDatabaseFilePath, out backgroundWorker) == false)
                {
                    this.state.MostRecentImageSets.TryRemove(templateDatabaseFilePath);
                    this.MenuItemRecentImageSets_Refresh();
                }
                dropEvent.Handled = true;
            }
        }

        private void HelpDocument_PreviewDrag(object sender, DragEventArgs dragEvent)
        {
            Utilities.OnHelpDocumentPreviewDrag(dragEvent);
        }
    }
}
