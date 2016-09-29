using Carnassial.Controls;
using Carnassial.Database;
using Carnassial.Dialog;
using Carnassial.Editor;
using Carnassial.Editor.Dialog;
using Carnassial.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
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
        public UserInterfaceTests()
        {
            this.EnsureTestClassSubdirectory();
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
            PrivateObject editorAccessor = new PrivateObject(editor);
            editor.Show();
            this.WaitForRenderingComplete();
            editorAccessor.Invoke(TestConstant.InitializeDataGridMethodName, templateDatabaseFilePath);
            this.WaitForRenderingComplete();
            editor.Close();

            // open, load existing database, pop dialogs, close
            // InitializeDataGrid() sets the template pane active but without the explicit set in test code the event gets dropped, resulting the EditChoiceList
            // show failing because the UIElement its position is referenced to is not visible.
            editor = new EditorWindow();
            editor.Show();
            this.WaitForRenderingComplete();
            editorAccessor.Invoke(TestConstant.InitializeDataGridMethodName, templateDatabaseFilePath);
            this.WaitForRenderingComplete();

            editor.TemplatePane.IsActive = true;
            this.WaitForRenderingComplete();

            this.ShowDialog(new AboutEditor(editor));
            this.ShowDialog(new EditChoiceList(editor.TemplateDataGrid, new List<string>() { "Choice0", "Choice1", "Choice2", "Choice3" }, editor));

            editor.Close();
        }

        [TestMethod]
        public void Carnassial()
        {
            // Constants.Images needs to load resources from Carnassial.exe and BitmapFrame.Create() relies on Application.ResourceAssembly to do this
            // for unit tests (or the editor) ResourceAssembly does not get set as Carnassial.exe is the entry point
            if (Application.ResourceAssembly == null)
            {
                Application.ResourceAssembly = typeof(Constants.Images).Assembly;
            }

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
            using (TemplateDatabase templateDatabase = this.CreateTemplateDatabase(TestConstant.File.DefaultTemplateDatabaseFileName))
            {
                templateDatabaseFilePath = templateDatabase.FilePath;
            }

            string imageDatabaseFilePath = Path.Combine(Path.GetDirectoryName(templateDatabaseFilePath), Path.GetFileNameWithoutExtension(templateDatabaseFilePath) + Constants.File.ImageDatabaseFileExtension);
            if (File.Exists(imageDatabaseFilePath))
            {
                File.Delete(imageDatabaseFilePath);
            }

            // open, load database by scanning folder, move through images, close
            // The threading model for this is somewhat involved.  The test thread is the UI thread and therefore must drive the dispatcher.  This means the test
            // thread locks into UI message pumping when modal dialog is displayed, such as when the BackgroundWorker for loading files from a directory pops the
            // file count summary upon completion.  The test must therefore spin up a separate thread to close the dialogs and allow the main test thread to return
            // from the dispatcher and resume test execution.  If something jams up on the dialog handler thread Visual Studio may still consider the test running
            // when also attached as a debugger even if the test thread has completed.
            using (CarnassialWindow carnassial = new CarnassialWindow())
            {
                // show main window
                carnassial.Show();
                this.WaitForRenderingComplete();

                // start thread for handling file dialogs
                BackgroundWorker backgroundWorker = null;
                Task dialogDismissal = Task.Factory.StartNew(() =>
                {
                    AutomationElement carnassialAutomation = AutomationElement.RootElement.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, TestConstant.CarnassialAutomationID));
                    InvokePattern ambiguousDatesOkButton = this.FindDialogOkButton(carnassialAutomation, TestConstant.MessageBoxAutomationID);
                    ambiguousDatesOkButton.Invoke();

                    InvokePattern fileCountsOkButton = this.FindDialogOkButton(carnassialAutomation, TestConstant.FileCountsAutomationID);
                    fileCountsOkButton.Invoke();
                });
                // import files from directory
                Dispatcher.CurrentDispatcher.Invoke(() =>
                {
                    Assert.IsTrue((bool)carnassial.TryOpenTemplateAndBeginLoadImagesAsync(templateDatabaseFilePath, out backgroundWorker));
                    this.WaitForWorkerComplete(backgroundWorker);
                });
                this.WaitForRenderingComplete();

                // verify import succeeded
                PrivateObject carnassialAccessor = new PrivateObject(carnassial);
                DataEntryHandler dataHandler = (DataEntryHandler)carnassialAccessor.GetField(TestConstant.DataHandlerFieldName);
                Assert.IsTrue(dataHandler.ImageDatabase.CurrentlySelectedImageCount == 2);
                Assert.IsNotNull(dataHandler.ImageCache.Current);

                // verify forward and backward moves of the displayed image
                Assert.IsTrue((bool)carnassialAccessor.Invoke(TestConstant.TryShowImageWithoutSliderCallbackMethodName, true, ModifierKeys.None));
                this.WaitForRenderingComplete();
                Assert.IsTrue((bool)carnassialAccessor.Invoke(TestConstant.TryShowImageWithoutSliderCallbackMethodName, false, ModifierKeys.None));
                this.WaitForRenderingComplete();

                carnassial.Close();
            }

            // open, load existing database, pop dialogs, close
            using (CarnassialWindow carnassial = new CarnassialWindow())
            {
                carnassial.Show();
                this.WaitForRenderingComplete();

                BackgroundWorker backgroundWorker;
                Dispatcher.CurrentDispatcher.Invoke(() =>
                {
                    Assert.IsTrue((bool)carnassial.TryOpenTemplateAndBeginLoadImagesAsync(templateDatabaseFilePath, out backgroundWorker));
                    this.WaitForWorkerComplete(backgroundWorker);
                });
                this.WaitForRenderingComplete();

                PrivateObject carnassialAccessor = new PrivateObject(carnassial);
                DataEntryHandler dataHandler = (DataEntryHandler)carnassialAccessor.GetField(TestConstant.DataHandlerFieldName);
                Assert.IsTrue(dataHandler.ImageDatabase.CurrentlySelectedImageCount > 0);
                Assert.IsNotNull(dataHandler.ImageCache.Current);

                this.ShowDialog(new About(carnassial));
                CarnassialState state = (CarnassialState)carnassialAccessor.GetField("state");
                this.ShowDialog(new AdvancedCarnassialOptions(state, carnassial.MarkableCanvas, carnassial));
                this.ShowDialog(new ChooseDatabaseFile(new string[] { TestConstant.File.DefaultNewImageDatabaseFileName }, carnassial));

                this.ShowDialog(new CustomViewSelection(dataHandler.ImageDatabase, carnassial));
                this.ShowDialog(new DateCorrectAmbiguous(dataHandler.ImageDatabase, carnassial));
                this.ShowDialog(new DateDaylightSavingsTimeCorrection(dataHandler.ImageDatabase, dataHandler.ImageCache, carnassial));

                DateTimeFixedCorrection clockSetCorrection = new DateTimeFixedCorrection(dataHandler.ImageDatabase, dataHandler.ImageCache.Current, carnassial);
                this.ShowDialog(clockSetCorrection);

                DateTimeLinearCorrection clockDriftCorrection = new DateTimeLinearCorrection(dataHandler.ImageDatabase, carnassial);
                Assert.IsTrue(clockDriftCorrection.Abort == (dataHandler.ImageCache.Current == null));
                this.ShowDialog(clockDriftCorrection);

                this.ShowDialog(new EditLog(dataHandler.ImageDatabase.ImageSet.Log, carnassial));
                this.ShowDialog(new ExportCsv("test.csv", carnassial));

                using (DarkImagesThreshold darkThreshold = new DarkImagesThreshold(dataHandler.ImageDatabase, dataHandler.ImageCache.CurrentRow, new CarnassialState(), carnassial))
                {
                    this.ShowDialog(darkThreshold);
                }
                this.ShowDialog(new PopulateFieldWithMetadata(dataHandler.ImageDatabase, dataHandler.ImageCache.Current.GetImagePath(dataHandler.ImageDatabase.FolderPath), carnassial));
                this.ShowDialog(new RenameImageDatabaseFile(dataHandler.ImageDatabase.FileName, carnassial));
                this.ShowDialog(new DateRereadFromFiles(dataHandler.ImageDatabase, carnassial));
                this.ShowDialog(new FileCountsByQuality(dataHandler.ImageDatabase.GetImageCountsByQuality(), carnassial));
                this.ShowDialog(new TemplatesDontMatch(dataHandler.ImageDatabase.TemplateSynchronizationIssues, carnassial));

                MessageBox okMessageBox = this.CreateMessageBox(carnassial, MessageBoxButton.OK, MessageBoxImage.Error);
                this.ShowDialog(okMessageBox);
                MessageBox okCancelMessageBox = this.CreateMessageBox(carnassial, MessageBoxButton.OKCancel, MessageBoxImage.Information);
                this.ShowDialog(okCancelMessageBox);
                MessageBox yesNoMessageBox = this.CreateMessageBox(carnassial, MessageBoxButton.YesNo, MessageBoxImage.Question);
                this.ShowDialog(yesNoMessageBox);

                carnassial.Close();
            }
        }

        private MessageBox CreateMessageBox(Window owner, MessageBoxButton buttonType, MessageBoxImage iconType)
        {
            MessageBox messageBox = new MessageBox("Message box title", owner, buttonType);
            messageBox.Message.Icon = iconType;
            messageBox.Message.Problem = "Problem description.";
            messageBox.Message.Reason = "Explanation of why issue is an issue.";
            messageBox.Message.Solution = "Suggested method for resolving the issue.";
            messageBox.Message.Result = "Current status.";
            messageBox.Message.Hint = "Additional suggestions as to how to resolve the issue.";
            return messageBox;
        }

        private InvokePattern FindDialogOkButton(AutomationElement parent, string automationID)
        {
            AutomationElement dialog = null;
            DateTime startTime = DateTime.UtcNow;
            while ((dialog == null) && (DateTime.UtcNow - startTime < TestConstant.UIElementSearchTimeout))
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(100));
                dialog = parent.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, automationID));
            }
            Assert.IsNotNull(dialog);

            AutomationElement okButton = dialog.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, TestConstant.OkButtonAutomationID));
            Assert.IsNotNull(okButton);
            return (InvokePattern)okButton.GetCurrentPattern(InvokePattern.Pattern);
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

        private void WaitForWorkerComplete(BackgroundWorker backgroundWorker)
        {
            if (backgroundWorker == null)
            {
                return;
            }

            while (backgroundWorker.IsBusy)
            {
                this.WaitForRenderingComplete();
            }
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
