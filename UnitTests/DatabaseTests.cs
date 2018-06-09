﻿using Carnassial.Data;
using Carnassial.Database;
using Carnassial.Dialog;
using Carnassial.Images;
using Carnassial.Native;
using Carnassial.Util;
using MetadataExtractor.Formats.Exif;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using MetadataTag = MetadataExtractor.Tag;

namespace Carnassial.UnitTests
{
    [TestClass]
    public class DatabaseTests : CarnassialTest
    {
        [TestMethod]
        public void CreateReuseDefaultFileDatabase()
        {
            this.CreateReuseFileDatabase(TestConstant.File.DefaultTemplateDatabaseFileName, TestConstant.File.DefaultFileDatabaseFileName, (FileDatabase fileDatabase) =>
            {
                return this.PopulateDefaultDatabase(fileDatabase);
            });
        }

        private void CreateReuseFileDatabase(string templateDatabaseBaseFileName, string fileDatabaseBaseFileName, Func<FileDatabase, List<FileExpectations>> addFiles)
        {
            // create database for test
            FileDatabase fileDatabase = this.CreateFileDatabase(templateDatabaseBaseFileName, fileDatabaseBaseFileName);
            List<FileExpectations> fileExpectations = addFiles(fileDatabase);

            // sanity coverage of image data table methods
            int deletedFiles = fileDatabase.GetFileCount(FileSelection.MarkedForDeletion);
            Assert.IsTrue(deletedFiles == 0);

            Assert.IsTrue(fileDatabase.GetFileCount(FileSelection.All) == fileExpectations.Count);
            Assert.IsTrue(fileDatabase.GetFileCount(FileSelection.MarkedForDeletion) == 0);
            Dictionary<FileSelection, int> fileCounts = fileDatabase.GetFileCountsBySelection();
            Assert.IsTrue(fileCounts.Count == 4);
            Assert.IsTrue(fileCounts[FileSelection.Corrupt] == 0);
            Assert.IsTrue(fileCounts[FileSelection.Dark] == 0);
            Assert.IsTrue(fileCounts[FileSelection.NoLongerAvailable] == 0);
            Assert.IsTrue(fileCounts[FileSelection.Ok] == fileExpectations.Count);

            FileTable filesToDelete = fileDatabase.GetFilesMarkedForDeletion();
            Assert.IsTrue(filesToDelete.RowCount == 0);

            // check images after initial add and again after reopen and application of selection
            // checks are not performed after last selection in list is applied
            List<ControlRow> counterControls = fileDatabase.Controls.Where(control => control.Type == ControlType.Counter).ToList();
            Assert.IsTrue(counterControls.Count == 4);
            string currentDirectoryName = Path.GetFileName(fileDatabase.FolderPath);
            fileDatabase.SelectFiles(FileSelection.All);
            TimeZoneInfo imageSetTimeZone = fileDatabase.ImageSet.GetTimeZoneInfo();
            foreach (FileSelection nextSelection in new List<FileSelection>() { FileSelection.All, FileSelection.Ok, FileSelection.All })
            {
                Assert.IsTrue(fileDatabase.CurrentlySelectedFileCount == fileExpectations.Count);
                fileDatabase.SelectFiles(FileSelection.All);
                Assert.IsTrue(fileDatabase.Files.RowCount == fileExpectations.Count);
                int displayableFileIndex = fileDatabase.GetCurrentOrNextDisplayableFile(fileDatabase.GetFileOrNextFileIndex(Constant.Database.DefaultFileID));
                Assert.IsTrue(displayableFileIndex == 0);

                for (int fileIndex = 0; fileIndex < fileExpectations.Count; ++fileIndex)
                {
                    // verify file
                    ImageRow file = fileDatabase.Files[fileIndex];
                    FileExpectations fileExpectation = fileExpectations[fileIndex];
                    fileExpectation.Verify(file, imageSetTimeZone);

                    // verify no markers associated with file
                    foreach (ControlRow control in counterControls)
                    {
                        MarkersForCounter markerForCounter = file.GetMarkersForCounter(control.DataLabel);
                        Assert.IsFalse(String.IsNullOrWhiteSpace(markerForCounter.DataLabel));
                        Assert.IsTrue(markerForCounter.Markers.Count == 0);
                    }

                    // retrieval by specific method
                    Assert.IsTrue(fileDatabase.IsFileDisplayable(fileIndex));
                    Assert.IsTrue(fileDatabase.IsFileRowInRange(fileIndex));

                    // retrieval by table
                    fileExpectation.Verify(fileDatabase.Files[fileIndex], imageSetTimeZone);
                 }

                // reopen database for test and refresh images so next iteration of the loop checks state after reload
                Assert.IsTrue(FileDatabase.TryCreateOrOpen(fileDatabase.FilePath, fileDatabase, false, LogicalOperator.And, out fileDatabase));
                fileDatabase.SelectFiles(nextSelection);
                Assert.IsTrue(fileDatabase.Files.RowCount > 0);
            }

            foreach (string dataLabel in fileDatabase.ControlsByDataLabel.Keys)
            {
                List<string> distinctValues = fileDatabase.GetDistinctValuesInFileDataColumn(dataLabel);
                int expectedValues;
                switch (dataLabel)
                {
                    case Constant.DatabaseColumn.DateTime:
                    case Constant.DatabaseColumn.File:
                    case Constant.DatabaseColumn.ID:
                        expectedValues = fileExpectations.Count;
                        break;
                    case Constant.DatabaseColumn.RelativePath:
                        expectedValues = fileExpectations.Select(expectation => expectation.RelativePath).Distinct().Count();
                        break;
                    case Constant.DatabaseColumn.UtcOffset:
                        expectedValues = fileExpectations.Select(expectation => expectation.DateTime.Offset).Distinct().Count();
                        break;
                    case Constant.DatabaseColumn.DeleteFlag:
                    case Constant.DatabaseColumn.ImageQuality:
                    case TestConstant.DefaultDatabaseColumn.Choice3:
                    case TestConstant.DefaultDatabaseColumn.ChoiceNotVisible:
                    case TestConstant.DefaultDatabaseColumn.ChoiceWithCustomDataLabel:
                    case TestConstant.DefaultDatabaseColumn.Counter3:
                    case TestConstant.DefaultDatabaseColumn.CounterNotVisible:
                    case TestConstant.DefaultDatabaseColumn.CounterWithCustomDataLabel:
                    case TestConstant.DefaultDatabaseColumn.Flag0:
                    case TestConstant.DefaultDatabaseColumn.Flag3:
                    case TestConstant.DefaultDatabaseColumn.FlagWithCustomDataLabel:
                    case TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel:
                    case TestConstant.DefaultDatabaseColumn.NoteNotVisible:
                        expectedValues = 1;
                        break;
                    case TestConstant.DefaultDatabaseColumn.Choice0:
                    case TestConstant.DefaultDatabaseColumn.Counter0:
                    case TestConstant.DefaultDatabaseColumn.FlagNotVisible:
                        expectedValues = 2;
                        break;
                    case TestConstant.DefaultDatabaseColumn.Note0:
                    case TestConstant.DefaultDatabaseColumn.Note3:
                        expectedValues = 3;
                        break;
                    default:
                        throw new NotSupportedException(String.Format("Unhandled data label '{0}'.", dataLabel));
                }
                Assert.IsTrue(distinctValues != null && distinctValues.Count == expectedValues);
                Assert.IsTrue(distinctValues.Count == distinctValues.Distinct().Count());
            }

            // sanity coverage of image set table methods
            string originalTimeZoneID = fileDatabase.ImageSet.TimeZone;

            this.VerifyDefaultImageSet(fileDatabase);
            fileDatabase.ImageSet.FileSelection = FileSelection.Custom;
            fileDatabase.ImageSet.MostRecentFileID = -1;
            fileDatabase.AppendToImageSetLog(new StringBuilder("Test log entry."));
            fileDatabase.ImageSet.Options = fileDatabase.ImageSet.Options.SetFlag(ImageSetOptions.Magnifier, true);
            fileDatabase.ImageSet.TimeZone = "Test Time Zone";
            fileDatabase.SyncImageSetToDatabase();
            Assert.IsTrue(fileDatabase.ImageSet.ID == 1);
            Assert.IsTrue(fileDatabase.ImageSet.FileSelection == FileSelection.Custom);
            Assert.IsTrue(fileDatabase.ImageSet.MostRecentFileID == -1);
            Assert.IsTrue(fileDatabase.ImageSet.Log == Constant.Database.ImageSetDefaultLog + "Test log entry.");
            Assert.IsTrue(fileDatabase.ImageSet.Options.HasFlag(ImageSetOptions.Magnifier));
            Assert.IsTrue(fileDatabase.ImageSet.TimeZone == "Test Time Zone");

            fileDatabase.ImageSet.TimeZone = originalTimeZoneID;

            // time and date manipulation
            fileDatabase.SelectFiles(FileSelection.All);
            Assert.IsTrue(fileDatabase.Files.RowCount == fileExpectations.Count);
            List<DateTimeOffset> fileTimesBeforeAdjustment = fileDatabase.GetFileTimes().ToList();
            TimeSpan adjustment = new TimeSpan(0, 1, 2, 3, 0);
            fileDatabase.AdjustFileTimes(adjustment);
            foreach (FileExpectations fileExpectation in fileExpectations)
            {
                fileExpectation.DateTime += adjustment;
            }
            this.VerifyFiles(fileDatabase, fileExpectations, imageSetTimeZone);
            fileDatabase.SelectFiles(FileSelection.All);
            this.VerifyFiles(fileDatabase, fileExpectations, imageSetTimeZone);

            fileTimesBeforeAdjustment = fileDatabase.GetFileTimes().ToList();
            adjustment = new TimeSpan(-1, -2, -3, -4, 0);
            int startRow = 1;
            int endRow = fileDatabase.CurrentlySelectedFileCount - 1;
            fileDatabase.AdjustFileTimes(adjustment, startRow, endRow);
            this.VerifyFileTimeAdjustment(fileTimesBeforeAdjustment, fileDatabase.GetFileTimes().ToList(), startRow, endRow, adjustment);
            fileDatabase.SelectFiles(FileSelection.All);
            this.VerifyFileTimeAdjustment(fileTimesBeforeAdjustment, fileDatabase.GetFileTimes().ToList(), startRow, endRow, adjustment);

            fileDatabase.ExchangeDayAndMonthInFileDates(0, fileDatabase.Files.RowCount - 1);

            // custom selection coverage
            // search terms should be created for all visible controls except Folder, but DateTime gets two
            Assert.IsTrue((fileDatabase.Controls.RowCount - 4) == fileDatabase.CustomSelection.SearchTerms.Count);
            Assert.IsTrue(fileDatabase.CustomSelection.CreateSelect().Where.Count == 0);
            Assert.IsTrue(fileDatabase.GetFileCount(FileSelection.Custom) == -1);

            SearchTerm dateTime = fileDatabase.CustomSelection.SearchTerms.First(term => term.DataLabel == Constant.DatabaseColumn.DateTime);
            dateTime.UseForSearching = true;
            dateTime.DatabaseValue = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
            Assert.IsTrue(fileDatabase.CustomSelection.CreateSelect().Where.Count == 1);
            fileDatabase.SelectFiles(FileSelection.Custom);
            Assert.IsTrue(fileDatabase.Files.RowCount == fileExpectations.Count);

            dateTime.Operator = Constant.SearchTermOperator.Equal;
            Assert.IsTrue(fileDatabase.CustomSelection.CreateSelect().Where.Count == 1);
            fileDatabase.SelectFiles(FileSelection.Custom);
            Assert.IsTrue(fileDatabase.Files.RowCount == 0);

            dateTime.UseForSearching = false;
            fileDatabase.CustomSelection.TermCombiningOperator = LogicalOperator.And;

            SearchTerm fileName = fileDatabase.CustomSelection.SearchTerms.Single(term => term.DataLabel == Constant.DatabaseColumn.File);
            fileName.UseForSearching = true;
            fileName.Operator = Constant.SearchTermOperator.Glob;
            fileName.DatabaseValue = "*" + Constant.File.JpgFileExtension.ToUpperInvariant();
            Assert.IsTrue(fileDatabase.CustomSelection.CreateSelect().Where.Count == 1);
            fileDatabase.SelectFiles(FileSelection.Custom);
            Assert.IsTrue(fileDatabase.Files.RowCount == fileExpectations.Count);

            SearchTerm fileQuality = fileDatabase.CustomSelection.SearchTerms.Single(term => term.DataLabel == Constant.DatabaseColumn.ImageQuality);
            fileQuality.UseForSearching = true;
            fileQuality.Operator = Constant.SearchTermOperator.Equal;
            fileQuality.DatabaseValue = FileSelection.Ok.ToString();
            Assert.IsTrue(fileDatabase.CustomSelection.CreateSelect().Where.Count == 2);
            fileDatabase.SelectFiles(FileSelection.Custom);
            Assert.IsTrue(fileDatabase.Files.RowCount == fileExpectations.Count);

            SearchTerm relativePath = fileDatabase.CustomSelection.SearchTerms.Single(term => term.DataLabel == Constant.DatabaseColumn.RelativePath);
            relativePath.UseForSearching = true;
            relativePath.Operator = Constant.SearchTermOperator.Equal;
            relativePath.DatabaseValue = fileExpectations[0].RelativePath;
            Assert.IsTrue(fileDatabase.CustomSelection.CreateSelect().Where.Count == 3);
            fileDatabase.SelectFiles(FileSelection.Custom);
            Assert.IsTrue(fileDatabase.Files.RowCount == 2);

            SearchTerm markedForDeletion = fileDatabase.CustomSelection.SearchTerms.Single(term => term.DataLabel == Constant.DatabaseColumn.DeleteFlag);
            markedForDeletion.UseForSearching = true;
            markedForDeletion.Operator = Constant.SearchTermOperator.Equal;
            markedForDeletion.DatabaseValue = Boolean.FalseString;
            Assert.IsTrue(fileDatabase.CustomSelection.CreateSelect().Where.Count == 4);
            fileDatabase.SelectFiles(FileSelection.Custom);
            Assert.IsTrue(fileDatabase.Files.RowCount == 2);

            fileQuality.DatabaseValue = FileSelection.Dark.ToString();
            Assert.IsTrue(fileDatabase.CustomSelection.CreateSelect().Where.Count == 4);
            Assert.IsTrue(fileDatabase.GetFileCount(FileSelection.All) == fileExpectations.Count);
            Assert.IsTrue(fileDatabase.GetFileCount(FileSelection.Corrupt) == 0);
            Assert.IsTrue(fileDatabase.GetFileCount(FileSelection.Custom) == 0);
            Assert.IsTrue(fileDatabase.GetFileCount(FileSelection.Dark) == 0);
            Assert.IsTrue(fileDatabase.GetFileCount(FileSelection.MarkedForDeletion) == 0);
            Assert.IsTrue(fileDatabase.GetFileCount(FileSelection.NoLongerAvailable) == 0);
            Assert.IsTrue(fileDatabase.GetFileCount(FileSelection.Ok) == fileExpectations.Count);

            // reread
            fileDatabase.SelectFiles(FileSelection.All);

            // get file for marker testing
            int martenImageID = 1;
            FileExpectations martenExpectations = fileExpectations.Single(expectation => expectation.ID == martenImageID);
            ImageRow martenImage = fileDatabase.Files.Single(file => file.ID == martenImageID);
            martenExpectations.Verify(martenImage, imageSetTimeZone);

            // add markers
            for (int counterIndex = 0; counterIndex < counterControls.Count; ++counterIndex)
            {
                ControlRow counter = counterControls[counterIndex];
                MarkersForCounter markersForCounter = martenImage.GetMarkersForCounter(counter.DataLabel);

                List<Point> expectedPositions = new List<Point>();
                for (int markerIndex = 0; markerIndex < counterIndex; ++markerIndex)
                {
                    int initialCounterCount = markersForCounter.Count;
                    int initialMarkers = markersForCounter.Markers.Count;

                    Point markerPosition = new Point((0.1 * counterIndex) + (0.1 * markerIndex), (0.05 * counterIndex) + (0.1 * markerIndex));
                    markersForCounter.AddMarker(markerPosition);

                    Assert.IsTrue(markersForCounter.Count == initialCounterCount + 1);
                    Assert.IsTrue(markersForCounter.Markers.Count == initialMarkers + 1);

                    Point expectedPosition = new Point(Math.Round(markerPosition.X, 3), Math.Round(markerPosition.Y, 3));
                    expectedPositions.Add(expectedPosition);
                }

                martenExpectations.UserControlsByDataLabel[counter.DataLabel] = markersForCounter.ToDatabaseString();
            }
            martenExpectations.Verify(martenImage, imageSetTimeZone);
            fileDatabase.SyncFileToDatabase(martenImage);
            martenExpectations.Verify(martenImage, imageSetTimeZone);

            // roundtrip
            fileDatabase.SelectFiles(FileSelection.All);
            martenImage = fileDatabase.Files.Single(file => file.ID == martenImageID);
            martenExpectations.Verify(martenImage, imageSetTimeZone);

            // remove last marker
            for (int counterIndex = 0; counterIndex < counterControls.Count; ++counterIndex)
            {
                MarkersForCounter markersForCounter = martenImage.GetMarkersForCounter(counterControls[counterIndex].DataLabel);
                if (markersForCounter.Markers.Count > 0)
                {
                    int initialCounterCount = markersForCounter.Count;
                    int initialMarkers = markersForCounter.Markers.Count;

                    markersForCounter.RemoveMarker(markersForCounter.Markers[markersForCounter.Markers.Count - 1]);

                    Assert.IsTrue(markersForCounter.Count == initialCounterCount - 1);
                    Assert.IsTrue(markersForCounter.Markers.Count == initialMarkers - 1);
                    martenExpectations.UserControlsByDataLabel[markersForCounter.DataLabel] = markersForCounter.ToDatabaseString();
                }
            }
            martenExpectations.Verify(martenImage, imageSetTimeZone);
            fileDatabase.SyncFileToDatabase(martenImage);
            martenExpectations.Verify(martenImage, imageSetTimeZone);

            // roundtrip
            fileDatabase.SelectFiles(FileSelection.All);
            martenImage = fileDatabase.Files.Single(file => file.ID == martenImageID);
            martenExpectations.Verify(martenImage, imageSetTimeZone);
        }

