//#define UNSAFE

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using System.IO;
using System.ComponentModel;
using System.Windows.Shapes;
using System.Diagnostics;
using System.Media;
using System.Speech.Synthesis;
using System.Globalization;
using System.Threading;
using System.Windows.Threading;
using System.Collections;
using System.Data;

namespace Timelapse {
    /// <summary>
    /// Timelapse
    /// </summary>
    public partial class TimelapseWindow : Window
    {
        #region Public Variables

        public FileInfo[] imageFilePaths;                             // an array of image file names, in sequence

        public Template template;                           // the database that holds the template
        public DBData dbData = new DBData();                          // the database that holds all the data
        public string FolderPath = "";

        public byte differenceThreshold { get; set; } // The threshold used for calculating combined differences
        public byte differenceThresholdMax = 255;
        public byte differenceThresholdMin = 0;

        public List<MetaTagCounter> CounterCoords = null;
        #endregion

        #region Private Variables
        // Handles to the controls window and to the controls
        private ControlWindow controlWindow;    
        private Controls myControls;



        // These are used for Image differencing
        // If a person toggles between the current image and its two differenced imaes, those images are stored
        // in a 'cache' so they can be redisplayed more quickly (vs. re-reading it from a file or regenerating it)
        private enum whichImage {PreviousDiff = 0, Unaltered = 1, NextDiff = 2, CombinedDiff = 3 };
        private int whichImageState = (int)whichImage.Unaltered;
        private BitmapSource[] cachedImages = new BitmapSource[4];  // Cache of unaltered image [1], previous[0], next[2] and combined [3] differenced image
        private HelpWindow overviewWindow; // Create the help window. 
        private OptionsWindow optionsWindow; // Create the options window
        private MarkableImageCanvas markableCanvas;

        // Status information concerning the state of the UI
        private State state = new State();
        private Canvas magCanvas = new Canvas(); // This canvas will contain the image and marks used for the magnifying glass
        private System.Windows.Controls.Image magImg = new System.Windows.Controls.Image(); // and this contain the image within it

        // Speech feedback
        SpeechSynthesizer speechSynthesizer = new SpeechSynthesizer();

        // Persistant information saved in the registry
        private PersistInRegistry persist = new PersistInRegistry();
        private bool ImageFolderReopened = true; // Whether  the image folder in the current session is the same as the folder used in the last session

        private DlgDataView dlgDataView; 
        #endregion

        #region Constructors, Cleaning up, Destructors
        public TimelapseWindow()
        {
            InitializeComponent();
            CheckForUpdate.GetAndParseVersion (this, false);

            ResetDifferenceThreshold();
            this.markableCanvas = new MarkableImageCanvas();
            this.markableCanvas.HorizontalAlignment = HorizontalAlignment.Stretch;
            this.markableCanvas.PreviewMouseDown +=new MouseButtonEventHandler(markableCanvas_PreviewMouseDown);
            this.markableCanvas.MouseEnter += new MouseEventHandler(markableCanvas_MouseEnter);
            markableCanvas.RaiseMetaTagEvent += new EventHandler<MetaTagEventArgs>(markableCanvas_RaiseMetaTagEvent);
            this.mainUI.Children.Add(markableCanvas);

            // Callbacks so the controls will highlight if they are copyable when one enters the btnCopy button
            this.btnCopy.MouseEnter += btnCopy_MouseEnter; 
            this.btnCopy.MouseLeave += btnCopy_MouseLeave;

            // Create data controls, including reparenting the copy button from the main window into the my control window.
            myControls = new Controls(this.dbData);
            this.ControlGrid.Children.Remove(this.btnCopy);
            myControls.AddButton(this.btnCopy);

            // Recall states from prior sessions
            this.state.audioFeedback = persist.ReadAudioFeedback();
            this.state.controlWindowSize = persist.ReadControlWindowSize();
            this.MenuItemAudioFeedback.IsChecked = this.state.audioFeedback;
            this.MenuItemControlsInSeparateWindow.IsChecked = persist.ReadControlWindow();
        }

        // On exiting, save various attributes so we can use recover them later
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (this.state.immediateExit) return;

            // If there is no data to write (e.g., if we didn't actually open an image set), just exit
            if ((null == this.dbData) || (0 == this.dbData.ImageCount)) return;

            // Save the current filter set and the index of the current image being viewed in that set, and save it into the registry
            PersistState();

            if (null != this.controlWindow ) this.controlWindow.Close();
            if (null != dlgDataView) this.dlgDataView.Close();
        } 

        public void PersistState()
        {
            // Save the following in the database as they are local to this image set
            dbData.State_Filter = this.state.imageFilter;
            dbData.State_Row = this.dbData.CurrentRow;
            dbData.State_Magnifyer = this.markableCanvas.IsMagnifyingGlassVisible;

            // Save the following in the registry as they are global to timelapse use
            persist.WriteAudioFeedback(this.state.audioFeedback);
            persist.WriteLastImageFolderPath(this.FolderPath);     // Save the opened image path to the registry 
            persist.WriteControlWindow(this.MenuItemControlsInSeparateWindow.IsChecked);
            if (null != this.controlWindow)
            {
                persist.WriteControlWindowSize(state.controlWindowSize);
                Debug.Print("WriteSize:" + this.state.controlWindowSize.ToString());
            }
        }
        #endregion

        #region Image Loading
        // When the user clicks the 'Load' button, load all image information and prepare the interface.
        private void loadButton_Click(object sender, RoutedEventArgs e)
        {
            loadImagesFromSources();
        }

        // Load the code template and then the images from either the database (if it exists) or the actual images (if it doesn't exist)
        private void loadImagesFromSources ()
        {
            // First, select a template file which should reside with  the image set, otherwise abort loading (which means the user can try again later)
            // Also pass it the last image folder / template file viewed, which will be shown in the file dialog
            string tpath = persist.ReadLastImageFolderPath();       // the last path opened by the user is stored in the registry
            string filename = persist.ReadLastImageTemplateName();  // the template filname opened by the user is stored in the registry

            tpath = Utilities.GetTemplateFileFromUser(tpath, filename);  // Returns the path and the file name
            if (tpath == null) return;
            // Parse the returned file path to get just the filename and the path to the folder. 
            filename = System.IO.Path.GetFileName(tpath);
            if (filename.Equals ("")) filename = Constants.DBTEMPLATEFILENAME; 
            persist.WriteLastImageTemplateName(filename);  // We should reallyl do this on exit as well, but I didn't feel like storing it

            tpath = System.IO.Path.GetDirectoryName(tpath);

            if ("" == tpath || null == tpath) return;
            
            this.FolderPath = tpath;        // We keep the path in two places for convenience of referencing them
            this.dbData.FolderPath = tpath;

            // Create the template to the Timelapse Template database
            this.template = new Template();

            if (!template.Open(this.FolderPath, filename)) return;

            // We now have the template file. Load the TemplateTable from that file, which makes it data accessible through its table
            template.LoadTemplateTable();

            // If there is a database, then load the data from it
            if (this.dbData.Exists)
            {
                if (this.LoadImagesFromDB(template) == true)
                {
                    if (state.immediateExit) return;
                    LoadComplete(false);
                }
            }
            // there is no database. Populate the database by scanning the images in the folder
            else if (LoadDByScanningImageFolder() == false)
            {
                DlgNoImagesInFolder dlg = new DlgNoImagesInFolder(this.FolderPath);
                dlg.Owner = this;
                dlg.ShowDialog();
                return;
            }

            state.isContentChanged = false; // We've altered some content

            // For persistance: set a flag if We've opened the same image folder we worked with in the last session. 
            // If its different, saved the new folder path 
            this.ImageFolderReopened = (tpath == this.FolderPath) ? true : false;
        }

