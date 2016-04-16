using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Speech.Synthesis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Timelapse.Database;
using Timelapse.Images;
using Timelapse.Util;
using FolderBrowserDialog = System.Windows.Forms.FolderBrowserDialog;

namespace Timelapse
{
    /// <summary>
    /// main window for Timelapse
    /// </summary>
    public partial class TimelapseWindow : Window
    {
        // Handles to the controls window and to the controls
        private ControlWindow controlWindow;
        
        private List<MetaTagCounter> counterCoords = null;
        private CustomFilter customfilter;

        // the database that holds all the data
        private ImageDatabase imageDatabase;
        private Controls dataEntryControls;

        // These are used for Image differencing
        // If a person toggles between the current image and its two differenced imaes, those images are stored
        // in a 'cache' so they can be redisplayed more quickly (vs. re-reading it from a file or regenerating it)
        private enum WhichImage
        {
            PreviousDiff = 0,
            Unaltered = 1,
            NextDiff = 2,
            CombinedDiff = 3
        }

        private int whichImageState = (int)WhichImage.Unaltered;
        private BitmapSource[] cachedImages = new BitmapSource[4];  // Cache of unaltered image [1], previous[0], next[2] and combined [3] differenced image
        private HelpWindow overviewWindow; // Create the help window. 
        private OptionsWindow optionsWindow; // Create the options window
        private MarkableImageCanvas markableCanvas;

        // Status information concerning the state of the UI
        private TimelapseState state = new TimelapseState();
        private Canvas magCanvas = new Canvas(); // This canvas will contain the image and marks used for the magnifying glass
        private System.Windows.Controls.Image magImg = new System.Windows.Controls.Image(); // and this contain the image within it

        // Speech feedback
        private SpeechSynthesizer speechSynthesizer = new SpeechSynthesizer();
        
        // the database that holds the template
        private TemplateDatabase template;

        private bool imageFolderReopened = true; // Whether  the image folder in the current session is the same as the folder used in the last session

        private DialogDataView dlgDataView;

        #region Constructors, Cleaning up, Destructors
        public TimelapseWindow()
        {
            this.InitializeComponent();
            // CheckForUpdate.GetAndParseVersion (this, false);

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
            this.dataEntryControls = new Controls();
            this.ControlGrid.Children.Remove(this.btnCopy);
            this.dataEntryControls.AddButton(this.btnCopy);

            // Recall states from prior sessions
            using (TimelapseRegistryUserSettings userSettings = new TimelapseRegistryUserSettings())
            {
                this.state.AudioFeedback = userSettings.ReadAudioFeedback();
                this.state.ControlWindowSize = userSettings.ReadControlWindowSize();
                this.MenuItemAudioFeedback.IsChecked = this.state.AudioFeedback;
                this.MenuItemControlsInSeparateWindow.IsChecked = userSettings.ReadControlsInSeparateWindow();
                this.state.DarkPixelThreshold = userSettings.ReadDarkPixelThreshold();
                this.state.DarkPixelRatioThreshold = userSettings.ReadDarkPixelRatioThreshold();
                this.state.ShowCsvDialog = userSettings.ReadShowCsvDialog();
                this.state.MostRecentDatabasePaths = userSettings.ReadMostRecentDataFilePaths();  // the last path opened by the user is stored in the registry
            }

            // populate the most recent databases list
            this.MenuItemRecentDataFiles_Refresh();
        }

        public byte DifferenceThreshold { get; set; } // The threshold used for calculating combined differences

        private string FolderPath
        {
            get { return this.imageDatabase.FolderPath; }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            string directoryContainingTimelapse = Path.GetDirectoryName(this.GetType().Assembly.Location);
            if (!File.Exists(Path.Combine(directoryContainingTimelapse, "System.Data.SQLite.dll")))
            {
                DialogMessageBox dlgMB = new DialogMessageBox();
                dlgMB.MessageTitle = "Timelapse needs to be in its original downloaded folder";
                dlgMB.MessageProblem = "The Timelapse Programs won't run properly as it was not correctly installed.";
                dlgMB.MessageReason = "When you downloaded Timelapse, it was in a folder with several other files and folders it needs. You probably dragged Timelapse out of that folder.";
                dlgMB.MessageSolution = "Put the Timelapse programs back in its original folder, or download it again.";
                dlgMB.MessageHint = "If you want to access these programs from elsewhere, create a shortcut to it." + Environment.NewLine;
                dlgMB.MessageHint += "1. From its original folder, right-click the Timelapse program icon  and select 'Create Shortcut' from the menu." + Environment.NewLine;
                dlgMB.MessageHint += "2. Drag the shortcut icon to the location of your choice.";
                dlgMB.IconType = MessageBoxImage.Error;
                dlgMB.ShowDialog();
                Application.Current.Shutdown();
            }
            else
            {
                CheckForUpdate.GetAndParseVersion(this, false);
            }
            // FOR MY DEBUGGING ONLY: THIS STARTS THE SYSTEM WITH THE LOAD MENU ITEM SELECTED loadImagesFromSources();  //OPENS THE MENU AUTOMATICALLY
        }

        // On exiting, save various attributes so we can use recover them later
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (this.state.ImmediateExit)
            {
                return;
            }

            if ((this.imageDatabase != null) && (this.imageDatabase.ImageCount > 0))
            {
                // Save the following in the database as they are local to this image set
                this.imageDatabase.State_Filter = (int)this.state.ImageFilter;
                if (this.imageDatabase.State_Filter == (int)ImageQualityFilter.Custom)
                {
                    this.imageDatabase.State_Filter = (int)ImageQualityFilter.All; // Don't save custom filters. Revert to All 
                }
                this.imageDatabase.State_Row = this.imageDatabase.CurrentRow;
                this.imageDatabase.State_Magnifyer = this.markableCanvas.IsMagnifyingGlassVisible;
            }

            // Save the current filter set and the index of the current image being viewed in that set, and save it into the registry
            using (TimelapseRegistryUserSettings userSettings = new TimelapseRegistryUserSettings())
            {
                userSettings.WriteAudioFeedback(this.state.AudioFeedback);
                userSettings.WriteControlWindowSize(this.state.ControlWindowSize);
                userSettings.WriteControlsInSeparateWindow(this.MenuItemControlsInSeparateWindow.IsChecked);
                userSettings.WriteDarkPixelThreshold(this.state.DarkPixelThreshold);
                userSettings.WriteDarkPixelRatioThreshold(this.state.DarkPixelRatioThreshold);
                userSettings.WriteMostRecentDataFilePaths(this.state.MostRecentDatabasePaths);
                userSettings.WriteShowCsvDialog(this.state.ShowCsvDialog);
            }

            if (null != this.controlWindow)
            {
                this.controlWindow.Close();
            }
            if (null != this.dlgDataView)
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
            this.state.MostRecentDatabasePaths.TryGetMostRecent(out defaultTemplateDatabasePath);
            if (Utilities.TryGetTemplateFileFromUser(defaultTemplateDatabasePath, out templateDatabasePath) == false)
            {
                return false;
            }

            string templateDatabaseDirectoryPath = Path.GetDirectoryName(templateDatabasePath);
            if (String.IsNullOrEmpty(templateDatabaseDirectoryPath))
            {
                return false;
            }

            // if the user didn't specify a file name for the .tdb use the default
            // TODO: Saul  is this case still reachable?
            if (String.IsNullOrEmpty(Path.GetFileName(templateDatabasePath)))
            {
                templateDatabasePath = Path.Combine(templateDatabaseDirectoryPath, Constants.File.DefaultTemplateDatabaseFileName);
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
            this.template = new TemplateDatabase();
            if (!TemplateDatabase.TryOpen(templateDatabasePath, out this.template))
            {
                this.OnDataFileLoadFailed();
                // TODO: Saul  notify the user the template couldn't be loaded rather than silently doing nothing
                return false;
            }

            // update state to the newly selected database template
            string imageDatabaseFileName = Constants.File.DefaultImageDatabaseFileName;
            if (String.Equals(Path.GetFileName(templateDatabasePath), Constants.File.DefaultTemplateDatabaseFileName, StringComparison.OrdinalIgnoreCase) == false)
            {
                imageDatabaseFileName = Path.GetFileNameWithoutExtension(templateDatabasePath) + Constants.File.ImageDatabaseFileExtension;
            }
            this.imageDatabase = new ImageDatabase(Path.GetDirectoryName(templateDatabasePath), imageDatabaseFileName);
            this.state.MostRecentDatabasePaths.SetMostRecent(templateDatabasePath);
            this.MenuItemRecentDataFiles_Refresh();

            // Find the .ddb file in the image set folder. If a single .ddb file is found, use that one
            // If there are multiple .ddb files, ask the use to choose one and use that
            // However, if the user cancels that choice, just abort.
            // If there are no .ddb files, then just create the standard one.
            switch (this.imageDatabase.FindFile())
            {
                case 0: // An existing .ddb file is available
                    if (this.LoadImagesFromDB(this.template) == true)
                    {
                        if (this.state.ImmediateExit)
                        {
                            return true;
                        }
                        this.OnImageLoadingComplete();
                    }
                    break;
                case 1: // User cancelled the process of choosing between .ddb files
                    if (this.state.ImmediateExit)
                    {
                        this.OnDataFileLoadFailed();
                        return false;
                    }
                    break;
                case 2: // There are no existing .ddb files
                default:
                    if (this.LoadByScanningImageFolder(this.FolderPath) == false)
                    {
                        DialogMessageBox messageBox = new DialogMessageBox();
                        messageBox.MessageTitle = "No Images Found in the Image Set Folder";
                        messageBox.MessageProblem = "There don't seem to be any JPG images in your chosen image folder:";
                        messageBox.MessageProblem += Environment.NewLine + "\u2022 " + this.FolderPath + Environment.NewLine;
                        messageBox.MessageReason = "\u2022 The folder has no JPG files in it (image files ending in '.jpg'), or" + Environment.NewLine;
                        messageBox.MessageReason += "\u2022 You may have selected the wrong folder, i.e., a folder other than the one containing the images.";
                        messageBox.MessageSolution = "\u2022 Check that the chosen folder actually contains JPG images (i.e., a 'jpg' suffix), or" + Environment.NewLine;
                        messageBox.MessageSolution += "\u2022 Choose another folder.";
                        messageBox.IconType = MessageBoxImage.Error;
                        messageBox.ButtonType = MessageBoxButton.OK;
                        messageBox.ShowDialog();

                        // revert UI to no database loaded state
                        // but enable adding images so that if the user needs to place the database in a folder which doesn't directly contain 
                        // any .jpg images they can still add images in other locations
                        this.OnDataFileLoadFailed();
                        this.MenuItemAddImagesToDataFile.IsEnabled = true;
                        return false;
                    }
                    break;
            }
            this.state.IsContentChanged = false; // We've altered some content

            // For persistance: set a flag if We've opened the same image folder we worked with in the last session. 
            // If its different, saved the new folder path 
            this.imageFolderReopened = (templateDatabasePath == this.FolderPath) ? true : false;
            return true;
        }