        [TestMethod]
        public void CreateUpdateReuseTemplateDatabase()
        {
            FileDatabase fileDatabase = null;
            TemplateDatabase templateDatabase = null;
            try
            {
                string templateDatabaseBaseFileName = TestConstant.File.DefaultNewTemplateDatabaseFileName;
                templateDatabase = this.CreateTemplateDatabase(templateDatabaseBaseFileName);

                // populate template database
                this.VerifyTemplateDatabase(templateDatabase, templateDatabaseBaseFileName);
                int numberOfStandardControls = Constant.Control.StandardControls.Count;
                Assert.IsTrue(templateDatabase.Controls.RowCount == numberOfStandardControls);
                this.VerifyControls(templateDatabase);

                ControlRow newControl = templateDatabase.AddUserDefinedControl(ControlType.Counter);
                Assert.IsTrue(templateDatabase.Controls.RowCount == numberOfStandardControls + 1);
                this.VerifyControl(newControl);

                newControl = templateDatabase.AddUserDefinedControl(ControlType.FixedChoice);
                Assert.IsTrue(templateDatabase.Controls.RowCount == numberOfStandardControls + 2);
                this.VerifyControl(newControl);

                newControl = templateDatabase.AddUserDefinedControl(ControlType.Flag);
                Assert.IsTrue(templateDatabase.Controls.RowCount == numberOfStandardControls + 3);
                this.VerifyControl(newControl);

                newControl = templateDatabase.AddUserDefinedControl(ControlType.Note);
                Assert.IsTrue(templateDatabase.Controls.RowCount == numberOfStandardControls + 4);
                this.VerifyControl(newControl);
                this.VerifyTemplateDatabase(templateDatabase, templateDatabaseBaseFileName);

                templateDatabase.RemoveUserDefinedControl(templateDatabase.Controls[numberOfStandardControls + 2]);
                Assert.IsTrue(templateDatabase.Controls.RowCount == numberOfStandardControls + 3);
                templateDatabase.RemoveUserDefinedControl(templateDatabase.Controls[numberOfStandardControls + 2]);
                Assert.IsTrue(templateDatabase.Controls.RowCount == numberOfStandardControls + 2);
                templateDatabase.RemoveUserDefinedControl(templateDatabase.Controls[numberOfStandardControls + 0]);
                Assert.IsTrue(templateDatabase.Controls.RowCount == numberOfStandardControls + 1);
                templateDatabase.RemoveUserDefinedControl(templateDatabase.Controls[numberOfStandardControls + 0]);
                Assert.IsTrue(templateDatabase.Controls.RowCount == numberOfStandardControls);
                this.VerifyTemplateDatabase(templateDatabase, templateDatabaseBaseFileName);

                int iterations = 10;
                for (int iteration = 0; iteration < iterations; ++iteration)
                {
                    ControlRow noteControl = templateDatabase.AddUserDefinedControl(ControlType.Note);
                    this.VerifyControl(noteControl);
                    ControlRow flagControl = templateDatabase.AddUserDefinedControl(ControlType.Flag);
                    this.VerifyControl(flagControl);
                    ControlRow choiceControl = templateDatabase.AddUserDefinedControl(ControlType.FixedChoice);
                    choiceControl.List = "DefaultChoice|OtherChoice";
                    templateDatabase.SyncControlToDatabase(choiceControl);
                    this.VerifyControl(choiceControl);
                    ControlRow counterControl = templateDatabase.AddUserDefinedControl(ControlType.Counter);
                    this.VerifyControl(counterControl);
                }

                // modify control and spreadsheet orders
                // control order ends up reverse order from ID, spreadsheet order is alphabetical
                Dictionary<string, long> newControlOrderByDataLabel = new Dictionary<string, long>();
                long controlOrder = templateDatabase.Controls.RowCount;
                for (int row = 0; row < templateDatabase.Controls.RowCount; --controlOrder, ++row)
                {
                    string dataLabel = templateDatabase.Controls[row].DataLabel;
                    newControlOrderByDataLabel.Add(dataLabel, controlOrder);
                }
                templateDatabase.UpdateDisplayOrder(Constant.Control.ControlOrder, newControlOrderByDataLabel);

                List<string> alphabeticalDataLabels = newControlOrderByDataLabel.Keys.ToList();
                alphabeticalDataLabels.Sort();
                Dictionary<string, long> newSpreadsheetOrderByDataLabel = new Dictionary<string, long>();
                long spreadsheetOrder = 0;
                foreach (string dataLabel in alphabeticalDataLabels)
                {
                    newSpreadsheetOrderByDataLabel.Add(dataLabel, ++spreadsheetOrder);
                }
                templateDatabase.UpdateDisplayOrder(Constant.Control.SpreadsheetOrder, newSpreadsheetOrderByDataLabel);
                this.VerifyTemplateDatabase(templateDatabase, templateDatabaseBaseFileName);

                // remove some controls and verify the control and spreadsheet orders are properly updated
                templateDatabase.RemoveUserDefinedControl(templateDatabase.Controls[numberOfStandardControls + 22]);
                templateDatabase.RemoveUserDefinedControl(templateDatabase.Controls[numberOfStandardControls + 16]);
                this.VerifyTemplateDatabase(templateDatabase, templateDatabaseBaseFileName);

                // create a database to capture the current template into its template table
                fileDatabase = this.CreateFileDatabase(templateDatabase, TestConstant.File.DefaultNewFileDatabaseFileName);

                // modify UX properties of some controls by data row manipulation
                int copyableIndex = numberOfStandardControls + (3 * iterations) - 1;
                ControlRow copyableControl = templateDatabase.Controls[copyableIndex];
                bool modifiedCopyable = !copyableControl.Copyable;
                copyableControl.Copyable = modifiedCopyable;
                templateDatabase.SyncControlToDatabase(copyableControl);
                Assert.IsTrue(templateDatabase.Controls[copyableIndex].Copyable == modifiedCopyable);

                int defaultValueIndex = numberOfStandardControls + (3 * iterations) - 4;
                ControlRow defaultValueControl = templateDatabase.Controls[defaultValueIndex];
                string modifiedDefaultValue = "Default value modification roundtrip.";
                defaultValueControl.DefaultValue = modifiedDefaultValue;
                templateDatabase.SyncControlToDatabase(defaultValueControl);
                Assert.IsTrue(templateDatabase.Controls[defaultValueIndex].DefaultValue == modifiedDefaultValue);

                int labelIndex = numberOfStandardControls + (3 * iterations) - 3;
                ControlRow labelControl = templateDatabase.Controls[labelIndex];
                string modifiedLabel = "Label modification roundtrip.";
                labelControl.Label = modifiedLabel;
                templateDatabase.SyncControlToDatabase(labelControl);
                Assert.IsTrue(templateDatabase.Controls[labelIndex].Label == modifiedLabel);

                int listIndex = numberOfStandardControls + (3 * iterations) - 2;
                ControlRow listControl = templateDatabase.Controls[listIndex];
                string modifiedList = listControl.List + "|NewChoice0|NewChoice1";
                listControl.List = modifiedList;
                templateDatabase.SyncControlToDatabase(listControl);
                Assert.IsTrue(templateDatabase.Controls[listIndex].List == modifiedList);

                int tooltipIndex = numberOfStandardControls + (3 * iterations) - 3;
                ControlRow tooltipControl = templateDatabase.Controls[tooltipIndex];
                string modifiedTooltip = "Tooltip modification roundtrip.";
                tooltipControl.Tooltip = modifiedTooltip;
                templateDatabase.SyncControlToDatabase(tooltipControl);
                Assert.IsTrue(templateDatabase.Controls[tooltipIndex].Tooltip == modifiedTooltip);

                int widthIndex = numberOfStandardControls + (3 * iterations) - 2;
                ControlRow widthControl = templateDatabase.Controls[widthIndex];
                int modifiedWidth = 1000;
                widthControl.MaxWidth = modifiedWidth;
                templateDatabase.SyncControlToDatabase(widthControl);
                Assert.IsTrue(templateDatabase.Controls[widthIndex].MaxWidth == modifiedWidth);

                int visibleIndex = numberOfStandardControls + (3 * iterations) - 3;
                ControlRow visibleControl = templateDatabase.Controls[visibleIndex];
                bool modifiedVisible = !visibleControl.Visible;
                visibleControl.Visible = modifiedVisible;
                templateDatabase.SyncControlToDatabase(visibleControl);
                Assert.IsTrue(templateDatabase.Controls[visibleIndex].Visible == modifiedVisible);

                // reopen the template database and check again
                string templateDatabaseFilePath = templateDatabase.FilePath;
                Assert.IsTrue(TemplateDatabase.TryCreateOrOpen(templateDatabaseFilePath, out templateDatabase));
                this.VerifyTemplateDatabase(templateDatabase, templateDatabaseFilePath);
                Assert.IsTrue(templateDatabase.Controls.RowCount == numberOfStandardControls + (4 * iterations) - 2);
                DataTable templateDataTable = templateDatabase.Controls.ExtractDataTable();
                Assert.IsTrue(templateDataTable.Columns.Count == TestConstant.ControlsColumns.Count);
                this.VerifyControls(templateDatabase);
                Assert.IsTrue(templateDatabase.Controls[copyableIndex].Copyable == modifiedCopyable);
                Assert.IsTrue(templateDatabase.Controls[defaultValueIndex].DefaultValue == modifiedDefaultValue);
                Assert.IsTrue(templateDatabase.Controls[labelIndex].Label == modifiedLabel);
                Assert.IsTrue(templateDatabase.Controls[listIndex].List == modifiedList);
                Assert.IsTrue(templateDatabase.Controls[tooltipIndex].Tooltip == modifiedTooltip);
                Assert.IsTrue(templateDatabase.Controls[visibleIndex].Visible == modifiedVisible);
                Assert.IsTrue(templateDatabase.Controls[widthIndex].MaxWidth == modifiedWidth);

                // reopen the file database to synchronize its template table with the modified table in the current template
                Assert.IsTrue(FileDatabase.TryCreateOrOpen(fileDatabase.FilePath, templateDatabase, false, LogicalOperator.And, out fileDatabase));
                Assert.IsTrue(fileDatabase.ControlSynchronizationIssues.Count == 0);
                this.VerifyTemplateDatabase(fileDatabase, fileDatabase.FilePath);
                Assert.IsTrue(fileDatabase.Controls.RowCount == numberOfStandardControls + (4 * iterations) - 2);
                DataTable templateTable = fileDatabase.Controls.ExtractDataTable();
                Assert.IsTrue(templateTable.Columns.Count == TestConstant.ControlsColumns.Count);
                this.VerifyControls(fileDatabase);
                Assert.IsTrue(fileDatabase.Controls[copyableIndex].Copyable == modifiedCopyable);
                Assert.IsTrue(fileDatabase.Controls[defaultValueIndex].DefaultValue == modifiedDefaultValue);
                Assert.IsTrue(fileDatabase.Controls[labelIndex].Label == modifiedLabel);
                Assert.IsTrue(fileDatabase.Controls[listIndex].List == modifiedList);
                Assert.IsTrue(fileDatabase.Controls[tooltipIndex].Tooltip == modifiedTooltip);
                Assert.IsTrue(fileDatabase.Controls[visibleIndex].Visible == modifiedVisible);
                Assert.IsTrue(fileDatabase.Controls[widthIndex].MaxWidth == modifiedWidth);
            }
            finally
            {
                if (fileDatabase != null)
                {
                    fileDatabase.Dispose();
                }
                if (templateDatabase != null)
                {
                    templateDatabase.Dispose();
                }
            }
        }

