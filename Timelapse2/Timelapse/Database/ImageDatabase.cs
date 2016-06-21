using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using Timelapse.Images;
using Timelapse.Util;

namespace Timelapse.Database
{
    public class ImageDatabase : TemplateDatabase
    {
        private bool disposed;

        /// <summary>Gets the file name of the image database on disk.</summary>
        public string FileName { get; private set; }

        /// <summary>Gets the complete path to the folder containing the image database.</summary>
        public string FolderPath { get; private set; }

        public Dictionary<string, string> DataLabelFromStandardControlType { get; private set; }

        public Dictionary<string, ImageDataColumn> ImageDataColumnsByDataLabel { get; private set; }

        // contains the results of the data query
        public DataTable ImageDataTable { get; private set; }

        public ImageSetRow ImageSet { get; private set; }

        // contains the markers
        public DataTable MarkersTable { get; private set; }

        public List<string> TemplateSynchronizationIssues { get; private set; }

        private ImageDatabase(string filePath)
            : base(filePath)
        {
            // TODOSAUL: Hmm. I've never seen database creation failure. Nonetheless, while I can pop up a messagebox here, the real issue is how
            // to revert the system back to some reasonable state. I will need to look at this closely 
            // What we really need is a function that we can call that will essentially bring the system back to its
            // virgin state, that we can invoke from various conditions. 
            // Alternately, we can just exit Timelapse (a poor solution but it could suffice for now)
            this.DataLabelFromStandardControlType = new Dictionary<string, string>();
            this.disposed = false;
            this.FolderPath = Path.GetDirectoryName(filePath);
            this.FileName = Path.GetFileName(filePath);
            this.ImageDataColumnsByDataLabel = new Dictionary<string, ImageDataColumn>();
            this.TemplateSynchronizationIssues = new List<string>();
        }

        public static ImageDatabase CreateOrOpen(string filePath, TemplateDatabase templateDatabase)
        {
            // check for an existing database before instantiating the databse as SQL wrapper instantiation creates the database file
            bool populateDatabase = !File.Exists(filePath);

            ImageDatabase imageDatabase = new ImageDatabase(filePath);
            if (populateDatabase)
            {
                // initialize the database if it's newly created
                imageDatabase.OnDatabaseCreated(templateDatabase);
            }
            else
            {
                // if it's an existing database check if it needs updating to current structure and load data tables
                imageDatabase.OnExistingDatabaseOpened(templateDatabase);
            }

            // load the single row of the image set table from the database
            string imageSetQuery = "Select * From " + Constants.Database.ImageSetTable + " WHERE " + Constants.DatabaseColumn.ID + " = " + Constants.Database.ImageSetRowID.ToString();
            DataTable imageSetTable = imageDatabase.Database.GetDataTableFromSelect(imageSetQuery);
            imageDatabase.ImageSet = new ImageSetRow(imageSetTable.Rows[0]);

            // load the marker table from the database
            string markersQuery = "Select * FROM " + Constants.Database.MarkersTable;
            imageDatabase.MarkersTable = imageDatabase.Database.GetDataTableFromSelect(markersQuery);

            imageDatabase.PopulateDataLabelMaps();
            return imageDatabase;
        }

        /// <summary>Gets the number of images currently in the image table.</summary>
        public int CurrentlySelectedImageCount
        {
            get { return this.ImageDataTable.Rows.Count; }
        }

