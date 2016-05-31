// <copyright company="University of Calgary">
// Open source, please ask at this point, license undecided
// </copyright>
// <author>Matthew Alan Dunlap and Saul Greenberg</author>
// <summary></summary>

using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions; // For debugging
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Timelapse;
using Timelapse.Database;
using Timelapse.Util;

namespace System.Runtime.CompilerServices
{
    sealed class CallerMemberNameAttribute : Attribute { }
}

namespace TimelapseTemplateEditor
{
    #region Debugging Class  
    public class MyTrace
    {
        public static void MethodName(string message,
        [CallerMemberName] string memberName = "")
        {
            // Uncomment to enable tracing
            //if (message.Equals (String.Empty))
            //    Debug.Print("**: "  + memberName);
            // else
            //     Debug.Print("**" + message + ": " + memberName);
        }
    }
    #endregion

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Interface values
        SolidColorBrush notEditableCellColor = Brushes.LightGray; // Color of non-editable data grid items 

        // Database-related values
        SQLiteWrapper db;                                   // The Database where the template is stored

        string dbFileName;                                  //  Filename of the database; by convention it is DataTemplate.db
        public DataTable templateTable = new DataTable();   // The table holding the primary data template

        // Combobx manipulations and values
        CsvHelperMethods h = new CsvHelperMethods();        // A class for converting data into comma-separated values to deal with our lists in our choice items

        // Booleans for tracking state
        bool rowsActionsOn = false;
        bool tabWasPressed = false; //to make tab trigger row update.
        public bool generateControlsAndSpreadsheet = true;

        // Counters for tracking how many of each item we have
        int counterCount = 0;
        int noteCount = 0;
        int choiceCount = 0;
        int flagCount = 0;

        // These variables support the drag/drop of controls
        private bool _isDown;
        private bool _isDragging;
        private Point _startPoint;
        private UIElement _realDragSource;
        private UIElement _dummyDragSource = new UIElement();

