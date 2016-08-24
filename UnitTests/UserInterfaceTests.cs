using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Threading;
using Timelapse.Controls;
using Timelapse.Database;
using Timelapse.Dialog;
using Timelapse.Editor;
using Timelapse.Util;
using MessageBox = Timelapse.Dialog.MessageBox;

namespace Timelapse.UnitTests
{
    [TestClass]
    public class UserInterfaceTests : TimelapseTest
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
            editor = new EditorWindow();
            PrivateObject editorAccessor = new PrivateObject(editor);
            editor.Show();
            this.WaitForRenderingComplete();
            Assert.IsFalse((bool)editorAccessor.Invoke(TestConstant.TrySaveDatabaseBackupFileMethodName));
            editorAccessor.Invoke(TestConstant.InitializeDataGridMethodName, templateDatabaseFilePath);
            this.WaitForRenderingComplete();
            editor.Close();

            // open, load existing database, pop dialogs, close
            editor = new EditorWindow();
            editor.Show();
            this.WaitForRenderingComplete();
            Assert.IsTrue((bool)editorAccessor.Invoke(TestConstant.TrySaveDatabaseBackupFileMethodName));
            editorAccessor.Invoke(TestConstant.InitializeDataGridMethodName, templateDatabaseFilePath);
            this.WaitForRenderingComplete();

            this.ShowDialog(editor, new DialogAboutTimelapseEditor());
            this.ShowDialog(editor, new DialogEditChoiceList(editor.HelpText, Utilities.ConvertBarsToLineBreaks("Choice0|Choice1|Choice2|Choice3")));