        // Load  all the jpg images found in the folder
        Boolean LoadDByScanningImageFolder ()
        {
            DateTimeHandler dateTimeHandler = new DateTimeHandler();
            FileInfo fileInfo;
            ProgressState progressState = new ProgressState();
            ImageProperties imgprop;  // Collect the image properties for for the 2nd pass...
            List<ImageProperties> imgprop_list = new List<ImageProperties>();
            Dictionary<String, String> dataline = new Dictionary<String, String>();   // Populate the data for the image
            Dictionary<String, String> markerline = new Dictionary<String, String>(); // Populate the markers database, where each key column corresponds to each key counter in the datatable
            int index = 0;
            string datalabel = "";
            bool ambiguous_daymonth_order = false;

            this.imageFilePaths = new DirectoryInfo(this.dbData.FolderPath).GetFiles("*.jpg");
            int count = imageFilePaths.Length;
            if (count == 0) return false;

            // Create the database and its table before we can load any data into it
            // Open a connection to the template DB
            bool result = this.dbData.CreateDB(template);

            // We generate the data user interface controls from the template description after the database has been created from the template
            myControls.GenerateControls(dbData);
            MenuItemControlsInSeparateWindow_Click(this.MenuItemControlsInSeparateWindow, null);  //this.ControlsInMainWindow();

            this.dbData.CreateTables();
            this.dbData.CreateLookupTables();

            // We want to show previews of the frames to the user as they are individually loaded
            // Because WPF uses a scene graph, we have to do this by a background worker, as this forces the update
            var bgw = new BackgroundWorker() { WorkerReportsProgress = true };
            bgw.DoWork += (ow, ea) =>
            {   // this runs on the background thread; its written as an anonymous delegate
                //We need to invoke this to allow updates on the UI
                this.Dispatcher.Invoke(new Action(() =>
                {;
                    // First, change the UI
                    this.helpControl.Visibility = System.Windows.Visibility.Collapsed;
                    Feedback(null, 0, "Examining images...");
                }));

                // First pass: Examine images to extract its basic properties
                BitmapSource bmap;
                BitmapSource corruptedbmp = BitmapFrame.Create(new Uri("pack://application:,,/Resources/corrupted.jpg"));;
                for (int i = 0; i < count; i++)
                {
                    fileInfo = imageFilePaths[i];
                     bmap = null;

                    imgprop = new ImageProperties();
                    imgprop.Name = fileInfo.Name;
                    imgprop.Folder = Utilities.GetFolderNameFromFolderPath(this.FolderPath);
                    try
                    {                        
                        // Create the bitmap and determine its ImageQuality 
                        bmap = BitmapFrame.Create(new Uri(fileInfo.FullName), BitmapCreateOptions.None, BitmapCacheOption.None);
                        bool dark = PixelBitmap.IsDark(bmap, 60, 0.9);  // 
                        imgprop.ImageQuality = (dark) ? (int)Constants.ImageQualityFilters.Dark : (int)Constants.ImageQualityFilters.Ok;
                    }
                    catch
                    {
                        bmap = corruptedbmp;
                        imgprop.ImageQuality = (int) Constants.ImageQualityFilters.Corrupted;
                    }

                    // Get the data from the metadata
                    BitmapMetadata meta = (BitmapMetadata) bmap.Metadata;
                    imgprop.DateMetadata = meta.DateTaken;
                    // For some reason, different versions of Windows treat creation time and modification time differently, 
                    // giving inconsisten values. So I just check both and take the lesser of the two.
                    DateTime time1 = File.GetCreationTime(fileInfo.FullName);
                    DateTime time2 = File.GetLastWriteTime(fileInfo.FullName);
                    imgprop.DateFileCreation = (DateTime.Compare (time1, time2) < 0) ? time1 : time2;
                    //string time3 = (meta.DateTaken == null) ? "null" : meta.DateTaken.ToString();
                    
                    //Debug.Print(fileInfo.Name + " " + time1.ToString() + " " + time2.ToString() + " " + time3);
                    imgprop.ID = index + 1; // its plus 1 as the Database IDs start at 1 rather than 0
                    imgprop_list.Add (imgprop);

                    index++;
                    int progress = Convert.ToInt32(Convert.ToDouble(index) / Convert.ToDouble(count) * 100);


                    if (index == 1 || (index % 1 == 0) )
                    {
                        progressState.Message = String.Format ("{0}/{1}: Examining {2}", i, count, imgprop.Name);
                        progressState.Bmap = bmap;
                        bgw.ReportProgress(progress, progressState);
                    }
                    else
                    {
                        progressState.Bmap = null;
                    }
                }

                // Second pass: Determine dates ... This can be pretty quick, so we don't really need to give any feedback on it.
                progressState.Message = "Second pass";
                progressState.Bmap = null;
                bgw.ReportProgress(0, progressState);
                ambiguous_daymonth_order = DateTimeHandler.VerifyAndUpdateDates(imgprop_list);

                // Third pass: Update database
                // TODO This is pretty slow... a good place to make it more efficient by adding multiple values in one shot

                // We need to get a list of which columns are counters vs notes or fixed coices, 
                // as we will shortly have to initialize them to some defaults
                List<string> CounterList = new List<string>();
                List<string> Notes_and_FixedChoicesList = new List<string>();
                List<string> FlagsList = new List<string>();
                for (int i = 0; i < this.dbData.dataTable.Columns.Count; i++)
                {
                    datalabel = this.dbData.dataTable.Columns[i].ColumnName;
                    string type = (string)this.dbData.TypeFromKey[datalabel];
                    if (null == type) continue; // Column must be the ID, which we skip over as its not a key.
                    if (type.Equals(Constants.COUNTER)) CounterList.Add(datalabel);
                    else if (type.Equals(Constants.NOTE) || type.Equals(Constants.FIXEDCHOICE)) Notes_and_FixedChoicesList.Add(datalabel);
                    else if (type.Equals(Constants.FLAG)) FlagsList.Add(datalabel);
                }

                // Create a dataline from the image properties, add it to a list of data lines,
                // then do a multiple insert of the list of datalines to the database 
                List <Dictionary<string, string>> dataline_list ; //= new List <Dictionary<string, string>> ();
                List<Dictionary<string, string>> markerline_list ; //= new List<Dictionary<string, string>>();
                //for (int i = 0; i < imgprop_list.Count; i++)
                
                const int interval = 100;
                for (int j = 0; j < imgprop_list.Count; j++)
                {
                    // Create a dataline from the image properties, add it to a list of data lines,
                    // then do a multiple insert of the list of datalines to the database 
                    dataline_list = new List<Dictionary<string, string>>();
                    markerline_list = new List<Dictionary<string, string>>();
                    for (int i = j; ( (i < (j + interval)) && (i < imgprop_list.Count) ); i++)
                    {
                       
                        // THE PROBLEM IS THAT WE ARE NOT ADDING THESE VALUES IN THE SAME ORDER AS THE TABLE
                        // THEY MUST BE IN THE SAME ORDER IE, AS IN THE COLUMNS. This case statement just fills up 
                        // the dataline in the same order as the template table.
                        // It assumes that the key is always the first column
                        dataline = new Dictionary<string, string>();
                        markerline = new Dictionary<string, string>();
                      //dataline.Add(Constants.ID, "NULL");     // Add the ID. Its Null to force autoincrement
                      //  markerline.Add(Constants.ID, (i+1).ToString());
                        for (int col = 0; col < dbData.dataTable.Columns.Count; col++) // Fill up each column in order
                        {
                            string col_datalabel = dbData.dataTable.Columns[col].ColumnName;
                            string type = (string) dbData.TypeFromKey [col_datalabel];
                            if (null == type) continue; // a null will be returned from the ID, as we don't add it to the typefromkey hash.
                            switch (type)
                            {
                                case Constants.FILE: // Add The File name
                                    datalabel = (string)this.dbData.DataLabelFromType[Constants.FILE];
                                    dataline.Add(datalabel, imgprop_list[i].Name);
                                    break;
                                case Constants.FOLDER: // Add The Folder name
                                    datalabel = (string)this.dbData.DataLabelFromType[Constants.FOLDER];
                                    dataline.Add(datalabel, imgprop_list[i].Folder);
                                    break;
                                case Constants.DATE:
                                    // Add the date
                                    datalabel = (string)this.dbData.DataLabelFromType[Constants.DATE];
                                    dataline.Add(datalabel, imgprop_list[i].FinalDate);
                                    break;
                                case Constants.TIME:
                                    // Add the time
                                    datalabel = (string)this.dbData.DataLabelFromType[Constants.TIME];
                                    dataline.Add(datalabel, imgprop_list[i].FinalTime);
                                    break;
                                case Constants.IMAGEQUALITY: // Add the Image Quality
                                    datalabel = (string)this.dbData.DataLabelFromType[Constants.IMAGEQUALITY];
                                    string str = Constants.IMAGEQUALITY_OK;
                                    if (imgprop_list[i].ImageQuality == (int)Constants.ImageQualityFilters.Dark) str = Constants.IMAGEQUALITY_DARK;
                                    else if (imgprop_list[i].ImageQuality == (int)Constants.ImageQualityFilters.Corrupted) str = Constants.IMAGEQUALITY_CORRUPTED;
                                    dataline.Add(datalabel, str);
                                    break;
                                case Constants.DELETEFLAG: // Add the Delete flag
                                    datalabel = (string)this.dbData.DataLabelFromType[Constants.DELETEFLAG];
                                    dataline.Add(datalabel, this.dbData.TemplateGetDefault(datalabel)); // Default as specified in the template file, which should be "false"
                                    break;
                                case Constants.NOTE:        // Find and then Add the Note or Fixed Choice
                                case Constants.FIXEDCHOICE:
                                    // Now initialize notes, counters, and fixed choices to the defaults
                                    foreach (string tkey in Notes_and_FixedChoicesList)
                                    {
                                        if (col_datalabel.Equals (tkey))
                                            dataline.Add(tkey, this.dbData.TemplateGetDefault(tkey) ); // Default as specified in the template file
                                        
                                    }
                                    break;
                                case Constants.FLAG:
                                    // Now initialize flags to the defaults
                                    foreach (string tkey in FlagsList)
                                    {
                                        if (col_datalabel.Equals(tkey))
                                            dataline.Add(tkey, this.dbData.TemplateGetDefault(tkey)); // Default as specified in the template file

                                    }
                                    break;
                                case Constants.COUNTER:
                                     foreach (string tkey in CounterList)
                                     { 
                                        if (col_datalabel.Equals(tkey))
                                        {
                                            dataline.Add(tkey, this.dbData.TemplateGetDefault(tkey)); // Default as specified in the template file
                                            markerline.Add(tkey, "");        // TODO ASSUMES THAT MARKER LIST IS IN SAME ORDER AS COUNTERS. THIS MAY NOT BE CORRECT ONCE WE SWITCH ROWS, SO SHOULD DO THIS SEPARATELY
                                        }
                                     }
                                    break;
                               
                                default:
                                    Debug.Print("Shouldn't be here!");
                                    break;
                            }
                        }
                        dataline_list.Add(dataline);
                        if (markerline.Count > 0)
                            markerline_list.Add(markerline);
                        index = i;

                    } 
                    this.dbData.InsertMultipleRows(Constants.TABLEDATA, dataline_list);
                    this.dbData.InsertMultipleRows(Constants.TABLEMARKERS, markerline_list);
                    j = j + interval - 1;
                    // Get the bitmap again to show it
                    if (imgprop_list[index].ImageQuality == (int)Constants.ImageQualityFilters.Corrupted)
                        bmap = corruptedbmp;
                    else
                        bmap = BitmapFrame.Create(new Uri(System.IO.Path.Combine(this.dbData.FolderPath, imgprop_list[index].Name)), BitmapCreateOptions.None, BitmapCacheOption.None);

                    // Show progress. Since its slow, we may as well do it every update
                    int progress2 = Convert.ToInt32(Convert.ToDouble(index) / Convert.ToDouble(count) * 100);
                    progressState.Message = String.Format("{0}/{1}: Adding {2}", index, count, imgprop_list[index].Name);
                    progressState.Bmap = bmap;
                    bgw.ReportProgress(progress2, progressState);
                }
                // this.dbData.AddNewRow(dataline);
                // this.dbData.AddNewRow(markerline, Constants.TABLEMARKERS);
            };
            bgw.ProgressChanged += (o, ea) =>
            {   // this gets called on the UI thread
                ProgressState progstate = (ProgressState)ea.UserState;
                Feedback (progressState.Bmap, ea.ProgressPercentage, progressState.Message);
                this.feedbackCtl.Visibility = System.Windows.Visibility.Visible;
            };
            bgw.RunWorkerCompleted += (o, ea) =>
            {

                // this.dbData.GetImagesAll(); // Now load up the data table
                // Get rid of the feedback panel, and show the main interface
                this.feedbackCtl.Visibility = Visibility.Collapsed;
                this.feedbackCtl.ShowImage = null;

                this.markableCanvas.Visibility = Visibility.Visible;

                // Finally warn the user if there are any ambiguous dates in terms of day/month or month/day order
                if (ambiguous_daymonth_order)
                { 
                    DlgGetDateOrder dlg = new DlgGetDateOrder("first date", "second date");
                    dlg.Owner = this;
                    bool? result2 = dlg.ShowDialog();
                    if (result2 == false)
                    {
                        
                    }
                }
                LoadComplete(true);
                // If we want to import old data from the ImageData.xml file, we can do it here...
                // Check to see if there is an ImageData.xml file in here. If there is, ask the user
                // if we want to load the data from that...
                if (File.Exists(System.IO.Path.Combine(this.FolderPath, Constants.XMLDATAFILENAME)))
                {
                    DlgImportImageDataXMLFile dlg = new DlgImportImageDataXMLFile();
                    dlg.Owner = this;
                    bool? result3 = dlg.ShowDialog();
                    if (result3 == true)
                    {
                        ImageDataXML.Read(System.IO.Path.Combine(this.FolderPath, Constants.XMLDATAFILENAME), dbData.templateTable, dbData);
                        SetImageFilterAndIndex(this.dbData.State_Row, this.dbData.State_Filter); // TO regenerate the controls and markers for this image
                    }
                }
            };
            bgw.RunWorkerAsync();
            return true;
        }

        private void Feedback(BitmapSource bmap, int percent, string message)
        {
            this.feedbackCtl.ShowMessage = message;
            this.feedbackCtl.ShowProgress = percent;
            if (null != bmap) this.feedbackCtl.ShowImage = bmap;
        }
        //Try to load the images from the DB file.
        Boolean LoadImagesFromDB(Template template)
        {
            if (this.dbData.CreateDB(template))
            {
                // When we are loading from an existing data file, ensure that the template in the template db matches  stored in the data db
                List<string> errors = CheckCodesVsImageData();
                if (errors.Count > 0)
                {
                    DlgTemplatesDontMatch dlg = new DlgTemplatesDontMatch(errors);
                    dlg.Owner = this;
                    bool? result = dlg.ShowDialog();
                    if (result == true)
                    {
                        this.state.immediateExit = true;
                        Application.Current.Shutdown();
                        return true;
                    }
                    else
                    {
                        this.dbData.templateTable =  dbData.CreateDataTableFromDatabaseTable(Constants.TABLETEMPLATE);
                    }
                }

                // We generate the data user interface controls from the template description after the database has been created from the template
                myControls.GenerateControls(dbData);
                MenuItemControlsInSeparateWindow_Click(this.MenuItemControlsInSeparateWindow, null);  //this.ControlsInMainWindow();
                this.dbData.CreateLookupTables();
                this.dbData.GetImagesAll();
                return true;
            }
            return false;
        }

