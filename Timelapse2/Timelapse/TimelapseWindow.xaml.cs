using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Speech.Synthesis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Timelapse.Database;
using Timelapse.Images;
using Timelapse.Util;

namespace Timelapse
{
    /// <summary>
    /// main window for Timelapse
    /// </summary>
    public partial class TimelapseWindow : Window, IDisposable
    {
        // Handles to the controls window and to the controls
        private ControlWindow controlWindow;
        private List<MetaTagCounter> counterCoordinates = null;
        private CustomFilter customfilter;

        private DataEntryControls dataEntryControls;
        private DataEntryHandler dataHandler;
        private bool disposed;

        private string mostRecentImageAddFolderPath;
        private HelpWindow overviewWindow; // Create the help window. 
        private OptionsWindow optionsWindow; // Create the options window
        private MarkableImageCanvas markableCanvas;

        // Status information concerning the state of the UI
        private TimelapseState state = new TimelapseState();

        // Speech feedback
        private SpeechSynthesizer speechSynthesizer = new SpeechSynthesizer();

        // the database that holds the template
        private TemplateDatabase template;

        private DialogDataView dlgDataView;

        #region Constructors, Cleaning up, Destructors
        public TimelapseWindow()
        {
            this.InitializeComponent();

            // Abort if some of the required dependencies are missing
            if (Dependencies.AreRequiredBinariesPresent(Constants.ApplicationName, Assembly.GetExecutingAssembly()) == false)
            {
                Dependencies.ShowMissingBinariesDialog(Constants.ApplicationName);
                Application.Current.Shutdown();
            }

            this.ResetDifferenceThreshold();
            this.markableCanvas = new MarkableImageCanvas();
            this.markableCanvas.HorizontalAlignment = HorizontalAlignment.Stretch;
            this.markableCanvas.PreviewMouseDown += new MouseButtonEventHandler(this.MarkableCanvas_PreviewMouseDown);
            this.markableCanvas.MouseEnter += new MouseEventHandler(this.MarkableCanvas_MouseEnter);
            this.markableCanvas.RaiseMetaTagEvent += new EventHandler<MetaTagEventArgs>(this.MarkableCanvas_RaiseMetaTagEvent);
            this.mainUI.Children.Add(this.markableCanvas);

            // Callbacks so the controls will highlight if they are copyable when one enters the btnCopy button
            this.btnCopy.MouseEnter += this.BtnCopy_MouseEnter;
            this.btnCopy.MouseLeave += this.BtnCopy_MouseLeave;

            // Create data controls, including reparenting the copy button from the main window into the my control window.
            this.dataEntryControls = new DataEntryControls();
            this.ControlGrid.Children.Remove(this.btnCopy);
            this.dataEntryControls.AddButton(this.btnCopy);

            // Recall state from prior sessions
            using (TimelapseRegistryUserSettings userSettings = new TimelapseRegistryUserSettings())
            {
                this.state.AudioFeedback = userSettings.ReadAudioFeedback();
                this.state.ControlWindowSize = userSettings.ReadControlWindowSize();
                this.MenuItemAudioFeedback.IsChecked = this.state.AudioFeedback;
                this.MenuItemControlsInSeparateWindow.IsChecked = userSettings.ReadControlsInSeparateWindow();
                this.state.DarkPixelThreshold = userSettings.ReadDarkPixelThreshold();
                this.state.DarkPixelRatioThreshold = userSettings.ReadDarkPixelRatioThreshold();
                // SAULTODO: Delete the code saving CSV state across sessions, as this state is only per session. this.state.ShowCsvDialog = userSettings.ReadShowCsvDialog();
                this.state.MostRecentImageSets = userSettings.ReadMostRecentImageSets();  // the last path opened by the user is stored in the registry
            }

            // populate the most recent databases list
            this.MenuItemRecentImageSets_Refresh();
        }

        public byte DifferenceThreshold { get; set; } // The threshold used for calculating combined differences

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
                this.speechSynthesizer.Dispose();
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
                // Save the following in the database as they are local to this image set
                if (this.state.ImageFilter == ImageQualityFilter.Custom)
                {
                    // don't save custom filters, revert to All 
                    this.state.ImageFilter = ImageQualityFilter.All;
                }
                this.dataHandler.ImageDatabase.SetImageSetFilter(this.state.ImageFilter);

                if (this.dataHandler.ImageCache != null)
                {
                    this.dataHandler.ImageDatabase.SetImageSetRowIndex(this.dataHandler.ImageCache.CurrentRow);
                }

                if (this.markableCanvas != null)
                {
                    this.dataHandler.ImageDatabase.SetMagnifierEnabled(this.markableCanvas.IsMagnifyingGlassVisible);
                }
            }

            // Save the current filter set and the index of the current image being viewed in that set, and save it into the registry
            using (TimelapseRegistryUserSettings userSettings = new TimelapseRegistryUserSettings())
            {
                userSettings.WriteAudioFeedback(this.state.AudioFeedback);
                userSettings.WriteControlWindowSize(this.state.ControlWindowSize);
                userSettings.WriteControlsInSeparateWindow(this.MenuItemControlsInSeparateWindow.IsChecked);
                userSettings.WriteDarkPixelThreshold(this.state.DarkPixelThreshold);
                userSettings.WriteDarkPixelRatioThreshold(this.state.DarkPixelRatioThreshold);
                userSettings.WriteMostRecentImageSets(this.state.MostRecentImageSets);
                // SAULTODO: DELETE THIS AS THIS IS SHOULD NOT BE REMEMBERED BETWEEN SESSIONS. userSettings.WriteShowCsvDialog(this.state.ShowCsvDialog);
            }

