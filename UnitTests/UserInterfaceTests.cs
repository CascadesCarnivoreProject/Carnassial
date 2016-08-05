using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Timelapse.Database;
using Timelapse.Editor;
using Timelapse.Util;

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
            Assert.IsFalse((bool)editorAccessor.Invoke("TrySaveDatabaseBackupFile"));
            editorAccessor.Invoke("InitializeDataGrid", templateDatabaseFilePath);
            this.WaitForRenderingComplete();
            editor.Close();

            // open, load existing database, pop dialogs, close
            editor = new EditorWindow();
            editor.Show();
            this.WaitForRenderingComplete();
            Assert.IsTrue((bool)editorAccessor.Invoke("TrySaveDatabaseBackupFile"));
            editorAccessor.Invoke("InitializeDataGrid", templateDatabaseFilePath);
            this.WaitForRenderingComplete();

            this.ShowDialog(editor, new DialogAboutTimelapseEditor());
            this.ShowDialog(editor, new DialogEditChoiceList(editor.HelpText, Utilities.ConvertBarsToLineBreaks("Choice0|Choice1|Choice2|Choice3")));

            editor.Close();
        }

        // [TestMethod]
        public void Timelapse()
        {
            // open, do nothing, close
            using (TimelapseWindow timelapse = new TimelapseWindow())
            {
                timelapse.Show();
                this.WaitForRenderingComplete();
                timelapse.Close();
            }

            // create template database
            string templateDatabaseFilePath;
            using (TemplateDatabase templateDatabase = this.CreateTemplateDatabase(TestConstant.File.DefaultTemplateDatabaseFileName2015))
            {
                templateDatabaseFilePath = templateDatabase.FilePath;
            }

            // open, load database by scanning folder, move through images, close
            using (TimelapseWindow timelapse = new TimelapseWindow())
            {
                PrivateObject timelapseAccessor = new PrivateObject(timelapse);
                timelapse.Show();
                Dispatcher.CurrentDispatcher.Invoke(() =>
                {
                    Assert.IsTrue((bool)timelapseAccessor.Invoke("TryOpenTemplateAndLoadImages", templateDatabaseFilePath));
                });
                this.WaitForRenderingComplete();

                Assert.IsTrue((bool)timelapseAccessor.Invoke("TryShowImageWithoutSliderCallback", true, ModifierKeys.None));
                this.WaitForRenderingComplete();
                Assert.IsTrue((bool)timelapseAccessor.Invoke("TryShowImageWithoutSliderCallback", false, ModifierKeys.None));
                this.WaitForRenderingComplete();

                timelapse.Close();
            }

            // open, load existing database, pop dialogs, close
            using (TimelapseWindow timelapse = new TimelapseWindow())
            {
                PrivateObject timelapseAccessor = new PrivateObject(timelapse);
                timelapse.Show();
                Dispatcher.CurrentDispatcher.Invoke(() =>
                {
                    Assert.IsTrue((bool)timelapseAccessor.Invoke("TryOpenTemplateAndLoadImages", templateDatabaseFilePath));
                });
                this.WaitForRenderingComplete();

                this.ShowDialog(timelapse, new DialogAboutTimelapse());
                this.ShowDialog(timelapse, new DialogChooseDatabaseFile(new string[] { TestConstant.File.DefaultNewImageDatabaseFileName }));

                DataEntryHandler dataHandler = (DataEntryHandler)timelapseAccessor.GetField("dataHandler");
                //this.ShowDialog(timelapse, new DialogCustomViewFilter(dataHandler.ImageDatabase));

                //this.ShowDialog(timelapse, new DialogDateSwapDayMonth(dataHandler.ImageDatabase));
                //this.ShowDialog(timelapse, new DialogDateSwapDayMonthBulk(dataHandler.ImageDatabase));

                DialogDateTimeFixedCorrection clockSetCorrection = new DialogDateTimeFixedCorrection(dataHandler.ImageDatabase, dataHandler.ImageCache.Current);
                Assert.IsFalse(clockSetCorrection.Abort);
                this.ShowDialog(timelapse, clockSetCorrection);
                DialogDateTimeLinearCorrection clockDriftCorrection = new DialogDateTimeLinearCorrection(dataHandler.ImageDatabase);
                Assert.IsFalse(clockDriftCorrection.Abort);
                this.ShowDialog(timelapse, clockDriftCorrection);

                this.ShowDialog(timelapse, new DialogDateDaylightSavingsTimeCorrection(dataHandler.ImageDatabase, dataHandler.ImageCache));
                this.ShowDialog(timelapse, new DialogEditLog(dataHandler.ImageDatabase.ImageSet.Log));

                using (DialogOptionsDarkImagesThreshold darkThreshold = new DialogOptionsDarkImagesThreshold(dataHandler.ImageDatabase, dataHandler.ImageCache.CurrentRow, new TimelapseState()))
                {
                    this.ShowDialog(timelapse, darkThreshold);
                }
                using (DialogPopulateFieldWithMetadata populateField = new DialogPopulateFieldWithMetadata(dataHandler.ImageDatabase, dataHandler.ImageCache.Current.GetImagePath(dataHandler.ImageDatabase.FolderPath)))
                {
                    this.ShowDialog(timelapse, populateField);
                }
                this.ShowDialog(timelapse, new DialogRenameImageDatabaseFile(dataHandler.ImageDatabase.FileName));
                this.ShowDialog(timelapse, new DialogDateRereadFromFiles(dataHandler.ImageDatabase));
                this.ShowDialog(timelapse, new DialogStatisticsOfImageCounts(dataHandler.ImageDatabase.GetImageCountsByQuality()));
                this.ShowDialog(timelapse, new DialogTemplatesDontMatch(dataHandler.ImageDatabase.TemplateSynchronizationIssues));

                DialogMessageBox okMessageBox = this.CreateMessageBox(timelapse, MessageBoxButton.OK, MessageBoxImage.Error);
                this.ShowDialog(timelapse, okMessageBox);
                DialogMessageBox okCancelMessageBox = this.CreateMessageBox(timelapse, MessageBoxButton.OKCancel, MessageBoxImage.Information);
                this.ShowDialog(timelapse, okCancelMessageBox);
                DialogMessageBox yesNoMessageBox = this.CreateMessageBox(timelapse, MessageBoxButton.YesNo, MessageBoxImage.Question);
                this.ShowDialog(timelapse, yesNoMessageBox);

                timelapse.Close();
            }
        }

        private DialogMessageBox CreateMessageBox(Window owner, MessageBoxButton buttonType, MessageBoxImage iconType)
        {
            DialogMessageBox messageBox = new DialogMessageBox("Message box title", owner, buttonType);
            messageBox.Message.Icon = iconType;
            messageBox.Message.Problem = "Problem description.";
            messageBox.Message.Reason = "Explanation of why issue is an issue.";
            messageBox.Message.Solution = "Suggested method for resolving the issue.";
            messageBox.Message.Result = "Current status.";
            messageBox.Message.Hint = "Additional suggestions as to how to resolve the issue.";
            return messageBox;
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