        //When we are done loading images, add callbacks, prepare the UI, set up the image set, and show the image.
        private void LoadComplete(bool isLoadInitializedFromImages)
        {
            // Load the Marker table from the database
            this.dbData.InitializeMarkerTableFromDataTable();

            // Set the magnifying glass status from the registry. 
            // Note that if it wasn't in the registry, the value returned will be true by default
            this.markableCanvas.IsMagnifyingGlassVisible = this.dbData.State_Magnifyer;

            // Add callbacks to all our controls
            MyAddControlsCallback();
            

            // Now that we have something to show, enable menus and menu items as needed
            // Note that we do not enable those menu items that would have no effect
            this.MenuItemLoadImages.IsEnabled = false;
            this.MenuItemExportThisImage.IsEnabled = true;
            this.MenuItemExportAsCSVAndPreview.IsEnabled = true;
            this.MenuItemExportAsCSV.IsEnabled = true;
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
            whichImageState = (int)whichImage.Unaltered;

            //Show the image, Hide the load button, and make the feedback panels visible
            this.sldrImageNavigatorEnableCallback(false);
            this.dbData.ToDataRowFirst();

            this.markableCanvas.Focus(); // Don't know if we need this... 

            // Finally, set the current filter and the image index to the same as the ones in the last session,
            // providing that we are working with the same image folder. 
            // Doing so also displays the image
            if (this.ImageFolderReopened)
                SetImageFilterAndIndex(this.dbData.State_Row, this.dbData.State_Filter);
            else  
                // Default to showing first  image of all images
                SetImageFilterAndIndex(0, (int)Constants.ImageQualityFilters.All);

            CreateBackups();

            // Finally, tell the user how many images were loaded, etc.
            if (isLoadInitializedFromImages) MenuItemImageCounts_Click(null, null);

        }

