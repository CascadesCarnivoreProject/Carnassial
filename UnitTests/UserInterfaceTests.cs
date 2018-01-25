using Carnassial.Command;
using Carnassial.Control;
using Carnassial.Data;
using Carnassial.Dialog;
using Carnassial.Editor;
using Carnassial.Editor.Dialog;
using Carnassial.Images;
using Carnassial.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Threading;
using MessageBox = Carnassial.Dialog.MessageBox;

namespace Carnassial.UnitTests
{
    [TestClass]
    public class UserInterfaceTests : CarnassialTest
    {
        private static App App;

        public UserInterfaceTests()
        {
            this.EnsureTestClassSubdirectory();
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            UserInterfaceTests.App.Shutdown();
        }
        
        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            // Carnassial and the editor need an application instance to be created to load resources from
            // WPF allows only one Application per app domain, so make instance persistent so it can be reused across multiple Carnassial and editor window
            // lifetimes.  This works because Carnassial and the editor use virtually identical styling, allowing the editor to consume Carnassial styles
            // with negligible effect on test coverage.
            UserInterfaceTests.App = new App() { ShutdownMode = ShutdownMode.OnExplicitShutdown };

            ResourceDictionary resourceDictionary = (ResourceDictionary)Application.LoadComponent(new Uri("/Carnassial;component/CarnassialWindowStyle.xaml", UriKind.Relative));
            foreach (object key in resourceDictionary.Keys)
            {
                Application.Current.Resources.Add(key, resourceDictionary[key]);
            }
        }