            editor.Close();
        }

        [TestMethod]
        public void Timelapse()
        {
            // Constants.Images needs to load resources from Timelapse.exe and BitmapFrame.Create() relies on Application.ResourceAssembly to do this
            // for unit tests (or the editor) ResourceAssembly does not get set as Timelapse.exe is the entry point
            if (Application.ResourceAssembly == null)
            {
                Application.ResourceAssembly = typeof(Constants.Images).Assembly;
            }

            // open, do nothing, close
            using (TimelapseWindow timelapse = new TimelapseWindow())
            {
                timelapse.Show();
                this.WaitForRenderingComplete();
                timelapse.Close();
                this.WaitForRenderingComplete();
            }

            // create template database and remove any image database from previous test executions
            string templateDatabaseFilePath;
            using (TemplateDatabase templateDatabase = this.CreateTemplateDatabase(TestConstant.File.DefaultTemplateDatabaseFileName2015))
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
            using (TimelapseWindow timelapse = new TimelapseWindow())
            {
                // show main window
                timelapse.Show();
                this.WaitForRenderingComplete();

                // start thread for handling file dialogs
                BackgroundWorker backgroundWorker = null;
                Task dialogDismissal = Task.Factory.StartNew(() =>
                {
                    AutomationElement timelapseAutomation = AutomationElement.RootElement.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, TestConstant.TimelapseAutomationID));
                    InvokePattern ambiguousDatesOkButton = this.FindDialogOkButton(timelapseAutomation, TestConstant.MessageBoxAutomationID);
                    ambiguousDatesOkButton.Invoke();

                    InvokePattern fileCountsOkButton = this.FindDialogOkButton(timelapseAutomation, TestConstant.FileCountsAutomationID);
                    fileCountsOkButton.Invoke();
                });
                // import files from directory
                Dispatcher.CurrentDispatcher.Invoke(() =>
                {
                    Assert.IsTrue((bool)timelapse.TryOpenTemplateAndBeginLoadImagesAsync(templateDatabaseFilePath, out backgroundWorker));
                    this.WaitForWorkerComplete(backgroundWorker);
                });
                this.WaitForRenderingComplete();

                // verify import succeeded
                PrivateObject timelapseAccessor = new PrivateObject(timelapse);
                DataEntryHandler dataHandler = (DataEntryHandler)timelapseAccessor.GetField(TestConstant.DataHandlerFieldName);
                Assert.IsTrue(dataHandler.ImageDatabase.CurrentlySelectedImageCount == 2);
                Assert.IsNotNull(dataHandler.ImageCache.Current);

                // verify forward and backward moves of the displayed image
                Assert.IsTrue((bool)timelapseAccessor.Invoke(TestConstant.TryShowImageWithoutSliderCallbackMethodName, true, ModifierKeys.None));
                this.WaitForRenderingComplete();
                Assert.IsTrue((bool)timelapseAccessor.Invoke(TestConstant.TryShowImageWithoutSliderCallbackMethodName, false, ModifierKeys.None));
                this.WaitForRenderingComplete();

                timelapse.Close();
            }

            // open, load existing database, pop dialogs, close
            using (TimelapseWindow timelapse = new TimelapseWindow())
            {
                timelapse.Show();
                this.WaitForRenderingComplete();

                BackgroundWorker backgroundWorker;
                Dispatcher.CurrentDispatcher.Invoke(() =>
                {
                    Assert.IsTrue((bool)timelapse.TryOpenTemplateAndBeginLoadImagesAsync(templateDatabaseFilePath, out backgroundWorker));
                    this.WaitForWorkerComplete(backgroundWorker);
                });
                this.WaitForRenderingComplete();

                PrivateObject timelapseAccessor = new PrivateObject(timelapse);
                DataEntryHandler dataHandler = (DataEntryHandler)timelapseAccessor.GetField(TestConstant.DataHandlerFieldName);
                Assert.IsTrue(dataHandler.ImageDatabase.CurrentlySelectedImageCount > 0);
                Assert.IsNotNull(dataHandler.ImageCache.Current);

                this.ShowDialog(timelapse, new AboutTimelapse());
                this.ShowDialog(timelapse, new ChooseDatabaseFile(new string[] { TestConstant.File.DefaultNewImageDatabaseFileName }));

                this.ShowDialog(timelapse, new CustomViewFilter(dataHandler.ImageDatabase, DateTime.Now));
                this.ShowDialog(timelapse, new DateCorrectAmbiguous(dataHandler.ImageDatabase));
                this.ShowDialog(timelapse, new DateDaylightSavingsTimeCorrection(dataHandler.ImageDatabase, dataHandler.ImageCache));

                DateTimeFixedCorrection clockSetCorrection = new DateTimeFixedCorrection(dataHandler.ImageDatabase, dataHandler.ImageCache.Current);
                Assert.IsFalse(clockSetCorrection.Abort);
                this.ShowDialog(timelapse, clockSetCorrection);

                DateTimeLinearCorrection clockDriftCorrection = new DateTimeLinearCorrection(dataHandler.ImageDatabase);
                Assert.IsTrue(clockDriftCorrection.Abort == (dataHandler.ImageCache.Current == null));
                this.ShowDialog(timelapse, clockDriftCorrection);

                this.ShowDialog(timelapse, new EditLog(dataHandler.ImageDatabase.ImageSet.Log));

                Throttles throttle = new Throttles();
                using (DarkImagesThreshold darkThreshold = new DarkImagesThreshold(dataHandler.ImageDatabase, dataHandler.ImageCache.CurrentRow, new TimelapseState(throttle)))
                {
                    this.ShowDialog(timelapse, darkThreshold);
                }
                using (PopulateFieldWithMetadata populateField = new PopulateFieldWithMetadata(dataHandler.ImageDatabase, dataHandler.ImageCache.Current.GetImagePath(dataHandler.ImageDatabase.FolderPath)))
                {
                    this.ShowDialog(timelapse, populateField);
                }
                this.ShowDialog(timelapse, new RenameImageDatabaseFile(dataHandler.ImageDatabase.FileName));
                this.ShowDialog(timelapse, new DateRereadFromFiles(dataHandler.ImageDatabase));
                this.ShowDialog(timelapse, new FileCountsByQuality(dataHandler.ImageDatabase.GetImageCountsByQuality()));
                this.ShowDialog(timelapse, new TemplatesDontMatch(dataHandler.ImageDatabase.TemplateSynchronizationIssues));

                MessageBox okMessageBox = this.CreateMessageBox(timelapse, MessageBoxButton.OK, MessageBoxImage.Error);
                this.ShowDialog(timelapse, okMessageBox);
                MessageBox okCancelMessageBox = this.CreateMessageBox(timelapse, MessageBoxButton.OKCancel, MessageBoxImage.Information);
                this.ShowDialog(timelapse, okCancelMessageBox);
                MessageBox yesNoMessageBox = this.CreateMessageBox(timelapse, MessageBoxButton.YesNo, MessageBoxImage.Question);
                this.ShowDialog(timelapse, yesNoMessageBox);

                timelapse.Close();
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

        private void ShowDialog(Window owner, Window dialog)
        {
            dialog.Loaded += (object sender, RoutedEventArgs eventArgs) => { dialog.Close(); };
            dialog.Owner = owner;
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