        #region Constructors/Destructors
        /// <summary>
        /// Starts the UI.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            Closing += MainWindow_Closing;
            this.ShowAllColumnsMenuItem_Click(this.ShowAllColumns, null);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            string executable_folder = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);

            // A check to make sure that users installed Timelapse properly, i.e., that they did not drag it out of its installation folder.
            if (!File.Exists(Path.Combine(executable_folder, "System.Data.SQLite.dll")))
            {
                DialogMessageBox dlgMB = new DialogMessageBox();
                dlgMB.IconType = MessageBoxImage.Error;
                dlgMB.MessageTitle = "Timelapse needs to be in its original downloaded folder";
                dlgMB.MessageProblem = "The Timelapse Programs won't run properly as it was not correctly installed.";
                dlgMB.MessageReason = "When you downloaded Timelapse, it was in a folder with several other files and folders it needs. You probably dragged Timelapse out of that folder.";
                dlgMB.MessageSolution = "Put the Timelapse programs back in its original folder, or download it again.";
                dlgMB.MessageResult = "Timelapse will shut down. Try again after you do the above solution.";
                dlgMB.MessageHint = "If you want to access these programs from elsewhere, create a shortcut to it." + Environment.NewLine;
                dlgMB.MessageHint += "1. From its original folder, right-click the Timelapse program icon  and select 'Create Shortcut' from the menu." + Environment.NewLine;
                dlgMB.MessageHint += "2. Drag the shortcut icon to the location of your choice.";

                dlgMB.ShowDialog();
                Application.Current.Shutdown();
            }
            else
            {
                CheckForUpdate.GetAndParseVersion(this, false);
            }
        }

        // When the main window closes, apply any pending edits.
        void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            TemplateDataGrid.CommitEdit();
        }
        #endregion Constructors/Destructors

        #region DataGrid and New Database Initialization
        /// <summary>
        /// Given a database file path,create a new DB file if one does not exist, or load a DB file if there is one.
        /// After a DB file is loaded, the table is extracted and loaded a DataTable for binding to the DataGrid.
        /// Some listeners are added to the DataTable, and the DataTable is bound. The add row buttons are enabled.
        /// </summary>
        /// <param name="databasePath">The path of the DB file created or loaded</param>
        /// <param name="ourTableName">The name of the table loaded in the DB file. Always the same in the current implementation</param>
        public void InitializeDataGrid(string databasePath, string ourTableName)
        {
            MyTrace.MethodName("DG");

            // Create a new DB file if one does not exist, or load a DB file if there is one.
            if (!File.Exists(databasePath))
            {
                db = new SQLiteWrapper(databasePath);
                PopulateTemplateDatabase();
            }
            else
            {
                db = new SQLiteWrapper(databasePath);
            }

            // Have the window title include the database file name
            OnlyWindow.Title = EditorConstant.MainWindowBaseTitle + " | File: " + Path.GetFileName(dbFileName);

            // Load the template table from the database into the data table
            templateTable = db.GetDataTableFromSelect(Constants.Sql.SelectStarFrom + ourTableName);

            // Map the data tableto the data grid, and create a callback executed whenever the datatable row changes
            TemplateDataGrid.DataContext = templateTable;
            templateTable.RowChanged += templateTable_RowChanged;

            //Now that there is a data table, enable the buttons that allows rows to be added.
            this.AddCountRowButton.IsEnabled = true;
            this.AddChoiceRowButton.IsEnabled = true;
            this.AddNoteRowButton.IsEnabled = true;
            this.AddFlagRowButton.IsEnabled = true;

            // Update the user interface specified by the contents of the table
            // Change the help text message

            DataTable tempTable = db.GetDataTableFromSelect(Constants.Sql.SelectStarFrom + Constants.Database.TemplateTable + EditorConstant.Sql.ByControlSortOrder);
            ControlGeneration.GenerateControls(this, this.wp, tempTable);
            GenerateSpreadsheet();
            this.InitializeUI();
        }

        // Reload a database into the grid. We do this as part of the convert, where we create the database, but then have to reinitialize the datagrid if we want to see the results.
        // So this is actually a reduced form of INitializeDataGrid
        public void ReInitializeDataGrid(string databasePath, string ourTableName)
        {
            MyTrace.MethodName("DG");

            // Create a new DB file if one does not exist, or load a DB file if there is one.
            if (!File.Exists(databasePath))
            {
                db = new SQLiteWrapper(databasePath);
                PopulateTemplateDatabase();
            }
            else
            {
                db = new SQLiteWrapper(databasePath);
            }

            // Have the window title include the database file name
            OnlyWindow.Title = EditorConstant.MainWindowBaseTitle + " | File: " + Path.GetFileName(dbFileName);

            // Load the template table from the database into the data table
            templateTable = db.GetDataTableFromSelect(Constants.Sql.SelectStarFrom + ourTableName);

            // Map the data table to the data grid, and create a callback executed whenever the datatable row changes
            TemplateDataGrid.DataContext = templateTable;
            templateTable.RowChanged += templateTable_RowChanged;

            // Update the user interface specified by the contents of the table
            DataTable tempTable = db.GetDataTableFromSelect(Constants.Sql.SelectStarFrom + Constants.Database.TemplateTable + EditorConstant.Sql.ByControlSortOrder);
            ControlGeneration.GenerateControls(this, this.wp, tempTable);
            GenerateSpreadsheet();
            this.InitializeUI();
        }


        /// <summary>
        /// Called when a new DB is created. A new table is created and populated with required fields set to their default values.
        /// </summary>
        public void PopulateTemplateDatabase()
        {
            // create the template table
            List<ColumnTuple> templateTableColumns = new List<ColumnTuple>();
            templateTableColumns.Add(new ColumnTuple(Constants.DatabaseColumn.ID, "INTEGER primary key autoincrement"));
            templateTableColumns.Add(new ColumnTuple(Constants.Control.ControlOrder, "INTEGER"));
            templateTableColumns.Add(new ColumnTuple(Constants.Control.SpreadsheetOrder, "INTEGER"));
            templateTableColumns.Add(new ColumnTuple(Constants.DatabaseColumn.Type, "text"));
            templateTableColumns.Add(new ColumnTuple(Constants.Control.DefaultValue, "text"));
            templateTableColumns.Add(new ColumnTuple(Constants.Control.Label, "text"));
            templateTableColumns.Add(new ColumnTuple(Constants.Control.DataLabel, "text"));
            templateTableColumns.Add(new ColumnTuple(Constants.Control.Tooltip, "text"));
            templateTableColumns.Add(new ColumnTuple(Constants.Control.TextBoxWidth, "text"));
            templateTableColumns.Add(new ColumnTuple(Constants.Control.Copyable, "text"));
            templateTableColumns.Add(new ColumnTuple(Constants.Control.Visible, "text"));
            templateTableColumns.Add(new ColumnTuple(Constants.Control.List, "text"));
            this.db.CreateTable(Constants.Database.TemplateTable, templateTableColumns);

            // add default controls to table
            List<List<ColumnTuple>> controls = new List<List<ColumnTuple>>();
            int controlOrder = 1; // The control order, incremented by 1 for every new entry
            int spreadsheetOrder = 1; // The spreadsheet order, incremented by 1 for every new entry

            // file
            List<ColumnTuple> file = new List<ColumnTuple>();
            file.Add(new ColumnTuple(Constants.Control.ControlOrder, ++controlOrder));
            file.Add(new ColumnTuple(Constants.Control.SpreadsheetOrder, ++spreadsheetOrder));
            file.Add(new ColumnTuple(Constants.DatabaseColumn.Type, Constants.DatabaseColumn.File));
            file.Add(new ColumnTuple(Constants.Control.DefaultValue, EditorConstant.DefaultValue.File));
            file.Add(new ColumnTuple(Constants.Control.Label, Constants.DatabaseColumn.File));
            file.Add(new ColumnTuple(Constants.Control.DataLabel, Constants.DatabaseColumn.File));
            file.Add(new ColumnTuple(Constants.Control.Tooltip, EditorConstant.Tooltip.File));
            file.Add(new ColumnTuple(Constants.Control.TextBoxWidth, EditorConstant.DefaultWidth.File));
            file.Add(new ColumnTuple(Constants.Control.Copyable, "false"));
            file.Add(new ColumnTuple(Constants.Control.Visible, "true"));
            file.Add(new ColumnTuple(Constants.Control.List, String.Empty));
            controls.Add(file);

            // folder
            List<ColumnTuple> folder = new List<ColumnTuple>();
            folder.Add(new ColumnTuple(Constants.Control.ControlOrder, ++controlOrder));
            folder.Add(new ColumnTuple(Constants.Control.SpreadsheetOrder, ++spreadsheetOrder));
            folder.Add(new ColumnTuple(Constants.DatabaseColumn.Type, Constants.DatabaseColumn.Folder));
            folder.Add(new ColumnTuple(Constants.Control.DefaultValue, EditorConstant.DefaultValue.Folder));
            folder.Add(new ColumnTuple(Constants.Control.Label, Constants.DatabaseColumn.Folder));
            folder.Add(new ColumnTuple(Constants.Control.DataLabel, Constants.DatabaseColumn.Folder));
            folder.Add(new ColumnTuple(Constants.Control.Tooltip, EditorConstant.Tooltip.Folder));
            folder.Add(new ColumnTuple(Constants.Control.TextBoxWidth, EditorConstant.DefaultWidth.Folder));
            folder.Add(new ColumnTuple(Constants.Control.Copyable, "false"));
            folder.Add(new ColumnTuple(Constants.Control.Visible, "true"));
            folder.Add(new ColumnTuple(Constants.Control.List, String.Empty));
            controls.Add(folder);

            // date
            List<ColumnTuple> date = new List<ColumnTuple>();
            date.Add(new ColumnTuple(Constants.Control.ControlOrder, ++controlOrder));
            date.Add(new ColumnTuple(Constants.Control.SpreadsheetOrder, ++spreadsheetOrder));
            date.Add(new ColumnTuple(Constants.DatabaseColumn.Type, Constants.DatabaseColumn.Date));
            date.Add(new ColumnTuple(Constants.Control.DefaultValue, EditorConstant.DefaultValue.Date));
            date.Add(new ColumnTuple(Constants.Control.Label, Constants.DatabaseColumn.Date));
            date.Add(new ColumnTuple(Constants.Control.DataLabel, Constants.DatabaseColumn.Date));
            date.Add(new ColumnTuple(Constants.Control.Tooltip, EditorConstant.Tooltip.Date));
            date.Add(new ColumnTuple(Constants.Control.TextBoxWidth, EditorConstant.DefaultWidth.Date));
            date.Add(new ColumnTuple(Constants.Control.Copyable, "false"));
            date.Add(new ColumnTuple(Constants.Control.Visible, "true"));
            date.Add(new ColumnTuple(Constants.Control.List, String.Empty));
            controls.Add(date);

            // time
            List<ColumnTuple> time = new List<ColumnTuple>();
            time.Add(new ColumnTuple(Constants.Control.ControlOrder, ++controlOrder));
            time.Add(new ColumnTuple(Constants.Control.SpreadsheetOrder, ++spreadsheetOrder));
            time.Add(new ColumnTuple(Constants.DatabaseColumn.Type, Constants.DatabaseColumn.Time));
            time.Add(new ColumnTuple(Constants.Control.DefaultValue, EditorConstant.DefaultValue.Time));
            time.Add(new ColumnTuple(Constants.Control.Label, Constants.DatabaseColumn.Time));
            time.Add(new ColumnTuple(Constants.Control.DataLabel, Constants.DatabaseColumn.Time));
            time.Add(new ColumnTuple(Constants.Control.Tooltip, EditorConstant.Tooltip.Time));
            time.Add(new ColumnTuple(Constants.Control.TextBoxWidth, EditorConstant.DefaultWidth.Time));
            time.Add(new ColumnTuple(Constants.Control.Copyable, "false"));
            time.Add(new ColumnTuple(Constants.Control.Visible, "true"));
            time.Add(new ColumnTuple(Constants.Control.List, String.Empty));
            controls.Add(time);

            // image quality
            List<ColumnTuple> imageQuality = new List<ColumnTuple>();
            imageQuality.Add(new ColumnTuple(Constants.Control.ControlOrder, ++controlOrder));
            imageQuality.Add(new ColumnTuple(Constants.Control.SpreadsheetOrder, ++spreadsheetOrder));
            imageQuality.Add(new ColumnTuple(Constants.DatabaseColumn.Type, Constants.DatabaseColumn.ImageQuality));
            imageQuality.Add(new ColumnTuple(Constants.Control.DefaultValue, EditorConstant.DefaultValue.ImageQuality));
            imageQuality.Add(new ColumnTuple(Constants.Control.Label, Constants.DatabaseColumn.ImageQuality));
            imageQuality.Add(new ColumnTuple(Constants.Control.DataLabel, Constants.DatabaseColumn.ImageQuality));
            imageQuality.Add(new ColumnTuple(Constants.Control.Tooltip, EditorConstant.Tooltip.ImageQuality));
            imageQuality.Add(new ColumnTuple(Constants.Control.TextBoxWidth, EditorConstant.DefaultWidth.ImageQuality));
            imageQuality.Add(new ColumnTuple(Constants.Control.Copyable, "false"));
            imageQuality.Add(new ColumnTuple(Constants.Control.Visible, "true"));
            imageQuality.Add(new ColumnTuple(Constants.Control.List, Constants.ImageQuality.ListOfValues));
            controls.Add(imageQuality);

            // delete flag
            List<ColumnTuple> markForDeletion = new List<ColumnTuple>();
            markForDeletion.Add(new ColumnTuple(Constants.Control.ControlOrder, ++controlOrder));
            markForDeletion.Add(new ColumnTuple(Constants.Control.SpreadsheetOrder, ++spreadsheetOrder));
            markForDeletion.Add(new ColumnTuple(Constants.DatabaseColumn.Type, Constants.DatabaseColumn.DeleteFlag));
            markForDeletion.Add(new ColumnTuple(Constants.Control.DefaultValue, EditorConstant.DefaultValue.Flag));
            markForDeletion.Add(new ColumnTuple(Constants.Control.Label, Constants.DatabaseColumn.DeleteFlag));
            markForDeletion.Add(new ColumnTuple(Constants.Control.DataLabel, Constants.DatabaseColumn.DeleteFlag));
            markForDeletion.Add(new ColumnTuple(Constants.Control.Tooltip, EditorConstant.Tooltip.MarkForDeletion));
            markForDeletion.Add(new ColumnTuple(Constants.Control.TextBoxWidth, EditorConstant.DefaultWidth.Flag));
            markForDeletion.Add(new ColumnTuple(Constants.Control.Copyable, "false"));
            markForDeletion.Add(new ColumnTuple(Constants.Control.Visible, "true"));
            markForDeletion.Add(new ColumnTuple(Constants.Control.List, String.Empty));
            controls.Add(markForDeletion);

            // Insert the datalist into the table
            db.Insert(Constants.Database.TemplateTable, controls);
        }
        #endregion DataGrid and New Database Initialization

        #region Data Changed Listeners and Methods
        ///////////////////////////////////////////////////////////////////////////////////////////
        //// DATA TABLE CHANGED LISTENERS & METHODS.
        ///////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Updates the db with the current state of the DataGrid. 
        /// Essentially clears and rebuilds the database table, so is very inefficient unless one really wants to do this
        /// </summary>
        private void UpdateDBFull()
        {
            MyTrace.MethodName("DB");
            
            //Build a Dictionary with all the rows/columns corresponding to the grid. 
            List<List<ColumnTuple>> dictionaryList = new List<List<ColumnTuple>>();
            DataRowCollection rowCol = templateTable.Rows;
            foreach (DataRow row in templateTable.Rows)
            {
                List<ColumnTuple> control = new List<ColumnTuple>();
                for (int i = 0; i < row.ItemArray.Length; i++)
                {
                    //sanitize quotes and add to Dictionary.
                    string value = row[i].ToString();
                    value = value.Replace("'", "''");
                    control.Add(new ColumnTuple(templateTable.Columns[i].ToString(), value));
                }
                dictionaryList.Add(control);
            }

            // Clear the existing table and add the new values
            db.Delete(Constants.Database.TemplateTable, null);
            db.Insert(Constants.Database.TemplateTable, dictionaryList);

            // Update the simulated UI controls so that it reflects the current values in the database
            DataTable tempTable = db.GetDataTableFromSelect(Constants.Sql.SelectStarFrom + Constants.Database.TemplateTable + EditorConstant.Sql.ByControlSortOrder);
            ControlGeneration.GenerateControls(this, this.wp, tempTable);
            GenerateSpreadsheet();
        }

        /// <summary>
        /// Updates a given row in the db with the current state of the DataGrid. 
        /// </summary>
        private void UpdateDBRow(DataRow row)
        {
            MyTrace.MethodName("DB");

            List<ColumnTuple> columns = new List<ColumnTuple>();
            for (int i = 0; i < row.ItemArray.Length; i++)
            {
                // sanitize quotes
                string value = row[i].ToString().Replace("'", "''");
                if (value == String.Empty)
                {
                    value = " ";
                }
                columns.Add(new ColumnTuple(templateTable.Columns[i].ToString(), value));
            }

            ColumnTuplesWithWhere updateQuery = new ColumnTuplesWithWhere(columns, Constants.DatabaseColumn.ID + " = " + row[Constants.DatabaseColumn.ID]);
            db.Update(Constants.Database.TemplateTable, updateQuery);

            // Update the simulatedcontrols so that it reflects the current values in the database
            DataTable tempTable = db.GetDataTableFromSelect(Constants.Sql.SelectStarFrom + Constants.Database.TemplateTable + EditorConstant.Sql.ByControlSortOrder);
            if (this.generateControlsAndSpreadsheet)
            {
                ControlGeneration.GenerateControls(this, this.wp, tempTable);
                GenerateSpreadsheet();
            }
        }

        ///// <summary>
        ///// Whenever a row changes , save the db, which also updates the grid colors.
        ///// </summary>
        void templateTable_RowChanged(object sender, DataRowChangeEventArgs e)
        {
            MyTrace.MethodName("DB");
            if (!rowsActionsOn)
            {
                UpdateDBRow(e.Row);
            }
        }
        #endregion Data Changed Listeners and Methods=

        #region Datagrid Row Modifyiers listeners and methods
        ///////////////////////////////////////////////////////////////////////////////////////////
        //// DATAGRID ROW MODIFYING LISTENERS & METHODS.
        ///////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Logic to enable/disable editing buttons depending on there being a row selection
        /// Also sets the text for the remove row button.
        /// </summary>
        private void TemplateDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            MyTrace.MethodName("DG");
            DataRowView selectedRowView = TemplateDataGrid.SelectedItem as DataRowView;
            if (selectedRowView == null)
            {
                RemoveRowButton.IsEnabled = false;
                RemoveRowButton.Content = "Remove";
            }
            else if ((selectedRowView.Row[Constants.DatabaseColumn.Type].Equals(Constants.DatabaseColumn.File)
                || selectedRowView.Row[Constants.DatabaseColumn.Type].Equals(Constants.DatabaseColumn.Folder)
                || selectedRowView.Row[Constants.DatabaseColumn.Type].Equals(Constants.DatabaseColumn.Date)
                || selectedRowView.Row[Constants.DatabaseColumn.Type].Equals(Constants.DatabaseColumn.Time)
                || selectedRowView.Row[Constants.DatabaseColumn.Type].Equals(Constants.DatabaseColumn.ImageQuality)))
            {
                RemoveRowButton.IsEnabled = false;
                RemoveRowButton.Content = "Item cannot" + Environment.NewLine + "be removed";
            }
            else if (selectedRowView != null)
            {
                RemoveRowButton.IsEnabled = true;
                RemoveRowButton.Content = "Remove";
            }
        }

        /// <summary>
        /// Adds a row to the table. The row type is decided by the button tags.
        /// Default values are set for the added row, differing depending on type.
        /// </summary>
        private void AddRowButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;

            // Get all the data labels, as we have to ensure that a new data label doesn't have the same name as an existing on
            List<string> dataLabels = new List<string>();
            for (int i = 0; i < templateTable.Rows.Count; i++)
            {
                dataLabels.Add((string)templateTable.Rows[i][Constants.Control.DataLabel]);
            }

            templateTable.Rows.Add();

            templateTable.Rows[templateTable.Rows.Count - 1][Constants.Control.ControlOrder] = templateTable.Rows.Count;
            templateTable.Rows[templateTable.Rows.Count - 1][Constants.Control.SpreadsheetOrder] = templateTable.Rows.Count;
            templateTable.Rows[templateTable.Rows.Count - 1][Constants.Control.List] = EditorConstant.DefaultValue.List;
            if (button.Tag.ToString().Equals("Count"))
            {
                templateTable.Rows[templateTable.Rows.Count - 1][Constants.Control.DefaultValue] = EditorConstant.DefaultValue.Counter;
                templateTable.Rows[templateTable.Rows.Count - 1][Constants.DatabaseColumn.Type] = Constants.Control.Counter;
                templateTable.Rows[templateTable.Rows.Count - 1][Constants.Control.TextBoxWidth] = EditorConstant.DefaultWidth.Counter;
                templateTable.Rows[templateTable.Rows.Count - 1][Constants.Control.Copyable] = false;
                templateTable.Rows[templateTable.Rows.Count - 1][Constants.Control.Visible] = true;
                templateTable.Rows[templateTable.Rows.Count - 1][Constants.Control.Tooltip] = EditorConstant.DefaultTooltip.Counter;

                // Add some default label names for counters (e.g., Counter1, Counter2, etc.) to ensure they are not empty
                // If the data label name  exists, keep incrementing the count that is appended to the end
                // of the field type until it forms a unique data label name
                string candidate_label = Constants.Control.Counter + counterCount.ToString();
                while (dataLabels.Contains(candidate_label))
                {
                    counterCount++;
                    candidate_label = Constants.Control.Counter + counterCount.ToString();
                }
                templateTable.Rows[templateTable.Rows.Count - 1][Constants.Control.Label] = candidate_label;
                templateTable.Rows[templateTable.Rows.Count - 1][Constants.Control.DataLabel] = candidate_label;
                counterCount++;
            }
            else if (button.Tag.ToString().Equals(Constants.Control.Note))
            {
                templateTable.Rows[templateTable.Rows.Count - 1][Constants.Control.DefaultValue] = EditorConstant.DefaultValue.Note;
                templateTable.Rows[templateTable.Rows.Count - 1][Constants.DatabaseColumn.Type] = Constants.Control.Note;
                templateTable.Rows[templateTable.Rows.Count - 1][Constants.Control.TextBoxWidth] = EditorConstant.DefaultWidth.Note;
                templateTable.Rows[templateTable.Rows.Count - 1][Constants.Control.Copyable] = true;
                templateTable.Rows[templateTable.Rows.Count - 1][Constants.Control.Visible] = true;
                templateTable.Rows[templateTable.Rows.Count - 1][Constants.Control.Tooltip] = EditorConstant.DefaultTooltip.Note;

                // Add some default label names for notes (e.g., Note1, Note2, etc.) to ensure they are not empty
                // If the data label name  exists, keep incrementing the count that is appended to the end
                // of the field type until it forms a unique data label name
                string candidate_label = Constants.Control.Note + noteCount.ToString();
                while (dataLabels.Contains(candidate_label))
                {
                    noteCount++;
                    candidate_label = Constants.Control.Note + noteCount.ToString();
                }
                templateTable.Rows[templateTable.Rows.Count - 1][Constants.Control.DataLabel] = candidate_label;
                templateTable.Rows[templateTable.Rows.Count - 1][Constants.Control.Label] = candidate_label;
                noteCount++;
            }
            else if (button.Tag.ToString().Equals("Choice"))
            {
                templateTable.Rows[templateTable.Rows.Count - 1][Constants.Control.DefaultValue] = EditorConstant.DefaultValue.Choice;
                templateTable.Rows[templateTable.Rows.Count - 1][Constants.DatabaseColumn.Type] = Constants.Control.FixedChoice;
                templateTable.Rows[templateTable.Rows.Count - 1][Constants.Control.TextBoxWidth] = EditorConstant.DefaultWidth.Choice;
                templateTable.Rows[templateTable.Rows.Count - 1][Constants.Control.Copyable] = true;
                templateTable.Rows[templateTable.Rows.Count - 1][Constants.Control.Visible] = true;
                templateTable.Rows[templateTable.Rows.Count - 1][Constants.Control.Tooltip] = EditorConstant.DefaultTooltip.Choice;

                // Add some default label names for choices (e.g., Choice1, Choice2, etc.) to ensure they are not empty
                // If the data label name  exists, keep incrementing the count that is appended to the end
                // of the field type until it forms a unique data label name
                string candidate_label = EditorConstant.Control.Choice + choiceCount.ToString();
                while (dataLabels.Contains(candidate_label))
                {
                    choiceCount++;
                    candidate_label = EditorConstant.Control.Choice + choiceCount.ToString();
                }
                templateTable.Rows[templateTable.Rows.Count - 1][Constants.Control.DataLabel] = candidate_label;
                templateTable.Rows[templateTable.Rows.Count - 1][Constants.Control.Label] = candidate_label;
                choiceCount++;
            }
            else if (button.Tag.ToString().Equals("Flag"))
            {
                templateTable.Rows[templateTable.Rows.Count - 1][Constants.Control.DefaultValue] = EditorConstant.DefaultValue.Flag;
                templateTable.Rows[templateTable.Rows.Count - 1][Constants.DatabaseColumn.Type] = Constants.Control.Flag;
                templateTable.Rows[templateTable.Rows.Count - 1][Constants.Control.TextBoxWidth] = EditorConstant.DefaultWidth.Flag;
                templateTable.Rows[templateTable.Rows.Count - 1][Constants.Control.Copyable] = true;
                templateTable.Rows[templateTable.Rows.Count - 1][Constants.Control.Visible] = true;
                templateTable.Rows[templateTable.Rows.Count - 1][Constants.Control.Tooltip] = EditorConstant.DefaultTooltip.Flag;

                // Add some default label names for flags (e.g., Flag1, Flag2, etc.) to ensure they are not empty
                // If the data label name  exists, keep incrementing the count that is appended to the end
                // of the field type until it forms a unique data label name
                string candidate_label = Constants.Control.Flag + flagCount.ToString();
                while (dataLabels.Contains(candidate_label))
                {
                    flagCount++;
                    candidate_label = Constants.Control.Flag + flagCount.ToString();
                }
                templateTable.Rows[templateTable.Rows.Count - 1][Constants.Control.DataLabel] = candidate_label;
                templateTable.Rows[templateTable.Rows.Count - 1][Constants.Control.Label] = candidate_label;
                flagCount++;
            }

            List<ColumnTuple> control = new List<ColumnTuple>();
            for (int i = 0; i < templateTable.Columns.Count; i++)
            {
                control.Add(new ColumnTuple(templateTable.Columns[i].ColumnName, templateTable.Rows[templateTable.Rows.Count - 1][i].ToString()));
            }
            List<List<ColumnTuple>> controlWrapper = new List<List<ColumnTuple>>() { control };
            db.Insert(Constants.Database.TemplateTable, controlWrapper);

            TemplateDataGrid.ScrollIntoView(TemplateDataGrid.Items[TemplateDataGrid.Items.Count - 1]);
            DataTable tempTable = db.GetDataTableFromSelect(Constants.Sql.SelectStarFrom + Constants.Database.TemplateTable + EditorConstant.Sql.ByControlSortOrder);

            ControlGeneration.GenerateControls(this, this.wp, tempTable);
            ControlsWereReordered();
        }

        /// <summary>
        /// Removes a row from the table and shifts up the ids on the remaining rows.
        /// The required rows are unable to be deleted.
        /// </summary>
        private void RemoveRowButton_Click(object sender, RoutedEventArgs e)
        {
            rowsActionsOn = true;

            DataRowView selectedRowView = TemplateDataGrid.SelectedItem as DataRowView;
            int deleted_control_number = Convert.ToInt32((Int64)selectedRowView.Row[Constants.Control.ControlOrder]);
            int deleted_spreadsheet_number = Convert.ToInt32((Int64)selectedRowView.Row[Constants.Control.SpreadsheetOrder]);
            int this_control_number;
            int this_spreadsheet_number;
            List<ColumnTuplesWithWhere> update_statements = new List<ColumnTuplesWithWhere>();
            if (!(selectedRowView == null
                || selectedRowView.Row[Constants.DatabaseColumn.Type].Equals(Constants.DatabaseColumn.File)
                || selectedRowView.Row[Constants.DatabaseColumn.Type].Equals(Constants.DatabaseColumn.Folder)
                || selectedRowView.Row[Constants.DatabaseColumn.Type].Equals(Constants.DatabaseColumn.Date)
                || selectedRowView.Row[Constants.DatabaseColumn.Type].Equals(Constants.DatabaseColumn.Time)
                || selectedRowView.Row[Constants.DatabaseColumn.Type].Equals(Constants.DatabaseColumn.ImageQuality)))
            {
                string where = Constants.DatabaseColumn.ID + " = " + selectedRowView.Row[Constants.DatabaseColumn.ID];
                db.Delete(Constants.Database.TemplateTable, where);
                templateTable.Rows.Remove(selectedRowView.Row);

                // Regenerate the Counter order and Spreadsheet Order. Essentially, what we do is look at the order number. If its
                // greater than the one that was removed, we just decrement it
                for (int i = 0; i < templateTable.Rows.Count; i++)
                {
                    // If its not the deleted row...
                    this_control_number = Convert.ToInt32(templateTable.Rows[i][Constants.Control.ControlOrder]);
                    this_spreadsheet_number = Convert.ToInt32(templateTable.Rows[i][Constants.Control.SpreadsheetOrder]);
                    if (this_control_number != deleted_control_number) // If its the deleted control, ignore it as it will be removed.
                    {
                        List<ColumnTuple> rowDict = new List<ColumnTuple>();
                        // Decrement control numbers for those that are greater than the one that was removed, to account for that removal
                        if (this_control_number > deleted_control_number)
                        {
                            rowDict.Add(new ColumnTuple(Constants.Control.ControlOrder, this_control_number - 1));
                            templateTable.Rows[i][Constants.Control.ControlOrder] = this_control_number - 1;
                        }
                        else
                        {
                            rowDict.Add(new ColumnTuple(Constants.Control.ControlOrder, this_control_number));
                            templateTable.Rows[i][Constants.Control.ControlOrder] = this_control_number;
                        }
                        where = Constants.DatabaseColumn.ID + " = " + templateTable.Rows[i][Constants.DatabaseColumn.ID];
                        update_statements.Add(new ColumnTuplesWithWhere(rowDict, where));
                    }

                    if (this_spreadsheet_number != deleted_spreadsheet_number) // If its the deleted control, ignore it as it will be removed.
                    {
                        List<ColumnTuple> rowDict = new List<ColumnTuple>();
                        // Decrement control numbers for those that are greater than the one that was removed, to account for that removal
                        if (this_spreadsheet_number > deleted_spreadsheet_number)
                        {
                            rowDict.Add(new ColumnTuple(Constants.Control.SpreadsheetOrder, this_spreadsheet_number - 1));
                            templateTable.Rows[i][Constants.Control.SpreadsheetOrder] = this_spreadsheet_number - 1;
                        }
                        else
                        {
                            rowDict.Add(new ColumnTuple(Constants.Control.SpreadsheetOrder, this_spreadsheet_number));
                            templateTable.Rows[i][Constants.Control.SpreadsheetOrder] = this_spreadsheet_number;
                        }
                        where = Constants.DatabaseColumn.ID + " = " + templateTable.Rows[i][Constants.DatabaseColumn.ID];
                        update_statements.Add(new ColumnTuplesWithWhere(rowDict, where));
                    }
                }
                db.Update(Constants.Database.TemplateTable, update_statements);

                //update the controls so that it reflects the current values in the database
                DataTable tempTable = db.GetDataTableFromSelect(Constants.Sql.SelectStarFrom + Constants.Database.TemplateTable + EditorConstant.Sql.ByControlSortOrder);
                ControlGeneration.GenerateControls(this, this.wp, tempTable);
                GenerateSpreadsheet();
            }
            rowsActionsOn = false;
        }
        #endregion Datagrid Row Modifyiers listeners and methods

        #region Choice Edit Box Handlers
        // When the  choice list button is clicked, raise a dialog box that lets the user edit the list of choices
        private void btnChoiceListButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            string choice_list = String.Empty;

            /// The button tag holds the Control Order of the row the button is in, not the ID.
            /// So we have to search through the rows to find the one with the correct control order
            /// and retrieve / set the ItemList menu in that row.
            DataRow foundRow = findRow(1, button.Tag.ToString());

            // It should always find a row, but just in case...
            if (null != foundRow)
            {
                choice_list = foundRow[Constants.Control.List].ToString();
            }

            choice_list = CsvHelperMethods.ConvertBarsToLineBreaks(choice_list);
            DlgEditChoiceList dlg = new DlgEditChoiceList(button, choice_list);
            dlg.Owner = this;
            bool? result = dlg.ShowDialog();
            if (result == true)
            {
                if (null != foundRow)
                    foundRow[Constants.Control.List] = CsvHelperMethods.ConvertLineBreaksToBars(dlg.ItemList);
                else
                {
                    // We should never be null, so shouldn't get here. But just in case this does happen, 
                    // I am setting the itemList to be the one in the ControlOrder row. This was the original buggy version that didn't work, but what the heck.
                    templateTable.Rows[Convert.ToInt32(button.Tag) - 1][Constants.Control.List] = CsvHelperMethods.ConvertLineBreaksToBars(dlg.ItemList);
                }
            }
        }
        // Helper function
        // Find a row in the templateTable given a search value and a column number in a DatTable
        private DataRow findRow(int column_number, string searchValue)
        {
            int rowIndex = -1;
            foreach (DataRow row in templateTable.Rows)
            {
                rowIndex++;
                if (row[column_number].ToString().Equals(searchValue))
                {
                    return (row);
                }
            }
            // It should never return null, but just in case...
            return null;
        }
        #endregion

        #region Cell Editing / Coloring Listeners and Methods
        ///////////////////////////////////////////////////////////////////////////////////////////
        /// CELL EDITING/COLORING LISTENERS AND METHODS
        ///////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Informs application tab was used, which allows it to more quickly visualize the grid values in the preview.
        /// Tab does not normal raise the rowedited listener, which we are using to do the update.
        /// Note that by setting the e.handled to true, we ignore the tab navigation (as this was introducing problems)
        /// </summary>
        private void TemplateDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            MyTrace.MethodName("DG");
            if (e.Key == Key.Tab)
            {
                tabWasPressed = true;
                e.Handled = true;
            }
            if (TemplateDataGrid.CurrentColumn.Header.Equals("Data Label")) // No white space in a data label
            {
                int keyValue = (int)e.Key;
                if (e.Key == Key.Space)
                {
                    DialogMessageBox dlgMB = new DialogMessageBox();
                    dlgMB.IconType = MessageBoxImage.Warning;
                    dlgMB.MessageTitle = "Data Labels can only contain letters, numbers and '_'";
                    dlgMB.MessageProblem = "Data labels must begin with a letter, followed only by letters, numbers, and '_'.";
                    dlgMB.MessageResult = "We will automatically ignore any other characters, including spaces";
                    dlgMB.MessageHint = "Start your label with a letter. Then use any combination of letters, numbers, and '_'.";
                    dlgMB.ShowDialog();
                    e.Handled = true;
                }
            }
        }

        // Accept only numbers in counters, and t/f in flags
        // This could probably be done way simpler
        private void TemplateDataGrid_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            MyTrace.MethodName("DG");
            if (TemplateDataGrid.SelectedIndex != -1)
            {
                //this is how you can get an actual cell.
                DataGridRow aRow = (DataGridRow)TemplateDataGrid.ItemContainerGenerator.ContainerFromIndex(TemplateDataGrid.SelectedIndex);
                DataGridCellsPresenter presenter = GetVisualChild<DataGridCellsPresenter>(aRow);
                if (TemplateDataGrid.CurrentColumn != null)
                {
                    DataGridCell cell = (DataGridCell)presenter.ItemContainerGenerator.ContainerFromIndex(TemplateDataGrid.CurrentColumn.DisplayIndex);

                    // We only do something if its an editable row and if we are in the Default Value column
                    if (cell.Background.Equals(notEditableCellColor))
                    {
                        e.Handled = true;
                        return;
                    }
                    if (!cell.Background.Equals(notEditableCellColor))
                    {
                        if (TemplateDataGrid.CurrentColumn.Header.Equals("Data Label")) // Only allow alphanumeric and  '_" in data labels
                        {
                            if ((!AreAllValidAlphaNumericChars(e.Text)) && !e.Text.Equals("_"))
                            {
                                DialogMessageBox dlgMB = new DialogMessageBox();
                                dlgMB.IconType = MessageBoxImage.Warning;
                                dlgMB.MessageTitle = "Data Labels can only contain letters, numbers and '_'";
                                dlgMB.MessageProblem = "Data labels must begin with a letter, followed only by letters, numbers, and '_'.";
                                dlgMB.MessageResult = "We will automatically ignore other characters, including spaces";
                                dlgMB.MessageHint = "Start your label with a letter. Then use any combination of letters, numbers, and '_'.";
                                dlgMB.ShowDialog();
                                e.Handled = true;
                            }

                        }
                        else if (TemplateDataGrid.CurrentColumn.Header.Equals("Default Value"))
                        {
                            DataRowView row = aRow.Item as DataRowView;
                            if ((string)row.Row.ItemArray[3] == Constants.Control.Counter) // Its a counter. Only allow numbers
                            {
                                e.Handled = !AreAllValidNumericChars(e.Text);
                            }
                            else if ((string)row.Row.ItemArray[3] == Constants.Control.Flag) // Its a flag. Only allow t/f and translate that to true / false
                            {
                                DataRowView dataRow = (DataRowView)TemplateDataGrid.SelectedItem;
                                int index = TemplateDataGrid.CurrentCell.Column.DisplayIndex;
                                if ((e.Text == "t" || e.Text == "T"))
                                {
                                    dataRow.Row[index] = "true";
                                    UpdateDBRow(dataRow.Row);
                                }
                                else if ((e.Text == "f" || e.Text == "F"))
                                {
                                    dataRow.Row[index] = "false";
                                    UpdateDBRow(dataRow.Row);
                                }
                                e.Handled = true;
                            }
                        }
                    }
                }
            }
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
        // Helper function for the above
        private bool AreAllValidAlphaNumericChars(string str)
        {
            foreach (char c in str)
            {
                if (!Char.IsLetterOrDigit(c)) return false;
            }
            return true;
        }
        /// <summary>
        /// Before cell editing begins on a cell click, the cell is disabled if it is grey (meaning cannot be edited).
        /// Another method re-enables the cell immediately afterwards.
        /// The reason for this implementation is because disabled cells cannot be single clicked, which is needed for row actions.
        /// </summary>
        private void NewTemplateDataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            MyTrace.MethodName("DG");
            if (TemplateDataGrid.SelectedIndex != -1)
            {
                //this is how you can get an actual cell.
                DataGridRow aRow = (DataGridRow)TemplateDataGrid.ItemContainerGenerator.ContainerFromIndex(TemplateDataGrid.SelectedIndex);
                DataGridCellsPresenter presenter = GetVisualChild<DataGridCellsPresenter>(aRow);
                if (TemplateDataGrid.CurrentColumn != null)
                {
                    DataGridCell cell = (DataGridCell)presenter.ItemContainerGenerator.ContainerFromIndex(TemplateDataGrid.CurrentColumn.DisplayIndex);

                    if (cell.Background.Equals(notEditableCellColor))
                    {
                        cell.IsEnabled = false;
                        TemplateDataGrid.CancelEdit();
                    }
                }
            }
        }


        /// <summary>
        /// After cell editing ends (prematurely or no), the cell is enabled.
        /// See TemplateDataGrid_BeginningEdit for full explaination.
        /// </summary>
        private void TemplateDataGrid_CurrentCellChanged(object sender, EventArgs e)
        {
            MyTrace.MethodName("DG");
            if (TemplateDataGrid.SelectedIndex != -1)
            {
                //this is how you can get an actual cell.
                DataGridRow aRow = (DataGridRow)TemplateDataGrid.ItemContainerGenerator.ContainerFromIndex(TemplateDataGrid.SelectedIndex);
                DataGridCellsPresenter presenter = GetVisualChild<DataGridCellsPresenter>(aRow);
                if (TemplateDataGrid.CurrentColumn != null)
                {
                    DataGridCell cell = (DataGridCell)presenter.ItemContainerGenerator.ContainerFromIndex(TemplateDataGrid.CurrentColumn.DisplayIndex);
                    cell.IsEnabled = true;
                }
            }
            if (tabWasPressed)
            {
                tabWasPressed = false;
                DataRowView selectedRowView = TemplateDataGrid.SelectedItem as DataRowView; //current cell
                UpdateDBRow(selectedRowView.Row);
            }
        }

        // Check to see if the data label entered is a reserved word or if its a non-unique label
        private void NewTemplateDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            MyTrace.MethodName("Main");

            // If the edited cell is not in the Data Label column, then just exit.
            if (!e.Column.Header.Equals("Data Label")) return;

            TextBox t = e.EditingElement as TextBox;
            string datalabel = t.Text;

            // Create a list of existing data labels, so we can compare the data label against it for a unique names
            List<string> data_label_list = new List<string>();
            for (int i = 0; i < templateTable.Rows.Count; i++)
            {
                data_label_list.Add((string)templateTable.Rows[i][Constants.Control.DataLabel]);
            }


            // Check to see if the data label is empty. If it is, generate a unique data label and warn the user
            if (datalabel.Trim().Equals(String.Empty))
            {
                // We need to generate a unique new datalabel  
                int suffix = 1;
                string candidate_datalabel = "DataLabel" + suffix.ToString();
                while (data_label_list.Contains(candidate_datalabel))  // Keep on incrementing the suffix until the datalabel is not in the list
                {
                    suffix++;
                    candidate_datalabel = "DataLabel" + suffix.ToString();
                }
                DialogMessageBox dlgMB = new DialogMessageBox();
                dlgMB.IconType = MessageBoxImage.Warning;
                dlgMB.MessageTitle = "Data Labels cannot be empty";
                dlgMB.MessageProblem = "Data Labels cannot be empty. They must begin with a letter, followed only by letters, numbers, and '_'.";
                dlgMB.MessageResult = "We will automatically create a uniquely named Data Label for you.";
                dlgMB.MessageHint = "You can create your own name for this Data Label. Start your label with a letter. Then use any combination of letters, numbers, and '_'.";
                dlgMB.ShowDialog();
                t.Text = candidate_datalabel;
            }

            // Check to see if the data label is unique. If not, generate a unique data label and warn the user
            for (int i = 0; i < templateTable.Rows.Count; i++)
            {
                if (datalabel.Equals((string)templateTable.Rows[i][Constants.Control.DataLabel]))
                {
                    if (TemplateDataGrid.SelectedIndex == i) continue; // Its the same row, so its the same key, so skip it

                    // We need to generate a unique new datalabel  
                    int suffix = 1;
                    string candidate_datalabel = datalabel + suffix.ToString();
                    while (data_label_list.Contains(candidate_datalabel))  // Keep on incrementing the suffix until the datalabel is not in the list
                    {
                        suffix++;
                        candidate_datalabel = datalabel + suffix.ToString();
                    }
                    DialogMessageBox dlgMB = new DialogMessageBox();
                    dlgMB.IconType = MessageBoxImage.Warning;
                    dlgMB.MessageTitle = "Data Labels must be unique";
                    dlgMB.MessageProblem = "'" + t.Text + "' is not a valid Data Label, as you have already used it in another row.";
                    dlgMB.MessageResult = "We will automatically create a unique Data Label for you by adding a number to its end.";
                    dlgMB.MessageHint = "You can create your own unique name for this Data Label. Start your label with a letter. Then use any combination of letters, numbers, and '_'.";
                    dlgMB.ShowDialog();

                    t.Text = candidate_datalabel;
                    break;
                }
            }

            // Check to see if the label (if its not empty, which it shouldn't be) has any illegal characters.
            // Note that most of this is redundant, as we have already checked for illegal characters as they are typed. However,
            // we have not checked to see if the first letter is alphabetic.
            if (datalabel.Length > 0)
            {
                Regex alphanumdash = new Regex("^[a-zA-Z0-9_]*$");
                Regex alpha = new Regex("^[a-zA-Z]*$");

                string first_letter = datalabel[0].ToString();

                if (!(alpha.IsMatch(first_letter) && alphanumdash.IsMatch(datalabel)))
                {
                    string candidate_datalabel = datalabel;

                    if (!alpha.IsMatch(first_letter))
                        candidate_datalabel = "X" + candidate_datalabel.Substring(1);
                    candidate_datalabel = Regex.Replace(candidate_datalabel, @"[^A-Za-z0-9_]+", "X");

                    DialogMessageBox dlgMB = new DialogMessageBox();
                    dlgMB.IconType = MessageBoxImage.Warning;
                    dlgMB.MessageTitle = "'" + t.Text + "' is not a valid Data Label.";
                    dlgMB.MessageProblem = "Data labels must begin with a letter, followed only by letters, numbers, and '_'.";
                    dlgMB.MessageResult = "We will replace all dissallowed characters with an 'X'.";
                    dlgMB.MessageHint = "Start your label with a letter. Then use any combination of letters, numbers, and '_'.";
                    dlgMB.ShowDialog();

                    t.Text = candidate_datalabel;
                }
            }

            // Check to see if its a reserved word
            datalabel = datalabel.ToUpper();
            foreach (string s in EditorConstant.ReservedWords)
            {
                if (s.Equals(datalabel))
                {
                    DialogMessageBox dlgMB = new DialogMessageBox();
                    dlgMB.IconType = MessageBoxImage.Warning;
                    dlgMB.MessageTitle = "'" + t.Text + "' is not a valid Data Label.";
                    dlgMB.MessageProblem = "Data labels cannot match the reserved words.";
                    dlgMB.MessageResult = "We will add an '_' suffix to this Data Label to make it differ from the reserved word";
                    dlgMB.MessageHint = "Avoid the resereved words listed below. Start your label with a letter. Then use any combination of letters, numbers, and '_'." + Environment.NewLine;
                    foreach (string m in this.RESERVED_KEYWORDS) dlgMB.MessageHint += m + " ";

                    dlgMB.ShowDialog();

                    t.Text += "_";
                    break;
                }
            }
        }

        //constants for below method. Could not find another way to reference the DataGrid items by name
        //If the DataGrid columns change, this needs to be adjusted to index correctly.
        int DG_TYPE = 3;

        //more constants to access checkbox columns and combobox columns.
        //the sortmemberpath is include, not the sort name, so we are accessing by head, which may change.
        string DG_HEAD_COPYABLE = Constants.Control.Copyable;
        string DG_HEAD_LIST = Constants.Control.List;

        /// <summary>
        /// Greys out cells as defined by logic. 
        /// This is to visually show the user uneditable cells, and informs events about whether a cell can be edited.
        /// This is called after row are added/moved/deleted to update the colors. 
        /// This also disables checkboxes that cannot be edited. Disabling checkboxes does not effect row interactions.
        /// </summary>
        public void UpdateCellColors()
        {
            for (int i = 0; i < TemplateDataGrid.Items.Count; i++)
            {
                DataGridRow aRow = (DataGridRow)TemplateDataGrid.ItemContainerGenerator.ContainerFromIndex(i);
                if (aRow != null)
                {
                    DataGridCellsPresenter presenter = GetVisualChild<DataGridCellsPresenter>(aRow);
                    for (int j = 0; j < TemplateDataGrid.Columns.Count; j++)
                    {
                        // The following attributes should always be editable.
                        // Note that this is hardwired to the header names in the xaml file, so this could break if that ever changes.. should probably do this so it works no matter what the header text is of the table
                        if (TemplateDataGrid.Columns[j].Header.Equals("Width") || TemplateDataGrid.Columns[j].Header.Equals("Visible") || TemplateDataGrid.Columns[j].Header.Equals("Label") || TemplateDataGrid.Columns[j].Header.Equals("Tooltip"))
                            continue;

                        // The following attributes should NOT be editable.
                        DataGridCell cell = (DataGridCell)presenter.ItemContainerGenerator.ContainerFromIndex(j);
                        DataRowView thisRow = (DataRowView)TemplateDataGrid.Items[i];


                        if (TemplateDataGrid.Columns[j].SortMemberPath.Equals(Constants.DatabaseColumn.ID)
                            || TemplateDataGrid.Columns[j].SortMemberPath.Equals(Constants.Control.ControlOrder)
                            || TemplateDataGrid.Columns[j].SortMemberPath.Equals(Constants.Control.SpreadsheetOrder)
                            || TemplateDataGrid.Columns[j].SortMemberPath.Equals(Constants.DatabaseColumn.Type)
                            || thisRow.Row.ItemArray[DG_TYPE].Equals(Constants.DatabaseColumn.File)
                            || thisRow.Row.ItemArray[DG_TYPE].Equals(Constants.DatabaseColumn.Folder)
                            || (thisRow.Row.ItemArray[DG_TYPE].Equals(Constants.DatabaseColumn.Date))
                            || (thisRow.Row.ItemArray[DG_TYPE].Equals(Constants.DatabaseColumn.Time))
                            || (thisRow.Row.ItemArray[DG_TYPE].Equals(Constants.DatabaseColumn.DeleteFlag))
                            || (thisRow.Row.ItemArray[DG_TYPE].Equals(Constants.Control.Counter) && TemplateDataGrid.Columns[j].Header.Equals(DG_HEAD_LIST))
                            || (thisRow.Row.ItemArray[DG_TYPE].Equals(Constants.Control.Note) && TemplateDataGrid.Columns[j].Header.Equals(DG_HEAD_LIST))
                            || (thisRow.Row.ItemArray[DG_TYPE].Equals(Constants.DatabaseColumn.File) && TemplateDataGrid.Columns[j].Header.Equals(DG_HEAD_COPYABLE))
                            || (thisRow.Row.ItemArray[DG_TYPE].Equals(Constants.DatabaseColumn.Folder) && TemplateDataGrid.Columns[j].Header.Equals(DG_HEAD_COPYABLE))
                            || (thisRow.Row.ItemArray[DG_TYPE].Equals(Constants.DatabaseColumn.Date) && TemplateDataGrid.Columns[j].Header.Equals(DG_HEAD_COPYABLE))
                            || (thisRow.Row.ItemArray[DG_TYPE].Equals(Constants.DatabaseColumn.Time) && TemplateDataGrid.Columns[j].Header.Equals(DG_HEAD_COPYABLE))
                            || (thisRow.Row.ItemArray[DG_TYPE].Equals(Constants.DatabaseColumn.ImageQuality) && TemplateDataGrid.Columns[j].Header.Equals("Data Label"))
                            || (thisRow.Row.ItemArray[DG_TYPE].Equals(Constants.DatabaseColumn.ImageQuality) && TemplateDataGrid.Columns[j].Header.Equals("List"))
                            || (thisRow.Row.ItemArray[DG_TYPE].Equals(Constants.DatabaseColumn.ImageQuality) && TemplateDataGrid.Columns[j].Header.Equals(DG_HEAD_COPYABLE))
                            || (thisRow.Row.ItemArray[DG_TYPE].Equals(Constants.DatabaseColumn.ImageQuality) && TemplateDataGrid.Columns[j].Header.Equals("List"))
                            || (thisRow.Row.ItemArray[DG_TYPE].Equals(Constants.DatabaseColumn.ImageQuality) && TemplateDataGrid.Columns[j].SortMemberPath.Equals(Constants.Control.DefaultValue)))
                        {
                            cell.Background = notEditableCellColor;
                            cell.Foreground = Brushes.Gray;

                            //if cell has a checkbox, also disable it.
                            var cp = cell.Content as ContentPresenter;
                            if (cp != null)
                            {
                                var checkbox = cp.ContentTemplate.FindName("CheckBox", cp) as CheckBox;
                                if (checkbox != null)
                                {
                                    checkbox.IsEnabled = false;
                                }
                                else if (thisRow.Row.ItemArray[DG_TYPE].Equals(Constants.DatabaseColumn.ImageQuality) && TemplateDataGrid.Columns[j].Header.Equals("List"))
                                {
                                    cell.IsEnabled = false; // Don't let users edit the ImageQuality menu
                                }
                            }
                        }
                        else
                        {
                            cell.ClearValue(DataGridCell.BackgroundProperty); //otherwise when scrolling cells offscreen get colored randomly
                            var cp = cell.Content as ContentPresenter;

                            //if cell has a checkbox, enable it.
                            if (cp != null)
                            {
                                var checkbox = cp.ContentTemplate.FindName("CheckBox", cp) as CheckBox;
                                if (checkbox != null)
                                {
                                    checkbox.IsEnabled = true;
                                }
                            }

                        }
                    }
                }
            }
        }

        /// <summary>
        /// Updates colors when the Layout changes.
        /// </summary>
        private void TemplateDataGrid_LayoutUpdated(object sender, EventArgs e)
        {
            UpdateCellColors();
        }

        /// <summary>
        /// Used in this code to get the child of a DataGridRows, DataGridCellsPresenter. This can be used to get the DataGridCell.
        /// WPF does not make it easy to get to the actual cells.
        /// Code from: http://techiethings.blogspot.com/2010/05/get-wpf-datagrid-row-and-cell.html
        /// </summary>
        public static T GetVisualChild<T>(Visual parent) where T : Visual
        {
            T child = default(T);
            int numVisuals = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < numVisuals; i++)
            {
                Visual v = (Visual)VisualTreeHelper.GetChild(parent, i);
                child = v as T;
                if (child == null)
                {
                    child = GetVisualChild<T>(v);
                }
                if (child != null)
                {
                    break;
                }
            }
            return child;
        }
        #endregion Cell Editing / Coloring Listeners and Methods

        #region Menu listeners
        ///////////////////////////////////////////////////////////////////////////////////////////
        //// MENU BAR METHODS & LISTENERS
        ///////////////////////////////////////////////////////////////////////////////////////////		

        /// <summary>
        /// Creates a new database file of a user chosen name in a user chosen location.
        /// </summary>
        private void NewFileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            TemplateDataGrid.CommitEdit(); //to apply edits that the enter key was not pressed

            // Configure save file dialog box
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.FileName = Path.GetFileNameWithoutExtension(Constants.File.DefaultTemplateDatabaseFileName); // Default file name without the extension
            dlg.DefaultExt = Constants.File.TemplateDatabaseFileExtension; // Default file extension
            dlg.Filter = "Database Files (" + Constants.File.TemplateDatabaseFileExtension + ")|*" + Constants.File.TemplateDatabaseFileExtension; // Filter files by extension 
            dlg.Title = "Select Location to Save New Template File";

            // Show save file dialog box
            Nullable<bool> result = dlg.ShowDialog();

            // Process save file dialog box results 
            if (result == true)
            {
                SaveDbBackup();
                if (File.Exists(dlg.FileName))     // Overwrite the file if it exists
                {
                    File.Delete(dlg.FileName);
                }

                // Open document 
                dbFileName = dlg.FileName;
                InitializeDataGrid(dbFileName, Constants.Database.TemplateTable);
            }

        }

        /// <summary>
        /// Opens a database file.
        /// </summary>
        private void OpenFileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            TemplateDataGrid.CommitEdit(); //to save any edits that the enter key was not pressed

            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.FileName = Path.GetFileNameWithoutExtension(Constants.File.DefaultTemplateDatabaseFileName); // Default file name without the extension
            dlg.DefaultExt = Constants.File.TemplateDatabaseFileExtension; // Default file extension
            dlg.Filter = "Database Files (" + Constants.File.TemplateDatabaseFileExtension + ")|*" + Constants.File.TemplateDatabaseFileExtension; // Filter files by extension 
            dlg.Title = "Select an Existing Template File to Open";

            // Show open file dialog box
            Nullable<bool> result = dlg.ShowDialog();

            // Process open file dialog box results 
            if (result == true)
            {
                SaveDbBackup();

                // Open document 
                dbFileName = dlg.FileName;
                InitializeDataGrid(dbFileName, Constants.Database.TemplateTable);
            }
        }

        // Depending on the menu's checkbox state, show all columns or hide selected columns
        private void ShowAllColumnsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MenuItem mi = sender as MenuItem;
            if (mi == null) return;
            foreach (DataGridColumn col in TemplateDataGrid.Columns)
            {
                if (!mi.IsChecked && (col.Header.Equals("ID") || col.Header.Equals("Control\norder") || col.Header.Equals("Spreadsheet\norder")))
                {
                    col.MinWidth = 0;
                    col.Width = new DataGridLength(0);
                }
                else
                {
                    col.MinWidth = (col.Header.Equals("Visible") || col.Header.Equals("Copyable") || col.Header.Equals("Width")) ? 65 : 90;
                    if (col.Header.Equals("Tooltip"))
                    {
                        col.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
                    }
                    else
                    {
                        col.Width = new DataGridLength(1, DataGridLengthUnitType.SizeToCells);
                    }
                }
            }
        }

        private void ConvertCodeTemplateFileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            string codeTemplateFileName = String.Empty;  // The code template file name

            TemplateDataGrid.CommitEdit(); //to save any edits that the enter key was not pressed

            // Get the name of the Code Template file to open
            Microsoft.Win32.OpenFileDialog dlg1 = new Microsoft.Win32.OpenFileDialog();
            dlg1.FileName = Path.GetFileName(Constants.File.XmlDataFileName); // Default file name
            string xmlDataFileExtension = Path.GetExtension(Constants.File.XmlDataFileName);
            dlg1.DefaultExt = xmlDataFileExtension; // Default file extension
            dlg1.Filter = "Code Template Files (" + xmlDataFileExtension + ")|*" + xmlDataFileExtension; // Filter files by extension 
            dlg1.Title = "Select Code Template File to convert...";

            Nullable<bool> result = dlg1.ShowDialog(); // Show the open file dialog box
            if (result == true) codeTemplateFileName = dlg1.FileName;  // Process open file dialog box results 
            else return;

            // Get the name of the new database file to create (over-writes it if it exists)
            Microsoft.Win32.SaveFileDialog dlg2 = new Microsoft.Win32.SaveFileDialog();
            dlg2.Title = "Select Location to Save the Converted Template File";
            dlg2.FileName = Path.GetFileNameWithoutExtension(Constants.File.DefaultTemplateDatabaseFileName); // Default file name
            dlg2.DefaultExt = Constants.File.TemplateDatabaseFileExtension; // Default file extension
            dlg2.Filter = "Database Files (" + Constants.File.TemplateDatabaseFileExtension + ")|*" + Constants.File.TemplateDatabaseFileExtension; // Filter files by extension 


            result = dlg2.ShowDialog(); // Show open file dialog box
            if (result == true)         // Process open file dialog box results 
            {
                SaveDbBackup();
                if (File.Exists(dlg2.FileName))     // Overwrite the file if it exists
                {
                    File.Delete(dlg2.FileName);
                }
                // Open document 
                dbFileName = dlg2.FileName;
            }

            // Start with the default layout of the data template
            InitializeDataGrid(dbFileName, Constants.Database.TemplateTable);

            // Now convert the code template file into a Data Template, overwriting values and adding rows as required
            Mouse.OverrideCursor = Cursors.Wait;
            List<string> error_messages = new List<string>();
            CodeTemplateImporter importer = new CodeTemplateImporter();
            this.templateTable = importer.Convert(this, codeTemplateFileName, this.templateTable, ref error_messages);
            this.generateControlsAndSpreadsheet = true;
            UpdateDBFull();
            ReInitializeDataGrid(dbFileName, Constants.Database.TemplateTable);
            Mouse.OverrideCursor = null;
            if (error_messages.Count > 0)
            {
                DialogMessageBox dlgMB = new DialogMessageBox();
                dlgMB.IconType = MessageBoxImage.Warning;
                dlgMB.MessageTitle = "One or more Data Labels were problematic";
                dlgMB.MessageProblem = error_messages.Count.ToString() + " of your Data Labels were problematic." + Environment.NewLine + Environment.NewLine +
                              "Data Labels:" + Environment.NewLine +
                              "\u2022 must be unique," + Environment.NewLine +
                              "\u2022 can only contain alphanumeric characters and '_'," + Environment.NewLine +
                              "\u2022 cannot match particular reserved words.";
                dlgMB.MessageResult = "We will automatically repair these Data Labels:";
                foreach (string s in error_messages)
                {
                    dlgMB.MessageSolution += Environment.NewLine + "\u2022 " + s;
                }
                dlgMB.MessageHint = "Check if these are the names you want. You can also rename these corrected Data Labels if you want";
               
                dlgMB.ShowDialog();
            }
        }

        /// <summary>
        /// Exits the application.
        /// </summary>
        private void ExitFileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            TemplateDataGrid.CommitEdit(); //to save any edits that the enter key was not pressed
            Application.Current.Shutdown();
        }

        /// <summary>  Display the Timelapse home page </summary> 
        private void MenuTimelapseWebPage_Click(object sender, RoutedEventArgs e)
        {
            Uri tutorialUri = new Uri("http://saul.cpsc.ucalgary.ca/timelapse/");
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
            Uri tutorialUri = new Uri("http://saul.cpsc.ucalgary.ca/timelapse/uploads/Main/TutorialImageSet.zip");
            Process.Start(new ProcessStartInfo(tutorialUri.AbsoluteUri));
        }

        /// <summary> Send mail to the timelapse mailing list</summary> 
        private void MenuMailToTimelapseMailingList_Click(object sender, RoutedEventArgs e)
        {
            Uri tutorialUri = new Uri("mailto:timelapse-l@mailman.ucalgary.ca");
            Process.Start(new ProcessStartInfo(tutorialUri.AbsoluteUri));
        }
        private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            DlgAboutTimelapseEditor dlg = new DlgAboutTimelapseEditor();
            dlg.Owner = this;
            dlg.ShowDialog();
        }

        #endregion Menu Listeners

        #region SpreadsheetAppearance
        private void GenerateSpreadsheet()
        {
            DataTable sortedview = this.templateTable.Copy();
            sortedview.DefaultView.Sort = Constants.Control.SpreadsheetOrder + " " + "ASC";
            DataTable temptable = sortedview.DefaultView.ToTable();
            this.dgSpreadsheet.Columns.Clear();
            for (int i = 0; i < temptable.Rows.Count; i++)
            {
                DataGridTextColumn dgColumb = new DataGridTextColumn();
                if (System.DBNull.Value != temptable.Rows[i][Constants.Control.DataLabel])
                {
                    dgColumb.Header = (string)temptable.Rows[i][Constants.Control.DataLabel];
                    this.dgSpreadsheet.Columns.Add(dgColumb);
                }
            }
        }

        private void ColumnReordered(object sender, DataGridColumnEventArgs e)
        {
            DataGrid dg = (DataGrid)sender;
            Dictionary<string, int> pairs = new Dictionary<string, int>();
            for (int i = 0; i < dg.Columns.Count; i++)
            {
                string header = dg.Columns[i].Header.ToString();
                int row_index = findRow(header);
                int new_position = dg.Columns[i].DisplayIndex + 1;
                pairs.Add(header, new_position);
            }

            for (int i = 0; i < this.templateTable.Rows.Count; i++)
            {
                string data_label = (string)this.templateTable.Rows[i][Constants.Control.DataLabel];
                int new_value = pairs[data_label];
                this.templateTable.Rows[i][Constants.Control.SpreadsheetOrder] = new_value;
            }
        }

        private int findRow(string to_find)
        {
            for (int i = 0; i < this.templateTable.Rows.Count; i++)
            {
                if (to_find.Equals((string)this.templateTable.Rows[i][Constants.Control.DataLabel]))
                    return i;
            }
            return -1;
        }
        #endregion

        #region Dragging and Dropping of Controls to Reorder them
        private void sp_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.Source == this.wp)
            {
            }
            else
            {
                _isDown = true;
                _startPoint = e.GetPosition(this.wp);

            }
        }

        private void sp_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                _isDown = false;
                _isDragging = false;
                if (!(_realDragSource == null))
                    _realDragSource.ReleaseMouseCapture();
            }
            catch { }
        }

        private void sp_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_isDown)
            {
                if ((_isDragging == false) && ((Math.Abs(e.GetPosition(this.wp).X - _startPoint.X) > SystemParameters.MinimumHorizontalDragDistance) ||
                    (Math.Abs(e.GetPosition(this.wp).Y - _startPoint.Y) > SystemParameters.MinimumVerticalDragDistance)))
                {

                    _isDragging = true;
                    _realDragSource = e.Source as UIElement;
                    _realDragSource.CaptureMouse();
                    DragDrop.DoDragDrop(_dummyDragSource, new DataObject("UIElement", e.Source, true), DragDropEffects.Move);
                }
            }
        }

        private void sp_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("UIElement"))
            {
                e.Effects = DragDropEffects.Move;
            }
        }

        private void sp_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("UIElement"))
            {
                UIElement droptarget = e.Source as UIElement;
                int droptargetIndex = -1, i = 0;
                foreach (UIElement element in this.wp.Children)
                {

                    if (element.Equals(droptarget))
                    {
                        droptargetIndex = i;
                        break;
                    }
                    else //Check if its a stack panel, and if so check to see if its children are the drop target
                    {
                        StackPanel tsp = element as StackPanel;
                        if (null != tsp) // Its a stackpanel
                        {
                            // Check the children...
                            foreach (UIElement subelement in tsp.Children)
                            {
                                if (subelement.Equals(droptarget))
                                {
                                    droptargetIndex = i;
                                    break;
                                }
                            }
                        }
                    }
                    i++;
                }
                if (droptargetIndex != -1)
                {
                    StackPanel tsp = _realDragSource as StackPanel;
                    if (null == tsp)
                    {
                        StackPanel parent = FindVisualParent<StackPanel>(_realDragSource);
                        _realDragSource = parent;
                    }
                    this.wp.Children.Remove(_realDragSource);
                    this.wp.Children.Insert(droptargetIndex, _realDragSource);
                    this.ControlsWereReordered();
                }

                _isDown = false;
                _isDragging = false;
                _realDragSource.ReleaseMouseCapture();
            }
        }

        public static T FindVisualParent<T>(UIElement element) where T : UIElement
        {
            UIElement parent = element;
            while (parent != null)
            {
                T correctlyTyped = parent as T;
                if (correctlyTyped != null)
                {
                    return correctlyTyped;
                }
                parent = VisualTreeHelper.GetParent(parent) as UIElement;
            }
            return null;
        }

        private void ControlsWereReordered()
        {
            StackPanel sp;
            Dictionary<string, int> pairs = new Dictionary<string, int>();
            int idx = 1;
            string data_label;

            foreach (UIElement element in this.wp.Children)
            {
                sp = element as StackPanel;
                if (sp == null) continue;
                pairs.Add((string)sp.Tag, idx);
                idx++;
            }

            Int64 id;
            for (int i = 0; i < this.templateTable.Rows.Count; i++)
            {
                data_label = (string)this.templateTable.Rows[i][Constants.Control.DataLabel];
                id = (Int64)this.templateTable.Rows[i][Constants.DatabaseColumn.ID];
                int new_value = pairs[data_label];
                DataRow foundRow = this.templateTable.Rows.Find(id);
                foundRow[Constants.Control.ControlOrder] = new_value;
            }
            
            DataTable tempTable = db.GetDataTableFromSelect(Constants.Sql.SelectStarFrom + Constants.Database.TemplateTable + EditorConstant.Sql.ByControlSortOrder);
            ControlGeneration.GenerateControls(this, this.wp, tempTable); // A contorted to make sure it updates itself
        }
        #endregion

        #region UI Visibility
        private void InitializeUI()
        {
            this.HelpText.Text = "Type directly in the white fields to edit them. Gray fields are not editable." + Environment.NewLine +
                "List items: create  menu items by typing. Click 'v' to view the menu, and raise each item's context menu to re-order its position.";
            this.HelpDocument.Visibility = Visibility.Collapsed;
            this.HelpText.Visibility = Visibility.Visible;
            this.TemplateDataGrid.Visibility = Visibility.Visible;
            this.RowControls.Visibility = Visibility.Visible;
            this.TextMessage1.Visibility = Visibility.Visible;
            this.OtherGrids.Visibility = Visibility.Visible;
            this.NewFileMenuItem.IsEnabled = false;
            this.OpenFileMenuItem.IsEnabled = false;
            this.ConvertFileMenuItem.IsEnabled = false;
            this.ViewMenu.IsEnabled = true;

        }
        #endregion
        #region Helper Methods
        /// <summary>
        /// Helper method that creates a database backup. Used when performing file menu options.
        /// </summary>
        public void SaveDbBackup()
        {
            if (!String.IsNullOrEmpty(dbFileName))
            {
                string backupPath = Path.GetDirectoryName(dbFileName) + "\\"
                    + "(backup)"
                    + Path.GetFileNameWithoutExtension(dbFileName)
                    + Path.GetExtension(dbFileName);
                File.Copy(dbFileName, backupPath, true);
            }
        }
        #endregion Helper Methods

    }

    #region Converter Classes
    /// <summary>
    /// Converter for ComboBox. Turns the CSV string into an array
    /// Also adds the edit item element. 
    /// Done here, we don't have to ever check for it when performing delete/edit/add (surprises/scares me a bit)
    /// </summary>
    public class ListBoxDBOutputConverter : IValueConverter
    {

        CsvHelperMethods h = new CsvHelperMethods();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string newList = CsvHelperMethods.ConvertLineBreaksToBars(value as string);
            //Debug.Print ("in ListBoxDBOutput Converter, Convert " + "=" + newList + "=");
            return newList;
        }

        //This does nothing, but it has to be here.
        //Kinda surprised I don't need it for the binding... but I don't.
        public object ConvertBack(object value, Type targetType,
        object parameter, CultureInfo culture)
        {
            //Debug.Print ("in ListBoxDBOutput Converter, Convert Back");
            return null;
        }
    }


    /// <summary>
    /// Converter for CellTextBlock. Removes spaces from beginning and end of string.
    /// </summary>
    public class CellTextBlockConverter : IValueConverter
    {
        public object Convert(object value, Type targetType,
        object parameter, CultureInfo culture)
        {
            return value;
        }

        public object ConvertBack(object value, Type targetType,
        object parameter, CultureInfo culture)
        {
            string valString = value as string;
            if (!string.IsNullOrEmpty(valString))
            {
                return valString.Trim();
            }
            return String.Empty;
        }
    }

    /// <summary>
    /// Converter for CellListButton. Doesn't do anything yet...
    /// </summary>
    public class CellListButtonConverter : IValueConverter
    {
        public object Convert(object value, Type targetType,
        object parameter, CultureInfo culture)
        {
            return value;
        }

        public object ConvertBack(object value, Type targetType,
        object parameter, CultureInfo culture)
        {
            string valString = value as string;
            if (!string.IsNullOrEmpty(valString))
            {
                return valString.Trim();
            }
            return String.Empty;
        }
    }
    #endregion Converters

    #region ControlGeneration
    // This static class generates controls in the provided wrap panel based upon the information in the data grid templateTable
    // It is meant to roughly approximate what the controls will look like in the user interface
    public class ControlGeneration
    {
        static public void GenerateControls(Window win, WrapPanel wp, DataTable tempTable)
        {
            const string EXAMPLE_DATE = "28-Dec-2014";
            const string EXAMPLE_TIME = "04:00 PM";

            wp.Children.Clear();

            for (int i = 0; i < tempTable.Rows.Count; i++)
            {
                DataRow row = tempTable.Rows[i];
                string type = row[Constants.DatabaseColumn.Type].ToString();
                string defaultValue = row[Constants.Control.DefaultValue].ToString();
                string label = row[Constants.Control.Label].ToString();
                string datalabel = row[Constants.Control.DataLabel].ToString();
                string tooltip = row[Constants.Control.Tooltip].ToString();
                string width = row[Constants.Control.TextBoxWidth].ToString();
                string visiblity = row[Constants.Control.Visible].ToString();
                string list = row[Constants.Control.List].ToString();

                int iwidth = (width == String.Empty) ? 0 : Convert.ToInt32(width);

                bool bvisiblity = (visiblity == "true" || visiblity == "True") ? true : false;

                StackPanel sp = null;

                if (type == Constants.DatabaseColumn.Date && defaultValue == String.Empty)
                {
                    defaultValue = EXAMPLE_DATE;
                }
                else if (type == Constants.DatabaseColumn.Time && defaultValue == String.Empty)
                {
                    defaultValue = EXAMPLE_TIME;
                }

                if (type == Constants.DatabaseColumn.File || type == Constants.DatabaseColumn.Folder || type == Constants.DatabaseColumn.Date || type == Constants.DatabaseColumn.Time || type == Constants.Control.Note)
                {
                    Label labelctl = CreateLabel(win, label, tooltip);
                    TextBox txtbox = CreateTextBox(win, defaultValue, tooltip, iwidth);
                    sp = CreateStackPanel(win, labelctl, txtbox);
                }
                else if (type == Constants.Control.Counter)
                {
                    RadioButton rb = CreateRadioButton(win, label, tooltip);
                    TextBox txtbox = CreateTextBox(win, defaultValue, tooltip, iwidth);
                    sp = CreateStackPanel(win, rb, txtbox);
                }
                else if (type == Constants.Control.Flag || type == Constants.DatabaseColumn.DeleteFlag)
                {
                    Label labelctl = CreateLabel(win, label, tooltip);
                    CheckBox flag = CreateFlag(win, String.Empty, tooltip);
                    flag.IsChecked = (defaultValue == "true") ? true : false;
                    //TextBox txtbox = CreateTextBox(win, defaultValue, tooltip, iwidth);
                    sp = CreateStackPanel(win, labelctl, flag);
                }
                else if (type == Constants.Control.FixedChoice || type == Constants.DatabaseColumn.ImageQuality)
                {
                    Label labelctl = CreateLabel(win, label, tooltip);
                    ComboBox combobox = CreateComboBox(win, list, tooltip, iwidth);
                    sp = CreateStackPanel(win, labelctl, combobox);
                }

                if (sp != null)
                {
                    sp.Tag = datalabel;
                    //Debug.Print("sp.Tag = " + datalabel);
                    wp.Children.Add(sp);
                }
                if (true != bvisiblity)
                {
                    sp.Visibility = Visibility.Collapsed;
                }
            }
        }

        // Returns a stack panel containing two controls
        // The stack panel ensures that controls are layed out as a single unit with certain spatial characteristcs 
        // i.e.,  a given height, right margin, where contents will not be broken durring (say) panel wrapping
        static public StackPanel CreateStackPanel(Window win, Control control1, Control control2)
        {
            StackPanel sp = new StackPanel();

            // Delete this or comment it out
            //if (this.useColor) sp.Background = Brushes.Red;   // Color red to check spacings

            // Add controls to the stack panel
            sp.Children.Add(control1);
            if (control2 != null)
                sp.Children.Add(control2);

            // Its look is dictated by this style
            Style style = win.FindResource("StackPanelCodeBar") as Style;
            sp.Style = style;
            return sp;
        }

        static public Label CreateLabel(Window win, string labeltxt, string tooltiptxt)
        {
            Label label = new Label();

            // Delete this or comment it out
            //if (this.useColor) label.Background = Brushes.Orange;  // Color  to check spacings

            // Configure it
            label.Content = labeltxt;
            label.ToolTip = tooltiptxt;

            // Its look is dictated by this style
            Style style = win.FindResource("LabelCodeBar") as Style;
            label.Style = style;

            return label;
        }

        static public TextBox CreateTextBox(Window win, string textboxtxt, string tooltiptxt, int width)
        {
            TextBox txtbox = new TextBox();

            // Delete this or comment it out
            //if (this.useColor) txtbox.Background = Brushes.Gold;  // Color  to check spacings

            // Configure the Textbox
            txtbox.Width = width;
            txtbox.Text = textboxtxt;
            txtbox.ToolTip = tooltiptxt;

            // Its look is dictated by this style
            Style style = win.FindResource("TextBoxCodeBar") as Style;
            txtbox.Style = style;

            return txtbox;
        }

        static public RadioButton CreateRadioButton(Window win, string labeltxt, string tooltiptxt)
        {
            RadioButton radiobtn = new RadioButton();

            // Delete this or comment it out
            //if (this.useColor) radiobtn.Background = Brushes.White;  // Color  to check spacings

            // Configure the Radio Button
            radiobtn.GroupName = "A";
            radiobtn.Content = labeltxt;
            radiobtn.ToolTip = tooltiptxt;

            // Its look is dictated by this style
            Style style = win.FindResource("RadioButtonCodeBar") as Style;
            radiobtn.Style = style;

            return radiobtn;
        }

        static public CheckBox CreateFlag(Window win, string labeltxt, string tooltiptxt)
        {
            CheckBox flagbtn = new CheckBox();

            // Delete this or comment it out
            //if (this.useColor) radiobtn.Background = Brushes.White;  // Color  to check spacings

            // Configure the Checkbox 
            flagbtn.Content = labeltxt;
            flagbtn.Visibility = Visibility.Visible;
            flagbtn.ToolTip = tooltiptxt;

            // Its look is dictated by this style
            Style style = win.FindResource("FlagCodeBar") as Style;
            flagbtn.Style = style;

            return flagbtn;
        }

        static public ComboBox CreateComboBox(Window win, string list, string tooltiptxt, int width)
        {
            ComboBox combobox = new ComboBox();

            // Configure the ComboBox
            combobox.ToolTip = tooltiptxt;
            combobox.Width = width;
            List<string> result = list.Split(new char[] { '|' }).ToList();
            foreach (string str in result)
            {
                combobox.Items.Add(str.Trim());
            }
            combobox.SelectedIndex = 0;

            // Its look is dictated by this style
            Style style = win.FindResource("ComboBoxCodeBar") as Style;
            combobox.Style = style;
            return combobox;
        }
    }
    #endregion ControlGeneration

    public class Extensions
    {
        public static readonly DependencyProperty ChoiceListProperty =
            DependencyProperty.RegisterAttached("ChoiceList", typeof(string), typeof(Extensions), new PropertyMetadata(default(string)));

        public static void SetChoiceList(UIElement element, string value)
        {
            element.SetValue(ChoiceListProperty, value);
        }

        public static string GetChoiceList(UIElement element)
        {
            return (string)element.GetValue(ChoiceListProperty);
        }
    }
}