        [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1126:PrefixCallsCorrectly", Justification = "StyleCop bug.")]
        public void AddImages(List<ImageRow> imagePropertiesList, Action<ImageRow, int> onImageAdded)
        {
            // We need to get a list of which columns are counters vs notes or fixed coices, 
            // as we will shortly have to initialize them to some defaults
            List<string> counterList = new List<string>();
            List<string> notesAndFixedChoicesList = new List<string>();
            List<string> flagsList = new List<string>();
            for (int column = 0; column < this.ImageDataTable.Columns.Count; column++)
            {
                string columnName = this.ImageDataTable.Columns[column].ColumnName;
                if (columnName == Constants.DatabaseColumn.ID)
                {
                    // skip the ID column as it's not associated with a data label and doesn't need to be set as it's autoincrement
                    continue;
                }

                string controlType = this.ImageDataColumnsByDataLabel[columnName].ControlType;
                if (controlType.Equals(Constants.Control.Counter))
                {
                    counterList.Add(columnName);
                }
                else if (controlType.Equals(Constants.Control.Note) || controlType.Equals(Constants.Control.FixedChoice))
                {
                    notesAndFixedChoicesList.Add(columnName);
                }
                else if (controlType.Equals(Constants.Control.Flag))
                {
                    flagsList.Add(columnName);
                }
            }

            // Create a dataline from each of the image properties, add it to a list of data lines,
            // then do a multiple insert of the list of datalines to the database 
            for (int image = 0; image < imagePropertiesList.Count; image += Constants.Database.RowsPerInsert)
            {
                // Create a dataline from the image properties, add it to a list of data lines,
                // then do a multiple insert of the list of datalines to the database 
                List<List<ColumnTuple>> imageTableRows = new List<List<ColumnTuple>>();
                List<List<ColumnTuple>> markerTableRows = new List<List<ColumnTuple>>();
                for (int insertIndex = image; (insertIndex < (image + Constants.Database.RowsPerInsert)) && (insertIndex < imagePropertiesList.Count); insertIndex++)
                {
                    // THE PROBLEM IS THAT WE ARE NOT ADDING THESE VALUES IN THE SAME ORDER AS THE TABLE
                    // THEY MUST BE IN THE SAME ORDER IE, AS IN THE COLUMNS. This case statement just fills up 
                    // the dataline in the same order as the template table.
                    // It assumes that the key is always the first column
                    List<ColumnTuple> imageRow = new List<ColumnTuple>();
                    List<ColumnTuple> markerRow = new List<ColumnTuple>();

                    for (int column = 0; column < this.ImageDataTable.Columns.Count; column++)
                    {
                        // Fill up each column in order
                        string columnName = this.ImageDataTable.Columns[column].ColumnName;
                        if (columnName == Constants.DatabaseColumn.ID)
                        {
                            // don't specify an ID in the insert statement as it's an autoincrement primary key
                            continue;
                        }
                        string controlType = this.ImageDataColumnsByDataLabel[columnName].ControlType;

                        ImageRow imageProperties = imagePropertiesList[insertIndex];
                        switch (controlType)
                        {
                            case Constants.DatabaseColumn.File: // Add The File name
                                string dataLabel = this.DataLabelFromStandardControlType[Constants.DatabaseColumn.File];
                                imageRow.Add(new ColumnTuple(dataLabel, imageProperties.FileName));
                                break;
                            case Constants.DatabaseColumn.RelativePath: // Add the relative path name
                                dataLabel = this.DataLabelFromStandardControlType[Constants.DatabaseColumn.RelativePath];
                                imageRow.Add(new ColumnTuple(dataLabel, imageProperties.RelativePath));
                                break;
                            case Constants.DatabaseColumn.Folder: // Add The Folder name
                                dataLabel = this.DataLabelFromStandardControlType[Constants.DatabaseColumn.Folder];
                                imageRow.Add(new ColumnTuple(dataLabel, imageProperties.InitialRootFolderName));
                                break;
                            case Constants.DatabaseColumn.Date:
                                // Add the date
                                dataLabel = this.DataLabelFromStandardControlType[Constants.DatabaseColumn.Date];
                                imageRow.Add(new ColumnTuple(dataLabel, imageProperties.Date));
                                break;
                            case Constants.DatabaseColumn.Time:
                                // Add the time
                                dataLabel = this.DataLabelFromStandardControlType[Constants.DatabaseColumn.Time];
                                imageRow.Add(new ColumnTuple(dataLabel, imageProperties.Time));
                                break;
                            case Constants.DatabaseColumn.ImageQuality: // Add the Image Quality
                                dataLabel = this.DataLabelFromStandardControlType[Constants.DatabaseColumn.ImageQuality];
                                string imageQuality = Constants.ImageQuality.Ok;
                                if (imageProperties.ImageQuality == ImageQualityFilter.Dark)
                                {
                                    imageQuality = Constants.ImageQuality.Dark;
                                }
                                else if (imageProperties.ImageQuality == ImageQualityFilter.Corrupted)
                                {
                                    imageQuality = Constants.ImageQuality.Corrupted;
                                }
                                imageRow.Add(new ColumnTuple(dataLabel, imageQuality));
                                break;
                            case Constants.Control.DeleteFlag: // Add the Delete flag
                                dataLabel = this.DataLabelFromStandardControlType[Constants.Control.DeleteFlag];
                                imageRow.Add(new ColumnTuple(dataLabel, this.GetControlDefaultValue(dataLabel))); // Default as specified in the template file, which should be "false"
                                break;
                            case Constants.Control.Note:        // Find and then Add the Note or Fixed Choice
                            case Constants.Control.FixedChoice:
                                // Now initialize notes, counters, and fixed choices to the defaults
                                foreach (string controlName in notesAndFixedChoicesList)
                                {
                                    if (columnName.Equals(controlName))
                                    {
                                        imageRow.Add(new ColumnTuple(controlName, this.GetControlDefaultValue(controlName))); // Default as specified in the template file
                                    }
                                }
                                break;
                            case Constants.Control.Flag:
                                // Now initialize flags to the defaults
                                foreach (string controlName in flagsList)
                                {
                                    if (columnName.Equals(controlName))
                                    {
                                        imageRow.Add(new ColumnTuple(controlName, this.GetControlDefaultValue(controlName))); // Default as specified in the template file
                                    }
                                }
                                break;
                            case Constants.Control.Counter:
                                foreach (string controlName in counterList)
                                {
                                    if (columnName.Equals(controlName))
                                    {
                                        imageRow.Add(new ColumnTuple(controlName, this.GetControlDefaultValue(controlName))); // Default as specified in the template file
                                        markerRow.Add(new ColumnTuple(controlName, String.Empty));        // TODOSAUL: ASSUMES THAT MARKER LIST IS IN SAME ORDER AS COUNTERS. THIS MAY NOT BE CORRECT ONCE WE SWITCH ROWS, SO SHOULD DO THIS SEPARATELY
                                    }
                                }
                                break;

                            default:
                                Debug.Assert(false, String.Format("Unhandled control type '{0}'.", controlType));
                                break;
                        }
                    }
                    imageTableRows.Add(imageRow);
                    if (markerRow.Count > 0)
                    {
                        markerTableRows.Add(markerRow);
                    }
                }

                this.InsertMultipleRows(Constants.Database.ImageDataTable, imageTableRows);
                this.InsertMultipleRows(Constants.Database.MarkersTable, markerTableRows);

                if (onImageAdded != null)
                {
                    int lastImageInserted = Math.Min(imagePropertiesList.Count - 1, image + Constants.Database.RowsPerInsert);
                    onImageAdded.Invoke(imagePropertiesList[lastImageInserted], lastImageInserted);
                }
            }
            // Load the marker table from the database - Doing so here will make sure that there is one row for each image.
            string command = "Select * FROM " + Constants.Database.MarkersTable;
            this.MarkersTable = this.Database.GetDataTableFromSelect(command);
        }

        public void AppendToImageSetLog(StringBuilder logEntry)
        {
            this.ImageSet.Log += logEntry;
            this.SyncImageSetToDatabase();
        }

        /// <summary>
        /// Make an empty Data Table based on the information in the Template Table.
        /// Assumes that the database has already been opened and that the Template Table is loaded, where the DataLabel always has a valid value.
        /// Then create both the ImageSet table and the Markers table
        /// </summary>
        protected override void OnDatabaseCreated(TemplateDatabase templateDatabase)
        {
            // copy the template's TemplateTable
            base.OnDatabaseCreated(templateDatabase);

            // Create the DataTable from the template
            // First, define the creation string based on the contents of the template. 
            List<ColumnTuple> columnDefinitions = new List<ColumnTuple>();
            columnDefinitions.Add(new ColumnTuple(Constants.DatabaseColumn.ID, Constants.Database.CreationStringPrimaryKey));  // It begins with the ID integer primary key
            foreach (DataRow row in this.TemplateTable.Rows)
            {
                columnDefinitions.Add(this.CreateImageDataColumnDefinition(new ControlRow(row)));
            }
            this.Database.CreateTable(Constants.Database.ImageDataTable, columnDefinitions);

            // initialize ImageDataTable
            // this is necessary as images can't be added unless ImageDataTable.Columns is available
            // can't use TryGetImagesAll() here as that function's contract is not to update ImageDataTable if the select against the underlying database table 
            // finds no rows, which is the case for a database being created
            this.ImageDataTable = this.GetAllImages();

            // Create the ImageSetTable and initialize a single row in it
            columnDefinitions.Clear();
            columnDefinitions.Add(new ColumnTuple(Constants.DatabaseColumn.ID, Constants.Database.CreationStringPrimaryKey));  // It begins with the ID integer primary key
            columnDefinitions.Add(new ColumnTuple(Constants.DatabaseColumn.Log, "TEXT DEFAULT 'Add text here.'"));
            columnDefinitions.Add(new ColumnTuple(Constants.DatabaseColumn.Magnifier, " TEXT DEFAULT 'true'"));
            columnDefinitions.Add(new ColumnTuple(Constants.DatabaseColumn.Row, "TEXT DEFAULT '0'"));
            int allImages = (int)ImageQualityFilter.All;
            columnDefinitions.Add(new ColumnTuple(Constants.DatabaseColumn.Filter, "TEXT DEFAULT '" + allImages.ToString() + "'"));
            // TODOSAUL: columnDefinitions.Add(new ColumnTuple(Constants.DatabaseColumn.WhiteSpaceTrimmed, Constants.Sql.Text));
            this.Database.CreateTable(Constants.Database.ImageSetTable, columnDefinitions);

            List<ColumnTuple> columnsToUpdate = new List<ColumnTuple>(); // Populate the data for the image set with defaults
            columnsToUpdate.Add(new ColumnTuple(Constants.DatabaseColumn.Log, Constants.Database.ImageSetDefaultLog));
            columnsToUpdate.Add(new ColumnTuple(Constants.DatabaseColumn.Magnifier, Constants.Boolean.True));
            columnsToUpdate.Add(new ColumnTuple(Constants.DatabaseColumn.Row, "0"));
            columnsToUpdate.Add(new ColumnTuple(Constants.DatabaseColumn.Filter, allImages.ToString()));
            // TODOSAUL: columnsToUpdate.Add(new ColumnTuple(Constants.DatabaseColumn.WhiteSpaceTrimmed, Constants.Boolean.True));
            List<List<ColumnTuple>> insertionStatements = new List<List<ColumnTuple>>();
            insertionStatements.Add(columnsToUpdate);
            this.Database.Insert(Constants.Database.ImageSetTable, insertionStatements);

            // Create the MarkersTable and initialize it from the template table
            columnDefinitions.Clear();
            columnDefinitions.Add(new ColumnTuple(Constants.DatabaseColumn.ID, Constants.Database.CreationStringPrimaryKey));  // t begins with the ID integer primary key
            string type = String.Empty;
            foreach (DataRow row in this.TemplateTable.Rows)
            {
                ControlRow control = new ControlRow(row);
                if (control.Type.Equals(Constants.Control.Counter))
                {
                    columnDefinitions.Add(new ColumnTuple(control.DataLabel, "TEXT Default ''"));
                }
            }
            this.Database.CreateTable(Constants.Database.MarkersTable, columnDefinitions);
        }

        protected override void OnExistingDatabaseOpened(TemplateDatabase templateDatabase)
        {
            // perform TemplateTable initializations and migrations, then check for synchronization issues
            base.OnExistingDatabaseOpened(templateDatabase);

            List<string> templateDataLabels = templateDatabase.GetDataLabelsExceptID();
            List<string> dataLabels = this.GetDataLabelsExceptID();
            List<string> dataLabelsInTemplateButNotImageDatabase = templateDataLabels.Except(dataLabels).ToList();
            foreach (string dataLabel in dataLabelsInTemplateButNotImageDatabase)
            {
                this.TemplateSynchronizationIssues.Add("- A field with the DataLabel '" + dataLabel + "' was found in the template, but nothing matches that in the image data file." + Environment.NewLine);
            }
            List<string> dataLabelsInImageButNotTemplateDatabase = dataLabels.Except(templateDataLabels).ToList();
            foreach (string dataLabel in dataLabelsInImageButNotTemplateDatabase)
            {
                this.TemplateSynchronizationIssues.Add("- A field with the DataLabel '" + dataLabel + "' was found in the image data file, but nothing matches that in the template." + Environment.NewLine);
            }

            if (this.TemplateSynchronizationIssues.Count == 0)
            {
                foreach (string dataLabel in dataLabels)
                {
                    ControlRow imageDatabaseControl = this.GetControlFromTemplateTable(dataLabel);
                    ControlRow templateControl = templateDatabase.GetControlFromTemplateTable(dataLabel);

                    if (imageDatabaseControl.Type != templateControl.Type)
                    {
                        this.TemplateSynchronizationIssues.Add(String.Format("- The field with DataLabel '{0}' is of type '{1}' in the image data file but of type '{2}' in the template.{3}", dataLabel, imageDatabaseControl.Type, templateControl.Type, Environment.NewLine));
                    }

                    List<string> imageDatabaseList = Utilities.ConvertBarsToList(imageDatabaseControl.List);
                    List<string> templateList = Utilities.ConvertBarsToList(templateControl.List);
                    List<string> choiceValuesRemovedInTemplate = imageDatabaseList.Except(templateList).ToList();
                    foreach (string removedValue in choiceValuesRemovedInTemplate)
                    {
                        this.TemplateSynchronizationIssues.Add(String.Format("- The choice with DataLabel '{0}' allows the value of '{1}' in the image data file but not in the template.{2}", dataLabel, removedValue, Environment.NewLine));
                    }
                }
            }

            // if there are no synchronization difficulties synchronize the image database's TemplateTable with the template's TemplateTable
            if (this.TemplateSynchronizationIssues.Count == 0)
            {
                foreach (string dataLabel in dataLabels)
                {
                    ControlRow imageDatabaseControl = this.GetControlFromTemplateTable(dataLabel);
                    ControlRow templateControl = templateDatabase.GetControlFromTemplateTable(dataLabel);

                    imageDatabaseControl.SpreadsheetOrder = templateControl.SpreadsheetOrder;
                    imageDatabaseControl.ControlOrder = templateControl.ControlOrder;
                    imageDatabaseControl.DefaultValue = templateControl.DefaultValue;
                    imageDatabaseControl.Label = templateControl.Label;
                    imageDatabaseControl.List = templateControl.List;
                    imageDatabaseControl.Tooltip = templateControl.Tooltip;
                    imageDatabaseControl.TextBoxWidth = templateControl.TextBoxWidth;
                    imageDatabaseControl.Copyable = templateControl.Copyable;
                    imageDatabaseControl.Visible = templateControl.Visible;
                    this.SyncControlToDatabase(imageDatabaseControl);
                }
            }

            // perform DataTable migrations
            // add RelativePath column if it's not present in the image data table at postion '2'
            this.ImageDataTable = this.GetAllImages();
            if (this.ImageDataTable.Columns.Contains(Constants.DatabaseColumn.RelativePath) == false)
            {
                long id = this.GetControlIDFromTemplateTable(Constants.DatabaseColumn.RelativePath);
                ControlRow control = new ControlRow(this.TemplateTable.Rows.Find(id));
                ColumnTuple columnDefinition = this.CreateImageDataColumnDefinition(control);
                this.Database.AddColumnToTable(Constants.Database.ImageDataTable, Constants.Database.RelativePathPosition, columnDefinition);
            }

            // perform ImageSetTable migrations
            // Make sure that all the string data in the datatable has white space trimmed from its beginning and end
            // This is needed as the custom filter doesn't work well in testing comparisons if there is leading or trailing white space in it
            // Newer versions of Timelapse will trim the data as it is entered, but older versions did not, so this is to make it backwards-compatable.
            // The WhiteSpaceExists column in the ImageSetTable did not exist before this version, so we add it to the table. If it exists, then 
            // we know the data has been trimmed and we don't have to do it again as the newer versions take care of trimmingon the fly.
            bool whiteSpaceColumnExists = this.Database.IsColumnInTable(Constants.Database.ImageSetTable, Constants.DatabaseColumn.WhiteSpaceTrimmed);
            if (!whiteSpaceColumnExists)
            {
                // create the whitespace column
                this.Database.AddColumnToEndOfTable(Constants.Database.ImageSetTable, new ColumnTuple(Constants.DatabaseColumn.WhiteSpaceTrimmed, Constants.Sql.Text));

                // trim the white space from all the data
                this.Database.TrimWhitespace(Constants.Database.ImageDataTable, dataLabels);
                this.UpdateImageSet(Constants.DatabaseColumn.WhiteSpaceTrimmed, Constants.Boolean.True);
            }
        }

        public ImageRow FindImageByID(long id)
        {
            return new ImageRow(this.ImageDataTable.Rows.Find(id));
        }

        /// <summary>
        /// Create lookup tables that allow us to retrieve a key from a type and vice versa
        /// </summary>
        private void PopulateDataLabelMaps()
        {
            foreach (DataRow row in this.TemplateTable.Rows)
            {
                ImageDataColumn column = ImageDataColumn.Create(new ControlRow(row));
                this.ImageDataColumnsByDataLabel.Add(column.DataLabel, column);

                // don't type map user defined controls as if there are multiple ones the key would not be unique
                if (Constants.Control.StandardTypes.Contains(column.ControlType))
                {
                    this.DataLabelFromStandardControlType.Add(column.ControlType, column.DataLabel);
                }
            }
        }

        public void RenameFile(string newFileName)
        {
            if (File.Exists(Path.Combine(this.FolderPath, this.FileName)))
            {
                File.Move(Path.Combine(this.FolderPath, this.FileName),
                          Path.Combine(this.FolderPath, newFileName));  // Change the file name to the new file name
                this.FileName = newFileName; // Store the file name
                this.Database = new SQLiteWrapper(newFileName);          // Recreate the database connecction
            }
        }

        /// <summary> 
        /// Populate the image table so that it matches all the entries in its associated database table.
        /// Then set the currentID and currentRow to the the first record in the returned set
        /// </summary>
        public bool TryGetImages(ImageQualityFilter quality)
        {
            string where;
            switch (quality)
            {
                case ImageQualityFilter.All:
                    where = String.Empty;
                    break;
                case ImageQualityFilter.Corrupted:
                case ImageQualityFilter.Dark:
                case ImageQualityFilter.Missing:
                case ImageQualityFilter.Ok:
                    where = this.DataLabelFromStandardControlType[Constants.DatabaseColumn.ImageQuality] + "=\"" + quality + "\"";
                    break;
                case ImageQualityFilter.MarkedForDeletion:
                    where = this.DataLabelFromStandardControlType[Constants.Control.DeleteFlag] + "=\"true\"";
                    break;
                case ImageQualityFilter.Custom:
                default:
                    throw new NotSupportedException(String.Format("Unhandled quality filter {0}.", quality));
            }
            return this.TryGetImages(where);
        }

        public DataTable GetImagesMarkedForDeletion()
        {
            string where = this.DataLabelFromStandardControlType[Constants.Control.DeleteFlag] + "=\"true\""; // = value
            return this.GetImages(where);
        }

        /// <summary>
        /// Return a data table containing a single image row, where that row is identified by the image's ID
        /// </summary>
        public DataTable GetImageByID(long id)
        {
            string where = Constants.DatabaseColumn.ID + "=\"" + id + "\""; // = value
            return this.GetImages(where);
        }

        // Custom filter - for a singe where Col=Value
        public bool TryGetImagesCustom(string dataLabel, string comparison, string value)
        {
            string where = dataLabel + comparison + "\"" + value + "\"";
            return this.TryGetImages(where);
        }

        /// <summary>
        /// Get the row matching the specified image or create a new image.  The caller is responsible to add newly created images the database and data table.
        /// </summary>
        /// <returns>true if the image is already in the database</returns>
        public bool GetOrCreateImage(FileInfo imageFile, out ImageRow imageProperties)
        {
            string initialRootFolderName = Path.GetFileName(this.FolderPath);
            // GetRelativePath() includes the image's file name; remove that from the relative path as it's stored separately
            // GetDirectoryName() returns String.Empty if there's no relative path; the SQL layer treats this inconsistently, resulting in 
            // DataRows returning with RelativePath = String.Empty even if null is passed despite setting String.Empty as a column default
            // resulting in RelativePath = null.  As a result, String.IsNullOrEmpty() is the appropriate test for lack of a RelativePath.
            string relativePath = NativeMethods.GetRelativePath(this.FolderPath, imageFile.FullName);
            relativePath = Path.GetDirectoryName(relativePath);

            ColumnTuplesWithWhere imageQuery = new ColumnTuplesWithWhere();
            imageQuery.SetWhere(initialRootFolderName, relativePath, imageFile.Name);
            DataTable images = this.GetImages(imageQuery.Where);

            if (images != null && images.Rows.Count == 1)
            {
                imageProperties = new ImageRow(images.Rows[0]);
                return true;
            }
            else
            {
                imageProperties = new ImageRow(this.ImageDataTable.NewRow());
                imageProperties.InitialRootFolderName = initialRootFolderName;
                imageProperties.RelativePath = relativePath;
                imageProperties.FileName = imageFile.Name;

                // populate new image's default date and time
                // Typically the creation time is the time a file was created in the local file system and the last write time when it was
                // last modified ever in any file system.  So, for example, copying an image from a camera's SD card to a computer results
                // in the image file on the computer having a write time which is before its creation time.  Check both and take the lesser 
                // of the two to provide a best effort default.  In most cases it's desirable to see if a more accurate time can be obtained
                // from the image's EXIF metadata.
                DateTime earliestTime = imageFile.CreationTime < imageFile.LastWriteTime ? imageFile.CreationTime : imageFile.LastWriteTime;
                imageProperties.SetDateAndTime(earliestTime);
                return false;
            }
        }

        public bool TryGetImages(string where)
        {
            DataTable imageTableWithNewSelect = this.GetImages(where);
            if (imageTableWithNewSelect.Rows.Count == 0)
            {
                return false;
            }

            this.ImageDataTable = imageTableWithNewSelect;
            return true;
        }

        private DataTable GetImages(string where)
        {
            string query = "Select * FROM " + Constants.Database.ImageDataTable;
            if (!String.IsNullOrEmpty(where))
            {
                query += " WHERE " + where;
            }

            DataTable tempTable = this.Database.GetDataTableFromSelect(query);
            return tempTable;
        }

        public DataTable GetAllImages()
        {
            return this.GetImages(null);
        }

        public Dictionary<ImageQualityFilter, int> GetImageCounts()
        {
            Dictionary<ImageQualityFilter, int> counts = new Dictionary<ImageQualityFilter, int>();
            counts[ImageQualityFilter.Dark] = this.GetImageCountByQuality(Constants.ImageQuality.Dark);
            counts[ImageQualityFilter.Corrupted] = this.GetImageCountByQuality(Constants.ImageQuality.Corrupted);
            counts[ImageQualityFilter.Missing] = this.GetImageCountByQuality(Constants.ImageQuality.Missing);
            counts[ImageQualityFilter.Ok] = this.GetImageCountByQuality(Constants.ImageQuality.Ok);
            return counts;
        }

        public int GetDeletedImageCount()
        {
            string query = "Select Count(*) FROM " + Constants.Database.ImageDataTable + " Where " + this.DataLabelFromStandardControlType[Constants.Control.DeleteFlag] + " = \"true\"";
            return this.Database.GetCountFromSelect(query);
        }

        public ImageRow GetImageByRow(int row)
        {
            return new ImageRow(this.ImageDataTable.Rows[row]);
        }

        // This first form just returns the count of all images with no filters applied
        public int GetImageCount()
        {
            string query = "Select Count(*) FROM " + Constants.Database.ImageDataTable;
            return this.Database.GetCountFromSelect(query);
        }

        private int GetImageCountByQuality(string imageQualityFilter)
        {
            string query = "Select Count(*) FROM " + Constants.Database.ImageDataTable + " Where " + this.DataLabelFromStandardControlType[Constants.DatabaseColumn.ImageQuality] + " = \"" + imageQualityFilter + "\"";
            return this.Database.GetCountFromSelect(query);
        }

        public int GetImageCountWithCustomFilter(string where)
        {
            string query = "Select Count(*) FROM " + Constants.Database.ImageDataTable + " Where " + where;
            return this.Database.GetCountFromSelect(query);
        }

        // Insert one or more rows into a table
        private void InsertMultipleRows(string table, List<List<ColumnTuple>> insertionStatements)
        {
            this.Database.Insert(table, insertionStatements);
        }

        /// <summary>
        /// Update a column value (identified by its key) in an existing row (identified by its ID) 
        /// By default, if the table parameter is not included, we use the TABLEDATA table
        /// </summary>
        public void UpdateImage(long id, string dataLabel, string value)
        {
            this.UpdateRow(id, dataLabel, value, Constants.Database.ImageDataTable);
        }

        public void UpdateRow(long id, string dataLabel, string value, string table)
        {
            // update the row in the database
            ColumnTuplesWithWhere columnToUpdate = new ColumnTuplesWithWhere();
            columnToUpdate.Columns.Add(new ColumnTuple(dataLabel, value)); // Populate the data 
            columnToUpdate.SetWhere(id);
            this.Database.Update(table, columnToUpdate);

            // update the copy of the row in the loaded data table
            DataTable dataTable;
            switch (table)
            {
                case Constants.Database.ImageDataTable:
                    dataTable = this.ImageDataTable;
                    break;
                case Constants.Database.ImageSetTable:
                    // image set operations go directly to database; no data table is in use
                    return;
                case Constants.Database.MarkersTable:
                    dataTable = this.MarkersTable;
                    break;
                case Constants.Database.TemplateTable:
                    dataTable = this.TemplateTable;
                    break;
                default:
                    throw new NotSupportedException(String.Format("Unhandled table {0}.", table));
            }

            // nothing to do if the data table hasn't been loaded
            if (dataTable == null)
            {
                return;
            }

            // update the table
            DataRow[] foundRows = dataTable.Select(Constants.DatabaseColumn.ID + " = " + id);
            if (foundRows.Length == 1)
            {
                int index = dataTable.Rows.IndexOf(foundRows[0]);
                dataTable.Rows[index][dataLabel] = value;
            }
            else
            {
                Debug.Assert(false, String.Format("Found {0} rows with ID {1}.", foundRows.Length, id));
            }
        }

        // Update all rows in the filtered view only with the given key/value pair
        public void UpdateAllImagesInFilteredView(string dataLabel, string value)
        {
            List<ColumnTuplesWithWhere> updateQuery = new List<ColumnTuplesWithWhere>();
            for (int image = 0; image < this.CurrentlySelectedImageCount; image++)
            {
                this.ImageDataTable.Rows[image][dataLabel] = value;
                List<ColumnTuple> columnToUpdate = new List<ColumnTuple>() { new ColumnTuple(dataLabel, value) };
                long id = this.ImageDataTable.Rows[image].GetID();
                updateQuery.Add(new ColumnTuplesWithWhere(columnToUpdate, id));
            }

            this.Database.Update(Constants.Database.ImageDataTable, updateQuery);
        }

        // Given a list of column/value pairs (the string,object) and the FILE name indicating a row, update it
        public void UpdateImages(List<ColumnTuplesWithWhere> imagesToUpdate)
        {
            this.Database.Update(Constants.Database.ImageDataTable, imagesToUpdate);
        }

        public void UpdateImages(string dataLabel, string value, int fromRow, int toRow)
        {
            List<ColumnTuplesWithWhere> imagesToUpdate = new List<ColumnTuplesWithWhere>();
            int fromIndex = fromRow + 1; // rows start at 0, while indexes start at 1
            int toIndex = toRow + 1;
            for (int index = fromRow; index <= toRow; index++)
            {
                // update data table
                // TODOSAUL: is there an off by one error here as .Rows is accessed with a one based count?
                // Um, I can't recall. I don't think it is an error as a vaguely recall somethings were indexed by 1, and others by 0.
                // But it should be checked.
                this.ImageDataTable.Rows[index][dataLabel] = value;
                List<ColumnTuple> columnToUpdate = new List<ColumnTuple>() { new ColumnTuple(dataLabel, value) };
                long id = this.ImageDataTable.Rows[index].GetID();

                // update database
                ColumnTuplesWithWhere imageUpdate = new ColumnTuplesWithWhere(columnToUpdate, id);
                imagesToUpdate.Add(imageUpdate);
            }

            this.Database.Update(Constants.Database.ImageDataTable, imagesToUpdate);
        }

        // Given a time difference in ticks, update all the date / time field in the database
        // Note that it does NOT update the dataTable - this has to be done outside of this routine by regenerating the datatables with whatever filter is being used..
        // TODOSAUL: modify this to include argments showing the current filtered view and row number, perhaps, so we could restore the datatable and the view?? 
        // But that would add complications if there are unanticipated filtered views.
        // Another option is to go through whatever the current datatable is and just update those fields. 
        public void AdjustAllImageTimes(TimeSpan adjustment, int from, int to)
        {
            // We create a temporary table. We do this just in case we are currently on a filtered view
            DataTable tempTable = this.GetAllImages();

            // We now have an unfiltered temporary data table
            // Get the original value of each, and update each date by the corrected amount if possible
            List<ImageRow> imagePropertiesList = new List<ImageRow>();
            for (int row = from; row < to; row++)
            {
                ImageRow imageProperties = new ImageRow(tempTable.Rows[row]);
                DateTime date = imageProperties.GetDateTime();
                // adjust the date
                date += adjustment;
                imageProperties.SetDateAndTime(date);
                imagePropertiesList.Add(imageProperties);
            }

            // Now update the actual database with the new date/time values stored in the temporary table
            List<ColumnTuplesWithWhere> imagesToUpdate = new List<ColumnTuplesWithWhere>();
            foreach (ImageRow imageProperties in imagePropertiesList)
            {
                List<ColumnTuple> columnsToUpdate = new List<ColumnTuple>();                       // Update the date and time
                columnsToUpdate.Add(new ColumnTuple(Constants.DatabaseColumn.Date, imageProperties.Date));
                columnsToUpdate.Add(new ColumnTuple(Constants.DatabaseColumn.Time, imageProperties.Time));
                imagesToUpdate.Add(new ColumnTuplesWithWhere(columnsToUpdate, imageProperties.ID));
            }

            this.Database.Update(Constants.Database.ImageDataTable, imagesToUpdate);
        }

        // Update all the date fields by swapping the days and months.
        // This should ONLY be called if such swapping across all dates (excepting corrupt ones) is possible
        // as otherwise it will only swap those dates it can
        // It also assumes that the data table is showing All images
        public void ExchangeDayAndMonthInImageDate()
        {
            this.ExchangeDayAndMonthInImageDate(0, this.CurrentlySelectedImageCount);
        }

        // Update all the date fields between the start and end index by swapping the days and months.
        // It  assumes that the data table is showing All images
        public void ExchangeDayAndMonthInImageDate(int startRow, int endRow)
        {
            if (this.CurrentlySelectedImageCount == 0 || startRow >= this.CurrentlySelectedImageCount || endRow > this.CurrentlySelectedImageCount)
            {
                return;
            }

            // Get the original date value of each. If we can swap the date order, do so. 
            List<ColumnTuplesWithWhere> updateQuery = new List<ColumnTuplesWithWhere>();
            for (int row = startRow; row <= endRow; row++)
            {
                // I originally didn't swap the date for corrupted images, but don't see why I shouldn't 
                // But in case we decide not to do so, I've left the code here
                // if (this.IsImageCorrupt(row))
                // {
                //    continue;  // skip over corrupted images
                // }

                DateTime reversedDate;
                try
                {
                    // If we fail on any of these, continue on to the next date
                    string dateAsString = this.ImageDataTable.Rows[row].GetStringField(Constants.DatabaseColumn.Date);
                    DateTime date = DateTime.Parse(dateAsString);
                    reversedDate = new DateTime(date.Year, date.Day, date.Month); // we have swapped the day with the month
                }
                catch
                {
                    continue;
                }

                // Now update the actual database with the new date/time values stored in the temporary table
                List<ColumnTuple> columnToUpdate = new List<ColumnTuple>();               // Update the date 
                columnToUpdate.Add(new ColumnTuple(Constants.DatabaseColumn.Date, DateTimeHandler.StandardDateString(reversedDate)));
                long id = this.ImageDataTable.Rows[row].GetID();
                updateQuery.Add(new ColumnTuplesWithWhere(columnToUpdate, id));
            }

            this.Database.Update(Constants.Database.ImageDataTable, updateQuery);
        }

        // Delete the data associated with the image identified by the ID
        public void DeleteImage(long id)
        {
            List<long> idList = new List<long>(); // Create a list containing one ID, and
            idList.Add(id);
            this.DeleteImages(idList);             // invoke the version of DeleteImage that operates over that list
        }

        // Delete the data (including markers associated with the images identified by the list of IDs.
        public void DeleteImages(List<long> idList)
        {
            List<string> idClauses = new List<string>();
            foreach (long id in idList)
            {
                idClauses.Add(Constants.DatabaseColumn.ID + " = " + id.ToString());
            }
            if (idClauses.Count > 0)
            {
                // Delete the data and markers associated with that image
                this.Database.Delete(Constants.Database.ImageDataTable, idClauses);
                this.Database.Delete(Constants.Database.MarkersTable, idClauses);
            }
        }

        // Given a row index, return the ID
        public long GetImageID(int rowIndex)
        {
            if (!this.IsImageRowInRange(rowIndex))
            {
                return -1;
            }
            long id = this.ImageDataTable.Rows[rowIndex].GetID();
            return id;
        }

        public string GetImageValue(int rowIndex, string dataLabel)
        {
            if (this.IsImageRowInRange(rowIndex))
            {
                return this.ImageDataTable.Rows[rowIndex].GetStringField(dataLabel);
            }
            else
            {
                return String.Empty;
            }
        }

        /// <summary>A convenience routine for checking to see if the image in the given row is displayable (i.e., not corrupted or missing)</summary>
        public bool IsImageDisplayable(int rowIndex)
        {
            string result = this.GetImageValue(rowIndex, this.DataLabelFromStandardControlType[Constants.DatabaseColumn.ImageQuality]);
            if (String.IsNullOrEmpty(result) || result.Equals(Constants.ImageQuality.Corrupted) || result.Equals(Constants.ImageQuality.Missing))
            {
                return false;
            }
            return true;
        }

        public bool IsImageRowInRange(int imageRowIndex)
        {
            return (imageRowIndex >= 0) && (imageRowIndex < this.CurrentlySelectedImageCount) ? true : false;
        }

        /// <summary>A convenience routine for checking to see if the image in the given row is corrupted</summary>
        public bool IsImageCorrupt(int rowIndex)
        {
            string result = this.GetImageValue(rowIndex, this.DataLabelFromStandardControlType[Constants.DatabaseColumn.ImageQuality]);
            return result.Equals(Constants.ImageQuality.Corrupted) ? true : false;
        }

        // Find the next displayable image after the provided row in the current image set
        // If there is no next displayable image, then find the first previous image before the provided row that is dispay
        public int FindFirstDisplayableImage(int firstRowInSearch)
        {
            for (int row = firstRowInSearch; row < this.CurrentlySelectedImageCount; row++)
            {
                if (this.IsImageDisplayable(row))
                {
                    return row;
                }
            }
            for (int row = firstRowInSearch - 1; row >= 0; row--)
            {
                if (this.IsImageDisplayable(row))
                {
                    return row;
                }
            }
            return -1;
        }

        // Find the image whose ID is closest to the provided ID  in the current image set
        // If the ID does not exist, then return the image row whose ID is just greater than the provided one. 
        // However, if there is no greater ID (i.e., we are at the end) return the last row. 
        public int FindClosestImage(long id)
        {
            for (int row = 0; row < this.CurrentlySelectedImageCount; row++)
            {
                if (this.GetImageID(row) >= id)
                {
                    return row;
                }
            }
            return this.CurrentlySelectedImageCount - 1;
        }

        public string GetControlDefaultValue(string dataLabel)
        {
            long id = this.GetControlIDFromTemplateTable(dataLabel);
            ControlRow control = new ControlRow(this.TemplateTable.Rows.Find(id));
            return control.DefaultValue;
        }

        /// <summary>
        /// Get the metatag counter list associated with all counters representing the current row
        /// It will have a MetaTagCounter for each control, even if there may be no metatags in it
        /// </summary>
        /// <returns>list of counters</returns>
        public List<MetaTagCounter> GetMetaTagCounters(long imageID)
        {
            List<MetaTagCounter> metaTagCounters = new List<MetaTagCounter>();

            // Test to see if we actually have a valid result
            if (this.MarkersTable.Rows.Count == 0)
            {
                return metaTagCounters;    // This should not really happen, but just in case
            }
            if (this.MarkersTable.Columns.Count == 0)
            {
                return metaTagCounters; // Should also not happen as this wouldn't be called unless we have at least one counter control
            }

            // Get the current row number of the id in the marker table
            MarkerRow marker = this.FindMarker(imageID);
            if (marker == null)
            {
                return metaTagCounters;
            }

            // Iterate through the columns, where we create a new MetaTagCounter for each control and add it to the MetaTagCounter list
            foreach (string dataLabel in marker.DataLabels)
            {
                // Create a new MetaTagCounter representing this control's meta tag,
                MetaTagCounter metaTagCounter = new MetaTagCounter();
                metaTagCounter.DataLabel = dataLabel;

                // Now create a new Metatag for each point and add it to the counter
                string value;
                try
                {
                    value = marker[dataLabel];
                }
                catch
                {
                    value = String.Empty;
                }

                List<Point> points = this.ParseCoordinate(value); // parse the contents into a set of points
                foreach (Point point in points)
                {
                    metaTagCounter.CreateMetaTag(point, dataLabel);  // add the metatage to the list
                }
                metaTagCounters.Add(metaTagCounter);   // and add that metaTag counter to our lists of metaTag counters
            }

            return metaTagCounters;
        }

        /// <summary>
        /// Set the list of marker points on the current row in the marker table. 
        /// </summary>
        /// <param name="imageID">the identifier of the row to update</param>
        /// <param name="dataLabel">data label</param>
        /// <param name="pointList">A list of points in the form x,y|x,y|x,y</param>
        public void SetMarkerPoints(long imageID, string dataLabel, string pointList)
        {
            // Find the current row number
            MarkerRow marker = this.FindMarker(imageID);
            if (marker == null)
            {
                return;
            }

            // Update the database and datatable
            marker[dataLabel] = pointList;
            this.UpdateRow(imageID, dataLabel, pointList, Constants.Database.MarkersTable);  // Update the database
        }

        public void SyncImageSetToDatabase()
        {
            this.Database.Update(Constants.Database.ImageSetTable, this.ImageSet.GetColumnTuples());
        }

        // The id is the row to update, the datalabels are the labels of each control to updata, 
        // and the markers are the respective point lists for each of those labels
        public void UpdateMarkers(List<ColumnTuplesWithWhere> markersToUpdate)
        {
            // update markers in database
            this.Database.Update(Constants.Database.MarkersTable, markersToUpdate);

            // update markers in marker data table
            char[] quote = { '\'' };
            foreach (ColumnTuplesWithWhere marker in markersToUpdate)
            {
                // We have to parse the id, as its in the form of Id=5 (for example)
                string idAsString = marker.Where.Substring(marker.Where.IndexOf("=") + 1);
                idAsString = idAsString.Trim(quote);

                long id;
                if (!Int64.TryParse(idAsString, out id))
                {
                    Debug.Print("Can't get the ID");
                    break;
                }
                foreach (ColumnTuple column in marker.Columns)
                {
                    if (!column.Value.Equals(String.Empty))
                    {
                        // TODOSAUL: .Rows is being indexed by ID rather than row index; is this correct?
                        // I think so... but need to check. I think the row ID will get the correct row but the row index (which I think can be reordered) could muck things up 
                        this.MarkersTable.Rows[(int)id - 1][column.Name] = column.Value;
                    }
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                if (this.ImageDataTable != null)
                {
                    this.ImageDataTable.Dispose();
                }
                if (this.MarkersTable != null)
                {
                    this.MarkersTable.Dispose();
                }
            }

            base.Dispose(disposing);
            this.disposed = true;
        }

        private ColumnTuple CreateImageDataColumnDefinition(ControlRow control)
        {
            return new ColumnTuple(control.DataLabel, "TEXT '" + control.DefaultValue + "'");
        }

        /// <summary>
        /// Given an id, find the row number that matches it in the Marker Table
        /// </summary>
        /// <returns>-1 on failure</returns>
        private MarkerRow FindMarker(long imageID)
        {
            return new MarkerRow(this.MarkersTable.Rows.Find(imageID));
        }

        private List<Point> ParseCoordinate(string value)
        {
            List<Point> points = new List<Point>();
            if (value.Equals(String.Empty))
            {
                return points;
            }

            char[] delimiterBar = { Constants.Database.MarkerBar };
            string[] pointsAsStrings = value.Split(delimiterBar);

            foreach (string pointAsString in pointsAsStrings)
            {
                Point point = Point.Parse(pointAsString);
                points.Add(point);
            }
            return points;
        }

        private void UpdateImageSet(string dataLabel, string value)
        {
            this.UpdateRow(Constants.Database.ImageSetRowID, dataLabel, value, Constants.Database.ImageSetTable);
        }
    }
}