        // Check if the code template file matches the Image data file. If not, return a list of errors,
        // i.e., columns that appera in one but not the other.
        private List <string> CheckCodesVsImageData()
        {
            List<String> dbtable_list = new List<String>();
            List<String> templatetable_list = new List<String>();

            DataTable dbTemplateTable = dbData.CreateDataTableFromDatabaseTable(Constants.TABLETEMPLATE);

            // Create two lists that we will compare, each containing the DataLabels from the template in the template file vs. db file.
            for (int i = 0; i < dbTemplateTable.Rows.Count; i++)
            {
                dbtable_list.Add((string)dbTemplateTable.Rows[i][Constants.DATALABEL]);
            }
            for (int i = 0; i < this.template.templateTable.Rows.Count; i++)
            {
                templatetable_list.Add((string)this.template.templateTable.Rows[i][Constants.DATALABEL]);
            }

            // Check to see if there are field in the template template that are not in the db template
            List <string> errors = new List<string>();
            foreach (string s in templatetable_list)
            {
                if (!dbtable_list.Contains(s))
                {
                    errors.Add ("- A field with the DataLabel '" + s + "' was found in the Template, but nothing matches that in the Data." + Environment.NewLine);
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
        private bool SetImageFilterAndIndex(int index, int filter)
        {
            const string caption = "Could not change the view";
            // Change the filter to reflect what the user selected. Update the menu state accordingly
            // Set the checked status of the radio button menu items to the filter.

            if (filter == (int)Constants.ImageQualityFilters.All)           // All images
            {
                 this.dbData.GetImagesAll();
                 StatusBarUpdate.View(this.statusBar, "all images.");
                 MenuItemViewSetSelected((int)Constants.ImageQualityFilters.All);
                 if (null != this.dlgDataView)
                 {
                     this.dlgDataView.RefreshDataTable();  // If its displaye, update the window that shows the filtered view data base
                 }
            }
            else if (filter == (int)Constants.ImageQualityFilters.Ok) // Light images
            {
                if (this.dbData.GetImagesAllButDarkAndCorrupted())
                {
                    StatusBarUpdate.View(this.statusBar, "light images.");
                    MenuItemViewSetSelected((int)Constants.ImageQualityFilters.Ok);
                    if (null != this.dlgDataView)
                    {
                        this.dlgDataView.RefreshDataTable();  // If its displaye, update the window that shows the filtered view data base
                    }
                }
                else 
                {
                    StatusBarUpdate.Message(this.statusBar, "no light images to display.");
                    MessageBox.Show(this, "There are no light images to display.", caption, MessageBoxButton.OK, MessageBoxImage.Information);
                    if (this.state.imageFilter == (int)Constants.ImageQualityFilters.Ok) 
                        return SetImageFilterAndIndex(0, (int)Constants.ImageQualityFilters.All);
                    MenuItemViewSetSelected(this.state.imageFilter);
                    return false;
                }
            } 
            else if (filter == (int)Constants.ImageQualityFilters.Corrupted) // Corrupted images
            {
                if (this.dbData.GetImagesCorrupted())
                {
                    StatusBarUpdate.View(this.statusBar, "corrupted images.");
                    MenuItemViewSetSelected((int)Constants.ImageQualityFilters.Corrupted);
                    if (null != this.dlgDataView) { 
                        this.dlgDataView.RefreshDataTable();  // If its displaye, update the window that shows the filtered view data base
                    }
                }
                else
                {
                    StatusBarUpdate.Message(this.statusBar, "no corrupted images to display.");
                    MessageBox.Show(this, "There are no corrupted images to display.", caption, MessageBoxButton.OK, MessageBoxImage.Information);
                    if (this.state.imageFilter == (int)Constants.ImageQualityFilters.Corrupted) 
                        return SetImageFilterAndIndex(0, (int)Constants.ImageQualityFilters.All);
                     MenuItemViewSetSelected(this.state.imageFilter); 
                    return false;
                }
            }
            else if (filter == (int)Constants.ImageQualityFilters.Dark) // Dark images
            {
                if (this.dbData.GetImagesDark())
                {
                    StatusBarUpdate.View(this.statusBar, "dark images.");
                    MenuItemViewSetSelected((int)Constants.ImageQualityFilters.Dark);
                    if (null != this.dlgDataView)  
                    {
                        this.dlgDataView.RefreshDataTable();  // If its displaye, update the window that shows the filtered view data base
                    }
                }
                else
                {
                    StatusBarUpdate.Message (this.statusBar, "no dark images to display.");
                    MessageBox.Show(this, "There are no dark images to display.", caption, MessageBoxButton.OK, MessageBoxImage.Information);
                    if (this.state.imageFilter == (int)Constants.ImageQualityFilters.Dark) 
                        return SetImageFilterAndIndex(0, (int)Constants.ImageQualityFilters.All);
                    MenuItemViewSetSelected(this.state.imageFilter); 
                    return false;
                }
            }
            else if (filter == (int)Constants.ImageQualityFilters.Missing) // Missing images
            {
                if (this.dbData.GetImagesMissing())
                {
                    StatusBarUpdate.View(this.statusBar, "missing images.");
                    MenuItemViewSetSelected((int)Constants.ImageQualityFilters.Missing); 
                    if (null != this.dlgDataView) {
                        this.dlgDataView.RefreshDataTable();  // If its displaye, update the window that shows the filtered view data base
                    }
                }
                else
                {
                    StatusBarUpdate.Message (this.statusBar, "no missing images to display.");
                    MessageBox.Show(this, "There are no missing images to display.", caption, MessageBoxButton.OK, MessageBoxImage.Information);
                    if (this.state.imageFilter == (int)Constants.ImageQualityFilters.Missing) 
                        return SetImageFilterAndIndex(0, (int)Constants.ImageQualityFilters.All);
                    MenuItemViewSetSelected(this.state.imageFilter); 
                    return false;
                }
            }
            else if (filter == (int)Constants.ImageQualityFilters.MarkedForDeletion) // Images marked for deletion
            {
                if (this.dbData.GetImagesMarkedForDeletion())
                {
                    StatusBarUpdate.View(this.statusBar, "images marked for deletion.");
                    MenuItemViewSetSelected((int)Constants.ImageQualityFilters.MarkedForDeletion);
                    if (null != this.dlgDataView) { 
                        dlgDataView.RefreshDataTable();
                        this.MenuItemViewFilteredDatabaseContents_Click(null, null); //Regenerate the DataView if needed
                    }
                }
                else
                {
                    StatusBarUpdate.Message(this.statusBar, "no images marked for deletion to display.");
                    MessageBox.Show(this, "There are no images to display that are marked for deletion.", caption, MessageBoxButton.OK, MessageBoxImage.Information);
                    if (this.state.imageFilter == (int)Constants.ImageQualityFilters.MarkedForDeletion) 
                        return SetImageFilterAndIndex(0, (int)Constants.ImageQualityFilters.All);
                    MenuItemViewSetSelected(this.state.imageFilter);
                    return false;
                }
            }

            // Go to the first row.
            // We may want to change this to try to go to last saved image, if its in this filtered view.
            this.dbData.ToDataRowIndex (index); 
           
            // After a filter change, set the slider to represent the index and the count of the current filter
            this.sldrImageNavigatorEnableCallback(false);
            this.sldrImageNavigator.Maximum = this.dbData.ImageCount - 1;  // Reset the slider to the size of images in this set
            this.sldrImageNavigator.Value = this.dbData.CurrentRow;

            // Update the status bar accordingly
            StatusBarUpdate.CurrentImageNumber(statusBar, this.dbData.CurrentRow + 1);  // We add 1 because its a 0-based list
            StatusBarUpdate.TotalCount(statusBar, this.dbData.ImageCount);
            showImage(this.dbData.CurrentRow);
            this.sldrImageNavigatorEnableCallback(true);
            this.state.imageFilter = filter;    // Remember the current filter
            return true;
        }

        /// <summary> Make a backup of the database and csv files, if they exist </summary>
        private void CreateBackups()
        {
            string dbFile = System.IO.Path.Combine (this.FolderPath, Constants.DBIMAGEDATAFILENAME);
            string dbFolderBackup = System.IO.Path.Combine(this.FolderPath, Constants.BACKUPFOLDER);
            string dbFileBackup = System.IO.Path.Combine(this.FolderPath, Constants.BACKUPFOLDER, Constants.DBIMAGEDATABACKUPFILENAME);
            try
            {
                if (File.Exists(dbFile))
                {
                    bool foo = !Directory.Exists(dbFolderBackup);
                    if (!Directory.Exists(dbFolderBackup))
                        Directory.CreateDirectory(dbFolderBackup);
                    File.Copy(dbFile, dbFileBackup, true);
                }

                string csvFile = System.IO.Path.Combine (this.FolderPath, Constants.CSVIMAGEDATAFILENAME);
                string csvFileBackup = System.IO.Path.Combine(this.FolderPath, Constants.BACKUPFOLDER, Constants.CSVIMAGEDATABACKUPFILENAME);
                if (File.Exists(csvFile))
                {
                    File.Copy(csvFile, csvFileBackup, true);
                }
                StatusBarUpdate.Message(this.statusBar, "Backups of files made.");
            } 
            catch 
            {
                StatusBarUpdate.Message(this.statusBar, "No file backups were made.");
            }
        }
        #endregion

        #region Configure Callbacks
        /// Add callbacks to all our controls. When the user changes an image's attribute using a particular control,
        /// the callback updates the matching field for that image in the imageData structure.

        /// <summary>
        /// Add the event handler callbacks for our (possibly invisible)  controls
        /// </summary>
        private void MyAddControlsCallback()
        {
            string type = "";
            MyNote notectl;
            MyCounter counterctl;
            MyFixedChoice fixedchoicectl;
            MyFlag flagctl;
            foreach (DictionaryEntry pair in myControls.ControlFromDataLabel )
            {
                type = (string) dbData.TypeFromKey[pair.Key];
                if (null == type) type = "Not a control";
                switch (type)
                {
                    case Constants.FILE:
                    case Constants.FOLDER:
                    case Constants.TIME:
                    case Constants.DATE:
                    case Constants.NOTE:
                        notectl = (MyNote)pair.Value; // get the control
                        notectl.ContentCtl.TextChanged += new TextChangedEventHandler(noteCtl_TextChanged);
                        notectl.ContentCtl.PreviewKeyDown += new KeyEventHandler(contentCtl_PreviewKeyDown);
                        break;
                    case Constants.DELETEFLAG:
                    case Constants.FLAG:
                        flagctl = (MyFlag)pair.Value; // get the control
                        flagctl.ContentCtl.Checked += FlagCtl_CheckedChanged;
                        flagctl.ContentCtl.Unchecked += FlagCtl_CheckedChanged;
                        flagctl.ContentCtl.PreviewKeyDown += new KeyEventHandler(contentCtl_PreviewKeyDown);
                        break;
                    case Constants.IMAGEQUALITY:
                    case Constants.FIXEDCHOICE:
                        fixedchoicectl = (MyFixedChoice)pair.Value; // get the control
                        fixedchoicectl.ContentCtl.SelectionChanged += new SelectionChangedEventHandler(fixedChoiceCtl_SelectionChanged);
                        fixedchoicectl.ContentCtl.PreviewKeyDown += new KeyEventHandler(contentCtl_PreviewKeyDown);
                        break;
                    case Constants.COUNTER:
                        counterctl = (MyCounter)pair.Value; // get the control
                        counterctl.ContentCtl.TextChanged += new TextChangedEventHandler(counterCtl_TextChanged);
                        counterctl.ContentCtl.PreviewKeyDown += new KeyEventHandler(contentCtl_PreviewKeyDown);
                        counterctl.ContentCtl.PreviewTextInput += new TextCompositionEventHandler(counterCtl_PreviewTextInput);
                        counterctl.Container.Tag = counterctl.DataLabel; // So we can access the parent from the container during the callback
                        counterctl.Container.MouseEnter += new MouseEventHandler(contentCtl_MouseEnter);
                        counterctl.Container.MouseLeave += new MouseEventHandler(contentCtl_MouseLeave);
                        counterctl.LabelCtl.Click += new RoutedEventHandler(counterCtl_Click);
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
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void contentCtl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SetTopLevelFocus();
                e.Handled = true;
            }
            else
            {
                Control ctlSender = (Control)sender;
                ctlSender.Focus();
            }
        }

        // TODO: If the result is a blank (i.e., spaces or empty string), need to clean it up.
        /// <summary>Preview callback for counters, to ensure ensure that we only accept numbers</summary>
        /// <param name="sender"></param>
        /// <param name="ex"></param>
        private void counterCtl_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !AreAllValidNumericChars(e.Text);
            base.OnPreviewTextInput(e);
        }

        // Helper function for the above
        private bool AreAllValidNumericChars(string str)
        {
            foreach (char c in str)
            {
                if (!Char.IsNumber(c)) return false;
            }
            return true;
        }

        /// <summary>Click callback: When the user selects a counter, refresh the markers, which will also readjust the colors and emphasis</summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void counterCtl_Click(object sender, RoutedEventArgs e)
        {
            RefreshTheMarkableCanvasListOfMetaTags();
        }

        /// <summary>When the user enters a counter, store the index of the counter and then refresh the markers, which will also readjust the colors and emphasis</summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// 
        void contentCtl_MouseEnter(object sender, MouseEventArgs e)
        {
            Panel panel = (Panel)sender; 
            state.isMouseOverCounter = (string) panel.Tag;
            RefreshTheMarkableCanvasListOfMetaTags();
        }

        // When the user enters a counter, clear the saved index of the counter and then refresh the markers, which will also readjust the colors and emphasis
        void contentCtl_MouseLeave(object sender, MouseEventArgs e)
        {
            // Recolor the marks
            state.isMouseOverCounter = "";
            RefreshTheMarkableCanvasListOfMetaTags();
        }

        // Whenever the text in a particular note box changes, update the particular note field in the database 
        void noteCtl_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (state.isContentValueChangedFromOutside) return;
            TextBox tb = (TextBox)sender;

            // Get the key identifying the control, and then add its value to the database
            string data_label = (string) tb.Tag;
            dbData.RowSetValueFromDataLabel(data_label, tb.Text);
            state.isContentChanged = true; // We've altered some content
            state.isContentValueChangedFromOutside = false;
        }

        // Whenever the text in a particular counter box changes, update the particular counter field in the database
        void counterCtl_TextChanged(object sender, TextChangedEventArgs e)
        {
            if ( state.isContentValueChangedFromOutside ) return;
            TextBox tb = (TextBox)sender;

            // Get the key identifying the control, and then add its value to the database
            string data_label = (string)tb.Tag;
            dbData.RowSetValueFromDataLabel(data_label, tb.Text);
            state.isContentChanged = true; // We've altered some content
            state.isContentValueChangedFromOutside = false;
            return;
        }

        // Whenever the text in a particular fixedChoice box changes, update the particular choice field in the database
        void fixedChoiceCtl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (state.isContentValueChangedFromOutside) return;
            ComboBox cb = (ComboBox)sender;
            if (null == cb.SelectedItem) return; // Make sure an item was actually selected (it could have been cancelled)

            // Get the key identifying the control, and then add its value to the database
            string data_label = (string)cb.Tag;
            dbData.RowSetValueFromDataLabel(data_label, cb.SelectedItem.ToString());
            SetTopLevelFocus();
            state.isContentChanged = true; // We've altered some content
            state.isContentValueChangedFromOutside = false;
        }
        // Whenever the checked state in a Flag  changes, update the particular choice field in the database
        void FlagCtl_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (state.isContentValueChangedFromOutside) return;
            CheckBox cb = (CheckBox)sender;
            // Get the key identifying the control, and then add its value to the database
            string data_label = (string)cb.Tag;
            string value = ((bool) cb.IsChecked) ? "true" : "false";
            dbData.RowSetValueFromDataLabel(data_label, value);
            state.isContentChanged = true; // We've altered some content
            state.isContentValueChangedFromOutside = false;
            return;
        }
        /// <summary>
        ///  When the mouse enters / leaves the copy button, the controls that are copyable will be highlit. 
        /// </summary>
        void btnCopy_MouseEnter(object sender, MouseEventArgs e)
        {
            string type = "";
            MyControl control;
            foreach (DictionaryEntry pair in myControls.ControlFromDataLabel)
            {
                type = (string)dbData.TypeFromKey[pair.Key];
                switch (type)
                {
                    case Constants.FILE:
                    case Constants.FOLDER:
                    case Constants.TIME:
                    case Constants.DATE:
                    case Constants.NOTE:
                    case Constants.FLAG:
                    case Constants.IMAGEQUALITY:
                    case Constants.DELETEFLAG:
                    case Constants.FIXEDCHOICE:
                    case Constants.COUNTER:
                        control = (MyControl)pair.Value;
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
        ///  When the mouse enters / leaves the copy button, the controls that are copyable will be highlit. 
        /// </summary>
        void btnCopy_MouseLeave(object sender, MouseEventArgs e)
        {
            string type = "";
            MyControl control;
            foreach (DictionaryEntry pair in myControls.ControlFromDataLabel)
            {
                type = (string)dbData.TypeFromKey[pair.Key];
                switch (type)
                {
                    case Constants.FILE:
                    case Constants.FOLDER:
                    case Constants.TIME:
                    case Constants.DATE:
                    case Constants.NOTE:
                    case Constants.FLAG:
                    case Constants.IMAGEQUALITY:
                    case Constants.DELETEFLAG:
                    case Constants.FIXEDCHOICE:
                    case Constants.COUNTER:
                        control = (MyControl)pair.Value;
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
            NextInCycle();
            
            // If we are supposed to display the unaltered image, do it and get out of here.
            // The unaltered image will always be cached at this point, so there is no need to check.
            if (whichImageState == (int) whichImage.Unaltered)
            {
                 this.markableCanvas.imgToMagnify.Source = cachedImages[(int) whichImage.Unaltered];
                 this.markableCanvas.imgToDisplay.Source = cachedImages[(int) whichImage.Unaltered];
                 
                 // Check if its a corrupted image
                 if (!dbData.RowIsImageDisplayable()) //TO DO AS WE MAY HAVE TO GET THE INDEX OF THE NEXT IN CYCLE IMAGE???
                 { 
                     StatusBarUpdate.Message(this.statusBar, "Image is corrupted");
                 }
                 else
                 {
                     StatusBarUpdate.ClearMessage(this.statusBar);
                 }
                 return;
            }

            // If we don't have the cached difference image, generate and cache it.
            if (cachedImages[whichImageState] == null)
            {
                // Decide which comparison image to use for differencing. 
                int idx;
                if ( whichImageState == (int)whichImage.PreviousDiff )
                {
                    idx = this.dbData.CurrentRow - 1;   // Find the previous image (unless we are already at the beginning)
                    if (idx < 0) idx = this.dbData.CurrentRow;
                } 
                else
                {
                    idx = this.dbData.CurrentRow + 1;
                    if (idx >= dbData.ImageCount) idx = this.dbData.CurrentRow;
                }

                // Generate the differenced image. 
                string fullFileName = System.IO.Path.Combine(this.FolderPath, this.dbData.RowGetValueFromDataLabel((string) this.dbData.DataLabelFromType [Constants.FILE], idx)); 
                // Check if that file actually exists
                if (!File.Exists (fullFileName))
                {
                    StatusBarUpdate.Message(this.statusBar, "Difference Image is missing");
                    return;
                }
                var otherImage = new BitmapImage(new Uri(fullFileName));
                var image1 = new PixelBitmap((BitmapSource)cachedImages[(int)whichImage.Unaltered]);
                var image2 = new PixelBitmap((BitmapSource)otherImage);
                var difference = image1 - image2;
                BitmapSource img = difference.ToBitmap();

                // and now cache the differenced image
                cachedImages[(int)whichImageState] = (BitmapSource)img;
            }
            // display the differenced image
            this.markableCanvas.imgToDisplay.Source = cachedImages[whichImageState];
            StatusBarUpdate.Message(this.statusBar, "Viewing " + ( (whichImageState == (int) whichImage.PreviousDiff) ? "previous" : "next") + " differenced image");
        }

        // Set the next image in the cycle
        private void NextInCycle()
        {
            // If we are looking at the combined differenced image, then always go to the unaltered image.
            if (whichImageState == (int) whichImage.CombinedDiff)
            {
                whichImageState = (int)whichImage.Unaltered;
                return;
            }

            // If the current image is marked as corrupted, we will only show the original (replacement) image
            int idx = this.dbData.CurrentRow;
            if (!dbData.RowIsImageDisplayable())
            {
                whichImageState = (int)whichImage.Unaltered;
                return;
            }
            else // We are going around in a cycle, so go back to the beginning if we are at the end of it.
            {
                whichImageState = (whichImageState >= (int)whichImage.NextDiff) ? (int)whichImage.PreviousDiff : ++whichImageState;
            }

            // Because we can always display the unaltered image, we don't have to do any more tests if that is the current one in the cyle
            if (whichImageState == (int)whichImage.Unaltered) return;

            // We can't actually show the previous or next image differencing if we are on the first or last image in the set respectively
            // Nor can we do it if the next image in the sequence is a corrupted one.
            // If that is the case, skip to the next one in the sequence
            if ( whichImageState == (int)whichImage.PreviousDiff && this.dbData.CurrentRow == 0 )  // Already at the beginning
            {
                NextInCycle();
            }
            else if (whichImageState == (int)whichImage.NextDiff && this.dbData.CurrentRow == this.dbData.ImageCount - 1) // Already at the end
            {
                NextInCycle();
            }
            //
            else if (whichImageState == (int)whichImage.NextDiff && !dbData.RowIsImageDisplayable( dbData.CurrentRow+1)) // Can't use the next image as its corrupted
            {
                NextInCycle();
            }
            else if (whichImageState == (int)whichImage.PreviousDiff && !dbData.RowIsImageDisplayable(dbData.CurrentRow - 1)) // Can't use the previous image as its corrupted
            {
                NextInCycle();
            }
        }
        // TODO: This needs to be fixed.
        public void ViewDifferencesCombined()
        {
            // If we are in any state other than the unaltered state, go to the unaltered state, otherwise the combined diff state
            if (whichImageState == (int)whichImage.NextDiff || whichImageState == (int)whichImage.PreviousDiff || whichImageState == (int) whichImage.CombinedDiff)
            {
                whichImageState = (int)whichImage.Unaltered;
            } 
            else 
            {
                whichImageState = (int)whichImage.CombinedDiff; 
            }

            // If we are on the unaltered image
            if (whichImageState == (int)whichImage.Unaltered)
            {
                this.markableCanvas.imgToDisplay.Source = this.cachedImages[whichImageState];
                this.markableCanvas.imgToMagnify.Source = this.cachedImages[whichImageState];
                StatusBarUpdate.ClearMessage(this.statusBar); 
                return;
            } 

            // If we are on  the first image, or the last image, then don't do anything
            if (this.dbData.CurrentRow == 0 || this.dbData.CurrentRow == dbData.ImageCount - 1) 
            {
                whichImageState = (int) whichImage.Unaltered;
                StatusBarUpdate.Message(this.statusBar, "Can't show combined differences without three good images");
                return;
            }

            // If any of the images are corrupted, then don't do anything
            if (!dbData.RowIsImageDisplayable() || !dbData.RowIsImageDisplayable(dbData.CurrentRow + 1) || !dbData.RowIsImageDisplayable(dbData.CurrentRow - 1)) 
            {
                whichImageState = (int) whichImage.Unaltered;
                StatusBarUpdate.Message(this.statusBar, "Can't show combined differences without three good images");
                return;
            }

            if (null == this.cachedImages[whichImageState])
            {
                // We need three valid images: the current one, the previous one, and the next one.
                // The current image is always in the cache. Create a PixeBitmap from it
                PixelBitmap currImage = new PixelBitmap((BitmapSource)cachedImages[(int)whichImage.Unaltered]);

                // Get the previous and next image
                int idx = dbData.CurrentRow - 1;

                string path = System.IO.Path.Combine(this.FolderPath, dbData.RowGetValueFromType((string)this.dbData.DataLabelFromType[Constants.FILE], idx));
                if (!File.Exists(path))
                {
                    StatusBarUpdate.Message(this.statusBar, "Can't show combined differences without three good images");
                    return;
                }
                BitmapImage prevImage = new BitmapImage(new Uri(path));

                idx = dbData.CurrentRow + 1; ;
                path = System.IO.Path.Combine(this.FolderPath, dbData.RowGetValueFromType((string)this.dbData.DataLabelFromType[Constants.FILE], idx));
                if (!File.Exists(path))
                {
                    StatusBarUpdate.Message(this.statusBar, "Can't show combined differences without three good images");
                    return;
                } 
                BitmapImage nextImage = new BitmapImage(new Uri(path));

                // Generate the differenced image and dislay it
                PixelBitmap differencedImage = PixelBitmap.Difference(cachedImages[(int)whichImage.Unaltered], prevImage, nextImage, differenceThreshold);
                this.cachedImages[whichImageState] = differencedImage.ToBitmap();
            }

            whichImageState = (int)whichImage.CombinedDiff;
            this.markableCanvas.imgToDisplay.Source = this.cachedImages[whichImageState];
            StatusBarUpdate.Message(this.statusBar, "Viewing surrounding differences");
        }
 
        #endregion

        #region Slider Event Handlers and related
        private void sldrImageNavigator_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            state.isContentValueChangedFromOutside = true;
            this.dbData.ToDataRowIndex((int)sldrImageNavigator.Value);
            this.showImage(this.dbData.CurrentRow);
            state.isContentValueChangedFromOutside = false;
        }

        public void sldrImageNavigatorEnableCallback(bool state)
        {
            if (state) this.sldrImageNavigator.ValueChanged += new RoutedPropertyChangedEventHandler<double>(sldrImageNavigator_ValueChanged);
            else this.sldrImageNavigator.ValueChanged -= new RoutedPropertyChangedEventHandler<double>(sldrImageNavigator_ValueChanged);
        }
        #endregion
          
        #region Showing the current Images
         // Display the current image
        public void showImage(int index)
        {
            showImage(index, true); // by default, use cached images
        }
        public void showImage(int index, bool UseCachedImages)
        {
            dbData.ToDataRowIndex (index); 

            // Get and display the bitmap
            var bi = new BitmapImage();
            string srcFile = System.IO.Path.Combine(this.dbData.FolderPath, this.dbData.RowGetValueFromType (Constants.FILE));
            if (this.dbData.RowGetValueFromType (Constants.IMAGEQUALITY).Equals (Constants.IMAGEQUALITY_CORRUPTED))
            {

                bi = Utilities.BitmapFromResource(bi, "corrupted.jpg", UseCachedImages, 0, 0);
            }
            else
            {
                if (File.Exists(srcFile))
                {
                    this.MenuItemDeleteImage.IsEnabled = true;
                    Utilities.BitmapFromFile(bi, srcFile, UseCachedImages);
                }
                else
                {
                    // The file is missing! show the missing image placeholder 
                    if ( ! Constants.IMAGEQUALITY_MISSING.Equals (dbData.RowGetValueFromType (Constants.IMAGEQUALITY))) // If its not already tagged as missing, then tag it as such.
                        dbData.RowSetValueFromDataLabel((string) dbData.DataLabelFromType [Constants.IMAGEQUALITY], Constants.IMAGEQUALITY_MISSING);
                    bi = Utilities.BitmapFromResource(bi, "missing.jpg", UseCachedImages, 0, 0);
                }
            }
            this.markableCanvas.imgToDisplay.Source = bi;

            // For each control, we get its type and then update its contents from the current data table row
            string type;
            MyNote notectl;
            MyFixedChoice fixedchoicectl;
            MyCounter counterctl;
            MyFlag flagctl;
            foreach (DictionaryEntry pair in myControls.ControlFromDataLabel)
            {
                type = (string)dbData.TypeFromKey[pair.Key];
                if (null == type) type = "Not a control";
                switch (type)
                {
                    case Constants.FILE:
                        notectl = (MyNote)pair.Value; 
                        notectl.Content = this.dbData.RowGetValueFromType(Constants.FILE);
                        break;
                    case Constants.FOLDER:
                        notectl = (MyNote)pair.Value; 
                        notectl.Content = this.dbData.RowGetValueFromType(Constants.FOLDER);
                        break;
                    case Constants.TIME:
                        notectl = (MyNote)pair.Value; 
                        notectl.Content = this.dbData.RowGetValueFromType(Constants.TIME);
                        break;
                    case Constants.DATE:
                        notectl = (MyNote)pair.Value; 
                        notectl.Content = this.dbData.RowGetValueFromType(Constants.DATE);
                        break;
                    case Constants.IMAGEQUALITY:
                        fixedchoicectl = (MyFixedChoice)pair.Value;
                        fixedchoicectl.Content = this.dbData.RowGetValueFromType(Constants.IMAGEQUALITY);
                        break;
                    case Constants.DELETEFLAG:
                        flagctl = (MyFlag)pair.Value; // get the control
                        flagctl.Content = this.dbData.RowGetValueFromDataLabel(flagctl.DataLabel);
                        break;
                    case Constants.NOTE:
                        notectl = (MyNote)pair.Value; // get the control
                        notectl.Content = this.dbData.RowGetValueFromDataLabel(notectl.DataLabel);
                        break;
                    case Constants.FLAG:
                        flagctl = (MyFlag)pair.Value; // get the control
                        flagctl.Content = this.dbData.RowGetValueFromDataLabel(flagctl.DataLabel);
                        break;
                    case Constants.FIXEDCHOICE:
                        fixedchoicectl = (MyFixedChoice)pair.Value; // get the control
                        fixedchoicectl.Content = this.dbData.RowGetValueFromDataLabel(fixedchoicectl.DataLabel);
                        break;
                    case Constants.COUNTER:
                        counterctl = (MyCounter)pair.Value; // get the control
                        counterctl.Content = this.dbData.RowGetValueFromDataLabel(counterctl.DataLabel);
                        break;
                    default:
                        break;
                }
            }

            // update the status bar to show which image we are on out of the total
            StatusBarUpdate.CurrentImageNumber(statusBar, this.dbData.CurrentRow + 1); // Add one because indexes are 0-based
            StatusBarUpdate.TotalCount(statusBar, this.dbData.dataTable.Rows.Count);
            StatusBarUpdate.ClearMessage(statusBar);

            this.sldrImageNavigator.Value = this.dbData.CurrentRow;

            //Set the magImage to the source so the unaltered image will appear on the magnifying glass
            //Although its probably not needed, also make the magCanvas the same size as the image
            this.markableCanvas.imgToMagnify.Source = bi;

            //Whenever we navigate to a new image, delete any markers that were displayed on the current image 
            //and then draw the markers assoicated with the new image
            this.GetTheMarkableCanvasListOfMetaTags();
            this.RefreshTheMarkableCanvasListOfMetaTags();

            //Always cache the current image
            cachedImages[(int)whichImage.Unaltered] = (BitmapImage)this.markableCanvas.imgToMagnify.Source;

            //Also reset the differencing variables
            cachedImages[(int)whichImage.PreviousDiff] = null;
            cachedImages[(int)whichImage.NextDiff] = null;
            cachedImages[(int)whichImage.CombinedDiff] = null;

            // And track that we are on the unaltered image
            this.whichImageState = (int)whichImage.Unaltered;
        }
         #endregion
       
        #region Keyboard shortcuts
        // If its an arrow key and the textbox doesn't have the focus,
        // navigate left/right image or up/down to look at differenced image
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (dbData.ImageCount == 0) return; // No images are loaded, so don't try to interpret any keys

            // Don't interpret keyboard shortcuts over any of the controls on the control grid, as the key entered may be directed
            // to the controls within it
            if ( this.ControlGrid.IsMouseOver) return;

            // This was the old code where we just checked the type. Keep it around in case we have to revert to it.
            // If  a textbox or combo box has the focus, then abort as this is normal text input
            //  and NOT a shortcut key
            //IInputElement ie = (IInputElement) FocusManager.GetFocusedElement(this);
            //if (ie == null) return;
            //Type type = ie.GetType();
            // if (typeof(TextBox) == type || typeof(ComboBox) == type || typeof(ComboBoxItem) == type)
            // {
            //     return;
            // }
            
            
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
                    ViewDifferencesCycleThrough();
                    break;
                case Key.Down:              // show visual difference to previous image
                    ViewDifferencesCombined();
                    break;
                case Key.C:
                    btnCopy_Click (null, null);
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
                this.MenuItemOptionsBox.IsEnabled = false;
        }
        #endregion

        #region Setting Focus
        // Because of shortcut keys, we want to reset the focus when appropriate to the 
        // image control. This is done from various places.

        // Whenever the user clicks on the image, reset the image focus to the image control 
        private void markableCanvas_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            SetTopLevelFocus();
        }


        // When we move over the canvas, reset the top level focus
        private void markableCanvas_MouseEnter(object sender, MouseEventArgs e)
        {
            SetTopLevelFocus();
        }

        // Actually set the top level keyboard focus to the image control
        private void SetTopLevelFocus()
        {
            // Don't raise the window just because we set the keyboard focus to it
            Keyboard.DefaultRestoreFocusMode = RestoreFocusMode.None;
            Keyboard.Focus(this.markableCanvas);
            //this.markableCanvas.Focus();      // TODO old remnants. Can probably delete these three lines. Not even sure what the lower two did.
            //IInputElement ie = (IInputElement)FocusManager.GetFocusedElement(this);
            //if (ie == null) return;
        }
        #endregion

        #region Marking and Counting

        // Get all the counters' metatags (if any)  from the current row in the database
        private void GetTheMarkableCanvasListOfMetaTags()
        {
            this.CounterCoords = this.dbData.MarkerTableGetMetaTagCounterList();
        }

        // Event handler: A marker, as defined in e.MetaTag, has been either added (if e.IsNew is true) or deleted (if it is false)
        // Depending on which it is, add or delete the tag from the current counter control's list of tags 
        // If its deleted, remove the tag from the current counter control's list of tags
        // Every addition / deletion requires us to:
        // - update the contents of the counter control 
        // - update the data held by the image
        // - update the list of MetaTags held by that counter
        // - regenerate the list of metatags used by the markableCanvas
        void markableCanvas_RaiseMetaTagEvent(object sender, MetaTagEventArgs e)
        {
            MyCounter currentCounter;
            if (e.IsNew)  // A marker has been added
            {
                currentCounter = FindSelectedCounter(); // No counters are selected, so don't mark anything
                if (null == currentCounter) return;
                Markers_NewMetaTag(currentCounter, e.metaTag);
            }
            else // An existing marker has been deleted.
            {
                MyCounter myCounter = (MyCounter) this.myControls.ControlFromDataLabel[e.metaTag.DataLabel];
                    
                // Part 1. Decrement the count 
                string old_counter_data = myCounter.Content;
                string new_counter_data = "";
                int count = Convert.ToInt32(old_counter_data);
                count = (count == 0) ? 0 : count - 1;           // Make sure its never negative, which could happen if a person manually enters the count 
                new_counter_data = count.ToString();
                if (! new_counter_data.Equals (old_counter_data)) // DOn't bother updating if the value hasn't changed (i.e., already at a 0 count)
                {
                    // Update the datatable and database with the new counter values
                    state.isContentValueChangedFromOutside = true;
                    myCounter.Content = new_counter_data ;
                    this.dbData.UpdateRow(dbData.CurrentId, myCounter.DataLabel, new_counter_data, Constants.TABLEDATA);
                    state.isContentValueChangedFromOutside = false;
                }

                // Part 2. Each metacounter in the countercoords list reperesents a different control. 
                // So just check the first metatag's  DataLabel in each metatagcounter to see if it matches the counter's datalabel.
                MetaTagCounter mtagCounter = null;
                int index = -1;         // Index is the position of the match within the CounterCoords
                foreach (MetaTagCounter mtcounter in this.CounterCoords)
                {
                    // If there are no metatags, we don't have to do anything.
                    if (mtcounter.MetaTags.Count == 0) continue;  
                    // There are no metatags associated with this counter
                    if (mtcounter.MetaTags[0].DataLabel == myCounter.Key)
                    {
                        // We found the metatag counter associated with that control
                        index++;
                        mtagCounter = mtcounter; 
                        break;
                    }
                }

                // Part 3. Remove the found metatag from the metatagcounter and from the database
                string point_list = "";
                Point point;
                if (mtagCounter != null)  // Shouldn't really need this test, but if for some reason there wasn't a match...
                {
                    for (int i = 0; i < mtagCounter.MetaTags.Count; i++)
                    {
                        // Check if we are looking at the same metatag. 
                        if (e.metaTag.Guid == mtagCounter.MetaTags[i].Guid)
                        {
                            // We found the metaTag. Remove that metatag from the metatags list 
                            mtagCounter.MetaTags.RemoveAt(i);
                            Speak(myCounter.Content); // Speak the current count
                        }
                        else // Because we are not deleting it, we can add it to the new the point list
                        {
                            // Reconstruct the point list in the string form x,y|x,y e.g.,  0.333,0.333|0.500, 0.600
                            // for writing to the markerTable. Note that it leaves out the deleted value
                            point = mtagCounter.MetaTags[i].Point;
                            if (!point_list.Equals("")) point_list += Constants.MARKERBAR;          // We don't put a marker bar at the beginning of the point list
                            point_list += String.Format("{0:0.000},{1:0.000}", point.X, point.Y);   // Add a point in the form 
                        }
                    }
                    dbData.UpdateRow(dbData.CurrentId, myCounter.DataLabel, point_list, Constants.TABLEMARKERS);
                }
                this.RefreshTheMarkableCanvasListOfMetaTags(); // Refresh the Markable Canvas, where it will also delete the metaTag at the same time
             }
             markableCanvas.MarkersRefresh();
             state.isContentChanged = true; // We've altered some content
        }

        /// <summary>
        /// A new Marker associated with a counter control has been created;
        /// Increment the counter controls value, and add the metatag to all data structures (including the database)
        /// </summary>
        /// <param name="myCounter"></param>
        /// <param name="mtag"></param>
        private void Markers_NewMetaTag(MyCounter myCounter, MetaTag mtag)
        {
            // Get the Counter Control's contents,  increment its value (as we have added a new marker) 
            // Then update the control's content as well as the database
            string counter_data = myCounter.Content;
            if (String.IsNullOrEmpty(counter_data)) counter_data = "0";
            int count = Convert.ToInt32(counter_data);
            count++;
            counter_data = count.ToString();
            this.state.isContentValueChangedFromOutside = true;
            dbData.UpdateRow(dbData.CurrentId, myCounter.DataLabel, counter_data);  
            myCounter.Content = counter_data;
            state.isContentValueChangedFromOutside = false;

            // Find the metatagCounter associated with this particular control so we can add a metatag to it
            MetaTagCounter metatagCounter = null;
            foreach (MetaTagCounter mtcounter in this.CounterCoords)
            {
                if (mtcounter.DataLabel == myCounter.Key)
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
            String pointlist = "";
            foreach (MetaTag mt in metatagCounter.MetaTags)
            {
                if (! pointlist.Equals ("") ) pointlist += Constants.MARKERBAR; // We don't put a marker bar at the beginning of the point list
                pointlist += String.Format("{0:0.000},{1:0.000}", mt.Point.X, mt.Point.Y); // Add a point in the form x,y e.g., 0.5, 0.7
            }
            this.dbData.MarkerTableAddPoint(myCounter.DataLabel, pointlist);
            RefreshTheMarkableCanvasListOfMetaTags(true);
            Speak(myCounter.Content + " " + myCounter.Label); // Speak the current count
        }


        // Create a list of metaTags from those stored in each image's metatag counters, 
        // and then set the markableCanvas's list of metaTags to that list. We also reset the emphasis for those tags as needed.
        private void RefreshTheMarkableCanvasListOfMetaTags()
        {
            RefreshTheMarkableCanvasListOfMetaTags(false); // By default, we don't show the annotation
        }
        private void RefreshTheMarkableCanvasListOfMetaTags(bool show_annotation)
        {
            // The markable canvas uses a simple list of metatags to decide what to do.
            // So we just create that list here, where we also reset the emphasis of some of the metatags
            List<MetaTag> metaTagList = new List<MetaTag>();

            MyCounter selectedCounter = this.FindSelectedCounter ();
            for (int i = 0; i < CounterCoords.Count; i++)
            {
                MetaTagCounter mtagCounter = CounterCoords[i];
                MyCounter current_counter = (MyCounter)this.myControls.ControlFromDataLabel[mtagCounter.DataLabel];

                // Update the emphasise for each tag to reflect how the user is interacting with tags
                foreach (MetaTag mtag in mtagCounter.MetaTags)
                {
                    mtag.Emphasise = (state.isMouseOverCounter == mtagCounter.DataLabel) ? true : false;
                    if (null != selectedCounter && current_counter.DataLabel == selectedCounter.DataLabel)
                    {
                         mtag.Brush = (SolidColorBrush)new BrushConverter().ConvertFromString(Constants.MARKER_SELECTIONCOLOR);
                    } 
                    else
                    {
                         mtag.Brush = (SolidColorBrush)new BrushConverter().ConvertFromString(Constants.MARKER_STANDARDCOLOR);
                    }

                    // the first time through, show an annotation. Otherwise we clear the flags to hide the annotation.
                    if (mtag.Annotate && !mtag.AnnotationAlreadyShown)
                    {
                        mtag.Annotate = true;
                        mtag.AnnotationAlreadyShown = true ;
                    }
                    else
                    {
                        mtag.Annotate = false;  
                    }
                    mtag.Label = current_counter.Label;
                    metaTagList.Add(mtag); // Add the MetaTag in the list 
                }
            }
            markableCanvas.MetaTags = metaTagList;
        }
        #endregion

        #region File Menu Callbacks

        /// <summary> Load the images from a folder </summary>
        private void MenuItemLoadImages_Click(object sender, RoutedEventArgs e)
        {
            this.loadImagesFromSources();
        }

        /// <summary> Write the  CSV file and preview it in excel.</summary>
        private void MenuItemExportCSV_Click(object sender, RoutedEventArgs e)
        {
            // Write the file
            SpreadsheetWriter.ExportDataAsCSV(this.dbData, System.IO.Path.Combine(this.FolderPath, Constants.CSVIMAGEDATAFILENAME));

            MenuItem mi = (MenuItem)sender;
            if (mi == this.MenuItemExportAsCSVAndPreview)
            {
                // Create a process that will try to show the file
                string csvpath = System.IO.Path.Combine(this.FolderPath, Constants.CSVIMAGEDATAFILENAME);
                Process process = new Process();

                process.StartInfo.UseShellExecute = true;
                process.StartInfo.RedirectStandardOutput = false;
                process.StartInfo.FileName = csvpath;
                process.Start();
            }
            state.isContentChanged = false; // We've altered some content
        }

        /// <summary> 
        /// Export the current image to the folder selected by the user via a folder browser dialog.
        /// and provide feedback in the status bar if done.
        /// </summary>
        private void MenuItemExportThisImage_Click(object sender, RoutedEventArgs e)
        {
            if (!this.dbData.RowIsImageDisplayable())
            {
                Messages.CantExportThisImage();
                return;
            }
            //Get the file name of the current image 
            string sourceFile = this.dbData.RowGetValueFromType(Constants.FILE);

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
                string sourceFileName = System.IO.Path.Combine(this.FolderPath, sourceFile);
                string destFileName = dialog.FileName;

                // Try to copy the source file to the destination, overwriting the destination file if it already exists.
                // And giving some feedback about its success (or failure) 
                try
                {
                    System.IO.File.Copy(sourceFileName, destFileName, true);
                    StatusBarUpdate.Message(this.statusBar, sourceFile + " copied to " + destFileName);
                }
                catch
                {
                    StatusBarUpdate.Message(this.statusBar, "Copy failed for some reason!");
                }
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
        /// <summary> Delete the current image by replacing it with a placeholder image, while still making a backup of it</summary>
        private void Delete_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            try
            {
                int i = this.dbData.GetDeletedImagesCounts();
                this.MenuItemDeleteImages.IsEnabled = (i > 0);
                this.MenuItemDeleteImagesAndData.IsEnabled = (i > 0);
                this.MenuItemDeleteImageAndData.IsEnabled = true;//(quality == Constants.IMAGEQUALITY_CORRUPTED || quality == Constants.IMAGEQUALITY_MISSING) ? false : true;
                string quality = this.dbData.RowGetValueFromDataLabel(Constants.IMAGEQUALITY);
                this.MenuItemDeleteImage.IsEnabled = (quality == Constants.IMAGEQUALITY_CORRUPTED || quality == Constants.IMAGEQUALITY_MISSING) ? false : true;
            }
            catch // TODO THIS FUNCTION WAS BLOWING UP ON THERESAS MACHINE, NOT SURE WHY> SO TRY TO RESOLVE IT WITH THIS FALLBACK.
            {
                this.MenuItemDeleteImages.IsEnabled = true;
                this.MenuItemDeleteImagesAndData.IsEnabled = true;
                this.MenuItemDeleteImage.IsEnabled = true;
                this.MenuItemDeleteImageAndData.IsEnabled = true;
            }
        }
        
        private void MenuItemDeleteImage_Click(object sender, RoutedEventArgs e)
        {
            bool isCorrupted = (Constants.IMAGEQUALITY_CORRUPTED).Equals ( (string)this.dbData.RowGetValueFromType (Constants.IMAGEQUALITY));
            MenuItem mi = sender as MenuItem;
            DlgDeleteImage dlg;
            int filter = this.state.imageFilter;
            int currentrow = this.dbData.CurrentRow;
            if (mi.Name.Equals(this.MenuItemDeleteImage.Name))
            {
                dlg = new DlgDeleteImage(this.dbData, this.dbData.RowGetValueFromType(Constants.FILE), this.FolderPath, isCorrupted, false);
            }
            else
            {
                dlg = new DlgDeleteImage(this.dbData, this.dbData.RowGetValueFromType(Constants.FILE), this.FolderPath, isCorrupted, true); // Delete the data as well
            }
            dlg.Owner = this;
            bool? result = dlg.ShowDialog();
            if (result == true)
            {
                // Shows the deleted image placeholder // (although if it is already marked as corrupted, it will show the corrupted image placeholder)
                if (mi.Name.Equals(this.MenuItemDeleteImageAndData.Name))
                {
                    this.dbData.ToDataRowPrevious();
                    this.SetImageFilterAndIndex(currentrow, filter);
                }
                this.showImage(this.dbData.CurrentRow, false);
            }
        }

        /// <summary> Delete all images marked for deletion, and optionally the data associated with those images.
        /// Deleted images are actually moved to a backup folder.</summary>
        private void MenuItemDeleteImages_Click(object sender, RoutedEventArgs e)
        {
            int filter = this.state.imageFilter;
            int currentrow = this.dbData.CurrentRow;
            MenuItem mi = sender as MenuItem;

            DataTable deletedTable = this.dbData.GetDataTableOfImagesMarkedForDeletion();
            if (null==deletedTable)
            {
                MessageBox.Show("No images are marked for deletion");
                return;
            }
            DlgDeleteImages dlg;
            if (mi.Name.Equals ("MenuItemDeleteImages"))
                dlg = new DlgDeleteImages(this.dbData, deletedTable, this.FolderPath, false);   // don't delete data
            else
                dlg = new DlgDeleteImages(this.dbData, deletedTable, this.FolderPath, true);   // delete data
            dlg.Owner = this;

            bool? result = dlg.ShowDialog();
            if (result == true)
            {
                this.SetImageFilterAndIndex(currentrow, filter);
                this.showImage(this.dbData.CurrentRow, false);
            }
        }

        /// <summary> Add some text to the Image Set Log </summary>
        private void MenuItemLog_Click(object sender, RoutedEventArgs e)
        {
            DlgEditLog dlg = new DlgEditLog(this.dbData.Log);
            dlg.Owner = this;
            bool? result = dlg.ShowDialog();
            if (result == true)
            {
                this.dbData.Log = dlg.LogContents;
                this.state.isContentChanged = true;
            }
        }
     

        private void btnCopy_Click(object sender, RoutedEventArgs e)
        {
            int previousRow = this.dbData.CurrentRow - 1;
            if (previousRow < 0) return; // We are already on the first image, so there is nothing to copy

            // Because it doesn't make sense, the filename and folder path fields will never be copyable, even though they may be marked as such in the Code Template File
            // It really doesn't make sense for some other fields to be copyable either (e.g., folderpath, time), but I will leave that up to the biologist
            string type = "";
            MyNote notectl;
            MyFlag flagctl;
            MyCounter counterctl;
            MyFixedChoice fixedchoicectl;
            foreach (DictionaryEntry pair in myControls.ControlFromDataLabel)
            {
                type = (string)dbData.TypeFromKey[pair.Key];
                if (null == type) type = "Not a control";
                switch (type)
                {
                    // case Constants.FILE:     
                    // case Constants.FOLDER:
                    case Constants.TIME:
                    case Constants.DATE:
                    case Constants.NOTE:
                        notectl = (MyNote)pair.Value; // get the control
                        if (this.dbData.TemplateIsCopyable(notectl.Key))
                            notectl.Content = this.dbData.RowGetValueFromDataLabel(notectl.Key, previousRow);
                        break;
                    case Constants.DELETEFLAG:
                    case Constants.FLAG:
                         flagctl = (MyFlag)pair.Value; // get the control
                        if (this.dbData.TemplateIsCopyable(flagctl.Key))
                            flagctl.Content = this.dbData.RowGetValueFromDataLabel(flagctl.Key, previousRow);
                        break;
                    case Constants.IMAGEQUALITY:
                    case Constants.FIXEDCHOICE:
                        fixedchoicectl = (MyFixedChoice)pair.Value; // get the control
                        if (this.dbData.TemplateIsCopyable(fixedchoicectl.Key))
                            fixedchoicectl.Content = this.dbData.RowGetValueFromDataLabel(fixedchoicectl.Key, previousRow);
                        break;
                    case Constants.COUNTER:
                        counterctl = (MyCounter)pair.Value; // get the control
                        if (this.dbData.TemplateIsCopyable(counterctl.DataLabel))
                            counterctl.Content = this.dbData.RowGetValueFromDataLabel(counterctl.DataLabel, previousRow);
                        break;
                    default:
                        break;
                }
            }
            state.isContentChanged = true; // We've altered some content
        }
        #endregion

        #region Options Menu Callbacks
        /// <summary> Toggle the showing of controls in a separate window</summary>
        private void MenuItemControlsInSeparateWindow_Click(object sender, RoutedEventArgs e)
        {
            MenuItem mi = (MenuItem)sender;
            if (mi.IsChecked)
                this.ControlsInSeparateWindow();
            else
                this.ControlsInMainWindow();
        }

        /// <summary> Toggle the magnifier on and off</summary>
        private void MenuItemMagnifier_Click(object sender, RoutedEventArgs e)
        {
            //We don't have to do anything here...
            this.markableCanvas.IsMagnifyingGlassVisible = !this.markableCanvas.IsMagnifyingGlassVisible;
            MenuItemMagnifier.IsChecked = this.markableCanvas.IsMagnifyingGlassVisible;
        }

        /// <summary> Increase the magnification of the magnifying glass. We do this several times to make
        /// the increase effect more visible through a menu option vs. the keyboard equivalent </summary>
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
        /// the increase effect more visible through a menu option vs. the keyboard equivalent </summary>
        private void MenuItemMagnifierDecrease_Click(object sender, RoutedEventArgs e)
        {
            this.markableCanvas.MagnifierZoomOut();
            this.markableCanvas.MagnifierZoomOut();
            this.markableCanvas.MagnifierZoomOut();
            this.markableCanvas.MagnifierZoomOut();
            this.markableCanvas.MagnifierZoomOut();
            this.markableCanvas.MagnifierZoomOut();
        }

        /// <summary> Swap the day / month fields if possible </summary>
        private void MenuItemSwapDayMonth_Click(object sender, RoutedEventArgs e)
        {
            // If we are not in the filter all view, or if its a corrupt image, tell the person. Selecting ok will shift the views..
            if (dbData.RowIsImageDisplayable() == false || this.state.imageFilter != (int)Constants.ImageQualityFilters.All)
            {
                string messageBoxText = "To swap the day / month, Timelapse must first  : " + Environment.NewLine;
                messageBoxText += " - be viewing All Images (in View menu) and " + Environment.NewLine;
                messageBoxText += " - preferably be displaying a valid image" + Environment.NewLine + Environment.NewLine;
                messageBoxText += "Select 'Ok' for Timelapse to do this.";
                string caption = "Swap the day / month...";
                MessageBoxButton button = MessageBoxButton.OKCancel;
                MessageBoxImage icon = MessageBoxImage.Information;
                MessageBoxResult msg_result = MessageBox.Show(this, messageBoxText, caption, button, icon);

                // Set the filter to show all images and a valid image
                if (msg_result == MessageBoxResult.OK)
                {
                    SetImageFilterAndIndex(0, (int)Constants.ImageQualityFilters.All); // Set it to all images
                    int row = dbData.RowFindNextDisplayableImage(1); // Start at Row 1, as they are numbered from 1 onwards...
                    if (row >= 0) showImage(row);
                }
                else return;
            }

            DlgSwapDayMonth dlg = new DlgSwapDayMonth(this.dbData);
            dlg.Owner = this;
            bool? result = dlg.ShowDialog();
            if (result == true)
            {
                this.showImage(dbData.CurrentRow);
            }
        }

        /// <summary> Correct the date by specifying an offset </summary>
        private void MenuItemDateCorrections_Click(object sender, RoutedEventArgs e)
        {
            // If we are not in the filter all view, or if its a corrupt image, tell the person. Selecting ok will shift the views..
            if (dbData.RowIsImageDisplayable() == false || this.state.imageFilter != (int)Constants.ImageQualityFilters.All)
            {
                string messageBoxText = "To correct the date, Timelapse must first  : " + Environment.NewLine;
                messageBoxText += " - be viewing All Images (in View menu)" + Environment.NewLine;
                messageBoxText += " - preferably be displaying a valid image" + Environment.NewLine + Environment.NewLine;
                messageBoxText += "Select 'Ok' for Timelapse to do this.";
                string caption = "Correct the date...";
                MessageBoxButton button = MessageBoxButton.OKCancel;
                MessageBoxImage icon = MessageBoxImage.Information;
                MessageBoxResult msg_result = MessageBox.Show(this, messageBoxText, caption, button, icon);

                // Set the filter to show all images and a valid image
                if (msg_result == MessageBoxResult.OK)
                {
                    SetImageFilterAndIndex(0, (int)Constants.ImageQualityFilters.All); // Set it to all images
                    int row = dbData.RowFindNextDisplayableImage(1); // Start at Row 1, as they are numbered from 1 onwards...
                    if (row >= 0) showImage(row);
                }
                else return;
            }

            // We should be in the right mode for correcting the date
            DlgDateCorrection dlg = new DlgDateCorrection(dbData);
            dlg.Owner = this;
            bool? result = dlg.ShowDialog();
            if (result == true)
            {
                this.showImage(dbData.CurrentRow);
            }
        }

        /// <summary> Correct for daylight savings time</summary>
        private void MenuItemCorrectDaylightSavings_Click(object sender, RoutedEventArgs e)
        {
            // If we are not in the filter all view, or if its a corrupt image, tell the person. Selecting ok will shift the views..
            if (dbData.RowIsImageDisplayable() == false || this.state.imageFilter != (int)Constants.ImageQualityFilters.All)
            {
                MessageBoxImage icon = MessageBoxImage.Information;
                string caption = "Correct for daylight savings time...";
                string messageBoxText = "To correct for daylight savings time, Timelapse must be first viewing  : " + Environment.NewLine;
                if (this.state.imageFilter != (int)Constants.ImageQualityFilters.All)
                {
                    messageBoxText += " - all images (in View menu)" + Environment.NewLine + Environment.NewLine;
                    messageBoxText += "Select 'Ok' for Timelapse to view All Images," + Environment.NewLine;
                    messageBoxText += " - then navigate to the image that is at the savings time threshold" + Environment.NewLine + Environment.NewLine;
                    MessageBoxButton button = MessageBoxButton.OKCancel;
                    MessageBoxResult msg_result = MessageBox.Show(this, messageBoxText, caption, button, icon);

                    // Set the filter to show all images and then go to the first image
                    if (msg_result == MessageBoxResult.OK)
                    {
                        SetImageFilterAndIndex(0, (int)Constants.ImageQualityFilters.All); // Set it to all images
                        showImage(0);
                    }
                }
                else // Just a corrupted image
                {
                    messageBoxText += " - an Image with a valid date" + Environment.NewLine + Environment.NewLine;
                    messageBoxText += "You need to navigate to the image whose date is at the savings time threshold";
                    MessageBoxButton button = MessageBoxButton.OK;
                    MessageBox.Show(this, messageBoxText, caption, button, icon);
                }
                return;
            }

            DlgTimeChangeCorrection dlg = new DlgTimeChangeCorrection(this.dbData);
            dlg.Owner = this;
            bool? result = dlg.ShowDialog();
            if (result == true)
            {
                this.showImage(dbData.CurrentRow);
            }
        }

        /// <summary>  Toggle the audio feedback on and off </summary>
        private void MenuItemAudioFeedback_Click(object sender, RoutedEventArgs e)
        {
            //We don't have to do anything here...
            this.state.audioFeedback = !this.state.audioFeedback;
            this.MenuItemAudioFeedback.IsChecked = this.state.audioFeedback;
        }

        /// <summary> Show advanced options</summary>
        private void MenuItemOptions_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                optionsWindow.Show();
            }
            catch
            {
                optionsWindow = new OptionsWindow(this, this.markableCanvas);
                optionsWindow.Show();
            }
        }
        #endregion

        #region View Menu Callbacks

        private void View_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            int[] counts = dbData.GetImageCounts();
            //if (counts[(int) Constants.ImageQualityFilters.Ok] == counts[(int) Constants.ImageQualityFilters.Ok])
            //{
            //    this.MenuItemViewLightImages.IsEnabled = false;
            ///}
            //else 
            //{
                 this.MenuItemViewLightImages.IsEnabled = (counts[(int) Constants.ImageQualityFilters.Ok] > 0);
            //}
            this.MenuItemViewDarkImages.IsEnabled =  (counts[(int) Constants.ImageQualityFilters.Dark] > 0);
            this.MenuItemViewCorruptedImages.IsEnabled = (counts[(int)Constants.ImageQualityFilters.Corrupted] > 0);
            this.MenuItemViewMissingImages.IsEnabled = (counts[(int)Constants.ImageQualityFilters.Missing] > 0);
            this.MenuItemViewImagesMarkedForDeletion.IsEnabled = (this.dbData.GetDeletedImagesCounts() > 0);
        }