        [TestMethod]
        public void DateTimeHandling()
        {
            // DateTimeExtensions
            DateTime utcNow = DateTime.UtcNow;
            DateTime utcNowUnspecified = utcNow.AsUnspecifed();
            Assert.IsTrue(utcNow.Date == utcNowUnspecified.Date);
            Assert.IsTrue((utcNow.Hour == utcNowUnspecified.Hour) &&
                          (utcNow.Minute == utcNowUnspecified.Minute) &&
                          (utcNow.Second == utcNowUnspecified.Second) &&
                          (utcNow.Millisecond == utcNowUnspecified.Millisecond));
            Assert.IsTrue(utcNowUnspecified.Kind == DateTimeKind.Unspecified);

            // DateTimeOffsetExtensions
            DateTime utcNowWithoutMicroseconds = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, utcNow.Hour, utcNow.Minute, utcNow.Second, utcNow.Millisecond, DateTimeKind.Utc);
            DateTimeOffset utcNowOffset = new DateTimeOffset(utcNowWithoutMicroseconds);
            TimeZoneInfo minUtcOffsetTimeZone = TimeZoneInfo.FindSystemTimeZoneById(TestConstant.TimeZone.Dateline);
            TimeZoneInfo maxUtcOffsetTimeZone = TimeZoneInfo.FindSystemTimeZoneById(TestConstant.TimeZone.LineIslands);
            DateTimeOffset minUtcOffset = utcNowOffset.SetOffset(minUtcOffsetTimeZone.BaseUtcOffset);
            DateTimeOffset localUtcOffset = utcNowOffset.SetOffset(TimeZoneInfo.Local.BaseUtcOffset);
            DateTimeOffset utcOffsetRoundtrip = utcNowOffset.SetOffset(TimeZoneInfo.Utc.BaseUtcOffset);
            DateTimeOffset maxUtcOffset = utcNowOffset.SetOffset(maxUtcOffsetTimeZone.BaseUtcOffset);

