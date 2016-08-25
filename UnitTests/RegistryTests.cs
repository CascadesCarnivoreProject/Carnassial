using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using Timelapse.Editor.Util;
using Timelapse.Util;

namespace Timelapse.UnitTests
{
    [TestClass]
    public class RegistryTests : TimelapseTest
    {
        /// <summary>
        /// Sanity test against whatever the current state of the editor's registry keys is.
        /// </summary>
        [TestMethod]
        public void EditorProductionKeysRead()
        {
            EditorUserRegistrySettings editorRegistry = new EditorUserRegistrySettings();
        }

        [TestMethod]
        public void EditorTestKeysCreateReuseUpdate()
        {
            string testRootKey = Constants.Registry.RootKey + "EditorUnitTest";
            using (RegistryKey testKey = Registry.CurrentUser.OpenSubKey(testRootKey))
            {
                if (testKey != null)
                {
                    Registry.CurrentUser.DeleteSubKeyTree(testRootKey);
                }
            }

            EditorUserRegistrySettings editorRegistry = new EditorUserRegistrySettings(testRootKey);
            Assert.IsNotNull(editorRegistry.MostRecentTemplates);
            Assert.IsTrue(editorRegistry.MostRecentTemplates.Count == 0);

            // write
            editorRegistry.WriteToRegistry();

            // loopback
            editorRegistry.ReadFromRegistry();
            Assert.IsNotNull(editorRegistry.MostRecentTemplates);
            Assert.IsTrue(editorRegistry.MostRecentTemplates.Count == 0);
            string mostRecentTemplatePath;
            Assert.IsFalse(editorRegistry.MostRecentTemplates.TryGetMostRecent(out mostRecentTemplatePath));

            // overwrite
            editorRegistry.WriteToRegistry();

            // modify
            string templatePath = Path.Combine(this.WorkingDirectory, Constants.File.DefaultTemplateDatabaseFileName);
            editorRegistry.MostRecentTemplates.SetMostRecent(templatePath);
            editorRegistry.WriteToRegistry();
            editorRegistry.ReadFromRegistry();

            Assert.IsNotNull(editorRegistry.MostRecentTemplates);
            Assert.IsTrue(editorRegistry.MostRecentTemplates.Count == 1);
            Assert.IsTrue(editorRegistry.MostRecentTemplates.TryGetMostRecent(out mostRecentTemplatePath));
            Assert.IsTrue(mostRecentTemplatePath == templatePath);

            Registry.CurrentUser.DeleteSubKeyTree(testRootKey);
        }

        /// <summary>
        /// Basic functional validation of the <see cref="MostRecentlyUsedList" /> class.
        /// </summary>
        [TestMethod]
        public void MostRecentlyUsedList()
        {
            MostRecentlyUsedList<int> mruList = new MostRecentlyUsedList<int>(5);

            mruList.SetMostRecent(0);
            Assert.IsFalse(mruList.IsFull());
            int mostRecent;
            Assert.IsTrue(mruList.TryGetMostRecent(out mostRecent));
            Assert.IsTrue(mostRecent == 0);
            List<int> list = mruList.ToList();
            Assert.IsTrue(list.Count == 1);
            Assert.IsTrue(list[0] == 0);

            mruList.SetMostRecent(1);
            Assert.IsFalse(mruList.IsFull());
            Assert.IsTrue(mruList.TryGetMostRecent(out mostRecent));
            Assert.IsTrue(mostRecent == 1);
            list = mruList.ToList();
            Assert.IsTrue(list.Count == 2);
            Assert.IsTrue(list[0] == 1);
            Assert.IsTrue(list[1] == 0);

            mruList.SetMostRecent(0);
            Assert.IsFalse(mruList.IsFull());
            Assert.IsTrue(mruList.TryGetMostRecent(out mostRecent));
            Assert.IsTrue(mostRecent == 0);
            list = mruList.ToList();
            Assert.IsTrue(list.Count == 2);
            Assert.IsTrue(list[0] == 0);
            Assert.IsTrue(list[1] == 1);

            Assert.IsTrue(mruList.TryRemove(0));
            Assert.IsFalse(mruList.IsFull());
            list = mruList.ToList();
            Assert.IsTrue(list.Count == 1);
            Assert.IsTrue(list[0] == 1);

            Assert.IsFalse(mruList.TryRemove(0));
            Assert.IsTrue(mruList.TryRemove(1));
            Assert.IsFalse(mruList.IsFull());
            list = mruList.ToList();
            Assert.IsTrue(list.Count == 0);

            mruList.SetMostRecent(2);
            mruList.SetMostRecent(3);
            mruList.SetMostRecent(4);
            mruList.SetMostRecent(5);
            mruList.SetMostRecent(6);
            mruList.SetMostRecent(7);
            Assert.IsTrue(mruList.IsFull());
            Assert.IsTrue(mruList.TryGetMostRecent(out mostRecent));
            Assert.IsTrue(mostRecent == 7);
            list = mruList.ToList();
            Assert.IsTrue(list.Count == 5);
            Assert.IsTrue(list[0] == 7);
            Assert.IsTrue(list[1] == 6);
            Assert.IsTrue(list[2] == 5);
            Assert.IsTrue(list[3] == 4);
            Assert.IsTrue(list[4] == 3);

            mruList.SetMostRecent(6);
            Assert.IsTrue(mruList.IsFull());
            Assert.IsTrue(mruList.TryGetMostRecent(out mostRecent));
            Assert.IsTrue(mostRecent == 6);
            list = mruList.ToList();
            Assert.IsTrue(list.Count == 5);
            Assert.IsTrue(list[0] == 6);
            Assert.IsTrue(list[1] == 7);
            Assert.IsTrue(list[2] == 5);
            Assert.IsTrue(list[3] == 4);
            Assert.IsTrue(list[4] == 3);

            mruList.SetMostRecent(3);
            Assert.IsTrue(mruList.IsFull());
            Assert.IsTrue(mruList.TryGetMostRecent(out mostRecent));
            Assert.IsTrue(mostRecent == 3);
            list = mruList.ToList();
            Assert.IsTrue(list.Count == 5);
            Assert.IsTrue(list[0] == 3);
            Assert.IsTrue(list[1] == 6);
            Assert.IsTrue(list[2] == 7);
            Assert.IsTrue(list[3] == 5);
            Assert.IsTrue(list[4] == 4);

            Assert.IsFalse(mruList.TryRemove(-1));
            Assert.IsTrue(mruList.IsFull());

            Assert.IsTrue(mruList.TryRemove(5));
            Assert.IsFalse(mruList.IsFull());
            list = mruList.ToList();
            Assert.IsTrue(list.Count == 4);
            Assert.IsTrue(list[0] == 3);
            Assert.IsTrue(list[1] == 6);
            Assert.IsTrue(list[2] == 7);
            Assert.IsTrue(list[3] == 4);

            int leastRecent;
            Assert.IsTrue(mruList.TryGetLeastRecent(out leastRecent));
            Assert.IsTrue(leastRecent == 4);
        }