        private void MenuItemZoomIn_Click(object sender, RoutedEventArgs e)
        {
            lock (this.markableCanvas.imgToDisplay )
            {
                Point location = Mouse.GetPosition(this.markableCanvas.imgToDisplay);
                if (location.X > this.markableCanvas.imgToDisplay.ActualWidth || location.Y > this.markableCanvas.imgToDisplay.ActualHeight)
                    return; // Ignore points if mouse is off the image
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
        /// 
        private void MenuItemViewNextImage_Click(object sender, RoutedEventArgs e)
        {
            this.ViewNextImage();// Goto the next image
        }

        /// <summary> Navigate to the previous image in this image set </summary>
        private void MenuItemViewPreviousImage_Click(object sender, RoutedEventArgs e)
        {
            this.ViewPreviousImage(); // Goto the previous image

        }

        /// <summary> Cycle through the image differences </summary>
        private void MenuItemViewDifferencesCycleThrough_Click(object sender, RoutedEventArgs e)
        {
            ViewDifferencesCycleThrough();
        }

        /// <summary> View the combined image differences </summary>
        private void MenuItemViewDifferencesCombined_Click(object sender, RoutedEventArgs e)
        {
            ViewDifferencesCombined();
        }

        /// <summary> Select the appropriate filter and update the view </summary>
        private void MenuItemView_Click(object sender, RoutedEventArgs e)
        {
            MenuItem item = (MenuItem) sender;
            int filter;
            // find out which filter was selected
            if (item == this.MenuItemViewAllImages) filter = (int)Constants.ImageQualityFilters.All;
            else if (item == this.MenuItemViewLightImages) filter = (int)Constants.ImageQualityFilters.Ok;
            else if (item == this.MenuItemViewCorruptedImages) filter = (int)Constants.ImageQualityFilters.Corrupted;
            else if (item == this.MenuItemViewDarkImages) filter = (int)Constants.ImageQualityFilters.Dark;
            else if (item == this.MenuItemViewMissingImages) filter = (int)Constants.ImageQualityFilters.Missing;
            else if (item == this.MenuItemViewImagesMarkedForDeletion) filter = (int)Constants.ImageQualityFilters.MarkedForDeletion;
            else filter = (int)Constants.ImageQualityFilters.All;   // Just in case

            // Treat the checked status as a radio button i.e., toggle their states so only the clicked menu item is checked.
            bool result = SetImageFilterAndIndex(0, filter);  // Go to the first result (i.e., index 0) in the given filter set
            // if (result == true) MenuItemViewSetSelected(item);  //Check the currently selected menu item and uncheck the others in this group
        }

        // helper function to put a checkbox on the currently selected menu item i.e., to make it behave like a radiobutton menu
        private void MenuItemViewSetSelected (MenuItem checked_item)
        {
            this.MenuItemViewAllImages.IsChecked = (this.MenuItemViewAllImages == checked_item) ? true : false;
            this.MenuItemViewCorruptedImages.IsChecked = (this.MenuItemViewCorruptedImages == checked_item) ? true : false;
            this.MenuItemViewDarkImages.IsChecked = (this.MenuItemViewDarkImages == checked_item) ? true : false;
            this.MenuItemViewLightImages.IsChecked = (this.MenuItemViewLightImages == checked_item) ? true : false;
            this.MenuItemViewImagesMarkedForDeletion.IsChecked = (this.MenuItemViewImagesMarkedForDeletion == checked_item) ? true : false;
            this.MenuItemView.IsChecked = false;
        }
        // helper function to put a checkbox on the currently selected menu item i.e., to make it behave like a radiobutton menu
        private void MenuItemViewSetSelected(int filter)
        {
            this.MenuItemViewAllImages.IsChecked = (filter==(int) Constants.ImageQualityFilters.All) ? true : false;
            this.MenuItemViewCorruptedImages.IsChecked = (filter == (int)Constants.ImageQualityFilters.Corrupted) ? true : false;
            this.MenuItemViewDarkImages.IsChecked = (filter == (int)Constants.ImageQualityFilters.Dark) ? true : false;
            this.MenuItemViewLightImages.IsChecked = (filter == (int)Constants.ImageQualityFilters.Ok) ? true : false;
            this.MenuItemViewMissingImages.IsChecked = (filter == (int)Constants.ImageQualityFilters.Missing) ? true : false; ;
            this.MenuItemViewImagesMarkedForDeletion.IsChecked = (filter == (int)Constants.ImageQualityFilters.MarkedForDeletion) ? true : false; ;
        }

        /// <summary> Show a dialog box telling the user how many images were loaded, etc.</summary>
        private void MenuItemImageCounts_Click(object sender, RoutedEventArgs e)
        {
            int[] counts = dbData.GetImageCounts();
            DlgStatisticsOfImageCounts dlg = new DlgStatisticsOfImageCounts(
                counts[(int)Constants.ImageQualityFilters.Ok],
                counts[(int)Constants.ImageQualityFilters.Dark],
                counts[(int)Constants.ImageQualityFilters.Corrupted],
                counts[(int)Constants.ImageQualityFilters.Missing]);
            dlg.Owner = this;
            dlg.ShowDialog();
        }

        /// <summary> Display the dialog showing the filtered view of the current database contents </summary>
        private void MenuItemViewFilteredDatabaseContents_Click (object sender, RoutedEventArgs e)
        {
            if (null != this.dlgDataView && this.dlgDataView.IsLoaded) return; // If its already displayed, don't bother.
            dlgDataView = new DlgDataView(this.dbData);
            dlgDataView.Show();
        }
        #endregion 

        #region Help Menu Callbacks
        /// <summary> Display a help window </summary> 
        private void MenuOverview_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                overviewWindow.Show();
            }
            catch
            {
                overviewWindow = new HelpWindow();
                overviewWindow.Show();
            }
        }

