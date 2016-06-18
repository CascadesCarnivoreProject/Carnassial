using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows.Threading;
using Timelapse.Database;

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
        public void Timelapse()
        {
            // open, do nothing, close
            using (TimelapseWindow mainWindow = new TimelapseWindow())
            {
                mainWindow.Show();
                mainWindow.Close();
            }

            // create template database
            string templateDatabaseFilePath;
            using (TemplateDatabase templateDatabase = this.CreateTemplateDatabase(TestConstant.File.DefaultTemplateDatabaseFileName2015))
            {
                templateDatabaseFilePath = templateDatabase.FilePath;
            }

            // open, load database by scanning folder, close
            using (TimelapseWindow mainWindow = new TimelapseWindow())
            {
                mainWindow.Show();
                mainWindow.TryOpenTemplateAndLoadImages(templateDatabaseFilePath);
                this.WaitForRenderingComplete();
                mainWindow.Close();
            }

            // open, load existing database, close
            using (TimelapseWindow mainWindow = new TimelapseWindow())
            {
                mainWindow.Show();
                mainWindow.TryOpenTemplateAndLoadImages(templateDatabaseFilePath);
                this.WaitForRenderingComplete();
                mainWindow.Close();
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
