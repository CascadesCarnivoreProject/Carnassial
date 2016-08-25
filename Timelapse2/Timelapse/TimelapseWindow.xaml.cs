using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Speech.Synthesis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Timelapse.Controls;
using Timelapse.Database;
using Timelapse.Dialog;
using Timelapse.Images;
using Timelapse.Util;
using MessageBox = Timelapse.Dialog.MessageBox;

namespace Timelapse
{
    /// <summary>
    /// main window for Timelapse
    /// </summary>
    public partial class TimelapseWindow : Window, IDisposable
    {
        // Handles to the controls window and to the controls
        private WindowControl controlWindow;
        private List<MetaTagCounter> counterCoordinates = null;

        private DataEntryControls dataEntryControls;
        private DataEntryHandler dataHandler;
        private bool disposed;

        private string mostRecentImageAddFolderPath;
        private WindowHelp overviewWindow; // Create the help window. 
        private MarkableImageCanvas markableCanvas;

        // Status information concerning the state of the UI
        private TimelapseState state;

        // Timer for periodically updating images as the ImageNavigator slider is being used
        private DispatcherTimer timerImageNavigator = new DispatcherTimer();

        // Speech feedback
        private SpeechSynthesizer speechSynthesizer = new SpeechSynthesizer();

        // The database that holds the template
        private TemplateDatabase template;

        // Non-modal dialogs
        private DataView dlgDataView;         // The view of the current database contents
        private WindowVideoPlayer dlgVideoPlayer;  // The video player 

        public TimelapseWindow()
        {
            this.InitializeComponent();
            Utilities.TryFitWindowInWorkingArea(this);

            // Abort if some of the required dependencies are missing
            if (Dependencies.AreRequiredBinariesPresent(Constants.ApplicationName, Assembly.GetExecutingAssembly()) == false)
            {
                Dependencies.ShowMissingBinariesDialog(Constants.ApplicationName);
                Application.Current.Shutdown();
            }

            this.markableCanvas = new MarkableImageCanvas();
            this.markableCanvas.HorizontalAlignment = HorizontalAlignment.Stretch;
            this.markableCanvas.PreviewMouseDown += new MouseButtonEventHandler(this.MarkableCanvas_PreviewMouseDown);
            this.markableCanvas.MouseEnter += new MouseEventHandler(this.MarkableCanvas_MouseEnter);
            this.markableCanvas.RaiseMetaTagEvent += new EventHandler<MetaTagEventArgs>(this.MarkableCanvas_RaiseMetaTagEvent);
            this.mainUI.Children.Add(this.markableCanvas);

            // Callbacks so the controls will highlight if they are copyable when one enters the copy button
            this.buttonCopy.MouseEnter += this.ButtonCopy_MouseEnter;
            this.buttonCopy.MouseLeave += this.ButtonCopy_MouseLeave;

            // Create data controls, including reparenting the copy button from the main window into the my control window.
            this.dataEntryControls = new DataEntryControls();
            this.ControlGrid.Children.Remove(this.buttonCopy);
            this.dataEntryControls.AddButton(this.buttonCopy);

            this.state = new TimelapseState();

            // Recall user's state from prior sessions
            this.state.ReadFromRegistry();

            this.MenuItemAudioFeedback.IsChecked = this.state.AudioFeedback;
            this.MenuItemControlsInSeparateWindow.IsChecked = this.state.ControlsInSeparateWindow;
            this.MenuItemEnableCsvExportDialog.IsChecked = !this.state.SuppressCsvExportDialog;
            this.MenuItemEnableCsvImportPrompt.IsChecked = !this.state.SuppressCsvImportPrompt;
            this.MenuItemEnableFilteredAmbiguousDatesPrompt.IsChecked = !this.state.SuppressFilteredAmbiguousDatesPrompt;
            this.MenuItemEnableFilteredCsvExportPrompt.IsChecked = !this.state.SuppressFilteredCsvExportPrompt;
            this.MenuItemEnableFilteredDarkThresholdPrompt.IsChecked = !this.state.SuppressFilteredDarkThresholdPrompt;
            this.MenuItemEnableFilteredDateTimeFixedCorrectionPrompt.IsChecked = !this.state.SuppressFilteredDateTimeFixedCorrectionPrompt;
            this.MenuItemEnableFilteredDateTimeLinearCorrectionPrompt.IsChecked = !this.state.SuppressFilteredDateTimeLinearCorrectionPrompt;
            this.MenuItemEnableFilteredDaylightSavingsCorrectionPrompt.IsChecked = !this.state.SuppressFilteredDaylightSavingsCorrectionPrompt;
            this.MenuItemEnableFilteredPopulateFieldFromMetadataPrompt.IsChecked = !this.state.SuppressFilteredPopulateFieldFromMetadataPrompt;
            this.MenuItemEnableFilteredRereadDatesFromFilesPrompt.IsChecked = !this.state.SuppressFilteredRereadDatesFromFilesPrompt;

            // Timer callback so the image will update to the current slider position when the user pauses dragging the image slider 
            this.timerImageNavigator.Interval = this.state.Throttles.DesiredIntervalBetweenRenders;
            this.timerImageNavigator.Tick += this.TimerImageNavigator_Tick;

            // populate the most recent databases list
            this.MenuItemRecentImageSets_Refresh();
        }

        private string FolderPath
        {
            get { return this.dataHandler.ImageDatabase.FolderPath; }
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
                if (this.template != null)
                {
                    this.template.Dispose();
                }
            }

            this.disposed = true;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            VersionClient updater = new VersionClient(Constants.ApplicationName, Constants.LatestVersionAddress);
            updater.TryGetAndParseVersion(false);
            // FOR MY DEBUGGING ONLY: Uncomment this to Start THE SYSTEM WITH THE LOAD MENU ITEM SELECTED loadImagesFromSources();  //OPENS THE MENU AUTOMATICALLY
        }

        // On exiting, save various attributes so we can use recover them later
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if ((this.dataHandler != null) &&
                (this.dataHandler.ImageDatabase != null) &&
                (this.dataHandler.ImageDatabase.CurrentlySelectedImageCount > 0))
            {
                // save image set properties to the database
                if (this.dataHandler.ImageDatabase.ImageSet.ImageFilter == ImageFilter.Custom)
                {
                    // don't save custom filters, revert to All 
                    this.dataHandler.ImageDatabase.ImageSet.ImageFilter = ImageFilter.All;
                }

                if (this.dataHandler.ImageCache != null)
                {
                    this.dataHandler.ImageDatabase.ImageSet.ImageRowIndex = this.dataHandler.ImageCache.CurrentRow;
                }

                if (this.markableCanvas != null)
                {
                    this.dataHandler.ImageDatabase.ImageSet.MagnifierEnabled = this.markableCanvas.IsMagnifyingGlassVisible;
                }

                this.dataHandler.ImageDatabase.SyncImageSetToDatabase();
            }

            // persist user specific state to the registry
            this.state.WriteToRegistry();