            Assert.IsTrue((minUtcOffset.Date == utcNowOffset.Date) &&
                          (minUtcOffset.TimeOfDay == utcNowOffset.TimeOfDay) &&
                          (minUtcOffset.Offset == minUtcOffsetTimeZone.BaseUtcOffset));
            Assert.IsTrue(minUtcOffset.DateTime == utcNowUnspecified);
            Assert.IsTrue((localUtcOffset.Date == utcNowOffset.Date) &&
                          (localUtcOffset.TimeOfDay == utcNowOffset.TimeOfDay) &&
                          (localUtcOffset.Offset == TimeZoneInfo.Local.BaseUtcOffset));
            Assert.IsTrue(localUtcOffset.DateTime == utcNowUnspecified);
            Assert.IsTrue((utcOffsetRoundtrip.Date == utcNowOffset.Date) &&
                          (utcOffsetRoundtrip.TimeOfDay == utcNowOffset.TimeOfDay) &&
                          (utcOffsetRoundtrip.Offset == TimeZoneInfo.Utc.BaseUtcOffset));
            Assert.IsTrue(utcOffsetRoundtrip.DateTime == utcNowUnspecified);
            Assert.IsTrue(utcOffsetRoundtrip.UtcDateTime == utcNowOffset.UtcDateTime);
            Assert.IsTrue(utcOffsetRoundtrip.UtcDateTime == utcNowUnspecified);
            Assert.IsTrue((maxUtcOffset.Date == utcNowOffset.Date) &&
                          (maxUtcOffset.TimeOfDay == utcNowOffset.TimeOfDay) &&
                          (maxUtcOffset.Offset == maxUtcOffsetTimeZone.BaseUtcOffset));
            Assert.IsTrue(maxUtcOffset.DateTime == utcNowUnspecified);

            // DateTimeHandler
            this.DateTimeHandling(new DateTimeOffset(utcNowUnspecified, minUtcOffsetTimeZone.GetUtcOffset(utcNowUnspecified)), minUtcOffsetTimeZone);

            DateTime now = DateTime.Now;
            DateTime nowWithoutMicroseconds = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second, now.Millisecond, DateTimeKind.Local);
            this.DateTimeHandling(new DateTimeOffset(nowWithoutMicroseconds), TimeZoneInfo.Local);
            DateTime nowUnspecified = now.AsUnspecifed();
            this.DateTimeHandling(new DateTimeOffset(nowUnspecified, TimeZoneInfo.Local.GetUtcOffset(nowUnspecified)), TimeZoneInfo.Local);

            this.DateTimeHandling(new DateTimeOffset(utcNowWithoutMicroseconds), TimeZoneInfo.Utc);

            this.DateTimeHandling(new DateTimeOffset(utcNowUnspecified, maxUtcOffsetTimeZone.GetUtcOffset(utcNowUnspecified)), maxUtcOffsetTimeZone);