            if (this.controlWindow != null)
            {
                this.controlWindow.Close();
            }
            if (this.dlgDataView != null)
            {
                this.dlgDataView.Close();
            }
        }
        #endregion

        #region Image Loading
        private bool TryGetTemplatePath(out string templateDatabasePath)
        {
            // prompt user to select a template
            // default the template selection dialog to the most recently opened database
            string defaultTemplateDatabasePath;
            this.state.MostRecentImageSets.TryGetMostRecent(out defaultTemplateDatabasePath);
            if (Utilities.TryGetFileFromUser("Select a TimelapseTemplate.tdb file, which should be one located in your image folder",
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
        /// <returns>true if the template and images were loaded, false otherwise</returns>
        internal bool TryOpenTemplateAndLoadImages(string templateDatabasePath)
        {
            // Create the template to the Timelapse Template database
            if (!TemplateDatabase.TryCreateOrOpen(templateDatabasePath, out this.template))
            {
                this.OnImageDatabaseNotLoaded();
                // notify the user the template couldn't be loaded rather than silently doing nothing
                DialogMessageBox messageBox = new DialogMessageBox();
                DialogMessageBox dlgMB = new DialogMessageBox();
                dlgMB.MessageTitle = "Timelapse could not load the template.";
                dlgMB.MessageProblem = "Timelapse could not load the Template File:" + Environment.NewLine;
                dlgMB.MessageProblem += "\u2022 " + templateDatabasePath;
                dlgMB.MessageReason = "The template may be corrupted or somehow otherwise invalid. ";
                dlgMB.MessageSolution = "You may have to recreate the template, or use another copy of it (if you have one).";
                dlgMB.MessageResult = "Timelapse won't do anything. You can try to select another template file.";
                dlgMB.MessageHint = "See if you can examine the template file in the Timelapse Template Editor.";
                dlgMB.MessageHint += "If you can't, there is likley something wrong with it and you will have to recreate it.";
                dlgMB.ButtonType = MessageBoxButton.OK;
                dlgMB.IconType = MessageBoxImage.Error;
                dlgMB.ShowDialog();
                return false;
            }

            // Find the .ddb file in the image set folder. If a single .ddb file is found, use that one
            // If there are multiple .ddb files, ask the use to choose one and use that
            // However, if the user cancels that choice, just abort.
            string imageDatabaseFilePath;
            bool importImages;
            if (this.TrySelectDatabaseFile(templateDatabasePath, out imageDatabaseFilePath, out importImages) == false)
            {
                this.OnImageDatabaseNotLoaded();
                return false;
            }

            // When we are loading from an existing image database, ensure that the template in the template database matches the template stored in
            // the image database
            ImageDatabase imageDatabase = ImageDatabase.CreateOrOpen(imageDatabaseFilePath, this.template);
            if (imageDatabase.TemplateSynchronizationIssues.Count > 0)
            {
                DialogTemplatesDontMatch dlg = new DialogTemplatesDontMatch(imageDatabase.TemplateSynchronizationIssues);
                dlg.Owner = this;
                bool? result = dlg.ShowDialog();
                if (result == true)
                {
                    // user indicated not to update to the current template so exit
                    Application.Current.Shutdown();
                    return false;
                }
                // user indicated to run with the stale copy of the template in the image database
            }

            this.dataHandler = new DataEntryHandler(imageDatabase);
            if (importImages)
            {
                if (this.LoadByScanningImageFolder(this.FolderPath) == false)
                {
                    // revert UI to no database loaded state
                    this.OnImageDatabaseNotLoaded();
                    return false;
                }
            }

            // generate and render the data entry controls
            this.dataEntryControls.Generate(imageDatabase, this.dataHandler);
            this.SetUserInterfaceCallbacks();
            this.MenuItemControlsInSeparateWindow_Click(this.MenuItemControlsInSeparateWindow, null);

            this.state.MostRecentImageSets.SetMostRecent(templateDatabasePath);
            this.MenuItemRecentImageSets_Refresh();
            this.OnImageLoadingComplete();
            return true;
        }

        private bool LoadByScanningImageFolder(string imageFolderPath)
        {
            FileInfo[] imageFilePaths = new DirectoryInfo(imageFolderPath).GetFiles("*.jpg");
            int count = imageFilePaths.Length;
            if (count == 0)
            {
                // no images were found in folder; see if user wants to try again
                DialogMessageBox messageBox = new DialogMessageBox();
                messageBox.MessageTitle = "No images found in the image set folder.";
                messageBox.MessageProblem = "There don't seem to be any JPG images in your chosen image folder:" + Environment.NewLine;
                messageBox.MessageProblem += "\u2022 " + this.FolderPath + Environment.NewLine;
                messageBox.MessageReason = "\u2022 The folder has no JPG files in it (image files ending in '.jpg'), or" + Environment.NewLine;
                messageBox.MessageReason += "\u2022 You may have selected the wrong folder, i.e., a folder other than the one containing the images.";
                messageBox.MessageSolution = "Check that the chosen folder actually contains JPG images (i.e., a 'jpg' suffix), or" + Environment.NewLine;
                messageBox.MessageSolution += "open a different folder." + Environment.NewLine;
                messageBox.MessageResult += "Would you like to choose another folder?";
                messageBox.IconType = MessageBoxImage.Question;
                messageBox.ButtonType = MessageBoxButton.YesNo;
                if (messageBox.ShowDialog() == false)
                {
                    return false;
                }

                string folderPath;
                if (this.ShowFolderSelectionDialog(out folderPath))
                {
                    return this.LoadByScanningImageFolder(folderPath);
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
                    this.helpControl.Visibility = Visibility.Collapsed;
                    this.Feedback(null, 0, "Examining images...");
                }));

                // First pass: Examine images to extract their basic properties and build a list of images not already in the database
                List<ImageProperties> imagesToInsert = new List<ImageProperties>();
                for (int image = 0; image < count; image++)
                {
                    FileInfo imageFile = imageFilePaths[image];
                    ImageProperties imageProperties = new ImageProperties(this.FolderPath, imageFile);
                    DataRow imageRow;
                    if (this.dataHandler.ImageDatabase.TryGetImage(imageProperties, out imageRow))
                    {
                        // the database already has an entry for this image so skip it
                        // if needed, a separate list of images to update could be generated
                        continue;
                    }

                    BitmapFrame bitmapFrame = null;
                    try
                    {
                        // Create the bitmap and determine its ImageQuality
                        // avoid ImageProperties.LoadImage() here as the create exception needs to surface to set the image quality to corrupt
                        // framework bug: WriteableBitmap.Metadata returns null rather than metatada offered by the underlying BitmapFrame, so 
                        // retain the frame and pass its metadata to TryUseImageTaken().
                        bitmapFrame = imageProperties.LoadBitmapFrame(this.FolderPath);
                        WriteableBitmap bitmap = new WriteableBitmap(bitmapFrame);
                        imageProperties.ImageQuality = bitmap.GetImageQuality(this.state.DarkPixelThreshold, this.state.DarkPixelRatioThreshold);

                        // see if the date can be updated from the metadata
                        DateTimeAdjustment imageTimeAdjustment = imageProperties.TryUseImageTaken((BitmapMetadata)bitmapFrame.Metadata);
                        if (imageTimeAdjustment == DateTimeAdjustment.MetadataDateAndTimeUsed ||
                            imageTimeAdjustment == DateTimeAdjustment.MetadataDateUsed)
                        {
                            DateTime imageTaken = imageProperties.GetDateTime();
                            if (imageTaken.Day < 13)
                            {
                                unambiguousDayMonthOrder = false;
                            }
                        }
                    }
                    catch (Exception exception)
                    {
                        Debug.Assert(false, String.Format("Load of {0} failed as it's likely corrupted.", imageProperties.FileName), exception.ToString());
                        bitmapFrame = Constants.Images.Corrupt;
                        imageProperties.ImageQuality = ImageQualityFilter.Corrupted;
                    }

                    imagesToInsert.Add(imageProperties);

                    if (image == 0 || (image % Constants.FolderScanProgressUpdateFrequency == 0))
                    {
                        progressState.Message = String.Format("{0}/{1}: Examining {2}", image, count, imageProperties.FileName);
                        progressState.Bmap = bitmapFrame;
                        int progress = Convert.ToInt32(Convert.ToDouble(image) / Convert.ToDouble(count) * 100);
                        backgroundWorker.ReportProgress(progress, progressState);
                    }
                    else
                    {
                        progressState.Bmap = null;
                    }
                }

                // Second pass: Determine dates ... This can be pretty quick, so we don't really need to give any feedback on it.
                progressState.Message = "Second pass";
                progressState.Bmap = null;
                backgroundWorker.ReportProgress(0, progressState);

                // Third pass: Update database
                // TODOSAUL This used to be slow... but I think its ok now. But check if its a good place to make it more efficient by having it add multiple values in one shot (it may already be doing that - if so, delete this comment)
                this.dataHandler.ImageDatabase.AddImages(imagesToInsert, (ImageProperties imageProperties, int imageIndex) =>
                {
                    // Get the bitmap again to show it
                    // WriteableBitmap bitmap = imageProperties.LoadWriteableBitmap(this.FolderPath);
                    BitmapFrame bitmapFrame = imageProperties.LoadBitmapFrame(this.FolderPath);
                    // Show progress. Since its slow, we may as well do it every update
                    int addImageProgress = (int)((double)imageIndex / (100.0 * (double)imagesToInsert.Count));
                    progressState.Message = String.Format("{0}/{1}: Adding {2}", imageIndex, count, imageProperties.FileName);
                    progressState.Bmap = bitmapFrame;
                    backgroundWorker.ReportProgress(addImageProgress, progressState);
                });
            };
            backgroundWorker.ProgressChanged += (o, ea) =>
            {
                // this gets called on the UI thread
                ProgressState progstate = (ProgressState)ea.UserState;
                this.Feedback(progressState.Bmap, ea.ProgressPercentage, progressState.Message);
                this.feedbackCtl.Visibility = Visibility.Visible;
            };
            backgroundWorker.RunWorkerCompleted += (o, ea) =>
            {
                // this.dbData.GetImagesAll(); // Now load up the data table
                // Get rid of the feedback panel, and show the main interface
                this.feedbackCtl.Visibility = Visibility.Collapsed;
                this.feedbackCtl.ShowImage = null;

                this.markableCanvas.Visibility = Visibility.Visible;

                // warn the user if there are any ambiguous dates in terms of day/month or month/day order
                if (unambiguousDayMonthOrder == false)
                {
                    DialogMessageBox dlgMB = new DialogMessageBox();
                    dlgMB.MessageTitle = "Timelapse was unsure about the month / day order of your image's dates.";
                    dlgMB.MessageProblem = "Timelapse is extracting the dates from your images. However, it cannot tell if the dates are in day/month order, or month/day order.";
                    dlgMB.MessageReason = "Image date formats can be ambiguous. For example, is 2015/03/05 March 5 or May 3?";
                    dlgMB.MessageSolution = "If Timelapse gets it wrong, you can correct the dates by choosing" + Environment.NewLine;
                    dlgMB.MessageSolution += "\u2022 Edit Menu -> Dates -> Swap Day and Month.";
                    dlgMB.MessageHint = "If you are unsure about the correct date, try the following." + Environment.NewLine;
                    dlgMB.MessageHint += "\u2022 If your camera prints the date on the image, check that." + Environment.NewLine;
                    dlgMB.MessageHint += "\u2022 Look at the images to see what season it is (e.g., winter vs. summer)." + Environment.NewLine;
                    dlgMB.MessageHint += "\u2022 Examine the creation date of the image file." + Environment.NewLine;
                    dlgMB.MessageHint += "\u2022 Check your own records.";
                    dlgMB.ButtonType = MessageBoxButton.OK;
                    dlgMB.IconType = MessageBoxImage.Information;
                    dlgMB.ShowDialog();
                }
                this.OnImageLoadingComplete();

                // Finally, tell the user how many images were loaded, etc.
                this.MenuItemImageCounts_Click(null, null);

                // If we want to import old data from the ImageData.xml file, we can do it here...
                // Check to see if there is an ImageData.xml file in here. If there is, ask the user
                // if we want to load the data from that...
                if (File.Exists(Path.Combine(this.FolderPath, Constants.File.XmlDataFileName)))
                {
                    DialogImportImageSetXmlFile importLegacyXmlDialog = new DialogImportImageSetXmlFile();
                    importLegacyXmlDialog.Owner = this;
                    bool? dialogResult = importLegacyXmlDialog.ShowDialog();
                    if (dialogResult == true)
                    {
                        ImageDataXml.Read(Path.Combine(this.FolderPath, Constants.File.XmlDataFileName), this.dataHandler.ImageDatabase);
                        this.SetImageFilterAndIndex(this.dataHandler.ImageDatabase.GetImageSetRowIndex(), this.dataHandler.ImageDatabase.GetImageSetFilter()); // to regenerate the controls and markers for this image
                    }
                }
            };

            backgroundWorker.RunWorkerAsync();
            return true;
        }

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
                DialogChooseDatabaseFile chooseDatabaseFile = new DialogChooseDatabaseFile(files);
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
            this.feedbackCtl.ShowMessage = message;
            this.feedbackCtl.ShowProgress = percent;
            if (bmap != null)
            {
                this.feedbackCtl.ShowImage = bmap;
            }
        }

        /// <summary>
        /// When image loading has completed add callbacks, prepare the UI, set up the image set, and show the image.
        /// </summary>
        private void OnImageLoadingComplete()
        {
            // Create a Custom Filter, which will hold the current custom filter expression (if any) that may be set in the DialogCustomViewFilter
            this.customfilter = new CustomFilter(this.dataHandler.ImageDatabase);

            // Set the magnifying glass status from the registry. 
            // Note that if it wasn't in the registry, the value returned will be true by default
            this.markableCanvas.IsMagnifyingGlassVisible = this.dataHandler.ImageDatabase.IsMagnifierEnabled();

            // Now that we have something to show, enable menus and menu items as needed
            // Note that we do not enable those menu items that would have no effect
            this.MenuItemAddImagesToImageSet.IsEnabled = true;
            this.MenuItemLoadImages.IsEnabled = false;
            this.MenuItemExportThisImage.IsEnabled = true;
            this.MenuItemExportAsCsvAndPreview.IsEnabled = true;
            this.MenuItemExportAsCsv.IsEnabled = true;
            this.MenuItemImportFromCsv.IsEnabled = true;
            this.MenuItemRecentImageSets.IsEnabled = false;
            this.MenuItemRenameImageDatabaseFile.IsEnabled = true;
            this.MenuItemEdit.IsEnabled = true;
            this.MenuItemDeleteImage.IsEnabled = true;
            this.MenuItemView.IsEnabled = true;
            this.MenuItemFilter.IsEnabled = true;
            this.MenuItemOptions.IsEnabled = true;

            this.MenuItemMagnifier.IsChecked = this.markableCanvas.IsMagnifyingGlassVisible;

            // Also adjust the visibility of the various other UI components.
            this.btnCopy.Visibility = Visibility.Visible;
            this.controlsTray.Visibility = Visibility.Visible;
            this.DockPanelNavigator.Visibility = Visibility.Visible;
            this.helpControl.Visibility = Visibility.Collapsed;

            // Set the image set filter to all images. This should also set the correct count, etc. 
            StatusBarUpdate.View(this.statusBar, "all images.");

            // Show the image, hide the load button, and make the feedback panels visible
            this.ImageNavigatorSlider_EnableOrDisableValueChangedCallback(false);
            this.markableCanvas.Focus(); // We start with this having the focus so it can interpret keyboard shortcuts if needed. 

            // set the current filter and the image index to the same as the ones in the last session, providing that we are working 
            // with the same image folder. 
            // Doing so also displays the image
            this.SetImageFilterAndIndex(this.dataHandler.ImageDatabase.GetImageSetRowIndex(), this.dataHandler.ImageDatabase.GetImageSetFilter());

            if (FileBackup.TryCreateBackups(this.FolderPath, this.dataHandler.ImageDatabase.FileName))
            {
                StatusBarUpdate.Message(this.statusBar, "Backups of files made.");
            }
            else
            {
                StatusBarUpdate.Message(this.statusBar, "No file backups were made.");
            }
        }

        /// <summary>
        /// If an image set could not be opened or the user canceled loading revert the UI to its initial state so the user can try
        /// loading another image set and isn't presented with menu options applicable only when an image set is open.
        /// </summary>
        private void OnImageDatabaseNotLoaded()
        {
            this.MenuItemAddImagesToImageSet.IsEnabled = false;
            this.MenuItemLoadImages.IsEnabled = true;
            this.MenuItemExportThisImage.IsEnabled = false;
            this.MenuItemExportAsCsvAndPreview.IsEnabled = false;
            this.MenuItemExportAsCsv.IsEnabled = false;
            this.MenuItemImportFromCsv.IsEnabled = false;
            this.MenuItemRecentImageSets.IsEnabled = true;
            this.MenuItemRenameImageDatabaseFile.IsEnabled = false;
            this.MenuItemEdit.IsEnabled = false;
            this.MenuItemDeleteImage.IsEnabled = false;
            this.MenuItemView.IsEnabled = false;
            this.MenuItemFilter.IsEnabled = false;
            this.MenuItemOptions.IsEnabled = false;
        }
        #endregion

        #region Filters
        private bool SetImageFilterAndIndex(int defaultImageRow, ImageQualityFilter filter)
        {
            bool imagesAvailableWithFilter;
            switch (filter)
            {
                case ImageQualityFilter.All:
                case ImageQualityFilter.Corrupted:
                case ImageQualityFilter.Dark:
                case ImageQualityFilter.MarkedForDeletion:
                case ImageQualityFilter.Missing:
                case ImageQualityFilter.Ok:
                    imagesAvailableWithFilter = this.dataHandler.ImageDatabase.TryGetImages(filter);
                    break;
                case ImageQualityFilter.Custom:
                    imagesAvailableWithFilter = this.customfilter.GetImageCount() != 0;
                    break;
                default:
                    throw new NotSupportedException(String.Format("Unhandled image quality filter {0}.", filter));
            }

            if (imagesAvailableWithFilter || filter == ImageQualityFilter.All)
            {
                // Change the filter to reflect what the user selected. Update the menu state accordingly
                // Set the checked status of the radio button menu items to the filter.
                string status;
                switch (filter)
                {
                    case ImageQualityFilter.All:
                        status = "all images.";
                        break;
                    case ImageQualityFilter.Corrupted:
                        status = "corrupted images.";
                        break;
                    case ImageQualityFilter.Custom:
                        status = "images matching your custom filter.";
                        break;
                    case ImageQualityFilter.Dark:
                        status = "dark images.";
                        break;
                    case ImageQualityFilter.MarkedForDeletion:
                        status = "images marked for deletion.";
                        break;
                    case ImageQualityFilter.Missing:
                        status = "missing images.";
                        break;
                    case ImageQualityFilter.Ok:
                        status = "light images.";
                        break;
                    default:
                        throw new NotSupportedException(String.Format("Unhandled image quality filter {0}.", filter));
                }

                StatusBarUpdate.View(this.statusBar, status);
                this.MenuItemViewSetSelected(filter);
                if (this.dlgDataView != null)
                {
                    this.dlgDataView.RefreshDataTable();  // If its displayed, update the window that shows the filtered view data base
                    if (filter == ImageQualityFilter.MarkedForDeletion)
                    {
                        this.MenuItemViewFilteredDatabaseContents_Click(null, null); // Regenerate the DataView if needed
                    }
                }
            }
            else
            {
                // other than custom filters these cases shouldn't be reachable as the menu options for the filters will be disabled if there aren't any
                // corresponding images
                DialogMessageBox messageBox = new DialogMessageBox();
                if (filter == ImageQualityFilter.Corrupted)
                {
                    StatusBarUpdate.Message(this.statusBar, "no corrupted images to display.");
                    messageBox.MessageTitle = "Corrupted filter selected, but no images are marked as corrupted.";
                    messageBox.MessageProblem = "None of the images in this image set are corrupted images, so nothing can be shown.";
                    messageBox.MessageReason = "None of the images have their 'ImageQuality' field set to Corrupted.";
                    messageBox.MessageHint = "If you have images you think should be marked as 'Corrupted', set their ImageQuality field to Corrupted.";
                }
                else if (filter == ImageQualityFilter.Custom)
                {
                    StatusBarUpdate.Message(this.statusBar, "no images to display.");
                    messageBox.MessageTitle = "Custom filter selected, but no images match the specified search.";
                    messageBox.MessageProblem = "None of the images in this image set match the specified search, so nothing can be shown.";
                    messageBox.MessageHint = "Try to create another custom filter.";
                }
                else if (filter == ImageQualityFilter.Dark)
                {
                    StatusBarUpdate.Message(this.statusBar, "no dark images to display.");
                    messageBox.MessageTitle = "Dark filter selected, but no images are marked as dark.";
                    messageBox.MessageProblem = "None of the images in this image set are dark images, so nothing can be shown.";
                    messageBox.MessageReason = "None of the images have their 'ImageQuality' field set to Dark.";
                    messageBox.MessageHint = "If you have images you think should be marked as 'Dark', set their ImageQuality field to Dark.";
                }
                else if (filter == ImageQualityFilter.Missing)
                {
                    StatusBarUpdate.Message(this.statusBar, "no missing images to display.");
                    messageBox.MessageTitle = "Missing filter selected, but no images are marked as missing.";
                    messageBox.MessageProblem = "None of the images in this image set are missing images, so nothing can be shown.";
                    messageBox.MessageReason = "None of the images have their 'ImageQuality' field set to Missing.";
                    messageBox.MessageHint = "If you have images you think should be marked as 'Missing', set their ImageQuality field to Missing.";
                }
                else if (filter == ImageQualityFilter.MarkedForDeletion)
                {
                    StatusBarUpdate.Message(this.statusBar, "No images marked for deletion to display.");
                    messageBox.MessageTitle = "Delete filter selected, but no images are marked for deletion";
                    messageBox.MessageProblem = "None of the images in this image set are marked for deletion, so nothing can be shown.";
                    messageBox.MessageReason = "None of the images have their 'Delete?' field checked.";
                    messageBox.MessageHint = "If you have images you think should be marked for deletion, check their Delete? field.";
                }
                else if (filter == ImageQualityFilter.Ok)
                {
                    StatusBarUpdate.Message(this.statusBar, "no light images to display.");
                    messageBox.MessageTitle = "Light filter selected, but no images are marked as light.";
                    messageBox.MessageProblem = "None of the images in this image set are light images, so nothing can be shown.";
                    messageBox.MessageReason = "None of the images have their 'ImageQuality' field set to OK.";
                    messageBox.MessageHint = "If you have images you think should be marked as 'light', set their ImageQuality field to OK.";
                }
                else
                {
                    throw new NotSupportedException(String.Format("Unhandled image quality filter {0}.", filter));
                }
                messageBox.ButtonType = MessageBoxButton.OK;
                messageBox.IconType = MessageBoxImage.Information;
                messageBox.MessageResult = "The filter will not be applied.";
                messageBox.ShowDialog();

                if (this.state.ImageFilter == filter)
                {
                    return this.SetImageFilterAndIndex(Constants.DefaultImageRowIndex, ImageQualityFilter.All);
                }
                this.MenuItemViewSetSelected(this.state.ImageFilter);
                return false;
            }

            // Display the first available image under the new filter
            if (this.dataHandler.ImageDatabase.CurrentlySelectedImageCount > 0)
            {
                // this.ShowFirstDisplayableImage(defaultImageRow); // SAULTODO: It used to be this call, but changed it to ShowImage. Check, but seems to work.
                this.ShowImage(defaultImageRow);
            }

            // After a filter change, set the slider to represent the index and the count of the current filter
            this.ImageNavigatorSlider_EnableOrDisableValueChangedCallback(false);
            this.ImageNavigatorSlider.Maximum = this.dataHandler.ImageDatabase.CurrentlySelectedImageCount - 1;  // Reset the slider to the size of images in this set
            this.ImageNavigatorSlider.Value = this.dataHandler.ImageCache.CurrentRow;

            // Update the status bar accordingly
            StatusBarUpdate.CurrentImageNumber(this.statusBar, this.dataHandler.ImageCache.CurrentRow + 1);  // We add 1 because its a 0-based list
            StatusBarUpdate.TotalCount(this.statusBar, this.dataHandler.ImageDatabase.CurrentlySelectedImageCount);
            this.ImageNavigatorSlider_EnableOrDisableValueChangedCallback(true);
            this.state.ImageFilter = filter;    // Remember the current filter
            return true;
        }

        #endregion

        #region Control Callbacks
        /// <summary>
        /// Add user interface event handler callbacks for (possibly invisible) controls
        /// </summary>
        private void SetUserInterfaceCallbacks()
        {
            // Add data entry callbacks to all editable controls. When the user changes an image's attribute using a particular control,
            // the callback updates the matching field for that image in the database.
            foreach (KeyValuePair<string, DataEntryControl> pair in this.dataEntryControls.ControlsByDataLabel)
            {
                string controlType = this.dataHandler.ImageDatabase.ImageDataColumnsByDataLabel[pair.Key].ControlType;
                switch (controlType)
                {
                    case Constants.DatabaseColumn.File:
                    case Constants.DatabaseColumn.RelativePath:
                    case Constants.DatabaseColumn.Folder:
                    case Constants.DatabaseColumn.Time:
                    case Constants.DatabaseColumn.Date:
                    case Constants.Control.Note:
                        DataEntryNote note = (DataEntryNote)pair.Value;
                        note.ContentControl.PreviewKeyDown += this.ContentCtl_PreviewKeyDown;
                        break;
                    case Constants.Control.DeleteFlag:
                    case Constants.Control.Flag:
                        DataEntryFlag flag = (DataEntryFlag)pair.Value;
                        flag.ContentControl.PreviewKeyDown += this.ContentCtl_PreviewKeyDown;
                        break;
                    case Constants.DatabaseColumn.ImageQuality:
                    case Constants.Control.FixedChoice:
                        DataEntryChoice choice = (DataEntryChoice)pair.Value;
                        choice.ContentControl.PreviewKeyDown += this.ContentCtl_PreviewKeyDown;
                        break;
                    case Constants.Control.Counter:
                        DataEntryCounter counter = (DataEntryCounter)pair.Value;
                        counter.ContentControl.PreviewKeyDown += this.ContentCtl_PreviewKeyDown;
                        counter.ContentControl.PreviewTextInput += this.CounterCtl_PreviewTextInput;
                        counter.Container.MouseEnter += this.CounterControl_MouseEnter;
                        counter.Container.MouseLeave += this.CounterControl_MouseLeave;
                        counter.LabelControl.Click += this.CounterControl_Click;
                        break;
                    default:
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
                // The false means don't check to see if a textbox or control has the focus, as we want to reset the focus elsewhere
                this.SetTopLevelFocus(false, eventArgs);
                eventArgs.Handled = true;
            }
        }

        /// <summary>Preview callback for counters, to ensure ensure that we only accept numbers</summary>
        /// <param name="sender">the event source</param>
        /// <param name="e">event information</param>
        private void CounterCtl_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !this.IsAllValidNumericChars(e.Text);
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
                if (typeof(CheckBox) == type ||
                    typeof(ComboBox) == type ||
                    typeof(ComboBoxItem) == type ||
                    typeof(TextBox) == type)
                {
                    DataEntryControl focusedControl = (DataEntryControl)((Control)focusedElement).Tag;
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
                    control.Focus();
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
        private void BtnCopy_MouseEnter(object sender, MouseEventArgs e)
        {
            foreach (KeyValuePair<string, DataEntryControl> pair in this.dataEntryControls.ControlsByDataLabel)
            {
                DataEntryControl control = (DataEntryControl)pair.Value;
                if (control.Copyable)
                {
                    SolidColorBrush brush = new SolidColorBrush(Color.FromArgb(255, (byte)200, (byte)251, (byte)200));
                    control.Container.Background = brush;
                }
                break;
            }
        }

        /// <summary>
        ///  When the mouse enters / leaves the copy button, the controls that are copyable will be highlighted. 
        /// </summary>
        private void BtnCopy_MouseLeave(object sender, MouseEventArgs e)
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
                    StatusBarUpdate.Message(this.statusBar, String.Format("Image is {0}.", this.dataHandler.ImageCache.Current.ImageQuality));
                }
                else
                {
                    StatusBarUpdate.ClearMessage(this.statusBar);
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
                        StatusBarUpdate.Message(this.statusBar, "Differences can't be shown unless the current image be loaded");
                        return;
                    case ImageDifferenceResult.NextImageNotAvailable:
                    case ImageDifferenceResult.PreviousImageNotAvailable:
                        StatusBarUpdate.Message(this.statusBar, String.Format("View of differences compared to {0} image not available", this.dataHandler.ImageCache.CurrentDifferenceState == ImageDifference.Previous ? "previous" : "next"));
                        return;
                    case ImageDifferenceResult.NotCalculable:
                        StatusBarUpdate.Message(this.statusBar, String.Format("{0} image is not compatible with {1}", this.dataHandler.ImageCache.CurrentDifferenceState == ImageDifference.Previous ? "Previous" : "Next", this.dataHandler.ImageCache.Current.FileName));
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
            StatusBarUpdate.Message(this.statusBar, "Viewing differences compared to " + (this.dataHandler.ImageCache.CurrentDifferenceState == ImageDifference.Previous ? "previous" : "next") + " image");
        }

        private void ViewCombinedDifference()
        {
            // If we are in any state other than the unaltered state, go to the unaltered state, otherwise the combined diff state
            this.dataHandler.ImageCache.MoveToNextStateInCombinedDifferenceCycle();
            if (this.dataHandler.ImageCache.CurrentDifferenceState != ImageDifference.Combined)
            {
                this.markableCanvas.ImageToDisplay.Source = this.dataHandler.ImageCache.GetCurrentImage();
                this.markableCanvas.ImageToMagnify.Source = this.markableCanvas.ImageToDisplay.Source;
                StatusBarUpdate.ClearMessage(this.statusBar);
                return;
            }

            // Generate the differenced image if it's not cached
            if (this.dataHandler.ImageCache.GetCurrentImage() == null)
            {
                ImageDifferenceResult result = this.dataHandler.ImageCache.TryCalculateCombinedDifference(this.DifferenceThreshold);
                switch (result)
                {
                    case ImageDifferenceResult.CurrentImageNotAvailable:
                        StatusBarUpdate.Message(this.statusBar, "Combined differences can't be shown unless the current image be loaded");
                        return;
                    case ImageDifferenceResult.NextImageNotAvailable:
                        StatusBarUpdate.Message(this.statusBar, "Combined differences can't be shown unless the next image can be loaded");
                        return;
                    case ImageDifferenceResult.NotCalculable:
                        StatusBarUpdate.Message(this.statusBar, String.Format("Previous or next image is not compatible with {0}", this.dataHandler.ImageCache.Current.FileName));
                        return;
                    case ImageDifferenceResult.PreviousImageNotAvailable:
                        StatusBarUpdate.Message(this.statusBar, "Combined differences can't be shown unless the previous image can be loaded");
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
            StatusBarUpdate.Message(this.statusBar, "Viewing differences compared to both the next and previous images");
        }
        #endregion

        #region Slider Event Handlers and related
        private void ImageNavigatorSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.ShowImage((int)ImageNavigatorSlider.Value);
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
        #endregion

        #region Showing images
        private void ShowFirstDisplayableImage(int firstRowInSearch)
        {
            int firstImageDisplayable = this.dataHandler.ImageDatabase.FindFirstDisplayableImage(firstRowInSearch);
            if (firstImageDisplayable != -1)
            {
                this.ShowImage(firstImageDisplayable);
            }
            // TODOSAUL: what if there's no displayable image?
            // I tested this, and it seems that this code would not be triggered anyways, so perhaps nothing needs to be done about this. 
        }

        // Show the image in the current row, forcing a refresh of that image. 
        private void ShowImage(int newImageRow)
        {
            // for the bitmap caching logic below to work this should be the only place where code in TimelapseWindow moves the image enumerator
            bool newImageToDisplay;
            if (this.dataHandler.ImageCache.TryMoveToImage(newImageRow, out newImageToDisplay) == false)
            {
                throw new ArgumentOutOfRangeException("newImageRow", String.Format("{0} is not a valid row index in the image table.", newImageRow));
            }

            // For each control, we get its type and then update its contents from the current data table row
            // this is always done as it's assumed either the image changed or that a control refresh is required due to database changes
            // the call to TryMoveToImage() above refreshes the data stored under this.dataHandler.ImageCache.Current
            this.dataHandler.IsProgrammaticControlUpdate = true;
            foreach (KeyValuePair<string, DataEntryControl> control in this.dataEntryControls.ControlsByDataLabel)
            {
                string controlType = this.dataHandler.ImageDatabase.ImageDataColumnsByDataLabel[control.Key].ControlType;
                switch (controlType)
                {
                    case Constants.DatabaseColumn.File:
                        control.Value.Content = this.dataHandler.ImageCache.Current.FileName;
                        break;
                    case Constants.DatabaseColumn.RelativePath:
                        control.Value.Content = this.dataHandler.ImageCache.Current.RelativePath;
                        break;
                    case Constants.DatabaseColumn.Folder:
                        control.Value.Content = this.dataHandler.ImageCache.Current.InitialRootFolderName;
                        break;
                    case Constants.DatabaseColumn.Time:
                        control.Value.Content = this.dataHandler.ImageCache.Current.Time;
                        break;
                    case Constants.DatabaseColumn.Date:
                        control.Value.Content = this.dataHandler.ImageCache.Current.Date;
                        break;
                    case Constants.DatabaseColumn.ImageQuality:
                        control.Value.Content = this.dataHandler.ImageCache.Current.ImageQuality.ToString();
                        break;
                    case Constants.Control.Counter:
                    case Constants.Control.DeleteFlag:
                    case Constants.Control.FixedChoice:
                    case Constants.Control.Flag:
                    case Constants.Control.Note:
                        control.Value.Content = this.dataHandler.ImageDatabase.GetImageValue(this.dataHandler.ImageCache.CurrentRow, control.Value.DataLabel);
                        break;
                    default:
                        break;
                }
            }
            this.dataHandler.IsProgrammaticControlUpdate = false;

            // update the status bar to show which image we are on out of the total displayed under the current filter
            // the total is always refreshed as it's not known if ShowImage() is being called due to a change in filtering
            StatusBarUpdate.CurrentImageNumber(this.statusBar, this.dataHandler.ImageCache.CurrentRow + 1); // Add one because indexes are 0-based
            StatusBarUpdate.TotalCount(this.statusBar, this.dataHandler.ImageDatabase.CurrentlySelectedImageCount);
            StatusBarUpdate.ClearMessage(this.statusBar);

            this.ImageNavigatorSlider.Value = this.dataHandler.ImageCache.CurrentRow;

            // get and display the new image if the image changed
            // this avoids unnecessary image reloads and refreshes in cases where ShowImage() is just being called to refresh controls
            // the image row can't be tested against as its meaning changes when filters are changed; use the image ID as that's both
            // unique and immutable
            if (newImageToDisplay)
            {
                WriteableBitmap unalteredImage = this.dataHandler.ImageCache.GetCurrentImage();
                this.markableCanvas.ImageToDisplay.Source = unalteredImage;

                // Set the image to magnify so the unaltered image will appear on the magnifying glass
                this.markableCanvas.ImageToMagnify.Source = unalteredImage;

                // Whenever we navigate to a new image, delete any markers that were displayed on the current image 
                // and then draw the markers assoicated with the new image
                this.GetTheMarkableCanvasListOfMetaTags();
                this.RefreshTheMarkableCanvasListOfMetaTags();
            }
        }
        #endregion

        #region Keyboard shortcuts
        // If its an arrow key and the textbox doesn't have the focus,
        // navigate left/right image or up/down to look at differenced image
        private void Window_PreviewKeyDown(object sender, KeyEventArgs eventData)
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
            if (this.SendKeyToDataEntryControlOrMenu(eventData))
            {
                return;
            }
            // An alternate way of doing this, but not as good -> 
            // if ( this.ControlGrid.IsMouseOver) return;
            // if (!this.markableCanvas.IsMouseOver) return; // if its outside the window, return as well.

            // Interpret key as a possible shortcut key. 
            // Depending on the key, take the appropriate action
            switch (eventData.Key)
            {
                case Key.B:                 // Bookmark (Save) the current pan / zoom level of the image
                    this.markableCanvas.BookmarkSaveZoomPan();
                    break;
                case Key.Escape:
                    this.SetTopLevelFocus(false, eventData);
                    break;
                case Key.OemPlus:           // Restore the zoom level / pan coordinates of the bookmark
                    this.markableCanvas.BookmarkSetZoomPan();
                    break;
                case Key.OemMinus:          // Restore the zoom level / pan coordinates of the bookmark
                    this.markableCanvas.BookmarkZoomOutAllTheWay();
                    break;
                case Key.M:                 // Toggle the magnifying glass on and off
                    this.markableCanvas.IsMagnifyingGlassVisible = !this.markableCanvas.IsMagnifyingGlassVisible;
                    this.MenuItemMagnifier.IsChecked = this.markableCanvas.IsMagnifyingGlassVisible;
                    break;
                case Key.U:                 // Increase the magnifing glass zoom level
                    this.markableCanvas.MagnifierZoomIn();
                    break;
                case Key.D:                 // Decrease the magnifing glass zoom level
                    this.markableCanvas.MagnifierZoomOut();
                    break;
                case Key.Right:             // next image
                    this.ViewNextImage();
                    break;
                case Key.Left:              // previous image
                    this.ViewPreviousImage();
                    break;
                case Key.Up:                // show visual difference to next image
                    this.ViewPreviousOrNextDifference();
                    break;
                case Key.Down:              // show visual difference to previous image
                    this.ViewCombinedDifference();
                    break;
                case Key.C:
                    this.BtnCopy_Click(null, null);
                    break;
                case Key.LeftCtrl:
                case Key.RightCtrl:
                    this.MenuItemOptionsBox.IsEnabled = true;
                    break;
                case Key.Tab:
                    this.MoveFocusToNextOrPreviousControlOrImageSlider(Keyboard.Modifiers == ModifierKeys.Shift);
                    break;
                default:
                    return;
            }
            eventData.Handled = true;
        }

        private void Window_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
            {
                this.MenuItemOptionsBox.IsEnabled = false;
            }
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

        // When we move over the canvas, reset the top level focus
        private void MarkableCanvas_MouseEnter(object sender, MouseEventArgs eventArgs)
        {
            this.SetTopLevelFocus(true, eventArgs);
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
                this.MenuItemViewFilteredDatabaseContents.IsVisible || // options menu
                this.MenuItemViewNextImage.IsVisible || // view menu
                this.MenuItemViewAllImages.IsVisible || // filter menu, and then the help menu...
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
            if (typeof(CheckBox) == type ||
                typeof(ComboBox) == type ||
                typeof(ComboBoxItem) == type ||
                typeof(TextBox) == type)
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
                int count = Convert.ToInt32(oldCounterData);
                count = (count == 0) ? 0 : count - 1;           // Make sure its never negative, which could happen if a person manually enters the count 
                newCounterData = count.ToString();
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
                    this.dataHandler.ImageDatabase.UpdateRow(this.dataHandler.ImageCache.Current.ID, counter.DataLabel, pointList, Constants.Database.MarkersTable);
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

            int count = 0;
            try
            {
                count = Convert.ToInt32(counterContent);
            }
            catch
            {
                count = 0; // If we can't convert it, assume that someone set the default value to a non-integer in the template, and just revert it to zero.
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

        private void RefreshTheMarkableCanvasListOfMetaTags(bool show_annotation)
        {
            // The markable canvas uses a simple list of metatags to decide what to do.
            // So we just create that list here, where we also reset the emphasis of some of the metatags
            List<MetaTag> metaTagList = new List<MetaTag>();

            DataEntryCounter selectedCounter = this.FindSelectedCounter();
            for (int i = 0; i < this.counterCoordinates.Count; i++)
            {
                MetaTagCounter mtagCounter = this.counterCoordinates[i];
                DataEntryControl control;
                DataEntryCounter current_counter;
                if (this.dataEntryControls.ControlsByDataLabel.TryGetValue(mtagCounter.DataLabel, out control) == true)
                {
                    current_counter = (DataEntryCounter)this.dataEntryControls.ControlsByDataLabel[mtagCounter.DataLabel];
                }
                else
                {
                    // If we can't find the counter, its likely because the control was made invisible in the template,
                    // which means that there is no control associated with the marker. So just don't create the 
                    // markers associated with this control. Note that if the control is later made visible in the template,
                    // the markers will then be shown. 
                    continue;
                }
                // Update the emphasise for each tag to reflect how the user is interacting with tags
                foreach (MetaTag mtag in mtagCounter.MetaTags)
                {
                    mtag.Emphasise = this.state.IsMouseOverCounter;
                    if (selectedCounter != null && current_counter.DataLabel == selectedCounter.DataLabel)
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
                    mtag.Label = current_counter.Label;
                    metaTagList.Add(mtag); // Add the MetaTag in the list 
                }
            }
            this.markableCanvas.MetaTags = metaTagList;
        }
        #endregion

        #region File Menu Callbacks and Support Functions
        private void MenuItemAddImagesToImageSet_Click(object sender, RoutedEventArgs e)
        {
            string folderPath;
            if (this.ShowFolderSelectionDialog(out folderPath))
            {
                this.LoadByScanningImageFolder(folderPath);
            }
        }

        /// <summary>Load the images from a folder.</summary>
        private void MenuItemLoadImages_Click(object sender, RoutedEventArgs e)
        {
            string templateDatabasePath;
            if (this.TryGetTemplatePath(out templateDatabasePath))
            {
                this.TryOpenTemplateAndLoadImages(templateDatabasePath);
            }
        }

        /// <summary>Write the CSV file and preview it in excel.</summary>
        private void MenuItemExportCsv_Click(object sender, RoutedEventArgs e)
        {
            if (this.state.ImageFilter != ImageQualityFilter.All)
            {
                DialogMessageBox dlgMB = new DialogMessageBox();
                dlgMB.MessageTitle = "Exporting to a CSV file on a filtered view...";
                dlgMB.MessageWhat = "Only a subset of your data will be exported to the CSV file.";

                dlgMB.MessageReason = "As your filter (in the Filter menu) is not set to view 'All Images', ";
                dlgMB.MessageReason += "only data for those images displayed by this filter will be exported. ";

                dlgMB.MessageSolution = "If you want to export just this subset, then " + Environment.NewLine;
                dlgMB.MessageSolution += "\u2022 click Okay" + Environment.NewLine + Environment.NewLine;
                dlgMB.MessageSolution += "If you want to export all your data for all your images, then " + Environment.NewLine;
                dlgMB.MessageSolution += "\u2022 click Cancel," + Environment.NewLine;
                dlgMB.MessageSolution += "\u2022 select 'All Images' in the Filter menu, " + Environment.NewLine;
                dlgMB.MessageSolution += "\u2022 retry exporting your data as a CSV file.";

                dlgMB.IconType = MessageBoxImage.Warning;
                dlgMB.ButtonType = MessageBoxButton.OKCancel;
                bool? msg_result = dlgMB.ShowDialog();

                // Set the filter to show all images and a valid image
                if (msg_result != true)
                {
                    return;
                }
            }

            // Write the file
            string csvFileName = Path.GetFileNameWithoutExtension(this.dataHandler.ImageDatabase.FileName) + ".csv";
            string csvFilePath = Path.Combine(this.FolderPath, csvFileName);
            CsvReaderWriter csvWriter = new CsvReaderWriter();
            csvWriter.ExportToCsv(this.dataHandler.ImageDatabase, csvFilePath);

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
            else
            {
                // Since we don't show the file, give the user some feedback about the export operation
                if (this.state.ShowCsvDialog)
                {
                    DialogExportCsv dlg = new DialogExportCsv(csvFileName);
                    dlg.Owner = this;
                    bool? result = dlg.ShowDialog();
                    if (result != null)
                    {
                        this.state.ShowCsvDialog = result.Value;
                    }
                }
            }
            StatusBarUpdate.Message(this.statusBar, "Data exported to " + csvFileName);
        }

        /// <summary>
        /// Export the current image to the folder selected by the user via a folder browser dialog.
        /// and provide feedback in the status bar if done.
        /// </summary>
        private void MenuItemExportThisImage_Click(object sender, RoutedEventArgs e)
        {
            if (!this.dataHandler.ImageCache.Current.IsDisplayable())
            {
                DialogMessageBox dlgMB = new DialogMessageBox();
                dlgMB.IconType = MessageBoxImage.Error;
                dlgMB.ButtonType = MessageBoxButton.OK;

                dlgMB.MessageTitle = "Can't export this image!";
                dlgMB.MessageProblem = "We can't export the currently displayed image.";
                dlgMB.MessageReason = "It is likely a corrupted or missing image.";
                dlgMB.MessageSolution = "Make sure you have navigated to, and are displaying, a valid image before you try to export it.";
                dlgMB.ShowDialog();
                return;
            }
            // Get the file name of the current image 
            string sourceFile = this.dataHandler.ImageCache.Current.FileName;

            // Set up a Folder Browser with some instructions
            var dialog = new System.Windows.Forms.SaveFileDialog();
            dialog.Title = "Export a copy of the currently displayed image";
            dialog.Filter = "JPeg Image|*.jpg";
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
                    StatusBarUpdate.Message(this.statusBar, sourceFile + " copied to " + destFileName);
                }
                catch
                {
                    StatusBarUpdate.Message(this.statusBar, "Copy failed for some reason!");
                }
            }
        }

        private void MenuItemImportFromCsv_Click(object sender, RoutedEventArgs e)
        {
            string csvFilePath;
            DialogMessageBox dlgMB = new DialogMessageBox();
            dlgMB.MessageTitle = "Importing CSV data rules...";
            dlgMB.MessageWhat = "Importing data from a CSV (comma separated value) file will only work if you follow the rules below." + Environment.NewLine;
            dlgMB.MessageWhat += "Otherwise your Timelapse data may become corrupted.";

            dlgMB.MessageReason = "Timelapse requires the CSV file and its data to follow a specific format.";

            dlgMB.MessageSolution = "\u2022 Only modify and import a CSV file previously exported by Timelapse." + Environment.NewLine;
            dlgMB.MessageSolution = "\u2022 Don't change the File or Folder names" + Environment.NewLine;
            dlgMB.MessageSolution += "\u2022 Do not change the order or names of any of the columns" + Environment.NewLine;
            dlgMB.MessageSolution += "\u2022 Restrict data modification as follows:" + Environment.NewLine;
            dlgMB.MessageSolution += "    \u2022 Counter data to positive integers" + Environment.NewLine;
            dlgMB.MessageSolution += "    \u2022 Flag data to either 'true' or 'false'" + Environment.NewLine;
            dlgMB.MessageSolution += "    \u2022 FixedChoice data to a string that exactly match one of the FixedChoice menu options, or empty." + Environment.NewLine;
            dlgMB.MessageSolution += "    \u2022 Note data to any string, or empty." + Environment.NewLine;
            dlgMB.MessageSolution += "    \u2022 As Date / Time field formats are sometimes altered by spreadsheets," + Environment.NewLine;
            dlgMB.MessageSolution += "           any changes to those fields will be ignored during the import.";

            dlgMB.MessageResult = "Timelapse will create a backup .ddb file in the Backups folder, and will then try its best.";
            dlgMB.MessageHint = "After you import, check your data. If it is not what you expect, restore your data by using that backup file.";

            dlgMB.IconType = MessageBoxImage.Warning;
            dlgMB.ButtonType = MessageBoxButton.OKCancel;
            bool? msg_result = dlgMB.ShowDialog();

            // Set the filter to show all images and a valid image
            if (msg_result != true)
            {
                return;
            }

            if (Utilities.TryGetFileFromUser("Select a .csv file to merge into the current image set",
                                             Path.Combine(this.dataHandler.ImageDatabase.FolderPath, Path.GetFileNameWithoutExtension(this.dataHandler.ImageDatabase.FileName) + Constants.File.CsvFileExtension),
                                             String.Format("Comma separated value files ({0})|*{0}", Constants.File.CsvFileExtension),
                                             out csvFilePath) == false)
            {
                return;
            }

            // Create a backup file
            if (FileBackup.TryCreateBackups(this.FolderPath, this.dataHandler.ImageDatabase.FileName))
            {
                StatusBarUpdate.Message(this.statusBar, "Backups of files made.");
            }
            else
            {
                StatusBarUpdate.Message(this.statusBar, "No file backups were made.");
            }

            CsvReaderWriter csvReader = new CsvReaderWriter();
            try
            {
                List<string> importErrors;
                if (csvReader.TryImportFromCsv(csvFilePath, this.dataHandler.ImageDatabase, out importErrors) == false)
                {
                    DialogMessageBox messageBox = new DialogMessageBox();
                    messageBox.IconType = MessageBoxImage.Error;
                    messageBox.ButtonType = MessageBoxButton.OK;
                    messageBox.MessageTitle = "Can't import the CSV file.";
                    messageBox.MessageProblem = String.Format("The file {0} could not be read.", csvFilePath);
                    messageBox.MessageReason = "The CSV file is not compatible with the current image set.";
                    messageBox.MessageSolution = "Check that:" + Environment.NewLine;
                    messageBox.MessageSolution += "\u2022 The first row of the CSV file is a header line." + Environment.NewLine;
                    messageBox.MessageSolution += "\u2022 The column names in the header line match the database.";
                    messageBox.MessageSolution += "\u2022 Choice values use the correct case.";
                    messageBox.MessageSolution += "\u2022 Counter values are numbers.";
                    messageBox.MessageSolution += "\u2022 Flag values are either 'true' or 'false'.";
                    messageBox.MessageResult = "Either no data was imported or invalid parts of the CSV were skipped.";
                    messageBox.MessageHint = "The errors encountered were:";
                    foreach (string importError in importErrors)
                    {
                        messageBox.MessageHint += "\u2022 " + importError;
                    }
                    messageBox.ShowDialog();
                }
            }
            catch (Exception exception)
            {
                DialogMessageBox messageBox = new DialogMessageBox();
                messageBox.IconType = MessageBoxImage.Error;
                messageBox.ButtonType = MessageBoxButton.OK;
                messageBox.MessageTitle = "Can't import the CSV file.";
                messageBox.MessageProblem = String.Format("The file {0} could not be opened.", csvFilePath);
                messageBox.MessageReason = "Most likely the file is open in another program.";
                messageBox.MessageSolution = "If the file is open in another program, close it.";
                messageBox.MessageResult = String.Format("{0}: {1}", exception.GetType().FullName, exception.Message);
                messageBox.MessageHint = "Is the file open in Excel?";
                messageBox.ShowDialog();
            }

            this.OnImageLoadingComplete();
            StatusBarUpdate.Message(this.statusBar, "CSV file imported.");
        }

        private void MenuItemRecentImageSet_Click(object sender, RoutedEventArgs e)
        {
            string recentDatabasePath = (string)((MenuItem)sender).ToolTip;
            if (this.TryOpenTemplateAndLoadImages(recentDatabasePath) == false)
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
            this.MenuItemRecentImageSets.IsEnabled = this.state.MostRecentImageSets.Count > 0;
            this.MenuItemRecentImageSets.Items.Clear();

            int index = 1;
            foreach (string recentImageSetPath in this.state.MostRecentImageSets)
            {
                MenuItem recentImageSetItem = new MenuItem();
                recentImageSetItem.Click += this.MenuItemRecentImageSet_Click;
                recentImageSetItem.Header = String.Format("_{0} {1}", index++, recentImageSetPath);
                recentImageSetItem.ToolTip = recentImageSetPath;
                this.MenuItemRecentImageSets.Items.Add(recentImageSetItem);
            }
        }

        private void MenuItemRenameImageDatabaseFile_Click(object sender, RoutedEventArgs e)
        {
            DialogRenameImageDatabaseFile dlg = new DialogRenameImageDatabaseFile(this.dataHandler.ImageDatabase.FileName);
            dlg.Owner = this;
            bool? result = dlg.ShowDialog();
            if (result == true)
            {
                this.dataHandler.ImageDatabase.RenameFile(dlg.NewFilename, this.template);
            }
        }

        /// <summary>
        /// Exit Timelapse
        /// </summary>
        private void MenuItemExit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private bool ShowFolderSelectionDialog(out string folderPath)
        {
            CommonOpenFileDialog folderSelectionDialog = new CommonOpenFileDialog();
            folderSelectionDialog.Title = "Select a folder to add image files from...";
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
        private void MenuItemPopulateFieldFromMetaData_Click(object sender, RoutedEventArgs e)
        {
            // If we are not in the filter all view, or if its a corrupt image or deleted image, tell the person. Selecting ok will shift the filter..
            if (this.dataHandler.ImageCache.Current.IsDisplayable() == false || this.state.ImageFilter != ImageQualityFilter.All)
            {
                DialogMessageBox dlgMB = new DialogMessageBox();
                dlgMB.MessageTitle = "Populate a data field with image metadata of your choosing.";
                dlgMB.MessageProblem = "To populate a data field with image metadata of your choosing, Timelapse must first" + Environment.NewLine;
                dlgMB.MessageProblem += "\u2022 be filtered to view All Images (normally set  in the Filter menu)" + Environment.NewLine;
                dlgMB.MessageProblem += "\u2022 be displaying a valid image";
                dlgMB.MessageSolution = "Select 'Ok' for Timelapse to do the above actions for you.";
                dlgMB.IconType = MessageBoxImage.Exclamation;
                dlgMB.ButtonType = MessageBoxButton.OKCancel;
                bool? msg_result = dlgMB.ShowDialog();

                // Set the filter to show all images and a valid image
                if (msg_result == true)
                {
                    this.SetImageFilterAndIndex(Constants.DefaultImageRowIndex, ImageQualityFilter.All); // Set it to all images
                }
                else
                {
                    return;
                }
            }

            DialogPopulateFieldWithMetadata dlg = new DialogPopulateFieldWithMetadata(this.dataHandler.ImageDatabase, this.dataHandler.ImageCache.Current.GetImagePath(this.FolderPath));
            dlg.Owner = this;
            bool? result = dlg.ShowDialog();
            if (result == true)
            {
                this.ShowImage(this.dataHandler.ImageCache.CurrentRow);
            }
        }

        /// <summary>Delete the current image by replacing it with a placeholder image, while still making a backup of it</summary>
        private void Delete_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            try
            {
                int deletedImages = this.dataHandler.ImageDatabase.GetDeletedImageCount();
                this.MenuItemDeleteImages.IsEnabled = deletedImages > 0;
                this.MenuItemDeleteImagesAndData.IsEnabled = deletedImages > 0;
                this.MenuItemDeleteImageAndData.IsEnabled = true;
                this.MenuItemDeleteImage.IsEnabled = this.dataHandler.ImageCache.Current.IsDisplayable() || this.dataHandler.ImageCache.Current.ImageQuality == ImageQualityFilter.Corrupted;
            }
            catch
            {
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
            DataTable deletedImages;
            bool isUseDeleteData;
            bool isUseDeleteFlag;
            int savedRow;

            // This callback is invoked by either variatons of DeleteImage (which deletes the current image) or 
            // DeleteImages (which deletes the images marked by the deletion flag)
            // Thus we need to use two different methods to construct a table containing all the images marked for deletion
            if (mi.Name.Equals(this.MenuItemDeleteImages.Name) || mi.Name.Equals(this.MenuItemDeleteImagesAndData.Name))
            {
                // Delete by deletion flags case. 
                // Construct a table that contains the datarows of all images with their delete flag set, and set various flags
                deletedImages = this.dataHandler.ImageDatabase.GetImagesMarkedForDeletion();
                isUseDeleteFlag = true;
                isUseDeleteData = mi.Name.Equals(this.MenuItemDeleteImages.Name) ? false : true;
            }
            else
            {
                // Delete current image case. Get the ID of the current image and construct a datatable that contains that image's datarow
                ImageProperties imageProperties = new ImageProperties(this.dataHandler.ImageDatabase.ImageDataTable.Rows[this.dataHandler.ImageCache.CurrentRow]);
                deletedImages = this.dataHandler.ImageDatabase.GetImageByID(imageProperties.ID);
                isUseDeleteFlag = false;
                isUseDeleteData = mi.Name.Equals(this.MenuItemDeleteImage.Name) ? false : true;
            }

            // If no images are selected for deletion. Warn the user.
            // Note that this should never happen, as the invoking menu item should be disabled (and thus not selectable)
            // if there aren't any images to delete. Still,...
            if (deletedImages == null)
            {
                DialogMessageBox dlgMB = new DialogMessageBox();
                dlgMB.MessageTitle = "No images are marked for deletion";
                dlgMB.MessageProblem = "You are trying to delete images marked for deletion, but none of the images have their 'Delete?' field checkmarked.";
                dlgMB.MessageHint = "If you have images that you think should be deleted, checkmark its Delete? field.";
                dlgMB.IconType = MessageBoxImage.Information;
                dlgMB.ButtonType = MessageBoxButton.OK;
                dlgMB.ShowDialog();
                return;
            }

            DialogDeleteImages deleteImagesDialog;
            if (mi.Name.Equals(this.MenuItemDeleteImages.Name) || mi.Name.Equals(this.MenuItemDeleteImagesAndData.Name))
            {
                deleteImagesDialog = new DialogDeleteImages(this.dataHandler.ImageDatabase, deletedImages, isUseDeleteData, isUseDeleteFlag);   // don't delete data
            }
            else
            {
                ImageProperties imageProperties = new ImageProperties(this.dataHandler.ImageDatabase.ImageDataTable.Rows[this.dataHandler.ImageCache.CurrentRow]);
                deleteImagesDialog = new DialogDeleteImages(this.dataHandler.ImageDatabase, deletedImages, isUseDeleteData, isUseDeleteFlag);   // delete data
            }
            deleteImagesDialog.Owner = this;

            bool? result = deleteImagesDialog.ShowDialog();
            if (result == true)
            {
                long currentID = this.dataHandler.ImageCache.Current.ID;
                savedRow = this.dataHandler.ImageCache.CurrentRow;  // TryInvalidate may reset the current row to -1, so we need to save it.
                foreach (long id in deleteImagesDialog.ImageFilesRemovedByID)
                {
                    this.dataHandler.ImageCache.TryInvalidate(id);
                }

                if (mi.Name.Equals(this.MenuItemDeleteImage.Name) || mi.Name.Equals(this.MenuItemDeleteImages.Name))
                {
                    // We only deleted the image, not the data. We invoke ShowImage with the saved current row to show the missing image placeholder
                    this.ShowImage(savedRow);
                }
                else
                {
                    // We deleted images and data, which may also include the current image. 
                    // Because we may be deleting the current image, we need to find the next displayable and non-deleted image after this one.
                    this.SetImageFilterAndIndex(0, this.state.ImageFilter); // Reset the filter to retrieve the remaining images

                    int currentRow = this.dataHandler.ImageDatabase.FindClosestImage(currentID);
                    this.ShowImage(currentRow);
                }
            }
        }

        /// <summary>Add some text to the image set log</summary>
        private void MenuItemLog_Click(object sender, RoutedEventArgs e)
        {
            DialogEditLog dlg = new DialogEditLog(this.dataHandler.ImageDatabase.GetImageSetLog());
            dlg.Owner = this;
            bool? result = dlg.ShowDialog();
            if (result == true)
            {
                this.dataHandler.ImageDatabase.SetImageSetLog(dlg.LogContents);
            }
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            int previousRow = this.dataHandler.ImageCache.CurrentRow - 1;
            if (previousRow < 0)
            {
                return; // We are already on the first image, so there is nothing to copy
            }

            foreach (KeyValuePair<string, DataEntryControl> pair in this.dataEntryControls.ControlsByDataLabel)
            {
                string type = this.dataHandler.ImageDatabase.ImageDataColumnsByDataLabel[pair.Key].ControlType;
                if (type == null)
                {
                    type = "Not a control";
                }

                DataEntryControl control = pair.Value;
                if (this.dataHandler.ImageDatabase.IsControlCopyable(control.DataLabel))
                {
                    control.Content = this.dataHandler.ImageDatabase.GetImageValue(previousRow, control.DataLabel);
                }
            }
        }
        #endregion

        #region Options Menu Callbacks
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
        private void MenuItemMagnifier_Click(object sender, RoutedEventArgs e)
        {
            // We don't have to do anything here...
            this.markableCanvas.IsMagnifyingGlassVisible = !this.markableCanvas.IsMagnifyingGlassVisible;
            this.MenuItemMagnifier.IsChecked = this.markableCanvas.IsMagnifyingGlassVisible;
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
            // If we are not in the filter all view, or if its a corrupt image, tell the person. Selecting ok will shift the views..
            if (this.state.ImageFilter != ImageQualityFilter.All)
            {
                DialogMessageBox dlgMB = new DialogMessageBox();
                dlgMB.MessageTitle = "Customize the threshold for determining dark images...";
                dlgMB.MessageProblem = "To customize the threshold for determining dark images, Timelapse must first be  filtered to view All Images (normally set  in the Filter menu).";
                dlgMB.MessageSolution = "Select 'Ok' for Timelapse to set the filter to 'All Images'.";
                dlgMB.IconType = MessageBoxImage.Exclamation;
                dlgMB.ButtonType = MessageBoxButton.OKCancel;
                bool? msg_result = dlgMB.ShowDialog();

                // Set the filter to show all images and a valid image
                if (msg_result == true)
                {
                    this.SetImageFilterAndIndex(Constants.DefaultImageRowIndex, ImageQualityFilter.All); // Set it to all images
                }
                else
                {
                    return;
                }
            }

            DialogOptionsDarkImagesThreshold dlg = new DialogOptionsDarkImagesThreshold(this.dataHandler.ImageDatabase, this.dataHandler.ImageCache.CurrentRow, this.state);
            dlg.Owner = this;
            bool? result = dlg.ShowDialog();
        }

        /// <summary>Swap the day / month fields if possible</summary>
        private void MenuItemSwapDayMonth_Click(object sender, RoutedEventArgs e)
        {
            // If we are not in the filter all view, or if its a corrupt image, tell the person. Selecting ok will shift the views..
            if (this.dataHandler.ImageCache.Current.IsDisplayable() == false || this.state.ImageFilter != ImageQualityFilter.All)
            {
                DialogMessageBox dlgMB = new DialogMessageBox();
                dlgMB.MessageTitle = "Swap the day / month...";
                dlgMB.MessageProblem = "To swap the day / month, Timelapse must first:" + Environment.NewLine;
                dlgMB.MessageProblem += "\u2022 be filtered to view All Images (normally set  in the Filter menu)" + Environment.NewLine;
                dlgMB.MessageProblem += "\u2022 preferably be displaying a valid image";
                dlgMB.MessageSolution = "Select 'Ok' for Timelapse to set the filter to 'All Images'.";
                dlgMB.IconType = MessageBoxImage.Exclamation;
                dlgMB.ButtonType = MessageBoxButton.OKCancel;
                bool? msg_result = dlgMB.ShowDialog();

                // Set the filter to show all images and a valid image
                if (msg_result == true)
                {
                    this.SetImageFilterAndIndex(Constants.DefaultImageRowIndex, ImageQualityFilter.All); // Set it to all images
                }
                else
                {
                    return;
                }
            }

            DialogDateSwapDayMonth dlg = new DialogDateSwapDayMonth(this.dataHandler.ImageDatabase);
            dlg.Owner = this;
            bool? result = dlg.ShowDialog();
            if (result == true)
            {
                this.ShowImage(this.dataHandler.ImageCache.CurrentRow);
            }
        }

        /// <summary>Correct the date by specifying an offset</summary>
        private void MenuItemDateCorrections_Click(object sender, RoutedEventArgs e)
        {
            // If we are not in the filter all view, or if its a corrupt image, tell the person. Selecting ok will shift the views..
            if (this.dataHandler.ImageCache.Current.IsDisplayable() == false || this.state.ImageFilter != ImageQualityFilter.All)
            {
                DialogMessageBox dlgMB = new DialogMessageBox();
                dlgMB.MessageTitle = "Add a correction value to every date...";
                dlgMB.MessageProblem = "To correct the dates, Timelapse must first:" + Environment.NewLine;
                dlgMB.MessageProblem += "\u2022 be filtered to view All Images (normally set  in the Filter menu)" + Environment.NewLine;
                dlgMB.MessageProblem += "\u2022 be displaying a valid image";
                dlgMB.MessageSolution = "Select 'Ok' for Timelapse to set the filter to 'All Images'.";
                dlgMB.IconType = MessageBoxImage.Exclamation;
                dlgMB.ButtonType = MessageBoxButton.OKCancel;
                bool? msg_result = dlgMB.ShowDialog();

                // Set the filter to show all images and a valid image
                if (msg_result == true)
                {
                    this.SetImageFilterAndIndex(Constants.DefaultImageRowIndex, ImageQualityFilter.All); // Set it to all images
                }
                else
                {
                    return;
                }
            }

            // We should be in the right mode for correcting the date
            DialogDateCorrection dlg = new DialogDateCorrection(this.dataHandler.ImageDatabase, this.dataHandler.ImageCache.Current);
            dlg.Owner = this;
            bool? result = dlg.ShowDialog();
            if (result == true)
            {
                // redisplay the current image to show the corrected date
                this.ShowImage(this.dataHandler.ImageCache.CurrentRow);
            }
        }

        /// <summary>Correct for daylight savings time</summary>
        private void MenuItemCorrectDaylightSavings_Click(object sender, RoutedEventArgs e)
        {
            // If we are not in the filter all view, or if its a corrupt image, tell the person. Selecting ok will shift the views..
            if (this.dataHandler.ImageCache.Current.IsDisplayable() == false || this.state.ImageFilter != ImageQualityFilter.All)
            {
                if (this.state.ImageFilter != ImageQualityFilter.All)
                {
                    DialogMessageBox dlgMB = new DialogMessageBox();
                    dlgMB.MessageTitle = "Can't correct for daylight savings time.";
                    dlgMB.MessageProblem = "To correct for daylight savings time:" + Environment.NewLine;
                    dlgMB.MessageProblem += "\u2022 Timelapse must first be filtered to view All Images (normally set  in the Filter menu)" + Environment.NewLine;
                    dlgMB.MessageProblem += "\u2022 The displayed image should also be the one at the daylight savings time threshold.";
                    dlgMB.MessageSolution = "Select 'Ok' for Timelapse to set the filter to 'All Images', and try again.";
                    dlgMB.MessageHint = "For this correction to work properly, you should navigate and display the image that is at the daylight savings time threshold.";
                    dlgMB.IconType = MessageBoxImage.Exclamation;
                    dlgMB.ButtonType = MessageBoxButton.OKCancel;
                    bool? msg_result = dlgMB.ShowDialog();

                    // Set the filter to show all images and then go to the first image
                    if (msg_result == true)
                    {
                        this.SetImageFilterAndIndex(Constants.DefaultImageRowIndex, ImageQualityFilter.All); // Set it to all images
                    }
                }
                else
                {
                    // Just a corrupted image
                    DialogMessageBox dlgMB = new DialogMessageBox();
                    dlgMB.MessageTitle = "Can't correct for daylight savings time.";
                    dlgMB.MessageProblem = "This is a corrupted image.  ";
                    dlgMB.MessageSolution = "To correct for daylight savings time, you need to:" + Environment.NewLine;
                    dlgMB.MessageSolution += "\u2022 be displaying  an image with a valid date ";
                    dlgMB.MessageSolution += "\u2022 where that image should be the one at the daylight savings time threshold.";
                    dlgMB.IconType = MessageBoxImage.Exclamation;
                    dlgMB.ButtonType = MessageBoxButton.OK;
                    bool? msg_result = dlgMB.ShowDialog();
                }
                return;
            }

            DialogDateTimeChangeCorrection dlg = new DialogDateTimeChangeCorrection(this.dataHandler.ImageDatabase, this.dataHandler.ImageCache);
            dlg.Owner = this;
            bool? result = dlg.ShowDialog();
            if (result == true)
            {
                this.ShowImage(this.dataHandler.ImageCache.CurrentRow);
            }
        }

        private void MenuItemCheckModifyAmbiguousDates_Click(object sender, RoutedEventArgs e)
        {
            // If we are not in the filter all view, or if its a corrupt image, tell the person. Selecting ok will shift the views..
            if (this.dataHandler.ImageCache.Current.IsDisplayable() == false || this.state.ImageFilter != ImageQualityFilter.All)
            {
                DialogMessageBox dlgMB = new DialogMessageBox();
                dlgMB.MessageTitle = "Check and modify ambiguous dates...";
                dlgMB.MessageProblem = "To check and modify ambiguous dates, Timelapse must first be filtered to view All Images (normally set  in the Filter menu)";
                dlgMB.MessageSolution = "Select 'Ok' for Timelapse to set the filter to 'All Images'.";
                dlgMB.IconType = MessageBoxImage.Exclamation;
                dlgMB.ButtonType = MessageBoxButton.OKCancel;
                bool? msg_result = dlgMB.ShowDialog();

                // Set the filter to show all images and a valid image
                if (msg_result == true)
                {
                    this.SetImageFilterAndIndex(Constants.DefaultImageRowIndex, ImageQualityFilter.All); // Set it to all images
                }
                else
                {
                    return;
                }
            }

            DialogDateModifyAmbiguousDates dlg = new DialogDateModifyAmbiguousDates(this.dataHandler.ImageDatabase);
            dlg.Owner = this;
            bool? result = dlg.ShowDialog();
            if (result == true)
            {
                this.ShowImage(this.dataHandler.ImageCache.CurrentRow);
            }
        }

        private void MenuItemRereadDatesfromImages_Click(object sender, RoutedEventArgs e)
        {
            // If we are not in the filter all view, or if its a corrupt image, tell the person. Selecting ok will shift the views..
            if (this.state.ImageFilter != ImageQualityFilter.All)
            {
                DialogMessageBox dlgMB = new DialogMessageBox();
                dlgMB.MessageTitle = "Re-read the dates from the images...";
                dlgMB.MessageProblem = "To re-read dates from the images, Timelapse must first be filtered to view All Images (normally set  in the Filter menu)";
                dlgMB.MessageSolution = "Select 'Ok' for Timelapse to set the filter to 'All Images'.";
                dlgMB.IconType = MessageBoxImage.Exclamation;
                dlgMB.ButtonType = MessageBoxButton.OKCancel;
                bool? msg_result = dlgMB.ShowDialog();

                // Set the filter to show all images and a valid image
                if (msg_result == true)
                {
                    this.SetImageFilterAndIndex(Constants.DefaultImageRowIndex, ImageQualityFilter.All); // Set it to all images
                }
                else
                {
                    return;
                }
            }

            DialogDateRereadDatesFromImages dlg = new DialogDateRereadDatesFromImages(this.dataHandler.ImageDatabase);
            dlg.Owner = this;
            bool? result = dlg.ShowDialog();
            if (result == true)
            {
                this.ShowImage(this.dataHandler.ImageCache.CurrentRow);
            }
        }

        /// <summary> Toggle the audio feedback on and off</summary>
        private void MenuItemAudioFeedback_Click(object sender, RoutedEventArgs e)
        {
            // We don't have to do anything here...
            this.state.AudioFeedback = !this.state.AudioFeedback;
            this.MenuItemAudioFeedback.IsChecked = this.state.AudioFeedback;
        }

        /// <summary>Show advanced options</summary>
        private void MenuItemOptions_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                this.optionsWindow.Show();
            }
            catch
            {
                this.optionsWindow = new OptionsWindow(this, this.markableCanvas);
                this.optionsWindow.Show();
            }
        }
        #endregion

        #region View Menu Callbacks
        private void View_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            Dictionary<ImageQualityFilter, int> counts = this.dataHandler.ImageDatabase.GetImageCounts();

            this.MenuItemViewLightImages.IsEnabled = counts[ImageQualityFilter.Ok] > 0;
            this.MenuItemViewDarkImages.IsEnabled = counts[ImageQualityFilter.Dark] > 0;
            this.MenuItemViewCorruptedImages.IsEnabled = counts[ImageQualityFilter.Corrupted] > 0;
            this.MenuItemViewMissingImages.IsEnabled = counts[ImageQualityFilter.Missing] > 0;
            this.MenuItemViewImagesMarkedForDeletion.IsEnabled = this.dataHandler.ImageDatabase.GetDeletedImageCount() > 0;
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
            this.ViewNextImage(); // Goto the next image
        }

        /// <summary>Navigate to the previous image in this image set</summary>
        private void MenuItemViewPreviousImage_Click(object sender, RoutedEventArgs e)
        {
            this.ViewPreviousImage(); // Goto the previous image
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

        /// <summary>Select the appropriate filter and update the view</summary>
        private void MenuItemView_Click(object sender, RoutedEventArgs e)
        {
            MenuItem item = (MenuItem)sender;
            ImageQualityFilter filter;
            // find out which filter was selected
            if (item == this.MenuItemViewAllImages)
            {
                filter = ImageQualityFilter.All;
            }
            else if (item == this.MenuItemViewLightImages)
            {
                filter = ImageQualityFilter.Ok;
            }
            else if (item == this.MenuItemViewCorruptedImages)
            {
                filter = ImageQualityFilter.Corrupted;
            }
            else if (item == this.MenuItemViewDarkImages)
            {
                filter = ImageQualityFilter.Dark;
            }
            else if (item == this.MenuItemViewMissingImages)
            {
                filter = ImageQualityFilter.Missing;
            }
            else if (item == this.MenuItemViewImagesMarkedForDeletion)
            {
                filter = ImageQualityFilter.MarkedForDeletion;
            }
            else
            {
                filter = ImageQualityFilter.All;   // Just in case
            }

            // Treat the checked status as a radio button i.e., toggle their states so only the clicked menu item is checked.
            bool result = this.SetImageFilterAndIndex(Constants.DefaultImageRowIndex, filter);  // Go to the first result (i.e., index 0) in the given filter set
            // if (result == true) MenuItemViewSetSelected(item);  //Check the currently selected menu item and uncheck the others in this group
        }

        // helper function to put a checkbox on the currently selected menu item i.e., to make it behave like a radiobutton menu
        private void MenuItemViewSetSelected(MenuItem checked_item)
        {
            this.MenuItemViewAllImages.IsChecked = (this.MenuItemViewAllImages == checked_item) ? true : false;
            this.MenuItemViewCorruptedImages.IsChecked = (this.MenuItemViewCorruptedImages == checked_item) ? true : false;
            this.MenuItemViewDarkImages.IsChecked = (this.MenuItemViewDarkImages == checked_item) ? true : false;
            this.MenuItemViewLightImages.IsChecked = (this.MenuItemViewLightImages == checked_item) ? true : false;
            this.MenuItemViewImagesMarkedForDeletion.IsChecked = (this.MenuItemViewImagesMarkedForDeletion == checked_item) ? true : false;
            this.MenuItemView.IsChecked = false;
        }

        // helper function to put a checkbox on the currently selected menu item i.e., to make it behave like a radiobutton menu
        private void MenuItemViewSetSelected(ImageQualityFilter filter)
        {
            this.MenuItemViewAllImages.IsChecked = (filter == ImageQualityFilter.All) ? true : false;
            this.MenuItemViewCorruptedImages.IsChecked = (filter == ImageQualityFilter.Corrupted) ? true : false;
            this.MenuItemViewDarkImages.IsChecked = (filter == ImageQualityFilter.Dark) ? true : false;
            this.MenuItemViewLightImages.IsChecked = (filter == ImageQualityFilter.Ok) ? true : false;
            this.MenuItemViewMissingImages.IsChecked = (filter == ImageQualityFilter.Missing) ? true : false;
            this.MenuItemViewImagesMarkedForDeletion.IsChecked = (filter == ImageQualityFilter.MarkedForDeletion) ? true : false;
            this.MenuItemViewCustomFilter.IsChecked = (filter == ImageQualityFilter.Custom) ? true : false;
        }

        private void MenuItemViewCustomFilter_Click(object sender, RoutedEventArgs e)
        {
            DialogCustomViewFilter dlg = new DialogCustomViewFilter(this.dataHandler.ImageDatabase, this.customfilter);
            dlg.Owner = this;
            bool? msg_result = dlg.ShowDialog();
            // Set the filter to show all images and a valid image
            if (msg_result == true)
            {
                // MenuItemViewSetSelected(ImageQualityFilters.Custom);
                this.SetImageFilterAndIndex(Constants.DefaultImageRowIndex, ImageQualityFilter.Custom);
            }
        }

        /// <summary>Show a dialog box telling the user how many images were loaded, etc.</summary>
        public void MenuItemImageCounts_Click(object sender, RoutedEventArgs e)
        {
            Dictionary<ImageQualityFilter, int> counts = this.dataHandler.ImageDatabase.GetImageCounts();
            DialogStatisticsOfImageCounts dlg = new DialogStatisticsOfImageCounts(counts);
            dlg.Owner = this;
            dlg.ShowDialog();
        }

        /// <summary>Display the dialog showing the filtered view of the current database contents</summary>
        private void MenuItemViewFilteredDatabaseContents_Click(object sender, RoutedEventArgs e)
        {
            if (this.dlgDataView != null && this.dlgDataView.IsLoaded)
            {
                return; // If its already displayed, don't bother.
            }
            this.dlgDataView = new DialogDataView(this.dataHandler.ImageDatabase, this.dataHandler.ImageCache);
            this.dlgDataView.Show();
        }
        #endregion 

        #region Help Menu Callbacks
        /// <summary>Display a help window</summary> 
        private void MenuOverview_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                this.overviewWindow.Show();
            }
            catch
            {
                this.overviewWindow = new HelpWindow();
                this.overviewWindow.Show();
            }
        }

        /// <summary> Display a message describing the version, etc.</summary> 
        private void MenuOverview_About(object sender, RoutedEventArgs e)
        {
            DialogAboutTimelapse dlg = new DialogAboutTimelapse();
            dlg.Owner = this;
            dlg.ShowDialog();
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

        public void ResetDifferenceThreshold()
        {
            this.DifferenceThreshold = Constants.Images.DifferenceThresholdDefault;
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
        #endregion

        #region Navigating Images
        private void TryViewImage(int newIndex)
        {
            if (this.dataHandler.ImageDatabase.IsImageRowInRange(newIndex))
            {
                this.ImageNavigatorSlider_EnableOrDisableValueChangedCallback(false);
                this.ShowImage(newIndex);
                this.ImageNavigatorSlider_EnableOrDisableValueChangedCallback(true);
            }
        }

        // Display the next image if one is available, otherwise do nothing
        private void ViewNextImage()
        {
            this.TryViewImage(this.dataHandler.ImageCache.CurrentRow + 1);
        }

        // Display the previous image if one is available, otherwise do nothing
        private void ViewPreviousImage()
        {
            this.TryViewImage(this.dataHandler.ImageCache.CurrentRow - 1);
        }
        #endregion

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

            this.controlWindow = new ControlWindow(this.state);    // Handles to the control window and to the controls
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

        #region Convenience classes
        // This class is used to define a tag, where a tag associates a control index and a point
        internal class TagFinder
        {
            public int ControlIndex { get; set; }

            public TagFinder(int ctlIndex)
            {
                this.ControlIndex = ctlIndex;
            }
        }

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
    }
}
