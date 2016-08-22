using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Images;
using Timelapse.Util;

namespace Timelapse.Database
{
    public class ImageDatabase : TemplateDatabase
    {
        private bool disposed;
        private DataGrid timelapseDataGrid;
        private DataRowChangeEventHandler onImageDataTableRowChanged;

        public CustomFilter CustomFilter { get; private set; }

        /// <summary>Gets the file name of the image database on disk.</summary>
        public string FileName { get; private set; }

        /// <summary>Gets the complete path to the folder containing the image database.</summary>
        public string FolderPath { get; private set; }

        public Dictionary<string, string> DataLabelFromStandardControlType { get; private set; }

        public Dictionary<string, ImageDataColumn> ImageDataColumnsByDataLabel { get; private set; }

        // contains the results of the data query
        public ImageDataTable ImageDataTable { get; private set; }

        public ImageSetRow ImageSet { get; private set; }

        // contains the markers
        public DataTableBackedList<MarkerRow> MarkersTable { get; private set; }

        public List<string> TemplateSynchronizationIssues { get; private set; }

        private ImageDatabase(string filePath)
            : base(filePath)
        {
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
            imageDatabase.GetMarkers();

            imageDatabase.CustomFilter = new CustomFilter(imageDatabase.TemplateTable, CustomFilterOperator.Or);
            imageDatabase.PopulateDataLabelMaps();
            return imageDatabase;
        }

        /// <summary>Gets the number of images currently in the image table.</summary>
        public int CurrentlySelectedImageCount
        {
            get { return this.ImageDataTable.RowCount; }
        }