        // Load all the jpg images found in the folder
        private bool LoadByScanningImageFolder(string imageFolderPath)
        {
            FileInfo[] imageFilePaths = new DirectoryInfo(imageFolderPath).GetFiles("*.jpg");
            int count = imageFilePaths.Length;
            if (count == 0)
            {
                return false;
            }

            // the database and its tables must exist before data can be loaded into it
            if (this.imageDatabase.Exists() == false)
            {
                // TODO: Saul  handle case where database creation fails
                bool result = this.imageDatabase.TryCreateImageDatabase(this.template);

                // We generate the data user interface controls from the template description after the database has been created from the template
                this.dataEntryControls.GenerateControls(this.imageDatabase);
                this.MenuItemControlsInSeparateWindow_Click(this.MenuItemControlsInSeparateWindow, null);

                this.imageDatabase.CreateTables();
                this.imageDatabase.CreateLookupTables();
            }

            // We want to show previews of the frames to the user as they are individually loaded
            // Because WPF uses a scene graph, we have to do this by a background worker, as this forces the update
            BackgroundWorker backgroundWorker = new BackgroundWorker()
            {
                WorkerReportsProgress = true
            };

            bool ambiguous_daymonth_order = false;
            ProgressState progressState = new ProgressState();
            backgroundWorker.DoWork += (ow, ea) =>
            {   // this runs on the background thread; its written as an anonymous delegate
                // We need to invoke this to allow updates on the UI
                this.Dispatcher.Invoke(new Action(() =>
                {
                    // First, change the UI
                    this.helpControl.Visibility = System.Windows.Visibility.Collapsed;
                    Feedback(null, 0, "Examining images...");
                }));

                // First pass: Examine images to extract its basic properties
                BitmapFrame corruptedbmp = BitmapFrame.Create(new Uri("pack://application:,,/Resources/corrupted.jpg"));
                List<ImageProperties> imagePropertyList = new List<ImageProperties>();
                for (int image = 0; image < count; image++)
                {
                    FileInfo imageFile = imageFilePaths[image];
                    ImageProperties imageProperties = new ImageProperties();
                    imageProperties.File = imageFile.Name;
                    imageProperties.Folder = NativeMethods.GetRelativePath(this.FolderPath, imageFile.FullName);
                    imageProperties.Folder = Path.GetDirectoryName(imageProperties.Folder);

                    BitmapFrame bmap = null;
                    try
                    {
                        // Create the bitmap and determine its ImageQuality 
                        bmap = BitmapFrame.Create(new Uri(imageFile.FullName), BitmapCreateOptions.None, BitmapCacheOption.None);

                        bool isDark = PixelBitmap.IsDark(bmap, this.state.DarkPixelThreshold, this.state.DarkPixelRatioThreshold);
                        imageProperties.ImageQuality = isDark ? ImageQualityFilter.Dark : ImageQualityFilter.Ok;
                    }
                    catch
                    {
                        bmap = corruptedbmp;
                        imageProperties.ImageQuality = ImageQualityFilter.Corrupted;
                    }

                    // Get the data from the metadata
                    BitmapMetadata meta = (BitmapMetadata)bmap.Metadata;
                    imageProperties.DateMetadata = meta.DateTaken;
                    // For some reason, different versions of Windows treat creation time and modification time differently, 
                    // giving inconsisten values. So I just check both and take the lesser of the two.
                    DateTime time1 = File.GetCreationTime(imageFile.FullName);
                    DateTime time2 = File.GetLastWriteTime(imageFile.FullName);
                    imageProperties.DateFileCreation = (DateTime.Compare(time1, time2) < 0) ? time1 : time2;

                    // Debug.Print(fileInfo.Name + " " + time1.ToString() + " " + time2.ToString() + " " + time3);
                    imageProperties.ID = image + 1; // its plus 1 as the Database IDs start at 1 rather than 0
                    imagePropertyList.Add(imageProperties);

                    if (imageProperties.ID == 1 || (imageProperties.ID % Constants.FolderScanProgressUpdateFrequency == 0))
                    {
                        progressState.Message = String.Format("{0}/{1}: Examining {2}", image, count, imageProperties.File);
                        progressState.Bmap = bmap;
                        int progress = Convert.ToInt32(Convert.ToDouble(imageProperties.ID) / Convert.ToDouble(count) * 100);
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
                ambiguous_daymonth_order = DateTimeHandler.VerifyAndUpdateDates(imagePropertyList);

                // Third pass: Update database
                // TODO This is pretty slow... a good place to make it more efficient by adding multiple values in one shot

                // We need to get a list of which columns are counters vs notes or fixed coices, 
                // as we will shortly have to initialize them to some defaults
                List<string> counterList = new List<string>();
                List<string> notesAndFixedChoicesList = new List<string>();
                List<string> flagsList = new List<string>();
                for (int i = 0; i < this.imageDatabase.DataTable.Columns.Count; i++)
                {
                    string dataLabel = this.imageDatabase.DataTable.Columns[i].ColumnName;
                    string type = (string)this.imageDatabase.TypeFromKey[dataLabel];
                    if (null == type)
                    {
                        continue; // Column must be the ID, which we skip over as its not a key.
                    }
                    if (type.Equals(Constants.DatabaseElement.Counter))
                    {
                        counterList.Add(dataLabel);
                    }
                    else if (type.Equals(Constants.DatabaseElement.Note) || type.Equals(Constants.DatabaseElement.FixedChoice))
                    {
                        notesAndFixedChoicesList.Add(dataLabel);
                    }
                    else if (type.Equals(Constants.DatabaseElement.Flag))
                    {
                        flagsList.Add(dataLabel);
                    }
                }

                // Create a dataline from the image properties, add it to a list of data lines,
                // then do a multiple insert of the list of datalines to the database 
                List<Dictionary<string, string>> dataline_list;
                List<Dictionary<string, string>> markerline_list;
                const int interval = 100;

                for (int j = 0; j < imagePropertyList.Count; j++)
                {
                    // Create a dataline from the image properties, add it to a list of data lines,
                    // then do a multiple insert of the list of datalines to the database 
                    dataline_list = new List<Dictionary<string, string>>();
                    markerline_list = new List<Dictionary<string, string>>();
                    int image = -1;
                    for (int i = j; (i < (j + interval)) && (i < imagePropertyList.Count); i++)
                    {
                        // THE PROBLEM IS THAT WE ARE NOT ADDING THESE VALUES IN THE SAME ORDER AS THE TABLE
                        // THEY MUST BE IN THE SAME ORDER IE, AS IN THE COLUMNS. This case statement just fills up 
                        // the dataline in the same order as the template table.
                        // It assumes that the key is always the first column
                        Dictionary<string, string> dataline = new Dictionary<string, string>();
                        Dictionary<string, string> markerline = new Dictionary<string, string>();

                        for (int col = 0; col < imageDatabase.DataTable.Columns.Count; col++) // Fill up each column in order
                        {
                            string col_datalabel = imageDatabase.DataTable.Columns[col].ColumnName;
                            string type = (string)imageDatabase.TypeFromKey[col_datalabel];
                            if (null == type)
                            {
                                continue; // a null will be returned from the ID, as we don't add it to the typefromkey hash.
                            }

                            switch (type)
                            {
                                case Constants.DatabaseElement.File: // Add The File name
                                    string dataLabel = (string)this.imageDatabase.DataLabelFromType[Constants.DatabaseElement.File];
                                    dataline.Add(dataLabel, imagePropertyList[i].File);
                                    break;
                                case Constants.DatabaseElement.Folder: // Add The Folder name
                                    dataLabel = (string)this.imageDatabase.DataLabelFromType[Constants.DatabaseElement.Folder];
                                    dataline.Add(dataLabel, imagePropertyList[i].Folder);
                                    break;
                                case Constants.DatabaseElement.Date:
                                    // Add the date
                                    dataLabel = (string)this.imageDatabase.DataLabelFromType[Constants.DatabaseElement.Date];
                                    dataline.Add(dataLabel, imagePropertyList[i].FinalDate);
                                    break;
                                case Constants.DatabaseElement.Time:
                                    // Add the time
                                    dataLabel = (string)this.imageDatabase.DataLabelFromType[Constants.DatabaseElement.Time];
                                    dataline.Add(dataLabel, imagePropertyList[i].FinalTime);
                                    break;
                                case Constants.DatabaseElement.ImageQuality: // Add the Image Quality
                                    dataLabel = (string)this.imageDatabase.DataLabelFromType[Constants.DatabaseElement.ImageQuality];
                                    string str = Constants.ImageQuality.Ok;
                                    if (imagePropertyList[i].ImageQuality == ImageQualityFilter.Dark)
                                    {
                                        str = Constants.ImageQuality.Dark;
                                    }
                                    else if (imagePropertyList[i].ImageQuality == ImageQualityFilter.Corrupted)
                                    {
                                        str = Constants.ImageQuality.Corrupted;
                                    }
                                    dataline.Add(dataLabel, str);
                                    break;
                                case Constants.DatabaseElement.DeleteFlag: // Add the Delete flag
                                    dataLabel = (string)this.imageDatabase.DataLabelFromType[Constants.DatabaseElement.DeleteFlag];
                                    dataline.Add(dataLabel, this.imageDatabase.TemplateGetDefault(dataLabel)); // Default as specified in the template file, which should be "false"
                                    break;
                                case Constants.DatabaseElement.Note:        // Find and then Add the Note or Fixed Choice
                                case Constants.DatabaseElement.FixedChoice:
                                    // Now initialize notes, counters, and fixed choices to the defaults
                                    foreach (string tkey in notesAndFixedChoicesList)
                                    {
                                        if (col_datalabel.Equals(tkey))
                                        {
                                            dataline.Add(tkey, this.imageDatabase.TemplateGetDefault(tkey)); // Default as specified in the template file
                                        }
                                    }
                                    break;
                                case Constants.DatabaseElement.Flag:
                                    // Now initialize flags to the defaults
                                    foreach (string tkey in flagsList)
                                    {
                                        if (col_datalabel.Equals(tkey))
                                        {
                                            dataline.Add(tkey, this.imageDatabase.TemplateGetDefault(tkey)); // Default as specified in the template file
                                        }
                                    }
                                    break;
                                case Constants.DatabaseElement.Counter:
                                    foreach (string tkey in counterList)
                                    {
                                        if (col_datalabel.Equals(tkey))
                                        {
                                            dataline.Add(tkey, this.imageDatabase.TemplateGetDefault(tkey)); // Default as specified in the template file
                                            markerline.Add(tkey, String.Empty);        // TODO ASSUMES THAT MARKER LIST IS IN SAME ORDER AS COUNTERS. THIS MAY NOT BE CORRECT ONCE WE SWITCH ROWS, SO SHOULD DO THIS SEPARATELY
                                        }
                                    }
                                    break;

                                default:
                                    Debug.Print("Shouldn't ever reach here!");
                                    break;
                            }
                        }
                        dataline_list.Add(dataline);
                        if (markerline.Count > 0)
                        {
                            markerline_list.Add(markerline);
                        }
                        image = i;
                    }

                    this.imageDatabase.InsertMultipleRows(Constants.Database.DataTable, dataline_list);
                    this.imageDatabase.InsertMultipleRows(Constants.Database.MarkersTable, markerline_list);
                    j = j + interval - 1;

                    // Get the bitmap again to show it
                    BitmapFrame bmap;
                    if (imagePropertyList[image].ImageQuality == ImageQualityFilter.Corrupted)
                    {
                        bmap = corruptedbmp;
                    }
                    else
                    {
                        bmap = imagePropertyList[image].Load(this.FolderPath);
                    }

                    // Show progress. Since its slow, we may as well do it every update
                    int progress2 = Convert.ToInt32(Convert.ToDouble(image) / Convert.ToDouble(count) * 100);
                    progressState.Message = String.Format("{0}/{1}: Adding {2}", image, count, imagePropertyList[image].File);
                    progressState.Bmap = bmap;
                    backgroundWorker.ReportProgress(progress2, progressState);
                }
            };
            backgroundWorker.ProgressChanged += (o, ea) =>
            {   
                // this gets called on the UI thread
                ProgressState progstate = (ProgressState)ea.UserState;
                Feedback(progressState.Bmap, ea.ProgressPercentage, progressState.Message);
                this.feedbackCtl.Visibility = System.Windows.Visibility.Visible;
            };
            backgroundWorker.RunWorkerCompleted += (o, ea) =>
            {
                // this.dbData.GetImagesAll(); // Now load up the data table
                // Get rid of the feedback panel, and show the main interface
                this.feedbackCtl.Visibility = Visibility.Collapsed;
                this.feedbackCtl.ShowImage = null;

                this.markableCanvas.Visibility = Visibility.Visible;

                // warn the user if there are any ambiguous dates in terms of day/month or month/day order
                if (ambiguous_daymonth_order)
                {
                    DialogMessageBox dlgMB = new DialogMessageBox();
                    dlgMB.MessageTitle = "Timelapse was unsure about the month / day order of your image's dates";
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
                    DialogImportImageDataXmlFile dlg = new DialogImportImageDataXmlFile();
                    dlg.Owner = this;
                    bool? result3 = dlg.ShowDialog();
                    if (result3 == true)
                    {
                        ImageDataXml.Read(Path.Combine(this.FolderPath, Constants.File.XmlDataFileName), imageDatabase.TemplateTable, imageDatabase);
                        this.SetImageFilterAndIndex(this.imageDatabase.State_Row, (ImageQualityFilter)this.imageDatabase.State_Filter); // to regenerate the controls and markers for this image
                    }
                }
            };

            backgroundWorker.RunWorkerAsync();
            return true;
        }

        private void Feedback(BitmapSource bmap, int percent, string message)
        {
            this.feedbackCtl.ShowMessage = message;
            this.feedbackCtl.ShowProgress = percent;
            if (null != bmap)
            {
                this.feedbackCtl.ShowImage = bmap;
            }
        }

        // Try to load the images from the DB file.
        private Boolean LoadImagesFromDB(TemplateDatabase template)
        {
            if (this.imageDatabase.TryCreateImageDatabase(template))
            {
                // When we are loading from an existing data file, ensure that the template in the template db matches the template stored in the data db
                List<string> errors = this.CheckCodesVsImageData();
                if (errors.Count > 0)
                {
                    DialogTemplatesDontMatch dlg = new DialogTemplatesDontMatch(errors);
                    dlg.Owner = this;
                    bool? result = dlg.ShowDialog();
                    if (result == true)
                    {
                        this.state.ImmediateExit = true;
                        Application.Current.Shutdown();
                        return true;
                    }
                    else
                    {
                        this.imageDatabase.TemplateTable = this.imageDatabase.CreateDataTableFromDatabaseTable(Constants.Database.TemplateTable);
                    }
                }

                // We generate the data user interface controls from the template description after the database has been created from the template
                this.dataEntryControls.GenerateControls(this.imageDatabase);
                this.MenuItemControlsInSeparateWindow_Click(this.MenuItemControlsInSeparateWindow, null);
                this.imageDatabase.CreateLookupTables();
                this.imageDatabase.GetImagesAll();
                return true;
            }
            return false;
        }

        /// <summary>
        /// When image loading has completed add callbacks, prepare the UI, set up the image set, and show the image.
        /// </summary>
        private void OnImageLoadingComplete()
        {
            // Make sure that all the string data in the datatable has white space trimmed from its beginning and end
            // This is needed as the custom filter doesn't work well in testing comparisons if there is leading or trailing white space in it
            // Newer versions of TImelapse will trim the data as it is entered, but older versions did not, so this is to make it backwards-compatable.
            // The WhiteSpaceExists column in the ImageSetTable did not exist before this version, so we add it to the table. If it exists, then 
            // we know the data has been trimmed and we don't have to do it again as the newer versions take care of trimmingon the fly.
            if (!this.imageDatabase.DoesWhiteSpaceColumnExist())
            {
                this.imageDatabase.CreateWhiteSpaceColumn();
                this.imageDatabase.DataTableTrimDataWhiteSpace();  // Trim the white space from all the data
                this.imageDatabase.State_WhiteSpaceTrimmed = true;
            }

            // Create a Custom Filter, which will hold the current custom filter expression (if any) that may be set in the DialogCustomViewFilter
            // TODO: HAVE THIS STORED IN THE IMAGESET DATABASE
            this.customfilter = new CustomFilter(this.imageDatabase);

            // Load the Marker table from the database
            this.imageDatabase.InitializeMarkerTableFromDataTable();

            // Set the magnifying glass status from the registry. 
            // Note that if it wasn't in the registry, the value returned will be true by default
            this.markableCanvas.IsMagnifyingGlassVisible = this.imageDatabase.State_Magnifyer;

            // Add callbacks to all our controls
            this.MyAddControlsCallback();

            // Now that we have something to show, enable menus and menu items as needed
            // Note that we do not enable those menu items that would have no effect
            this.MenuItemAddImagesToDataFile.IsEnabled = true;
            this.MenuItemLoadImages.IsEnabled = false;
            this.MenuItemExportThisImage.IsEnabled = true;
            this.MenuItemExportAsCsvAndPreview.IsEnabled = true;
            this.MenuItemExportAsCSV.IsEnabled = true;
            this.MenuItemRecentDataFiles.IsEnabled = false;
            this.MenuItemRenameDataFile.IsEnabled = true;
            this.MenuItemEdit.IsEnabled = true;
            this.MenuItemDeleteImage.IsEnabled = true;
            this.MenuItemView.IsEnabled = true;
            this.MenuItemFilter.IsEnabled = true;
            this.MenuItemOptions.IsEnabled = true;

            this.MenuItemMagnifier.IsChecked = this.markableCanvas.IsMagnifyingGlassVisible;

            // Also adjust the visibility of the various other UI components.
            this.helpControl.Visibility = System.Windows.Visibility.Collapsed;
            this.DockPanelNavigator.Visibility = System.Windows.Visibility.Visible;
            this.controlsTray.Visibility = System.Windows.Visibility.Visible;
            this.btnCopy.Visibility = System.Windows.Visibility.Visible;

            // Set the image set filter to all images. This should also set the correct count, etc. 
            StatusBarUpdate.View(this.statusBar, "all images.");

            // We will be showing the unaltered image, so set that flag as well.
            this.whichImageState = (int)WhichImage.Unaltered;

            // Show the image, Hide the load button, and make the feedback panels visible
            this.SldrImageNavigatorEnableCallback(false);
            this.imageDatabase.ToDataRowFirst();

            this.markableCanvas.Focus(); // We start with this having the focus so it can interpret keyboard shortcuts if needed. 

            // set the current filter and the image index to the same as the ones in the last session, providing that we are working 
            // with the same image folder. 
            // Doing so also displays the image
            if (this.imageFolderReopened)
            {
                this.SetImageFilterAndIndex(this.imageDatabase.State_Row, (ImageQualityFilter)this.imageDatabase.State_Filter);
            }
            else
            {
                this.SetImageFilterAndIndex(Constants.DefaultImageRowIndex, ImageQualityFilter.All);
            }

            if (FileBackup.CreateBackups(this.FolderPath, this.imageDatabase.FileName))
            {
                StatusBarUpdate.Message(this.statusBar, "Backups of files made.");
            }
            else
            {
                StatusBarUpdate.Message(this.statusBar, "No file backups were made.");
            }
        }

        /// <summary>
        /// If a data file could not be opened or the user canceled loading revert the UI to its initial state so the user can try
        /// loading another data file and isn't presented with menu options applicable only when a data file has been opened.
        /// </summary>
        private void OnDataFileLoadFailed()
        {
            this.MenuItemAddImagesToDataFile.IsEnabled = false;
            this.MenuItemLoadImages.IsEnabled = true;
            this.MenuItemExportThisImage.IsEnabled = false;
            this.MenuItemExportAsCsvAndPreview.IsEnabled = false;
            this.MenuItemExportAsCSV.IsEnabled = false;
            this.MenuItemRecentDataFiles.IsEnabled = true;
            this.MenuItemRenameDataFile.IsEnabled = false;
            this.MenuItemEdit.IsEnabled = false;
            this.MenuItemDeleteImage.IsEnabled = false;
            this.MenuItemView.IsEnabled = false;
            this.MenuItemFilter.IsEnabled = false;
            this.MenuItemOptions.IsEnabled = false;
        }

        // Check if the code template file matches the Image data file. If not, return a list of errors,
        // i.e., columns that appera in one but not the other.
        private List<string> CheckCodesVsImageData()
        {
            List<String> dbtable_list = new List<String>();
            List<String> templatetable_list = new List<String>();

            DataTable databaseTemplateTable = this.imageDatabase.CreateDataTableFromDatabaseTable(Constants.Database.TemplateTable);

            // Create two lists that we will compare, each containing the DataLabels from the template in the template file vs. db file.
            for (int i = 0; i < databaseTemplateTable.Rows.Count; i++)
            {
                dbtable_list.Add((string)databaseTemplateTable.Rows[i][Constants.Control.DataLabel]);
            }
            for (int i = 0; i < this.template.TemplateTable.Rows.Count; i++)
            {
                templatetable_list.Add((string)this.template.TemplateTable.Rows[i][Constants.Control.DataLabel]);
            }

            // Check to see if there are field in the template template that are not in the db template
            List<string> errors = new List<string>();
            foreach (string s in templatetable_list)
            {
                if (!dbtable_list.Contains(s))
                {
                    errors.Add("- A field with the DataLabel '" + s + "' was found in the Template, but nothing matches that in the Data." + Environment.NewLine);
                }
            }

            // Check to see if there are fields in the db template that are not in the template template
            foreach (string s in dbtable_list)
            {
                if (!templatetable_list.Contains(s))
                {
                    errors.Add("- A field with the DataLabel '" + s + "' was found in the Data, but nothing matches that in the Template." + Environment.NewLine);
                }
            }
            return errors;
        }
        #endregion

        #region Filters
        private bool SetImageFilterAndIndex(int image, ImageQualityFilter filter)
        {
            // Change the filter to reflect what the user selected. Update the menu state accordingly
            // Set the checked status of the radio button menu items to the filter.
            if (filter == ImageQualityFilter.All)
            {
                // All images
                this.imageDatabase.GetImagesAll();
                StatusBarUpdate.View(this.statusBar, "all images.");
                this.MenuItemViewSetSelected(ImageQualityFilter.All);
                if (null != this.dlgDataView)
                {
                    this.dlgDataView.RefreshDataTable();  // If its displayed, update the window that shows the filtered view data base
                }
            }
            else if (filter == ImageQualityFilter.Ok)
            {
                // Light images
                if (this.imageDatabase.GetImagesAllButDarkAndCorrupted())
                {
                    StatusBarUpdate.View(this.statusBar, "light images.");
                    this.MenuItemViewSetSelected(ImageQualityFilter.Ok);
                    if (null != this.dlgDataView)
                    {
                        this.dlgDataView.RefreshDataTable();  // If its displayed, update the window that shows the filtered view data base
                    }
                }
                else
                {
                    // It really should never get here, as the menu option for filtering by light images will be disabled if there aren't any. 
                    // Still,...
                    StatusBarUpdate.Message(this.statusBar, "no light images to display.");
                    DialogMessageBox dlgMB = new DialogMessageBox();
                    dlgMB.MessageTitle = "Light filter selected, but no images are marked as light.";
                    dlgMB.MessageProblem = "None of the images in this image set are light images, so nothing can be shown.";
                    dlgMB.MessageReason = "None of the images have their 'ImageQuality' field  set to OK.";
                    dlgMB.MessageResult = "The filter will not be applied.";
                    dlgMB.MessageHint = "If you have images that you think should be marked as 'light', set its ImageQUality field to OK.";
                    dlgMB.IconType = MessageBoxImage.Information;
                    dlgMB.ButtonType = MessageBoxButton.OK;
                    dlgMB.ShowDialog();

                    if (this.state.ImageFilter == ImageQualityFilter.Ok)
                    {
                        return this.SetImageFilterAndIndex(Constants.DefaultImageRowIndex, ImageQualityFilter.All);
                    }
                    this.MenuItemViewSetSelected(this.state.ImageFilter);
                    return false;
                }
            }
            else if (filter == ImageQualityFilter.Corrupted)
            {
                // Corrupted images
                if (this.imageDatabase.GetImagesCorrupted())
                {
                    StatusBarUpdate.View(this.statusBar, "corrupted images.");
                    this.MenuItemViewSetSelected(ImageQualityFilter.Corrupted);
                    if (null != this.dlgDataView)
                    {
                        this.dlgDataView.RefreshDataTable();  // If its displaye, update the window that shows the filtered view data base
                    }
                }
                else
                {
                    // It really should never get here, as the menu option for filtering by corrupted images will be disabled if there aren't any. 
                    // Still,...
                    StatusBarUpdate.Message(this.statusBar, "no corrupted images to display.");
                    DialogMessageBox dlgMB = new DialogMessageBox();
                    dlgMB.MessageTitle = "Corrupted filter selected, but no images are marked as corrupted.";
                    dlgMB.MessageProblem = "None of the images in this image set are corrupted images, so nothing can be shown.";
                    dlgMB.MessageReason = "None of the images have their 'ImageQuality' field  set to Corrupted.";
                    dlgMB.MessageResult = "The filter will not be applied.";
                    dlgMB.MessageHint = "If you have images that you think should be marked as 'Corrupted', set its ImageQUality field to Corrupted.";
                    dlgMB.IconType = MessageBoxImage.Information;
                    dlgMB.ButtonType = MessageBoxButton.OK;
                    dlgMB.ShowDialog();

                    if (this.state.ImageFilter == ImageQualityFilter.Corrupted)
                    {
                        return this.SetImageFilterAndIndex(Constants.DefaultImageRowIndex, ImageQualityFilter.All);
                    }

                    this.MenuItemViewSetSelected(this.state.ImageFilter);
                    return false;
                }
            }
            else if (filter == ImageQualityFilter.Dark)
            {
                // Dark images
                if (this.imageDatabase.GetImagesDark())
                {
                    StatusBarUpdate.View(this.statusBar, "dark images.");
                    this.MenuItemViewSetSelected(ImageQualityFilter.Dark);
                    if (null != this.dlgDataView)
                    {
                        this.dlgDataView.RefreshDataTable();  // If its displayed, update the window that shows the filtered view data base
                    }
                }
                else
                {
                    // It really should never get here, as the menu option for filtering by dark images will be disabled if there aren't any. 
                    // Still,...
                    StatusBarUpdate.Message(this.statusBar, "no dark images to display.");
                    DialogMessageBox messageBox = new DialogMessageBox();
                    messageBox.MessageTitle = "Dark filter selected, but no images are marked as dark.";
                    messageBox.MessageProblem = "None of the images in this image set are dark images, so nothing can be shown.";
                    messageBox.MessageReason = "None of the images have their 'ImageQuality' field  set to Dark.";
                    messageBox.MessageResult = "The filter will not be applied.";
                    messageBox.MessageHint = "If you have images that you think should be marked as 'Dark', set its ImageQUality field to Dark.";
                    messageBox.IconType = MessageBoxImage.Information;
                    messageBox.ButtonType = MessageBoxButton.OK;
                    messageBox.ShowDialog();

                    if (this.state.ImageFilter == ImageQualityFilter.Dark)
                    {
                        return this.SetImageFilterAndIndex(Constants.DefaultImageRowIndex, ImageQualityFilter.All);
                    }

                    this.MenuItemViewSetSelected(this.state.ImageFilter);
                    return false;
                }
            }
            else if (filter == ImageQualityFilter.Missing)
            {
                // Missing images
                if (this.imageDatabase.GetImagesMissing())
                {
                    StatusBarUpdate.View(this.statusBar, "missing images.");
                    this.MenuItemViewSetSelected(ImageQualityFilter.Missing);
                    if (null != this.dlgDataView)
                    {
                        this.dlgDataView.RefreshDataTable();  // If its displayed, update the window that shows the filtered view data base
                    }
                }
                else
                {
                    // It really should never get here, as the menu option for filtering by missing images will be disabled if there aren't any. 
                    // Still,...
                    StatusBarUpdate.Message(this.statusBar, "no missing images to display.");
                    DialogMessageBox dlgMB = new DialogMessageBox();
                    dlgMB.MessageTitle = "Missing filter selected, but no images are marked as missing.";
                    dlgMB.MessageProblem = "None of the images in this image set are missing images, so nothing can be shown.";
                    dlgMB.MessageReason = "None of the images have their 'ImageQuality' field  set to Missing.";
                    dlgMB.MessageResult = "The filter will not be applied.";
                    dlgMB.MessageHint = "If you have images that you think should be marked as 'Missing', set its ImageQUality field to Missing.";
                    dlgMB.IconType = MessageBoxImage.Information;
                    dlgMB.ButtonType = MessageBoxButton.OK;
                    dlgMB.ShowDialog();
                    if (this.state.ImageFilter == ImageQualityFilter.Missing)
                    {
                        return this.SetImageFilterAndIndex(Constants.DefaultImageRowIndex, ImageQualityFilter.All);
                    }
                    this.MenuItemViewSetSelected(this.state.ImageFilter);
                    return false;
                }
            }
            else if (filter == ImageQualityFilter.MarkedForDeletion)
            {
                // Images marked for deletion
                if (this.imageDatabase.GetImagesMarkedForDeletion())
                {
                    StatusBarUpdate.View(this.statusBar, "images marked for deletion.");
                    this.MenuItemViewSetSelected(ImageQualityFilter.MarkedForDeletion);
                    if (null != this.dlgDataView)
                    {
                        this.dlgDataView.RefreshDataTable();
                        this.MenuItemViewFilteredDatabaseContents_Click(null, null); // Regenerate the DataView if needed
                    }
                }
                else
                {
                    // It really should never get here, as the menu option for filtering by images marked for deletion will be disabled if there aren't any. 
                    // Still,...
                    StatusBarUpdate.Message(this.statusBar, "No images marked for deletion to display.");
                    DialogMessageBox dlgMB = new DialogMessageBox();
                    dlgMB.MessageTitle = "Delete filter selected, but no images are marked for deletion";
                    dlgMB.MessageProblem = "None of the images in this image set are marked for deletion, so nothing can be shown.";
                    dlgMB.MessageReason = "None of the images have their 'Delete?' field checkmarked.";
                    dlgMB.MessageResult = "The filter will not be applied.";
                    dlgMB.MessageHint = "If you have images that you think should be marked for deletion, checkmark its Delete? field.";
                    dlgMB.IconType = MessageBoxImage.Information;
                    dlgMB.ButtonType = MessageBoxButton.OK;
                    dlgMB.ShowDialog();

                    if (this.state.ImageFilter == ImageQualityFilter.MarkedForDeletion)
                    {
                        return this.SetImageFilterAndIndex(Constants.DefaultImageRowIndex, ImageQualityFilter.All);
                    }
                    this.MenuItemViewSetSelected(this.state.ImageFilter);
                    return false;
                }
            }
            else if (filter == ImageQualityFilter.Custom)
            {
                // Custom Filter
                if (this.customfilter.QueryResultCount != 0)
                {
                    StatusBarUpdate.View(this.statusBar, "images matching your custom filter.");
                    this.MenuItemViewSetSelected(ImageQualityFilter.Custom);
                    if (null != this.dlgDataView)
                    {
                        this.dlgDataView.RefreshDataTable();  // If its displayed, update the window that shows the filtered view data base
                    }
                }
                else
                {
                    // It really should never get here, as the dialog for filtering images shouldn't allow it, but... 
                    // Still,...
                    StatusBarUpdate.Message(this.statusBar, "no images to display.");
                    DialogMessageBox dlgMB = new DialogMessageBox();
                    dlgMB.MessageTitle = "Custom filter selected, but no images match the specified search.";
                    dlgMB.MessageProblem = "None of the images in this image set match the specified search, so nothing can be shown.";
                    dlgMB.MessageResult = "The filter will not be applied.";
                    dlgMB.MessageHint = "Try to create another custom filter.";
                    dlgMB.IconType = MessageBoxImage.Information;
                    dlgMB.ButtonType = MessageBoxButton.OK;
                    dlgMB.ShowDialog();
                    if (this.state.ImageFilter == ImageQualityFilter.Missing)
                    {
                        return this.SetImageFilterAndIndex(Constants.DefaultImageRowIndex, ImageQualityFilter.All);
                    }
                    this.MenuItemViewSetSelected(this.state.ImageFilter);
                    return false;
                }
            }

            // go to the specified image
            bool imageSeekSucceeded = this.imageDatabase.ToDataRowIndex(image);
            Debug.Assert(imageSeekSucceeded, String.Format("Failed to reach row index {0} in the image table.  Did table loading fail?", image));

            // After a filter change, set the slider to represent the index and the count of the current filter
            this.SldrImageNavigatorEnableCallback(false);
            this.sldrImageNavigator.Maximum = this.imageDatabase.ImageCount - 1;  // Reset the slider to the size of images in this set
            this.sldrImageNavigator.Value = this.imageDatabase.CurrentRow;

            // Update the status bar accordingly
            StatusBarUpdate.CurrentImageNumber(this.statusBar, this.imageDatabase.CurrentRow + 1);  // We add 1 because its a 0-based list
            StatusBarUpdate.TotalCount(this.statusBar, this.imageDatabase.ImageCount);
            this.ShowImage(this.imageDatabase.CurrentRow);
            this.SldrImageNavigatorEnableCallback(true);
            this.state.ImageFilter = filter;    // Remember the current filter
            return true;
        }

        #endregion

        #region Configure Callbacks
        // Add callbacks to all our controls. When the user changes an image's attribute using a particular control,
        // the callback updates the matching field for that image in the imageData structure.

        /// <summary>
        /// Add the event handler callbacks for our (possibly invisible)  controls
        /// </summary>
        private void MyAddControlsCallback()
        {
            string type = String.Empty;
            DataEntryNote notectl;
            DataEntryCounter counterctl;
            DataEntryChoice fixedchoicectl;
            DataEntryFlag flagctl;
            foreach (KeyValuePair<string, DataEntryControl> pair in this.dataEntryControls.ControlFromDataLabel)
            {
                type = (string)this.imageDatabase.TypeFromKey[pair.Key];
                if (null == type)
                {
                    type = "Not a control";
                }

                switch (type)
                {
                    case Constants.DatabaseElement.File:
                    case Constants.DatabaseElement.Folder:
                    case Constants.DatabaseElement.Time:
                    case Constants.DatabaseElement.Date:
                    case Constants.DatabaseElement.Note:
                        notectl = (DataEntryNote)pair.Value; // get the control
                        notectl.ContentControl.TextChanged += new TextChangedEventHandler(this.NoteCtl_TextChanged);
                        notectl.ContentControl.PreviewKeyDown += new KeyEventHandler(this.ContentCtl_PreviewKeyDown);
                        break;
                    case Constants.DatabaseElement.DeleteFlag:
                    case Constants.DatabaseElement.Flag:
                        flagctl = (DataEntryFlag)pair.Value; // get the control
                        flagctl.ContentControl.Checked += this.FlagControl_CheckedChanged;
                        flagctl.ContentControl.Unchecked += this.FlagControl_CheckedChanged;
                        flagctl.ContentControl.PreviewKeyDown += new KeyEventHandler(this.ContentCtl_PreviewKeyDown);
                        break;
                    case Constants.DatabaseElement.ImageQuality:
                    case Constants.DatabaseElement.FixedChoice:
                        fixedchoicectl = (DataEntryChoice)pair.Value; // get the control
                        fixedchoicectl.ContentControl.SelectionChanged += new SelectionChangedEventHandler(this.ChoiceControl_SelectionChanged);
                        fixedchoicectl.ContentControl.PreviewKeyDown += new KeyEventHandler(this.ContentCtl_PreviewKeyDown);
                        break;
                    case Constants.DatabaseElement.Counter:
                        counterctl = (DataEntryCounter)pair.Value; // get the control
                        counterctl.ContentControl.TextChanged += new TextChangedEventHandler(this.CounterControl_TextChanged);
                        counterctl.ContentControl.PreviewKeyDown += new KeyEventHandler(this.ContentCtl_PreviewKeyDown);
                        counterctl.ContentControl.PreviewTextInput += new TextCompositionEventHandler(this.CounterCtl_PreviewTextInput);
                        counterctl.Container.Tag = counterctl.DataLabel; // So we can access the parent from the container during the callback
                        counterctl.Container.MouseEnter += new MouseEventHandler(this.ContentCtl_MouseEnter);
                        counterctl.Container.MouseLeave += new MouseEventHandler(this.ContentCtl_MouseLeave);
                        counterctl.LabelControl.Click += new RoutedEventHandler(this.CounterCtl_Click);
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
        /// <param name="e">event information</param>
        private void ContentCtl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                this.SetTopLevelFocus(false); // The false means don't check to see if a textbox or control has the focus, as we want to reset the focus elsewhere
                e.Handled = true;
            }
            else
            {
                Control ctlSender = (Control)sender;
                ctlSender.Focus();
            }
        }

        /// <summary>Preview callback for counters, to ensure ensure that we only accept numbers</summary>
        /// <param name="sender">the event source</param>
        /// <param name="e">event information</param>
        private void CounterCtl_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // TODO: If the result is a blank (i.e., spaces or empty string), need to clean it up.
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
        private void CounterCtl_Click(object sender, RoutedEventArgs e)
        {
            this.RefreshTheMarkableCanvasListOfMetaTags();
        }

        /// <summary>When the user enters a counter, store the index of the counter and then refresh the markers, which will also readjust the colors and emphasis</summary>
        /// <param name="sender">the event source</param>
        /// <param name="e">event information</param>
        private void ContentCtl_MouseEnter(object sender, MouseEventArgs e)
        {
            Panel panel = (Panel)sender;
            this.state.IsMouseOverCounter = (string)panel.Tag;
            this.RefreshTheMarkableCanvasListOfMetaTags();
        }

        // When the user enters a counter, clear the saved index of the counter and then refresh the markers, which will also readjust the colors and emphasis
        private void ContentCtl_MouseLeave(object sender, MouseEventArgs e)
        {
            // Recolor the marks
            this.state.IsMouseOverCounter = String.Empty;
            this.RefreshTheMarkableCanvasListOfMetaTags();
        }

        // Whenever the text in a particular note box changes, update the particular note field in the database 
        private void NoteCtl_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (this.state.IsContentValueChangedFromOutside)
            {
                return;
            }

            TextBox textBox = (TextBox)sender;
            textBox.Text = textBox.Text.TrimStart();  // Don't allow leading spaces in the note
            // Get the key identifying the control, and then add its value to the database
            DataEntryControl control = (DataEntryControl)textBox.Tag;
            this.imageDatabase.RowSetValueFromDataLabel(control.DataLabel, textBox.Text.Trim());
            this.state.IsContentChanged = true; // We've altered some content
            this.state.IsContentValueChangedFromOutside = false;
        }

        // Whenever the text in a particular counter box changes, update the particular counter field in the database
        private void CounterControl_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (this.state.IsContentValueChangedFromOutside)
            {
                return;
            }

            TextBox textBox = (TextBox)sender;
            textBox.Text = textBox.Text.TrimStart();  // Don't allow leading spaces in the counter
            // Get the key identifying the control, and then add its value to the database
            DataEntryControl control = (DataEntryControl)textBox.Tag;
            this.imageDatabase.RowSetValueFromDataLabel(control.DataLabel, textBox.Text.Trim());
            this.state.IsContentChanged = true; // We've altered some content
            this.state.IsContentValueChangedFromOutside = false;
            return;
        }

        // Whenever the text in a particular fixedChoice box changes, update the particular choice field in the database
        private void ChoiceControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.state.IsContentValueChangedFromOutside)
            {
                return;
            }

