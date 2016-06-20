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
            using (EditorRegistryUserSettings editorRegistry = new EditorRegistryUserSettings())
            {
                MostRecentlyUsedList<string> mostRecentTemplates = editorRegistry.ReadMostRecentTemplates();
            }
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

            using (EditorRegistryUserSettings editorRegistry = new EditorRegistryUserSettings(testRootKey))
            {
                // read
                MostRecentlyUsedList<string> mostRecentTemplates = editorRegistry.ReadMostRecentTemplates();
                Assert.IsTrue(mostRecentTemplates != null);
                Assert.IsTrue(mostRecentTemplates.Count == 0);

                // write
                string templatePath = Path.Combine(this.WorkingDirectory, Constants.File.DefaultTemplateDatabaseFileName);
                mostRecentTemplates.SetMostRecent(templatePath);
                editorRegistry.WriteMostRecentTemplates(mostRecentTemplates);

                // loopback
                mostRecentTemplates = editorRegistry.ReadMostRecentTemplates();
                Assert.IsTrue(mostRecentTemplates != null);
                Assert.IsTrue(mostRecentTemplates.Count == 1);
                string mostRecentTemplatePath;
                Assert.IsTrue(mostRecentTemplates.TryGetMostRecent(out mostRecentTemplatePath));
                Assert.IsTrue(mostRecentTemplatePath == templatePath);

                // overwrite
                editorRegistry.WriteMostRecentTemplates(mostRecentTemplates);
            }

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
            using (TimelapseRegistryUserSettings timelapseRegistry = new TimelapseRegistryUserSettings())
            {
                bool audioFeedback = timelapseRegistry.ReadAudioFeedback();
                bool controlInSeparateWindow = timelapseRegistry.ReadControlsInSeparateWindow();
                Point controlWindowSize = timelapseRegistry.ReadControlWindowSize();
                double darkPixelRatioThreshold = timelapseRegistry.ReadDarkPixelRatioThreshold();
                int darkPixelThreshold = timelapseRegistry.ReadDarkPixelThreshold();
                MostRecentlyUsedList<string> mostRecentImageSets = timelapseRegistry.ReadMostRecentImageSets();
                bool showCsvDialog = timelapseRegistry.ReadShowCsvDialog();
            }
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

            using (TimelapseRegistryUserSettings timelapseRegistry = new TimelapseRegistryUserSettings(testRootKey))
            {
                // read
                bool audioFeedback = timelapseRegistry.ReadAudioFeedback();
                bool controlInSeparateWindow = timelapseRegistry.ReadControlsInSeparateWindow();
                Point controlWindowSize = timelapseRegistry.ReadControlWindowSize();
                double darkPixelRatioThreshold = timelapseRegistry.ReadDarkPixelRatioThreshold();
                int darkPixelThreshold = timelapseRegistry.ReadDarkPixelThreshold();
                MostRecentlyUsedList<string> mostRecentImageSets = timelapseRegistry.ReadMostRecentImageSets();
                bool showCsvDialog = timelapseRegistry.ReadShowCsvDialog();

                Assert.IsFalse(audioFeedback);
                Assert.IsFalse(controlInSeparateWindow);
                Assert.IsTrue(controlWindowSize.X == 0 && controlWindowSize.Y == 0);
                Assert.IsTrue(darkPixelRatioThreshold == Constants.Images.DarkPixelRatioThresholdDefault);
                Assert.IsTrue(darkPixelThreshold == Constants.Images.DarkPixelThresholdDefault);
                Assert.IsTrue(mostRecentImageSets != null);
                Assert.IsTrue(mostRecentImageSets.Count == 0);

                // write
                string databasePath = Path.Combine(this.WorkingDirectory, Constants.File.DefaultImageDatabaseFileName);
                mostRecentImageSets.SetMostRecent(databasePath);

                timelapseRegistry.WriteAudioFeedback(audioFeedback);
                timelapseRegistry.WriteControlsInSeparateWindow(controlInSeparateWindow);
                timelapseRegistry.WriteControlWindowSize(controlWindowSize);
                timelapseRegistry.WriteDarkPixelRatioThreshold(darkPixelRatioThreshold);
                timelapseRegistry.WriteDarkPixelThreshold(darkPixelThreshold);
                timelapseRegistry.WriteMostRecentImageSets(mostRecentImageSets);
                timelapseRegistry.WriteShowCsvDialog(showCsvDialog);

                // loopback
                audioFeedback = timelapseRegistry.ReadAudioFeedback();
                controlInSeparateWindow = timelapseRegistry.ReadControlsInSeparateWindow();
                controlWindowSize = timelapseRegistry.ReadControlWindowSize();
                darkPixelRatioThreshold = timelapseRegistry.ReadDarkPixelRatioThreshold();
                darkPixelThreshold = timelapseRegistry.ReadDarkPixelThreshold();
                mostRecentImageSets = timelapseRegistry.ReadMostRecentImageSets();
                showCsvDialog = timelapseRegistry.ReadShowCsvDialog();

                Assert.IsFalse(audioFeedback);
                Assert.IsFalse(controlInSeparateWindow);
                Assert.IsTrue(controlWindowSize.X == 0 && controlWindowSize.Y == 0);
                Assert.IsTrue(darkPixelRatioThreshold == Constants.Images.DarkPixelRatioThresholdDefault);
                Assert.IsTrue(darkPixelThreshold == Constants.Images.DarkPixelThresholdDefault);
                Assert.IsTrue(mostRecentImageSets != null);
                Assert.IsTrue(mostRecentImageSets.Count == 1);
                string mostRecentDatabasePath;
                Assert.IsTrue(mostRecentImageSets.TryGetMostRecent(out mostRecentDatabasePath));
                Assert.IsTrue(mostRecentDatabasePath == databasePath);

                // overwrite
                timelapseRegistry.WriteAudioFeedback(audioFeedback);
                timelapseRegistry.WriteControlsInSeparateWindow(controlInSeparateWindow);
                timelapseRegistry.WriteControlWindowSize(controlWindowSize);
                timelapseRegistry.WriteDarkPixelRatioThreshold(darkPixelRatioThreshold);
                timelapseRegistry.WriteDarkPixelThreshold(darkPixelThreshold);
                timelapseRegistry.WriteMostRecentImageSets(mostRecentImageSets);
                timelapseRegistry.WriteShowCsvDialog(showCsvDialog);
            }

            Registry.CurrentUser.DeleteSubKeyTree(testRootKey);
        }
    }
}