            DateTime nowWithoutMilliseconds = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second, DateTimeKind.Local);
            foreach (string format in new List<string>() { "yyyy:MM:dd HH:mm:ss", "ddd MMM dd HH:mm:ss K yyyy" })
            {
                string metadataDateAsString = now.ToString(format);
                Assert.IsTrue(DateTimeHandler.TryParseMetadataDateTaken(metadataDateAsString, TimeZoneInfo.Local, out DateTimeOffset metadataDateParsed));
                Assert.IsTrue((metadataDateParsed.Date == nowWithoutMilliseconds.Date) &&
                              (metadataDateParsed.TimeOfDay == nowWithoutMilliseconds.TimeOfDay) &&
                              (metadataDateParsed.Offset == TimeZoneInfo.Local.GetUtcOffset(nowWithoutMilliseconds)));
            }

            DateTimeOffset swappable = new DateTimeOffset(new DateTime(now.Year, 1, 12, now.Hour, now.Minute, now.Second, now.Millisecond), TimeZoneInfo.Local.BaseUtcOffset);
            Assert.IsTrue(DateTimeHandler.TrySwapDayMonth(swappable, out DateTimeOffset swapped));
            DateTimeOffset notSwappable = new DateTimeOffset(new DateTime(now.Year, 1, 13, now.Hour, now.Minute, now.Second, now.Millisecond), TimeZoneInfo.Local.BaseUtcOffset);
            Assert.IsFalse(DateTimeHandler.TrySwapDayMonth(notSwappable, out swapped));

            string timeSpanDisplayStringLessThanOneDay = DateTimeHandler.ToDisplayTimeSpanString(new TimeSpan(-1, -45, -15));
            string timeSpanDisplayStringOneDay = DateTimeHandler.ToDisplayTimeSpanString(new TimeSpan(1, 13, 00, 18));
            string timeSpanDisplayStringMoreThanOneDay = DateTimeHandler.ToDisplayTimeSpanString(new TimeSpan(-2, -22, -22, -22, -222));
            Assert.IsTrue(timeSpanDisplayStringLessThanOneDay == "-01:45:15");
            Assert.IsTrue(timeSpanDisplayStringOneDay == "1 day 13:00:18");
            Assert.IsTrue(timeSpanDisplayStringMoreThanOneDay == "-2 days -22:22:22");
        }

        private void DateTimeHandling(DateTimeOffset dateTimeOffset, TimeZoneInfo timeZone)
        {
            // database format roundtrips
            string dateTimeAsDatabaseString = DateTimeHandler.ToDatabaseDateTimeString(dateTimeOffset);
            DateTime dateTimeParse = DateTimeHandler.ParseDatabaseDateTimeString(dateTimeAsDatabaseString);
            Assert.IsTrue(DateTimeHandler.TryParseDatabaseDateTime(dateTimeAsDatabaseString, out DateTime dateTimeTryParse));

            Assert.IsTrue(dateTimeParse == dateTimeOffset.UtcDateTime);
            Assert.IsTrue(dateTimeTryParse == dateTimeOffset.UtcDateTime);

            string utcOffsetAsDatabaseString = DateTimeHandler.ToDatabaseUtcOffsetString(dateTimeOffset.Offset);
            TimeSpan utcOffsetParse = DateTimeHandler.ParseDatabaseUtcOffsetString(utcOffsetAsDatabaseString);
            Assert.IsTrue(DateTimeHandler.TryParseDatabaseUtcOffsetString(utcOffsetAsDatabaseString, out TimeSpan utcOffsetTryParse));

            Assert.IsTrue(utcOffsetParse == dateTimeOffset.Offset);
            Assert.IsTrue(utcOffsetTryParse == dateTimeOffset.Offset);

            // display format roundtrips
            string dateTimeAsDisplayString = DateTimeHandler.ToDisplayDateTimeString(dateTimeOffset);
            dateTimeParse = DateTimeHandler.ParseDisplayDateTimeString(dateTimeAsDisplayString);
            Assert.IsTrue(DateTimeHandler.TryParseDisplayDateTime(dateTimeAsDisplayString, out dateTimeTryParse));

            DateTimeOffset dateTimeOffsetWithoutMilliseconds = new DateTimeOffset(new DateTime(dateTimeOffset.Year, dateTimeOffset.Month, dateTimeOffset.Day, dateTimeOffset.Hour, dateTimeOffset.Minute, dateTimeOffset.Second), dateTimeOffset.Offset);
            Assert.IsTrue(dateTimeParse == dateTimeOffsetWithoutMilliseconds.DateTime);
            Assert.IsTrue(dateTimeTryParse == dateTimeOffsetWithoutMilliseconds.DateTime);

            // display only formats
            string dateTimeOffsetAsDisplayString = DateTimeHandler.ToDisplayDateTimeUtcOffsetString(dateTimeOffset);
            string utcOffsetAsDisplayString = DateTimeHandler.ToDisplayUtcOffsetString(dateTimeOffset.Offset);

            Assert.IsTrue(dateTimeOffsetAsDisplayString.Length > Constant.Time.DateTimeDisplayFormat.Length);
            Assert.IsTrue(utcOffsetAsDisplayString.Length >= Constant.Time.UtcOffsetDisplayFormat.Length - 1);
        }

        [TestMethod]
        public void EnumHandling()
        {
            ImageSetOptions options = ImageSetOptions.None;

            options = options.SetFlag(ImageSetOptions.None, false);
            Assert.IsTrue(options == ImageSetOptions.None);
            options = options.SetFlag(ImageSetOptions.None, true);
            Assert.IsTrue(options == ImageSetOptions.None);
            options = options.SetFlag(ImageSetOptions.None, false);
            Assert.IsTrue(options == ImageSetOptions.None);

            options = options.SetFlag(ImageSetOptions.Magnifier, false);
            Assert.IsTrue(options == ImageSetOptions.None);
            options = options.SetFlag(ImageSetOptions.Magnifier, true);
            Assert.IsTrue(options == ImageSetOptions.Magnifier);
            options = options.SetFlag(ImageSetOptions.Magnifier, false);
            Assert.IsTrue(options == ImageSetOptions.None);
        }

        [TestMethod]
        public void FileDatabaseNegative()
        {
            TemplateDatabase templateDatabase = null;
            FileDatabase fileDatabase = null;
            try
            {
                templateDatabase = this.CloneTemplateDatabase(TestConstant.File.DefaultTemplateDatabaseFileName);
                fileDatabase = this.CreateFileDatabase(templateDatabase, TestConstant.File.DefaultFileDatabaseFileName);
                this.PopulateDefaultDatabase(fileDatabase);

                // FileDatabase methods
                int firstDisplayableFile = fileDatabase.GetCurrentOrNextDisplayableFile(fileDatabase.CurrentlySelectedFileCount);
                Assert.IsTrue(firstDisplayableFile == fileDatabase.CurrentlySelectedFileCount - 1);

                int closestDisplayableFile = fileDatabase.GetFileOrNextFileIndex(Int64.MinValue);
                Assert.IsTrue(closestDisplayableFile == 0);
                closestDisplayableFile = fileDatabase.GetFileOrNextFileIndex(Int64.MaxValue);
                Assert.IsTrue(closestDisplayableFile == fileDatabase.CurrentlySelectedFileCount - 1);

                Assert.IsFalse(fileDatabase.IsFileDisplayable(-1));
                Assert.IsFalse(fileDatabase.IsFileDisplayable(fileDatabase.CurrentlySelectedFileCount));

                Assert.IsFalse(fileDatabase.IsFileRowInRange(-1));
                Assert.IsFalse(fileDatabase.IsFileRowInRange(fileDatabase.CurrentlySelectedFileCount));

                ImageRow file = fileDatabase.Files[0];
                FileInfo fileInfo = file.GetFileInfo(fileDatabase.FolderPath);

                // template table synchronization
                // remove choices and change a note to a choice to produce a type failure
                ControlRow choiceControl = templateDatabase.FindControl(TestConstant.DefaultDatabaseColumn.Choice0);
                choiceControl.List = "Choice0|Choice1|Choice2|Choice3|Choice4|Choice5|Choice6|Choice7";
                templateDatabase.SyncControlToDatabase(choiceControl);
                ControlRow noteControl = templateDatabase.FindControl(TestConstant.DefaultDatabaseColumn.Note0);
                noteControl.Type = ControlType.FixedChoice;
                templateDatabase.SyncControlToDatabase(noteControl);

                Assert.IsFalse(FileDatabase.TryCreateOrOpen(fileDatabase.FileName, templateDatabase, false, LogicalOperator.And, out fileDatabase));
                Assert.IsTrue(fileDatabase.ControlSynchronizationIssues.Count == 5);
            }
            finally
            {
                if (fileDatabase != null)
                {
                    fileDatabase.Dispose();
                }
                if (templateDatabase != null)
                {
                    templateDatabase.Dispose();
                }
            }
        }

        [TestMethod]
        public async Task HybridVideoFileIOTransactionsAsync()
        {
            // add files to database
            FileDatabase fileDatabase = this.CreateFileDatabase(TestConstant.File.DefaultTemplateDatabaseFileName, TestConstant.File.DefaultNewFileDatabaseFileName);
            CarnassialState state = new CarnassialState()
            {
                SkipDarkImagesCheck = false
            };
            TimeSpan desiredStatusUpdateInterval = state.Throttles.GetDesiredIntervalBetweenFileLoadProgress();
            string folderToLoad = Path.Combine(this.WorkingDirectory, TestConstant.File.HybridVideoDirectoryName);
            FileInfo[] imagesAndVideos = new DirectoryInfo(folderToLoad).GetFiles();

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            using (AddFilesIOComputeTransactionManager folderLoad = new AddFilesIOComputeTransactionManager(this.UpdateFolderLoadProgress, desiredStatusUpdateInterval))
            {
                folderLoad.FolderPaths.Add(folderToLoad);
                folderLoad.FindFilesToLoad(fileDatabase.FolderPath);
                Assert.IsTrue(folderLoad.FilesToLoad == imagesAndVideos.Length);

                int filesLoaded = await folderLoad.AddFilesAsync(fileDatabase, state, Constant.Images.MinimumRenderWidth);
                Assert.IsTrue(filesLoaded == imagesAndVideos.Length);
                Assert.IsTrue(fileDatabase.Files.RowCount == imagesAndVideos.Length);
                fileDatabase.SelectFiles(FileSelection.All);
                Assert.IsTrue(fileDatabase.Files.RowCount == imagesAndVideos.Length);
                this.TestContext.WriteLine("AddFilesAsync({0}): {1:0.000}s", filesLoaded, stopwatch.Elapsed.TotalSeconds);

                Assert.IsTrue(folderLoad.IODuration >= TimeSpan.Zero);
                Assert.IsTrue(folderLoad.ComputeDuration >= folderLoad.IODuration);
                Assert.IsTrue(folderLoad.DatabaseDuration >= folderLoad.ComputeDuration);
            }
            stopwatch.Stop();

            // components for rereading metadata
            List<DateTimeOffset> fileDateTimes = new List<DateTimeOffset>(imagesAndVideos.Length);
            TimeZoneInfo imageSetTimeZone = fileDatabase.ImageSet.GetTimeZoneInfo();
            ImageRow previousFile = null;
            for (int fileIndex = 0; fileIndex < imagesAndVideos.Length; ++fileIndex)
            {
                FileInfo fileInfo = imagesAndVideos[fileIndex];
                ImageRow file = fileDatabase.Files.Single(fileInfo.Name, TestConstant.File.HybridVideoDirectoryName);
                Assert.IsTrue(String.Equals(file.FileName, fileInfo.Name, StringComparison.OrdinalIgnoreCase));
                DateTimeOffset dateTimeOffsetAsAdded = file.DateTimeOffset;
                bool expectedIsVideo = !fileInfo.Name.EndsWith(Constant.File.JpgFileExtension, StringComparison.OrdinalIgnoreCase);
                Assert.IsTrue(file.IsVideo == expectedIsVideo);
                FileSelection expectedClassification = expectedIsVideo ? FileSelection.Video : FileSelection.Ok;
                Assert.IsTrue(file.ImageQuality == expectedClassification);

                if (expectedIsVideo == false)
                {
                    using (MemoryImage image = await file.LoadAsync(fileDatabase.FolderPath, Constant.Images.MinimumRenderWidth))
                    {
                        stopwatch.Restart();
                        double luminosity = image.GetLuminosityAndColoration(0, out double coloration);
                        file.ImageQuality = new ImageProperties(luminosity, coloration).EvaluateNewClassification(Constant.Images.DarkLuminosityThresholdDefault);
                        stopwatch.Stop();
                        this.TestContext.WriteLine("Classify({0}, {1:0.00}MP): {2}ms", file.FileName, 1E-6 * image.TotalPixels, stopwatch.ElapsedMilliseconds);
                        Assert.IsTrue(file.ImageQuality == FileSelection.Ok);
                    }
                }
                else
                {
                    Assert.IsTrue(file.ImageQuality == FileSelection.Video);
                }

                // for images, verify the date can be found in metadata
                // for videos, verify the date is found in the previous image's metadata or not found if there's no previous image
                FileLoad firstLoad = new FileLoad(file);
                FileLoad secondLoad = new FileLoad((string)null);
                bool isSecondFile = false;
                if (file.IsVideo && (previousFile != null) && (previousFile.IsVideo == false))
                {
                    secondLoad = firstLoad;
                    firstLoad = new FileLoad(previousFile);
                    isSecondFile = true;
                }
                MetadataReadResult metadataReadResult;
                using (FileLoadAtom loadAtom = new FileLoadAtom(file.RelativePath, firstLoad, secondLoad, 0))
                {
                    loadAtom.CreateJpegs(fileDatabase.FolderPath);
                    loadAtom.ReadDateTimeOffsets(fileDatabase.FolderPath, imageSetTimeZone);
                    metadataReadResult = isSecondFile ? loadAtom.Second.MetadataReadResult : loadAtom.First.MetadataReadResult;
                }

                Assert.IsFalse(metadataReadResult.HasFlag(MetadataReadResult.Failed));
                Assert.IsTrue(this.DateTimesWithinOneMillisecond(file.DateTimeOffset, dateTimeOffsetAsAdded));
                if (String.Equals(file.FileName, "06260001.AVI", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(Path.GetFileNameWithoutExtension(file.FileName), "06260048", StringComparison.OrdinalIgnoreCase))
                {
                    // 06260001.AVI, 06260048.AVI, and 06260048.mp4 lack an associated .jpg to read metadata from
                    Assert.IsTrue(metadataReadResult == MetadataReadResult.None);
                    Assert.IsTrue(file.DateTimeOffset.Date >= TestConstant.FileExpectation.HybridVideoFileDate);
                    Assert.IsTrue(file.DateTimeOffset.Date < DateTime.Now);
                }
                else if (String.Equals(Path.GetExtension(file.FileName), Constant.File.JpgFileExtension, StringComparison.OrdinalIgnoreCase))
                {
                    Assert.IsTrue(file.DateTimeOffset.Date == TestConstant.FileExpectation.HybridVideoFileDate);
                    Assert.IsTrue(metadataReadResult == MetadataReadResult.DateTime);
                }
                else
                {
                    Assert.IsTrue(file.DateTimeOffset.Date == TestConstant.FileExpectation.HybridVideoFileDate);
                    Assert.IsTrue(metadataReadResult == MetadataReadResult.DateTimeInferredFromPrevious);
                }

                fileDateTimes.Add(dateTimeOffsetAsAdded);
                previousFile = file;
            }

            // verify add of files already added doesn't duplicate them
            using (AddFilesIOComputeTransactionManager folderLoad = new AddFilesIOComputeTransactionManager(this.UpdateFolderLoadProgress, desiredStatusUpdateInterval))
            {
                folderLoad.FolderPaths.Add(folderToLoad);
                folderLoad.FindFilesToLoad(fileDatabase.FolderPath);
                int filesLoaded = await folderLoad.AddFilesAsync(fileDatabase, state, Constant.Images.MinimumRenderWidth);
                Assert.IsTrue(filesLoaded == 0);
            }
            fileDatabase.SelectFiles(FileSelection.All);
            Assert.IsTrue(fileDatabase.Files.RowCount == imagesAndVideos.Length);

            // reread datetimes
            ObservableArray<DateTimeRereadResult> dateTimeResults = new ObservableArray<DateTimeRereadResult>(imagesAndVideos.Length, DateTimeRereadResult.Default);
            using (DateTimeRereadIOComputeTransactionManager rereadDateTimes = new DateTimeRereadIOComputeTransactionManager(this.UpdateDateTimeRereadProgress, dateTimeResults, desiredStatusUpdateInterval))
            {
                await rereadDateTimes.RereadDateTimesAsync(fileDatabase);
                Assert.IsTrue(rereadDateTimes.FilesCompleted == imagesAndVideos.Length);
                foreach (DateTimeRereadResult feedbackRow in dateTimeResults)
                {
                    Assert.IsNotNull(feedbackRow);
                    Assert.IsFalse(String.IsNullOrWhiteSpace(feedbackRow.FileName));
                    Assert.IsFalse(String.IsNullOrWhiteSpace(feedbackRow.Message));
                }
                Assert.IsTrue(rereadDateTimes.IODuration >= TimeSpan.Zero);
                Assert.IsTrue(rereadDateTimes.ComputeDuration >= rereadDateTimes.IODuration);
                Assert.IsTrue(rereadDateTimes.DatabaseDuration >= rereadDateTimes.ComputeDuration);
            }
            for (int fileIndex = 0; fileIndex < imagesAndVideos.Length; ++fileIndex)
            {
                DateTimeOffset dateTimeOffsetAsAdded = fileDateTimes[fileIndex];
                ImageRow file = fileDatabase.Files[fileIndex];
                Assert.IsTrue(this.DateTimesWithinOneMillisecond(file.DateTimeOffset, dateTimeOffsetAsAdded));
            }

            // reclassify
            using (DarkImagesIOComputeTransaction reclassify = new DarkImagesIOComputeTransaction(this.UpdateReclassifyProgress, desiredStatusUpdateInterval))
            {
                await reclassify.ReclassifyFilesAsync(fileDatabase, state.DarkLuminosityThreshold, Constant.Images.MinimumRenderWidth);
            }
            fileDatabase.SelectFiles(FileSelection.All);
            for (int fileIndex = 0; fileIndex < imagesAndVideos.Length; ++fileIndex)
            {
                FileInfo fileInfo = imagesAndVideos[fileIndex];
                ImageRow file = fileDatabase.Files.Single(fileInfo.Name, TestConstant.File.HybridVideoDirectoryName);
                if (file.IsVideo)
                {
                    Assert.IsTrue(file.ImageQuality == FileSelection.Video);
                }
                else
                {
                    Assert.IsTrue(file.ImageQuality == FileSelection.Ok);
                }
            }

            // read metadata
            MetadataTag note0Tag;
            MetadataTag note3Tag;
            using (JpegImage jpeg = new JpegImage(imagesAndVideos.First(file => JpegImage.IsJpeg(file.FullName)).FullName))
            {
                Assert.IsTrue(jpeg.TryGetMetadata());
                ExifSubIfdDirectory subIfd = jpeg.Metadata.OfType<ExifSubIfdDirectory>().Single();
                note0Tag = subIfd.Tags.Single(tag => tag.Type == ExifSubIfdDirectory.TagExifImageHeight);
                note3Tag = subIfd.Tags.Single(tag => tag.Type == ExifSubIfdDirectory.TagExifImageWidth);
            }
            ObservableArray<MetadataFieldResult> metadataResults = new ObservableArray<MetadataFieldResult>(imagesAndVideos.Length, MetadataFieldResult.Default);
            using (MetadataIOComputeTransactionManager readMetadata = new MetadataIOComputeTransactionManager(this.UpdateMetadataProgress, metadataResults, desiredStatusUpdateInterval))
            {
                await readMetadata.ReadFieldAsync(fileDatabase, TestConstant.DefaultDatabaseColumn.Note0, note0Tag, false);
            }
            fileDatabase.SelectFiles(FileSelection.All);
            foreach (MetadataFieldResult result in metadataResults)
            {
                Assert.IsFalse(Object.ReferenceEquals(result, MetadataFieldResult.Default));
                Assert.IsFalse(String.IsNullOrWhiteSpace(result.FileName));
                Assert.IsFalse(String.IsNullOrWhiteSpace(result.Message));
            }

            metadataResults = new ObservableArray<MetadataFieldResult>(imagesAndVideos.Length, MetadataFieldResult.Default);
            using (MetadataIOComputeTransactionManager readMetadata = new MetadataIOComputeTransactionManager(this.UpdateMetadataProgress, metadataResults, desiredStatusUpdateInterval))
            {
                await readMetadata.ReadFieldAsync(fileDatabase, TestConstant.DefaultDatabaseColumn.Note3, note3Tag, true);
            }
            fileDatabase.SelectFiles(FileSelection.All);
            foreach (MetadataFieldResult result in metadataResults)
            {
                Assert.IsFalse(Object.ReferenceEquals(result, MetadataFieldResult.Default));
                Assert.IsFalse(String.IsNullOrWhiteSpace(result.FileName));
                Assert.IsFalse(String.IsNullOrWhiteSpace(result.Message));
            }
        }

        [TestMethod]
        public void MoveFile()
        {
            // remove any existing files from previous test executions
            string sourceFilePath = Path.Combine(Environment.CurrentDirectory, "DatabaseTests.MoveFile.jpg");
            if (File.Exists(sourceFilePath))
            {
                File.Delete(sourceFilePath);
            }
            string subfolderPath = Path.Combine(Environment.CurrentDirectory, "DatabaseTests.MoveFileFolder");
            if (Directory.Exists(subfolderPath) == false)
            {
                Directory.CreateDirectory(subfolderPath);
            }
            string destinationFilePath = Path.Combine(subfolderPath, Path.GetFileName(sourceFilePath));
            if (File.Exists(destinationFilePath))
            {
                File.Delete(destinationFilePath);
            }

            FileExpectations fileExpectation = new FileExpectations(TestConstant.FileExpectation.DaylightBobcat)
            {
                ID = Constant.Database.InvalidID,
                SkipUserControlVerification = true
            };

            // create ImageRow object for file
            File.Copy(fileExpectation.FileName, sourceFilePath);
            FileInfo fileInfo = new FileInfo(sourceFilePath);

            FileDatabase fileDatabase = this.CreateFileDatabase(TestConstant.File.DefaultTemplateDatabaseFileName, TestConstant.File.DefaultNewFileDatabaseFileName);
            ImageRow file = fileDatabase.Files.CreateAndAppendFile(fileInfo.Name, String.Empty);
            TimeZoneInfo imageSetTimeZone = fileDatabase.ImageSet.GetTimeZoneInfo();
            using (FileLoadAtom loadAtom = new FileLoadAtom(file.RelativePath, new FileLoad(file), new FileLoad((string)null), 0))
            {
                loadAtom.CreateJpegs(fileDatabase.FolderPath);
                loadAtom.ReadDateTimeOffsets(fileDatabase.FolderPath, imageSetTimeZone);
            }

            fileExpectation.FileName = Path.GetFileName(sourceFilePath);
            fileExpectation.Verify(file, imageSetTimeZone);

            // move file
            Assert.IsTrue(file.TryMoveFileToFolder(fileDatabase.FolderPath, subfolderPath));
            fileExpectation.RelativePath = Path.GetFileName(subfolderPath);
            fileExpectation.Verify(file, imageSetTimeZone);

            // move file back
            Assert.IsTrue(file.TryMoveFileToFolder(fileDatabase.FolderPath, fileDatabase.FolderPath));
            fileExpectation.RelativePath = null;
            fileExpectation.Verify(file, imageSetTimeZone);
        }

        [TestMethod]
        public void RoundtripSpreadsheets()
        {
            foreach (string spreadsheetFileExtension in new List<string>() { Constant.File.CsvFileExtension, Constant.File.ExcelFileExtension })
            {
                bool xlsx = spreadsheetFileExtension == Constant.File.ExcelFileExtension;

                // create database, push test images into the database, and load the image data table
                FileDatabase fileDatabase = this.CreateFileDatabase(TestConstant.File.DefaultTemplateDatabaseFileName, TestConstant.File.DefaultNewFileDatabaseFileName);
                List<FileExpectations> fileExpectations = this.PopulateDefaultDatabase(fileDatabase);

                // roundtrip data
                SpreadsheetReaderWriter readerWriter = new SpreadsheetReaderWriter();
                string initialFilePath = this.GetUniqueFilePathForTest(Path.GetFileNameWithoutExtension(Constant.File.DefaultFileDatabaseFileName) + spreadsheetFileExtension);
                if (xlsx)
                {
                    readerWriter.ExportFileDataToXlsx(fileDatabase, initialFilePath);
                }
                else
                {
                    readerWriter.ExportFileDataToCsv(fileDatabase, initialFilePath);
                }

                FileImportResult importResult;
                if (xlsx)
                {
                    importResult = readerWriter.TryImportFileDataFromXlsx(initialFilePath, fileDatabase);
                }
                else
                {
                    importResult = readerWriter.TryImportFileDataFromCsv(initialFilePath, fileDatabase);
                }
                Assert.IsTrue(importResult.Errors.Count == 0);

                // verify File table content hasn't changed
                TimeZoneInfo imageSetTimeZone = fileDatabase.ImageSet.GetTimeZoneInfo();
                for (int fileIndex = 0; fileIndex < fileExpectations.Count; ++fileIndex)
                {
                    ImageRow file = fileDatabase.Files[fileIndex];
                    FileExpectations fileExpectation = fileExpectations[fileIndex];
                    fileExpectation.Verify(file, imageSetTimeZone);
                }

                // verify consistency of .csv export
                string roundtripFilePath = Path.Combine(Path.GetDirectoryName(initialFilePath), Path.GetFileNameWithoutExtension(initialFilePath) + ".Roundtrip" + spreadsheetFileExtension);
                if (xlsx)
                {
                    readerWriter.ExportFileDataToXlsx(fileDatabase, roundtripFilePath);
                }
                else
                {
                    readerWriter.ExportFileDataToCsv(fileDatabase, roundtripFilePath);
                }

                if (xlsx == false)
                {
                    // check .csv content is identical
                    // For .xlsx this isn't meaningful as file internals can change.
                    string initialFileContent = File.ReadAllText(initialFilePath);
                    string roundtripFileContent = File.ReadAllText(roundtripFilePath);
                    Assert.IsTrue(initialFileContent == roundtripFileContent, "Initial and roundtrip {0} files don't match.", spreadsheetFileExtension);
                }

                // merge and refresh in memory table
                int filesBeforeMerge = fileDatabase.CurrentlySelectedFileCount;
                string mergeFilePath = Path.Combine(Path.GetDirectoryName(initialFilePath), Path.GetFileNameWithoutExtension(initialFilePath) + ".FilesToMerge" + spreadsheetFileExtension);
                if (xlsx)
                {
                    importResult = readerWriter.TryImportFileDataFromXlsx(mergeFilePath, fileDatabase);
                }
                else
                {
                    importResult = readerWriter.TryImportFileDataFromCsv(mergeFilePath, fileDatabase);
                }
                Assert.IsTrue(importResult.Errors.Count == 0);

                fileDatabase.SelectFiles(FileSelection.All);
                Assert.IsTrue(fileDatabase.CurrentlySelectedFileCount - filesBeforeMerge == 2);

                // verify merge didn't affect existing file table content
                for (int fileIndex = 0; fileIndex < fileExpectations.Count; ++fileIndex)
                {
                    ImageRow file = fileDatabase.Files[fileIndex];
                    FileExpectations fileExpectation = fileExpectations[fileIndex];
                    fileExpectation.Verify(file, imageSetTimeZone);
                }
            }
        }

        /// <summary>
        /// Backwards compatibility test against editor 2.0.1.5 template database.
        /// </summary>
        [TestMethod]
        public void TemplateDatabase2015()
        {
            // load database
            string templateDatabaseBaseFileName = TestConstant.File.DefaultTemplateDatabaseFileName;
            TemplateDatabase templateDatabase = this.CloneTemplateDatabase(templateDatabaseBaseFileName);

            this.VerifyTemplateDatabase(templateDatabase, templateDatabaseBaseFileName);
        }

        [TestMethod]
        public void TimeZones()
        {
            // no change - daylight savings -> UTC
            // change from Pacific time to mountain standard time as it has the same UTC offset as Pacific daylight savings time
            this.TimeZones(TestConstant.TimeZone.Pacific, TestConstant.TimeZone.Arizona);

            // daylight savings -> UTC
            this.TimeZones(TestConstant.TimeZone.Pacific, "UTC-08");
            this.TimeZones(TestConstant.TimeZone.Pacific, "UTC-09");

            // daylight savings -> daylight savings - hour earlier
            this.TimeZones(TestConstant.TimeZone.Pacific, TestConstant.TimeZone.Mountain);
            // daylight savings -> daylight savings - hour later
            this.TimeZones(TestConstant.TimeZone.Pacific, TestConstant.TimeZone.Alaska);

            // UTC -> UTC - hour earlier
            this.TimeZones(TestConstant.TimeZone.CapeVerde, TestConstant.TimeZone.Utc);
            // UTC -> UTC - hour later
            this.TimeZones(TestConstant.TimeZone.Utc, TestConstant.TimeZone.WestCentralAfrica);

            // UTC -> daylight savings
            this.TimeZones(TestConstant.TimeZone.Utc, TestConstant.TimeZone.Gmt);
        }

        private void TimeZones(string initialTimeZoneID, string secondTimeZoneID)
        {
            // create file database and populate images in initial time zone
            FileDatabase fileDatabase = this.CreateFileDatabase(TestConstant.File.DefaultTemplateDatabaseFileName, Constant.File.DefaultFileDatabaseFileName);
            Assert.IsTrue(fileDatabase.ImageSet.TimeZone == TimeZoneInfo.Local.Id);
            // TimeZoneInfo doesn't implement operator == so Equals() must be called
            Assert.IsTrue(TimeZoneInfo.Local.Equals(fileDatabase.ImageSet.GetTimeZoneInfo()));

            fileDatabase.ImageSet.TimeZone = initialTimeZoneID;
            fileDatabase.SyncImageSetToDatabase();

            TimeZoneInfo initialImageSetTimeZone = TimeZoneInfo.FindSystemTimeZoneById(initialTimeZoneID);
            Assert.IsTrue(fileDatabase.ImageSet.TimeZone == initialTimeZoneID);
            Assert.IsTrue(initialImageSetTimeZone.Equals(fileDatabase.ImageSet.GetTimeZoneInfo()));

            List<FileExpectations> fileExpectations = this.PopulateDefaultDatabase(fileDatabase, true);

            // change to second time zone
            fileDatabase.ImageSet.TimeZone = secondTimeZoneID;
            fileDatabase.SyncImageSetToDatabase();

            TimeZoneInfo secondImageSetTimeZone = TimeZoneInfo.FindSystemTimeZoneById(secondTimeZoneID);
            Assert.IsTrue(fileDatabase.ImageSet.TimeZone == secondTimeZoneID);
            Assert.IsTrue(secondImageSetTimeZone.Equals(fileDatabase.ImageSet.GetTimeZoneInfo()));

            // verify date times of existing images haven't changed
            int initialFileCount = fileDatabase.Files.RowCount;
            this.VerifyFiles(fileDatabase, fileExpectations, initialImageSetTimeZone, initialFileCount, secondImageSetTimeZone);

            // add more images
            ImageRow martenPairImage = this.CreateFile(fileDatabase, secondImageSetTimeZone, TestConstant.FileExpectation.DaylightMartenPair, out MetadataReadResult metadataReadResult);
            Assert.IsTrue(metadataReadResult == MetadataReadResult.DateTime);
            ImageRow coyoteImage = this.CreateFile(fileDatabase, secondImageSetTimeZone, TestConstant.FileExpectation.DaylightCoyote, out metadataReadResult);
            Assert.IsTrue(metadataReadResult == MetadataReadResult.DateTime);

            using (AddFilesTransaction addFiles = fileDatabase.CreateAddFilesTransaction())
            {
                addFiles.AddFiles(new List<FileLoad>() { new FileLoad(martenPairImage), new FileLoad(coyoteImage) });
                addFiles.Commit();
            }
            fileDatabase.SelectFiles(FileSelection.All);

            // generate expectations for new images
            FileExpectations martenPairExpectation = new FileExpectations(TestConstant.FileExpectation.DaylightMartenPair)
            {
                ID = fileExpectations.Count + 1,
                SkipUserControlVerification = true
            };
            fileExpectations.Add(martenPairExpectation);

            FileExpectations daylightCoyoteExpectation = new FileExpectations(TestConstant.FileExpectation.DaylightCoyote)
            {
                ID = fileExpectations.Count + 1,
                SkipUserControlVerification = true
            };
            fileExpectations.Add(daylightCoyoteExpectation);

            // verify new images pick up the current timezone
            this.VerifyFiles(fileDatabase, fileExpectations, initialImageSetTimeZone, initialFileCount, secondImageSetTimeZone);
        }

        private bool DateTimesWithinOneMillisecond(DateTimeOffset actual, DateTimeOffset expected)
        {
            TimeSpan difference = actual - expected;
            if (difference < TimeSpan.Zero)
            {
                difference = difference.Negate();
            }
            return difference <= TimeSpan.FromMilliseconds(1.0);
        }

        private void UpdateDateTimeRereadProgress(ObservableStatus<DateTimeRereadResult> status)
        {
            // for now, do nothing
        }

        private void UpdateFolderLoadProgress(FileLoadStatus status)
        {
            // for now, do nothing
        }

        private void UpdateMetadataProgress(ObservableStatus<MetadataFieldResult> status)
        {
            // for now, do nothing
        }

        private void UpdateReclassifyProgress(ReclassifyStatus status)
        {
            // for now, do nothing
        }

        private void VerifyControl(ControlRow control)
        {
            Assert.IsTrue(control.ControlOrder > 0);
            Assert.IsTrue((control.Copyable == true) || (control.Copyable == false));
            Assert.IsFalse(String.IsNullOrWhiteSpace(control.DataLabel));
            // nothing to sanity check for control.DefaultValue
            Assert.IsTrue(control.ID >= 0);
            Assert.IsFalse(String.IsNullOrWhiteSpace(control.Label));
            // nothing to sanity check for control.List
            Assert.IsTrue(control.SpreadsheetOrder > 0);
            Assert.IsTrue(control.MaxWidth > 0);
            Assert.IsFalse(String.IsNullOrWhiteSpace(control.Tooltip));
            Assert.IsTrue(Enum.IsDefined(typeof(ControlType), control.Type));
            Assert.IsTrue((control.Visible == true) || (control.Visible == false));
        }

        private void VerifyControls(TemplateDatabase database)
        {
            for (int row = 0; row < database.Controls.RowCount; ++row)
            {
                // sanity check control
                ControlRow control = database.Controls[row];
                this.VerifyControl(control);

                // verify controls are sorted in control order and that control order is ones based
                Assert.IsTrue(control.ControlOrder == row + 1);
            }
        }

        private void VerifyFiles(FileDatabase fileDatabase, List<FileExpectations> fileExpectations, TimeZoneInfo imageSetTimeZone)
        {
            for (int file = 0; file < fileExpectations.Count; ++file)
            {
                FileExpectations fileExpectation = fileExpectations[file];
                fileExpectation.Verify(fileDatabase.Files[file], imageSetTimeZone);
            }
        }

        private void VerifyFiles(FileDatabase fileDatabase, List<FileExpectations> fileExpectations, TimeZoneInfo initialImageSetTimeZone, int initialImageCount, TimeZoneInfo secondImageSetTimeZone)
        {
            for (int file = 0; file < fileExpectations.Count; ++file)
            {
                TimeZoneInfo expectedTimeZone = file >= initialImageCount ? secondImageSetTimeZone : initialImageSetTimeZone;
                FileExpectations fileExpectation = fileExpectations[file];
                fileExpectation.Verify(fileDatabase.Files[file], expectedTimeZone);
            }
        }

        private void VerifyFileTimeAdjustment(List<DateTimeOffset> fileTimesBeforeAdjustment, List<DateTimeOffset> fileTimesAfterAdjustment, TimeSpan expectedAdjustment)
        {
            this.VerifyFileTimeAdjustment(fileTimesBeforeAdjustment, fileTimesAfterAdjustment, 0, fileTimesBeforeAdjustment.Count - 1, expectedAdjustment);
        }

        private void VerifyFileTimeAdjustment(List<DateTimeOffset> fileTimesBeforeAdjustment, List<DateTimeOffset> fileTimesAfterAdjustment, int startRow, int endRow, TimeSpan expectedAdjustment)
        {
            for (int row = 0; row < startRow; ++row)
            {
                TimeSpan actualAdjustment = fileTimesAfterAdjustment[row] - fileTimesBeforeAdjustment[row];
                Assert.IsTrue(actualAdjustment == TimeSpan.Zero, "Expected file time not to change but it shifted by {0}.", actualAdjustment);
            }
            for (int row = startRow; row <= endRow; ++row)
            {
                TimeSpan actualAdjustment = fileTimesAfterAdjustment[row] - fileTimesBeforeAdjustment[row];
                Assert.IsTrue(actualAdjustment == expectedAdjustment, "Expected file time to change by {0} but it shifted by {1}.", expectedAdjustment, actualAdjustment);
            }
            for (int row = endRow + 1; row < fileTimesBeforeAdjustment.Count; ++row)
            {
                TimeSpan actualAdjustment = fileTimesAfterAdjustment[row] - fileTimesBeforeAdjustment[row];
                Assert.IsTrue(actualAdjustment == TimeSpan.Zero, "Expected file time not to change but it shifted by {0}.", actualAdjustment);
            }
        }

        private void VerifyMarkers(ImageRow file, List<ControlRow> counterControls, List<List<Point>> expectedMarkerPositions)
        {
            Assert.IsTrue(counterControls.Count == expectedMarkerPositions.Count);
            for (int counterIndex = 0; counterIndex < counterControls.Count; ++counterIndex)
            {
                MarkersForCounter markersForCounter = file.GetMarkersForCounter(counterControls[counterIndex].DataLabel);
                List<Point> expectedPositions = expectedMarkerPositions[counterIndex];
                Assert.IsTrue(markersForCounter.Markers.Count == expectedPositions.Count);
                for (int markerIndex = 0; markerIndex < expectedPositions.Count; ++markerIndex)
                {
                    Marker marker = markersForCounter.Markers[markerIndex];
                    // only Point is persisted to the database so other Marker fields should have default values on read
                    Assert.IsFalse(marker.ShowLabel);
                    Assert.IsTrue(marker.LabelShownPreviously);
                    Assert.IsTrue(marker.DataLabel == markersForCounter.DataLabel);
                    Assert.IsFalse(marker.Emphasize);
                    Assert.IsFalse(marker.Highlight);
                    Assert.IsTrue(marker.Position == expectedPositions[markerIndex]);
                    Assert.IsNull(marker.Tooltip);
                }
            }
        }

        private void VerifyTemplateDatabase(TemplateDatabase templateDatabase, string templateDatabaseBaseFileName)
        {
            // sanity checks
            Assert.IsNotNull(templateDatabase);
            Assert.IsNotNull(templateDatabase.FilePath);
            Assert.IsNotNull(templateDatabase.Controls);

            // FilePath checks
            string templateDatabaseFileName = Path.GetFileName(templateDatabase.FilePath);
            Assert.IsTrue(templateDatabaseFileName.StartsWith(Path.GetFileNameWithoutExtension(templateDatabaseBaseFileName)));
            Assert.IsTrue(templateDatabaseFileName.EndsWith(Path.GetExtension(templateDatabaseBaseFileName)));
            Assert.IsTrue(File.Exists(templateDatabase.FilePath));

            // TemplateTable checks
            DataTable templateDataTable = templateDatabase.Controls.ExtractDataTable();
            Assert.IsTrue(templateDataTable.Columns.Count == TestConstant.ControlsColumns.Count);
            List<long> ids = new List<long>();
            List<long> controlOrders = new List<long>();
            List<long> spreadsheetOrders = new List<long>();
            foreach (ControlRow control in templateDatabase.Controls)
            {
                ids.Add(control.ID);
                controlOrders.Add(control.ControlOrder);
                spreadsheetOrders.Add(control.SpreadsheetOrder);
            }
            List<long> uniqueIDs = ids.Distinct().ToList();
            Assert.IsTrue(ids.Count == uniqueIDs.Count, "Expected {0} unique control IDs but found {1}.  {2} id(s) are duplicated.", ids.Count, uniqueIDs.Count, ids.Count - uniqueIDs.Count);
            List<long> uniqueControlOrders = controlOrders.Distinct().ToList();
            Assert.IsTrue(controlOrders.Count == uniqueControlOrders.Count, "Expected {0} unique control orders but found {1}.  {2} order(s) are duplicated.", controlOrders.Count, uniqueControlOrders.Count, controlOrders.Count - uniqueControlOrders.Count);
            List<long> uniqueSpreadsheetOrders = spreadsheetOrders.Distinct().ToList();
            Assert.IsTrue(spreadsheetOrders.Count == uniqueSpreadsheetOrders.Count, "Expected {0} unique spreadsheet orders but found {1}.  {2} order(s) are duplicated.", spreadsheetOrders.Count, uniqueSpreadsheetOrders.Count, spreadsheetOrders.Count - uniqueSpreadsheetOrders.Count);
        }

        private void VerifyDefaultImageSet(FileDatabase fileDatabase)
        {
            // InitialFolderName - current directory case occurs when the test creates a new file database, UnitTests case when the default .ddb is cloned
            Assert.IsTrue(fileDatabase.ImageSet.FileSelection == FileSelection.All);
            Assert.IsTrue((fileDatabase.ImageSet.InitialFolderName == Path.GetFileName(Environment.CurrentDirectory)) ||
                          (fileDatabase.ImageSet.InitialFolderName == "UnitTests"));
            Assert.IsTrue(fileDatabase.ImageSet.MostRecentFileID == Constant.Database.DefaultFileID);
            Assert.IsTrue(fileDatabase.ImageSet.Log == Constant.Database.ImageSetDefaultLog);
            Assert.IsFalse(fileDatabase.ImageSet.Options.HasFlag(ImageSetOptions.Magnifier));
            Assert.IsTrue(fileDatabase.ImageSet.TimeZone == TimeZoneInfo.Local.Id);
        }
    }
}