        /// <summary>  Display a message describing the version, etc.</summary> 
        private void MenuOverview_About(object sender, RoutedEventArgs e)
        {
            DlgAboutTimelapse dlg = new DlgAboutTimelapse();
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
        private MyCounter FindSelectedCounter()
        {
            foreach (MyCounter counter in this.myControls.MyCountersList)
            {
                if (counter.isSelected) return counter;
            }
            return null;
        }

        public void ResetDifferenceThreshold ()
        {
            this.differenceThreshold = Constants.DEFAULT_DIFFERENCE_THRESHOLD;
        }
 
        // Say the given text
        public void Speak(string text)
        {
            if (this.state.audioFeedback)
            {
                speechSynthesizer.SpeakAsyncCancelAll();
                speechSynthesizer.SpeakAsync(text);
            }
        }
        #endregion

        #region Navigating Images
        // Display the next image
        private void ViewNextImage () 
        {
            this.state.isContentValueChangedFromOutside = true;
            this.dbData.ToDataRowNext();
            this.ViewRefresh();
            state.isContentValueChangedFromOutside = false;
        }

        // Display the previous image
        private void ViewPreviousImage()
        {
            this.state.isContentValueChangedFromOutside = true;
            this.dbData.ToDataRowPrevious();
            this.ViewRefresh();
            state.isContentValueChangedFromOutside = false;

        }
        // Refresh the view by readjusting the slider position and showing the image.
        private void ViewRefresh ()
        {
            this.sldrImageNavigatorEnableCallback(false);
            this.showImage(this.dbData.CurrentRow);
            this.sldrImageNavigatorEnableCallback(true);
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

        #region Control Management
        /// <summary>
        /// Show the Coding Controls in the main timelapse window
        /// </summary>
        private void ControlsInMainWindow()
        {
            if (null != this.controlWindow)
            {
                this.controlWindow.ChildRemove(myControls);
                this.controlWindow.Close();
                this.controlWindow = null;
            }
            else
            {
                this.controlsTray.Children.Remove(this.myControls);
                this.controlsTray.Children.Add(myControls);
                this.MenuItemControlsInSeparateWindow.IsChecked = false;
            }
        }
        /// <summary> Show the Coding Controls in a separate window </summary>
        private void ControlsInSeparateWindow()
        {
            // this.controlsTray.Children.Clear();
            this.controlsTray.Children.Remove(this.myControls);

            controlWindow = new ControlWindow(state);    // Handles to the control window and to the controls
            controlWindow.Owner = this;             // Keeps this window atop its parent no matter what
            controlWindow.Closed += controlWindow_Closing;
            controlWindow.AddControls(this.myControls);
            controlWindow.RestorePreviousSize(); 
            controlWindow.Show();
            this.MenuItemControlsInSeparateWindow.IsChecked = true;
        }

        /// <summary>
        /// Callback  invoked when the Control Window is unloaded
        /// If so, make sure the controls are in the main control window
        /// </summary>
        private void controlWindow_Closing(object sender, EventArgs e)
        {
            if (this.state.immediateExit) return;
            this.controlWindow.ChildRemove(this.myControls);
            this.controlsTray.Children.Remove(this.myControls);

            this.controlsTray.Children.Add(myControls);
            this.MenuItemControlsInSeparateWindow.IsChecked = false;
        }
        #endregion
    }

    #region MetaTag Class
    // A class representing counters for counting things. 
    public class MetaTagCounter
    {
        // A list of metatags
        // Each metatag represents the coordinates of an entity on the screen being counted
        public List<MetaTag> MetaTags = new List<MetaTag>();

        public int MetaTagCount { get { return MetaTags.Count; }}  // the count amount
        public String DataLabel { get; set; }  // The datalabel associated with this Metatag counter

        public MetaTagCounter() { }

        // Add a MetaTag to the list of MetaTags
        public void AddMetaTag(MetaTag mtag)
        {
            this.MetaTags.Add(mtag);
        }

        //Create a metatag with the given point and add it to the metatag list
        public MetaTag CreateMetaTag(System.Windows.Point point, string dataLabel)
        {
            MetaTag mtag = new MetaTag();
            
            mtag.Point = point;
            mtag.DataLabel = dataLabel;
            this.AddMetaTag(mtag);
            return mtag;
        }
    }
    #endregion

    #region Convenience classes
    // This class is used to define a tag, where a tag associates a control index and a point
    class TagFinder
    {
        public int controlIndex { get; set; }
        public TagFinder(int ctlIndex)
        {
            controlIndex = ctlIndex;
        }
    }


    // A class that tracks various states and flags.
    public class State
    {
        public bool isContentChanged { get; set; }
        public string isMouseOverCounter { get; set; }
        public bool isDateTimeOrder { get; set; }
        public bool isContentValueChangedFromOutside { get; set; }
        public int imageFilter { get; set; }
        public bool audioFeedback { get; set; }
        public bool immediateExit { get; set; }
        public Point controlWindowSize { get; set; }

        public State()
        {
            this.isContentChanged = false;
            this.isMouseOverCounter = "";
            this.isDateTimeOrder = true;
            this.isContentValueChangedFromOutside = false;
            this.imageFilter = (int) Constants.ImageQualityFilters.All;
            this.audioFeedback = false;
            this.immediateExit = false;
            this.controlWindowSize = new Point(0, 0);
        }
    }

    // A class that tracks our progress as we load the images
    public class ProgressState
    {

        public string Message { get; set; }
        public BitmapSource Bmap { get; set; }
        public ProgressState()
        {
            this.Message = "";
            this.Bmap = null;
        }
    }

    // A class that tracks our progress as we load the images
    public class ImageProperties
    {
        public string Name { get; set; }
        public string Folder { get; set; }
        public string DateMetadata { get; set; }
        public DateTime DateFileCreation { get; set; }
        public int ID { get; set; }
        public int ImageQuality { get; set; }
        public string FinalDate { get; set; }
        public string FinalTime { get; set; }
        public int DateOrder { get; set; }
        public bool UseMetadata { get; set; }
        public ImageProperties()
        {
        }
    }
    #endregion
}

