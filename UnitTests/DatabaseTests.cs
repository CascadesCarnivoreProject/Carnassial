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
        public void CreateReuseDefaultImageDatabase()
        {
            this.CreateReuseImageDatabase(TestConstant.File.DefaultTemplateDatabaseFileName, TestConstant.File.DefaultImageDatabaseFileName, (ImageDatabase imageDatabase) =>
            {
                return this.PopulateDefaultDatabase(imageDatabase);
            });
        }

        private void CreateReuseImageDatabase(string templateDatabaseBaseFileName, string imageDatabaseBaseFileName, Func<ImageDatabase, List<ImageExpectations>> addImages)
        {
            // create database for test
            ImageDatabase imageDatabase = this.CreateImageDatabase(templateDatabaseBaseFileName, imageDatabaseBaseFileName);
            List<ImageExpectations> imageExpectations = addImages(imageDatabase);

            // sanity coverage of image data table methods
            int deletedImages = imageDatabase.GetImageCount(ImageSelection.MarkedForDeletion);
            Assert.IsTrue(deletedImages == 0);

            Assert.IsTrue(imageDatabase.GetImageCount(ImageSelection.All) == imageExpectations.Count);
            Assert.IsTrue(imageDatabase.GetImageCount(ImageSelection.MarkedForDeletion) == 0);
            Dictionary<ImageSelection, int> imageCounts = imageDatabase.GetImageCountsByQuality();
            Assert.IsTrue(imageCounts.Count == 4);
            Assert.IsTrue(imageCounts[ImageSelection.Corrupted] == 0);
            Assert.IsTrue(imageCounts[ImageSelection.Dark] == 0);
            Assert.IsTrue(imageCounts[ImageSelection.Missing] == 0);
            Assert.IsTrue(imageCounts[ImageSelection.Ok] == imageExpectations.Count);

            ImageDataTable imagesToDelete = imageDatabase.GetImagesMarkedForDeletion();
            Assert.IsTrue(imagesToDelete.RowCount == 0);

            // check images after initial add and again after reopen and application of selection
            // checks are not performed after last selection in list is applied
            int counterControls = 4;
            string currentDirectoryName = Path.GetFileName(imageDatabase.FolderPath);
            imageDatabase.SelectDataTableImages(ImageSelection.All);
            TimeZoneInfo imageSetTimeZone = imageDatabase.ImageSet.GetTimeZone();
            foreach (ImageSelection nextSelection in new List<ImageSelection>() { ImageSelection.All, ImageSelection.Ok, ImageSelection.Ok })
            {
                Assert.IsTrue(imageDatabase.CurrentlySelectedImageCount == imageExpectations.Count);
                imageDatabase.SelectDataTableImages(ImageSelection.All);
                Assert.IsTrue(imageDatabase.ImageDataTable.RowCount == imageExpectations.Count);
                int firstDisplayableImage = imageDatabase.FindFirstDisplayableImage(Constants.DefaultImageRowIndex);
                Assert.IsTrue(firstDisplayableImage == Constants.DefaultImageRowIndex);

                for (int imageIndex = 0; imageIndex < imageExpectations.Count; ++imageIndex)
                {
                    // verify image
                    ImageRow image = imageDatabase.ImageDataTable[imageIndex];
                    ImageExpectations imageExpectation = imageExpectations[imageIndex];
                    imageExpectation.Verify(image, imageSetTimeZone);

                    // verify no markers associated with image
                    List<MarkersForCounter> markersOnImage = imageDatabase.GetMarkersOnImage(image.ID);
                    Assert.IsTrue(markersOnImage.Count == counterControls);
                    foreach (MarkersForCounter markerForCounter in markersOnImage)
                    {
                        Assert.IsFalse(String.IsNullOrWhiteSpace(markerForCounter.DataLabel));
                        Assert.IsTrue(markerForCounter.Markers.Count == 0);
                    }

                    // retrieval by path
                    FileInfo imageFile = image.GetFileInfo(imageDatabase.FolderPath);
                    Assert.IsTrue(imageDatabase.GetOrCreateImage(imageFile, imageSetTimeZone, out image));

                    // retrieval by specific method
                    // imageDatabase.GetImageValue();
                    Assert.IsTrue(imageDatabase.IsImageDisplayable(imageIndex));
                    Assert.IsTrue(imageDatabase.IsImageRowInRange(imageIndex));

                    // retrieval by table
                    imageExpectation.Verify(imageDatabase.ImageDataTable[imageIndex], imageSetTimeZone);
                }

                // reopen database for test and refresh images so next iteration of the loop checks state after reload
                imageDatabase = ImageDatabase.CreateOrOpen(imageDatabase.FilePath, imageDatabase, CustomSelectionOperator.And);
                imageDatabase.SelectDataTableImages(nextSelection);
                Assert.IsTrue(imageDatabase.ImageDataTable.RowCount > 0);
            }

            // sanity coverage of image set table methods
            string originalTimeZoneID = imageDatabase.ImageSet.TimeZone;

            this.VerifyDefaultImageSetTableContent(imageDatabase);
            imageDatabase.ImageSet.ImageSelection = ImageSelection.Custom;
            imageDatabase.ImageSet.ImageRowIndex = -1;
            imageDatabase.AppendToImageSetLog(new StringBuilder("Test log entry."));
            imageDatabase.ImageSet.MagnifierEnabled = true;
            imageDatabase.ImageSet.TimeZone = "Test Time Zone";
            imageDatabase.SyncImageSetToDatabase();
            Assert.IsTrue(imageDatabase.ImageSet.ID == 1);
            Assert.IsTrue(imageDatabase.ImageSet.ImageSelection == ImageSelection.Custom);
            Assert.IsTrue(imageDatabase.ImageSet.ImageRowIndex == -1);
            Assert.IsTrue(imageDatabase.ImageSet.Log == Constants.Database.ImageSetDefaultLog + "Test log entry.");
            Assert.IsTrue(imageDatabase.ImageSet.MagnifierEnabled);
            Assert.IsTrue(imageDatabase.ImageSet.TimeZone == "Test Time Zone");

            imageDatabase.ImageSet.TimeZone = originalTimeZoneID;

            // sanity coverage of marker table methods
            this.VerifyDefaultMarkerTableContent(imageDatabase, imageExpectations.Count);
            // imageDatabase.SetMarkerPoints();
            // imageDatabase.UpdateMarkers();

            // date manipulation
            imageDatabase.SelectDataTableImages(ImageSelection.All);
            Assert.IsTrue(imageDatabase.ImageDataTable.RowCount > 0);
            List<DateTimeOffset> imageTimesBeforeAdjustment = imageDatabase.GetImageTimes().ToList();
            TimeSpan adjustment = new TimeSpan(0, 1, 2, 3, 0);
            imageDatabase.AdjustImageTimes(adjustment);
            this.VerifyImageTimeAdjustment(imageTimesBeforeAdjustment, imageDatabase.GetImageTimes().ToList(), adjustment);
            imageDatabase.SelectDataTableImages(ImageSelection.All);
            this.VerifyImageTimeAdjustment(imageTimesBeforeAdjustment, imageDatabase.GetImageTimes().ToList(), adjustment);

            imageTimesBeforeAdjustment = imageDatabase.GetImageTimes().ToList();
            adjustment = new TimeSpan(-1, -2, -3, -4, 0);
            int startRow = 1;
            int endRow = imageDatabase.CurrentlySelectedImageCount - 1;
            imageDatabase.AdjustImageTimes(adjustment, startRow, endRow);
            this.VerifyImageTimeAdjustment(imageTimesBeforeAdjustment, imageDatabase.GetImageTimes().ToList(), startRow, endRow, adjustment);
            imageDatabase.SelectDataTableImages(ImageSelection.All);
            this.VerifyImageTimeAdjustment(imageTimesBeforeAdjustment, imageDatabase.GetImageTimes().ToList(), startRow, endRow, adjustment);

            imageDatabase.ExchangeDayAndMonthInImageDates();
            imageDatabase.ExchangeDayAndMonthInImageDates(0, imageDatabase.ImageDataTable.RowCount - 1);

            // custom selection coverage
            // search terms should be created for all visible controls except Folder, but DateTime gets two
            Assert.IsTrue((imageDatabase.TemplateTable.RowCount - 5) == imageDatabase.CustomSelection.SearchTerms.Count);
            Assert.IsTrue(String.IsNullOrEmpty(imageDatabase.CustomSelection.GetImagesWhere()));
            Assert.IsTrue(imageDatabase.GetImageCount(ImageSelection.Custom) == -1);

            SearchTerm dateTime = imageDatabase.CustomSelection.SearchTerms.First(term => term.DataLabel == Constants.DatabaseColumn.DateTime);
            dateTime.UseForSearching = true;
            dateTime.DatabaseValue = DateTimeHandler.ToDisplayDateString(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero));
            Assert.IsFalse(String.IsNullOrEmpty(imageDatabase.CustomSelection.GetImagesWhere()));
            imageDatabase.SelectDataTableImages(ImageSelection.Custom);
            Assert.IsTrue(imageDatabase.ImageDataTable.RowCount == imageExpectations.Count);

            dateTime.Operator = Constants.SearchTermOperator.Equal;
            Assert.IsFalse(String.IsNullOrEmpty(imageDatabase.CustomSelection.GetImagesWhere()));
            imageDatabase.SelectDataTableImages(ImageSelection.Custom);
            Assert.IsTrue(imageDatabase.ImageDataTable.RowCount == 0);

            dateTime.UseForSearching = false;
            imageDatabase.CustomSelection.TermCombiningOperator = CustomSelectionOperator.And;

            SearchTerm file = imageDatabase.CustomSelection.SearchTerms.Single(term => term.DataLabel == Constants.DatabaseColumn.File);
            file.UseForSearching = true;
            file.Operator = Constants.SearchTermOperator.Glob;
            file.DatabaseValue = "*" + Constants.File.JpgFileExtension.ToUpperInvariant();
            Assert.IsFalse(String.IsNullOrEmpty(imageDatabase.CustomSelection.GetImagesWhere()));
            imageDatabase.SelectDataTableImages(ImageSelection.Custom);
            Assert.IsTrue(imageDatabase.ImageDataTable.RowCount == imageExpectations.Count);

            SearchTerm imageQuality = imageDatabase.CustomSelection.SearchTerms.Single(term => term.DataLabel == Constants.DatabaseColumn.ImageQuality);
            imageQuality.UseForSearching = true;
            imageQuality.Operator = Constants.SearchTermOperator.Equal;
            imageQuality.DatabaseValue = ImageSelection.Ok.ToString();
            Assert.IsFalse(String.IsNullOrEmpty(imageDatabase.CustomSelection.GetImagesWhere()));
            imageDatabase.SelectDataTableImages(ImageSelection.Custom);
            Assert.IsTrue(imageDatabase.ImageDataTable.RowCount == imageExpectations.Count);

            SearchTerm relativePath = imageDatabase.CustomSelection.SearchTerms.Single(term => term.DataLabel == Constants.DatabaseColumn.RelativePath);
            relativePath.UseForSearching = true;
            relativePath.Operator = Constants.SearchTermOperator.Equal;
            relativePath.DatabaseValue = imageExpectations[0].RelativePath;
            Assert.IsFalse(String.IsNullOrEmpty(imageDatabase.CustomSelection.GetImagesWhere()));
            imageDatabase.SelectDataTableImages(ImageSelection.Custom);
            Assert.IsTrue(imageDatabase.ImageDataTable.RowCount == 2);

            SearchTerm markedForDeletion = imageDatabase.CustomSelection.SearchTerms.Single(term => term.ControlType == Constants.DatabaseColumn.DeleteFlag);
            markedForDeletion.UseForSearching = true;
            markedForDeletion.Operator = Constants.SearchTermOperator.Equal;
            markedForDeletion.DatabaseValue = Constants.Boolean.False;
            Assert.IsFalse(String.IsNullOrEmpty(imageDatabase.CustomSelection.GetImagesWhere()));
            imageDatabase.SelectDataTableImages(ImageSelection.Custom);
            Assert.IsTrue(imageDatabase.ImageDataTable.RowCount == 2);

            imageQuality.DatabaseValue = ImageSelection.Dark.ToString();
            Assert.IsFalse(String.IsNullOrEmpty(imageDatabase.CustomSelection.GetImagesWhere()));
            Assert.IsTrue(imageDatabase.GetImageCount(ImageSelection.All) == imageExpectations.Count);
            Assert.IsTrue(imageDatabase.GetImageCount(ImageSelection.Corrupted) == 0);
            Assert.IsTrue(imageDatabase.GetImageCount(ImageSelection.Custom) == 0);
            Assert.IsTrue(imageDatabase.GetImageCount(ImageSelection.Dark) == 0);
            Assert.IsTrue(imageDatabase.GetImageCount(ImageSelection.MarkedForDeletion) == 0);
            Assert.IsTrue(imageDatabase.GetImageCount(ImageSelection.Missing) == 0);
            Assert.IsTrue(imageDatabase.GetImageCount(ImageSelection.Ok) == imageExpectations.Count);

            // markers
            // reread
            imageDatabase.SelectDataTableImages(ImageSelection.All);

            int martenImageID = 1;
            List<MarkersForCounter> markersForMartenImage = imageDatabase.GetMarkersOnImage(martenImageID);
            Assert.IsTrue(markersForMartenImage.Count == counterControls);
            foreach (MarkersForCounter markerForCounter in markersForMartenImage)
            {
                Assert.IsFalse(String.IsNullOrWhiteSpace(markerForCounter.DataLabel));
                Assert.IsTrue(markerForCounter.Markers.Count == 0);
            }

            // no op - write empty
            foreach (MarkersForCounter markersForCounter in markersForMartenImage)
            {
                imageDatabase.SetMarkerPositions(martenImageID, markersForCounter);
            }

            // add
            markersForMartenImage = imageDatabase.GetMarkersOnImage(martenImageID);
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

                imageDatabase.SetMarkerPositions(martenImageID, markersForCounter);
                expectedMarkerPositions.Add(expectedPositions);
            }

            // roundtrip
            markersForMartenImage = imageDatabase.GetMarkersOnImage(martenImageID);
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
                    imageDatabase.SetMarkerPositions(martenImageID, markersForCounter);

                    expectedPositions.RemoveAt(expectedPositions.Count - 1);
                }
            }

            // roundtrip
            markersForMartenImage = imageDatabase.GetMarkersOnImage(martenImageID);
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
            Assert.IsTrue(templateDatabase.TemplateTable.RowCount == numberOfStandardControls);
            this.VerifyControls(templateDatabase);

            ControlRow newControl = templateDatabase.AddUserDefinedControl(Constants.Control.Counter);
            Assert.IsTrue(templateDatabase.TemplateTable.RowCount == numberOfStandardControls + 1);
            this.VerifyControl(newControl);

            newControl = templateDatabase.AddUserDefinedControl(Constants.Control.FixedChoice);
            Assert.IsTrue(templateDatabase.TemplateTable.RowCount == numberOfStandardControls + 2);
            this.VerifyControl(newControl);

            newControl = templateDatabase.AddUserDefinedControl(Constants.Control.Flag);
            Assert.IsTrue(templateDatabase.TemplateTable.RowCount == numberOfStandardControls + 3);
            this.VerifyControl(newControl);

            newControl = templateDatabase.AddUserDefinedControl(Constants.Control.Note);
            Assert.IsTrue(templateDatabase.TemplateTable.RowCount == numberOfStandardControls + 4);
            this.VerifyControl(newControl);
            this.VerifyTemplateDatabase(templateDatabase, templateDatabaseBaseFileName);

            templateDatabase.RemoveUserDefinedControl(templateDatabase.TemplateTable[numberOfStandardControls + 2]);
            Assert.IsTrue(templateDatabase.TemplateTable.RowCount == numberOfStandardControls + 3);
            templateDatabase.RemoveUserDefinedControl(templateDatabase.TemplateTable[numberOfStandardControls + 2]);
            Assert.IsTrue(templateDatabase.TemplateTable.RowCount == numberOfStandardControls + 2);
            templateDatabase.RemoveUserDefinedControl(templateDatabase.TemplateTable[numberOfStandardControls + 0]);
            Assert.IsTrue(templateDatabase.TemplateTable.RowCount == numberOfStandardControls + 1);
            templateDatabase.RemoveUserDefinedControl(templateDatabase.TemplateTable[numberOfStandardControls + 0]);
            Assert.IsTrue(templateDatabase.TemplateTable.RowCount == numberOfStandardControls);
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
            long controlOrder = templateDatabase.TemplateTable.RowCount;
            for (int row = 0; row < templateDatabase.TemplateTable.RowCount; --controlOrder, ++row)
            {
                string dataLabel = templateDatabase.TemplateTable[row].DataLabel;
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
            templateDatabase.RemoveUserDefinedControl(templateDatabase.TemplateTable[numberOfStandardControls + 22]);
            templateDatabase.RemoveUserDefinedControl(templateDatabase.TemplateTable[numberOfStandardControls + 16]);
            this.VerifyTemplateDatabase(templateDatabase, templateDatabaseBaseFileName);

            // create a database to capture the current template into its template table
            ImageDatabase imageDatabase = this.CreateImageDatabase(templateDatabase, TestConstant.File.DefaultNewImageDatabaseFileName);

            // modify UX properties of some controls by data row manipulation
            int copyableIndex = numberOfStandardControls + (3 * iterations) - 1;
            ControlRow copyableControl = templateDatabase.TemplateTable[copyableIndex];
            bool modifiedCopyable = !copyableControl.Copyable;
            copyableControl.Copyable = modifiedCopyable;
            templateDatabase.SyncControlToDatabase(copyableControl);
            Assert.IsTrue(templateDatabase.TemplateTable[copyableIndex].Copyable == modifiedCopyable);

            int defaultValueIndex = numberOfStandardControls + (3 * iterations) - 4;
            ControlRow defaultValueControl = templateDatabase.TemplateTable[defaultValueIndex];
            string modifiedDefaultValue = "Default value modification roundtrip.";
            defaultValueControl.DefaultValue = modifiedDefaultValue;
            templateDatabase.SyncControlToDatabase(defaultValueControl);
            Assert.IsTrue(templateDatabase.TemplateTable[defaultValueIndex].DefaultValue == modifiedDefaultValue);

            int labelIndex = numberOfStandardControls + (3 * iterations) - 3;
            ControlRow labelControl = templateDatabase.TemplateTable[labelIndex];
            string modifiedLabel = "Label modification roundtrip.";
            labelControl.Label = modifiedLabel;
            templateDatabase.SyncControlToDatabase(labelControl);
            Assert.IsTrue(templateDatabase.TemplateTable[labelIndex].Label == modifiedLabel);

            int listIndex = numberOfStandardControls + (3 * iterations) - 2;
            ControlRow listControl = templateDatabase.TemplateTable[listIndex];
            string modifiedList = listControl.List + "|NewChoice0|NewChoice1";
            listControl.List = modifiedList;
            templateDatabase.SyncControlToDatabase(listControl);
            Assert.IsTrue(templateDatabase.TemplateTable[listIndex].List == modifiedList);

            int tooltipIndex = numberOfStandardControls + (3 * iterations) - 3;
            ControlRow tooltipControl = templateDatabase.TemplateTable[tooltipIndex];
            string modifiedTooltip = "Tooltip modification roundtrip.";
            tooltipControl.Tooltip = modifiedTooltip;
            templateDatabase.SyncControlToDatabase(tooltipControl);
            Assert.IsTrue(templateDatabase.TemplateTable[tooltipIndex].Tooltip == modifiedTooltip);

            int widthIndex = numberOfStandardControls + (3 * iterations) - 2;
            ControlRow widthControl = templateDatabase.TemplateTable[widthIndex];
            int modifiedWidth = 1000;
            widthControl.Width = modifiedWidth;
            templateDatabase.SyncControlToDatabase(widthControl);
            Assert.IsTrue(templateDatabase.TemplateTable[widthIndex].Width == modifiedWidth);

            int visibleIndex = numberOfStandardControls + (3 * iterations) - 3;
            ControlRow visibleControl = templateDatabase.TemplateTable[visibleIndex];
            bool modifiedVisible = !visibleControl.Visible;
            visibleControl.Visible = modifiedVisible;
            templateDatabase.SyncControlToDatabase(visibleControl);
            Assert.IsTrue(templateDatabase.TemplateTable[visibleIndex].Visible == modifiedVisible);

            // reopen the template database and check again
            string templateDatabaseFilePath = templateDatabase.FilePath;
            templateDatabase = TemplateDatabase.CreateOrOpen(templateDatabaseFilePath);
            this.VerifyTemplateDatabase(templateDatabase, templateDatabaseFilePath);
            Assert.IsTrue(templateDatabase.TemplateTable.RowCount == numberOfStandardControls + (4 * iterations) - 2);
            DataTable templateDataTable = templateDatabase.TemplateTable.ExtractDataTable();
            Assert.IsTrue(templateDataTable.Columns.Count == TestConstant.TemplateTableColumns.Count);
            this.VerifyControls(templateDatabase);
            Assert.IsTrue(templateDatabase.TemplateTable[copyableIndex].Copyable == modifiedCopyable);
            Assert.IsTrue(templateDatabase.TemplateTable[defaultValueIndex].DefaultValue == modifiedDefaultValue);
            Assert.IsTrue(templateDatabase.TemplateTable[labelIndex].Label == modifiedLabel);
            Assert.IsTrue(templateDatabase.TemplateTable[listIndex].List == modifiedList);
            Assert.IsTrue(templateDatabase.TemplateTable[tooltipIndex].Tooltip == modifiedTooltip);
            Assert.IsTrue(templateDatabase.TemplateTable[visibleIndex].Visible == modifiedVisible);
            Assert.IsTrue(templateDatabase.TemplateTable[widthIndex].Width == modifiedWidth);

            // reopen the image database to synchronize its template table with the modified table in the current template
            imageDatabase = ImageDatabase.CreateOrOpen(imageDatabase.FilePath, templateDatabase, CustomSelectionOperator.And);
            Assert.IsTrue(imageDatabase.TemplateSynchronizationIssues.Count == 0);
            this.VerifyTemplateDatabase(imageDatabase, imageDatabase.FilePath);
            Assert.IsTrue(imageDatabase.TemplateTable.RowCount == numberOfStandardControls + (4 * iterations) - 2);
            DataTable templateTable = imageDatabase.TemplateTable.ExtractDataTable();
            Assert.IsTrue(templateTable.Columns.Count == TestConstant.TemplateTableColumns.Count);
            this.VerifyControls(imageDatabase);
            Assert.IsTrue(imageDatabase.TemplateTable[copyableIndex].Copyable == modifiedCopyable);
            Assert.IsTrue(imageDatabase.TemplateTable[defaultValueIndex].DefaultValue == modifiedDefaultValue);
            Assert.IsTrue(imageDatabase.TemplateTable[labelIndex].Label == modifiedLabel);
            Assert.IsTrue(imageDatabase.TemplateTable[listIndex].List == modifiedList);
            Assert.IsTrue(imageDatabase.TemplateTable[tooltipIndex].Tooltip == modifiedTooltip);
            Assert.IsTrue(imageDatabase.TemplateTable[visibleIndex].Visible == modifiedVisible);
            Assert.IsTrue(imageDatabase.TemplateTable[widthIndex].Width == modifiedWidth);
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
                    ImageDatabaseFileName = Constants.File.DefaultImageDatabaseFileName,
                    TemplateDatabaseFileName = TestConstant.File.DefaultTemplateDatabaseFileName,
                    ExpectedControls = TestConstant.DefaultImageTableColumns.Count - 6
                }
            };

            foreach (DatabaseExpectations databaseExpectation in databaseExpectations)
            {
                ImageDatabase imageDatabase = this.CreateImageDatabase(databaseExpectation.TemplateDatabaseFileName, databaseExpectation.ImageDatabaseFileName);
                DataEntryHandler dataHandler = new DataEntryHandler(imageDatabase);

                DataEntryControls controls = new DataEntryControls();
                controls.CreateControls(imageDatabase, dataHandler);
                Assert.IsTrue(controls.ControlsByDataLabel.Count == databaseExpectation.ExpectedControls, "Expected {0} controls to be generated but {1} were.", databaseExpectation.ExpectedControls, controls.ControlsByDataLabel.Count);

                // check copies aren't possible when the image enumerator's not pointing to an image
                foreach (DataEntryControl control in controls.Controls)
                {
                    Assert.IsFalse(dataHandler.IsCopyForwardPossible(control));
                    Assert.IsFalse(dataHandler.IsCopyFromLastNonEmptyValuePossible(control));
                }

                // check only copy forward is possible when enumerator's on first image
                List<ImageExpectations> imageExpectations = this.PopulateDefaultDatabase(imageDatabase);
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
            ImageDatabase imageDatabase = this.CreateImageDatabase(TestConstant.File.DefaultTemplateDatabaseFileName, TestConstant.File.DefaultNewImageDatabaseFileName);
            TimeZoneInfo imageSetTimeZone = imageDatabase.ImageSet.GetTimeZone();
            List<ImageRow> imagesToInsert = new List<ImageRow>();
            FileInfo[] imagesAndVideos = new DirectoryInfo(Path.Combine(this.WorkingDirectory, TestConstant.File.HybridVideoDirectoryName)).GetFiles();
            foreach (FileInfo imageFile in imagesAndVideos)
            {
                ImageRow imageRow;
                Assert.IsFalse(imageDatabase.GetOrCreateImage(imageFile, imageSetTimeZone, out imageRow));

                BitmapSource bitmapSource = imageRow.LoadBitmap(imageDatabase.FolderPath);
                WriteableBitmap writeableBitmap = bitmapSource.AsWriteable();
                Assert.IsFalse(writeableBitmap.IsBlack());
                imageRow.ImageQuality = writeableBitmap.GetImageQuality(Constants.Images.DarkPixelThresholdDefault, Constants.Images.DarkPixelRatioThresholdDefault);
                Assert.IsTrue(imageRow.ImageQuality == ImageSelection.Ok);

                // see if the date can be updated from the metadata
                // currently supported for images but not for videos
                DateTimeAdjustment imageTimeAdjustment = imageRow.TryReadDateTimeOriginalFromMetadata(imageDatabase.FolderPath, imageSetTimeZone);
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

            imageDatabase.AddImages(imagesToInsert, (ImageRow imageProperties, int imageIndex) => { });
            imageDatabase.SelectDataTableImages(ImageSelection.All);

            Assert.IsTrue(imageDatabase.ImageDataTable.RowCount == imagesAndVideos.Length);
            for (int rowIndex = 0; rowIndex < imageDatabase.ImageDataTable.RowCount; ++rowIndex)
            {
                FileInfo imageFile = imagesAndVideos[rowIndex];
                ImageRow imageRow = imageDatabase.ImageDataTable[rowIndex];
                bool expectedIsVideo = String.Equals(Path.GetExtension(imageFile.Name), Constants.File.JpgFileExtension, StringComparison.OrdinalIgnoreCase) ? false : true;
                Assert.IsTrue(imageRow.IsVideo == expectedIsVideo);
            }
        }

        [TestMethod]
        public void ImageDatabaseVerfication()
        {
            // load database
            string imageDatabaseBaseFileName = TestConstant.File.DefaultImageDatabaseFileName;
            ImageDatabase imageDatabase = this.CloneImageDatabase(TestConstant.File.DefaultTemplateDatabaseFileName, imageDatabaseBaseFileName);
            imageDatabase.SelectDataTableImages(ImageSelection.All);
            Assert.IsTrue(imageDatabase.ImageDataTable.RowCount > 0);

            // verify template portion
            this.VerifyTemplateDatabase(imageDatabase, imageDatabaseBaseFileName);
            DefaultTemplateTableExpectation templateTableExpectation = new DefaultTemplateTableExpectation(new Version(2, 2, 0, 0));
            templateTableExpectation.Verify(imageDatabase);

            // verify image set table
            this.VerifyDefaultImageSetTableContent(imageDatabase);

            // verify markers table
            int imagesExpected = 2;
            this.VerifyDefaultMarkerTableContent(imageDatabase, imagesExpected);

            MarkerExpectation martenMarkerExpectation = new MarkerExpectation();
            martenMarkerExpectation.ID = 1;
            martenMarkerExpectation.UserDefinedCountersByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Counter0, "0.498,0.575|0.550,0.566|0.584,0.555");
            martenMarkerExpectation.UserDefinedCountersByDataLabel.Add(TestConstant.DefaultDatabaseColumn.CounterWithCustomDataLabel, String.Empty);
            martenMarkerExpectation.UserDefinedCountersByDataLabel.Add(TestConstant.DefaultDatabaseColumn.CounterNotVisible, String.Empty);
            martenMarkerExpectation.UserDefinedCountersByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Counter3, String.Empty);
            martenMarkerExpectation.Verify(imageDatabase.MarkersTable[0]);

            MarkerExpectation bobcatMarkerExpectation = new MarkerExpectation();
            bobcatMarkerExpectation.ID = 2;
            bobcatMarkerExpectation.UserDefinedCountersByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Counter0, String.Empty);
            bobcatMarkerExpectation.UserDefinedCountersByDataLabel.Add(TestConstant.DefaultDatabaseColumn.CounterWithCustomDataLabel, String.Empty);
            bobcatMarkerExpectation.UserDefinedCountersByDataLabel.Add(TestConstant.DefaultDatabaseColumn.CounterNotVisible, String.Empty);
            bobcatMarkerExpectation.UserDefinedCountersByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Counter3, String.Empty);
            bobcatMarkerExpectation.Verify(imageDatabase.MarkersTable[1]);

            // verify ImageDataTable
            Assert.IsTrue(imageDatabase.ImageDataTable.ColumnNames.Count() == TestConstant.DefaultImageTableColumns.Count);
            Assert.IsTrue(imageDatabase.ImageDataTable.RowCount == imagesExpected);

            TimeZoneInfo imageSetTimeZone = imageDatabase.ImageSet.GetTimeZone();
            string initialRootFolderName = "UnitTests";
            ImageExpectations martenExpectation = new ImageExpectations(TestConstant.ImageExpectation.InfraredMarten);
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
            martenExpectation.Verify(imageDatabase.ImageDataTable[0], imageSetTimeZone);

            ImageExpectations bobcatExpectation = new ImageExpectations(TestConstant.ImageExpectation.DaylightBobcat);
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
            bobcatExpectation.Verify(imageDatabase.ImageDataTable[1], imageSetTimeZone);
        }

        [TestMethod]
        public void ImageDatabaseNegative()
        {
            TemplateDatabase templateDatabase = this.CloneTemplateDatabase(TestConstant.File.DefaultTemplateDatabaseFileName);
            ImageDatabase imageDatabase = this.CreateImageDatabase(templateDatabase, TestConstant.File.DefaultImageDatabaseFileName);
            this.PopulateDefaultDatabase(imageDatabase);

            // ImageDatabase methods
            int firstDisplayableImage = imageDatabase.FindFirstDisplayableImage(imageDatabase.CurrentlySelectedImageCount);
            Assert.IsTrue(firstDisplayableImage == imageDatabase.CurrentlySelectedImageCount - 1);

            int closestDisplayableImage = imageDatabase.FindClosestImage(Int64.MinValue);
            Assert.IsTrue(closestDisplayableImage == 0);
            closestDisplayableImage = imageDatabase.FindClosestImage(Int64.MaxValue);
            Assert.IsTrue(closestDisplayableImage == imageDatabase.CurrentlySelectedImageCount - 1);

            Assert.IsFalse(imageDatabase.IsImageDisplayable(-1));
            Assert.IsFalse(imageDatabase.IsImageDisplayable(imageDatabase.CurrentlySelectedImageCount));

            Assert.IsFalse(imageDatabase.IsImageRowInRange(-1));
            Assert.IsFalse(imageDatabase.IsImageRowInRange(imageDatabase.CurrentlySelectedImageCount));

            ImageRow imageProperties = imageDatabase.ImageDataTable[0];
            FileInfo imageFile = imageProperties.GetFileInfo(imageDatabase.FolderPath);
            TimeZoneInfo imageSetTimeZone = imageDatabase.ImageSet.GetTimeZone();
            Assert.IsTrue(imageDatabase.GetOrCreateImage(imageFile, imageSetTimeZone, out imageProperties));

            // template table synchronization
            // remove choices and change a note to a choice to produce a type failure
            ControlRow choiceControl = templateDatabase.GetControlFromTemplateTable(TestConstant.DefaultDatabaseColumn.Choice0);
            choiceControl.List = "Choice0|Choice1|Choice2|Choice3|Choice4|Choice5|Choice6|Choice7";
            templateDatabase.SyncControlToDatabase(choiceControl);
            ControlRow noteControl = templateDatabase.GetControlFromTemplateTable(TestConstant.DefaultDatabaseColumn.Note0);
            noteControl.Type = Constants.Control.FixedChoice;
            templateDatabase.SyncControlToDatabase(noteControl);

            imageDatabase = ImageDatabase.CreateOrOpen(imageDatabase.FileName, templateDatabase, CustomSelectionOperator.And);
            Assert.IsTrue(imageDatabase.TemplateSynchronizationIssues.Count == 5);
        }

        [TestMethod]
        public void RoundtripCsv()
        {
            // create database, push test images into the database, and load the image data table
            ImageDatabase imageDatabase = this.CreateImageDatabase(TestConstant.File.DefaultTemplateDatabaseFileName, TestConstant.File.DefaultNewImageDatabaseFileName);
            List<ImageExpectations> imageExpectations = this.PopulateDefaultDatabase(imageDatabase);

            // roundtrip data through .csv
            CsvReaderWriter csvReaderWriter = new CsvReaderWriter();
            string initialCsvFilePath = this.GetUniqueFilePathForTest(Path.GetFileNameWithoutExtension(Constants.File.DefaultImageDatabaseFileName) + Constants.File.CsvFileExtension);
            csvReaderWriter.ExportToCsv(imageDatabase, initialCsvFilePath);
            List<string> importErrors;
            Assert.IsTrue(csvReaderWriter.TryImportFromCsv(initialCsvFilePath, imageDatabase, out importErrors));
            Assert.IsTrue(importErrors.Count == 0);

            // verify ImageDataTable content hasn't changed
            TimeZoneInfo imageSetTimeZone = imageDatabase.ImageSet.GetTimeZone();
            for (int imageIndex = 0; imageIndex < imageExpectations.Count; ++imageIndex)
            {
                ImageRow image = imageDatabase.ImageDataTable[imageIndex];
                ImageExpectations imageExpectation = imageExpectations[imageIndex];
                imageExpectation.Verify(image, imageSetTimeZone);
            }

            // verify consistency of .csv export
            string roundtripCsvFilePath = Path.Combine(Path.GetDirectoryName(initialCsvFilePath), Path.GetFileNameWithoutExtension(initialCsvFilePath) + "-roundtrip" + Constants.File.CsvFileExtension);
            csvReaderWriter.ExportToCsv(imageDatabase, roundtripCsvFilePath);

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
            ImageDatabase imageDatabase = this.CreateImageDatabase(TestConstant.File.DefaultTemplateDatabaseFileName, Constants.File.DefaultImageDatabaseFileName);
            Assert.IsTrue(imageDatabase.ImageSet.TimeZone == TimeZoneInfo.Local.Id);
            Assert.IsTrue(TimeZoneInfo.Local.Equals(imageDatabase.ImageSet.GetTimeZone()));

            imageDatabase.ImageSet.TimeZone = initialTimeZoneID;
            imageDatabase.SyncImageSetToDatabase();

            TimeZoneInfo initialImageSetTimeZone = TimeZoneInfo.FindSystemTimeZoneById(initialTimeZoneID);
            Assert.IsTrue(imageDatabase.ImageSet.TimeZone == initialTimeZoneID);
            Assert.IsTrue(initialImageSetTimeZone.Equals(imageDatabase.ImageSet.GetTimeZone()));

            List<ImageExpectations> imageExpectations = this.PopulateDefaultDatabase(imageDatabase, true);

            // change to second time zone
            imageDatabase.ImageSet.TimeZone = secondTimeZoneID;
            imageDatabase.SyncImageSetToDatabase();

            TimeZoneInfo secondImageSetTimeZone = TimeZoneInfo.FindSystemTimeZoneById(secondTimeZoneID);
            Assert.IsTrue(imageDatabase.ImageSet.TimeZone == secondTimeZoneID);
            Assert.IsTrue(secondImageSetTimeZone.Equals(imageDatabase.ImageSet.GetTimeZone()));

            // verify date times of existing images haven't changed
            int initialImageCount = imageDatabase.ImageDataTable.RowCount;
            this.VerifyImages(imageDatabase, imageExpectations, initialImageSetTimeZone, initialImageCount, secondImageSetTimeZone);

            // add more images
            DateTimeAdjustment timeAdjustment;
            ImageRow martenPairImage = this.CreateImage(imageDatabase, secondImageSetTimeZone, TestConstant.ImageExpectation.DaylightMartenPair, out timeAdjustment);
            ImageRow coyoteImage = this.CreateImage(imageDatabase, secondImageSetTimeZone, TestConstant.ImageExpectation.DaylightCoyote, out timeAdjustment);

            imageDatabase.AddImages(new List<ImageRow>() { martenPairImage, coyoteImage }, null);
            imageDatabase.SelectDataTableImages(ImageSelection.All);

            // generate expectations for new images
            string initialRootFolderName = imageExpectations[0].InitialRootFolderName;
            ImageExpectations martenPairExpectation = new ImageExpectations(TestConstant.ImageExpectation.DaylightMartenPair);
            martenPairExpectation.ID = imageExpectations.Count + 1;
            martenPairExpectation.InitialRootFolderName = initialRootFolderName;
            imageExpectations.Add(martenPairExpectation);

            ImageExpectations daylightCoyoteExpectation = new ImageExpectations(TestConstant.ImageExpectation.DaylightCoyote);
            daylightCoyoteExpectation.ID = imageExpectations.Count + 1;
            daylightCoyoteExpectation.InitialRootFolderName = initialRootFolderName;
            imageExpectations.Add(daylightCoyoteExpectation);

            // verify new images pick up the current timezone
            this.VerifyImages(imageDatabase, imageExpectations, initialImageSetTimeZone, initialImageCount, secondImageSetTimeZone);
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
            for (int row = 0; row < database.TemplateTable.RowCount; ++row)
            {
                // sanity check control
                ControlRow control = database.TemplateTable[row];
                this.VerifyControl(control);

                // verify controls are sorted in control order and that control order is ones based
                Assert.IsTrue(control.ControlOrder == row + 1);
            }
        }

        private void VerifyImages(ImageDatabase imageDatabase, List<ImageExpectations> imageExpectations, TimeZoneInfo initialImageSetTimeZone, int initialImageCount, TimeZoneInfo secondImageSetTimeZone)
        {
            for (int image = 0; image < imageExpectations.Count; ++image)
            {
                TimeZoneInfo expectedTimeZone = image >= initialImageCount ? secondImageSetTimeZone : initialImageSetTimeZone;
                ImageExpectations imageExpectation = imageExpectations[image];
                imageExpectation.Verify(imageDatabase.ImageDataTable[image], expectedTimeZone);
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
            Assert.IsNotNull(templateDatabase.TemplateTable);

            // FilePath checks
            string templateDatabaseFileName = Path.GetFileName(templateDatabase.FilePath);
            Assert.IsTrue(templateDatabaseFileName.StartsWith(Path.GetFileNameWithoutExtension(templateDatabaseBaseFileName)));
            Assert.IsTrue(templateDatabaseFileName.EndsWith(Path.GetExtension(templateDatabaseBaseFileName)));
            Assert.IsTrue(File.Exists(templateDatabase.FilePath));

            // TemplateTable checks
            DataTable templateDataTable = templateDatabase.TemplateTable.ExtractDataTable();
            Assert.IsTrue(templateDataTable.Columns.Count == TestConstant.TemplateTableColumns.Count);
            List<long> ids = new List<long>();
            List<long> controlOrders = new List<long>();
            List<long> spreadsheetOrders = new List<long>();
            foreach (ControlRow control in templateDatabase.TemplateTable)
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

        private void VerifyDefaultImageSetTableContent(ImageDatabase imageDatabase)
        {
            Assert.IsTrue(imageDatabase.ImageSet.ImageSelection == ImageSelection.All);
            Assert.IsTrue(imageDatabase.ImageSet.ImageRowIndex == 0);
            Assert.IsTrue(imageDatabase.ImageSet.Log == Constants.Database.ImageSetDefaultLog);
            Assert.IsFalse(imageDatabase.ImageSet.MagnifierEnabled);
            Assert.IsTrue(imageDatabase.ImageSet.TimeZone == TimeZoneInfo.Local.Id);
        }

        private void VerifyDefaultMarkerTableContent(ImageDatabase imageDatabase, int imagesExpected)
        {
            DataTable markersTable = imageDatabase.MarkersTable.ExtractDataTable();

            List<string> expectedColumns = new List<string>() { Constants.DatabaseColumn.ID };
            foreach (ControlRow control in imageDatabase.TemplateTable)
            {
                if (control.Type == Constants.Control.Counter)
                {
                    expectedColumns.Add(control.DataLabel);
                }
            }

            Assert.IsTrue(markersTable.Columns.Count == expectedColumns.Count);
            for (int column = 0; column < expectedColumns.Count; ++column)
            {
                Assert.IsTrue(expectedColumns[column] == markersTable.Columns[column].ColumnName, "Expected column named '{0}' but found '{1}'.", expectedColumns[column], markersTable.Columns[column].ColumnName);
            }
            if (expectedColumns.Count == TestConstant.DefaultMarkerTableColumns.Count)
            {
                for (int column = 0; column < expectedColumns.Count; ++column)
                {
                    Assert.IsTrue(expectedColumns[column] == TestConstant.DefaultMarkerTableColumns[column], "Expected column named '{0}' but found '{1}'.", expectedColumns[column], TestConstant.DefaultMarkerTableColumns[column]);
                }
            }

            // marker rows aren't populated if no counters are present in the database
            if (expectedColumns.Count == 1)
            {
                Assert.IsTrue(imageDatabase.MarkersTable.RowCount == 0);
            }
            else
            {
                Assert.IsTrue(imageDatabase.MarkersTable.RowCount == imagesExpected);
            }
        }
    }
}