            // Close the various non-modal windows if they are opened
            if (this.controlWindow != null)
            {
                this.controlWindow.Close();
            }
            if (this.dlgDataView != null)
            {
                this.dlgDataView.Close();
            }
            if (this.dlgVideoPlayer != null)
            {
                this.dlgVideoPlayer.Close();
            }
        }

        #region Image Loading
        private bool TryGetTemplatePath(out string templateDatabasePath)
        {
            // prompt user to select a template
            // default the template selection dialog to the most recently opened database
            string defaultTemplateDatabasePath;
            this.state.MostRecentImageSets.TryGetMostRecent(out defaultTemplateDatabasePath);
            if (Utilities.TryGetFileFromUser("Select a TimelapseTemplate.tdb file, which should be located in the root folder containing your images and videos",
                                             defaultTemplateDatabasePath,
                                             String.Format("Template files ({0})|*{0}", Constants.File.TemplateDatabaseFileExtension),
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
            if (!TemplateDatabase.TryCreateOrOpen(templateDatabasePath, out this.template))
            {
                // notify the user the template couldn't be loaded rather than silently doing nothing
                MessageBox messageBox = new MessageBox("Timelapse could not load the template.", this);
                messageBox.Message.Problem = "Timelapse could not load the Template File:" + Environment.NewLine;
                messageBox.Message.Problem += "\u2022 " + templateDatabasePath;
                messageBox.Message.Reason = "The template may be corrupted or somehow otherwise invalid. ";
                messageBox.Message.Solution = "You may have to recreate the template, or use another copy of it (if you have one).";
                messageBox.Message.Result = "Timelapse won't do anything. You can try to select another template file.";
                messageBox.Message.Hint = "See if you can examine the template file in the Timelapse Template Editor.";
                messageBox.Message.Hint += "If you can't, there is likley something wrong with it and you will have to recreate it.";
                messageBox.Message.Icon = MessageBoxImage.Error;
                messageBox.ShowDialog();
                return false;
            }

            // Try to get the image database file path (imageDatabaseFilePath)
            // If its a new image database file, importImages will be true (meaning we should later ask the user to try to import some images)
            string imageDatabaseFilePath;
            bool importImages;
            if (this.TrySelectDatabaseFile(templateDatabasePath, out imageDatabaseFilePath, out importImages) == false)
            {
                // No database file was selected
                return false;
            }

            // We now have a template and an image database.
            // Before loading from an existing image database, ensure that the template in the template database matches the template stored in
            // the image database
            ImageDatabase imageDatabase = ImageDatabase.CreateOrOpen(imageDatabaseFilePath, this.template);
            if (imageDatabase.TemplateSynchronizationIssues.Count > 0)
            {
                TemplatesDontMatch templateMismatchDialog = new TemplatesDontMatch(imageDatabase.TemplateSynchronizationIssues, this);
                templateMismatchDialog.Owner = this;
                bool? result = templateMismatchDialog.ShowDialog();
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
            this.dataEntryControls.CreateControls(imageDatabase, this.dataHandler);
            this.SetUserInterfaceCallbacks();
            this.MenuItemControlsInSeparateWindow_Click(this.MenuItemControlsInSeparateWindow, null);

            this.state.MostRecentImageSets.SetMostRecent(templateDatabasePath);
            this.MenuItemRecentImageSets_Refresh();

            // If this is a new image database, try to load images (if any) from the folder...  
            if (importImages)
            {
                this.TryBeginImageFolderLoadAsync(this.FolderPath, out backgroundWorker);
            }
            else
            { 
                this.OnImageLoadingComplete(false);
            }

            return true;
        }

        // out parameters can't be used in anonymous methods, so a separate pointer to backgroundWorker is required for return to the caller
        private bool TryBeginImageFolderLoadAsync(string imageFolderPath, out BackgroundWorker externallyVisibleWorker)
        {
            DirectoryInfo imageFolder = new DirectoryInfo(imageFolderPath);
            List<FileInfo> imageFiles = new List<FileInfo>();
            foreach (string extension in new List<string>() { Constants.File.AviFileExtension, Constants.File.Mp4FileExtension, Constants.File.JpgFileExtension })
            {
                imageFiles.AddRange(imageFolder.GetFiles("*" + extension));
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

                string folderPath;
                if (this.ShowFolderSelectionDialog(out folderPath))
                {
                    return this.TryBeginImageFolderLoadAsync(folderPath, out externallyVisibleWorker);
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

            bool unambiguousDayMonthOrder = true;
            ProgressState progressState = new ProgressState();
            backgroundWorker.DoWork += (ow, ea) =>
            {
                // this runs on the background thread; its written as an anonymous delegate
                // We need to invoke this to allow updates on the UI
                this.Dispatcher.Invoke(new Action(() =>
                {
                    // First, change the UI
                    this.HelpDocument.Visibility = Visibility.Collapsed;
                    this.Feedback(null, 0, "Examining images...");
                }));

                // First pass: Examine images to extract their basic properties and build a list of images not already in the database
                List<ImageRow> imagesToInsert = new List<ImageRow>();
                TimeZoneInfo imageSetTimeZone = this.dataHandler.ImageDatabase.ImageSet.GetTimeZone();
                DateTime previousImageRender = DateTime.UtcNow - this.state.Throttles.DesiredIntervalBetweenRenders;
                for (int image = 0; image < imageFiles.Count; image++)
                {
                    FileInfo imageFile = imageFiles[image];
                    ImageRow imageProperties;
                    if (this.dataHandler.ImageDatabase.GetOrCreateImage(imageFile, imageSetTimeZone, out imageProperties))
                    {
                        // the database already has an entry for this image so skip it
                        // if needed, a separate list of images to update could be generated
                        continue;
                    }

                    BitmapSource bitmapSource = null;
                    try
                    {
                        // Create the bitmap and determine its ImageQuality
                        // avoid ImageProperties.LoadImage() here as the create exception needs to surface to set the image quality to corrupt
                        // framework bug: WriteableBitmap.Metadata returns null rather than metatada offered by the underlying BitmapFrame, so 
                        // retain the frame and pass its metadata to TryUseImageTaken().
                        bitmapSource = imageProperties.LoadBitmap(this.FolderPath, ImageDisplayIntent.TransientLoading);
 
                        // Set the ImageQuality to corrupt if the returned bitmap is the corrupt image, otherwise set it to its Ok/Dark setting
                        if (bitmapSource == Constants.Images.Corrupt)
                        {
                            imageProperties.ImageQuality = ImageFilter.Corrupted;
                        }
                        else
                        { 
                            imageProperties.ImageQuality = bitmapSource.AsWriteable().GetImageQuality(this.state.DarkPixelThreshold, this.state.DarkPixelRatioThreshold);
                        }

                        // see if the date can be updated from the metadata
                        DateTimeAdjustment imageTimeAdjustment = imageProperties.TryUseImageTaken((BitmapMetadata)bitmapSource.Metadata, imageSetTimeZone);
                        if (imageTimeAdjustment == DateTimeAdjustment.MetadataDateAndTimeUsed ||
                            imageTimeAdjustment == DateTimeAdjustment.MetadataDateUsed)
                        {
                            DateTimeOffset imageTaken;
                            bool result = imageProperties.TryGetDateTime(imageSetTimeZone, out imageTaken);
                            if (result == true)
                            {
                                if (imageTaken.Day <= Constants.Time.MonthsInYear)
                                {
                                    unambiguousDayMonthOrder = false;
                                }
                            }
                            // No else - thus if the date can't be read, the day/month order is assumed to be unambiguous
                            // Thus invalid dates must be handled elsewhere
                        }
                    }
                    catch (Exception exception)
                    {
                        Debug.Fail(String.Format("Load of {0} failed as it's likely corrupted.", imageProperties.FileName), exception.ToString());
                        bitmapSource = Constants.Images.Corrupt;
                        imageProperties.ImageQuality = ImageFilter.Corrupted;
                    }

                    imagesToInsert.Add(imageProperties);

                    DateTime utcNow = DateTime.UtcNow;
                    if (utcNow - previousImageRender > this.state.Throttles.DesiredIntervalBetweenRenders)
                    {
                        progressState.Bmap = bitmapSource;
                        progressState.Message = String.Format("{0}/{1}: Examining {2}", image, imageFiles.Count, imageProperties.FileName);
                        int percentProgress = (int)(100.0 * image / (double)imageFiles.Count);
                        backgroundWorker.ReportProgress(percentProgress, progressState);
                        previousImageRender = utcNow;
                    }
                    else
                    {
                        progressState.Bmap = null;
                    }
                }

                // Second pass: Update database
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
                ProgressState progstate = (ProgressState)ea.UserState;
                this.Feedback(progressState.Bmap, ea.ProgressPercentage, progressState.Message);
                this.FeedbackControl.Visibility = Visibility.Visible;
                progressState.Bmap = null;
            };
            backgroundWorker.RunWorkerCompleted += (o, ea) =>
            {
                // this.dbData.GetImagesAll(); // Now load up the data table
                // Get rid of the feedback panel, and show the main interface
                this.FeedbackControl.Visibility = Visibility.Collapsed;
                this.FeedbackControl.ShowImage = null;

                this.markableCanvas.Visibility = Visibility.Visible;

                // warn the user if there are any ambiguous dates in terms of day/month or month/day order
                if (unambiguousDayMonthOrder == false && this.state.SuppressAmbiguousDatesDialog == false)
                {
                    MessageBox messageBox = new MessageBox("Timelapse was unsure about the month / day order of your file(s) dates.", this);
                    messageBox.Message.Problem = "Timelapse is extracting the dates from your files. However, File date formats can be ambiguous: is 2016/03/05 March 5 or May 3?";
                    messageBox.Message.Problem += "Timelapse tries its best by using the date format specified in the Windows Control Panel";
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

                // If we want to import old data from the ImageData.xml file, we can do it here...
                // Check to see if there is an ImageData.xml file in here. If there is, ask the user
                // if we want to load the data from that...
                if (File.Exists(Path.Combine(this.FolderPath, Constants.File.XmlDataFileName)))
                {
                    ImportImageSetXmlFile importLegacyXmlDialog = new ImportImageSetXmlFile();
                    importLegacyXmlDialog.Owner = this;
                    bool? dialogResult = importLegacyXmlDialog.ShowDialog();
                    if (dialogResult == true)
                    {
                        ImageDataXml.Read(Path.Combine(this.FolderPath, Constants.File.XmlDataFileName), this.dataHandler.ImageDatabase);
                        this.SelectDataTableImagesAndShowImage(this.dataHandler.ImageDatabase.ImageSet.ImageRowIndex, this.dataHandler.ImageDatabase.ImageSet.ImageFilter); // to regenerate the controls and markers for this image
                    }
                }
            };

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
            this.markableCanvas.IsMagnifyingGlassVisible = this.dataHandler.ImageDatabase.ImageSet.MagnifierEnabled;

            // Also adjust the visibility of the various other UI components.
            this.buttonCopy.Visibility = Visibility.Visible;
            this.controlsTray.Visibility = Visibility.Visible;
            this.DockPanelNavigator.Visibility = Visibility.Visible;
            this.HelpDocument.Visibility = Visibility.Collapsed;

            // Show the image, hide the load button, and make the feedback panels visible
            this.ImageNavigatorSlider_EnableOrDisableValueChangedCallback(false);
            this.markableCanvas.Focus(); // We start with this having the focus so it can interpret keyboard shortcuts if needed. 

            // if this is completion of an existing .ddb open set the current filter and the image index to the ones from the previous session with the image set
            // also if this is completion of import to a new .ddb
            int imageRow = this.dataHandler.ImageDatabase.ImageSet.ImageRowIndex;
            ImageFilter imageFilter = this.dataHandler.ImageDatabase.ImageSet.ImageFilter;
            if (imagesJustImported && this.dataHandler.ImageCache.CurrentRow != Constants.Database.InvalidRow)
            {
                // if this is completion of an add to an existing image set stay on the image and, ideally, filter selected before the import
                // TODO: add an API to read the current filter setting
                imageRow = this.dataHandler.ImageCache.CurrentRow;
            }
            this.SelectDataTableImagesAndShowImage(imageRow, imageFilter);

            // match UX availability to image availability
            this.EnableOrDisableMenusAndControls();

            if (FileBackup.TryCreateBackups(this.FolderPath, this.dataHandler.ImageDatabase.FileName))
            {
                this.statusBar.SetMessage("Backup of data file made.");
            }
            else
            {
                this.statusBar.SetMessage("No file backups were made.");
            }
        }

        private void EnableOrDisableMenusAndControls()
        {
            bool imagesExist = this.dataHandler.ImageDatabase.CurrentlySelectedImageCount > 0;

            // Depending upon whether images exist in the data set,
            // enable / disable menus and menu items as needed
            // file menu
            this.MenuItemAddImagesToImageSet.IsEnabled = true;
            this.MenuItemLoadImages.IsEnabled = false;
            this.MenuItemRecentImageSets.IsEnabled = false;
            this.MenuItemExportThisImage.IsEnabled = imagesExist;
            this.MenuItemExportAsCsvAndPreview.IsEnabled = imagesExist;
            this.MenuItemExportAsCsv.IsEnabled = imagesExist;
            this.MenuItemImportFromCsv.IsEnabled = imagesExist;
            this.MenuItemRenameImageDatabaseFile.IsEnabled = imagesExist;
            // edit menu
            this.MenuItemEdit.IsEnabled = imagesExist;
            this.MenuItemDeleteImage.IsEnabled = imagesExist;
            // view menu
            this.MenuItemView.IsEnabled = imagesExist;
            // filter menu
            this.MenuItemFilter.IsEnabled = imagesExist;
            // options menu
            // always enable at top level when an image set exists so that image set advanced options are accessible
            this.MenuItemOptions.IsEnabled = true;
            this.MenuItemControlsInSeparateWindow.IsEnabled = imagesExist;
            this.MenuItemAudioFeedback.IsEnabled = imagesExist;
            this.MenuItemMagnifyingGlass.IsEnabled = imagesExist;
            this.MenuItemDisplayMagnifier.IsChecked = this.dataHandler.ImageDatabase.ImageSet.MagnifierEnabled;
            this.MenuItemImageCounts.IsEnabled = imagesExist;
            this.MenuItemViewFilteredDatabaseContents.IsEnabled = imagesExist;
            this.MenuItemDialogsOnOrOff.IsEnabled = imagesExist;
            this.MenuItemAdvancedTimelapseOptions.IsEnabled = imagesExist;

            // Also adjust the enablement of the various other UI components.
            if (this.controlWindow != null)
            {
                this.controlWindow.IsEnabled = imagesExist;
            }

            this.controlsTray.IsEnabled = imagesExist;  // If images don't exist, the user shouldn't be allowed to interact with the control tray
            this.ImageNavigatorSlider.IsEnabled = imagesExist;
            this.markableCanvas.IsEnabled = imagesExist;
            this.markableCanvas.IsMagnifyingGlassVisible = imagesExist && this.dataHandler.ImageDatabase.ImageSet.MagnifierEnabled;

            if (imagesExist == false)
            {
                this.ShowImage(Constants.DefaultImageRowIndex);
                this.statusBar.SetMessage("Image set is empty.");
                this.statusBar.SetCurrentImage(0);
                this.statusBar.SetCount(0);
            }

            this.HelpDocument.Visibility = Visibility.Collapsed;
        }
        #endregion

        private void SelectDataTableImagesAndShowImage(int imageRow, ImageFilter filter)
        {
            this.dataHandler.ImageDatabase.SelectDataTableImages(filter);
            if (this.dataHandler.ImageDatabase.CurrentlySelectedImageCount > 0 || filter == ImageFilter.All)
            {
                // Change the filter to reflect what the user selected. Update the menu state accordingly
                // Set the checked status of the radio button menu items to the filter.
                string status;
                switch (filter)
                {
                    case ImageFilter.All:
                        status = "(all files selected)";
                        break;
                    case ImageFilter.Corrupted:
                        status = "corrupted files";
                        break;
                    case ImageFilter.Custom:
                        status = "files matching your custom filter";
                        break;
                    case ImageFilter.Dark:
                        status = "dark files";
                        break;
                    case ImageFilter.MarkedForDeletion:
                        status = "files marked for deletion";
                        break;
                    case ImageFilter.Missing:
                        status = "missing files";
                        break;
                    case ImageFilter.Ok:
                        status = "light files";
                        break;
                    default:
                        throw new NotSupportedException(String.Format("Unhandled image quality filter {0}.", filter));
                }

                this.statusBar.SetView(status);
                this.MenuItemFilterSetSelected(filter);
                this.RefreshDataViewDialogWindow();  // If its displayed, update the window that shows the filtered view data base
            }
            else
            {
                // These cases are typically reached only when a user deletes all images that fit that filter.  
                // corresponding images
                string status;
                string title;
                string problem;
                string reason = null;
                string hint;
                status = "Resetting filter to All Files";
                title = "Resetting filter to All Files (no files currently match the current filter)";
                if (filter == ImageFilter.Corrupted)
                {
                    problem = "The 'Corrupted filter' was previously selected. Yet no files currently match that filter, so nothing can be shown.";
                    reason = "None of the files have their 'ImageQuality' field set to Corrupted.";
                    hint = "If you have files you think should be marked as 'Corrupted', set their 'ImageQuality' field to 'Corrupted' and then reapply the filter to view only those corrupted files.";
                }
                else if (filter == ImageFilter.Custom)
                {
                    problem = "The 'Custom filter' was previously selected. Yet no files currently match that filter, so nothing can be shown.";
                    reason = "None of the files match the criteria set in the current Custom Filter.";
                    hint = "Try to create another custom filter and then reapply the filter to view only those files matching the filter.";
                }
                else if (filter == ImageFilter.Dark)
                {
                    problem = "The 'Dark filter' was previously selected. Yet no files currently match that filter, so nothing can be shown.";
                    reason = "None of the files have their 'ImageQuality' field set to Dark.";
                    hint = "If you have files you think should be marked as 'Dark', set their 'ImageQuality' field to 'Dark' and then reapply the filter to view only those dark files.";
                }
                else if (filter == ImageFilter.Missing)
                {
                    problem = "The 'Missing filter' was previously selected. Yet no files currently match that filter, so nothing can be shown.";
                    reason = "None of the files have their 'ImageQuality' field set to Missing.";
                    hint = "If you have files you think should be marked as 'Missing', set their 'ImageQuality' field to 'Missing' and then reapply the filter to view only those missing files.";
                }
                else if (filter == ImageFilter.MarkedForDeletion)
                {
                    problem = "The 'Marked for Deletion' filter was previously selected. Yet no files currently match that filter, so nothing can be shown.";
                    reason = "None of the files have their 'Delete?' field checked.";
                    hint = "If you have files you think should be marked for deletion, check their 'Delete?' field and then reapply the filter to view only those files marked for deletion.";
                }
                else if (filter == ImageFilter.Ok)
                {
                    problem = "The 'Ok filter' was previously selected. Yet no files currently match that filter, so nothing can be shown.";
                    reason = "None of the files have their 'ImageQuality' field set to OK.";
                    hint = "If you have files you think should be marked as 'Ok', set their 'ImageQuality' field to 'Ok' and then reapply the filter to view only those Ok files.";
                }
                else
                {
                    throw new NotSupportedException(String.Format("Unhandled filter {0}.", filter));
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
                messageBox.Message.Result = "The 'All Images' filter will be applied, where all images in your image set will be displayed.";
                messageBox.ShowDialog();

                this.SelectDataTableImagesAndShowImage(Constants.DefaultImageRowIndex, ImageFilter.All);
                return;
            }

            // Display the first available image under the new filter
            if (this.dataHandler.ImageDatabase.CurrentlySelectedImageCount > 0)
            {
                this.ShowImage(imageRow);
            }

            // After a filter change, set the slider to represent the index and the count of the current filter
            this.ImageNavigatorSlider_EnableOrDisableValueChangedCallback(false);
            this.ImageNavigatorSlider.Maximum = this.dataHandler.ImageDatabase.CurrentlySelectedImageCount - 1;  // Reset the slider to the size of images in this set
            this.ImageNavigatorSlider.Value = this.dataHandler.ImageCache.CurrentRow;

            // Update the status bar accordingly
            this.statusBar.SetCurrentImage(this.dataHandler.ImageCache.CurrentRow + 1);  // We add 1 because its a 0-based list
            this.statusBar.SetCount(this.dataHandler.ImageDatabase.CurrentlySelectedImageCount);
            this.ImageNavigatorSlider_EnableOrDisableValueChangedCallback(true);
            this.dataHandler.ImageDatabase.ImageSet.ImageFilter = filter;    // Remember the current filter
        }

        #region Control Callbacks
        /// <summary>
        /// Add user interface event handler callbacks for (possibly invisible) controls
        /// </summary>
        private void SetUserInterfaceCallbacks()
        {
            // Add data entry callbacks to all editable controls. When the user changes an image's attribute using a particular control,
            // the callback updates the matching field for that image in the database.
            DataEntryNote date = null;
            DataEntryDateTime dateTime = null;
            DataEntryNote time = null;
            foreach (KeyValuePair<string, DataEntryControl> pair in this.dataEntryControls.ControlsByDataLabel)
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
                    case Constants.DatabaseColumn.Date:
                    case Constants.DatabaseColumn.File:
                    case Constants.DatabaseColumn.Folder:
                    case Constants.DatabaseColumn.RelativePath:
                    case Constants.DatabaseColumn.Time:
                        DataEntryNote note = (DataEntryNote)pair.Value;
                        note.ContentControl.PreviewKeyDown += this.ContentCtl_PreviewKeyDown;
                        if (controlType == Constants.DatabaseColumn.Date)
                        {
                            date = note;
                        }
                        if (controlType == Constants.DatabaseColumn.Time)
                        {
                            time = note;
                        }
                        break;
                    case Constants.DatabaseColumn.DateTime:
                        dateTime = (DataEntryDateTime)pair.Value;
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

            // if needed, link date and time controls to datetime control
            if (dateTime != null && date != null)
            {
                dateTime.DateControl = date;
            }
            if (dateTime != null && time != null)
            {
                dateTime.TimeControl = time;
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
            e.Handled = (this.IsAllValidNumericChars(e.Text) || String.IsNullOrWhiteSpace(e.Text)) ? false : true;
            this.OnPreviewTextInput(e);
        }

        // Helper function for the above
        private bool IsAllValidNumericChars(string str)
        {
            foreach (char c in str)
            {
                if (!Char.IsNumber(c))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Click callback: When the user selects a counter, refresh the markers, which will also readjust the colors and emphasis</summary>
        /// <param name="sender">the event source</param>
        /// <param name="e">event information</param>
        private void CounterControl_Click(object sender, RoutedEventArgs e)
        {
            this.RefreshTheMarkableCanvasListOfMetaTags();
        }

        /// <summary>When the user enters a counter, store the index of the counter and then refresh the markers, which will also readjust the colors and emphasis</summary>
        /// <param name="sender">the event source</param>
        /// <param name="e">event information</param>
        private void CounterControl_MouseEnter(object sender, MouseEventArgs e)
        {
            Panel panel = (Panel)sender;
            this.state.IsMouseOverCounter = panel.Tag is DataEntryCounter;
            this.RefreshTheMarkableCanvasListOfMetaTags();
        }

        // When the user enters a counter, clear the saved index of the counter and then refresh the markers, which will also readjust the colors and emphasis
        private void CounterControl_MouseLeave(object sender, MouseEventArgs e)
        {
            // Recolor the marks
            this.state.IsMouseOverCounter = false;
            this.RefreshTheMarkableCanvasListOfMetaTags();
        }

        private void MoveFocusToNextOrPreviousControlOrImageSlider(bool moveToPreviousControl)
        {
            // identify the currently selected control
            // if focus is currently set to the canvas this defaults to the first or last control, as appropriate
            int currentControl = moveToPreviousControl ? this.dataEntryControls.Controls.Count : -1;

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
                        foreach (DataEntryControl control in this.dataEntryControls.Controls)
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
                 currentControl > -1 && currentControl < this.dataEntryControls.Controls.Count;
                 currentControl = incrementOrDecrement(currentControl))
            {
                DataEntryControl control = this.dataEntryControls.Controls[currentControl];
                if (control.ReadOnly == false)
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
        private void ButtonCopy_MouseEnter(object sender, MouseEventArgs e)
        {
            foreach (KeyValuePair<string, DataEntryControl> pair in this.dataEntryControls.ControlsByDataLabel)
            {
                DataEntryControl control = (DataEntryControl)pair.Value;
                if (control.Copyable)
                {
                    SolidColorBrush brush = new SolidColorBrush(Color.FromArgb(255, (byte)200, (byte)251, (byte)200));
                    control.Container.Background = brush;
                }
            }
        }

        /// <summary>
        ///  When the mouse enters / leaves the copy button, the controls that are copyable will be highlighted. 
        /// </summary>
        private void ButtonCopy_MouseLeave(object sender, MouseEventArgs e)
        {
            foreach (KeyValuePair<string, DataEntryControl> pair in this.dataEntryControls.ControlsByDataLabel)
            {
                DataEntryControl control = (DataEntryControl)pair.Value;
                control.Container.ClearValue(Control.BackgroundProperty);
            }
        }
        #endregion

        #region Differencing
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
                this.markableCanvas.ImageToMagnify.Source = this.dataHandler.ImageCache.GetCurrentImage();
                this.markableCanvas.ImageToDisplay.Source = this.markableCanvas.ImageToMagnify.Source;

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
            this.markableCanvas.ImageToDisplay.Source = this.dataHandler.ImageCache.GetCurrentImage();
            this.statusBar.SetMessage("Viewing differences compared to " + (this.dataHandler.ImageCache.CurrentDifferenceState == ImageDifference.Previous ? "previous" : "next") + " file");
        }

        private void ViewCombinedDifference()
        {
            // If we are in any state other than the unaltered state, go to the unaltered state, otherwise the combined diff state
            this.dataHandler.ImageCache.MoveToNextStateInCombinedDifferenceCycle();
            if (this.dataHandler.ImageCache.CurrentDifferenceState != ImageDifference.Combined)
            {
                this.markableCanvas.ImageToDisplay.Source = this.dataHandler.ImageCache.GetCurrentImage();
                this.markableCanvas.ImageToMagnify.Source = this.markableCanvas.ImageToDisplay.Source;
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
            this.markableCanvas.ImageToDisplay.Source = this.dataHandler.ImageCache.GetCurrentImage();
            this.statusBar.SetMessage("Viewing differences compared to both the next and previous files");
        }
        #endregion

        #region Slider Event Handlers and related

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
        #endregion

        // Various dialogs perform a bulk edit, after which various states have to be refreshed
        // This method shows the dialog and (if a bulk edit is done) refreshes those states.
        private void ShowBulkImageEditDialog(Window dialog)
        {
            dialog.Owner = this;
            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                this.dataHandler.ImageDatabase.SelectDataTableImages(this.dataHandler.ImageDatabase.ImageSet.ImageFilter);
                this.RefreshCurrentImageProperties();
                this.RefreshDataViewDialogWindow();
            }
        }

        private void ShowImageCountsDialog(bool onImageLoading)
        {
            if (onImageLoading && this.state.SuppressFileCountOnImportDialog)
            {
                return;
            }

            Dictionary<ImageFilter, int> counts = this.dataHandler.ImageDatabase.GetImageCountsByQuality();
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

        #region Showing images
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
                BitmapSource unalteredImage = Constants.Images.EmptyImageSet;
                this.markableCanvas.ImageToDisplay.Source = unalteredImage;
                this.markableCanvas.ImageToMagnify.Source = unalteredImage; // Probably not needed

                // Delete any markers that may have been previously displayed 
                this.ClearTheMarkableCanvasListOfMetaTags();
                this.RefreshTheMarkableCanvasListOfMetaTags();

                // We could invalidate the cache here, but it will be reset anyways when images are loaded. 
                return;
            }

            // for the bitmap caching logic below to work this should be the only place where code in TimelapseWindow moves the image enumerator
            bool newImageToDisplay;
            if (this.dataHandler.ImageCache.TryMoveToImage(imageRow, out newImageToDisplay) == false)
            {
                throw new ArgumentOutOfRangeException("newImageRow", String.Format("{0} is not a valid row index in the image table.", imageRow));
            }

            // For each control, we get its type and then update its contents from the current data table row
            // this is always done as it's assumed either the image changed or that a control refresh is required due to database changes
            // the call to TryMoveToImage() above refreshes the data stored under this.dataHandler.ImageCache.Current
            TimeZoneInfo imageSetTimeZone = this.dataHandler.ImageDatabase.ImageSet.GetTimeZone();
            this.dataHandler.IsProgrammaticControlUpdate = true;
            foreach (KeyValuePair<string, DataEntryControl> control in this.dataEntryControls.ControlsByDataLabel)
            {
                string controlType = this.dataHandler.ImageDatabase.ImageDataColumnsByDataLabel[control.Key].ControlType;
                control.Value.Content = this.dataHandler.ImageCache.Current.GetValueDisplayString(control.Value.DataLabel, imageSetTimeZone);
            }
            this.dataHandler.IsProgrammaticControlUpdate = false;

            // update the status bar to show which image we are on out of the total displayed under the current filter
            // the total is always refreshed as it's not known if ShowImage() is being called due to a change in filtering
            this.statusBar.SetCurrentImage(this.dataHandler.ImageCache.CurrentRow + 1); // Add one because indexes are 0-based
            this.statusBar.SetCount(this.dataHandler.ImageDatabase.CurrentlySelectedImageCount);
            this.statusBar.ClearMessage();

            this.ImageNavigatorSlider.Value = this.dataHandler.ImageCache.CurrentRow;

            // get and display the new image if the image changed
            // this avoids unnecessary image reloads and refreshes in cases where ShowImage() is just being called to refresh controls
            // the image row can't be tested against as its meaning changes when filters are changed; use the image ID as that's both
            // unique and immutable
            if (newImageToDisplay)
            {
                BitmapSource unalteredImage = this.dataHandler.ImageCache.GetCurrentImage();
                this.markableCanvas.ImageToDisplay.Source = unalteredImage;

                // Set the image to magnify so the unaltered image will appear on the magnifying glass
                this.markableCanvas.ImageToMagnify.Source = unalteredImage;

                // Whenever we navigate to a new image, delete any markers that were displayed on the current image 
                // and then draw the markers assoicated with the new image
                this.GetTheMarkableCanvasListOfMetaTags();
                this.RefreshTheMarkableCanvasListOfMetaTags();
            }
            this.SetVideoPlayerToCurrentRow(); 
        }
        #endregion

        #region Keyboard shortcuts
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
                    this.markableCanvas.BookmarkSaveZoomPan();
                    break;
                case Key.Escape:
                    this.SetTopLevelFocus(false, currentKey);
                    break;
                case Key.OemPlus:           // Restore the zoom level / pan coordinates of the bookmark
                    this.markableCanvas.BookmarkSetZoomPan();
                    break;
                case Key.OemMinus:          // Restore the zoom level / pan coordinates of the bookmark
                    this.markableCanvas.BookmarkZoomOutAllTheWay();
                    break;
                case Key.M:                 // Toggle the magnifying glass on and off
                    this.MenuItemDisplayMagnifier_Click(this, null);
                    break;
                case Key.U:                 // Increase the magnifing glass zoom level
                    this.markableCanvas.MagnifierZoomIn();
                    break;
                case Key.D:                 // Decrease the magnifing glass zoom level
                    this.markableCanvas.MagnifierZoomOut();
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
                    this.ButtonCopy_Click(null, null);
                    break;
                case Key.Tab:
                    this.MoveFocusToNextOrPreviousControlOrImageSlider(Keyboard.Modifiers == ModifierKeys.Shift);
                    break;
                default:
                    return;
            }
            currentKey.Handled = true;
        }
        #endregion

        #region Setting Focus
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
            Keyboard.Focus(this.markableCanvas);
        }

        // Return true if the current focus is in a textbox or combobox data control
        private bool SendKeyToDataEntryControlOrMenu(KeyEventArgs eventData)
        {
            // check if a menu is open
            // it is sufficient to check one always visible item from each top level menu (file, edit, etc.)
            // NOTE: this must be kept in sync with 
            if (this.MenuItemExit.IsVisible || // file menu
                this.MenuItemCopyPreviousValues.IsVisible || // edit menu
                this.MenuItemControlsInSeparateWindow.IsVisible || // options menu
                this.MenuItemViewNextImage.IsVisible || // view menu
                this.MenuItemFilterAllImages.IsVisible || // filter menu, and then the help menu...
                this.MenuItemOverview.IsVisible)
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
            // NOTE: this list must be kept in sync with the System.Windows classes used by the classes in Timelapse\Util\DataEntry*.cs
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
        #endregion

        #region Marking and Counting

        // Get all the counters' metatags (if any) from the current row in the database
        private void GetTheMarkableCanvasListOfMetaTags()
        {
            this.counterCoordinates = this.dataHandler.ImageDatabase.GetMetaTagCounters(this.dataHandler.ImageCache.Current.ID);
        }

        // Event handler: A marker, as defined in e.MetaTag, has been either added (if e.IsNew is true) or deleted (if it is false)
        // Depending on which it is, add or delete the tag from the current counter control's list of tags 
        // If its deleted, remove the tag from the current counter control's list of tags
        // Every addition / deletion requires us to:
        // - update the contents of the counter control 
        // - update the data held by the image
        // - update the list of MetaTags held by that counter
        // - regenerate the list of metatags used by the markableCanvas
        private void MarkableCanvas_RaiseMetaTagEvent(object sender, MetaTagEventArgs e)
        {
            DataEntryCounter currentCounter;
            if (e.IsNew)
            {
                // A marker has been added
                currentCounter = this.FindSelectedCounter(); // No counters are selected, so don't mark anything
                if (currentCounter == null)
                {
                    return;
                }
                this.Markers_NewMetaTag(currentCounter, e.MetaTag);
            }
            else
            {
                // An existing marker has been deleted.
                DataEntryCounter counter = (DataEntryCounter)this.dataEntryControls.ControlsByDataLabel[e.MetaTag.DataLabel];

                // Part 1. Decrement the count 
                string oldCounterData = counter.Content;
                string newCounterData = String.Empty;

                // Decrement the counter only if there is a number in it
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
                    counter.Content = newCounterData;
                    this.dataHandler.IsProgrammaticControlUpdate = false;
                    this.dataHandler.ImageDatabase.UpdateImage(this.dataHandler.ImageCache.Current.ID, counter.DataLabel, newCounterData);
                }

                // Part 2. Each metacounter in the countercoords list reperesents a different control. 
                // So just check the first metatag's  DataLabel in each metatagcounter to see if it matches the counter's datalabel.
                MetaTagCounter metaTagCounter = null;
                foreach (MetaTagCounter coordinates in this.counterCoordinates)
                {
                    // If there are no metatags, we don't have to do anything.
                    if (coordinates.MetaTags.Count == 0)
                    {
                        continue;
                    }

                    // There are no metatags associated with this counter
                    if (coordinates.MetaTags[0].DataLabel == coordinates.DataLabel)
                    {
                        // We found the metatag counter associated with that control
                        metaTagCounter = coordinates;
                        break;
                    }
                }

                // Part 3. Remove the found metatag from the metatagcounter and from the database
                if (metaTagCounter != null)
                {
                    // Shouldn't really need this test, but if for some reason there wasn't a match...
                    string pointList = String.Empty;
                    for (int i = 0; i < metaTagCounter.MetaTags.Count; i++)
                    {
                        // Check if we are looking at the same metatag. 
                        if (e.MetaTag.Guid == metaTagCounter.MetaTags[i].Guid)
                        {
                            // We found the metaTag. Remove that metatag from the metatags list 
                            metaTagCounter.MetaTags.RemoveAt(i);
                            this.Speak(counter.Content); // Speak the current count
                        }
                        else
                        {
                            // Because we are not deleting it, we can add it to the new the point list
                            // Reconstruct the point list in the string form x,y|x,y e.g.,  0.333,0.333|0.500, 0.600
                            // for writing to the markerTable. Note that it leaves out the deleted value
                            Point point = metaTagCounter.MetaTags[i].Point;
                            if (!pointList.Equals(String.Empty))
                            {
                                pointList += Constants.Database.MarkerBar;          // We don't put a marker bar at the beginning of the point list
                            }
                            pointList += String.Format("{0:0.000},{1:0.000}", point.X, point.Y);   // Add a point in the form 
                        }
                    }
                    this.dataHandler.ImageDatabase.SetMarkerPoints(this.dataHandler.ImageCache.Current.ID, counter.DataLabel, pointList);
                }
                this.RefreshTheMarkableCanvasListOfMetaTags(); // Refresh the Markable Canvas, where it will also delete the metaTag at the same time
            }
            this.markableCanvas.MarkersRefresh();
        }

        /// <summary>
        /// A new Marker associated with a counter control has been created;
        /// Increment the counter controls value, and add the metatag to all data structures (including the database)
        /// </summary>
        private void Markers_NewMetaTag(DataEntryCounter counter, MetaTag metaTag)
        {
            // Get the Counter Control's contents,  increment its value (as we have added a new marker) 
            // Then update the control's content as well as the database
            string counterContent = counter.Content;

            if (String.IsNullOrWhiteSpace(counterContent))
            {
                counterContent = "0";
            }

            // If we can't convert it to an int, assume that someone set the default value to either a non-integer in the template, or that it's a space. In either case, revert it to zero.
            int count;
            if (Int32.TryParse(counterContent, out count) == false)
            {
                count = 0; 
            }
  
            count++;
            counterContent = count.ToString();
            this.dataHandler.IsProgrammaticControlUpdate = true;
            this.dataHandler.ImageDatabase.UpdateImage(this.dataHandler.ImageCache.Current.ID, counter.DataLabel, counterContent);
            counter.Content = counterContent;
            this.dataHandler.IsProgrammaticControlUpdate = false;

            // Find the metatagCounter associated with this particular control so we can add a metatag to it
            MetaTagCounter metatagCounter = null;
            foreach (MetaTagCounter mtcounter in this.counterCoordinates)
            {
                if (mtcounter.DataLabel == counter.DataLabel)
                {
                    metatagCounter = mtcounter;
                    break;
                }
            }

            // Fill in the metatag information. Also create a TagFinder (which contains a reference to the counter index) and add it as the object's metatag
            metaTag.Label = counter.Label;   // The tooltip will be the counter label plus its data label
            metaTag.Label += "\n" + counter.DataLabel;
            metaTag.Brush = Brushes.Red;               // Make it Red (for now)
            metaTag.DataLabel = counter.DataLabel;
            metaTag.Annotate = true; // Show the annotation as its created. We will clear it on the next refresh
            metaTag.AnnotationAlreadyShown = false;

            // Add the meta tag to the metatag counter
            metatagCounter.AddMetaTag(metaTag);

            // Update this counter's list of points in the marker database
            String pointList = String.Empty;
            foreach (MetaTag mt in metatagCounter.MetaTags)
            {
                if (!pointList.Equals(String.Empty))
                {
                    pointList += Constants.Database.MarkerBar; // We don't put a marker bar at the beginning of the point list
                }
                pointList += String.Format("{0:0.000},{1:0.000}", mt.Point.X, mt.Point.Y); // Add a point in the form x,y e.g., 0.5, 0.7
            }
            this.dataHandler.ImageDatabase.SetMarkerPoints(this.dataHandler.ImageCache.Current.ID, counter.DataLabel, pointList);
            this.RefreshTheMarkableCanvasListOfMetaTags(true);
            this.Speak(counter.Content + " " + counter.Label); // Speak the current count
        }

        // Create a list of metaTags from those stored in each image's metatag counters, 
        // and then set the markableCanvas's list of metaTags to that list. We also reset the emphasis for those tags as needed.
        private void RefreshTheMarkableCanvasListOfMetaTags()
        {
            this.RefreshTheMarkableCanvasListOfMetaTags(false); // By default, we don't show the annotation
        }

        private void RefreshTheMarkableCanvasListOfMetaTags(bool showAnnotation)
        {
            // The markable canvas uses a simple list of metatags to decide what to do.
            // So we just create that list here, where we also reset the emphasis of some of the metatags
            List<MetaTag> metaTagList = new List<MetaTag>();
            if (this.counterCoordinates != null)
            {
                DataEntryCounter selectedCounter = this.FindSelectedCounter();
                for (int counter = 0; counter < this.counterCoordinates.Count; counter++)
                {
                    MetaTagCounter mtagCounter = this.counterCoordinates[counter];
                    DataEntryControl control;
                    if (this.dataEntryControls.ControlsByDataLabel.TryGetValue(mtagCounter.DataLabel, out control) == false)
                    {
                        // If we can't find the counter, its likely because the control was made invisible in the template,
                        // which means that there is no control associated with the marker. So just don't create the 
                        // markers associated with this control. Note that if the control is later made visible in the template,
                        // the markers will then be shown. 
                        continue;
                    }

                    // Update the emphasise for each tag to reflect how the user is interacting with tags
                    DataEntryCounter currentCounter = (DataEntryCounter)this.dataEntryControls.ControlsByDataLabel[mtagCounter.DataLabel];
                    foreach (MetaTag mtag in mtagCounter.MetaTags)
                    {
                        mtag.Emphasise = this.state.IsMouseOverCounter;
                        if (selectedCounter != null && currentCounter.DataLabel == selectedCounter.DataLabel)
                        {
                            mtag.Brush = (SolidColorBrush)new BrushConverter().ConvertFromString(Constants.SelectionColour);
                        }
                        else
                        {
                            mtag.Brush = (SolidColorBrush)new BrushConverter().ConvertFromString(Constants.StandardColour);
                        }

                        // the first time through, show an annotation. Otherwise we clear the flags to hide the annotation.
                        if (mtag.Annotate && !mtag.AnnotationAlreadyShown)
                        {
                            mtag.Annotate = true;
                            mtag.AnnotationAlreadyShown = true;
                        }
                        else
                        {
                            mtag.Annotate = false;
                        }
                        mtag.Label = currentCounter.Label;
                        metaTagList.Add(mtag); // Add the MetaTag in the list 
                    }
                }
            }
            this.markableCanvas.MetaTags = metaTagList;
        }

        // Clear the counters' metatags (if any) from the current row in the database
        private void ClearTheMarkableCanvasListOfMetaTags()
        {
            this.counterCoordinates = null;
        }
        #endregion

        #region File Menu Callbacks and Support Functions
        private void File_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            this.MenuItemRecentImageSets_Refresh();
        }

        private void MenuItemAddImagesToImageSet_Click(object sender, RoutedEventArgs e)
        {
            string folderPath;
            if (this.ShowFolderSelectionDialog(out folderPath))
            {
                BackgroundWorker backgroundWorker;
                this.TryBeginImageFolderLoadAsync(folderPath, out backgroundWorker);
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
            if (this.state.SuppressFilteredCsvExportPrompt == false &&
                this.dataHandler.ImageDatabase.ImageSet.ImageFilter != ImageFilter.All)
            {
                MessageBox messageBox = new MessageBox("Exporting to a CSV file on a filtered view...", this, MessageBoxButton.OKCancel);
                messageBox.Message.What = "Only a subset of your data will be exported to the CSV file.";
                messageBox.Message.Reason = "As your filter (in the Filter menu) is not set to view 'All', ";
                messageBox.Message.Reason += "only data for those files displayed by this filter will be exported. ";
                messageBox.Message.Solution = "If you want to export just this subset, then " + Environment.NewLine;
                messageBox.Message.Solution += "\u2022 click Okay" + Environment.NewLine + Environment.NewLine;
                messageBox.Message.Solution += "If you want to export all your data for all your files, then " + Environment.NewLine;
                messageBox.Message.Solution += "\u2022 click Cancel," + Environment.NewLine;
                messageBox.Message.Solution += "\u2022 select 'All Files' in the Filter menu, " + Environment.NewLine;
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
                    this.state.SuppressFilteredCsvExportPrompt = messageBox.DontShowAgain.IsChecked.Value;
                    this.MenuItemEnableFilteredCsvExportPrompt.IsChecked = !this.state.SuppressFilteredCsvExportPrompt;
                }
            }

            // Generate the file names/path
            string csvFileName = Path.GetFileNameWithoutExtension(this.dataHandler.ImageDatabase.FileName) + ".csv";
            string csvFilePath = Path.Combine(this.FolderPath, csvFileName);

            // Backup the csv file if it exists, as the export will overwrite it. 
            if (FileBackup.TryCreateBackups(this.FolderPath, csvFileName))
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
                MessageBox messageBox = new MessageBox("Importing CSV data rules...", this, MessageBoxButton.OKCancel);
                messageBox.Message.What = "Importing data from a CSV (comma separated value) file will only work if you follow the rules below." + Environment.NewLine;
                messageBox.Message.What += "Otherwise your Timelapse data may become corrupted.";
                messageBox.Message.Reason = "Timelapse requires the CSV file and its data to follow a specific format.";
                messageBox.Message.Solution = "\u2022 Only modify and import a CSV file previously exported by Timelapse." + Environment.NewLine;
                messageBox.Message.Solution += "\u2022 Do not change Folder, RelativePath, or File as those fields uniquely identify a file" + Environment.NewLine;
                messageBox.Message.Solution += "\u2022 Do not change Date or Time as those columns are ignored (change DateTime instead)" + Environment.NewLine;
                messageBox.Message.Solution += "\u2022 Do not change column names" + Environment.NewLine;
                messageBox.Message.Solution += "\u2022 Do not add or delete rows (those changes will be ignored)" + Environment.NewLine;
                messageBox.Message.Solution += "\u2022 Restrict modifications as follows:" + Environment.NewLine;
                messageBox.Message.Solution += String.Format("    \u2022 DateTime must be in '{0}' format{1}", Constants.Time.DateTimeDatabaseFormat, Environment.NewLine);
                messageBox.Message.Solution += String.Format("    \u2022 UtcOffset must be a floating point number between {0} and {1}, inclusive{2}", DateTimeHandler.ToDatabaseUtcOffsetString(Constants.Time.MinimumUtcOffset), DateTimeHandler.ToDatabaseUtcOffsetString(Constants.Time.MinimumUtcOffset), Environment.NewLine);
                messageBox.Message.Solution += "    \u2022 Counter data must be zero or a positive integer" + Environment.NewLine;
                messageBox.Message.Solution += "    \u2022 Flag data must be 'true' or 'false'" + Environment.NewLine;
                messageBox.Message.Solution += "    \u2022 FixedChoice data must be a string that exactly matches one of the FixedChoice menu options, or empty." + Environment.NewLine;
                messageBox.Message.Solution += "    \u2022 Note data to any string, including empty.";
                messageBox.Message.Result = "Timelapse will create a backup .ddb file in the Backups folder, and will then try its best.";
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
                                             String.Format("Comma separated value files ({0})|*{0}", Constants.File.CsvFileExtension),
                                             out csvFilePath) == false)
            {
                return;
            }

            // Create a backup database file
            if (FileBackup.TryCreateBackups(this.FolderPath, this.dataHandler.ImageDatabase.FileName))
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
            this.SelectDataTableImagesAndShowImage(this.dataHandler.ImageCache.CurrentRow, this.dataHandler.ImageDatabase.ImageSet.ImageFilter);
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
        /// Exit Timelapse
        /// </summary>
        private void MenuItemExit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
            Application.Current.Shutdown();
        }

        private bool ShowFolderSelectionDialog(out string folderPath)
        {
            CommonOpenFileDialog folderSelectionDialog = new CommonOpenFileDialog();
            folderSelectionDialog.Title = "Select a folder ...";
            folderSelectionDialog.DefaultDirectory = this.mostRecentImageAddFolderPath == null ? this.FolderPath : this.mostRecentImageAddFolderPath;
            folderSelectionDialog.IsFolderPicker = true;
            if (folderSelectionDialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                folderPath = folderSelectionDialog.FileName;

                // remember the parent of the selected folder path to save the user clicks and scrolling in case images from additional 
                // directories are added
                this.mostRecentImageAddFolderPath = Path.GetDirectoryName(folderPath);
                return true;
            }

            folderPath = null;
            return false;
        }
        #endregion

        #region Edit Menu Callbacks

        // Populate a data field from metadata (example metadata displayed from the currently selected image)
        private void MenuItemPopulateFieldFromMetadata_Click(object sender, RoutedEventArgs e)
        {
            // If we are not in the filter all view, or if its a corrupt image or deleted image, tell the person. Selecting ok will shift the filter.
            // We want to be on a valid image as otherwise the metadata of interest won't appear
            if (this.dataHandler.ImageCache.Current.IsDisplayable() == false)
            {
                int firstImageDisplayable = this.dataHandler.ImageDatabase.FindFirstDisplayableImage(Constants.DefaultImageRowIndex);
                if (firstImageDisplayable == -1)
                {
                    // There are no displayable images, and thus no metadata to choose from, so abort
                    MessageBox messageBox = new MessageBox("Populate a data field with image metadata of your choosing.", this);
                    messageBox.Message.Problem = "We can't extract any metadata, as there are no valid displayable file." + Environment.NewLine;
                    messageBox.Message.Reason += "Timelapse must have at least one valid file in order to get its metadata. All files are either missing or corrupted.";
                    messageBox.Message.Icon = MessageBoxImage.Error;
                    messageBox.ShowDialog();
                    return;
                }
            }

            if (this.MaybePromptToApplyOperationIfFilteredView(this.state.SuppressFilteredPopulateFieldFromMetadataPrompt, 
                                                               "'Populate a data field with image metadata of your choosing...'", 
                                                               (bool optOut) =>
                                                               {
                                                                   this.state.SuppressFilteredPopulateFieldFromMetadataPrompt = optOut;
                                                                   this.MenuItemEnableFilteredPopulateFieldFromMetadataPrompt.IsChecked = !optOut;
                                                               }))
            {
                using (PopulateFieldWithMetadata populateField = new PopulateFieldWithMetadata(this.dataHandler.ImageDatabase, this.dataHandler.ImageCache.Current.GetImagePath(this.FolderPath), this))
                {
                    this.ShowBulkImageEditDialog(populateField);
                }
            }
        }

        /// <summary>Delete the current image by replacing it with a placeholder image, while still making a backup of it</summary>
        private void Delete_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            try
            {
                int deletedImages = this.dataHandler.ImageDatabase.GetImageCount(ImageFilter.MarkedForDeletion);
                this.MenuItemDeleteImages.IsEnabled = deletedImages > 0;
                this.MenuItemDeleteImagesAndData.IsEnabled = deletedImages > 0;
                this.MenuItemDeleteImageAndData.IsEnabled = true;
                this.MenuItemDeleteImage.IsEnabled = this.dataHandler.ImageCache.Current.IsDisplayable() || this.dataHandler.ImageCache.Current.ImageQuality == ImageFilter.Corrupted;
            }
            catch (Exception exception)
            {
                Debug.Fail("Delete submenu failed to open.", exception.ToString());

                // This function was blowing up on one user's machine, but not others.
                // I couldn't figure out why, so I just put this fallback in here to catch that unusual case.
                this.MenuItemDeleteImages.IsEnabled = true;
                this.MenuItemDeleteImagesAndData.IsEnabled = true;
                this.MenuItemDeleteImage.IsEnabled = true;
                this.MenuItemDeleteImageAndData.IsEnabled = true;
            }
        }

        /// <summary>Delete all images marked for deletion, and optionally the data associated with those images.
        /// Deleted images are actually moved to a backup folder.</summary>
        private void MenuItemDeleteImages_Click(object sender, RoutedEventArgs e)
        {
            MenuItem mi = sender as MenuItem;
            List<ImageRow> imagesToDelete;
            bool deleteData;
            bool deletingCurrentImage;

            // This callback is invoked by either variatons of DeleteImage (which deletes the current image) or 
            // DeleteImages (which deletes the images marked by the deletion flag)
            // Thus we need to use two different methods to construct a table containing all the images marked for deletion
            if (mi.Name.Equals(this.MenuItemDeleteImages.Name) || mi.Name.Equals(this.MenuItemDeleteImagesAndData.Name))
            {
                // Delete by deletion flags case. 
                // Construct a table that contains the datarows of all images with their delete flag set, and set various flags
                imagesToDelete = this.dataHandler.ImageDatabase.GetImagesMarkedForDeletion().ToList();
                // Prune image rows that are not in the current filter 
                for (int i = imagesToDelete.Count - 1; i >= 0;  i--)
                {
                    if (this.dataHandler.ImageDatabase.ImageDataTable.Find(imagesToDelete[i].ID) == null)
                    {
                        imagesToDelete.Remove(imagesToDelete[i]);
                    }
                }
                deleteData = mi.Name.Equals(this.MenuItemDeleteImages.Name) ? false : true;
                deletingCurrentImage = false;
            }
            else
            {
                // Delete current image case. Get the ID of the current image and construct a datatable that contains that image's datarow
                imagesToDelete = new List<ImageRow>();
                if (this.dataHandler.ImageCache.Current != null)
                {
                    imagesToDelete.Add(this.dataHandler.ImageCache.Current);
                }
                deleteData = mi.Name.Equals(this.MenuItemDeleteImage.Name) ? false : true;
                deletingCurrentImage = true;
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

            DeleteImages deleteImagesDialog = new DeleteImages(this.dataHandler.ImageDatabase, imagesToDelete, deleteData, deletingCurrentImage);
            deleteImagesDialog.Owner = this;
            bool? result = deleteImagesDialog.ShowDialog();

            if (result == true)
            {
                long currentID = this.dataHandler.ImageCache.Current.ID;
                int currentRow;
                if (mi.Name.Equals(this.MenuItemDeleteImage.Name) || mi.Name.Equals(this.MenuItemDeleteImages.Name))
                {
                    // We only deleted the image, not the data. We invoke ShowImage with the saved current row to show the missing image placeholder
                    currentRow = this.dataHandler.ImageCache.CurrentRow;  // TryInvalidate may reset the current row to -1, so we need to save it.
                    foreach (long id in deleteImagesDialog.ImageFilesRemovedByID)
                    {
                        this.dataHandler.ImageCache.TryInvalidate(id);
                    }
                    this.ShowImage(currentRow);
                }
                else
                {
                    // Data has been deleted as well.
                    // Reload the datatable. Then find and show the image closest to the last one shown
                    this.SelectDataTableImagesAndShowImage(Constants.DefaultImageRowIndex, this.dataHandler.ImageDatabase.ImageSet.ImageFilter); // Reset the filter to retrieve the remaining images
                    int nextImage = this.dataHandler.ImageDatabase.FindClosestImage(currentID);
                    if (this.dataHandler.ImageDatabase.CurrentlySelectedImageCount > 0)
                    {
                        this.ShowImage(nextImage); // Reset the filter to retrieve the remaining images
                    }
                    else
                    { 
                        this.EnableOrDisableMenusAndControls();
                    }
                }
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

        private void ButtonCopy_Click(object sender, RoutedEventArgs e)
        {
            int previousRow = this.dataHandler.ImageCache.CurrentRow - 1;
            if (previousRow < 0)
            {
                return; // We are already on the first image, so there is nothing to copy
            }

            TimeZoneInfo imageSetTimeZone = this.dataHandler.ImageDatabase.ImageSet.GetTimeZone();
            foreach (KeyValuePair<string, DataEntryControl> pair in this.dataEntryControls.ControlsByDataLabel)
            {
                DataEntryControl control = pair.Value;
                if (this.dataHandler.ImageDatabase.IsControlCopyable(control.DataLabel))
                {
                    control.Content = this.dataHandler.ImageDatabase.ImageDataTable[previousRow].GetValueDisplayString(control.DataLabel, imageSetTimeZone);
                }
            }
        }
        #endregion

        #region Options Menu Callbacks
        /// <summary>Show advanced image set options</summary>
        private void MenuItemAdvancedImageSetOptions_Click(object sender, RoutedEventArgs e)
        {
            AdvancedImageSetOptions advancedImageSetOptions = new AdvancedImageSetOptions(this.dataHandler.ImageDatabase, this);
            advancedImageSetOptions.ShowDialog();
        }

        /// <summary>Show advanced Timelapse options</summary>
        private void MenuItemAdvancedTimelapseOptions_Click(object sender, RoutedEventArgs e)
        {
            AdvancedTimelapseOptions advancedTimelapseOptions = new AdvancedTimelapseOptions(this.state, this.markableCanvas, this);
            advancedTimelapseOptions.ShowDialog();
        }

        /// <summary>Toggle the showing of controls in a separate window</summary>
        private void MenuItemControlsInSeparateWindow_Click(object sender, RoutedEventArgs e)
        {
            MenuItem mi = (MenuItem)sender;
            if (mi.IsChecked)
            {
                this.ControlsInSeparateWindow();
            }
            else
            {
                this.ControlsInMainWindow();
            }
        }

        /// <summary>Toggle the magnifier on and off</summary>
        private void MenuItemDisplayMagnifier_Click(object sender, RoutedEventArgs e)
        {
            this.dataHandler.ImageDatabase.ImageSet.MagnifierEnabled = !this.dataHandler.ImageDatabase.ImageSet.MagnifierEnabled;
            this.markableCanvas.IsMagnifyingGlassVisible = this.dataHandler.ImageDatabase.ImageSet.MagnifierEnabled;
            this.MenuItemDisplayMagnifier.IsChecked = this.dataHandler.ImageDatabase.ImageSet.MagnifierEnabled;
        }

        /// <summary>Increase the magnification of the magnifying glass. We do this several times to make
        /// the increase effect more visible through a menu option versus the keyboard equivalent</summary>
        private void MenuItemMagnifierIncrease_Click(object sender, RoutedEventArgs e)
        {
            this.markableCanvas.MagnifierZoomIn();
            this.markableCanvas.MagnifierZoomIn();
            this.markableCanvas.MagnifierZoomIn();
            this.markableCanvas.MagnifierZoomIn();
            this.markableCanvas.MagnifierZoomIn();
            this.markableCanvas.MagnifierZoomIn();
        }

        /// <summary> Decrease the magnification of the magnifying glass. We do this several times to make
        /// the increase effect more visible through a menu option versus the keyboard equivalent</summary>
        private void MenuItemMagnifierDecrease_Click(object sender, RoutedEventArgs e)
        {
            this.markableCanvas.MagnifierZoomOut();
            this.markableCanvas.MagnifierZoomOut();
            this.markableCanvas.MagnifierZoomOut();
            this.markableCanvas.MagnifierZoomOut();
            this.markableCanvas.MagnifierZoomOut();
            this.markableCanvas.MagnifierZoomOut();
        }

        private void MenuItemOptionsDarkImagesThreshold_Click(object sender, RoutedEventArgs e)
        {
            if (this.MaybePromptToApplyOperationIfFilteredView(this.state.SuppressFilteredDarkThresholdPrompt,
                                                               "'Customize the threshold for determining dark files...'", 
                                                               (bool optOut) =>
                                                               {
                                                                   this.state.SuppressFilteredDarkThresholdPrompt = optOut;
                                                                   this.MenuItemEnableFilteredDarkThresholdPrompt.IsChecked = !optOut;
                                                               }))
            {
                using (DarkImagesThreshold darkThreshold = new DarkImagesThreshold(this.dataHandler.ImageDatabase, this.dataHandler.ImageCache.CurrentRow, this.state, this))
                {
                    darkThreshold.Owner = this;
                    darkThreshold.ShowDialog();
                }
            }
        }

        /// <summary>Correct the date by specifying an offset</summary>
        private void MenuItemDateTimeFixedCorrection_Click(object sender, RoutedEventArgs e)
        {
             // Warn user that they are in a filtered view, and verify that they want to continue
            if (this.MaybePromptToApplyOperationIfFilteredView(this.state.SuppressFilteredDateTimeFixedCorrectionPrompt, 
                                                               "'Add a fixed correction value to every date/time...'", 
                                                               (bool optOut) =>
                                                               {
                                                                   this.state.SuppressFilteredDateTimeFixedCorrectionPrompt = optOut;
                                                                   this.MenuItemEnableFilteredDateTimeFixedCorrectionPrompt.IsChecked = !optOut;
                                                               }))
            {
                DateTimeFixedCorrection fixedDateCorrection = new DateTimeFixedCorrection(this.dataHandler.ImageDatabase, this.dataHandler.ImageCache.Current, this);
                if (fixedDateCorrection.Abort)
                {
                    TimeZoneInfo imageSetTimeZone = this.dataHandler.ImageDatabase.ImageSet.GetTimeZone();
                    MessageBox messageBox = new MessageBox("Can't add a fixed correction value to every date/time...", this);
                    messageBox.Message.Problem = "Can't add a fixed correction value to every date/time in this filtered view.";
                    messageBox.Message.Reason = "This operation requires using the current image's date and time fields. " + Environment.NewLine;
                    messageBox.Message.Reason += "However, " + this.dataHandler.ImageCache.Current.GetDisplayDateTime(imageSetTimeZone) + " is not a recognizable date/time." + Environment.NewLine;
                    messageBox.Message.Reason += "\u2022 dates should look like dd-MMM-yyyy e.g., 16-Jan-2016" + Environment.NewLine;
                    messageBox.Message.Reason += "\u2022 times should look like HH:mm:ss using 24 hour time e.g., 01:05:30 or 13:30:00";
                    messageBox.Message.Result = "Date correction will be aborted and nothing will be changed.";
                    messageBox.Message.Hint = "\u2022 Check the format of this image's date and time." + Environment.NewLine;
                    messageBox.Message.Hint += "\u2022 Navigate to another image that has a properly formatted date and time.";
                    messageBox.Message.Icon = MessageBoxImage.Error;
                    messageBox.ShowDialog();
                    return;
                }
                this.ShowBulkImageEditDialog(fixedDateCorrection);
            }
        }

        /// <summary>Correct for drifting clock times. Correction applied only to images in the filtered view.</summary>
        private void MenuItemDateTimeLinearCorrection_Click(object sender, RoutedEventArgs e)
        {
            // Warn user that they are in a filtered view, and verify that they want to continue
            if (this.MaybePromptToApplyOperationIfFilteredView(this.state.SuppressFilteredDateTimeLinearCorrectionPrompt,
                                                               "'Correct for camera clock drift'",
                                                               (bool optOut) =>
                                                               {
                                                                   this.state.SuppressFilteredDateTimeLinearCorrectionPrompt = optOut;
                                                                   this.MenuItemEnableFilteredDateTimeLinearCorrectionPrompt.IsChecked = !optOut;
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
                    messageBox.Message.Hint = "Check the format of your dates and times. You may also want to change your filter (if you're not viewing All Images)";
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
            // If we are not in the filter all view, or if its a corrupt image, tell the person. Selecting ok will shift the views..
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

            if (this.MaybePromptToApplyOperationIfFilteredView(this.state.SuppressFilteredDaylightSavingsCorrectionPrompt, 
                                                               "'Correct for daylight savings time...'", 
                                                               (bool optOut) =>
                                                               {
                                                                   this.state.SuppressFilteredDaylightSavingsCorrectionPrompt = optOut;
                                                                   this.MenuItemEnableFilteredDaylightSavingsCorrectionPrompt.IsChecked = !optOut;
                                                               }))
            {
                DateDaylightSavingsTimeCorrection dateTimeChange = new DateDaylightSavingsTimeCorrection(this.dataHandler.ImageDatabase, this.dataHandler.ImageCache, this);
                this.ShowBulkImageEditDialog(dateTimeChange);
            }
        }

        // Correct ambiguous dates dialog (i.e. dates that could be read as either month/day or day/month
        private void MenuItemCorrectAmbiguousDates_Click(object sender, RoutedEventArgs e)
        {
            // Warn user that they are in a filtered view, and verify that they want to continue
            if (this.MaybePromptToApplyOperationIfFilteredView(this.state.SuppressFilteredAmbiguousDatesPrompt, 
                                                               "'Correct ambiguous dates...'",
                                                               (bool optOut) =>
                                                               {
                                                                   this.state.SuppressFilteredAmbiguousDatesPrompt = optOut;
                                                                   this.MenuItemEnableFilteredAmbiguousDatesPrompt.IsChecked = !optOut;
                                                               }))
            {
                DateCorrectAmbiguous dateCorrection = new DateCorrectAmbiguous(this.dataHandler.ImageDatabase, this);
                if (dateCorrection.Abort)
                {
                    MessageBox messageBox = new MessageBox("No ambiguous dates found", this);
                    messageBox.Message.What = "No ambiguous dates found.";
                    messageBox.Message.Reason = "All of the images in this filtered view have unambguous date fields." + Environment.NewLine;
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
            // Warn user that they are in a filtered view, and verify that they want to continue
            if (this.MaybePromptToApplyOperationIfFilteredView(this.state.SuppressFilteredSetTimeZonePrompt,
                                                               "'Set the time zone of every date/time...'",
                                                               (bool optOut) =>
                                                               {
                                                                   this.state.SuppressFilteredSetTimeZonePrompt = optOut;
                                                                   this.MenuItemEnableFilteredSetTimeZonePrompt.IsChecked = !optOut;
                                                               }))
            {
                DateTimeSetTimeZone fixedDateCorrection = new DateTimeSetTimeZone(this.dataHandler.ImageDatabase, this.dataHandler.ImageCache.Current, this);
                if (fixedDateCorrection.Abort)
                {
                    TimeZoneInfo imageSetTimeZone = this.dataHandler.ImageDatabase.ImageSet.GetTimeZone();
                    MessageBox messageBox = new MessageBox("Can't set the time zone for every file...", this);
                    messageBox.Message.Problem = "Can't set the UTC offset of every file's date/time in this filtered view.";
                    messageBox.Message.Reason = "This operation requires an image with valid date and time fields. " + Environment.NewLine;
                    messageBox.Message.Reason += "However, " + this.dataHandler.ImageCache.Current.GetDisplayDateTime(imageSetTimeZone) + " is not a recognizable date/time." + Environment.NewLine;
                    messageBox.Message.Reason += "\u2022 dates should look like dd-MMM-yyyy e.g., 16-Jan-2016" + Environment.NewLine;
                    messageBox.Message.Reason += "\u2022 times should look like HH:mm:ss using 24 hour time e.g., 01:05:30 or 13:30:00";
                    messageBox.Message.Result = "Time zone assignment will be aborted and nothing will be changed.";
                    messageBox.Message.Hint = "\u2022 Check the format of this image's date and time." + Environment.NewLine;
                    messageBox.Message.Hint += "\u2022 Select images with properly formatted dates and times.";
                    messageBox.Message.Icon = MessageBoxImage.Error;
                    messageBox.ShowDialog();
                    return;
                }
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

        private void MenuItemEnableFilteredAmbiguousDatesPrompt_Click(object sender, RoutedEventArgs e)
        {
            this.state.SuppressFilteredAmbiguousDatesPrompt = !this.state.SuppressFilteredAmbiguousDatesPrompt;
            this.MenuItemEnableFilteredAmbiguousDatesPrompt.IsChecked = !this.state.SuppressFilteredAmbiguousDatesPrompt;
        }

        private void MenuItemEnableFilteredCsvExportPrompt_Click(object sender, RoutedEventArgs e)
        {
            this.state.SuppressFilteredCsvExportPrompt = !this.state.SuppressFilteredCsvExportPrompt;
            this.MenuItemEnableFilteredCsvExportPrompt.IsChecked = !this.state.SuppressFilteredCsvExportPrompt;
        }

        private void MenuItemEnableFilteredDarkThresholdPrompt_Click(object sender, RoutedEventArgs e)
        {
            this.state.SuppressFilteredDarkThresholdPrompt = !this.state.SuppressFilteredDarkThresholdPrompt;
            this.MenuItemEnableFilteredDarkThresholdPrompt.IsChecked = !this.state.SuppressFilteredDarkThresholdPrompt;
        }

        private void MenuItemEnableFilteredDateTimeFixedCorrectionPrompt_Click(object sender, RoutedEventArgs e)
        {
            this.state.SuppressFilteredDateTimeFixedCorrectionPrompt = !this.state.SuppressFilteredDateTimeFixedCorrectionPrompt;
            this.MenuItemEnableFilteredDateTimeFixedCorrectionPrompt.IsChecked = !this.state.SuppressFilteredDateTimeFixedCorrectionPrompt;
        }

        private void MenuItemEnableFilteredDateTimeLinearCorrectionPrompt_Click(object sender, RoutedEventArgs e)
        {
            this.state.SuppressFilteredDateTimeLinearCorrectionPrompt = !this.state.SuppressFilteredDateTimeLinearCorrectionPrompt;
            this.MenuItemEnableFilteredDateTimeLinearCorrectionPrompt.IsChecked = !this.state.SuppressFilteredDateTimeLinearCorrectionPrompt;
        }

        private void MenuItemEnableFilteredDaylightSavingsCorrectionPrompt_Click(object sender, RoutedEventArgs e)
        {
            this.state.SuppressFilteredDaylightSavingsCorrectionPrompt = !this.state.SuppressFilteredDaylightSavingsCorrectionPrompt;
            this.MenuItemEnableFilteredDaylightSavingsCorrectionPrompt.IsChecked = !this.state.SuppressFilteredDaylightSavingsCorrectionPrompt;
        }

        private void MenuItemEnableFilteredPopulateFieldFromMetadataPrompt_Click(object sender, RoutedEventArgs e)
        {
            this.state.SuppressFilteredPopulateFieldFromMetadataPrompt = !this.state.SuppressFilteredPopulateFieldFromMetadataPrompt;
            this.MenuItemEnableFilteredPopulateFieldFromMetadataPrompt.IsChecked = !this.state.SuppressFilteredPopulateFieldFromMetadataPrompt;
        }

        private void MenuItemEnableFilteredRereadDatesFromFilesPrompt_Click(object sender, RoutedEventArgs e)
        {
            this.state.SuppressFilteredRereadDatesFromFilesPrompt = !this.state.SuppressFilteredRereadDatesFromFilesPrompt;
            this.MenuItemEnableFilteredRereadDatesFromFilesPrompt.IsChecked = !this.state.SuppressFilteredRereadDatesFromFilesPrompt;
        }

        private void MenuItemEnableFilteredSetTimeZonePrompt_Click(object sender, RoutedEventArgs e)
        {
            this.state.SuppressFilteredSetTimeZonePrompt = !this.state.SuppressFilteredSetTimeZonePrompt;
            this.MenuItemEnableFilteredSetTimeZonePrompt.IsChecked = !this.state.SuppressFilteredSetTimeZonePrompt;
        }

        private void MenuItemRereadDatesfromImages_Click(object sender, RoutedEventArgs e)
        {
            // If we are not in the filter all view, or if its a corrupt image, tell the person. Selecting ok will shift the views..
            if (this.MaybePromptToApplyOperationIfFilteredView(this.state.SuppressFilteredRereadDatesFromFilesPrompt,
                                                               "'Reread dates from files...'",
                                                               (bool optOut) =>
                                                               {
                                                                   this.state.SuppressFilteredRereadDatesFromFilesPrompt = optOut;
                                                                   this.MenuItemEnableFilteredRereadDatesFromFilesPrompt.IsChecked = !optOut;
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
        #endregion

        #region View Menu Callbacks
        private void Filter_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            Dictionary<ImageFilter, int> counts = this.dataHandler.ImageDatabase.GetImageCountsByQuality();

            this.MenuItemFilterLightImages.IsEnabled = counts[ImageFilter.Ok] > 0;
            this.MenuItemFilterDarkImages.IsEnabled = counts[ImageFilter.Dark] > 0;
            this.MenuItemFilterCorruptedImages.IsEnabled = counts[ImageFilter.Corrupted] > 0;
            this.MenuItemFilterMissingImages.IsEnabled = counts[ImageFilter.Missing] > 0;
            this.MenuItemFilterImagesMarkedForDeletion.IsEnabled = this.dataHandler.ImageDatabase.GetImageCount(ImageFilter.MarkedForDeletion) > 0;
        }

        private void MenuItemZoomIn_Click(object sender, RoutedEventArgs e)
        {
            lock (this.markableCanvas.ImageToDisplay)
            {
                Point location = Mouse.GetPosition(this.markableCanvas.ImageToDisplay);
                if (location.X > this.markableCanvas.ImageToDisplay.ActualWidth || location.Y > this.markableCanvas.ImageToDisplay.ActualHeight)
                {
                    return; // Ignore points if mouse is off the image
                }
                this.markableCanvas.ScaleImage(location, true); // Zooming in if delta is positive, else zooming out
            }
        }

        private void MenuItemZoomOut_Click(object sender, RoutedEventArgs e)
        {
            lock (this.markableCanvas.ImageToDisplay)
            {
                Point location = Mouse.GetPosition(this.markableCanvas.ImageToDisplay);
                this.markableCanvas.ScaleImage(location, false); // Zooming in if delta is positive, else zooming out
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
            if (this.dlgVideoPlayer == null || this.dlgVideoPlayer.IsLoaded != true)
            {
                this.dlgVideoPlayer = new WindowVideoPlayer(this, this.FolderPath);
                this.dlgVideoPlayer.Owner = this;
            }

            // Initialize the video player to display the file held by the current row
            this.SetVideoPlayerToCurrentRow();

            // If the video player is already loaded, ensure that it is not minimized
            if (this.dlgVideoPlayer.IsLoaded)
            {
                this.dlgVideoPlayer.WindowState = WindowState.Normal;
            }
            else
            {
                this.dlgVideoPlayer.Show();
            }
        }

        // Set the video player to the current row, where it will try to display it (or provide appropriate feedback)
        private void SetVideoPlayerToCurrentRow()
        {
            if (this.dlgVideoPlayer == null)
            {
                return;
            }
            this.dlgVideoPlayer.CurrentRow = this.dataHandler.ImageCache.Current;
        }
        #endregion

        #region Filter Menu
        /// <summary>Select the appropriate filter and update the view</summary>
        private void MenuItemFilter_Click(object sender, RoutedEventArgs e)
        {
            MenuItem item = (MenuItem)sender;
            ImageFilter filter;
            // find out which filter was selected
            if (item == this.MenuItemFilterAllImages)
            {
                filter = ImageFilter.All;
            }
            else if (item == this.MenuItemFilterLightImages)
            {
                filter = ImageFilter.Ok;
            }
            else if (item == this.MenuItemFilterCorruptedImages)
            {
                filter = ImageFilter.Corrupted;
            }
            else if (item == this.MenuItemFilterDarkImages)
            {
                filter = ImageFilter.Dark;
            }
            else if (item == this.MenuItemFilterMissingImages)
            {
                filter = ImageFilter.Missing;
            }
            else if (item == this.MenuItemFilterImagesMarkedForDeletion)
            {
                filter = ImageFilter.MarkedForDeletion;
            }
            else
            {
                filter = ImageFilter.All;   // Just in case
            }

            // Treat the checked status as a radio button i.e., toggle their states so only the clicked menu item is checked.
            this.SelectDataTableImagesAndShowImage(Constants.DefaultImageRowIndex, filter);  // Go to the first result (i.e., index 0) in the given filter set
        }

        // helper function to put a checkbox on the currently selected menu item i.e., to make it behave like a radiobutton menu
        private void MenuItemFilterSetSelected(MenuItem checked_item)
        {
            this.MenuItemFilterAllImages.IsChecked = (this.MenuItemFilterAllImages == checked_item) ? true : false;
            this.MenuItemFilterCorruptedImages.IsChecked = (this.MenuItemFilterCorruptedImages == checked_item) ? true : false;
            this.MenuItemFilterDarkImages.IsChecked = (this.MenuItemFilterDarkImages == checked_item) ? true : false;
            this.MenuItemFilterLightImages.IsChecked = (this.MenuItemFilterLightImages == checked_item) ? true : false;
            this.MenuItemFilterImagesMarkedForDeletion.IsChecked = (this.MenuItemFilterImagesMarkedForDeletion == checked_item) ? true : false;
            this.MenuItemView.IsChecked = false;
        }

        // helper function to put a checkbox on the currently selected menu item i.e., to make it behave like a radiobutton menu
        private void MenuItemFilterSetSelected(ImageFilter filter)
        {
            this.MenuItemFilterAllImages.IsChecked = (filter == ImageFilter.All) ? true : false;
            this.MenuItemFilterCorruptedImages.IsChecked = (filter == ImageFilter.Corrupted) ? true : false;
            this.MenuItemFilterDarkImages.IsChecked = (filter == ImageFilter.Dark) ? true : false;
            this.MenuItemFilterLightImages.IsChecked = (filter == ImageFilter.Ok) ? true : false;
            this.MenuItemFilterMissingImages.IsChecked = (filter == ImageFilter.Missing) ? true : false;
            this.MenuItemFilterImagesMarkedForDeletion.IsChecked = (filter == ImageFilter.MarkedForDeletion) ? true : false;
            this.MenuItemFilterCustomFilter.IsChecked = (filter == ImageFilter.Custom) ? true : false;
        }

        private void MenuItemFilterCustomFilter_Click(object sender, RoutedEventArgs e)
        {
            DateTimeOffset defaultDate;
            TimeZoneInfo imageSetTimeZone = this.dataHandler.ImageDatabase.ImageSet.GetTimeZone();
            if (this.dataHandler.ImageCache.Current.TryGetDateTime(imageSetTimeZone, out defaultDate) == false)
            {
                defaultDate = DateTime.UtcNow;
            }
            CustomViewFilter customFilter = new CustomViewFilter(this.dataHandler.ImageDatabase, defaultDate, this);
            customFilter.Owner = this;
            bool? changeToCustomFilter = customFilter.ShowDialog();
            // Set the filter to show all images and a valid image
            if (changeToCustomFilter == true)
            {
                this.SelectDataTableImagesAndShowImage(Constants.DefaultImageRowIndex, ImageFilter.Custom);
            }
            else
            {
                // Resets the checked menu item filter to the currently active filter
                // TODOSAUL: why is a reset necessary when the dialog was cancelled, meaning the active filter wasn't changed?
                this.MenuItemFilterSetSelected(this.dataHandler.ImageDatabase.ImageSet.ImageFilter);
            }
        }

        /// <summary>Display the dialog showing the filtered view of the current database contents</summary>
        private void MenuItemViewFilteredDatabaseContents_Click(object sender, RoutedEventArgs e)
        {
            if (this.dlgDataView != null && this.dlgDataView.IsLoaded)
            {
                this.RefreshDataViewDialogWindow(); // If its already displayed, just refresh it.
                return;
            }
            // We need to create it
            this.dlgDataView = new DataView(this.dataHandler.ImageDatabase, this.dataHandler.ImageCache);
            this.dlgDataView.Owner = this;
            this.dlgDataView.Show();
        }
        #endregion

        #region Help Menu Callbacks
        /// <summary>Display a help window</summary> 
        private void MenuOverview_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Create and show the overview window if it doesn't exist
                if (this.overviewWindow == null)
                {
                    this.overviewWindow = new WindowHelp();
                    this.overviewWindow.Closed += new System.EventHandler(this.OverviewWindow_Closed);
                    this.overviewWindow.Show();
                }
                else
                {
                    // Raise the overview window to the surface if it exists
                    if (this.overviewWindow.WindowState == WindowState.Minimized)
                    {
                        this.overviewWindow.WindowState = WindowState.Normal;
                    }
                    this.overviewWindow.Activate();
                }
            }
            catch (Exception exception)
            {
                Debug.Fail("Overview window failed to open.", exception.ToString());
            }
        }

        // Whem we are done with the overview window, set it to null so we can start afresh
        private void OverviewWindow_Closed(object sender, System.EventArgs e)
        {
            this.overviewWindow = null;
        }

        /// <summary> Display a message describing the version, etc.</summary> 
        private void MenuItemAbout_Click(object sender, RoutedEventArgs e)
        {
            AboutTimelapse about = new AboutTimelapse(this);
            about.ShowDialog();
        }

        /// <summary> Display the Timelapse home page</summary> 
        private void MenuTimelapseWebPage_Click(object sender, RoutedEventArgs e)
        {
            Uri tutorialUri = new Uri("http://saul.cpsc.ucalgary.ca/timelapse/pmwiki.php?n=Main.Version2HomePage");
            Process.Start(new ProcessStartInfo(tutorialUri.AbsoluteUri));
        }

        /// <summary>  Display the manual in a web browser</summary> 
        private void MenuTutorialManual_Click(object sender, RoutedEventArgs e)
        {
            Uri tutorialUri = new Uri("http://saul.cpsc.ucalgary.ca/timelapse/uploads/Installs/Timelapse2/Timelapse2Manual.pdf");
            Process.Start(new ProcessStartInfo(tutorialUri.AbsoluteUri));
        }

        /// <summary> Display the page in the web browser that lets you join the timelapse mailing list </summary> 
        private void MenuJoinTimelapseMailingList_Click(object sender, RoutedEventArgs e)
        {
            Uri tutorialUri = new Uri("http://mailman.ucalgary.ca/mailman/listinfo/timelapse-l");
            Process.Start(new ProcessStartInfo(tutorialUri.AbsoluteUri));
        }

        /// <summary> Download the sample images from a web browser </summary> 
        private void MenuDownloadSampleImages_Click(object sender, RoutedEventArgs e)
        {
            Uri tutorialUri = new Uri("http://saul.cpsc.ucalgary.ca/timelapse/uploads/Main/TutorialImageSet2.zip");
            Process.Start(new ProcessStartInfo(tutorialUri.AbsoluteUri));
        }

        /// <summary>Send mail to the timelapse mailing list</summary> 
        private void MenuMailToTimelapseMailingList_Click(object sender, RoutedEventArgs e)
        {
            Uri tutorialUri = new Uri("mailto:timelapse-l@mailman.ucalgary.ca");
            Process.Start(new ProcessStartInfo(tutorialUri.AbsoluteUri));
        }
        #endregion

        #region Utilities
        // Returns the currently active counter control, otherwise null
        private DataEntryCounter FindSelectedCounter()
        {
            foreach (DataEntryControl control in this.dataEntryControls.Controls)
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
        private bool MaybePromptToApplyOperationIfFilteredView(bool userOptedOutOfMessage, string operationDescription, Action<bool> persistOptOut)
        {
            // if showing all images then no need for showing the warning message
            if (userOptedOutOfMessage || this.dataHandler.ImageDatabase.ImageSet.ImageFilter == ImageFilter.All)
            {
                return true;
            }

            string title = "Apply " + operationDescription + " to this filtered view?";
            MessageBox messageBox = new MessageBox(title, this, MessageBoxButton.OKCancel);

            messageBox.Message.What = operationDescription + " will be applied only to the subset of images shown by the " + this.dataHandler.ImageDatabase.ImageSet.ImageFilter + " filter." + Environment.NewLine;
            messageBox.Message.What += "Is this what you want?";

            messageBox.Message.Reason = "You have the following filter on: " + this.dataHandler.ImageDatabase.ImageSet.ImageFilter + " filter." + Environment.NewLine;
            messageBox.Message.Reason += "Only data for those images available in this " + this.dataHandler.ImageDatabase.ImageSet.ImageFilter + " filter view will be affected" + Environment.NewLine;
            messageBox.Message.Reason += "Data for images not shown in this " + this.dataHandler.ImageDatabase.ImageSet.ImageFilter + " filter view will be unaffected." + Environment.NewLine;

            messageBox.Message.Solution = "Select " + Environment.NewLine;
            messageBox.Message.Solution += "\u2022 'Ok' for Timelapse to continue to " + operationDescription + Environment.NewLine;
            messageBox.Message.Solution += "\u2022 'Cancel' to abort";

            messageBox.Message.Hint = "This is not an error." + Environment.NewLine;
            messageBox.Message.Hint += "\u2022 We are asking just in case you forgot you had the " + this.dataHandler.ImageDatabase.ImageSet.ImageFilter + " filter on. " + Environment.NewLine;
            messageBox.Message.Hint += "\u2022 You can use the 'Filter' menu to change to other filters (including viewing All Images)" + Environment.NewLine;
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
        #endregion

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

        #region Bookmarking pan/zoom levels
        // Bookmark (Save) the current pan / zoom level of the image
        private void MenuItem_BookmarkSavePanZoom(object sender, RoutedEventArgs e)
        {
            this.markableCanvas.BookmarkSaveZoomPan();
        }

        // Restore the zoom level / pan coordinates of the bookmark
        private void MenuItem_BookmarkSetPanZoom(object sender, RoutedEventArgs e)
        {
            this.markableCanvas.BookmarkSetZoomPan();
        }

        // Restore the zoomed out / pan coordinates 
        private void MenuItem_BookmarkDefaultPanZoom(object sender, RoutedEventArgs e)
        {
            this.markableCanvas.BookmarkZoomOutAllTheWay();
        }
        #endregion

        #region Control Window Management
        /// <summary>
        /// Show the Coding Controls in the main timelapse window
        /// </summary>
        private void ControlsInMainWindow()
        {
            if (this.controlWindow != null)
            {
                this.controlWindow.ChildRemove(this.dataEntryControls);
                this.controlWindow.Close();
                this.controlWindow = null;
            }
            else
            {
                this.controlsTray.Children.Remove(this.dataEntryControls);
                this.controlsTray.Children.Add(this.dataEntryControls);
                this.MenuItemControlsInSeparateWindow.IsChecked = false;
            }
        }

        /// <summary>Show the Coding Controls in a separate window</summary>
        private void ControlsInSeparateWindow()
        {
            // this.controlsTray.Children.Clear();
            this.controlsTray.Children.Remove(this.dataEntryControls);

            this.controlWindow = new WindowControl(this.state);    // Handles to the control window and to the controls
            this.controlWindow.Owner = this;             // Keeps this window atop its parent no matter what
            this.controlWindow.Closed += this.ControlWindow_Closing;
            this.controlWindow.AddControls(this.dataEntryControls);
            this.controlWindow.RestorePreviousSize();
            this.controlWindow.Show();
            this.MenuItemControlsInSeparateWindow.IsChecked = true;
        }

        /// <summary>
        /// Callback  invoked when the Control Window is unloaded
        /// If so, make sure the controls are in the main control window
        /// </summary>
        private void ControlWindow_Closing(object sender, EventArgs e)
        {
            this.controlWindow.ChildRemove(this.dataEntryControls);
            this.controlsTray.Children.Remove(this.dataEntryControls);

            this.controlsTray.Children.Add(this.dataEntryControls);
            this.MenuItemControlsInSeparateWindow.IsChecked = false;
        }
        #endregion

        #region DataView Window Management
        private void RefreshDataViewDialogWindow()
        {
            if (this.dlgDataView != null)
            {
                // If its displayed, update the window that shows the filtered view data base
                this.dlgDataView.RefreshDataTable();
            }
        }
        #endregion

        #region Convenience classes
        // A class that tracks our progress as we load the images
        internal class ProgressState
        {
            public string Message { get; set; }
            public BitmapSource Bmap { get; set; }

            public ProgressState()
            {
                this.Message = String.Empty;
                this.Bmap = null;
            }
        }
        #endregion

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
