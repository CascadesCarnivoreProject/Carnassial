using Carnassial.Editor.Util;
using Carnassial.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;
using System.IO;
using System.Windows;

namespace Carnassial.UnitTests
{
    [TestClass]
    public class RegistryTests : CarnassialTest
    {
        /// <summary>
        /// Sanity test against whatever the current state of Carnassial's registry keys is.
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
            string testRootKey = Constant.Registry.RootKey + "CarnassialUnitTest";
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
            userSettings.CarnassialWindowPosition = new Rect(windowLocation, windowLocation, windowSize, windowSize);
            userSettings.CustomSelectionTermCombiningOperator = Database.CustomSelectionOperator.Or;
            double modifiedDarkPixelRatioThreshold = userSettings.DarkPixelRatioThreshold + 1.0;
            userSettings.DarkPixelRatioThreshold = modifiedDarkPixelRatioThreshold;
            byte modifiedDarkPixelThreshold = (byte)(userSettings.DarkPixelThreshold + 1);
            userSettings.DarkPixelThreshold = modifiedDarkPixelThreshold;
            string databasePath = Path.Combine(this.WorkingDirectory, Constant.File.DefaultFileDatabaseFileName);
            userSettings.MostRecentImageSets.SetMostRecent(databasePath);
            userSettings.OrderFilesByDateTime = true;
            userSettings.SkipDarkImagesCheck = true;
            userSettings.SuppressAmbiguousDatesDialog = true;
            userSettings.SuppressFileCountOnImportDialog = true;
            userSettings.SuppressSpreadsheetImportPrompt = true;
            userSettings.Throttles.SetDesiredImageRendersPerSecond(Constant.ThrottleValues.DesiredMaximumImageRendersPerSecondUpperBound);

            userSettings.WriteToRegistry();
            userSettings.ReadFromRegistry();

            Assert.IsTrue(userSettings.AudioFeedback);
            Assert.IsTrue(userSettings.CarnassialWindowPosition.X == windowLocation && userSettings.CarnassialWindowPosition.Y == windowLocation);
            Assert.IsTrue(userSettings.CarnassialWindowPosition.Width == windowSize && userSettings.CarnassialWindowPosition.Height == windowSize);
            Assert.IsTrue(userSettings.CustomSelectionTermCombiningOperator == Database.CustomSelectionOperator.Or);
            Assert.IsTrue(userSettings.DarkPixelRatioThreshold == modifiedDarkPixelRatioThreshold);
            Assert.IsTrue(userSettings.DarkPixelThreshold == modifiedDarkPixelThreshold);
            Assert.IsNotNull(userSettings.MostRecentImageSets);
            Assert.IsTrue(userSettings.MostRecentImageSets.Count == 1);
            string mostRecentDatabasePath;
            Assert.IsTrue(userSettings.MostRecentImageSets.TryGetMostRecent(out mostRecentDatabasePath));
            Assert.IsTrue(mostRecentDatabasePath == databasePath);
            Assert.IsTrue(userSettings.OrderFilesByDateTime);
            Assert.IsTrue(userSettings.SkipDarkImagesCheck);
            Assert.IsTrue(userSettings.SuppressAmbiguousDatesDialog);
            Assert.IsTrue(userSettings.SuppressFileCountOnImportDialog);
            Assert.IsTrue(userSettings.SuppressSpreadsheetImportPrompt);
            Assert.IsTrue(userSettings.Throttles.DesiredImageRendersPerSecond == Constant.ThrottleValues.DesiredMaximumImageRendersPerSecondUpperBound);

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
            string testRootKey = Constant.Registry.RootKey + "EditorUnitTest";
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
            string templatePath = Path.Combine(this.WorkingDirectory, Constant.File.DefaultTemplateDatabaseFileName);
            editorRegistry.MostRecentTemplates.SetMostRecent(templatePath);
            editorRegistry.WriteToRegistry();
            editorRegistry.ReadFromRegistry();

            Assert.IsNotNull(editorRegistry.MostRecentTemplates);
            Assert.IsTrue(editorRegistry.MostRecentTemplates.Count == 1);
            Assert.IsTrue(editorRegistry.MostRecentTemplates.TryGetMostRecent(out mostRecentTemplatePath));
            Assert.IsTrue(mostRecentTemplatePath == templatePath);

            Registry.CurrentUser.DeleteSubKeyTree(testRootKey);
        }

        private void VerifyDefaultState(CarnassialUserRegistrySettings userSettings)
        {
            Assert.IsFalse(userSettings.AudioFeedback);
            Assert.IsTrue(userSettings.CarnassialWindowPosition.X == 0 && userSettings.CarnassialWindowPosition.Y == 0);
            Assert.IsTrue(userSettings.CarnassialWindowPosition.Width == 1350 && userSettings.CarnassialWindowPosition.Height == 900);
            Assert.IsTrue(userSettings.DarkPixelRatioThreshold == Constant.Images.DarkPixelRatioThresholdDefault);
            Assert.IsTrue(userSettings.DarkPixelThreshold == Constant.Images.DarkPixelThresholdDefault);
            Assert.IsTrue(userSettings.Throttles.DesiredImageRendersPerSecond == Constant.ThrottleValues.DesiredMaximumImageRendersPerSecondDefault);
            Assert.IsNotNull(userSettings.MostRecentImageSets);
            Assert.IsTrue(userSettings.MostRecentImageSets.Count == 0);
            string mostRecentDatabasePath;
            Assert.IsFalse(userSettings.MostRecentImageSets.TryGetMostRecent(out mostRecentDatabasePath));
            Assert.IsNull(mostRecentDatabasePath);
            Assert.IsFalse(userSettings.OrderFilesByDateTime);
            Assert.IsFalse(userSettings.SkipDarkImagesCheck);
            Assert.IsFalse(userSettings.SuppressAmbiguousDatesDialog);
            Assert.IsFalse(userSettings.SuppressSpreadsheetImportPrompt);
            Assert.IsFalse(userSettings.SuppressFileCountOnImportDialog);
        }
    }
}