            ComboBox comboBox = (ComboBox)sender;
            // Make sure an item was actually selected (it could have been cancelled)
            if (null == comboBox.SelectedItem)
            {
                return;
            }

            // Get the key identifying the control, and then add its value to the database
            DataEntryControl control = (DataEntryControl)comboBox.Tag;
            this.imageDatabase.RowSetValueFromDataLabel(control.DataLabel, comboBox.SelectedItem.ToString().Trim());
            this.SetTopLevelFocus();
            this.state.IsContentChanged = true; // We've altered some content
            this.state.IsContentValueChangedFromOutside = false;
        }

        // Whenever the checked state in a Flag  changes, update the particular choice field in the database
        private void FlagControl_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (this.state.IsContentValueChangedFromOutside)
            {
                return;
            }

            CheckBox checkBox = (CheckBox)sender;
            // Get the key identifying the control, and then add its value to the database
            DataEntryControl control = (DataEntryControl)checkBox.Tag;
            string value = ((bool)checkBox.IsChecked) ? "true" : "false";
            this.imageDatabase.RowSetValueFromDataLabel(control.DataLabel, value);
            this.state.IsContentChanged = true; // We've altered some content
            this.state.IsContentValueChangedFromOutside = false;
            return;
        }

        /// <summary>
        /// When the mouse enters / leaves the copy button, the controls that are copyable will be highlighted. 
        /// </summary>
        private void BtnCopy_MouseEnter(object sender, MouseEventArgs e)
        {
            foreach (KeyValuePair<string, DataEntryControl> pair in this.dataEntryControls.ControlFromDataLabel)
            {
                string type = (string)this.imageDatabase.TypeFromKey[pair.Key];
                switch (type)
                {
                    case Constants.DatabaseElement.File:
                    case Constants.DatabaseElement.Folder:
                    case Constants.DatabaseElement.Time:
                    case Constants.DatabaseElement.Date:
                    case Constants.DatabaseElement.Note:
                    case Constants.DatabaseElement.Flag:
                    case Constants.DatabaseElement.ImageQuality:
                    case Constants.DatabaseElement.DeleteFlag:
                    case Constants.DatabaseElement.FixedChoice:
                    case Constants.DatabaseElement.Counter:
                        DataEntryControl control = (DataEntryControl)pair.Value;
                        if (control.Copyable)
                        {
                            var brush = new SolidColorBrush(Color.FromArgb(255, (byte)200, (byte)251, (byte)200));
                            control.Container.Background = brush;
                        }
                        break;
                    default:
                        break;
                }
            }
        }

        /// <summary>
        ///  When the mouse enters / leaves the copy button, the controls that are copyable will be highlighted. 
        /// </summary>
        private void BtnCopy_MouseLeave(object sender, MouseEventArgs e)
        {
            foreach (KeyValuePair<string, DataEntryControl> pair in this.dataEntryControls.ControlFromDataLabel)
            {
                string type = (string)this.imageDatabase.TypeFromKey[pair.Key];
                switch (type)
                {
                    case Constants.DatabaseElement.File:
                    case Constants.DatabaseElement.Folder:
                    case Constants.DatabaseElement.Time:
                    case Constants.DatabaseElement.Date:
                    case Constants.DatabaseElement.Note:
                    case Constants.DatabaseElement.Flag:
                    case Constants.DatabaseElement.ImageQuality:
                    case Constants.DatabaseElement.DeleteFlag:
                    case Constants.DatabaseElement.FixedChoice:
                    case Constants.DatabaseElement.Counter:
                        DataEntryControl control = (DataEntryControl)pair.Value;
                        control.Container.ClearValue(Control.BackgroundProperty);
                        break;
                    default:
                        break;
                }
            }
        }
        #endregion

        #region Differencing
        // Cycle through the  image enhancements in the order current, then previous and next differenced images.
        // Create the differenced image if needed
        // For display efficiency, cache the differenced image.
        private void ViewDifferencesCycleThrough()
        {
            // Note:  No matter what image we are viewing, the source image will have already been cached before entering this function
            // Go to the next image in the cycle we want to show.
            this.NextInCycle();

            // If we are supposed to display the unaltered image, do it and get out of here.
            // The unaltered image will always be cached at this point, so there is no need to check.
            if (this.whichImageState == (int)WhichImage.Unaltered)
            {
                this.markableCanvas.imgToMagnify.Source = this.cachedImages[(int)WhichImage.Unaltered];
                this.markableCanvas.imgToDisplay.Source = this.cachedImages[(int)WhichImage.Unaltered];

                // Check if its a corrupted image
                if (!this.imageDatabase.RowIsImageDisplayable())
                {
                    // TO DO AS WE MAY HAVE TO GET THE INDEX OF THE NEXT IN CYCLE IMAGE???
                    StatusBarUpdate.Message(this.statusBar, "Image is corrupted");
                }
                else
                {
                    StatusBarUpdate.ClearMessage(this.statusBar);
                }
                return;
            }

            // If we don't have the cached difference image, generate and cache it.
            if (this.cachedImages[this.whichImageState] == null)
            {
                // Decide which comparison image to use for differencing. 
                int idx;
                if (this.whichImageState == (int)WhichImage.PreviousDiff)
                {
                    idx = this.imageDatabase.CurrentRow - 1;   // Find the previous image (unless we are already at the beginning)
                    if (idx < 0)
                    {
                        idx = this.imageDatabase.CurrentRow;
                    }
                }
                else
                {
                    idx = this.imageDatabase.CurrentRow + 1;
                    if (idx >= this.imageDatabase.ImageCount)
                    {
                        idx = this.imageDatabase.CurrentRow;
                    }
                }

                // Generate the differenced image. 
                string fullFileName = Path.Combine(this.FolderPath, this.imageDatabase.RowGetValueFromDataLabel((string)this.imageDatabase.DataLabelFromType[Constants.DatabaseElement.File], idx));
                // Check if that file actually exists
                if (!File.Exists(fullFileName))
                {
                    StatusBarUpdate.Message(this.statusBar, "Difference Image is missing");
                    return;
                }

                BitmapImage otherImage = new BitmapImage(new Uri(fullFileName));
                PixelBitmap image1 = new PixelBitmap((BitmapSource)this.cachedImages[(int)WhichImage.Unaltered]);
                PixelBitmap image2 = new PixelBitmap((BitmapSource)otherImage);
                PixelBitmap difference = image1 - image2;
                BitmapSource img = difference.ToBitmap();

                // and now cache the differenced image
                this.cachedImages[(int)this.whichImageState] = (BitmapSource)img;
            }
            // display the differenced image
            this.markableCanvas.imgToDisplay.Source = this.cachedImages[this.whichImageState];
            StatusBarUpdate.Message(this.statusBar, "Viewing " + ((this.whichImageState == (int)WhichImage.PreviousDiff) ? "previous" : "next") + " differenced image");
        }

        // Set the next image in the cycle
        private void NextInCycle()
        {
            // If we are looking at the combined differenced image, then always go to the unaltered image.
            if (this.whichImageState == (int)WhichImage.CombinedDiff)
            {
                this.whichImageState = (int)WhichImage.Unaltered;
                return;
            }

            // If the current image is marked as corrupted, we will only show the original (replacement) image
            int idx = this.imageDatabase.CurrentRow;
            if (!this.imageDatabase.RowIsImageDisplayable())
            {
                this.whichImageState = (int)WhichImage.Unaltered;
                return;
            }
            else
            {
                // We are going around in a cycle, so go back to the beginning if we are at the end of it.
                this.whichImageState = (this.whichImageState >= (int)WhichImage.NextDiff) ? (int)WhichImage.PreviousDiff : ++this.whichImageState;
            }

            // Because we can always display the unaltered image, we don't have to do any more tests if that is the current one in the cyle
            if (this.whichImageState == (int)WhichImage.Unaltered)
            {
                return;
            }

            // We can't actually show the previous or next image differencing if we are on the first or last image in the set respectively
            // Nor can we do it if the next image in the sequence is a corrupted one.
            // If that is the case, skip to the next one in the sequence
            if (this.whichImageState == (int)WhichImage.PreviousDiff && this.imageDatabase.CurrentRow == 0)
            {
                // Already at the beginning
                this.NextInCycle();
            }
            else if (this.whichImageState == (int)WhichImage.NextDiff && this.imageDatabase.CurrentRow == this.imageDatabase.ImageCount - 1)
            {
                // Already at the end
                this.NextInCycle();
            }
            else if (this.whichImageState == (int)WhichImage.NextDiff && !this.imageDatabase.RowIsImageDisplayable(this.imageDatabase.CurrentRow + 1))
            {
                // Can't use the next image as its corrupted
                this.NextInCycle();
            }
            else if (this.whichImageState == (int)WhichImage.PreviousDiff && !this.imageDatabase.RowIsImageDisplayable(this.imageDatabase.CurrentRow - 1))
            {
                // Can't use the previous image as its corrupted
                this.NextInCycle();
            }
        }

        // TODO: This needs to be fixed.
        public void ViewDifferencesCombined()
        {
            // If we are in any state other than the unaltered state, go to the unaltered state, otherwise the combined diff state
            if (this.whichImageState == (int)WhichImage.NextDiff || 
                this.whichImageState == (int)WhichImage.PreviousDiff || 
                this.whichImageState == (int)WhichImage.CombinedDiff)
            {
                this.whichImageState = (int)WhichImage.Unaltered;
            }
            else
            {
                this.whichImageState = (int)WhichImage.CombinedDiff;
            }

            // If we are on the unaltered image
            if (this.whichImageState == (int)WhichImage.Unaltered)
            {
                this.markableCanvas.imgToDisplay.Source = this.cachedImages[this.whichImageState];
                this.markableCanvas.imgToMagnify.Source = this.cachedImages[this.whichImageState];
                StatusBarUpdate.ClearMessage(this.statusBar);
                return;
            }

            // If we are on  the first image, or the last image, then don't do anything
            if (this.imageDatabase.CurrentRow == 0 || this.imageDatabase.CurrentRow == this.imageDatabase.ImageCount - 1)
            {
                this.whichImageState = (int)WhichImage.Unaltered;
                StatusBarUpdate.Message(this.statusBar, "Can't show combined differences without three good images");
                return;
            }

            // If any of the images are corrupted, then don't do anything
            if (!this.imageDatabase.RowIsImageDisplayable() || 
                !this.imageDatabase.RowIsImageDisplayable(this.imageDatabase.CurrentRow + 1) || 
                !this.imageDatabase.RowIsImageDisplayable(this.imageDatabase.CurrentRow - 1))
            {
                this.whichImageState = (int)WhichImage.Unaltered;
                StatusBarUpdate.Message(this.statusBar, "Can't show combined differences without three good images");
                return;
            }

            if (null == this.cachedImages[this.whichImageState])
            {
                // We need three valid images: the current one, the previous one, and the next one.
                // The current image is always in the cache. Create a PixeBitmap from it
                PixelBitmap currImage = new PixelBitmap((BitmapSource)this.cachedImages[(int)WhichImage.Unaltered]);

                // Get the previous and next image
                int idx = this.imageDatabase.CurrentRow - 1;

                string path = Path.Combine(this.FolderPath, this.imageDatabase.RowGetValueFromType((string)this.imageDatabase.DataLabelFromType[Constants.DatabaseElement.File], idx));
                if (!File.Exists(path))
                {
                    StatusBarUpdate.Message(this.statusBar, "Can't show combined differences without three good images");
                    return;
                }
                BitmapImage prevImage = new BitmapImage(new Uri(path));

                idx = this.imageDatabase.CurrentRow + 1;
                path = Path.Combine(this.FolderPath, this.imageDatabase.RowGetValueFromType((string)this.imageDatabase.DataLabelFromType[Constants.DatabaseElement.File], idx));
                if (!File.Exists(path))
                {
                    StatusBarUpdate.Message(this.statusBar, "Can't show combined differences without three good images");
                    return;
                }
                BitmapImage nextImage = new BitmapImage(new Uri(path));

                // Generate the differenced image and dislay it
                PixelBitmap differencedImage = PixelBitmap.Difference(this.cachedImages[(int)WhichImage.Unaltered], prevImage, nextImage, this.DifferenceThreshold);
                this.cachedImages[this.whichImageState] = differencedImage.ToBitmap();
            }

            this.whichImageState = (int)WhichImage.CombinedDiff;
            this.markableCanvas.imgToDisplay.Source = this.cachedImages[this.whichImageState];
            StatusBarUpdate.Message(this.statusBar, "Viewing surrounding differences");
        }

        #endregion

        #region Slider Event Handlers and related
        private void SldrImageNavigator_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.state.IsContentValueChangedFromOutside = true;
            this.imageDatabase.ToDataRowIndex((int)sldrImageNavigator.Value);
            this.ShowImage(this.imageDatabase.CurrentRow);
            this.state.IsContentValueChangedFromOutside = false;
        }

        private void SldrImageNavigatorEnableCallback(bool state)
        {
            if (state)
            {
                this.sldrImageNavigator.ValueChanged += new RoutedPropertyChangedEventHandler<double>(this.SldrImageNavigator_ValueChanged);
            }
            else
            {
                this.sldrImageNavigator.ValueChanged -= new RoutedPropertyChangedEventHandler<double>(this.SldrImageNavigator_ValueChanged);
            }
        }
        #endregion

        #region Showing the current Images
        // Display the current image
        public void ShowImage(int index)
        {
            this.ShowImage(index, true); // by default, use cached images
        }

        public void ShowImage(int index, bool useCachedImages)
        {
            if (this.imageDatabase.ToDataRowIndex(index) == false)
            {
                throw new ArgumentOutOfRangeException("index", String.Format("{0} is not a valid row index in the image table.", index));
            }
            ImageProperties imageProperties = new ImageProperties();
            imageProperties.ImageQuality = (ImageQualityFilter)Enum.Parse(typeof(ImageQualityFilter), this.imageDatabase.RowGetValueFromType(Constants.DatabaseElement.ImageQuality));

            // Get and display the bitmap
            BitmapImage bitmap = new BitmapImage();
            if (imageProperties.ImageQuality == ImageQualityFilter.Corrupted)
            {
                bitmap = Utilities.BitmapFromResource(bitmap, "corrupted.jpg", useCachedImages);
            }
            else
            {
                imageProperties.File = this.imageDatabase.RowGetValueFromType(Constants.DatabaseElement.File);
                imageProperties.Folder = this.imageDatabase.RowGetValueFromType(Constants.DatabaseElement.Folder);
                string imagePath = imageProperties.GetImagePath(this.FolderPath);
                if (File.Exists(imagePath))
                {
                    this.MenuItemDeleteImage.IsEnabled = true;
                    Utilities.BitmapFromFile(bitmap, imagePath, useCachedImages);
                }
                else
                {
                    // The file is missing! show the missing image placeholder 
                    // If its not already tagged as missing, then tag it as such.
                    if (!Constants.ImageQuality.Missing.Equals(this.imageDatabase.RowGetValueFromType(Constants.DatabaseElement.ImageQuality)))
                    {
                        this.imageDatabase.RowSetValueFromDataLabel((string)this.imageDatabase.DataLabelFromType[Constants.DatabaseElement.ImageQuality], Constants.ImageQuality.Missing);
                    }
                    bitmap = Utilities.BitmapFromResource(bitmap, "missing.jpg", useCachedImages);
                }
            }
            this.markableCanvas.imgToDisplay.Source = bitmap;

            // For each control, we get its type and then update its contents from the current data table row
            string type;
            DataEntryNote notectl;
            DataEntryChoice fixedchoicectl;
            DataEntryCounter counterctl;
            DataEntryFlag flagctl;
            foreach (KeyValuePair<string, DataEntryControl> pair in this.dataEntryControls.ControlFromDataLabel)
            {
                type = (string)this.imageDatabase.TypeFromKey[pair.Key];
                if (null == type)
                {
                    type = "Not a control";
                }

                switch (type)
                {
                    case Constants.DatabaseElement.File:
                        notectl = (DataEntryNote)pair.Value;
                        notectl.Content = this.imageDatabase.RowGetValueFromType(Constants.DatabaseElement.File);
                        break;
                    case Constants.DatabaseElement.Folder:
                        notectl = (DataEntryNote)pair.Value;
                        notectl.Content = this.imageDatabase.RowGetValueFromType(Constants.DatabaseElement.Folder);
                        break;
                    case Constants.DatabaseElement.Time:
                        notectl = (DataEntryNote)pair.Value;
                        notectl.Content = this.imageDatabase.RowGetValueFromType(Constants.DatabaseElement.Time);
                        break;
                    case Constants.DatabaseElement.Date:
                        notectl = (DataEntryNote)pair.Value;
                        notectl.Content = this.imageDatabase.RowGetValueFromType(Constants.DatabaseElement.Date);
                        break;
                    case Constants.DatabaseElement.ImageQuality:
                        fixedchoicectl = (DataEntryChoice)pair.Value;
                        fixedchoicectl.Content = this.imageDatabase.RowGetValueFromType(Constants.DatabaseElement.ImageQuality);
                        break;
                    case Constants.DatabaseElement.DeleteFlag:
                        flagctl = (DataEntryFlag)pair.Value; // get the control
                        flagctl.Content = this.imageDatabase.RowGetValueFromDataLabel(flagctl.DataLabel);
                        break;
                    case Constants.DatabaseElement.Note:
                        notectl = (DataEntryNote)pair.Value; // get the control
                        notectl.Content = this.imageDatabase.RowGetValueFromDataLabel(notectl.DataLabel);
                        break;
                    case Constants.DatabaseElement.Flag:
                        flagctl = (DataEntryFlag)pair.Value; // get the control
                        flagctl.Content = this.imageDatabase.RowGetValueFromDataLabel(flagctl.DataLabel);
                        break;
                    case Constants.DatabaseElement.FixedChoice:
                        fixedchoicectl = (DataEntryChoice)pair.Value; // get the control
                        fixedchoicectl.Content = this.imageDatabase.RowGetValueFromDataLabel(fixedchoicectl.DataLabel);
                        break;
                    case Constants.DatabaseElement.Counter:
                        counterctl = (DataEntryCounter)pair.Value; // get the control
                        counterctl.Content = this.imageDatabase.RowGetValueFromDataLabel(counterctl.DataLabel);
                        break;
                    default:
                        break;
                }
            }

            // update the status bar to show which image we are on out of the total
            StatusBarUpdate.CurrentImageNumber(this.statusBar, this.imageDatabase.CurrentRow + 1); // Add one because indexes are 0-based
            StatusBarUpdate.TotalCount(this.statusBar, this.imageDatabase.DataTable.Rows.Count);
            StatusBarUpdate.ClearMessage(this.statusBar);

            this.sldrImageNavigator.Value = this.imageDatabase.CurrentRow;

            // Set the magImage to the source so the unaltered image will appear on the magnifying glass
            // Although its probably not needed, also make the magCanvas the same size as the image
            this.markableCanvas.imgToMagnify.Source = bitmap;

            // Whenever we navigate to a new image, delete any markers that were displayed on the current image 
            // and then draw the markers assoicated with the new image
            this.GetTheMarkableCanvasListOfMetaTags();
            this.RefreshTheMarkableCanvasListOfMetaTags();

            // Always cache the current image
            this.cachedImages[(int)WhichImage.Unaltered] = (BitmapImage)this.markableCanvas.imgToMagnify.Source;

            // Also reset the differencing variables
            this.cachedImages[(int)WhichImage.PreviousDiff] = null;
            this.cachedImages[(int)WhichImage.NextDiff] = null;
            this.cachedImages[(int)WhichImage.CombinedDiff] = null;

            // And track that we are on the unaltered image
            this.whichImageState = (int)WhichImage.Unaltered;
        }
        #endregion

        #region Keyboard shortcuts
        // If its an arrow key and the textbox doesn't have the focus,
        // navigate left/right image or up/down to look at differenced image
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (this.imageDatabase == null || this.imageDatabase.ImageCount == 0)
            {
                return; // No images are loaded, so don't try to interpret any keys
            }

            // Don't interpret keyboard shortcuts if the focus is on a control in the control grid, as the text entered may be directed
            // to the controls within it. That is, if  a textbox or combo box has the focus, then abort as this is normal text input
            // and NOT a shortcut key
            if (this.IsFocusInTextboxOrCombobox())
            {
                return;
            }
            // An alternate way of doing this, but not as good -> 
            // if ( this.ControlGrid.IsMouseOver) return;
            // if (!this.markableCanvas.IsMouseOver) return; // if its outside the window, return as well.

            // Interpret key as a possible shortcut key. 
            // Depending on the key, take the appropriate action
            switch (e.Key)
            {
                case Key.B:
                    this.markableCanvas.BookmarkSaveZoomPan(); // Bookmark (Save) the current pan / zoom level of the image
                    break;
                case Key.OemPlus:                 // Restore the zoom level / pan coordinates of the bookmark
                    this.markableCanvas.BookmarkSetZoomPan();
                    break;
                case Key.OemMinus:                 // Restore the zoom level / pan coordinates of the bookmark
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
                    this.ViewDifferencesCycleThrough();
                    break;
                case Key.Down:              // show visual difference to previous image
                    this.ViewDifferencesCombined();
                    break;
                case Key.C:
                    this.BtnCopy_Click(null, null);
                    break;
                case Key.LeftCtrl:
                case Key.RightCtrl:
                    this.MenuItemOptionsBox.IsEnabled = true;
                    break;
                default:
                    return;
            }
            e.Handled = true;
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
        private void MarkableCanvas_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            this.SetTopLevelFocus();
        }

        // When we move over the canvas, reset the top level focus
        private void MarkableCanvas_MouseEnter(object sender, MouseEventArgs e)
        {
            this.SetTopLevelFocus(true);
        }

        private void SetTopLevelFocus()
        {
            this.SetTopLevelFocus(false);
        }

        // Actually set the top level keyboard focus to the image control
        private void SetTopLevelFocus(bool checkForControlFocus)
        {
            // If the text box or combobox has the focus, we usually don't want to reset the focus. 
            // However, there are a few instances (e.g., after enter has been pressed) where we no longer want it 
            // to have the focus, so we allow for that via this flag.
            if (checkForControlFocus)
            {
                // If we are in a data control, don't reset the focus.
                if (this.IsFocusInTextboxOrCombobox())
                {
                    return;
                }
            }

            // Don't raise the window just because we set the keyboard focus to it
            Keyboard.DefaultRestoreFocusMode = RestoreFocusMode.None;
            Keyboard.Focus(this.markableCanvas);
        }

        // Return true if the current focus is in a textbox or combobox data control
        private bool IsFocusInTextboxOrCombobox()
        {
            IInputElement ie = (IInputElement)FocusManager.GetFocusedElement(this);
            if (ie == null)
            {
                return false;
            }

            Type type = ie.GetType();
            if (typeof(TextBox) == type || typeof(ComboBox) == type || typeof(ComboBoxItem) == type)
            {
                return true;
            }

            return false;
        }
        #endregion

        #region Marking and Counting

        // Get all the counters' metatags (if any)  from the current row in the database
        private void GetTheMarkableCanvasListOfMetaTags()
        {
            this.counterCoords = this.imageDatabase.MarkerTableGetMetaTagCounterList();
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
                if (null == currentCounter)
                {
                    return;
                }
                this.Markers_NewMetaTag(currentCounter, e.metaTag);
            }
            else
            {
                // An existing marker has been deleted.
                DataEntryCounter myCounter = (DataEntryCounter)this.dataEntryControls.ControlFromDataLabel[e.metaTag.DataLabel];

                // Part 1. Decrement the count 
                string old_counter_data = myCounter.Content;
                string new_counter_data = String.Empty;
                int count = Convert.ToInt32(old_counter_data);
                count = (count == 0) ? 0 : count - 1;           // Make sure its never negative, which could happen if a person manually enters the count 
                new_counter_data = count.ToString();
                if (!new_counter_data.Equals(old_counter_data))
                {
                    // Don't bother updating if the value hasn't changed (i.e., already at a 0 count)
                    // Update the datatable and database with the new counter values
                    this.state.IsContentValueChangedFromOutside = true;
                    myCounter.Content = new_counter_data;
                    this.imageDatabase.UpdateRow(this.imageDatabase.CurrentId, myCounter.DataLabel, new_counter_data, Constants.Database.DataTable);
                    this.state.IsContentValueChangedFromOutside = false;
                }

                // Part 2. Each metacounter in the countercoords list reperesents a different control. 
                // So just check the first metatag's  DataLabel in each metatagcounter to see if it matches the counter's datalabel.
                MetaTagCounter mtagCounter = null;
                int index = -1;         // Index is the position of the match within the CounterCoords
                foreach (MetaTagCounter mtcounter in this.counterCoords)
                {
                    // If there are no metatags, we don't have to do anything.
                    if (mtcounter.MetaTags.Count == 0)
                    {
                        continue;
                    }

                    // There are no metatags associated with this counter
                    if (mtcounter.MetaTags[0].DataLabel == myCounter.DataLabel)
                    {
                        // We found the metatag counter associated with that control
                        index++;
                        mtagCounter = mtcounter;
                        break;
                    }
                }

                // Part 3. Remove the found metatag from the metatagcounter and from the database
                string point_list = String.Empty;
                Point point;
                if (mtagCounter != null)
                {
                    // Shouldn't really need this test, but if for some reason there wasn't a match...
                    for (int i = 0; i < mtagCounter.MetaTags.Count; i++)
                    {
                        // Check if we are looking at the same metatag. 
                        if (e.metaTag.Guid == mtagCounter.MetaTags[i].Guid)
                        {
                            // We found the metaTag. Remove that metatag from the metatags list 
                            mtagCounter.MetaTags.RemoveAt(i);
                            this.Speak(myCounter.Content); // Speak the current count
                        }
                        else
                        {
                            // Because we are not deleting it, we can add it to the new the point list
                            // Reconstruct the point list in the string form x,y|x,y e.g.,  0.333,0.333|0.500, 0.600
                            // for writing to the markerTable. Note that it leaves out the deleted value
                            point = mtagCounter.MetaTags[i].Point;
                            if (!point_list.Equals(String.Empty))
                            {
                                point_list += Constants.Database.MarkerBar;          // We don't put a marker bar at the beginning of the point list
                            }
                            point_list += String.Format("{0:0.000},{1:0.000}", point.X, point.Y);   // Add a point in the form 
                        }
                    }
                    this.imageDatabase.UpdateRow(this.imageDatabase.CurrentId, myCounter.DataLabel, point_list, Constants.Database.MarkersTable);
                }
                this.RefreshTheMarkableCanvasListOfMetaTags(); // Refresh the Markable Canvas, where it will also delete the metaTag at the same time
            }
            this.markableCanvas.MarkersRefresh();
            this.state.IsContentChanged = true; // We've altered some content
        }

        /// <summary>
        /// A new Marker associated with a counter control has been created;
        /// Increment the counter controls value, and add the metatag to all data structures (including the database)
        /// </summary>
        private void Markers_NewMetaTag(DataEntryCounter myCounter, MetaTag mtag)
        {
            // Get the Counter Control's contents,  increment its value (as we have added a new marker) 
            // Then update the control's content as well as the database
            string counter_data = myCounter.Content;

            if (String.IsNullOrWhiteSpace(counter_data))
            {
                counter_data = "0";
            }

            int count = 0;
            try
            {
                count = Convert.ToInt32(counter_data);
            }
            catch
            {
                count = 0; // If we can't convert it, assume that someone set the default value to a non-integer in the template, and just revert it to zero.
            }
            count++;
            counter_data = count.ToString();
            this.state.IsContentValueChangedFromOutside = true;
            this.imageDatabase.UpdateRow(this.imageDatabase.CurrentId, myCounter.DataLabel, counter_data);
            myCounter.Content = counter_data;
            this.state.IsContentValueChangedFromOutside = false;

            // Find the metatagCounter associated with this particular control so we can add a metatag to it
            MetaTagCounter metatagCounter = null;
            foreach (MetaTagCounter mtcounter in this.counterCoords)
            {
                if (mtcounter.DataLabel == myCounter.DataLabel)
                {
                    metatagCounter = mtcounter;
                    break;
                }
            }

            // Fill in the metatag information. Also create a TagFinder (which contains a reference to the counter index) and add it as the object's metatag
            mtag.Label = myCounter.Label;   // The tooltip will be the counter label plus its data label
            mtag.Label += "\n" + myCounter.DataLabel;
            mtag.Brush = Brushes.Red;               // Make it Red (for now)
            mtag.DataLabel = myCounter.DataLabel;
            mtag.Annotate = true; // Show the annotation as its created. We will clear it on the next refresh
            mtag.AnnotationAlreadyShown = false;

            // Add the meta tag to the metatag counter
            metatagCounter.AddMetaTag(mtag);

            // Update this counter's list of points in the marker atabase
            String pointlist = String.Empty;
            foreach (MetaTag mt in metatagCounter.MetaTags)
            {
                if (!pointlist.Equals(String.Empty))
                {
                    pointlist += Constants.Database.MarkerBar; // We don't put a marker bar at the beginning of the point list
                }
                pointlist += String.Format("{0:0.000},{1:0.000}", mt.Point.X, mt.Point.Y); // Add a point in the form x,y e.g., 0.5, 0.7
            }
            this.imageDatabase.MarkerTableAddPoint(myCounter.DataLabel, pointlist);
            this.RefreshTheMarkableCanvasListOfMetaTags(true);
            this.Speak(myCounter.Content + " " + myCounter.Label); // Speak the current count
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
            for (int i = 0; i < this.counterCoords.Count; i++)
            {
                MetaTagCounter mtagCounter = this.counterCoords[i];
                DataEntryCounter current_counter = (DataEntryCounter)this.dataEntryControls.ControlFromDataLabel[mtagCounter.DataLabel];

                // Update the emphasise for each tag to reflect how the user is interacting with tags
                foreach (MetaTag mtag in mtagCounter.MetaTags)
                {
                    mtag.Emphasise = (this.state.IsMouseOverCounter == mtagCounter.DataLabel) ? true : false;
                    if (null != selectedCounter && current_counter.DataLabel == selectedCounter.DataLabel)
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
        private void MenuItemAddImagesToDataFile_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog folderSelectionDialog = new FolderBrowserDialog();
            folderSelectionDialog.Description = "Select a folder to add additional image files from";
            folderSelectionDialog.SelectedPath = this.FolderPath;
            switch (folderSelectionDialog.ShowDialog())
            {
                case System.Windows.Forms.DialogResult.OK:
                case System.Windows.Forms.DialogResult.Yes:
                    this.LoadByScanningImageFolder(folderSelectionDialog.SelectedPath);
                    break;
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
        private void MenuItemExportCSV_Click(object sender, RoutedEventArgs e)
        {
            // Write the file
            string csvfile = Path.GetFileNameWithoutExtension(this.imageDatabase.FileName) + ".csv";
            string csvpath = Path.Combine(this.FolderPath, csvfile);
            SpreadsheetWriter.ExportDataAsCsv(this.imageDatabase, csvpath);

            MenuItem mi = (MenuItem)sender;
            if (mi == this.MenuItemExportAsCsvAndPreview)
            {
                // Show the file in excel
                // Create a process that will try to show the file
                Process process = new Process();

                process.StartInfo.UseShellExecute = true;
                process.StartInfo.RedirectStandardOutput = false;
                process.StartInfo.FileName = csvpath;
                process.Start();
            }
            else
            {
                // Since we don't show the file, give the user some feedback about the export operation
                if (this.state.ShowCsvDialog)
                {
                    DialogExportCsv dlg = new DialogExportCsv(csvfile);
                    dlg.Owner = this;
                    bool? result = dlg.ShowDialog();
                    if (result != null)
                    {
                        this.state.ShowCsvDialog = result.Value;
                    }
                }
            }
            StatusBarUpdate.Message(this.statusBar, "Data exported to " + csvfile);
            this.state.IsContentChanged = false; // We've altered some content
        }

        /// <summary> 
        /// Export the current image to the folder selected by the user via a folder browser dialog.
        /// and provide feedback in the status bar if done.
        /// </summary>
        private void MenuItemExportThisImage_Click(object sender, RoutedEventArgs e)
        {
            if (!this.imageDatabase.RowIsImageDisplayable())
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
            string sourceFile = this.imageDatabase.RowGetValueFromType(Constants.DatabaseElement.File);

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

        private void MenuItemRecentDataFile_Click(object sender, RoutedEventArgs e)
        {
            string recentDatabasePath = (string)((MenuItem)sender).ToolTip;
            if (this.TryOpenTemplateAndLoadImages(recentDatabasePath) == false)
            {
                this.state.MostRecentDatabasePaths.TryRemove(recentDatabasePath);
                this.MenuItemRecentDataFiles_Refresh();
            }
        }

        /// <summary>
        /// Update the list of recent databases displayed under File -> Recent Databases.
        /// </summary>
        private void MenuItemRecentDataFiles_Refresh()
        {
            this.MenuItemRecentDataFiles.IsEnabled = this.state.MostRecentDatabasePaths.Count > 0;
            this.MenuItemRecentDataFiles.Items.Clear();

            int index = 1;
            foreach (string recentDatabasePath in this.state.MostRecentDatabasePaths)
            {
                MenuItem recentDatabaseItem = new MenuItem();
                recentDatabaseItem.Click += this.MenuItemRecentDataFile_Click;
                recentDatabaseItem.Header = String.Format("_{0} {1}", index++, recentDatabasePath);
                recentDatabaseItem.ToolTip = recentDatabasePath;
                this.MenuItemRecentDataFiles.Items.Add(recentDatabaseItem);
            }
        }

        private void MenuItemRenameDataFile_Click(object sender, RoutedEventArgs e)
        {
            DialogRenameDataFile dlg = new DialogRenameDataFile(this.imageDatabase.FileName);
            dlg.Owner = this;
            bool? result = dlg.ShowDialog();
            if (result == true)
            {
                this.imageDatabase.RenameDataFile(dlg.NewFilename, this.template);
            }
        }

        /// <summary> 
        /// Exit Timelapse
        /// </summary>
        private void MenuItemExit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        #endregion

        #region Edit Menu Callbacks

        // Populate a data field from metadata (example metadata displayed from the currently selected image)
        private void MenuItemPopulateFieldFromMetaData_Click(object sender, RoutedEventArgs e)
        {
            // If we are not in the filter all view, or if its a corrupt image or deleted image, tell the person. Selecting ok will shift the filter..
            if (this.imageDatabase.RowIsImageDisplayable() == false || this.state.ImageFilter != ImageQualityFilter.All)
            {
                DialogMessageBox dlgMB = new DialogMessageBox();
                dlgMB.MessageTitle = "Populate a Data Field with Image Metadata of your Choosing...";
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
                    int row = this.imageDatabase.RowFindNextDisplayableImage(1); // Start at Row 1, as they are numbered from 1 onwards...
                    if (row >= 0)
                    {
                        this.ShowImage(row);
                    }
                }
                else
                {
                    return;
                }
            }

            DialogPopulateFieldWithMetadata dlg = new DialogPopulateFieldWithMetadata(this.imageDatabase, this.imageDatabase.RowGetValueFromType(Constants.DatabaseElement.File), this.FolderPath);
            dlg.Owner = this;
            bool? result = dlg.ShowDialog();
            if (result == true)
            {
                this.ShowImage(this.imageDatabase.CurrentRow);
                this.state.IsContentChanged = true;
            }
        }

        /// <summary>Delete the current image by replacing it with a placeholder image, while still making a backup of it</summary>
        private void Delete_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            try
            {
                int i = this.imageDatabase.GetDeletedImagesCounts();
                this.MenuItemDeleteImages.IsEnabled = i > 0;
                this.MenuItemDeleteImagesAndData.IsEnabled = i > 0;
                this.MenuItemDeleteImageAndData.IsEnabled = true; // (quality == Constants.ImageQuality.CORRUPTED || quality == Constants.ImageQuality.MISSING) ? false : true;
                string quality = this.imageDatabase.RowGetValueFromDataLabel(Constants.DatabaseElement.ImageQuality);
                this.MenuItemDeleteImage.IsEnabled = (quality == Constants.ImageQuality.Corrupted || quality == Constants.ImageQuality.Missing) ? false : true;
            }
            catch
            {
                // TODO THIS FUNCTION WAS BLOWING UP ON THERESAS MACHINE, NOT SURE WHY> SO TRY TO RESOLVE IT WITH THIS FALLBACK.
                this.MenuItemDeleteImages.IsEnabled = true;
                this.MenuItemDeleteImagesAndData.IsEnabled = true;
                this.MenuItemDeleteImage.IsEnabled = true;
                this.MenuItemDeleteImageAndData.IsEnabled = true;
            }
        }

        private void MenuItemDeleteImage_Click(object sender, RoutedEventArgs e)
        {
            ImageProperties imageProperties = new ImageProperties();
            imageProperties.File = this.imageDatabase.RowGetValueFromType(Constants.DatabaseElement.File);
            imageProperties.Folder = this.imageDatabase.RowGetValueFromType(Constants.DatabaseElement.Folder);
            imageProperties.ImageQuality = (ImageQualityFilter)Enum.Parse(typeof(ImageQualityFilter), (string)this.imageDatabase.RowGetValueFromType(Constants.DatabaseElement.ImageQuality));

            MenuItem sendingMenuItem = sender as MenuItem;
            bool deleteData = !sendingMenuItem.Name.Equals(this.MenuItemDeleteImage.Name);
            DialogDeleteImage deleteImageDialog = new DialogDeleteImage(this.imageDatabase, imageProperties, this.FolderPath, deleteData);
            deleteImageDialog.Owner = this;
            bool? result = deleteImageDialog.ShowDialog();
            if (result == true)
            {
                // Shows the deleted image placeholder // (although if it is already marked as corrupted, it will show the corrupted image placeholder)
                if (sendingMenuItem.Name.Equals(this.MenuItemDeleteImageAndData.Name))
                {
                    this.imageDatabase.ToDataRowPrevious();
                    this.SetImageFilterAndIndex(this.imageDatabase.CurrentRow, this.state.ImageFilter);
                }
                this.ShowImage(this.imageDatabase.CurrentRow, false);
            }
        }

        /// <summary> Delete all images marked for deletion, and optionally the data associated with those images.
        /// Deleted images are actually moved to a backup folder.</summary>
        private void MenuItemDeleteImages_Click(object sender, RoutedEventArgs e)
        {
            MenuItem mi = sender as MenuItem;

            DataTable deletedTable = this.imageDatabase.GetDataTableOfImagesMarkedForDeletion();
            if (null == deletedTable)
            {
                // It really should never get here, as this menu will be disabled if there aren't any images to delete. 
                // Still,...
                DialogMessageBox dlgMB = new DialogMessageBox();
                dlgMB.MessageTitle = "No images are marked for deletion";
                dlgMB.MessageProblem = "You are trying to delete images marked for deletion, but none of the images have their 'Delete?' field checkmarked.";
                dlgMB.MessageHint = "If you have images that you think should be deleted, checkmark its Delete? field.";
                dlgMB.IconType = MessageBoxImage.Information;
                dlgMB.ButtonType = MessageBoxButton.OK;
                dlgMB.ShowDialog();
                return;
            }

            DialogDeleteImages dlg;
            if (mi.Name.Equals("MenuItemDeleteImages"))
            {
                dlg = new DialogDeleteImages(this.imageDatabase, deletedTable, this.FolderPath, false);   // don't delete data
            }
            else
            {
                dlg = new DialogDeleteImages(this.imageDatabase, deletedTable, this.FolderPath, true);   // delete data
            }
            dlg.Owner = this;

            bool? result = dlg.ShowDialog();
            if (result == true)
            {
                this.SetImageFilterAndIndex(this.imageDatabase.CurrentRow, this.state.ImageFilter);
                this.ShowImage(this.imageDatabase.CurrentRow, false);
            }
        }

        /// <summary> Add some text to the Image Set Log </summary>
        private void MenuItemLog_Click(object sender, RoutedEventArgs e)
        {
            DialogEditLog dlg = new DialogEditLog(this.imageDatabase.Log);
            dlg.Owner = this;
            bool? result = dlg.ShowDialog();
            if (result == true)
            {
                this.imageDatabase.Log = dlg.LogContents;
                this.state.IsContentChanged = true;
            }
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            int previousRow = this.imageDatabase.CurrentRow - 1;
            if (previousRow < 0)
            {
                return; // We are already on the first image, so there is nothing to copy
            }

            string type = String.Empty;
            foreach (KeyValuePair<string, DataEntryControl> pair in this.dataEntryControls.ControlFromDataLabel)
            {
                type = (string)this.imageDatabase.TypeFromKey[pair.Key];
                if (null == type)
                {
                    type = "Not a control";
                }

                DataEntryControl control = pair.Value;
                if (this.imageDatabase.TemplateIsCopyable(control.DataLabel))
                {
                    control.Content = this.imageDatabase.RowGetValueFromDataLabel(control.DataLabel, previousRow);
                }
            }
            this.state.IsContentChanged = true; // We've altered some content
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

        /// <summary> Increase the magnification of the magnifying glass. We do this several times to make
        /// the increase effect more visible through a menu option versus the keyboard equivalent </summary>
        private void MenuItemMagnifierIncrease_Click(object sender, RoutedEventArgs e)
        {
            this.markableCanvas.MagnifierZoomIn();
            this.markableCanvas.MagnifierZoomIn();
            this.markableCanvas.MagnifierZoomIn();
            this.markableCanvas.MagnifierZoomIn();
            this.markableCanvas.MagnifierZoomIn();
            this.markableCanvas.MagnifierZoomIn();
        }

        /// <summary>  Decrease the magnification of the magnifying glass. We do this several times to make
        /// the increase effect more visible through a menu option versus the keyboard equivalent </summary>
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
                    int row = this.imageDatabase.RowFindNextDisplayableImage(1); // Start at Row 1, as they are numbered from 1 onwards...
                    if (row >= 0)
                    {
                        this.ShowImage(row);
                    }
                }
                else
                {
                    return;
                }
            }
            DialogOptionsDarkImagesThreshold dlg = new DialogOptionsDarkImagesThreshold(this.imageDatabase, this.state);
            dlg.Owner = this;
            bool? result = dlg.ShowDialog();
            if (result == true)
            {
                this.state.IsContentChanged = true;
            }
        }

        /// <summary> Swap the day / month fields if possible </summary>
        private void MenuItemSwapDayMonth_Click(object sender, RoutedEventArgs e)
        {
            // If we are not in the filter all view, or if its a corrupt image, tell the person. Selecting ok will shift the views..
            if (this.imageDatabase.RowIsImageDisplayable() == false || this.state.ImageFilter != ImageQualityFilter.All)
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
                    int row = this.imageDatabase.RowFindNextDisplayableImage(1); // Start at Row 1, as they are numbered from 1 onwards...
                    if (row >= 0)
                    {
                        this.ShowImage(row);
                    }
                }
                else
                {
                    return;
                }
            }

            DialogDateSwapDayMonth dlg = new DialogDateSwapDayMonth(this.imageDatabase);
            dlg.Owner = this;
            bool? result = dlg.ShowDialog();
            if (result == true)
            {
                this.ShowImage(this.imageDatabase.CurrentRow);
            }
        }

        /// <summary> Correct the date by specifying an offset </summary>
        private void MenuItemDateCorrections_Click(object sender, RoutedEventArgs e)
        {
            // If we are not in the filter all view, or if its a corrupt image, tell the person. Selecting ok will shift the views..
            if (this.imageDatabase.RowIsImageDisplayable() == false || this.state.ImageFilter != ImageQualityFilter.All)
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
                    int row = this.imageDatabase.RowFindNextDisplayableImage(1); // Start at Row 1, as they are numbered from 1 onwards...
                    if (row >= 0)
                    {
                        this.ShowImage(row);
                    }
                }
                else
                {
                    return;
                }
            }

            // We should be in the right mode for correcting the date
            DialogDateCorrection dlg = new DialogDateCorrection(this.imageDatabase);
            dlg.Owner = this;
            bool? result = dlg.ShowDialog();
            if (result == true)
            {
                this.ShowImage(this.imageDatabase.CurrentRow);
            }
        }

        /// <summary> Correct for daylight savings time</summary>
        private void MenuItemCorrectDaylightSavings_Click(object sender, RoutedEventArgs e)
        {
            // If we are not in the filter all view, or if its a corrupt image, tell the person. Selecting ok will shift the views..
            if (this.imageDatabase.RowIsImageDisplayable() == false || this.state.ImageFilter != ImageQualityFilter.All)
            {
                if (this.state.ImageFilter != ImageQualityFilter.All)
                {
                    DialogMessageBox dlgMB = new DialogMessageBox();
                    dlgMB.MessageTitle = "Can't correct for daylight savings time...";
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
                        this.ShowImage(0);
                    }
                }
                else
                {
                    // Just a corrupted image
                    DialogMessageBox dlgMB = new DialogMessageBox();
                    dlgMB.MessageTitle = "Can't correct for daylight savings time...";
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

            DialogDateTimeChangeCorrection dlg = new DialogDateTimeChangeCorrection(this.imageDatabase);
            dlg.Owner = this;
            bool? result = dlg.ShowDialog();
            if (result == true)
            {
                this.ShowImage(this.imageDatabase.CurrentRow);
            }
        }

        private void MenuItemCheckModifyAmbiguousDates_Click(object sender, RoutedEventArgs e)
        {
            // If we are not in the filter all view, or if its a corrupt image, tell the person. Selecting ok will shift the views..
            if (this.imageDatabase.RowIsImageDisplayable() == false || this.state.ImageFilter != ImageQualityFilter.All)
            {
                DialogMessageBox dlgMB = new DialogMessageBox();
                dlgMB.MessageTitle = "Check and Modify Ambiguous Dates...";
                dlgMB.MessageProblem = "To check and modify ambiguous dates, Timelapse must first be filtered to view All Images (normally set  in the Filter menu)";
                dlgMB.MessageSolution = "Select 'Ok' for Timelapse to set the filter to 'All Images'.";
                dlgMB.IconType = MessageBoxImage.Exclamation;
                dlgMB.ButtonType = MessageBoxButton.OKCancel;
                bool? msg_result = dlgMB.ShowDialog();

                // Set the filter to show all images and a valid image
                if (msg_result == true)
                {
                    this.SetImageFilterAndIndex(Constants.DefaultImageRowIndex, ImageQualityFilter.All); // Set it to all images
                    int row = this.imageDatabase.RowFindNextDisplayableImage(1); // Start at Row 1, as they are numbered from 1 onwards...
                    if (row >= 0)
                    {
                        this.ShowImage(row);
                    }
                }
                else
                {
                    return;
                }
            }

            DialogDateModifyAmbiguousDates dlg = new DialogDateModifyAmbiguousDates(this.imageDatabase);
            dlg.Owner = this;
            bool? result = dlg.ShowDialog();
            if (result == true)
            {
                this.ShowImage(this.imageDatabase.CurrentRow);
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
                    int row = this.imageDatabase.RowFindNextDisplayableImage(1); // Start at Row 1, as they are numbered from 1 onwards...
                    if (row >= 0)
                    {
                        this.ShowImage(row);
                    }
                }
                else
                {
                    return;
                }
            }

            DialogDateRereadDatesFromImages dlg = new DialogDateRereadDatesFromImages(this.imageDatabase);
            dlg.Owner = this;
            bool? result = dlg.ShowDialog();
            if (result == true)
            {
                this.ShowImage(this.imageDatabase.CurrentRow);
            }
        }

        /// <summary>  Toggle the audio feedback on and off </summary>
        private void MenuItemAudioFeedback_Click(object sender, RoutedEventArgs e)
        {
            // We don't have to do anything here...
            this.state.AudioFeedback = !this.state.AudioFeedback;
            this.MenuItemAudioFeedback.IsChecked = this.state.AudioFeedback;
        }

        /// <summary> Show advanced options</summary>
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
            int[] counts = this.imageDatabase.GetImageCounts();

            this.MenuItemViewLightImages.IsEnabled = counts[(int)ImageQualityFilter.Ok] > 0;
            this.MenuItemViewDarkImages.IsEnabled = counts[(int)ImageQualityFilter.Dark] > 0;
            this.MenuItemViewCorruptedImages.IsEnabled = counts[(int)ImageQualityFilter.Corrupted] > 0;
            this.MenuItemViewMissingImages.IsEnabled = counts[(int)ImageQualityFilter.Missing] > 0;
            this.MenuItemViewImagesMarkedForDeletion.IsEnabled = this.imageDatabase.GetDeletedImagesCounts() > 0;
        }

        private void MenuItemZoomIn_Click(object sender, RoutedEventArgs e)
        {
            lock (this.markableCanvas.imgToDisplay)
            {
                Point location = Mouse.GetPosition(this.markableCanvas.imgToDisplay);
                if (location.X > this.markableCanvas.imgToDisplay.ActualWidth || location.Y > this.markableCanvas.imgToDisplay.ActualHeight)
                {
                    return; // Ignore points if mouse is off the image
                }
                this.markableCanvas.ScaleImage(location, true); // Zooming in if delta is positive, else zooming out
            }
        }

        private void MenuItemZoomOut_Click(object sender, RoutedEventArgs e)
        {
            lock (this.markableCanvas.imgToDisplay)
            {
                Point location = Mouse.GetPosition(this.markableCanvas.imgToDisplay);
                this.markableCanvas.ScaleImage(location, false); // Zooming in if delta is positive, else zooming out
            }
        }

        /// <summary> Navigate to the next image in this image set </summary>
        private void MenuItemViewNextImage_Click(object sender, RoutedEventArgs e)
        {
            this.ViewNextImage(); // Goto the next image
        }

        /// <summary> Navigate to the previous image in this image set </summary>
        private void MenuItemViewPreviousImage_Click(object sender, RoutedEventArgs e)
        {
            this.ViewPreviousImage(); // Goto the previous image
        }

        /// <summary> Cycle through the image differences </summary>
        private void MenuItemViewDifferencesCycleThrough_Click(object sender, RoutedEventArgs e)
        {
            this.ViewDifferencesCycleThrough();
        }

        /// <summary> View the combined image differences </summary>
        private void MenuItemViewDifferencesCombined_Click(object sender, RoutedEventArgs e)
        {
            this.ViewDifferencesCombined();
        }

        /// <summary> Select the appropriate filter and update the view </summary>
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
            DialogCustomViewFilter dlg = new DialogCustomViewFilter(this.imageDatabase, this.customfilter);
            dlg.Owner = this;
            bool? msg_result = dlg.ShowDialog();
            // Set the filter to show all images and a valid image
            if (msg_result == true)
            {
                // MenuItemViewSetSelected(ImageQualityFilters.Custom);
                this.SetImageFilterAndIndex(Constants.DefaultImageRowIndex, ImageQualityFilter.Custom);
                // this.showImage(dbData.CurrentRow);
            }
        }

        /// <summary> Show a dialog box telling the user how many images were loaded, etc.</summary>
        public void MenuItemImageCounts_Click(object sender, RoutedEventArgs e)
        {
            int[] counts = this.imageDatabase.GetImageCounts();
            DialogStatisticsOfImageCounts dlg = new DialogStatisticsOfImageCounts(
                counts[(int)ImageQualityFilter.Ok],
                counts[(int)ImageQualityFilter.Dark],
                counts[(int)ImageQualityFilter.Corrupted],
                counts[(int)ImageQualityFilter.Missing]);
            dlg.Owner = this;
            dlg.ShowDialog();
        }

        /// <summary> Display the dialog showing the filtered view of the current database contents </summary>
        private void MenuItemViewFilteredDatabaseContents_Click(object sender, RoutedEventArgs e)
        {
            if (null != this.dlgDataView && this.dlgDataView.IsLoaded)
            {
                return; // If its already displayed, don't bother.
            }
            this.dlgDataView = new DialogDataView(this.imageDatabase);
            this.dlgDataView.Show();
        }
        #endregion 

        #region Help Menu Callbacks
        /// <summary> Display a help window </summary> 
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

        /// <summary>  Display a message describing the version, etc.</summary> 
        private void MenuOverview_About(object sender, RoutedEventArgs e)
        {
            DialogAboutTimelapse dlg = new DialogAboutTimelapse();
            dlg.Owner = this;
            dlg.ShowDialog();
        }

        /// <summary>  Display the Timelapse home page </summary> 
        private void MenuTimelapseWebPage_Click(object sender, RoutedEventArgs e)
        {
            Uri tutorialUri = new Uri("http://saul.cpsc.ucalgary.ca/timelapse/pmwiki.php?n=Main.Version2HomePage");
            Process.Start(new ProcessStartInfo(tutorialUri.AbsoluteUri));
        }

        /// <summary>   Display the manual in a web browser </summary> 
        private void MenuTutorialManual_Click(object sender, RoutedEventArgs e)
        {
            Uri tutorialUri = new Uri("http://saul.cpsc.ucalgary.ca/timelapse/uploads/Installs/Timelapse2/Timelapse2Manual.pdf");
            Process.Start(new ProcessStartInfo(tutorialUri.AbsoluteUri));
        }

        /// <summary>  Display the page in the web browser that lets you join the timelapse mailing list  </summary> 
        private void MenuJoinTimelapseMailingList_Click(object sender, RoutedEventArgs e)
        {
            Uri tutorialUri = new Uri("http://mailman.ucalgary.ca/mailman/listinfo/timelapse-l");
            Process.Start(new ProcessStartInfo(tutorialUri.AbsoluteUri));
        }

        /// <summary>  Download the sample images from a web browser  </summary> 
        private void MenuDownloadSampleImages_Click(object sender, RoutedEventArgs e)
        {
            Uri tutorialUri = new Uri("http://saul.cpsc.ucalgary.ca/timelapse/uploads/Main/TutorialImageSet2.zip");
            Process.Start(new ProcessStartInfo(tutorialUri.AbsoluteUri));
        }

        /// <summary> Send mail to the timelapse mailing list</summary> 
        private void MenuMailToTimelapseMailingList_Click(object sender, RoutedEventArgs e)
        {
            Uri tutorialUri = new Uri("mailto:timelapse-l@mailman.ucalgary.ca");
            Process.Start(new ProcessStartInfo(tutorialUri.AbsoluteUri));
        }
        #endregion

        #region Utilities
        // Return the index of the currently active counter Control, otherwise -1
        private DataEntryCounter FindSelectedCounter()
        {
            foreach (DataEntryCounter counter in this.dataEntryControls.CounterControls)
            {
                if (counter.IsSelected)
                {
                    return counter;
                }
            }
            return null;
        }

        public void ResetDifferenceThreshold()
        {
            this.DifferenceThreshold = Constants.DifferenceThresholdDefault;
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
        // Display the next image
        private void ViewNextImage()
        {
            this.state.IsContentValueChangedFromOutside = true;
            this.imageDatabase.ToDataRowNext();
            this.ViewRefresh();
            this.state.IsContentValueChangedFromOutside = false;
        }

        // Display the previous image
        private void ViewPreviousImage()
        {
            this.state.IsContentValueChangedFromOutside = true;
            this.imageDatabase.ToDataRowPrevious();
            this.ViewRefresh();
            this.state.IsContentValueChangedFromOutside = false;
        }

        // Refresh the view by readjusting the slider position and showing the image.
        private void ViewRefresh()
        {
            this.SldrImageNavigatorEnableCallback(false);
            this.ShowImage(this.imageDatabase.CurrentRow);
            this.SldrImageNavigatorEnableCallback(true);
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
            if (null != this.controlWindow)
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

        /// <summary> Show the Coding Controls in a separate window </summary>
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
            if (this.state.ImmediateExit)
            {
                return;
            }
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
