﻿using Carnassial.Editor.Util;
using Carnassial.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace Carnassial.UnitTests
{
    [TestClass]
    public class RegistryTests : CarnassialTest
    {
        /// <summary>
        /// Sanity test against whatever the current state of Carnassial' registry keys is.
        /// </summary>
        [TestMethod]
        public void CarnassialProductionKeysRead()
        {
            CarnassialState state = new CarnassialState();
            state.ReadFromRegistry();
        }

        [TestMethod]
        public void CarnassialTestKeysCreateReuseUpdate()
        {
            string testRootKey = Constants.Registry.RootKey + "CarnassialUnitTest";
            using (RegistryKey testKey = Registry.CurrentUser.OpenSubKey(testRootKey))
            {
                if (testKey != null)
                {
                    Registry.CurrentUser.DeleteSubKeyTree(testRootKey);
                }
            }

            CarnassialUserRegistrySettings userSettings = new CarnassialUserRegistrySettings(testRootKey);
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
            int windowLocation = 100; 
            int windowSize = 1000;
            userSettings.CarnassialWindowLocation = new Point(windowLocation, windowLocation);
            userSettings.CarnassialWindowSize = new Size(windowSize, windowSize);
            userSettings.CustomSelectionTermCombiningOperator = Database.CustomSelectionOperator.Or;
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
            userSettings.SuppressSelectedAmbiguousDatesPrompt = true;
            userSettings.SuppressSelectedCsvExportPrompt = true;
            userSettings.SuppressSelectedDarkThresholdPrompt = true;
            userSettings.SuppressSelectedDateTimeFixedCorrectionPrompt = true;
            userSettings.SuppressSelectedDateTimeLinearCorrectionPrompt = true;
            userSettings.SuppressSelectedDaylightSavingsCorrectionPrompt = true;
            userSettings.SuppressSelectedPopulateFieldFromMetadataPrompt = true;
            userSettings.SuppressSelectedRereadDatesFromFilesPrompt = true;
            userSettings.SuppressSelectedSetTimeZonePrompt = true;
            userSettings.Throttles.SetDesiredImageRendersPerSecond(Constants.ThrottleValues.DesiredMaximumImageRendersPerSecondUpperBound);

            userSettings.WriteToRegistry();
            userSettings.ReadFromRegistry();

            Assert.IsTrue(userSettings.AudioFeedback);
            Assert.IsTrue(userSettings.CarnassialWindowLocation.X == windowLocation && userSettings.CarnassialWindowLocation.Y == windowLocation);
            Assert.IsTrue(userSettings.CarnassialWindowSize.Width == windowSize && userSettings.CarnassialWindowSize.Height == windowSize);
            Assert.IsTrue(userSettings.CustomSelectionTermCombiningOperator == Database.CustomSelectionOperator.Or);
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
            Assert.IsTrue(userSettings.SuppressSelectedAmbiguousDatesPrompt);
            Assert.IsTrue(userSettings.SuppressSelectedCsvExportPrompt);
            Assert.IsTrue(userSettings.SuppressSelectedDarkThresholdPrompt);
            Assert.IsTrue(userSettings.SuppressSelectedDateTimeFixedCorrectionPrompt);
            Assert.IsTrue(userSettings.SuppressSelectedDateTimeLinearCorrectionPrompt);
            Assert.IsTrue(userSettings.SuppressSelectedDaylightSavingsCorrectionPrompt);
            Assert.IsTrue(userSettings.SuppressSelectedPopulateFieldFromMetadataPrompt);
            Assert.IsTrue(userSettings.SuppressSelectedRereadDatesFromFilesPrompt);
            Assert.IsTrue(userSettings.SuppressSelectedSetTimeZonePrompt);
            Assert.IsTrue(userSettings.Throttles.DesiredImageRendersPerSecond == Constants.ThrottleValues.DesiredMaximumImageRendersPerSecondUpperBound);

            Registry.CurrentUser.DeleteSubKeyTree(testRootKey);
        }

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

        private void VerifyDefaultState(CarnassialUserRegistrySettings userSettings)
        {
            Assert.IsFalse(userSettings.AudioFeedback);
            Assert.IsTrue(userSettings.CarnassialWindowLocation.X == 0 && userSettings.CarnassialWindowLocation.Y == 0);
            Assert.IsTrue(userSettings.CarnassialWindowSize.Width == 1300 && userSettings.CarnassialWindowSize.Height == 900);
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
            Assert.IsFalse(userSettings.SuppressSelectedAmbiguousDatesPrompt);
            Assert.IsFalse(userSettings.SuppressSelectedCsvExportPrompt);
            Assert.IsFalse(userSettings.SuppressSelectedDarkThresholdPrompt);
            Assert.IsFalse(userSettings.SuppressSelectedDateTimeFixedCorrectionPrompt);
            Assert.IsFalse(userSettings.SuppressSelectedDateTimeLinearCorrectionPrompt);
            Assert.IsFalse(userSettings.SuppressSelectedPopulateFieldFromMetadataPrompt);
            Assert.IsFalse(userSettings.SuppressSelectedRereadDatesFromFilesPrompt);
            Assert.IsFalse(userSettings.SuppressSelectedSetTimeZonePrompt);
        }
    }
}