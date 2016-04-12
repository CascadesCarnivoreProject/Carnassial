using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;
using System;
using System.Windows;

namespace Timelapse.UnitTests
{
    [TestClass]
    public class RegistryTests
    {
        /// <summary>
        /// Sanity test against whatever the current state of Timelapse' registry keys is.
        /// </summary>
        [TestMethod]
        public void ReadWriteProductionKeys()
        {
            using (TimelapseRegistryUserSettings timelapseRegistry = new TimelapseRegistryUserSettings())
            {
                bool audioFeedback = timelapseRegistry.ReadAudioFeedback();
                bool controlInSeparateWindow = timelapseRegistry.ReadControlsInSeparateWindow();
                Point controlWindowSize = timelapseRegistry.ReadControlWindowSize();
                double darkPixelRatioThreshold = timelapseRegistry.ReadDarkPixelRatioThreshold();
                int darkPixelThreshold = timelapseRegistry.ReadDarkPixelThreshold();
                string lastImageFolderPath = timelapseRegistry.ReadLastDatabaseFolderPath();
                string lastImageTemplateName = timelapseRegistry.ReadLastDatabaseTemplateName();
                bool showCsvDialog = timelapseRegistry.ReadShowCsvDialog();
            }
        }

        [TestMethod]
        public void ReadWriteTestKeys()
        {
            string testRootKey = Constants.Registry.RootKey + "UnitTest";
            using (RegistryKey testKey = Registry.CurrentUser.OpenSubKey(testRootKey))
            {
                if (testKey != null)
                {
                    Registry.CurrentUser.DeleteSubKeyTree(testRootKey);
                }
            }

            using (TimelapseRegistryUserSettings timelapseRegistry = new TimelapseRegistryUserSettings(testRootKey))
            {
                bool audioFeedback = timelapseRegistry.ReadAudioFeedback();
                bool controlInSeparateWindow = timelapseRegistry.ReadControlsInSeparateWindow();
                Point controlWindowSize = timelapseRegistry.ReadControlWindowSize();
                double darkPixelRatioThreshold = timelapseRegistry.ReadDarkPixelRatioThreshold();
                int darkPixelThreshold = timelapseRegistry.ReadDarkPixelThreshold();
                string lastImageFolderPath = timelapseRegistry.ReadLastDatabaseFolderPath();
                string lastImageTemplateName = timelapseRegistry.ReadLastDatabaseTemplateName();
                bool showCsvDialog = timelapseRegistry.ReadShowCsvDialog();

                Assert.IsFalse(audioFeedback);
                Assert.IsFalse(controlInSeparateWindow);
                Assert.IsTrue(controlWindowSize.X == 0 && controlWindowSize.Y == 0);
                Assert.IsTrue(darkPixelRatioThreshold == Constants.DarkPixelRatioThresholdDefault);
                Assert.IsTrue(darkPixelThreshold == Constants.DarkPixelThresholdDefault);
                Assert.IsTrue(lastImageFolderPath == null);
                Assert.IsTrue(lastImageTemplateName == null);

                lastImageFolderPath = Environment.CurrentDirectory;
                lastImageTemplateName = Constants.File.TemplateDatabaseFileName;

                timelapseRegistry.WriteAudioFeedback(audioFeedback);
                timelapseRegistry.WriteControlsInSeparateWindow(controlInSeparateWindow);
                timelapseRegistry.WriteControlWindowSize(controlWindowSize);
                timelapseRegistry.WriteDarkPixelRatioThreshold(darkPixelRatioThreshold);
                timelapseRegistry.WriteDarkPixelThreshold(darkPixelThreshold);
                timelapseRegistry.TryWriteLastDatabaseFolderPath(lastImageFolderPath);
                timelapseRegistry.TryWriteLastDatabaseTemplateName(lastImageTemplateName);
                timelapseRegistry.WriteShowCsvDialog(showCsvDialog);

                audioFeedback = timelapseRegistry.ReadAudioFeedback();
                controlInSeparateWindow = timelapseRegistry.ReadControlsInSeparateWindow();
                controlWindowSize = timelapseRegistry.ReadControlWindowSize();
                darkPixelRatioThreshold = timelapseRegistry.ReadDarkPixelRatioThreshold();
                darkPixelThreshold = timelapseRegistry.ReadDarkPixelThreshold();
                lastImageFolderPath = timelapseRegistry.ReadLastDatabaseFolderPath();
                lastImageTemplateName = timelapseRegistry.ReadLastDatabaseTemplateName();
                showCsvDialog = timelapseRegistry.ReadShowCsvDialog();

                Assert.IsFalse(audioFeedback);
                Assert.IsFalse(controlInSeparateWindow);
                Assert.IsTrue(controlWindowSize.X == 0 && controlWindowSize.Y == 0);
                Assert.IsTrue(darkPixelRatioThreshold == Constants.DarkPixelRatioThresholdDefault);
                Assert.IsTrue(darkPixelThreshold == Constants.DarkPixelThresholdDefault);
                Assert.IsTrue(lastImageFolderPath == Environment.CurrentDirectory);
                Assert.IsTrue(lastImageTemplateName == Constants.File.TemplateDatabaseFileName);
            }

            Registry.CurrentUser.DeleteSubKeyTree(testRootKey);
        }
    }
}