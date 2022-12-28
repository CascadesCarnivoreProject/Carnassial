using Carnassial.Editor;
using Carnassial.Editor.Util;
using Carnassial.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Windows;

namespace Carnassial.UnitTests
{
    [TestClass]
    public class SettingsTests : CarnassialTest
    {
        [TestMethod]
        public void CarnassialUserSettingsReuseUpdate()
        {
            // all tests which can result in user settings changes lock on CarnassialTest.App, here and in UserInterfaceTests
            // Locking on settings causes WPF UX hangs, presumably due to another lock internal to WPF deadlocking.
            lock (CarnassialTest.App)
            {
                try
                {
                    CarnassialSettings.Default.Reset();
                    CarnassialUserSettings carnassialSettings = new();
                    SettingsTests.VerifyDefaultState(carnassialSettings);

                    // modify
                    CarnassialSettings.Default.AudioFeedback = true;
                    int windowLocation = 100;
                    int windowSize = 1000;
                    CarnassialSettings.Default.CarnassialWindowPosition = new Rect(windowLocation, windowLocation, windowSize, windowSize).ToString();
                    double controlGridWidth = CarnassialSettings.Default.ControlGridWidth + 22;
                    CarnassialSettings.Default.ControlGridWidth = controlGridWidth;
                    carnassialSettings.CustomSelectionTermCombiningOperator = Database.LogicalOperator.Or;
                    double modifiedDarkPixelRatioThreshold = CarnassialSettings.Default.DarkLuminosityThreshold + 1.0;
                    CarnassialSettings.Default.DarkLuminosityThreshold = modifiedDarkPixelRatioThreshold;
                    string databasePath = Path.Combine(this.WorkingDirectory, Constant.File.DefaultFileDatabaseFileName);
                    carnassialSettings.MostRecentImageSets.SetMostRecent(databasePath);
                    CarnassialSettings.Default.OrderFilesByDateTime = true;
                    CarnassialSettings.Default.SkipFileClassification = true;
                    CarnassialSettings.Default.SuppressFileCountOnImportDialog = true;
                    CarnassialSettings.Default.SuppressImportPrompt = true;
                    CarnassialSettings.Default.ImageClassificationChangeSlowdown = Constant.ThrottleValues.ImageClassificationSlowdownMinimum;
                    carnassialSettings.Throttles.SetDesiredImageRendersPerSecond(Constant.ThrottleValues.DesiredMaximumImageRendersPerSecondUpperBound);
                    CarnassialSettings.Default.VideoSlowdown = Constant.ThrottleValues.VideoSlowdownMinimum;

                    carnassialSettings.SerializeToSettings();
                    CarnassialSettings.Default.Save();

                    carnassialSettings = new();
                    Assert.IsTrue(CarnassialSettings.Default.AudioFeedback);
                    Rect carnassialWindowPosition = Rect.Parse(CarnassialSettings.Default.CarnassialWindowPosition);
                    Assert.IsTrue((carnassialWindowPosition.X == windowLocation) && (carnassialWindowPosition.Y == windowLocation));
                    Assert.IsTrue((carnassialWindowPosition.Width == windowSize) && (carnassialWindowPosition.Height == windowSize));
                    Assert.IsTrue(CarnassialSettings.Default.ControlGridWidth == controlGridWidth);
                    Assert.IsTrue(carnassialSettings.CustomSelectionTermCombiningOperator == Database.LogicalOperator.Or);
                    Assert.IsTrue(CarnassialSettings.Default.DarkLuminosityThreshold == modifiedDarkPixelRatioThreshold);
                    Assert.IsNotNull(carnassialSettings.MostRecentImageSets);
                    Assert.IsTrue(carnassialSettings.MostRecentImageSets.Count == 1);
                    Assert.IsTrue(carnassialSettings.MostRecentImageSets.TryGetMostRecent(out string? mostRecentDatabasePath));
                    Assert.IsTrue(mostRecentDatabasePath == databasePath);
                    Assert.IsTrue(CarnassialSettings.Default.OrderFilesByDateTime);
                    Assert.IsTrue(CarnassialSettings.Default.SkipFileClassification);
                    Assert.IsTrue(CarnassialSettings.Default.SuppressFileCountOnImportDialog);
                    Assert.IsTrue(CarnassialSettings.Default.SuppressImportPrompt);
                    Assert.IsTrue(CarnassialSettings.Default.ImageClassificationChangeSlowdown == Constant.ThrottleValues.ImageClassificationSlowdownMinimum);
                    Assert.IsTrue(CarnassialSettings.Default.DesiredImageRendersPerSecond == Constant.ThrottleValues.DesiredMaximumImageRendersPerSecondUpperBound);
                    Assert.IsTrue(CarnassialSettings.Default.VideoSlowdown == Constant.ThrottleValues.VideoSlowdownMinimum);

                    // revert test user's app.config
                    CarnassialSettings.Default.CarnassialWindowPosition = new Rect(0, 0, 1350, 900).ToString();
                    CarnassialSettings.Default.ControlGridWidth = CarnassialSettings.Default.ControlGridWidth;
                    carnassialSettings.CustomSelectionTermCombiningOperator = Database.LogicalOperator.And;
                    CarnassialSettings.Default.DarkLuminosityThreshold = CarnassialSettings.Default.DarkLuminosityThreshold;
                    Assert.IsTrue(carnassialSettings.MostRecentImageSets.TryRemove(databasePath));
                    CarnassialSettings.Default.OrderFilesByDateTime = false;
                    CarnassialSettings.Default.SkipFileClassification = false;
                    CarnassialSettings.Default.SuppressFileCountOnImportDialog = false;
                    CarnassialSettings.Default.SuppressImportPrompt = false;
                    CarnassialSettings.Default.ImageClassificationChangeSlowdown = Constant.ThrottleValues.ImageClassificationSlowdownMinimum;
                    carnassialSettings.Throttles.SetDesiredImageRendersPerSecond(Constant.ThrottleValues.DesiredMaximumImageRendersPerSecondUpperBound);
                    CarnassialSettings.Default.VideoSlowdown = Constant.ThrottleValues.VideoSlowdownMinimum;

                    CarnassialSettings.Default.Reset();
                    CarnassialSettings.Default.Save();

                    carnassialSettings = new();
                    SettingsTests.VerifyDefaultState(carnassialSettings);
                }
                catch
                {
                    CarnassialSettings.Default.Reset();
                    throw;
                }
            }
        }