        /// <summary>
        /// Sanity test against whatever the current state of Timelapse' registry keys is.
        /// </summary>
        [TestMethod]
        public void TimelapseProductionKeysRead()
        {
            TimelapseState state = new TimelapseState();
            state.ReadFromRegistry();
        }

        [TestMethod]
        public void TimelapseTestKeysCreateReuseUpdate()
        {
            string testRootKey = Constants.Registry.RootKey + "TimelapseUnitTest";
            using (RegistryKey testKey = Registry.CurrentUser.OpenSubKey(testRootKey))
            {
                if (testKey != null)
                {
                    Registry.CurrentUser.DeleteSubKeyTree(testRootKey);
                }
            }

            TimelapseUserRegistrySettings userSettings = new TimelapseUserRegistrySettings(testRootKey);
            this.VerifyDefaultState(userSettings);

            // write
            userSettings.WriteToRegistry();

            // loopback
            userSettings.ReadFromRegistry();
            this.VerifyDefaultState(userSettings);

            // overwrite
            userSettings.WriteToRegistry();

            // modify
            userSettings.AudioFeedback = true;
            userSettings.ControlsInSeparateWindow = true;
            int controlWindowSizeInPixels = 200;
            userSettings.ControlWindowSize = new Point(controlWindowSizeInPixels, controlWindowSizeInPixels);
            double modifiedDarkPixelRatioThreshold = userSettings.DarkPixelRatioThreshold + 1.0;
            userSettings.DarkPixelRatioThreshold = modifiedDarkPixelRatioThreshold;
            int modifiedDarkPixelThreshold = userSettings.DarkPixelThreshold + 1;
            userSettings.DarkPixelThreshold = modifiedDarkPixelThreshold;
            string databasePath = Path.Combine(this.WorkingDirectory, Constants.File.DefaultImageDatabaseFileName);
            userSettings.MostRecentImageSets.SetMostRecent(databasePath);
            userSettings.SuppressAmbiguousDatesDialog = true;
            userSettings.SuppressCsvExportDialog = true;
            userSettings.SuppressCsvImportPrompt = true;
            userSettings.SuppressFileCountOnImportDialog = true;
            userSettings.SuppressFilteredAmbiguousDatesPrompt = true;
            userSettings.SuppressFilteredCsvExportPrompt = true;
            userSettings.SuppressFilteredDarkThresholdPrompt = true;
            userSettings.SuppressFilteredDateTimeFixedCorrectionPrompt = true;
            userSettings.SuppressFilteredDateTimeLinearCorrectionPrompt = true;
            userSettings.SuppressFilteredDaylightSavingsCorrectionPrompt = true;
            userSettings.SuppressFilteredPopulateFieldFromMetadataPrompt = true;
            userSettings.SuppressFilteredRereadDatesFromFilesPrompt = true;
            userSettings.Throttles.SetDesiredImageRendersPerSecond(Constants.ThrottleValues.DesiredMaximumImageRendersPerSecondUpperBound);

            userSettings.WriteToRegistry();
            userSettings.ReadFromRegistry();

            Assert.IsTrue(userSettings.AudioFeedback);
            Assert.IsTrue(userSettings.ControlsInSeparateWindow);
            Assert.IsTrue(userSettings.ControlWindowSize.X == controlWindowSizeInPixels && userSettings.ControlWindowSize.Y == controlWindowSizeInPixels);
            Assert.IsTrue(userSettings.DarkPixelRatioThreshold == modifiedDarkPixelRatioThreshold);
            Assert.IsTrue(userSettings.DarkPixelThreshold == modifiedDarkPixelThreshold);
            Assert.IsNotNull(userSettings.MostRecentImageSets);
            Assert.IsTrue(userSettings.MostRecentImageSets.Count == 1);
            string mostRecentDatabasePath;
            Assert.IsTrue(userSettings.MostRecentImageSets.TryGetMostRecent(out mostRecentDatabasePath));
            Assert.IsTrue(mostRecentDatabasePath == databasePath);
            Assert.IsTrue(userSettings.SuppressAmbiguousDatesDialog);
            Assert.IsTrue(userSettings.SuppressCsvExportDialog);
            Assert.IsTrue(userSettings.SuppressCsvImportPrompt);
            Assert.IsTrue(userSettings.SuppressFileCountOnImportDialog);
            Assert.IsTrue(userSettings.SuppressFilteredAmbiguousDatesPrompt);
            Assert.IsTrue(userSettings.SuppressFilteredCsvExportPrompt);
            Assert.IsTrue(userSettings.SuppressFilteredDarkThresholdPrompt);
            Assert.IsTrue(userSettings.SuppressFilteredDateTimeFixedCorrectionPrompt);
            Assert.IsTrue(userSettings.SuppressFilteredDateTimeLinearCorrectionPrompt);
            Assert.IsTrue(userSettings.SuppressFilteredDaylightSavingsCorrectionPrompt);
            Assert.IsTrue(userSettings.SuppressFilteredPopulateFieldFromMetadataPrompt);
            Assert.IsTrue(userSettings.SuppressFilteredRereadDatesFromFilesPrompt);
            Assert.IsTrue(userSettings.Throttles.DesiredImageRendersPerSecond == Constants.ThrottleValues.DesiredMaximumImageRendersPerSecondUpperBound);

            Registry.CurrentUser.DeleteSubKeyTree(testRootKey);
        }