        [TestMethod]
        public void Carnassial()
        {
            // open, do nothing, close
            using (CarnassialWindow carnassial = new CarnassialWindow())
            {
                carnassial.Show();
                this.WaitForRenderingComplete();
                carnassial.Close();
                this.WaitForRenderingComplete();
            }

            // create template database and remove any image database from previous test executions
            string templateDatabaseFilePath;
            using (TemplateDatabase templateDatabase = this.CloneTemplateDatabase(TestConstant.File.DefaultTemplateDatabaseFileName))
            {
                templateDatabaseFilePath = templateDatabase.FilePath;
            }

            string fileDatabaseFilePath = Path.Combine(Path.GetDirectoryName(templateDatabaseFilePath), Path.GetFileNameWithoutExtension(templateDatabaseFilePath) + Constant.File.FileDatabaseFileExtension);
            if (File.Exists(fileDatabaseFilePath))
            {
                File.Delete(fileDatabaseFilePath);
            }

            // open, load database by scanning folder, move through images, close
            // The threading model for this is somewhat involved.  The test thread is the UI thread and therefore must drive the dispatcher.  This means the test
            // thread locks into UI message pumping when modal dialog is displayed, such as when loading files from a directory pops the file count summary upon 
            // completion.  The test must therefore spin up a separate thread to close the dialogs and allow the main test thread to return from the dispatcher 
            // and resume test execution.  If something jams up on the dialog handler thread Visual Studio may still consider the test running when also attached 
            // as a debugger even if the test thread has completed.
            // See remarks in CreateReuseControlsAndPropagate() regarding lock.
            lock (UserInterfaceTests.App)
            {
                using (CarnassialWindow carnassial = new CarnassialWindow())
                {
                    // show main window
                    carnassial.Show();
                    this.WaitForRenderingComplete();

                    // start thread for handling file dialogs
                    CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
                    Task fileCountsDismissal = Task.Run(() =>
                    {
                        AutomationElement carnassialAutomation = AutomationElement.RootElement.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, TestConstant.CarnassialAutomationID));
                        if (this.TryFindDialogOkButton(carnassialAutomation, cancellationTokenSource.Token, TestConstant.FileCountsAutomationID, out InvokePattern fileCountsOkButton))
                        {
                            fileCountsOkButton.Invoke();
                        }
                    }, cancellationTokenSource.Token);

                    // import files from directory
                    Task<bool> loadFolder = null;
                    Dispatcher.CurrentDispatcher.Invoke(() =>
                    {
                        loadFolder = carnassial.TryOpenTemplateAndFileDatabaseAsync(templateDatabaseFilePath);
                    });
                    this.WaitForFolderLoadComplete(loadFolder);

                    // verify import succeeded
                    Assert.IsTrue(carnassial.DataHandler.FileDatabase.FilePath == fileDatabaseFilePath);
                    Assert.IsTrue(carnassial.DataHandler.FileDatabase.CurrentlySelectedFileCount == 2);
                    Assert.IsNotNull(carnassial.DataHandler.ImageCache.Current);

                    // verify forward and backward moves of the displayed file
                    // The template is set for defaulting to file ID 1.  With two files in the image set ID 1 can be either the first or last image in the set depending
                    // on whether files are sorted by date.  Set the direction to move so that both move invocations change the displayed file.
                    bool moveDirection = carnassial.DataHandler.ImageCache.CurrentRow == 0;
                    DispatcherOperation<Task> initialMove = carnassial.Dispatcher.InvokeAsync(async () =>
                    {
                        await carnassial.ShowFileWithoutSliderCallbackAsync(moveDirection, ModifierKeys.None);
                        await carnassial.ShowFileWithoutSliderCallbackAsync(moveDirection, ModifierKeys.None);
                    });
                    this.WaitForRenderingComplete();

                    moveDirection = !moveDirection;
                    DispatcherOperation<Task> returnMove = carnassial.Dispatcher.InvokeAsync(async () =>
                    {
                        await carnassial.ShowFileWithoutSliderCallbackAsync(moveDirection, ModifierKeys.None);
                        await carnassial.ShowFileWithoutSliderCallbackAsync(moveDirection, ModifierKeys.None);
                    });
                    this.WaitForRenderingComplete();

                    // verify undo/redo of navigation
                    Assert.IsFalse(carnassial.MenuEditRedo.IsEnabled);
                    Assert.IsTrue(carnassial.MenuEditUndo.IsEnabled);
                    carnassial.Dispatcher.Invoke(() => { carnassial.MenuEditUndo_Click(null, null); });
                    this.WaitForRenderingComplete();
                    Assert.IsTrue(carnassial.DataHandler.ImageCache.CurrentRow == (moveDirection ? 0 : 1));

                    Assert.IsTrue(carnassial.MenuEditRedo.IsEnabled);
                    Assert.IsTrue(carnassial.MenuEditUndo.IsEnabled);
                    carnassial.Dispatcher.Invoke(() => { carnassial.MenuEditRedo_Click(null, null); });
                    this.WaitForRenderingComplete();
                    Assert.IsTrue(carnassial.DataHandler.ImageCache.CurrentRow == (moveDirection ? 1 : 0));

                    // file ordering
                    bool originalOrderFilesByDateTime = carnassial.State.OrderFilesByDateTime;
                    FileOrdering orderingCommand = new FileOrdering(carnassial.DataHandler.ImageCache);
                    Assert.IsTrue(orderingCommand.CanExecute(carnassial));
                    Assert.IsFalse(orderingCommand.CanUndo(carnassial));
                    Assert.IsTrue(orderingCommand.IsAsync);
                    Assert.IsFalse(orderingCommand.IsExecuted);
                    DispatcherOperation<Task> changeOrdering = carnassial.Dispatcher.InvokeAsync(async () =>
                    {
                        await orderingCommand.ExecuteAsync(carnassial);
                    });
                    this.WaitForRenderingComplete();
                    Assert.IsTrue(carnassial.DataHandler.FileDatabase.OrderFilesByDateTime != originalOrderFilesByDateTime);
                    Assert.IsTrue(carnassial.State.OrderFilesByDateTime != originalOrderFilesByDateTime);
                    Assert.IsFalse(orderingCommand.CanExecute(carnassial));
                    Assert.IsTrue(orderingCommand.CanUndo(carnassial));

                    DispatcherOperation<Task> undoOrdering = carnassial.Dispatcher.InvokeAsync(async () =>
                    {
                        await orderingCommand.UndoAsync(carnassial);
                    });
                    this.WaitForRenderingComplete();
                    Assert.IsTrue(carnassial.DataHandler.FileDatabase.OrderFilesByDateTime == originalOrderFilesByDateTime);
                    Assert.IsTrue(carnassial.State.OrderFilesByDateTime == originalOrderFilesByDateTime);
                    Assert.IsTrue(orderingCommand.CanExecute(carnassial));
                    Assert.IsFalse(orderingCommand.CanUndo(carnassial));

                    // sanity check file selection
                    DispatcherOperation<Task> selectAll = carnassial.Dispatcher.InvokeAsync(async () =>
                    {
                        await carnassial.SelectFilesAndShowFileAsync(FileSelection.All);
                    });
                    this.WaitForRenderingComplete();
                    Assert.IsTrue(carnassial.DataHandler.FileDatabase.ImageSet.FileSelection == FileSelection.All);
                    Assert.IsFalse(carnassial.MenuEditRedo.IsEnabled);
                    Assert.IsTrue(carnassial.MenuEditUndo.IsEnabled);

                    carnassial.Dispatcher.Invoke(() => { carnassial.MenuEditUndo_Click(null, null); });
                    this.WaitForRenderingComplete();
                    Assert.IsTrue(carnassial.DataHandler.FileDatabase.ImageSet.FileSelection == FileSelection.All);

                    carnassial.Dispatcher.Invoke(() => { carnassial.MenuEditRedo_Click(null, null); });
                    this.WaitForRenderingComplete();
                    Assert.IsTrue(carnassial.DataHandler.FileDatabase.ImageSet.FileSelection == FileSelection.All);

                    // file edit commands on notes
                    Assert.IsTrue(carnassial.State.CurrentFileSnapshot.Count == TestConstant.DefaultFileDataColumns.Count - 1);
                    string originalNote0Value = (string)carnassial.DataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.Note0];
                    string newNote0Value = "note 0 new value";
                    string originalNote3Value = (string)carnassial.DataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.Note3];
                    string newNote3Value = "note 3 new value";
                    Dictionary<string, object> multipleChanges = carnassial.DataHandler.ImageCache.Current.GetValues();
                    multipleChanges[TestConstant.DefaultDatabaseColumn.Note0] = newNote0Value;
                    multipleChanges[TestConstant.DefaultDatabaseColumn.Note3] = newNote3Value;
                    Assert.IsTrue(multipleChanges.Count == TestConstant.DefaultFileDataColumns.Count - 1);
                    FileMultipleFieldChange multipleEdit = new FileMultipleFieldChange(carnassial.DataHandler.ImageCache, multipleChanges);
                    Assert.IsTrue(multipleEdit.CanExecute(carnassial));
                    Assert.IsFalse(multipleEdit.CanUndo(carnassial));
                    Assert.IsTrue(multipleEdit.Changes == 2);
                    Assert.IsFalse(multipleEdit.IsAsync);
                    Assert.IsFalse(multipleEdit.IsExecuted);
                    Assert.IsTrue((string)carnassial.DataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.Note0] == originalNote0Value);
                    Assert.IsTrue((string)carnassial.DataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.Note3] == originalNote3Value);
                    Assert.IsTrue(carnassial.State.CurrentFileSnapshot.Count == TestConstant.DefaultFileDataColumns.Count - 1);
                    Assert.IsTrue((string)carnassial.State.CurrentFileSnapshot[TestConstant.DefaultDatabaseColumn.Note0] == originalNote0Value);
                    Assert.IsTrue((string)carnassial.State.CurrentFileSnapshot[TestConstant.DefaultDatabaseColumn.Note3] == originalNote3Value);

                    multipleEdit.Execute(carnassial);
                    Assert.IsFalse(multipleEdit.CanExecute(carnassial));
                    Assert.IsTrue(multipleEdit.CanUndo(carnassial));
                    Assert.IsTrue(multipleEdit.Changes == 2);
                    Assert.IsTrue(multipleEdit.IsExecuted);
                    Assert.IsTrue((string)carnassial.DataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.Note0] == newNote0Value);
                    Assert.IsTrue((string)carnassial.DataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.Note3] == newNote3Value);
                    Assert.IsTrue(carnassial.State.CurrentFileSnapshot.Count == TestConstant.DefaultFileDataColumns.Count - 1);
                    Assert.IsTrue((string)carnassial.State.CurrentFileSnapshot[TestConstant.DefaultDatabaseColumn.Note0] == newNote0Value);
                    Assert.IsTrue((string)carnassial.State.CurrentFileSnapshot[TestConstant.DefaultDatabaseColumn.Note3] == newNote3Value);
                    this.WaitForRenderingComplete();

                    multipleEdit.Undo(carnassial);
                    Assert.IsTrue(multipleEdit.CanExecute(carnassial));
                    Assert.IsFalse(multipleEdit.CanUndo(carnassial));
                    Assert.IsTrue(multipleEdit.Changes == 2);
                    Assert.IsFalse(multipleEdit.IsExecuted);
                    Assert.IsTrue((string)carnassial.DataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.Note0] == originalNote0Value);
                    Assert.IsTrue((string)carnassial.DataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.Note3] == originalNote3Value);
                    Assert.IsTrue((string)carnassial.State.CurrentFileSnapshot[TestConstant.DefaultDatabaseColumn.Note0] == originalNote0Value);
                    Assert.IsTrue((string)carnassial.State.CurrentFileSnapshot[TestConstant.DefaultDatabaseColumn.Note3] == originalNote3Value);
                    this.WaitForRenderingComplete();

                    DispatcherOperation<Task> showFile = carnassial.Dispatcher.InvokeAsync(async () =>
                    {
                    // move to the other file so change no longer matches current file and can't be exected or undone
                    int otherFileIndex = carnassial.DataHandler.ImageCache.CurrentRow == 0 ? 1 : 0;
                        await carnassial.ShowFileAsync(otherFileIndex, true);
                    });
                    this.WaitForRenderingComplete();
                    Assert.IsFalse(multipleEdit.CanExecute(carnassial));
                    Assert.IsFalse(multipleEdit.CanUndo(carnassial));

                    string previousNoteValue = (string)carnassial.DataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel];
                    string newNoteValue = "note single change value";
                    Dictionary<string, object> singleChangeAsMultipleChanges = carnassial.DataHandler.ImageCache.Current.GetValues();
                    singleChangeAsMultipleChanges[TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel] = newNoteValue;
                    FileMultipleFieldChange singleEditAsMultiple = new FileMultipleFieldChange(carnassial.DataHandler.ImageCache, singleChangeAsMultipleChanges);
                    Assert.IsTrue(singleEditAsMultiple.Changes == 1);
                    FileSingleFieldChange singleEdit = singleEditAsMultiple.AsSingleChange();
                    Assert.IsTrue(singleEdit.CanExecute(carnassial));
                    Assert.IsFalse(singleEdit.CanUndo(carnassial));
                    Assert.IsTrue(singleEdit.DataLabel == TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel);
                    Assert.IsFalse(singleEdit.IsAsync);
                    Assert.IsFalse(singleEdit.IsExecuted);
                    Assert.IsTrue((string)singleEdit.NewValue == newNoteValue);
                    Assert.IsTrue((string)singleEdit.PreviousValue == previousNoteValue);
                    Assert.IsTrue(singleEdit.PropertyName == TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel);
                    this.WaitForRenderingComplete();

                    singleEdit.Execute(carnassial);
                    Assert.IsFalse(singleEdit.CanExecute(carnassial));
                    Assert.IsTrue(singleEdit.CanUndo(carnassial));
                    Assert.IsTrue(singleEdit.DataLabel == TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel);
                    Assert.IsFalse(singleEdit.IsAsync);
                    Assert.IsTrue(singleEdit.IsExecuted);
                    Assert.IsTrue((string)singleEdit.NewValue == newNoteValue);
                    Assert.IsTrue((string)singleEdit.PreviousValue == previousNoteValue);
                    Assert.IsTrue(singleEdit.PropertyName == TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel);
                    Assert.IsTrue((string)carnassial.DataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel] == newNoteValue);
                    Assert.IsTrue((string)carnassial.State.CurrentFileSnapshot[TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel] == newNoteValue);
                    this.WaitForRenderingComplete();

                    singleEdit.Undo(carnassial);
                    Assert.IsTrue(singleEdit.CanExecute(carnassial));
                    Assert.IsFalse(singleEdit.CanUndo(carnassial));
                    Assert.IsTrue(singleEdit.DataLabel == TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel);
                    Assert.IsFalse(singleEdit.IsAsync);
                    Assert.IsFalse(singleEdit.IsExecuted);
                    Assert.IsTrue((string)singleEdit.NewValue == newNoteValue);
                    Assert.IsTrue((string)singleEdit.PreviousValue == previousNoteValue);
                    Assert.IsTrue(singleEdit.PropertyName == TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel);
                    Assert.IsTrue((string)carnassial.DataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel] == previousNoteValue);
                    Assert.IsTrue((string)carnassial.State.CurrentFileSnapshot[TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel] == previousNoteValue);
                    this.WaitForRenderingComplete();

                    // choice change
                    string previousChoiceValue = (string)carnassial.DataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel];
                    string newChoiceValue = "choice changed value";
                    FileSingleFieldChange choiceEdit = new FileSingleFieldChange(carnassial.DataHandler.ImageCache.Current.ID, TestConstant.DefaultDatabaseColumn.Choice0, TestConstant.DefaultDatabaseColumn.Choice0, previousChoiceValue, newChoiceValue, false);
                    choiceEdit.Execute(carnassial);
                    Assert.IsTrue((string)carnassial.DataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.Choice0] == newChoiceValue);
                    choiceEdit.Undo(carnassial);
                    Assert.IsTrue((string)carnassial.DataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.Choice0] == previousChoiceValue);

                    // flag change
                    string previousFlagValue = (string)carnassial.DataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.Flag0];
                    string newFlagValue = String.Equals(previousFlagValue, Boolean.TrueString) ? Boolean.TrueString : Boolean.FalseString;
                    FileSingleFieldChange flagEdit = new FileSingleFieldChange(carnassial.DataHandler.ImageCache.Current.ID, TestConstant.DefaultDatabaseColumn.Flag0, TestConstant.DefaultDatabaseColumn.Flag0, previousFlagValue, newFlagValue, false);
                    flagEdit.Execute(carnassial);
                    Assert.IsTrue((string)carnassial.DataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.Flag0] == newFlagValue.ToString());
                    flagEdit.Undo(carnassial);
                    Assert.IsTrue((string)carnassial.DataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.Flag0] == previousFlagValue.ToString());

                    // marker insertion and removal
                    string previousCounterValue = (string)carnassial.DataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.Counter0];
                    string newCounterValue = (Int32.Parse(previousCounterValue) + 1).ToString();
                    Marker newMarker = new Marker(TestConstant.DefaultDatabaseColumn.Counter0, new Point()) { LabelShownPreviously = false, ShowLabel = true };
                    MarkerCreatedOrDeletedEventArgs markerEvent = new MarkerCreatedOrDeletedEventArgs(newMarker, true);
                    FileMarkerChange counterEdit = new FileMarkerChange(carnassial.DataHandler.ImageCache.Current.ID, markerEvent);
                    counterEdit.Execute(carnassial);
                    Assert.IsTrue((string)carnassial.DataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.Counter0] == newCounterValue.ToString());
                    counterEdit.Undo(carnassial);
                    Assert.IsTrue((string)carnassial.DataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.Counter0] == previousCounterValue.ToString());

                    // custom selection edit
                    Data.CustomSelection currentSelection = carnassial.DataHandler.FileDatabase.CustomSelection;
                    Data.CustomSelection undoSelection = new Data.CustomSelection(currentSelection);
                    CustomSelectionChange customSelectionEdit = new CustomSelectionChange(undoSelection, currentSelection);
                    Assert.IsFalse(customSelectionEdit.CanExecute(carnassial));
                    Assert.IsTrue(customSelectionEdit.CanUndo(carnassial));
                    Assert.IsFalse(customSelectionEdit.IsAsync);
                    Assert.IsTrue(customSelectionEdit.IsExecuted);

                    Assert.IsFalse(customSelectionEdit.HasChanges());
                    undoSelection.SearchTerms[0].UseForSearching = !undoSelection.SearchTerms[0].UseForSearching;
                    Assert.IsTrue(customSelectionEdit.HasChanges());

                    customSelectionEdit.Undo(carnassial);
                    Assert.IsTrue(Object.ReferenceEquals(undoSelection, carnassial.DataHandler.FileDatabase.CustomSelection));
                    Assert.IsTrue(customSelectionEdit.CanExecute(carnassial));
                    Assert.IsFalse(customSelectionEdit.CanUndo(carnassial));
                    Assert.IsFalse(customSelectionEdit.IsExecuted);

                    customSelectionEdit.Execute(carnassial);
                    // CustomSelectionChange..ctor() clones the current selection, so the clone should be in place after execution
                    Assert.IsFalse(Object.ReferenceEquals(currentSelection, carnassial.DataHandler.FileDatabase.CustomSelection));
                    Assert.IsFalse(Object.ReferenceEquals(undoSelection, carnassial.DataHandler.FileDatabase.CustomSelection));
                    Assert.IsFalse(customSelectionEdit.CanExecute(carnassial));
                    Assert.IsTrue(customSelectionEdit.CanUndo(carnassial));
                    Assert.IsTrue(customSelectionEdit.IsExecuted);

                    // verify application exit
                    carnassial.Close();
                    if (cancellationTokenSource.Token.CanBeCanceled)
                    {
                        cancellationTokenSource.Cancel();
                    }
                }

                // open, load existing database, pop dialogs, close
                using (CarnassialWindow carnassial = new CarnassialWindow())
                {
                    carnassial.Show();
                    this.WaitForRenderingComplete();

                    Task<bool> loadFolder = null;
                    Dispatcher.CurrentDispatcher.Invoke(() =>
                    {
                        loadFolder = carnassial.TryOpenTemplateAndFileDatabaseAsync(templateDatabaseFilePath);
                    });
                    this.WaitForFolderLoadComplete(loadFolder);

                    Assert.IsTrue(carnassial.DataHandler.FileDatabase.CurrentlySelectedFileCount > 0);
                    Assert.IsNotNull(carnassial.DataHandler.ImageCache.Current);

                    this.ShowDialog(new About(carnassial));
                    this.ShowDialog(new AdvancedCarnassialOptions(carnassial.State, carnassial.MarkableCanvas, carnassial));
                    this.ShowDialog(new ChooseFileDatabase(new string[] { TestConstant.File.DefaultNewFileDatabaseFileName }, TestConstant.File.DefaultTemplateDatabaseFileName, carnassial));

                    this.ShowDialog(new Dialog.CustomSelection(carnassial.DataHandler.FileDatabase, carnassial));
                    using (DarkImagesThreshold darkThreshold = new DarkImagesThreshold(carnassial.DataHandler.FileDatabase, carnassial.DataHandler.ImageCache.CurrentRow, new CarnassialState(), carnassial))
                    {
                        this.ShowDialog(darkThreshold);
                    }

                    this.ShowDialog(new DateCorrectAmbiguous(carnassial.DataHandler.FileDatabase, carnassial));
                    this.ShowDialog(new DateDaylightSavingsTimeCorrection(carnassial.DataHandler.FileDatabase, carnassial.DataHandler.ImageCache, carnassial));

                    DateTimeFixedCorrection clockSetCorrection = new DateTimeFixedCorrection(carnassial.DataHandler.FileDatabase, carnassial.DataHandler.ImageCache.Current, carnassial);
                    this.ShowDialog(clockSetCorrection);

                    DateTimeLinearCorrection clockDriftCorrection = new DateTimeLinearCorrection(carnassial.DataHandler.FileDatabase, carnassial);
                    Assert.IsTrue(clockDriftCorrection.Abort == (carnassial.DataHandler.ImageCache.Current == null));
                    this.ShowDialog(clockDriftCorrection);

                    this.ShowDialog(new DateTimeRereadFromFiles(carnassial.DataHandler.FileDatabase, carnassial.State.Throttles.GetDesiredIntervalBetweenFileLoadProgress(), carnassial));
                    this.ShowDialog(new DateTimeSetTimeZone(carnassial.DataHandler.FileDatabase, carnassial.DataHandler.ImageCache.Current, carnassial));
                    this.ShowDialog(new FileCountsByQuality(carnassial.DataHandler.FileDatabase.GetFileCountsBySelection(), carnassial));
                    this.ShowDialog(new EditLog(carnassial.DataHandler.FileDatabase.ImageSet.Log, carnassial));

                    this.ShowDialog(new PopulateFieldWithMetadata(carnassial.DataHandler.FileDatabase, carnassial.DataHandler.ImageCache.Current.GetFilePath(carnassial.DataHandler.FileDatabase.FolderPath), carnassial.State.Throttles.GetDesiredIntervalBetweenFileLoadProgress(), carnassial));
                    this.ShowDialog(new RenameFileDatabaseFile(carnassial.DataHandler.FileDatabase.FileName, carnassial));
                    this.ShowDialog(new TemplateSynchronization(carnassial.DataHandler.FileDatabase.ControlSynchronizationIssues, carnassial));

                    MessageBox okMessageBox = this.CreateMessageBox(carnassial, MessageBoxButton.OK, MessageBoxImage.Error);
                    this.ShowDialog(okMessageBox);
                    MessageBox okCancelMessageBox = this.CreateMessageBox(carnassial, MessageBoxButton.OKCancel, MessageBoxImage.Information);
                    this.ShowDialog(okCancelMessageBox);
                    MessageBox yesNoMessageBox = this.CreateMessageBox(carnassial, MessageBoxButton.YesNo, MessageBoxImage.Question);
                    this.ShowDialog(yesNoMessageBox);

                    carnassial.Close();
                }
            }
        }

        private MessageBox CreateMessageBox(Window owner, MessageBoxButton buttonType, MessageBoxImage iconType)
        {
            MessageBox messageBox = new MessageBox("Message box title.", owner, buttonType);
            messageBox.Message.StatusImage = iconType;
            messageBox.Message.Problem = "Problem description.";
            messageBox.Message.Reason = "Explanation of why issue is an issue.";
            messageBox.Message.Solution = "Suggested method for resolving the issue.";
            messageBox.Message.Result = "Current status.";
            messageBox.Message.Hint = "Additional suggestions as to how to resolve the issue.";
            return messageBox;
        }

        [TestMethod]
        public void CreateReuseControlsAndPropagate()
        {
            List<DatabaseExpectations> databaseExpectations = new List<DatabaseExpectations>()
            {
                new DatabaseExpectations()
                {
                    FileName = Constant.File.DefaultFileDatabaseFileName,
                    TemplateDatabaseFileName = TestConstant.File.DefaultTemplateDatabaseFileName,
                    ExpectedColumns = TestConstant.DefaultFileDataColumns,
                    ExpectedControls = TestConstant.DefaultFileDataColumns.Count - 6
                }
            };

            // CreateReuseControlsAndPropagate() needs an app instance in order for DataEntryControls controls to load resources
            // but Carnassial() requires only one .ddb be present in the UI tests subdirectory; lock to exclude concurrent execution
            // of the relative parts of these two tests
            lock (UserInterfaceTests.App)
            {
                foreach (DatabaseExpectations databaseExpectation in databaseExpectations)
                {
                    FileDatabase fileDatabase = this.CreateFileDatabase(databaseExpectation.TemplateDatabaseFileName, databaseExpectation.FileName);
                    DataEntryHandler dataHandler = new DataEntryHandler(fileDatabase);

                    DataEntryControls controls = new DataEntryControls();
                    controls.CreateControls(fileDatabase, dataHandler, (string dataLabel) => { return fileDatabase.GetDistinctValuesInFileDataColumn(dataLabel); });
                    Assert.IsTrue(controls.ControlsByDataLabel.Count == databaseExpectation.ExpectedControls, "Expected {0} controls to be generated but {1} were.", databaseExpectation.ExpectedControls, controls.ControlsByDataLabel.Count);

                    // check copies aren't possible when the image enumerator's not pointing to an image
                    List<DataEntryControl> copyableControls = controls.Controls.Where(control => control.Copyable).ToList();
                    foreach (DataEntryControl control in copyableControls)
                    {
                        Assert.IsFalse(dataHandler.IsCopyForwardPossible(control));
                        Assert.IsFalse(dataHandler.IsCopyFromLastNonEmptyValuePossible(control));
                    }

                    // check only copy forward is possible when enumerator's on first image
                    List<FileExpectations> fileExpectations = this.PopulateDefaultDatabase(fileDatabase);
                    foreach (FileExpectations fileExpectation in fileExpectations)
                    {
                        fileExpectation.RelativePath = "..";
                    }
                    Assert.IsTrue(dataHandler.ImageCache.MoveNext());

                    foreach (DataEntryControl control in copyableControls)
                    {
                        Assert.IsTrue(dataHandler.IsCopyForwardPossible(control));
                        Assert.IsFalse(dataHandler.IsCopyFromLastNonEmptyValuePossible(control));
                    }

                    // check only copy last is possible when enumerator's on last image
                    // check also copy last is not possible if no previous instance of the field has been filled out
                    while (dataHandler.ImageCache.CurrentRow < fileExpectations.Count - 1)
                    {
                        Assert.IsTrue(dataHandler.ImageCache.MoveNext());
                    }

                    foreach (DataEntryControl control in copyableControls)
                    {
                        Assert.IsFalse(dataHandler.IsCopyForwardPossible(control));
                        if (control.DataLabel == TestConstant.CarnivoreDatabaseColumn.Pelage ||
                            control.DataLabel == TestConstant.DefaultDatabaseColumn.ChoiceNotVisible ||
                            control.DataLabel == TestConstant.DefaultDatabaseColumn.ChoiceWithCustomDataLabel ||
                            control.DataLabel == TestConstant.DefaultDatabaseColumn.Choice3 ||
                            control.DataLabel == TestConstant.DefaultDatabaseColumn.Counter3 ||
                            control.DataLabel == TestConstant.DefaultDatabaseColumn.Flag0 ||
                            control.DataLabel == TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel)
                        {
                            Assert.IsFalse(dataHandler.IsCopyFromLastNonEmptyValuePossible(control), control.DataLabel);
                        }
                        else
                        {
                            Assert.IsTrue(dataHandler.IsCopyFromLastNonEmptyValuePossible(control), control.DataLabel);
                        }
                    }

                    // propagation methods not covered due to requirement of UX interaction
                    // dataHandler.CopyForward(control);
                    // dataHandler.CopyFromLastValue(control);
                    // dataHandler.CopyToAll(control);

                    // verify roundtrip of fields subject to copy/paste and analysis assignment
                    // AsDictionary() returns a dictionary with one fewer values than there are columns as the DateTime and UtcOffset
                    // columns are merged to DateTimeOffset.
                    Assert.IsTrue(dataHandler.ImageCache.TryMoveToFile(0));
                    ImageRow firstFile = fileDatabase.Files[0];
                    FileExpectations firstFileExpectations = fileExpectations[0];

                    // DateTime and UtcOffset are merged into DateTime, so there should be one fewer value than there are columns
                    // in the file table
                    Dictionary<string, object> firstFileValuesByPropertyName = firstFile.GetValues();
                    Assert.IsTrue(firstFileValuesByPropertyName.Count == databaseExpectation.ExpectedColumns.Count - 1);
                    foreach (KeyValuePair<string, object> singlePropertyEdit in firstFileValuesByPropertyName)
                    {
                        if (singlePropertyEdit.Key == Constant.DatabaseColumn.ID)
                        {
                            continue;
                        }
                        firstFile[singlePropertyEdit.Key] = singlePropertyEdit.Value;
                    }

                    TimeZoneInfo imageSetTimeZone = fileDatabase.ImageSet.GetTimeZoneInfo();
                    firstFileExpectations.Verify(firstFile, imageSetTimeZone);

                    // verify roundtrip of fields via display string
                    foreach (DataEntryControl control in controls.Controls)
                    {
                        string displayString = firstFile.GetDisplayString(control);
                        control.SetValue(displayString);
                    }

                    firstFileExpectations.Verify(firstFile, imageSetTimeZone);

                    // verify availability of database strings
                    foreach (string dataLabel in databaseExpectation.ExpectedColumns)
                    {
                        string databaseString = firstFile.GetDatabaseString(dataLabel);
                    }

                    // verify counter increment and decrement
                    // UI thread isn't running to perform data binding (and controls aren't visible) so most checks are against the underlying ImageRow.
                    DataEntryCounter counter0 = (DataEntryCounter)controls.ControlsByDataLabel[TestConstant.DefaultDatabaseColumn.Counter0];
                    counter0.DataContext = dataHandler.ImageCache.Current;
                    Assert.IsTrue(counter0.Content == "1");
                    dataHandler.DecrementOrResetCounter(counter0);
                    Assert.IsTrue((string)dataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.Counter0] == "0");
                    dataHandler.DecrementOrResetCounter(counter0);
                    Assert.IsTrue((string)dataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.Counter0] == "0");

                    dataHandler.IncrementOrResetCounter(counter0);
                    Assert.IsTrue((string)dataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.Counter0] == "1");
                    dataHandler.DecrementOrResetCounter(counter0);
                    Assert.IsTrue((string)dataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.Counter0] == "0");

                    dataHandler.IncrementOrResetCounter(counter0);
                    Assert.IsTrue((string)dataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.Counter0] == "1");
                    dataHandler.DecrementOrResetCounter(counter0);
                    Assert.IsTrue((string)dataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.Counter0] == "0");

                    DataEntryCounter counter3 = (DataEntryCounter)controls.ControlsByDataLabel[TestConstant.DefaultDatabaseColumn.Counter3];
                    counter3.DataContext = dataHandler.ImageCache.Current;
                    Assert.IsTrue(counter3.Content == "0");
                    dataHandler.IncrementOrResetCounter(counter0);
                    dataHandler.IncrementOrResetCounter(counter3);
                    dataHandler.IncrementOrResetCounter(counter3);
                    dataHandler.IncrementOrResetCounter(counter0);
                    Assert.IsTrue((string)dataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.Counter0] == "2");
                    Assert.IsTrue((string)dataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.Counter3] == "2");
                    dataHandler.DecrementOrResetCounter(counter0);
                    dataHandler.DecrementOrResetCounter(counter3);
                    dataHandler.DecrementOrResetCounter(counter0);
                    dataHandler.DecrementOrResetCounter(counter3);
                    dataHandler.IncrementOrResetCounter(counter3);
                    dataHandler.IncrementOrResetCounter(counter3);
                    dataHandler.IncrementOrResetCounter(counter3);
                    dataHandler.IncrementOrResetCounter(counter3);
                    dataHandler.DecrementOrResetCounter(counter3);
                    Assert.IsTrue((string)dataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.Counter0] == "0");
                    Assert.IsTrue((string)dataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.Counter3] == "3");

                    fileDatabase.Dispose();
                    File.Delete(fileDatabase.FilePath);
                }
            }
        }

        [TestMethod]
        public void Editor()
        {
            // open, do nothing, close
            EditorWindow editor = new EditorWindow();
            editor.Show();
            this.WaitForRenderingComplete();
            editor.Close();

            // open, create template database, close
            string templateDatabaseFilePath = this.GetUniqueFilePathForTest(TestConstant.File.DefaultNewTemplateDatabaseFileName);
            if (File.Exists(templateDatabaseFilePath))
            {
                File.Delete(templateDatabaseFilePath);
            }
            editor = new EditorWindow();
            editor.Show();
            this.WaitForRenderingComplete();
            PrivateObject editorAccessor = new PrivateObject(editor);
            editorAccessor.Invoke(TestConstant.InitializeDataGridMethodName, templateDatabaseFilePath);
            this.WaitForRenderingComplete();
            editor.Close();

            // open, load existing database, pop dialogs, close
            // InitializeDataGrid() sets the template pane active but without the explicit set in test code the event gets dropped, resulting the EditChoiceList
            // show failing because the UIElement its position is referenced to is not visible.
            editor = new EditorWindow();
            editor.Show();
            this.WaitForRenderingComplete();
            editorAccessor = new PrivateObject(editor);
            editorAccessor.Invoke(TestConstant.InitializeDataGridMethodName, templateDatabaseFilePath);
            this.WaitForRenderingComplete();

            editor.Tabs.SelectedIndex = 1;
            this.WaitForRenderingComplete();

            this.ShowDialog(new AboutEditor(editor));
            TemplateDatabase templateDatabase = (TemplateDatabase)editorAccessor.GetField(TestConstant.EditorTemplateDatabaseFieldName);
            this.ShowDialog(new AdvancedImageSetOptions(templateDatabase, editor));
            this.ShowDialog(new EditChoiceList(editor.TemplateDataGrid, new List<string>() { "Choice0", "Choice1", "Choice2", "Choice3" }, editor));

            editor.Close();
        }

        private void ShowDialog(Window dialog)
        {
            dialog.Loaded += (object sender, RoutedEventArgs eventArgs) => { dialog.Close(); };
            Dispatcher.CurrentDispatcher.InvokeAsync(() =>
            {
                dialog.ShowDialog();
            });
            this.WaitForRenderingComplete();
        }

        private bool TryFindDialogOkButton(AutomationElement parent, CancellationToken cancellationToken, string automationID, out InvokePattern okButtonInvoke)
        {
            okButtonInvoke = null;
            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            AutomationElement dialog = null;
            DateTime startTime = DateTime.UtcNow;
            while ((dialog == null) && (DateTime.UtcNow - startTime < TestConstant.UIElementSearchTimeout))
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(100));
                if (cancellationToken.IsCancellationRequested)
                {
                    return false;
                }
                dialog = parent.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, automationID));
            }
            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }
            Assert.IsNotNull(dialog);

            AutomationElement okButton = dialog.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, TestConstant.OkButtonAutomationID));
            Assert.IsNotNull(okButton);
            okButtonInvoke = (InvokePattern)okButton.GetCurrentPattern(InvokePattern.Pattern);
            return true;
        }

        private void WaitForFolderLoadComplete(Task<bool> loadFolder)
        {
            while (loadFolder.IsCompleted == false)
            {
                this.WaitForRenderingComplete();
            }
            Assert.IsFalse(loadFolder.IsCanceled);
            if (loadFolder.IsFaulted)
            {
                throw loadFolder.Exception;
            }
            Assert.IsTrue(loadFolder.Result);
            this.WaitForRenderingComplete();
        }

        private void WaitForRenderingComplete()
        {
            // make a best effort at letting WPF's render thread(s) catch up
            // from https://msdn.microsoft.com/en-us/library/system.windows.threading.dispatcher.pushframe.aspx
            DispatcherFrame frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new DispatcherOperationCallback((object arg) =>
            {
                ((DispatcherFrame)arg).Continue = false;
                return null;
            }), frame);
            Dispatcher.PushFrame(frame);
        }
    }
}