        [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1126:PrefixCallsCorrectly", Justification = "StyleCop bug.")]
        public void AddImages(List<ImageRow> imagePropertiesList, Action<ImageRow, int> onImageAdded)
        {
            // We need to get a list of which columns are counters vs notes or fixed coices, 
            // as we will shortly have to initialize them to some defaults
            List<string> counterList = new List<string>();
            List<string> notesAndFixedChoicesList = new List<string>();
            List<string> flagsList = new List<string>();
            foreach (string columnName in this.ImageDataTable.ColumnNames)
            {
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

                    foreach (string columnName in this.ImageDataTable.ColumnNames)
                    {
                        // Fill up each column in order
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
                                imageRow.Add(new ColumnTuple(dataLabel, imageProperties.ImageQuality.ToString()));
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
                                        markerRow.Add(new ColumnTuple(controlName, String.Empty));
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

                this.InsertRows(Constants.Database.ImageDataTable, imageTableRows);
                this.InsertRows(Constants.Database.MarkersTable, markerTableRows);

                if (onImageAdded != null)
                {
                    int lastImageInserted = Math.Min(imagePropertiesList.Count - 1, image + Constants.Database.RowsPerInsert);
                    onImageAdded.Invoke(imagePropertiesList[lastImageInserted], lastImageInserted);
                }
            }

            // Load the marker table from the database - Doing so here will make sure that there is one row for each image.
            this.GetMarkers();
        }

        public void AppendToImageSetLog(StringBuilder logEntry)
        {
            this.ImageSet.Log += logEntry;
            this.SyncImageSetToDatabase();
        }

        public void BindToTimelapseDataGrid(DataGrid dataGrid, DataRowChangeEventHandler onRowChanged)
        {
            this.timelapseDataGrid = dataGrid;
            this.onImageDataTableRowChanged = onRowChanged;
            this.ImageDataTable.BindDataGrid(dataGrid, onRowChanged);
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
            foreach (ControlRow control in this.TemplateTable)
            {
                columnDefinitions.Add(this.CreateImageDataColumnDefinition(control));
            }
            this.Database.CreateTable(Constants.Database.ImageDataTable, columnDefinitions);

            // initialize ImageDataTable
            // this is necessary as images can't be added unless ImageDataTable.Columns is available
            // can't use TryGetImagesAll() here as that function's contract is not to update ImageDataTable if the select against the underlying database table 
            // finds no rows, which is the case for a database being created
            this.SelectDataTableImages(ImageFilter.All);

            // Create the ImageSetTable and initialize a single row in it
            columnDefinitions.Clear();
            columnDefinitions.Add(new ColumnTuple(Constants.DatabaseColumn.ID, Constants.Database.CreationStringPrimaryKey));  // It begins with the ID integer primary key
            columnDefinitions.Add(new ColumnTuple(Constants.DatabaseColumn.Log, "TEXT DEFAULT 'Add text here.'"));
            columnDefinitions.Add(new ColumnTuple(Constants.DatabaseColumn.Magnifier, " TEXT DEFAULT 'true'"));
            columnDefinitions.Add(new ColumnTuple(Constants.DatabaseColumn.Row, "TEXT DEFAULT '0'"));
            int allImages = (int)ImageFilter.All;
            columnDefinitions.Add(new ColumnTuple(Constants.DatabaseColumn.Filter, "TEXT DEFAULT '" + allImages.ToString() + "'"));
            columnDefinitions.Add(new ColumnTuple(Constants.DatabaseColumn.WhiteSpaceTrimmed, Constants.Sql.Text));
            this.Database.CreateTable(Constants.Database.ImageSetTable, columnDefinitions);

            List<ColumnTuple> columnsToUpdate = new List<ColumnTuple>(); // Populate the data for the image set with defaults
            columnsToUpdate.Add(new ColumnTuple(Constants.DatabaseColumn.Log, Constants.Database.ImageSetDefaultLog));
            columnsToUpdate.Add(new ColumnTuple(Constants.DatabaseColumn.Magnifier, Constants.Boolean.True));
            columnsToUpdate.Add(new ColumnTuple(Constants.DatabaseColumn.Row, "0"));
            columnsToUpdate.Add(new ColumnTuple(Constants.DatabaseColumn.Filter, allImages.ToString()));
            columnsToUpdate.Add(new ColumnTuple(Constants.DatabaseColumn.WhiteSpaceTrimmed, Constants.Boolean.True));
            List<List<ColumnTuple>> insertionStatements = new List<List<ColumnTuple>>();
            insertionStatements.Add(columnsToUpdate);
            this.Database.Insert(Constants.Database.ImageSetTable, insertionStatements);

            // Create the MarkersTable and initialize it from the template table
            columnDefinitions.Clear();
            columnDefinitions.Add(new ColumnTuple(Constants.DatabaseColumn.ID, Constants.Database.CreationStringPrimaryKey));  // t begins with the ID integer primary key
            string type = String.Empty;
            foreach (ControlRow control in this.TemplateTable)
            {
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
            // Correct for backwards compatability as needed, where:
            // - RelativePath (if missing) needs to be added 
            // - MarkForDeletion (if present) needs to be removed 
            // - DeleteFlag (if missing) needs to be added
            // add RelativePath column if it's not present in the image data table at postion '2'
            this.SelectDataTableImages(ImageFilter.All);
            if (this.ImageDataTable.ColumnNames.Contains(Constants.DatabaseColumn.RelativePath) == false)
            {
                long id = this.GetControlIDFromTemplateTable(Constants.DatabaseColumn.RelativePath);
                ControlRow control = this.TemplateTable.Find(id);
                ColumnTuple columnDefinition = this.CreateImageDataColumnDefinition(control);
                this.Database.AddColumnToTable(Constants.Database.ImageDataTable, Constants.Database.RelativePathPosition, columnDefinition);
            }

            // For backwards compatability, check if there is a MarkForDeletion column. If there is, remove it as it has been renamed to DeleteFlag.
            if (this.Database.IsColumnInTable(Constants.Database.ImageDataTable, Constants.ControlsDeprecated.MarkForDeletion) == true)
            {
                this.Database.DeleteColumn(Constants.Database.ImageDataTable, Constants.ControlsDeprecated.MarkForDeletion);
            }

            // For backwards compatability, check if there is a DeleteFlag Column. If not, add one.
            if (this.Database.IsColumnInTable(Constants.Database.ImageDataTable, Constants.Control.DeleteFlag) == false)
            {
                long id = this.GetControlIDFromTemplateTable(Constants.Control.DeleteFlag);
                ControlRow control = this.TemplateTable.Find(id);
                ColumnTuple columnDefinition = this.CreateImageDataColumnDefinition(control);
                this.Database.AddColumnToEndOfTable(Constants.Database.ImageDataTable, columnDefinition);
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
                this.Database.AddColumnToEndOfTable(Constants.Database.ImageSetTable, new ColumnTuple(Constants.DatabaseColumn.WhiteSpaceTrimmed, Constants.Sql.False));

                // trim the white space from all the data
                this.Database.TrimWhitespace(Constants.Database.ImageDataTable, dataLabels);
                ColumnTuplesWithWhere columnToUpdate = new ColumnTuplesWithWhere();
                columnToUpdate.Columns.Add(new ColumnTuple(Constants.DatabaseColumn.WhiteSpaceTrimmed, Constants.Boolean.True)); // Populate the data 
                columnToUpdate.SetWhere(Constants.Database.ImageSetRowID);
                this.Database.Update(Constants.Database.ImageSetTable, columnToUpdate);
            }
        }

        /// <summary>
        /// Create lookup tables that allow us to retrieve a key from a type and vice versa
        /// </summary>
        private void PopulateDataLabelMaps()
        {
            foreach (ControlRow control in this.TemplateTable)
            {
                ImageDataColumn column = ImageDataColumn.Create(control);
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
                this.Database = new SQLiteWrapper(Path.Combine(this.FolderPath, newFileName));          // Recreate the database connecction
            }
        }

        private ImageRow GetImage(string where)
        {
            if (String.IsNullOrWhiteSpace(where))
            {
                throw new ArgumentOutOfRangeException("where");
            }

            string query = "Select * FROM " + Constants.Database.ImageDataTable + " WHERE " + where;
            DataTable images = this.Database.GetDataTableFromSelect(query);
            ImageDataTable temporaryTable = new ImageDataTable(images);
            if (temporaryTable.RowCount != 1)
            {
                return null;
            }
            return temporaryTable[0];
        }

        /// <summary> 
        /// Populate the image table so that it matches all the entries in its associated database table.
        /// Then set the currentID and currentRow to the the first record in the returned set
        /// </summary>
        public void SelectDataTableImages(ImageFilter filter)
        {
            string query = "Select * FROM " + Constants.Database.ImageDataTable;
            bool dateFilteringRequired = false;
            string where = this.GetImagesWhere(filter, out dateFilteringRequired);
            if (String.IsNullOrEmpty(where) == false)
            {
                query += Constants.Sql.Where + where;
            }

            DataTable images = this.Database.GetDataTableFromSelect(query);
            if (dateFilteringRequired)
            {
                this.CustomFilter.FilterByDate(images);
            }

            this.ImageDataTable = new ImageDataTable(images);
            this.ImageDataTable.BindDataGrid(this.timelapseDataGrid, this.onImageDataTableRowChanged);
        }

        public ImageDataTable GetImagesMarkedForDeletion()
        {
            string where = this.DataLabelFromStandardControlType[Constants.Control.DeleteFlag] + "=\"true\""; // = value
            string query = "Select * FROM " + Constants.Database.ImageDataTable + " WHERE " + where;
            DataTable images = this.Database.GetDataTableFromSelect(query);
            return new ImageDataTable(images);
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
            imageProperties = this.GetImage(imageQuery.Where);

            if (imageProperties != null)
            {
                return true;
            }
            else
            {
                imageProperties = this.ImageDataTable.NewRow(imageFile);
                imageProperties.InitialRootFolderName = initialRootFolderName;
                imageProperties.RelativePath = relativePath;
                imageProperties.SetDateAndTimeFromFileInfo(this.FolderPath);
                return false;
            }
        }

        public Dictionary<ImageFilter, int> GetImageCountsByQuality()
        {
            Dictionary<ImageFilter, int> counts = new Dictionary<ImageFilter, int>();
            counts[ImageFilter.Dark] = this.GetImageCount(ImageFilter.Dark);
            counts[ImageFilter.Corrupted] = this.GetImageCount(ImageFilter.Corrupted);
            counts[ImageFilter.Missing] = this.GetImageCount(ImageFilter.Missing);
            counts[ImageFilter.Ok] = this.GetImageCount(ImageFilter.Ok);
            return counts;
        }

        public int GetImageCount(ImageFilter imageQuality)
        {
            string query = "Select Count(*) FROM " + Constants.Database.ImageDataTable;
            bool dateFilteringRequired = false;
            string where = this.GetImagesWhere(imageQuality, out dateFilteringRequired);
            if (String.IsNullOrEmpty(where))
            {
                if ((imageQuality == ImageFilter.Custom) && (dateFilteringRequired == false))
                {
                    // if no custom filter search terms are selected the image count is undefined as no filter is in operation
                    return -1;
                }
                // otherwise, the query is for all images as no where clause is present
            }
            else
            {
                query += Constants.Sql.Where + where;
            }

            if (dateFilteringRequired == false)
            {
                return this.Database.GetCountFromSelect(query);
            }

            query = "Select * FROM " + Constants.Database.ImageDataTable;
            if (String.IsNullOrEmpty(where) == false)
            {
                query += Constants.Sql.Where + where;
            }

            DataTable images = this.Database.GetDataTableFromSelect(query);
            this.CustomFilter.FilterByDate(images);
            return images.Rows.Count;
        }

        // Insert one or more rows into a table
        private void InsertRows(string table, List<List<ColumnTuple>> insertionStatements)
        {
            this.Database.Insert(table, insertionStatements);
        }

        private string GetImagesWhere(ImageFilter imageQuality, out bool dateFilteringRequired)
        {
            dateFilteringRequired = false;
            switch (imageQuality)
            {
                case ImageFilter.All:
                    return String.Empty;
                case ImageFilter.Corrupted:
                case ImageFilter.Dark:
                case ImageFilter.Missing:
                case ImageFilter.Ok:
                    return this.DataLabelFromStandardControlType[Constants.DatabaseColumn.ImageQuality] + "=\"" + imageQuality + "\"";
                case ImageFilter.MarkedForDeletion:
                    return this.DataLabelFromStandardControlType[Constants.Control.DeleteFlag] + "=\"true\"";
                case ImageFilter.Custom:
                    return this.CustomFilter.GetImagesWhere(out dateFilteringRequired);
                default:
                    throw new NotSupportedException(String.Format("Unhandled quality filter {0}.  For custom filters call CustomFilter.GetImagesWhere().", imageQuality));
            }
        }

        /// <summary>
        /// Update a column value (identified by its key) in an existing row (identified by its ID) 
        /// By default, if the table parameter is not included, we use the TABLEDATA table
        /// </summary>
        public void UpdateImage(long id, string dataLabel, string value)
        {
            // update the row in the database
            ColumnTuplesWithWhere columnToUpdate = new ColumnTuplesWithWhere();
            columnToUpdate.Columns.Add(new ColumnTuple(dataLabel, value)); // Populate the data 
            columnToUpdate.SetWhere(id);
            this.Database.Update(Constants.Database.ImageDataTable, columnToUpdate);

            // update the table
            ImageRow image = this.ImageDataTable.Find(id);
            image[dataLabel] = value;
        }

        // Update all rows in the filtered view only with the given key/value pair
        public void UpdateImagesInDataTable(string dataLabel, string value)
        {
            List<ColumnTuplesWithWhere> updateQuery = new List<ColumnTuplesWithWhere>();
            foreach (ImageRow image in this.ImageDataTable)
            {
                image[dataLabel] = value;
                List<ColumnTuple> columnToUpdate = new List<ColumnTuple>() { new ColumnTuple(dataLabel, value) };
                updateQuery.Add(new ColumnTuplesWithWhere(columnToUpdate, image.ID));
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
                ImageRow image = this.ImageDataTable[index];
                image[dataLabel] = value;
                List<ColumnTuple> columnToUpdate = new List<ColumnTuple>() { new ColumnTuple(dataLabel, value) };

                // update database
                ColumnTuplesWithWhere imageUpdate = new ColumnTuplesWithWhere(columnToUpdate, image.ID);
                imagesToUpdate.Add(imageUpdate);
            }

            this.Database.Update(Constants.Database.ImageDataTable, imagesToUpdate);
        }

        public void AdjustImageTimes(TimeSpan adjustment)
        {
            this.AdjustImageTimes(adjustment, 0, this.CurrentlySelectedImageCount - 1);
        }

        public void AdjustImageTimes(TimeSpan adjustment, int startRow, int endRow)
        {
            if (adjustment.Milliseconds != 0)
            {
                throw new ArgumentOutOfRangeException("adjustment", "The current format of the time column does not support milliseconds.");
            }
            this.AdjustImageTimes((DateTime imageTime) => { return adjustment; }, startRow, endRow);
        }

        // Given a time difference in ticks, update all the date / time field in the database
        // Note that it does NOT update the dataTable - this has to be done outside of this routine by regenerating the datatables with whatever filter is being used..
        public void AdjustImageTimes(Func<DateTime, TimeSpan> adjustment, int startRow, int endRow)
        {
            if (this.IsImageRowInRange(startRow) == false)
            {
                throw new ArgumentOutOfRangeException("startRow");
            }
            if (this.IsImageRowInRange(endRow) == false)
            {
                throw new ArgumentOutOfRangeException("endRow");
            }
            if (endRow < startRow)
            {
                throw new ArgumentOutOfRangeException("endRow", "endRow must be greater than or equal to startRow.");
            }
            if (this.CurrentlySelectedImageCount == 0)
            {
                return;
            }

            // We now have an unfiltered temporary data table
            // Get the original value of each, and update each date by the corrected amount if possible
            List<ImageRow> imagesToAdjust = new List<ImageRow>();
            TimeSpan mostRecentAdjustment = TimeSpan.Zero;
            for (int row = startRow; row <= endRow; ++row)
            { 
                ImageRow image = this.ImageDataTable[row];
                DateTime imageDateTime;
                if (image.TryGetDateTime(out imageDateTime))
                {
                    // adjust the date / time
                    mostRecentAdjustment = adjustment.Invoke(imageDateTime);
                    if (mostRecentAdjustment == TimeSpan.Zero)
                    {
                        continue;
                    }
                    imageDateTime += mostRecentAdjustment;
                    image.SetDateAndTime(imageDateTime);
                    imagesToAdjust.Add(image);
                }
                // Note that there is no else, which means we skip dates that can't be retrieved properly
            }

            // Now update the actual database with the new date/time values stored in the temporary table
            List<ColumnTuplesWithWhere> imagesToUpdate = new List<ColumnTuplesWithWhere>();
            foreach (ImageRow image in imagesToAdjust)
            {
                List<ColumnTuple> columnsToUpdate = new List<ColumnTuple>();                       // Update the date and time
                columnsToUpdate.Add(new ColumnTuple(Constants.DatabaseColumn.Date, image.Date));
                columnsToUpdate.Add(new ColumnTuple(Constants.DatabaseColumn.Time, image.Time));
                imagesToUpdate.Add(new ColumnTuplesWithWhere(columnsToUpdate, image.ID));
            }

            if (imagesToUpdate.Count > 0)
            {
                this.Database.Update(Constants.Database.ImageDataTable, imagesToUpdate);

                // Add an entry into the log detailing what we just did
                StringBuilder log = new StringBuilder(Environment.NewLine);
                log.AppendFormat("System entry: Adjusted dates and times of {0} selected files.{1}", imagesToAdjust.Count, Environment.NewLine);
                log.AppendFormat("The first file adjusted was '{0}', the last '{1}', and the last file was adjusted by {2}.{3}", imagesToAdjust[0].FileName, imagesToAdjust[imagesToAdjust.Count - 1].FileName, mostRecentAdjustment, Environment.NewLine);
                this.AppendToImageSetLog(log);
            }
        }

        // Update all the date fields by swapping the days and months.
        // This should ONLY be called if such swapping across all dates (excepting corrupt ones) is possible
        // as otherwise it will only swap those dates it can
        // It also assumes that the data table is showing All images
        public void ExchangeDayAndMonthInImageDates()
        {
            this.ExchangeDayAndMonthInImageDates(0, this.CurrentlySelectedImageCount - 1);
        }

        // Update all the date fields between the start and end index by swapping the days and months.
        public void ExchangeDayAndMonthInImageDates(int startRow, int endRow)
        {
            if (this.IsImageRowInRange(startRow) == false)
            {
                throw new ArgumentOutOfRangeException("startRow");
            }
            if (this.IsImageRowInRange(endRow) == false)
            {
                throw new ArgumentOutOfRangeException("endRow");
            }
            if (endRow < startRow)
            {
                throw new ArgumentOutOfRangeException("endRow", "endRow must be greater than or equal to startRow.");
            }
            if (this.CurrentlySelectedImageCount == 0)
            {
                return;
            }

            // Get the original date value of each. If we can swap the date order, do so. 
            List<ColumnTuplesWithWhere> imagesToUpdate = new List<ColumnTuplesWithWhere>();
            ImageRow firstImage = this.ImageDataTable[startRow];
            ImageRow lastImage = null;
            DateTime mostRecentOriginalDate = DateTime.MinValue;
            DateTime mostRecentReversedDate = DateTime.MinValue;
            for (int row = startRow; row <= endRow; row++)
            {
                ImageRow image = this.ImageDataTable[row];
                DateTime originalDateTime;
                DateTime reversedDate;
                if (image.TryGetDateTime(out originalDateTime))
                {
                    // If we fail on any of these, continue on to the next date
                    if (originalDateTime.Day > Constants.MonthsInYear)
                    {
                        continue;
                    }
                    try
                    {
                        reversedDate = new DateTime(originalDateTime.Year, originalDateTime.Day, originalDateTime.Month); // we have swapped the day with the month
                    }
                    catch (Exception exception)
                    {
                        Debug.Assert(false, String.Format("Reverse of date '{0}' failed.", image.Date), exception.ToString());
                        continue;
                    }
                }
                else
                {
                    continue;
                }

                // Now update the actual database with the new date/time values stored in the temporary table
                List<ColumnTuple> columnToUpdate = new List<ColumnTuple>();               // Update the date 
                columnToUpdate.Add(new ColumnTuple(Constants.DatabaseColumn.Date, DateTimeHandler.ToStandardDateString(reversedDate)));
                imagesToUpdate.Add(new ColumnTuplesWithWhere(columnToUpdate, image.ID));
                lastImage = image;
                mostRecentOriginalDate = originalDateTime;
                mostRecentReversedDate = reversedDate;
            }

            if (imagesToUpdate.Count > 0)
            {
                this.Database.Update(Constants.Database.ImageDataTable, imagesToUpdate);

                StringBuilder log = new StringBuilder(Environment.NewLine);
                log.AppendFormat("System entry: Swapped days and months for {0} files.{1}", imagesToUpdate.Count, Environment.NewLine);
                log.AppendFormat("The first file adjusted was '{0}' and the last '{1}'.{2}", firstImage.FileName, lastImage.FileName, Environment.NewLine);
                log.AppendFormat("The last file's date was changed from '{0}' to '{1}'.{2}", DateTimeHandler.ToStandardDateString(mostRecentOriginalDate), DateTimeHandler.ToStandardDateString(mostRecentReversedDate), Environment.NewLine);
                this.AppendToImageSetLog(log);
            }
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

        /// <summary>A convenience routine for checking to see if the image in the given row is displayable (i.e., not corrupted or missing)</summary>
        public bool IsImageDisplayable(int rowIndex)
        {
            if (this.IsImageRowInRange(rowIndex) == false)
            {
                return false;
            }

            ImageFilter imageQuality = this.ImageDataTable[rowIndex].ImageQuality;
            if ((imageQuality == ImageFilter.Corrupted) || (imageQuality == ImageFilter.Missing))
            {
                return false;
            }
            return true;
        }

        public bool IsImageRowInRange(int imageRowIndex)
        {
            return (imageRowIndex >= 0) && (imageRowIndex < this.CurrentlySelectedImageCount) ? true : false;
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
            for (int row = 0; row < this.CurrentlySelectedImageCount; ++row)
            {
                if (this.ImageDataTable[row].ID >= id)
                {
                    return row;
                }
            }
            return this.CurrentlySelectedImageCount - 1;
        }

        public string GetControlDefaultValue(string dataLabel)
        {
            long id = this.GetControlIDFromTemplateTable(dataLabel);
            ControlRow control = this.TemplateTable.Find(id);
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
            if (this.MarkersTable.RowCount == 0)
            {
                return metaTagCounters;    // This should not really happen, but just in case
            }

            // Get the current row number of the id in the marker table
            MarkerRow marker = this.MarkersTable.Find(imageID);
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
                catch (Exception exception)
                {
                    Debug.Assert(false, String.Format("Read of marker failed for dataLabel '{0}'.", dataLabel), exception.ToString());
                    value = String.Empty;
                }

                List<Point> points = this.ParseMarkerPoints(value); // parse the contents into a set of points
                foreach (Point point in points)
                {
                    metaTagCounter.CreateMetaTag(point, dataLabel);  // add the metatage to the list
                }
                metaTagCounters.Add(metaTagCounter);   // and add that metaTag counter to our lists of metaTag counters
            }

            return metaTagCounters;
        }

        private void GetMarkers()
        {
            string markersQuery = "Select * FROM " + Constants.Database.MarkersTable;
            this.MarkersTable = new DataTableBackedList<MarkerRow>(this.Database.GetDataTableFromSelect(markersQuery), (DataRow row) => { return new MarkerRow(row); });
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
            MarkerRow marker = this.MarkersTable.Find(imageID);
            if (marker == null)
            {
                return;
            }

            // Update the database and datatable
            marker[dataLabel] = pointList;
            this.SyncMarkerToDatabase(marker);
        }

        public void SyncImageSetToDatabase()
        {
            this.Database.Update(Constants.Database.ImageSetTable, this.ImageSet.GetColumnTuples());
        }

        public void SyncMarkerToDatabase(MarkerRow marker)
        {
            this.Database.Update(Constants.Database.MarkersTable, marker.GetColumnTuples());
        }

        // The id is the row to update, the datalabels are the labels of each control to updata, 
        // and the markers are the respective point lists for each of those labels
        public void UpdateMarkers(List<ColumnTuplesWithWhere> markersToUpdate)
        {
            // update markers in database
            this.Database.Update(Constants.Database.MarkersTable, markersToUpdate);

            // update markers in marker data table
            this.GetMarkers();
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
            if (control.DefaultValue.Trim() == String.Empty)
            { 
                 return new ColumnTuple(control.DataLabel + " TEXT", String.Empty);
            }
            else
            {
                return new ColumnTuple(control.DataLabel + " TEXT DEFAULT ", Utilities.QuoteForSql(control.DefaultValue)); // We quote defaults
            }
        }

        private List<Point> ParseMarkerPoints(string value)
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
    }
}