        [TestMethod]
        public void EditorUserSettingsReuseUpdate()
        {
            lock (CarnassialTest.App)
            {
                try
                {
                    EditorSettings.Default.Reset();
                    EditorUserSettings editorSettings = new();
                    Assert.IsNotNull(editorSettings.MostRecentTemplates);
                    Assert.IsTrue(editorSettings.MostRecentTemplates.Count == 0);
                    Assert.IsTrue(editorSettings.MostRecentTemplates.TryGetMostRecent(out string? _) == false);

                    // modify
                    string templatePath = Path.Combine(this.WorkingDirectory, Constant.File.DefaultTemplateDatabaseFileName);
                    editorSettings.MostRecentTemplates.SetMostRecent(templatePath);

                    editorSettings.SerializeToSettings();
                    EditorSettings.Default.Save();

                    editorSettings = new();
                    Assert.IsNotNull(editorSettings.MostRecentTemplates);
                    Assert.IsTrue(editorSettings.MostRecentTemplates.Count == 1);
                    Assert.IsTrue(editorSettings.MostRecentTemplates.TryGetMostRecent(out string? mostRecentTemplatePath));
                    Assert.IsTrue(String.Equals(mostRecentTemplatePath, templatePath, StringComparison.Ordinal));

                    // revert test user's app.config
                    EditorSettings.Default.Reset();
                    EditorSettings.Default.Save();

                    editorSettings = new();
                    Assert.IsTrue(editorSettings.MostRecentTemplates.Count == 0);
                    Assert.IsTrue(editorSettings.MostRecentTemplates.TryGetMostRecent(out string? _) == false);
                }
                catch
                {
                    EditorSettings.Default.Reset();
                    throw;
                }
            }
        }

        private static void VerifyDefaultState(CarnassialUserSettings userSettings)
        {
            Assert.IsNotNull(userSettings.MostRecentImageSets);
            Assert.IsTrue(userSettings.MostRecentImageSets.Count == 0);
            Assert.IsFalse(userSettings.MostRecentImageSets.TryGetMostRecent(out string? mostRecentDatabasePath));
            Assert.IsNull(mostRecentDatabasePath);

            Rect carnassialWindowPosition = Rect.Parse(CarnassialSettings.Default.CarnassialWindowPosition);
            Assert.IsTrue(CarnassialSettings.Default.AudioFeedback == false);
            Assert.IsTrue((carnassialWindowPosition.X) == 0 && (carnassialWindowPosition.Y == 0));
            Assert.IsTrue((carnassialWindowPosition.Width == 1350) && (carnassialWindowPosition.Height == 900));
            Assert.IsTrue(CarnassialSettings.Default.DarkLuminosityThreshold == Constant.Images.DarkLuminosityThresholdDefault);
            Assert.IsTrue(CarnassialSettings.Default.DesiredImageRendersPerSecond == TestConstant.ThrottleValues.DesiredMaximumImageRendersPerSecondDefault);
            Assert.IsTrue(CarnassialSettings.Default.ImageClassificationChangeSlowdown == TestConstant.ThrottleValues.ImageClassificationSlowdownDefault);
            Assert.IsTrue(CarnassialSettings.Default.OrderFilesByDateTime == false);
            Assert.IsTrue(CarnassialSettings.Default.SkipFileClassification == false);
            Assert.IsTrue(CarnassialSettings.Default.SuppressImportPrompt == false);
            Assert.IsTrue(CarnassialSettings.Default.SuppressFileCountOnImportDialog == false);
            Assert.IsTrue(CarnassialSettings.Default.VideoSlowdown == TestConstant.ThrottleValues.VideoSlowdownDefault);
        }
    }
}