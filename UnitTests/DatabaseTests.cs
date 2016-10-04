using Carnassial.Controls;
using Carnassial.Database;
using Carnassial.Images;
using Carnassial.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media.Imaging;

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

        private void CreateReuseFileDatabase(string templateDatabaseBaseFileName, string fileDatabaseBaseFileName, Func<FileDatabase, List<FileExpectations>> addImages)
        {
            // create database for test
            FileDatabase fileDatabase = this.CreateFileDatabase(templateDatabaseBaseFileName, fileDatabaseBaseFileName);
            List<FileExpectations> imageExpectations = addImages(fileDatabase);

            // sanity coverage of image data table methods
            int deletedImages = fileDatabase.GetImageCount(FileSelection.MarkedForDeletion);
            Assert.IsTrue(deletedImages == 0);

            Assert.IsTrue(fileDatabase.GetImageCount(FileSelection.All) == imageExpectations.Count);
            Assert.IsTrue(fileDatabase.GetImageCount(FileSelection.MarkedForDeletion) == 0);
            Dictionary<FileSelection, int> imageCounts = fileDatabase.GetImageCountsByQuality();
            Assert.IsTrue(imageCounts.Count == 4);
            Assert.IsTrue(imageCounts[FileSelection.CorruptFile] == 0);
            Assert.IsTrue(imageCounts[FileSelection.Dark] == 0);
            Assert.IsTrue(imageCounts[FileSelection.FileNoLongerAvailable] == 0);
            Assert.IsTrue(imageCounts[FileSelection.Ok] == imageExpectations.Count);

            FileTable imagesToDelete = fileDatabase.GetImagesMarkedForDeletion();
            Assert.IsTrue(imagesToDelete.RowCount == 0);

            // check images after initial add and again after reopen and application of selection
            // checks are not performed after last selection in list is applied
            int counterControls = 4;
            string currentDirectoryName = Path.GetFileName(fileDatabase.FolderPath);
            fileDatabase.SelectFiles(FileSelection.All);
            TimeZoneInfo imageSetTimeZone = fileDatabase.ImageSet.GetTimeZone();
            foreach (FileSelection nextSelection in new List<FileSelection>() { FileSelection.All, FileSelection.Ok, FileSelection.Ok })
            {
                Assert.IsTrue(fileDatabase.CurrentlySelectedImageCount == imageExpectations.Count);
                fileDatabase.SelectFiles(FileSelection.All);
                Assert.IsTrue(fileDatabase.Files.RowCount == imageExpectations.Count);
                int firstDisplayableImage = fileDatabase.FindFirstDisplayableImage(Constants.DefaultImageRowIndex);
                Assert.IsTrue(firstDisplayableImage == Constants.DefaultImageRowIndex);

                for (int imageIndex = 0; imageIndex < imageExpectations.Count; ++imageIndex)
                {
                    // verify image
                    ImageRow image = fileDatabase.Files[imageIndex];
                    FileExpectations imageExpectation = imageExpectations[imageIndex];
                    imageExpectation.Verify(image, imageSetTimeZone);

                    // verify no markers associated with image
                    List<MarkersForCounter> markersOnImage = fileDatabase.GetMarkersOnImage(image.ID);
                    Assert.IsTrue(markersOnImage.Count == counterControls);
                    foreach (MarkersForCounter markerForCounter in markersOnImage)
                    {
                        Assert.IsFalse(String.IsNullOrWhiteSpace(markerForCounter.DataLabel));
                        Assert.IsTrue(markerForCounter.Markers.Count == 0);
                    }

                    // retrieval by path
                    FileInfo imageFile = image.GetFileInfo(fileDatabase.FolderPath);
                    Assert.IsTrue(fileDatabase.GetOrCreateImage(imageFile, imageSetTimeZone, out image));

                    // retrieval by specific method
                    // fileDatabase.GetImageValue();
                    Assert.IsTrue(fileDatabase.IsImageDisplayable(imageIndex));
                    Assert.IsTrue(fileDatabase.IsImageRowInRange(imageIndex));

                    // retrieval by table
                    imageExpectation.Verify(fileDatabase.Files[imageIndex], imageSetTimeZone);
                }

                // reopen database for test and refresh images so next iteration of the loop checks state after reload
                fileDatabase = FileDatabase.CreateOrOpen(fileDatabase.FilePath, fileDatabase, CustomSelectionOperator.And);
                fileDatabase.SelectFiles(nextSelection);
                Assert.IsTrue(fileDatabase.Files.RowCount > 0);
            }

            foreach (string dataLabel in fileDatabase.FileTableColumnsByDataLabel.Keys)
            {
                List<string> distinctValues = fileDatabase.GetDistinctValuesInImageColumn(dataLabel);
                int expectedValues;
                switch (dataLabel)
                {
                    case Constants.DatabaseColumn.DateTime:
                    case Constants.DatabaseColumn.File:
                    case Constants.DatabaseColumn.ID:
                        expectedValues = imageExpectations.Count;
                        break;
                    case Constants.DatabaseColumn.RelativePath:
                        expectedValues = imageExpectations.Select(expectation => expectation.RelativePath).Distinct().Count();
                        break;
                    case Constants.DatabaseColumn.UtcOffset:
                        expectedValues = imageExpectations.Select(expectation => expectation.DateTime.Offset).Distinct().Count();
                        break;
                    case Constants.DatabaseColumn.DeleteFlag:
                    case Constants.DatabaseColumn.Folder:
                    case Constants.DatabaseColumn.ImageQuality:
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
                        expectedValues = 1;
                        break;
                    case TestConstant.DefaultDatabaseColumn.Choice0:
                    case TestConstant.DefaultDatabaseColumn.Counter0:
                    case TestConstant.DefaultDatabaseColumn.FlagNotVisible:
                    case TestConstant.DefaultDatabaseColumn.NoteNotVisible:
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
            fileDatabase.ImageSet.ImageSelection = FileSelection.Custom;
            fileDatabase.ImageSet.ImageRowIndex = -1;
            fileDatabase.AppendToImageSetLog(new StringBuilder("Test log entry."));
            fileDatabase.ImageSet.MagnifierEnabled = true;
            fileDatabase.ImageSet.TimeZone = "Test Time Zone";
            fileDatabase.SyncImageSetToDatabase();
            Assert.IsTrue(fileDatabase.ImageSet.ID == 1);
            Assert.IsTrue(fileDatabase.ImageSet.ImageSelection == FileSelection.Custom);
            Assert.IsTrue(fileDatabase.ImageSet.ImageRowIndex == -1);
            Assert.IsTrue(fileDatabase.ImageSet.Log == Constants.Database.ImageSetDefaultLog + "Test log entry.");
            Assert.IsTrue(fileDatabase.ImageSet.MagnifierEnabled);
            Assert.IsTrue(fileDatabase.ImageSet.TimeZone == "Test Time Zone");

            fileDatabase.ImageSet.TimeZone = originalTimeZoneID;

            // date manipulation
            fileDatabase.SelectFiles(FileSelection.All);
            Assert.IsTrue(fileDatabase.Files.RowCount > 0);
            List<DateTimeOffset> imageTimesBeforeAdjustment = fileDatabase.GetImageTimes().ToList();
            TimeSpan adjustment = new TimeSpan(0, 1, 2, 3, 0);
            fileDatabase.AdjustImageTimes(adjustment);
            this.VerifyImageTimeAdjustment(imageTimesBeforeAdjustment, fileDatabase.GetImageTimes().ToList(), adjustment);
            fileDatabase.SelectFiles(FileSelection.All);
            this.VerifyImageTimeAdjustment(imageTimesBeforeAdjustment, fileDatabase.GetImageTimes().ToList(), adjustment);

            imageTimesBeforeAdjustment = fileDatabase.GetImageTimes().ToList();
            adjustment = new TimeSpan(-1, -2, -3, -4, 0);
            int startRow = 1;
            int endRow = fileDatabase.CurrentlySelectedImageCount - 1;
            fileDatabase.AdjustImageTimes(adjustment, startRow, endRow);
            this.VerifyImageTimeAdjustment(imageTimesBeforeAdjustment, fileDatabase.GetImageTimes().ToList(), startRow, endRow, adjustment);
            fileDatabase.SelectFiles(FileSelection.All);
            this.VerifyImageTimeAdjustment(imageTimesBeforeAdjustment, fileDatabase.GetImageTimes().ToList(), startRow, endRow, adjustment);

            fileDatabase.ExchangeDayAndMonthInImageDates();
            fileDatabase.ExchangeDayAndMonthInImageDates(0, fileDatabase.Files.RowCount - 1);

            // custom selection coverage
            // search terms should be created for all visible controls except Folder, but DateTime gets two
            Assert.IsTrue((fileDatabase.Controls.RowCount - 5) == fileDatabase.CustomSelection.SearchTerms.Count);
            Assert.IsTrue(String.IsNullOrEmpty(fileDatabase.CustomSelection.GetImagesWhere()));
            Assert.IsTrue(fileDatabase.GetImageCount(FileSelection.Custom) == -1);

            SearchTerm dateTime = fileDatabase.CustomSelection.SearchTerms.First(term => term.DataLabel == Constants.DatabaseColumn.DateTime);
            dateTime.UseForSearching = true;
            dateTime.DatabaseValue = DateTimeHandler.ToDisplayDateString(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero));
            Assert.IsFalse(String.IsNullOrEmpty(fileDatabase.CustomSelection.GetImagesWhere()));
            fileDatabase.SelectFiles(FileSelection.Custom);
            Assert.IsTrue(fileDatabase.Files.RowCount == imageExpectations.Count);

            dateTime.Operator = Constants.SearchTermOperator.Equal;
            Assert.IsFalse(String.IsNullOrEmpty(fileDatabase.CustomSelection.GetImagesWhere()));
            fileDatabase.SelectFiles(FileSelection.Custom);
            Assert.IsTrue(fileDatabase.Files.RowCount == 0);

            dateTime.UseForSearching = false;
            fileDatabase.CustomSelection.TermCombiningOperator = CustomSelectionOperator.And;

            SearchTerm file = fileDatabase.CustomSelection.SearchTerms.Single(term => term.DataLabel == Constants.DatabaseColumn.File);
            file.UseForSearching = true;
            file.Operator = Constants.SearchTermOperator.Glob;
            file.DatabaseValue = "*" + Constants.File.JpgFileExtension.ToUpperInvariant();
            Assert.IsFalse(String.IsNullOrEmpty(fileDatabase.CustomSelection.GetImagesWhere()));
            fileDatabase.SelectFiles(FileSelection.Custom);
            Assert.IsTrue(fileDatabase.Files.RowCount == imageExpectations.Count);

            SearchTerm imageQuality = fileDatabase.CustomSelection.SearchTerms.Single(term => term.DataLabel == Constants.DatabaseColumn.ImageQuality);
            imageQuality.UseForSearching = true;
            imageQuality.Operator = Constants.SearchTermOperator.Equal;
            imageQuality.DatabaseValue = FileSelection.Ok.ToString();
            Assert.IsFalse(String.IsNullOrEmpty(fileDatabase.CustomSelection.GetImagesWhere()));
            fileDatabase.SelectFiles(FileSelection.Custom);
            Assert.IsTrue(fileDatabase.Files.RowCount == imageExpectations.Count);

            SearchTerm relativePath = fileDatabase.CustomSelection.SearchTerms.Single(term => term.DataLabel == Constants.DatabaseColumn.RelativePath);
            relativePath.UseForSearching = true;
            relativePath.Operator = Constants.SearchTermOperator.Equal;
            relativePath.DatabaseValue = imageExpectations[0].RelativePath;
            Assert.IsFalse(String.IsNullOrEmpty(fileDatabase.CustomSelection.GetImagesWhere()));
            fileDatabase.SelectFiles(FileSelection.Custom);
            Assert.IsTrue(fileDatabase.Files.RowCount == 2);

            SearchTerm markedForDeletion = fileDatabase.CustomSelection.SearchTerms.Single(term => term.ControlType == Constants.DatabaseColumn.DeleteFlag);
            markedForDeletion.UseForSearching = true;
            markedForDeletion.Operator = Constants.SearchTermOperator.Equal;
            markedForDeletion.DatabaseValue = Constants.Boolean.False;
            Assert.IsFalse(String.IsNullOrEmpty(fileDatabase.CustomSelection.GetImagesWhere()));
            fileDatabase.SelectFiles(FileSelection.Custom);
            Assert.IsTrue(fileDatabase.Files.RowCount == 2);

            imageQuality.DatabaseValue = FileSelection.Dark.ToString();
            Assert.IsFalse(String.IsNullOrEmpty(fileDatabase.CustomSelection.GetImagesWhere()));
            Assert.IsTrue(fileDatabase.GetImageCount(FileSelection.All) == imageExpectations.Count);
            Assert.IsTrue(fileDatabase.GetImageCount(FileSelection.CorruptFile) == 0);
            Assert.IsTrue(fileDatabase.GetImageCount(FileSelection.Custom) == 0);
            Assert.IsTrue(fileDatabase.GetImageCount(FileSelection.Dark) == 0);
            Assert.IsTrue(fileDatabase.GetImageCount(FileSelection.MarkedForDeletion) == 0);
            Assert.IsTrue(fileDatabase.GetImageCount(FileSelection.FileNoLongerAvailable) == 0);
            Assert.IsTrue(fileDatabase.GetImageCount(FileSelection.Ok) == imageExpectations.Count);

            // markers
            this.VerifyDefaultMarkerTableContent(fileDatabase, imageExpectations.Count);

            // reread
            fileDatabase.SelectFiles(FileSelection.All);

            int martenImageID = 1;
            List<MarkersForCounter> markersForMartenImage = fileDatabase.GetMarkersOnImage(martenImageID);
            Assert.IsTrue(markersForMartenImage.Count == counterControls);
            foreach (MarkersForCounter markerForCounter in markersForMartenImage)
            {
                Assert.IsFalse(String.IsNullOrWhiteSpace(markerForCounter.DataLabel));
                Assert.IsTrue(markerForCounter.Markers.Count == 0);
            }

            // no op - write empty
            foreach (MarkersForCounter markersForCounter in markersForMartenImage)
            {
                fileDatabase.SetMarkerPositions(martenImageID, markersForCounter);
            }

            // add
            markersForMartenImage = fileDatabase.GetMarkersOnImage(martenImageID);
            Assert.IsTrue(markersForMartenImage.Count == counterControls);
            foreach (MarkersForCounter markerForCounter in markersForMartenImage)
            {
                Assert.IsFalse(String.IsNullOrWhiteSpace(markerForCounter.DataLabel));
                Assert.IsTrue(markerForCounter.Markers.Count == 0);
            }

            List<List<Point>> expectedMarkerPositions = new List<List<Point>>();
            for (int counterIndex = 0; counterIndex < markersForMartenImage.Count; ++counterIndex)
            {
                MarkersForCounter markersForCounter = markersForMartenImage[counterIndex];
                List<Point> expectedPositions = new List<Point>();
                for (int markerIndex = 0; markerIndex < counterIndex; ++markerIndex)
                {
                    Point markerPosition = new Point((0.1 * counterIndex) + (0.1 * markerIndex), (0.05 * counterIndex) + (0.1 * markerIndex));
                    markersForCounter.AddMarker(markerPosition);

                    Point expectedPosition = new Point(Math.Round(markerPosition.X, 3), Math.Round(markerPosition.Y, 3));
                    expectedPositions.Add(expectedPosition);
                }

                fileDatabase.SetMarkerPositions(martenImageID, markersForCounter);
                expectedMarkerPositions.Add(expectedPositions);
            }

            // roundtrip
            markersForMartenImage = fileDatabase.GetMarkersOnImage(martenImageID);
            this.VerifyMarkers(markersForMartenImage, expectedMarkerPositions);

            // remove
            for (int counterIndex = 0; counterIndex < markersForMartenImage.Count; ++counterIndex)
            {
                MarkersForCounter markersForCounter = markersForMartenImage[counterIndex];
                List<Point> expectedPositions = expectedMarkerPositions[counterIndex];

                Assert.IsTrue(markersForCounter.Markers.Count == expectedPositions.Count);
                if (expectedPositions.Count > 0)
                {
                    markersForCounter.RemoveMarker(markersForCounter.Markers[expectedPositions.Count - 1]);
                    fileDatabase.SetMarkerPositions(martenImageID, markersForCounter);

                    expectedPositions.RemoveAt(expectedPositions.Count - 1);
                }
            }

            // roundtrip
            markersForMartenImage = fileDatabase.GetMarkersOnImage(martenImageID);
            this.VerifyMarkers(markersForMartenImage, expectedMarkerPositions);
        }

        [TestMethod]
        public void CreateUpdateReuseTemplateDatabase()
        {
            string templateDatabaseBaseFileName = TestConstant.File.DefaultNewTemplateDatabaseFileName;
            TemplateDatabase templateDatabase = this.CreateTemplateDatabase(templateDatabaseBaseFileName);

            // populate template database
            this.VerifyTemplateDatabase(templateDatabase, templateDatabaseBaseFileName);
            int numberOfStandardControls = Constants.Control.StandardTypes.Count;
            Assert.IsTrue(templateDatabase.Controls.RowCount == numberOfStandardControls);
            this.VerifyControls(templateDatabase);

            ControlRow newControl = templateDatabase.AddUserDefinedControl(Constants.Control.Counter);
            Assert.IsTrue(templateDatabase.Controls.RowCount == numberOfStandardControls + 1);
            this.VerifyControl(newControl);

            newControl = templateDatabase.AddUserDefinedControl(Constants.Control.FixedChoice);
            Assert.IsTrue(templateDatabase.Controls.RowCount == numberOfStandardControls + 2);
            this.VerifyControl(newControl);

            newControl = templateDatabase.AddUserDefinedControl(Constants.Control.Flag);
            Assert.IsTrue(templateDatabase.Controls.RowCount == numberOfStandardControls + 3);
            this.VerifyControl(newControl);

            newControl = templateDatabase.AddUserDefinedControl(Constants.Control.Note);
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
                ControlRow noteControl = templateDatabase.AddUserDefinedControl(Constants.Control.Note);
                this.VerifyControl(noteControl);
                ControlRow flagControl = templateDatabase.AddUserDefinedControl(Constants.Control.Flag);
                this.VerifyControl(flagControl);
                ControlRow choiceControl = templateDatabase.AddUserDefinedControl(Constants.Control.FixedChoice);
                choiceControl.List = "DefaultChoice|OtherChoice";
                templateDatabase.SyncControlToDatabase(choiceControl);
                this.VerifyControl(newControl);
                ControlRow counterControl = templateDatabase.AddUserDefinedControl(Constants.Control.Counter);
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
            templateDatabase.UpdateDisplayOrder(Constants.Control.ControlOrder, newControlOrderByDataLabel);

            List<string> alphabeticalDataLabels = newControlOrderByDataLabel.Keys.ToList();
            alphabeticalDataLabels.Sort();
            Dictionary<string, long> newSpreadsheetOrderByDataLabel = new Dictionary<string, long>();
            long spreadsheetOrder = 0;
            foreach (string dataLabel in alphabeticalDataLabels)
            {
                newSpreadsheetOrderByDataLabel.Add(dataLabel, ++spreadsheetOrder);
            }
            templateDatabase.UpdateDisplayOrder(Constants.Control.SpreadsheetOrder, newSpreadsheetOrderByDataLabel);
            this.VerifyTemplateDatabase(templateDatabase, templateDatabaseBaseFileName);

            // remove some controls and verify the control and spreadsheet orders are properly updated
            templateDatabase.RemoveUserDefinedControl(templateDatabase.Controls[numberOfStandardControls + 22]);
            templateDatabase.RemoveUserDefinedControl(templateDatabase.Controls[numberOfStandardControls + 16]);
            this.VerifyTemplateDatabase(templateDatabase, templateDatabaseBaseFileName);

            // create a database to capture the current template into its template table
            FileDatabase fileDatabase = this.CreateFileDatabase(templateDatabase, TestConstant.File.DefaultNewFileDatabaseFileName);

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
            widthControl.Width = modifiedWidth;
            templateDatabase.SyncControlToDatabase(widthControl);
            Assert.IsTrue(templateDatabase.Controls[widthIndex].Width == modifiedWidth);

            int visibleIndex = numberOfStandardControls + (3 * iterations) - 3;
            ControlRow visibleControl = templateDatabase.Controls[visibleIndex];
            bool modifiedVisible = !visibleControl.Visible;
            visibleControl.Visible = modifiedVisible;
            templateDatabase.SyncControlToDatabase(visibleControl);
            Assert.IsTrue(templateDatabase.Controls[visibleIndex].Visible == modifiedVisible);

            // reopen the template database and check again
            string templateDatabaseFilePath = templateDatabase.FilePath;
            templateDatabase = TemplateDatabase.CreateOrOpen(templateDatabaseFilePath);
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
            Assert.IsTrue(templateDatabase.Controls[widthIndex].Width == modifiedWidth);

            // reopen the image database to synchronize its template table with the modified table in the current template
            fileDatabase = FileDatabase.CreateOrOpen(fileDatabase.FilePath, templateDatabase, CustomSelectionOperator.And);
            Assert.IsTrue(fileDatabase.TemplateSynchronizationIssues.Count == 0);
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
            Assert.IsTrue(fileDatabase.Controls[widthIndex].Width == modifiedWidth);
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
                DateTimeOffset metadataDateParsed;
                Assert.IsTrue(DateTimeHandler.TryParseMetadataDateTaken(metadataDateAsString, TimeZoneInfo.Local, out metadataDateParsed));
                Assert.IsTrue((metadataDateParsed.Date == nowWithoutMilliseconds.Date) &&
                              (metadataDateParsed.TimeOfDay == nowWithoutMilliseconds.TimeOfDay) &&
                              (metadataDateParsed.Offset == TimeZoneInfo.Local.GetUtcOffset(nowWithoutMilliseconds)));
            }

            DateTimeOffset swappable = new DateTimeOffset(new DateTime(now.Year, 1, 12, now.Hour, now.Minute, now.Second, now.Millisecond), TimeZoneInfo.Local.BaseUtcOffset);
            DateTimeOffset swapped;
            Assert.IsTrue(DateTimeHandler.TrySwapDayMonth(swappable, out swapped));
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
            DateTime dateTimeTryParse;
            Assert.IsTrue(DateTimeHandler.TryParseDatabaseDateTime(dateTimeAsDatabaseString, out dateTimeTryParse));

            Assert.IsTrue(dateTimeParse == dateTimeOffset.UtcDateTime);
            Assert.IsTrue(dateTimeTryParse == dateTimeOffset.UtcDateTime);

            string utcOffsetAsDatabaseString = DateTimeHandler.ToDatabaseUtcOffsetString(dateTimeOffset.Offset);
            TimeSpan utcOffsetParse = DateTimeHandler.ParseDatabaseUtcOffsetString(utcOffsetAsDatabaseString);
            TimeSpan utcOffsetTryParse;
            Assert.IsTrue(DateTimeHandler.TryParseDatabaseUtcOffsetString(utcOffsetAsDatabaseString, out utcOffsetTryParse));

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

            Assert.IsTrue(dateTimeOffsetAsDisplayString.Length > Constants.Time.DateFormat.Length + Constants.Time.TimeFormat.Length);
            Assert.IsTrue(utcOffsetAsDisplayString.Length >= Constants.Time.UtcOffsetDisplayFormat.Length - 1);
        }

        [TestMethod]
        public void GenerateControlsAndPropagate()
        {
            List<DatabaseExpectations> databaseExpectations = new List<DatabaseExpectations>()
            {
                new DatabaseExpectations()
                {
                    FileName = Constants.File.DefaultFileDatabaseFileName,
                    TemplateDatabaseFileName = TestConstant.File.DefaultTemplateDatabaseFileName,
                    ExpectedControls = TestConstant.DefaultFileDataColumns.Count - 6
                }
            };

            foreach (DatabaseExpectations databaseExpectation in databaseExpectations)
            {
                FileDatabase fileDatabase = this.CreateFileDatabase(databaseExpectation.TemplateDatabaseFileName, databaseExpectation.FileName);
                DataEntryHandler dataHandler = new DataEntryHandler(fileDatabase);

                DataEntryControls controls = new DataEntryControls();
                controls.CreateControls(fileDatabase, dataHandler);
                Assert.IsTrue(controls.ControlsByDataLabel.Count == databaseExpectation.ExpectedControls, "Expected {0} controls to be generated but {1} were.", databaseExpectation.ExpectedControls, controls.ControlsByDataLabel.Count);

                // check copies aren't possible when the image enumerator's not pointing to an image
                foreach (DataEntryControl control in controls.Controls)
                {
                    Assert.IsFalse(dataHandler.IsCopyForwardPossible(control));
                    Assert.IsFalse(dataHandler.IsCopyFromLastNonEmptyValuePossible(control));
                }

                // check only copy forward is possible when enumerator's on first image
                List<FileExpectations> imageExpectations = this.PopulateDefaultDatabase(fileDatabase);
                Assert.IsTrue(dataHandler.ImageCache.MoveNext());

                List<DataEntryControl> copyableControls = controls.Controls.Where(control => control.Copyable).ToList();
                foreach (DataEntryControl control in copyableControls)
                {
                    Assert.IsTrue(dataHandler.IsCopyForwardPossible(control));
                    Assert.IsFalse(dataHandler.IsCopyFromLastNonEmptyValuePossible(control));
                }

                List<DataEntryControl> notCopyableControls = controls.Controls.Where(control => control.Copyable == false).ToList();
                foreach (DataEntryControl control in notCopyableControls)
                {
                    Assert.IsFalse(dataHandler.IsCopyForwardPossible(control));
                    Assert.IsFalse(dataHandler.IsCopyFromLastNonEmptyValuePossible(control));
                }

                // check only copy last is possible when enumerator's on last image
                // check also copy last is not possible if no previous instance of the field has been filled out
                while (dataHandler.ImageCache.CurrentRow < imageExpectations.Count - 1)
                {
                    Assert.IsTrue(dataHandler.ImageCache.MoveNext());
                }

                foreach (DataEntryControl control in copyableControls)
                {
                    Assert.IsFalse(dataHandler.IsCopyForwardPossible(control));
                    if (control.DataLabel == TestConstant.CarnivoreDatabaseColumn.Pelage ||
                        control.DataLabel == TestConstant.DefaultDatabaseColumn.ChoiceNotVisible ||
                        control.DataLabel == TestConstant.DefaultDatabaseColumn.ChoiceWithCustomDataLabel ||
                        control.DataLabel == TestConstant.DefaultDatabaseColumn.Choice3 ||
                        control.DataLabel == TestConstant.DefaultDatabaseColumn.Counter3 ||
                        control.DataLabel == TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel)
                    {
                        Assert.IsFalse(dataHandler.IsCopyFromLastNonEmptyValuePossible(control));
                    }
                    else
                    {
                        Assert.IsTrue(dataHandler.IsCopyFromLastNonEmptyValuePossible(control));
                    }
                }

                // methods not covered due to requirement of UX interaction
                // dataHandler.CopyForward(control);
                // dataHandler.CopyFromLastValue(control);
                // dataHandler.CopyToAll(control);
            }
        }

        /// <summary>
        /// Coverage of first and second import passes in CarnassialWindow.TryBeginImageFolderLoadAsync() on a mix of image and video files.
        /// </summary>
        [TestMethod]
        public void HybridVideo()
        {
            FileDatabase fileDatabase = this.CreateFileDatabase(TestConstant.File.DefaultTemplateDatabaseFileName, TestConstant.File.DefaultNewFileDatabaseFileName);
            TimeZoneInfo imageSetTimeZone = fileDatabase.ImageSet.GetTimeZone();
            List<ImageRow> imagesToInsert = new List<ImageRow>();
            FileInfo[] imagesAndVideos = new DirectoryInfo(Path.Combine(this.WorkingDirectory, TestConstant.File.HybridVideoDirectoryName)).GetFiles();
            foreach (FileInfo imageFile in imagesAndVideos)
            {
                ImageRow imageRow;
                Assert.IsFalse(fileDatabase.GetOrCreateImage(imageFile, imageSetTimeZone, out imageRow));

                BitmapSource bitmapSource = imageRow.LoadBitmap(fileDatabase.FolderPath);
                WriteableBitmap writeableBitmap = bitmapSource.AsWriteable();
                Assert.IsFalse(writeableBitmap.IsBlack());
                imageRow.ImageQuality = writeableBitmap.IsDark(Constants.Images.DarkPixelThresholdDefault, Constants.Images.DarkPixelRatioThresholdDefault);
                Assert.IsTrue(imageRow.ImageQuality == FileSelection.Ok);

                // see if the date can be updated from the metadata
                // currently supported for images but not for videos
                DateTimeAdjustment imageTimeAdjustment = imageRow.TryReadDateTimeOriginalFromMetadata(fileDatabase.FolderPath, imageSetTimeZone);
                if (imageRow.IsVideo)
                {
                    Assert.IsTrue(imageTimeAdjustment == DateTimeAdjustment.MetadataNotUsed);
                }
                else
                {
                    Assert.IsTrue(imageTimeAdjustment == DateTimeAdjustment.MetadataDateAndTimeUsed ||
                                  imageTimeAdjustment == DateTimeAdjustment.MetadataTimeUsed ||
                                  imageTimeAdjustment == DateTimeAdjustment.SameFileAndMetadataTime);
                }

                imagesToInsert.Add(imageRow);
            }

            fileDatabase.AddImages(imagesToInsert, (ImageRow imageProperties, int imageIndex) => { });
            fileDatabase.SelectFiles(FileSelection.All);

            Assert.IsTrue(fileDatabase.Files.RowCount == imagesAndVideos.Length);
            for (int rowIndex = 0; rowIndex < fileDatabase.Files.RowCount; ++rowIndex)
            {
                FileInfo imageFile = imagesAndVideos[rowIndex];
                ImageRow imageRow = fileDatabase.Files[rowIndex];
                bool expectedIsVideo = String.Equals(Path.GetExtension(imageFile.Name), Constants.File.JpgFileExtension, StringComparison.OrdinalIgnoreCase) ? false : true;
                Assert.IsTrue(imageRow.IsVideo == expectedIsVideo);
            }
        }

        [TestMethod]
        public void FileDatabaseVerfication()
        {
            // load database
            string fileDatabaseBaseFileName = TestConstant.File.DefaultFileDatabaseFileName;
            FileDatabase fileDatabase = this.CloneFileDatabase(TestConstant.File.DefaultTemplateDatabaseFileName, fileDatabaseBaseFileName);
            fileDatabase.SelectFiles(FileSelection.All);
            Assert.IsTrue(fileDatabase.Files.RowCount > 0);

            // verify template portion
            this.VerifyTemplateDatabase(fileDatabase, fileDatabaseBaseFileName);
            DefaultTemplateTableExpectation templateTableExpectation = new DefaultTemplateTableExpectation(new Version(2, 2, 0, 0));
            templateTableExpectation.Verify(fileDatabase);

            // verify image set table
            this.VerifyDefaultImageSet(fileDatabase);

            // verify markers table
            int imagesExpected = 2;
            this.VerifyDefaultMarkerTableContent(fileDatabase, imagesExpected);

            MarkerExpectation martenMarkerExpectation = new MarkerExpectation();
            martenMarkerExpectation.ID = 1;
            martenMarkerExpectation.UserDefinedCountersByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Counter0, "0.498,0.575|0.550,0.566|0.584,0.555");
            martenMarkerExpectation.UserDefinedCountersByDataLabel.Add(TestConstant.DefaultDatabaseColumn.CounterWithCustomDataLabel, String.Empty);
            martenMarkerExpectation.UserDefinedCountersByDataLabel.Add(TestConstant.DefaultDatabaseColumn.CounterNotVisible, String.Empty);
            martenMarkerExpectation.UserDefinedCountersByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Counter3, String.Empty);
            martenMarkerExpectation.Verify(fileDatabase.Markers[0]);

            MarkerExpectation bobcatMarkerExpectation = new MarkerExpectation();
            bobcatMarkerExpectation.ID = 2;
            bobcatMarkerExpectation.UserDefinedCountersByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Counter0, String.Empty);
            bobcatMarkerExpectation.UserDefinedCountersByDataLabel.Add(TestConstant.DefaultDatabaseColumn.CounterWithCustomDataLabel, String.Empty);
            bobcatMarkerExpectation.UserDefinedCountersByDataLabel.Add(TestConstant.DefaultDatabaseColumn.CounterNotVisible, String.Empty);
            bobcatMarkerExpectation.UserDefinedCountersByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Counter3, String.Empty);
            bobcatMarkerExpectation.Verify(fileDatabase.Markers[1]);

            // verify Files
            Assert.IsTrue(fileDatabase.Files.ColumnNames.Count() == TestConstant.DefaultFileDataColumns.Count);
            Assert.IsTrue(fileDatabase.Files.RowCount == imagesExpected);

            TimeZoneInfo imageSetTimeZone = fileDatabase.ImageSet.GetTimeZone();
            string initialRootFolderName = "UnitTests";
            FileExpectations martenExpectation = new FileExpectations(TestConstant.ImageExpectation.InfraredMarten);
            martenExpectation.ID = 1;
            martenExpectation.InitialRootFolderName = initialRootFolderName;
            martenExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Counter0, "3");
            martenExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Choice0, "choice c");
            martenExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Note0, "0");
            martenExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Flag0, Constants.Boolean.True);
            martenExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.CounterWithCustomDataLabel, "100");
            martenExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.ChoiceWithCustomDataLabel, "Genus species");
            martenExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel, "custom label");
            martenExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.FlagWithCustomDataLabel, Constants.Boolean.False);
            martenExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.CounterNotVisible, templateTableExpectation.CounterNotVisible.DefaultValue);
            martenExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.ChoiceNotVisible, Constants.ControlDefault.Value);
            martenExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.NoteNotVisible, Constants.ControlDefault.Value);
            martenExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.FlagNotVisible, Constants.ControlDefault.FlagValue);
            martenExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Counter3, "1");
            martenExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Choice3, Constants.ControlDefault.Value);
            martenExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Note3, "note");
            martenExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Flag3, Constants.Boolean.True);
            martenExpectation.Verify(fileDatabase.Files[0], imageSetTimeZone);

            FileExpectations bobcatExpectation = new FileExpectations(TestConstant.ImageExpectation.DaylightBobcat);
            bobcatExpectation.ID = 2;
            bobcatExpectation.InitialRootFolderName = initialRootFolderName;
            bobcatExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Counter0, templateTableExpectation.Counter0.DefaultValue);
            bobcatExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Choice0, "choice a");
            bobcatExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Note0, "1");
            bobcatExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Flag0, Constants.Boolean.True);
            bobcatExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.CounterWithCustomDataLabel, "3");
            bobcatExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.ChoiceWithCustomDataLabel, "with , comma");
            bobcatExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel, Constants.ControlDefault.Value);
            bobcatExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.FlagWithCustomDataLabel, Constants.Boolean.True);
            bobcatExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.CounterNotVisible, templateTableExpectation.CounterNotVisible.DefaultValue);
            bobcatExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.ChoiceNotVisible, Constants.ControlDefault.Value);
            bobcatExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.NoteNotVisible, Constants.ControlDefault.Value);
            bobcatExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.FlagNotVisible, Constants.ControlDefault.FlagValue);
            bobcatExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Counter3, templateTableExpectation.Counter3.DefaultValue);
            bobcatExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Choice3, Constants.ControlDefault.Value);
            bobcatExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Note3, Constants.ControlDefault.Value);
            bobcatExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Flag3, Constants.Boolean.True);
            bobcatExpectation.Verify(fileDatabase.Files[1], imageSetTimeZone);
        }

        [TestMethod]
        public void FileDatabaseNegative()
        {
            TemplateDatabase templateDatabase = this.CloneTemplateDatabase(TestConstant.File.DefaultTemplateDatabaseFileName);
            FileDatabase fileDatabase = this.CreateFileDatabase(templateDatabase, TestConstant.File.DefaultFileDatabaseFileName);
            this.PopulateDefaultDatabase(fileDatabase);

            // FileDatabase methods
            int firstDisplayableImage = fileDatabase.FindFirstDisplayableImage(fileDatabase.CurrentlySelectedImageCount);
            Assert.IsTrue(firstDisplayableImage == fileDatabase.CurrentlySelectedImageCount - 1);

            int closestDisplayableImage = fileDatabase.FindClosestImageRow(Int64.MinValue);
            Assert.IsTrue(closestDisplayableImage == 0);
            closestDisplayableImage = fileDatabase.FindClosestImageRow(Int64.MaxValue);
            Assert.IsTrue(closestDisplayableImage == fileDatabase.CurrentlySelectedImageCount - 1);

            Assert.IsFalse(fileDatabase.IsImageDisplayable(-1));
            Assert.IsFalse(fileDatabase.IsImageDisplayable(fileDatabase.CurrentlySelectedImageCount));

            Assert.IsFalse(fileDatabase.IsImageRowInRange(-1));
            Assert.IsFalse(fileDatabase.IsImageRowInRange(fileDatabase.CurrentlySelectedImageCount));

            ImageRow imageProperties = fileDatabase.Files[0];
            FileInfo imageFile = imageProperties.GetFileInfo(fileDatabase.FolderPath);
            TimeZoneInfo imageSetTimeZone = fileDatabase.ImageSet.GetTimeZone();
            Assert.IsTrue(fileDatabase.GetOrCreateImage(imageFile, imageSetTimeZone, out imageProperties));

            // template table synchronization
            // remove choices and change a note to a choice to produce a type failure
            ControlRow choiceControl = templateDatabase.GetControlFromTemplateTable(TestConstant.DefaultDatabaseColumn.Choice0);
            choiceControl.List = "Choice0|Choice1|Choice2|Choice3|Choice4|Choice5|Choice6|Choice7";
            templateDatabase.SyncControlToDatabase(choiceControl);
            ControlRow noteControl = templateDatabase.GetControlFromTemplateTable(TestConstant.DefaultDatabaseColumn.Note0);
            noteControl.Type = Constants.Control.FixedChoice;
            templateDatabase.SyncControlToDatabase(noteControl);

            fileDatabase = FileDatabase.CreateOrOpen(fileDatabase.FileName, templateDatabase, CustomSelectionOperator.And);
            Assert.IsTrue(fileDatabase.TemplateSynchronizationIssues.Count == 5);
        }

        [TestMethod]
        public void RoundtripCsv()
        {
            // create database, push test images into the database, and load the image data table
            FileDatabase fileDatabase = this.CreateFileDatabase(TestConstant.File.DefaultTemplateDatabaseFileName, TestConstant.File.DefaultNewFileDatabaseFileName);
            List<FileExpectations> imageExpectations = this.PopulateDefaultDatabase(fileDatabase);

            // roundtrip data through .csv
            CsvReaderWriter csvReaderWriter = new CsvReaderWriter();
            string initialCsvFilePath = this.GetUniqueFilePathForTest(Path.GetFileNameWithoutExtension(Constants.File.DefaultFileDatabaseFileName) + Constants.File.CsvFileExtension);
            csvReaderWriter.ExportToCsv(fileDatabase, initialCsvFilePath);
            List<string> importErrors;
            Assert.IsTrue(csvReaderWriter.TryImportFromCsv(initialCsvFilePath, fileDatabase, out importErrors));
            Assert.IsTrue(importErrors.Count == 0);

            // verify Files content hasn't changed
            TimeZoneInfo imageSetTimeZone = fileDatabase.ImageSet.GetTimeZone();
            for (int imageIndex = 0; imageIndex < imageExpectations.Count; ++imageIndex)
            {
                ImageRow image = fileDatabase.Files[imageIndex];
                FileExpectations imageExpectation = imageExpectations[imageIndex];
                imageExpectation.Verify(image, imageSetTimeZone);
            }

            // verify consistency of .csv export
            string roundtripCsvFilePath = Path.Combine(Path.GetDirectoryName(initialCsvFilePath), Path.GetFileNameWithoutExtension(initialCsvFilePath) + "-roundtrip" + Constants.File.CsvFileExtension);
            csvReaderWriter.ExportToCsv(fileDatabase, roundtripCsvFilePath);

            string initialCsv = File.ReadAllText(initialCsvFilePath);
            string roundtripCsv = File.ReadAllText(roundtripCsvFilePath);
            Assert.IsTrue(initialCsv == roundtripCsv, "Initial and roundtrip .csv files don't match.");
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
            // create image database and populate images in initial time zone
            // TimeZoneInfo doesn't implement operator == so Equals() must be called
            FileDatabase fileDatabase = this.CreateFileDatabase(TestConstant.File.DefaultTemplateDatabaseFileName, Constants.File.DefaultFileDatabaseFileName);
            Assert.IsTrue(fileDatabase.ImageSet.TimeZone == TimeZoneInfo.Local.Id);
            Assert.IsTrue(TimeZoneInfo.Local.Equals(fileDatabase.ImageSet.GetTimeZone()));

            fileDatabase.ImageSet.TimeZone = initialTimeZoneID;
            fileDatabase.SyncImageSetToDatabase();

            TimeZoneInfo initialImageSetTimeZone = TimeZoneInfo.FindSystemTimeZoneById(initialTimeZoneID);
            Assert.IsTrue(fileDatabase.ImageSet.TimeZone == initialTimeZoneID);
            Assert.IsTrue(initialImageSetTimeZone.Equals(fileDatabase.ImageSet.GetTimeZone()));

            List<FileExpectations> imageExpectations = this.PopulateDefaultDatabase(fileDatabase, true);

            // change to second time zone
            fileDatabase.ImageSet.TimeZone = secondTimeZoneID;
            fileDatabase.SyncImageSetToDatabase();

            TimeZoneInfo secondImageSetTimeZone = TimeZoneInfo.FindSystemTimeZoneById(secondTimeZoneID);
            Assert.IsTrue(fileDatabase.ImageSet.TimeZone == secondTimeZoneID);
            Assert.IsTrue(secondImageSetTimeZone.Equals(fileDatabase.ImageSet.GetTimeZone()));

            // verify date times of existing images haven't changed
            int initialImageCount = fileDatabase.Files.RowCount;
            this.VerifyImages(fileDatabase, imageExpectations, initialImageSetTimeZone, initialImageCount, secondImageSetTimeZone);

            // add more images
            DateTimeAdjustment timeAdjustment;
            ImageRow martenPairImage = this.CreateImage(fileDatabase, secondImageSetTimeZone, TestConstant.ImageExpectation.DaylightMartenPair, out timeAdjustment);
            ImageRow coyoteImage = this.CreateImage(fileDatabase, secondImageSetTimeZone, TestConstant.ImageExpectation.DaylightCoyote, out timeAdjustment);

            fileDatabase.AddImages(new List<ImageRow>() { martenPairImage, coyoteImage }, null);
            fileDatabase.SelectFiles(FileSelection.All);

            // generate expectations for new images
            string initialRootFolderName = imageExpectations[0].InitialRootFolderName;
            FileExpectations martenPairExpectation = new FileExpectations(TestConstant.ImageExpectation.DaylightMartenPair);
            martenPairExpectation.ID = imageExpectations.Count + 1;
            martenPairExpectation.InitialRootFolderName = initialRootFolderName;
            imageExpectations.Add(martenPairExpectation);

            FileExpectations daylightCoyoteExpectation = new FileExpectations(TestConstant.ImageExpectation.DaylightCoyote);
            daylightCoyoteExpectation.ID = imageExpectations.Count + 1;
            daylightCoyoteExpectation.InitialRootFolderName = initialRootFolderName;
            imageExpectations.Add(daylightCoyoteExpectation);

            // verify new images pick up the current timezone
            this.VerifyImages(fileDatabase, imageExpectations, initialImageSetTimeZone, initialImageCount, secondImageSetTimeZone);
        }

        private void VerifyControl(ControlRow control)
        {
            Assert.IsTrue(control.ControlOrder > 0);
            // nothing to sanity check for control.Copyable
            Assert.IsFalse(String.IsNullOrWhiteSpace(control.DataLabel));
            // nothing to sanity check for control.DefaultValue
            Assert.IsTrue(control.ID >= 0);
            Assert.IsFalse(String.IsNullOrWhiteSpace(control.Label));
            // nothing to sanity check for control.List
            Assert.IsTrue(control.SpreadsheetOrder > 0);
            Assert.IsTrue(control.Width > 0);
            Assert.IsFalse(String.IsNullOrWhiteSpace(control.Tooltip));
            Assert.IsFalse(String.IsNullOrWhiteSpace(control.Type));
            // nothing to sanity check for control.Visible
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

        private void VerifyImages(FileDatabase fileDatabase, List<FileExpectations> imageExpectations, TimeZoneInfo initialImageSetTimeZone, int initialImageCount, TimeZoneInfo secondImageSetTimeZone)
        {
            for (int image = 0; image < imageExpectations.Count; ++image)
            {
                TimeZoneInfo expectedTimeZone = image >= initialImageCount ? secondImageSetTimeZone : initialImageSetTimeZone;
                FileExpectations imageExpectation = imageExpectations[image];
                imageExpectation.Verify(fileDatabase.Files[image], expectedTimeZone);
            }
        }

        private void VerifyImageTimeAdjustment(List<DateTimeOffset> imageTimesBeforeAdjustment, List<DateTimeOffset> imageTimesAfterAdjustment, TimeSpan expectedAdjustment)
        {
            this.VerifyImageTimeAdjustment(imageTimesBeforeAdjustment, imageTimesAfterAdjustment, 0, imageTimesBeforeAdjustment.Count - 1, expectedAdjustment);
        }

        private void VerifyImageTimeAdjustment(List<DateTimeOffset> imageTimesBeforeAdjustment, List<DateTimeOffset> imageTimesAfterAdjustment, int startRow, int endRow, TimeSpan expectedAdjustment)
        {
            for (int row = 0; row < startRow; ++row)
            {
                TimeSpan actualAdjustment = imageTimesAfterAdjustment[row] - imageTimesBeforeAdjustment[row];
                Assert.IsTrue(actualAdjustment == TimeSpan.Zero, "Expected image time not to change but it shifted by {0}.", actualAdjustment);
            }
            for (int row = startRow; row <= endRow; ++row)
            {
                TimeSpan actualAdjustment = imageTimesAfterAdjustment[row] - imageTimesBeforeAdjustment[row];
                Assert.IsTrue(actualAdjustment == expectedAdjustment, "Expected image time to change by {0} but it shifted by {1}.", expectedAdjustment, actualAdjustment);
            }
            for (int row = endRow + 1; row < imageTimesBeforeAdjustment.Count; ++row)
            {
                TimeSpan actualAdjustment = imageTimesAfterAdjustment[row] - imageTimesBeforeAdjustment[row];
                Assert.IsTrue(actualAdjustment == TimeSpan.Zero, "Expected image time not to change but it shifted by {0}.", actualAdjustment);
            }
        }

        private void VerifyMarkers(List<MarkersForCounter> markersOnImage, List<List<Point>> expectedMarkerPositions)
        {
            Assert.IsTrue(markersOnImage.Count == expectedMarkerPositions.Count);
            for (int counterIndex = 0; counterIndex < markersOnImage.Count; ++counterIndex)
            {
                MarkersForCounter markersForCounter = markersOnImage[counterIndex];
                List<Point> expectedPositions = expectedMarkerPositions[counterIndex];
                Assert.IsTrue(markersForCounter.Markers.Count == expectedPositions.Count);
                for (int markerIndex = 0; markerIndex < expectedPositions.Count; ++markerIndex)
                {
                    Marker marker = markersForCounter.Markers[markerIndex];
                    // only Point is persisted to the database so other Marker fields should have default values on read
                    Assert.IsFalse(marker.Annotate);
                    Assert.IsTrue(marker.AnnotationPreviouslyShown);
                    Assert.IsNotNull(marker.Brush);
                    Assert.IsTrue(marker.DataLabel == markersForCounter.DataLabel);
                    Assert.IsFalse(marker.Emphasise);
                    Assert.IsNotNull(marker.Guid);
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
            Assert.IsTrue(fileDatabase.ImageSet.ImageSelection == FileSelection.All);
            Assert.IsTrue(fileDatabase.ImageSet.ImageRowIndex == 0);
            Assert.IsTrue(fileDatabase.ImageSet.Log == Constants.Database.ImageSetDefaultLog);
            Assert.IsFalse(fileDatabase.ImageSet.MagnifierEnabled);
            Assert.IsTrue(fileDatabase.ImageSet.TimeZone == TimeZoneInfo.Local.Id);
        }

        private void VerifyDefaultMarkerTableContent(FileDatabase fileDatabase, int imagesExpected)
        {
            DataTable markers = fileDatabase.Markers.ExtractDataTable();

            List<string> expectedColumns = new List<string>() { Constants.DatabaseColumn.ID };
            foreach (ControlRow control in fileDatabase.Controls)
            {
                if (control.Type == Constants.Control.Counter)
                {
                    expectedColumns.Add(control.DataLabel);
                }
            }

            Assert.IsTrue(markers.Columns.Count == expectedColumns.Count);
            for (int column = 0; column < expectedColumns.Count; ++column)
            {
                Assert.IsTrue(expectedColumns[column] == markers.Columns[column].ColumnName, "Expected column named '{0}' but found '{1}'.", expectedColumns[column], markers.Columns[column].ColumnName);
            }
            if (expectedColumns.Count == TestConstant.DefaultMarkerColumns.Count)
            {
                for (int column = 0; column < expectedColumns.Count; ++column)
                {
                    Assert.IsTrue(expectedColumns[column] == TestConstant.DefaultMarkerColumns[column], "Expected column named '{0}' but found '{1}'.", expectedColumns[column], TestConstant.DefaultMarkerColumns[column]);
                }
            }

            // marker rows aren't populated if no counters are present in the database
            if (expectedColumns.Count == 1)
            {
                Assert.IsTrue(fileDatabase.Markers.RowCount == 0);
            }
            else
            {
                Assert.IsTrue(fileDatabase.Markers.RowCount == imagesExpected);
            }
        }
    }
}
