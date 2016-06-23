using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using Timelapse.Database;
using Timelapse.Images;
using Timelapse.Util;

namespace Timelapse.UnitTests
{
    [TestClass]
    public class DatabaseTests : TimelapseTest
    {
        [TestMethod]
        public void CreateReuseCarnivoreImageDatabase()
        {
            this.CreateReuseImageDatabase(TestConstant.File.CarnivoreTemplateDatabaseFileName, TestConstant.File.CarnivoreNewImageDatabaseFileName, (ImageDatabase imageDatabase) =>
            {
                return this.PopulateCarnivoreDatabase(imageDatabase);
            });
        }

        [TestMethod]
        public void CreateReuseDefaultImageDatabase()
        {
            this.CreateReuseImageDatabase(TestConstant.File.DefaultTemplateDatabaseFileName2015, TestConstant.File.DefaultImageDatabaseFileName2023, (ImageDatabase imageDatabase) =>
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
            int deletedImages = imageDatabase.GetImageCount(ImageQualityFilter.MarkedForDeletion);
            Assert.IsTrue(deletedImages == 0);

            Assert.IsTrue(imageDatabase.GetImageCount(ImageQualityFilter.All) == imageExpectations.Count);
            Dictionary<ImageQualityFilter, int> imageCounts = imageDatabase.GetImageCountByQuality();
            Assert.IsTrue(imageCounts.Count == 4);
            Assert.IsTrue(imageCounts[ImageQualityFilter.Corrupted] == 0);
            Assert.IsTrue(imageCounts[ImageQualityFilter.Dark] == 0);
            Assert.IsTrue(imageCounts[ImageQualityFilter.Missing] == 0);
            Assert.IsTrue(imageCounts[ImageQualityFilter.Ok] == imageExpectations.Count);

            ImageDataTable imagesToDelete = imageDatabase.GetImagesMarkedForDeletion();
            Assert.IsTrue(imagesToDelete.RowCount == 0);

            // check images after initial add and again after reopen and application of filters
            // checks are not performed after last filter in list is applied
            string currentDirectoryName = Path.GetFileName(imageDatabase.FolderPath);
            imageDatabase.SelectDataTableImagesAll();
            foreach (ImageQualityFilter nextFilter in new List<ImageQualityFilter>() { ImageQualityFilter.All, ImageQualityFilter.Ok, ImageQualityFilter.Ok })
            {
                Assert.IsTrue(imageDatabase.CurrentlySelectedImageCount == imageExpectations.Count);
                imageDatabase.SelectDataTableImagesAll();
                Assert.IsTrue(imageDatabase.ImageDataTable.RowCount == imageExpectations.Count);
                int firstDisplayableImage = imageDatabase.FindFirstDisplayableImage(Constants.DefaultImageRowIndex);
                Assert.IsTrue(firstDisplayableImage == Constants.DefaultImageRowIndex);

                for (int image = 0; image < imageExpectations.Count; ++image)
                {
                    // marshalling to ImageProperties
                    ImageRow imageProperties = imageDatabase.ImageDataTable[image];
                    ImageExpectations imageExpectation = imageExpectations[image];
                    imageExpectation.Verify(imageProperties);

                    List<MetaTagCounter> metaTagCounters = imageDatabase.GetMetaTagCounters(imageProperties.ID);
                    Assert.IsTrue(metaTagCounters.Count >= 0);

                    // retrieval by path
                    FileInfo imageFile = imageProperties.GetFileInfo(imageDatabase.FolderPath);
                    Assert.IsTrue(imageDatabase.GetOrCreateImage(imageFile, out imageProperties));

                    // retrieval by specific method
                    // imageDatabase.GetImageValue();
                    Assert.IsTrue(imageDatabase.IsImageDisplayable(image));
                    Assert.IsTrue(imageDatabase.IsImageRowInRange(image));

                    // retrieval by table
                    imageExpectation.Verify(imageDatabase.ImageDataTable[image]);
                }

                // reopen database for test and refresh images so next iteration of the loop checks state after reload
                imageDatabase = ImageDatabase.CreateOrOpen(imageDatabase.FilePath, imageDatabase);
                imageDatabase.SelectDataTableImages(nextFilter);
                Assert.IsTrue(imageDatabase.ImageDataTable.RowCount > 0);
            }

            imageDatabase.SelectDataTableImagesAll();
            Assert.IsTrue(imageDatabase.ImageDataTable.RowCount > 0);
            imageDatabase.AdjustImageTimes(new TimeSpan(1, 2, 3, 4, 5), 0, imageDatabase.CurrentlySelectedImageCount);
            imageDatabase.AdjustImageTimes(new TimeSpan(-5, -4, -3, -2, -1), 0, imageDatabase.CurrentlySelectedImageCount);
            int firstNonSwappableImage = DateTimeHandler.SwapDayMonthIsPossible(imageDatabase);
            Assert.IsTrue(firstNonSwappableImage == 0 || firstNonSwappableImage == 1);

            // imageDatabase.TryGetImagesCustom();
            // imageDatabase.UpdateAllImagesInFilteredView();
            // imageDatabase.UpdateID();
            // imageDatabase.UpdateImage();
            // imageDatabase.UpdateImages();

            // sanity coverage of image set table methods
            this.VerifyDefaultImageSetTableContent(imageDatabase);
            // imageDatabase.SetImageSetLog();
            // imageDatabase.UpdateImageSetFilter();
            // imageDatabase.UpdateImageSetRowIndex();
            // imageDatabase.UpdateMagnifierEnabled();

            // sanity coverage of marker table methods
            // this.VerifyDefaultMarkerTableContent(imageDatabase, imageExpectations.Count);
            // imageDatabase.SetMarkerPoints();
            // imageDatabase.UpdateMarkers();
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
            widthControl.TextBoxWidth = modifiedWidth;
            templateDatabase.SyncControlToDatabase(widthControl);
            Assert.IsTrue(templateDatabase.TemplateTable[widthIndex].TextBoxWidth == modifiedWidth);

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
            Assert.IsTrue(templateDatabase.TemplateTable[widthIndex].TextBoxWidth == modifiedWidth);

            // reopen the image database to synchronize its template table with the modified table in the current template
            imageDatabase = ImageDatabase.CreateOrOpen(imageDatabase.FilePath, templateDatabase);
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
            Assert.IsTrue(imageDatabase.TemplateTable[widthIndex].TextBoxWidth == modifiedWidth);
        }

        [TestMethod]
        public void GenerateControlsAndPropagate()
        {
            List<DatabaseExpectations> databaseExpectations = new List<DatabaseExpectations>()
            {
                new DatabaseExpectations()
                {
                    ImageDatabaseFileName = TestConstant.File.CarnivoreNewImageDatabaseFileName,
                    TemplateDatabaseFileName = TestConstant.File.CarnivoreTemplateDatabaseFileName,
                    ExpectedControls = Constants.Control.StandardTypes.Count - 2 + 10
                },
                new DatabaseExpectations()
                {
                    ImageDatabaseFileName = Constants.File.DefaultImageDatabaseFileName,
                    TemplateDatabaseFileName = TestConstant.File.DefaultTemplateDatabaseFileName2015,
                    ExpectedControls = TestConstant.DefaultImageTableColumns.Count - 6
                }
            };

            foreach (DatabaseExpectations databaseExpectation in databaseExpectations)
            {
                ImageDatabase imageDatabase = this.CreateImageDatabase(databaseExpectation.TemplateDatabaseFileName, databaseExpectation.ImageDatabaseFileName);
                DataEntryHandler dataHandler = new DataEntryHandler(imageDatabase);

                DataEntryControls controls = new DataEntryControls();
                controls.Generate(imageDatabase, dataHandler);
                Assert.IsTrue(controls.ControlsByDataLabel.Count == databaseExpectation.ExpectedControls, "Expected {0} controls to be generated but {1} were.", databaseExpectation.ExpectedControls, controls.ControlsByDataLabel.Count);

                // check copies aren't possible when the image enumerator's not pointing to an image
                List<DataEntryControl> copyableControls = controls.Controls.Where(control => control.DataLabel != Constants.DatabaseColumn.Folder &&
                                                                                  control.DataLabel != Constants.DatabaseColumn.RelativePath &&
                                                                                  control.DataLabel != Constants.DatabaseColumn.File &&
                                                                                  control.DataLabel != Constants.DatabaseColumn.Date &&
                                                                                  control.DataLabel != Constants.DatabaseColumn.Time).ToList();
                foreach (DataEntryControl control in copyableControls)
                {
                    Assert.IsFalse(dataHandler.IsCopyForwardPossible(control));
                    Assert.IsFalse(dataHandler.IsCopyFromLastValuePossible(control));
                }

                // check only copy forward is possible when enumerator's on first image
                if (databaseExpectation.ImageDatabaseFileName == TestConstant.File.CarnivoreNewImageDatabaseFileName)
                {
                    this.PopulateCarnivoreDatabase(imageDatabase);
                }
                else
                {
                    this.PopulateDefaultDatabase(imageDatabase);
                }

                Assert.IsTrue(dataHandler.ImageCache.MoveNext());
                foreach (DataEntryControl control in copyableControls)
                {
                    Assert.IsTrue(dataHandler.IsCopyForwardPossible(control));
                    Assert.IsFalse(dataHandler.IsCopyFromLastValuePossible(control));
                }

                // check only copy last is possible when enumerator's on last image
                // check also copy last is not possible if no previous instance of the field has been filled out
                Assert.IsTrue(dataHandler.ImageCache.MoveNext());
                foreach (DataEntryControl control in copyableControls)
                {
                    Assert.IsFalse(dataHandler.IsCopyForwardPossible(control));
                    if (control.DataLabel == TestConstant.CarnivoreDatabaseColumn.Pelage ||
                        control.DataLabel == TestConstant.DefaultDatabaseColumn.ChoiceNotVisible ||
                        control.DataLabel == TestConstant.DefaultDatabaseColumn.ChoiceWithCustomDataLabel ||
                        control.DataLabel == TestConstant.DefaultDatabaseColumn.Choice3 ||
                        control.DataLabel == TestConstant.DefaultDatabaseColumn.Counter3 ||
                        control.DataLabel == TestConstant.DefaultDatabaseColumn.Note0 ||
                        control.DataLabel == TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel)
                    {
                        Assert.IsFalse(dataHandler.IsCopyFromLastValuePossible(control));
                    }
                    else
                    {
                        Assert.IsTrue(dataHandler.IsCopyFromLastValuePossible(control));
                    }
                }

                // methods not covered due to requirement of UX interaction
                // dataHandler.CopyForward(control);
                // dataHandler.CopyFromLastValue(control);
                // dataHandler.CopyToAll(control);
            }
        }

        /// <summary>
        /// Backwards compatibility test against editor 2.0.1.5 template database and Timelapse 2.0.2.3 image database.
        /// </summary>
        [TestMethod]
        public void ImageDatabase2023()
        {
            // load database
            string imageDatabaseBaseFileName = TestConstant.File.DefaultImageDatabaseFileName2023;
            ImageDatabase imageDatabase = this.CloneImageDatabase(TestConstant.File.DefaultTemplateDatabaseFileName2015, imageDatabaseBaseFileName);
            imageDatabase.SelectDataTableImagesAll();
            Assert.IsTrue(imageDatabase.ImageDataTable.RowCount > 0);

            // verify template portion
            this.VerifyTemplateDatabase(imageDatabase, imageDatabaseBaseFileName);
            this.VerifyDefaultTemplateTableContent(imageDatabase);

            // verify image set table
            this.VerifyDefaultImageSetTableContent(imageDatabase);

            // verify markers table
            int imagesExpected = 2;
            this.VerifyDefaultMarkerTableContent(imageDatabase, imagesExpected);

            MarkerExpectation martenMarkerExpectation = new MarkerExpectation();
            martenMarkerExpectation.ID = 1;
            martenMarkerExpectation.UserDefinedCountersByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Counter0, String.Empty);
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

            string initialRootFolderName = "Timelapse 2.0.2.3";
            ImageExpectations martenExpectation = new ImageExpectations(TestConstant.DefaultExpectation.InfraredMartenImage);
            martenExpectation.ID = 1;
            martenExpectation.InitialRootFolderName = initialRootFolderName;
            martenExpectation.RelativePath = null;
            martenExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Counter0, "3");
            martenExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Choice0, "choice c");
            martenExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Note0, "0");
            martenExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Flag0, Constants.Boolean.True);
            martenExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.CounterWithCustomDataLabel, "100");
            martenExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.ChoiceWithCustomDataLabel, "Genus species");
            martenExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel, "custom label");
            martenExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.FlagWithCustomDataLabel, Constants.Boolean.False);
            martenExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.CounterNotVisible, TestConstant.DefaultExpectation.CounterNotVisible.DefaultValue);
            martenExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.ChoiceNotVisible, Constants.ControlDefault.Value);
            martenExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.NoteNotVisible, Constants.ControlDefault.Value);
            martenExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.FlagNotVisible, Constants.ControlDefault.FlagValue);
            martenExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Counter3, "1");
            martenExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Choice3, Constants.ControlDefault.Value);
            martenExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Note3, "note");
            martenExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Flag3, Constants.Boolean.True);
            martenExpectation.Verify(imageDatabase.ImageDataTable[0]);

            ImageExpectations bobcatExpectation = new ImageExpectations(TestConstant.DefaultExpectation.DaylightBobcatImage);
            bobcatExpectation.ID = 2;
            bobcatExpectation.InitialRootFolderName = initialRootFolderName;
            bobcatExpectation.RelativePath = null;
            bobcatExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Counter0, TestConstant.DefaultExpectation.Counter0.DefaultValue);
            bobcatExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Choice0, "choice a");
            bobcatExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Note0, "1");
            bobcatExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Flag0, Constants.Boolean.True);
            bobcatExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.CounterWithCustomDataLabel, "3");
            bobcatExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.ChoiceWithCustomDataLabel, "with , comma");
            bobcatExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel, Constants.ControlDefault.Value);
            bobcatExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.FlagWithCustomDataLabel, Constants.Boolean.True);
            bobcatExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.CounterNotVisible, TestConstant.DefaultExpectation.CounterNotVisible.DefaultValue);
            bobcatExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.ChoiceNotVisible, Constants.ControlDefault.Value);
            bobcatExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.NoteNotVisible, Constants.ControlDefault.Value);
            bobcatExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.FlagNotVisible, Constants.ControlDefault.FlagValue);
            bobcatExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Counter3, TestConstant.DefaultExpectation.Counter3.DefaultValue);
            bobcatExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Choice3, Constants.ControlDefault.Value);
            bobcatExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Note3, Constants.ControlDefault.Value);
            bobcatExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Flag3, Constants.Boolean.True);
            bobcatExpectation.Verify(imageDatabase.ImageDataTable[1]);
        }

        [TestMethod]
        public void ImageDatabaseNegative()
        {
            TemplateDatabase templateDatabase = this.CloneTemplateDatabase(TestConstant.File.DefaultTemplateDatabaseFileName2015);
            ImageDatabase imageDatabase = this.CreateImageDatabase(templateDatabase, TestConstant.File.DefaultImageDatabaseFileName2023);
            this.PopulateDefaultDatabase(imageDatabase);

            // ImageDatabase methods
            int firstDisplayableImage = imageDatabase.FindFirstDisplayableImage(imageDatabase.CurrentlySelectedImageCount);
            Assert.IsTrue(firstDisplayableImage == imageDatabase.CurrentlySelectedImageCount - 1);

            int closestDisplayableImage = imageDatabase.FindClosestImage(Int64.MinValue);
            Assert.IsTrue(closestDisplayableImage == 0);
            closestDisplayableImage = imageDatabase.FindClosestImage(Int64.MaxValue);
            Assert.IsTrue(closestDisplayableImage == imageDatabase.CurrentlySelectedImageCount - 1);

            Assert.IsTrue(imageDatabase.GetImageCountWithCustomFilter(Constants.DatabaseColumn.File + " = 'InvalidValue'") == 0);

            Assert.IsFalse(imageDatabase.IsImageDisplayable(-1));
            Assert.IsFalse(imageDatabase.IsImageDisplayable(imageDatabase.CurrentlySelectedImageCount));

            Assert.IsFalse(imageDatabase.IsImageRowInRange(-1));
            Assert.IsFalse(imageDatabase.IsImageRowInRange(imageDatabase.CurrentlySelectedImageCount));

            ImageRow imageProperties = imageDatabase.ImageDataTable[0];
            FileInfo imageFile = imageProperties.GetFileInfo(imageDatabase.FolderPath);
            Assert.IsTrue(imageDatabase.GetOrCreateImage(imageFile, out imageProperties));

            // template table synchronization
            // remove choices and change a note to a choice to produce a type failure
            ControlRow choiceControl = templateDatabase.GetControlFromTemplateTable(TestConstant.DefaultDatabaseColumn.Choice0);
            choiceControl.List = "Choice0|Choice1|Choice2|Choice3|Choice4|Choice5|Choice6|Choice7";
            templateDatabase.SyncControlToDatabase(choiceControl);
            ControlRow noteControl = templateDatabase.GetControlFromTemplateTable(TestConstant.DefaultDatabaseColumn.Note0);
            noteControl.Type = Constants.Control.FixedChoice;
            templateDatabase.SyncControlToDatabase(noteControl);

            imageDatabase = ImageDatabase.CreateOrOpen(imageDatabase.FileName, templateDatabase);
            Assert.IsTrue(imageDatabase.TemplateSynchronizationIssues.Count == 4);
        }

        [TestMethod]
        public void RoundtripCsv()
        {
            // create database, push test images into the database, and load the image data table
            ImageDatabase imageDatabase = this.CreateImageDatabase(TestConstant.File.CarnivoreTemplateDatabaseFileName, TestConstant.File.CarnivoreNewImageDatabaseFileName);
            this.PopulateCarnivoreDatabase(imageDatabase);

            // roundtrip data through .csv
            CsvReaderWriter csvReaderWriter = new CsvReaderWriter();
            string initialCsvFilePath = this.GetUniqueFilePathForTest(Path.GetFileNameWithoutExtension(Constants.File.DefaultImageDatabaseFileName) + Constants.File.CsvFileExtension);
            csvReaderWriter.ExportToCsv(imageDatabase, initialCsvFilePath);
            List<string> importErrors;
            Assert.IsTrue(csvReaderWriter.TryImportFromCsv(initialCsvFilePath, imageDatabase, out importErrors));
            Assert.IsTrue(importErrors.Count == 0);

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
            string templateDatabaseBaseFileName = TestConstant.File.DefaultTemplateDatabaseFileName2015;
            TemplateDatabase templateDatabase = this.CloneTemplateDatabase(templateDatabaseBaseFileName);

            this.VerifyTemplateDatabase(templateDatabase, templateDatabaseBaseFileName);
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
            Assert.IsTrue(control.TextBoxWidth > 0);
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
            Assert.IsTrue(imageDatabase.ImageSet.ImageQualityFilter == ImageQualityFilter.All);
            Assert.IsTrue(imageDatabase.ImageSet.ImageRowIndex == 0);
            Assert.IsTrue(imageDatabase.ImageSet.Log == Constants.Database.ImageSetDefaultLog);
            Assert.IsTrue(imageDatabase.ImageSet.MagnifierEnabled);
        }

        private void VerifyDefaultMarkerTableContent(ImageDatabase imageDatabase, int imagesExpected)
        {
            DataTable markersTable = imageDatabase.MarkersTable.ExtractDataTable();
            Assert.IsTrue(markersTable.Columns.Count == TestConstant.DefaultMarkerTableColumns.Count);
            Assert.IsTrue(imageDatabase.MarkersTable.RowCount == imagesExpected);
        }

        private void VerifyDefaultTemplateTableContent(TemplateDatabase templateDatabase)
        {
            Assert.IsTrue(templateDatabase.TemplateTable.RowCount == TestConstant.DefaultImageTableColumns.Count - 1);
            TestConstant.DefaultExpectation.File.Verify(templateDatabase.TemplateTable[0]);
            TestConstant.DefaultExpectation.RelativePath.Verify(templateDatabase.TemplateTable[1]);
            TestConstant.DefaultExpectation.Folder.Verify(templateDatabase.TemplateTable[2]);
            TestConstant.DefaultExpectation.Date.Verify(templateDatabase.TemplateTable[3]);
            TestConstant.DefaultExpectation.Time.Verify(templateDatabase.TemplateTable[4]);
            TestConstant.DefaultExpectation.ImageQuality.Verify(templateDatabase.TemplateTable[5]);
            TestConstant.DefaultExpectation.MarkForDeletion.Verify(templateDatabase.TemplateTable[6]);
            TestConstant.DefaultExpectation.Counter0.Verify(templateDatabase.TemplateTable[7]);
            TestConstant.DefaultExpectation.Choice0.Verify(templateDatabase.TemplateTable[8]);
            TestConstant.DefaultExpectation.Note0.Verify(templateDatabase.TemplateTable[9]);
            TestConstant.DefaultExpectation.Flag0.Verify(templateDatabase.TemplateTable[10]);
            TestConstant.DefaultExpectation.CounterWithCustomDataLabel.Verify(templateDatabase.TemplateTable[11]);
            TestConstant.DefaultExpectation.ChoiceWithCustomDataLabel.Verify(templateDatabase.TemplateTable[12]);
            TestConstant.DefaultExpectation.NoteWithCustomDataLabel.Verify(templateDatabase.TemplateTable[13]);
            TestConstant.DefaultExpectation.FlagWithCustomDataLabel.Verify(templateDatabase.TemplateTable[14]);
            TestConstant.DefaultExpectation.CounterNotVisible.Verify(templateDatabase.TemplateTable[15]);
            TestConstant.DefaultExpectation.ChoiceNotVisible.Verify(templateDatabase.TemplateTable[16]);
            TestConstant.DefaultExpectation.NoteNotVisible.Verify(templateDatabase.TemplateTable[17]);
            TestConstant.DefaultExpectation.FlagNotVisible.Verify(templateDatabase.TemplateTable[18]);
            TestConstant.DefaultExpectation.Counter3.Verify(templateDatabase.TemplateTable[19]);
            TestConstant.DefaultExpectation.Choice3.Verify(templateDatabase.TemplateTable[20]);
            TestConstant.DefaultExpectation.Note3.Verify(templateDatabase.TemplateTable[21]);
            TestConstant.DefaultExpectation.Flag3.Verify(templateDatabase.TemplateTable[22]);
        }
    }
}