        private void VerifyDefaultState(TimelapseUserRegistrySettings userSettings)
        {
            Assert.IsFalse(userSettings.AudioFeedback);
            Assert.IsFalse(userSettings.ControlsInSeparateWindow);
            Assert.IsTrue(userSettings.ControlWindowSize.X == 0 && userSettings.ControlWindowSize.Y == 0);
            Assert.IsTrue(userSettings.DarkPixelRatioThreshold == Constants.Images.DarkPixelRatioThresholdDefault);
            Assert.IsTrue(userSettings.DarkPixelThreshold == Constants.Images.DarkPixelThresholdDefault);
            Assert.IsTrue(userSettings.Throttles.DesiredImageRendersPerSecond == Constants.ThrottleValues.DesiredMaximumImageRendersPerSecondDefault);
            Assert.IsNotNull(userSettings.MostRecentImageSets);
            Assert.IsTrue(userSettings.MostRecentImageSets.Count == 0);
            string mostRecentDatabasePath;
            Assert.IsFalse(userSettings.MostRecentImageSets.TryGetMostRecent(out mostRecentDatabasePath));
            Assert.IsNull(mostRecentDatabasePath);
            Assert.IsFalse(userSettings.SuppressAmbiguousDatesDialog);
            Assert.IsFalse(userSettings.SuppressCsvExportDialog);
            Assert.IsFalse(userSettings.SuppressCsvImportPrompt);
            Assert.IsFalse(userSettings.SuppressFileCountOnImportDialog);
            Assert.IsFalse(userSettings.SuppressFilteredAmbiguousDatesPrompt);
            Assert.IsFalse(userSettings.SuppressFilteredCsvExportPrompt);
            Assert.IsFalse(userSettings.SuppressFilteredDarkThresholdPrompt);
            Assert.IsFalse(userSettings.SuppressFilteredDateTimeFixedCorrectionPrompt);
            Assert.IsFalse(userSettings.SuppressFilteredDateTimeLinearCorrectionPrompt);
            Assert.IsFalse(userSettings.SuppressFilteredPopulateFieldFromMetadataPrompt);
            Assert.IsFalse(userSettings.SuppressFilteredRereadDatesFromFilesPrompt);
        }
    }
}