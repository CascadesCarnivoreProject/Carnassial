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
            int deletedImages = imageDatabase.GetDeletedImageCount();
            Assert.IsTrue(deletedImages == 0);

            Assert.IsTrue(imageDatabase.GetImageCount() == imageExpectations.Count);
            Dictionary<ImageQualityFilter, int> imageCounts = imageDatabase.GetImageCounts();
            Assert.IsTrue(imageCounts.Count == 4);
            Assert.IsTrue(imageCounts[ImageQualityFilter.Corrupted] == 0);
            Assert.IsTrue(imageCounts[ImageQualityFilter.Dark] == 0);
            Assert.IsTrue(imageCounts[ImageQualityFilter.Missing] == 0);
            Assert.IsTrue(imageCounts[ImageQualityFilter.Ok] == imageExpectations.Count);

            // check images after initial add and again after reopen and application of filters
            // checks are not performed after last filter in list is applied
            string currentDirectoryName = Path.GetFileName(imageDatabase.FolderPath);
            imageDatabase.TryGetImages(ImageQualityFilter.All);
            foreach (ImageQualityFilter nextFilter in new List<ImageQualityFilter>() { ImageQualityFilter.All, ImageQualityFilter.Ok, ImageQualityFilter.Ok })
            {
                Assert.IsTrue(imageDatabase.CurrentlySelectedImageCount == imageExpectations.Count);
                DataTable allImageTable = imageDatabase.GetAllImages();
                Assert.IsTrue(allImageTable.Rows.Count == imageExpectations.Count);
                int firstDisplayableImage = imageDatabase.FindFirstDisplayableImage(Constants.DefaultImageRowIndex);
                Assert.IsTrue(firstDisplayableImage == Constants.DefaultImageRowIndex);
                DataTable markedForDeletionTable = imageDatabase.GetImagesMarkedForDeletion();
                Assert.IsTrue(markedForDeletionTable.Rows.Count == 0);

                for (int image = 0; image < imageExpectations.Count; ++image)
                {
                    // marshalling to ImageProperties
                    ImageProperties imageProperties = imageDatabase.GetImageByRow(image);
                    ImageExpectations imageExpectation = imageExpectations[image];
                    imageExpectation.Verify(imageProperties);

                    // row to ID conversion
                    long id = imageDatabase.GetImageID(image);
                    Assert.IsTrue(imageProperties.ID == id);

                    // retrieval by ID
                    DataTable imageTable = imageDatabase.GetImageByID(id);
                    Assert.IsTrue(imageTable != null && imageTable.Rows.Count == 1);
                    imageExpectation.Verify(imageTable.Rows[0]);

                    List<MetaTagCounter> metaTagCounters = imageDatabase.GetMetaTagCounters(id);
                    Assert.IsTrue(metaTagCounters.Count >= 0);

                    // retrieval by path
                    imageProperties.ID = Constants.Database.InvalidID;
                    DataRow imageRow;
                    Assert.IsTrue(imageDatabase.TryGetImage(imageProperties, out imageRow));

                    // retrieval by specific method
                    // imageDatabase.GetImageValue();
                    Assert.IsFalse(imageDatabase.IsImageCorrupt(image));
                    Assert.IsTrue(imageDatabase.IsImageDisplayable(image));
                    Assert.IsTrue(imageDatabase.IsImageRowInRange(image));

                    // retrieval by table
                    imageExpectation.Verify(allImageTable.Rows[image]);
                }

                // reopen database for test and refresh images so next iteration of the loop checks state after reload
                imageDatabase = ImageDatabase.CreateOrOpen(imageDatabase.FilePath, imageDatabase);
                Assert.IsTrue(imageDatabase.TryGetImages(nextFilter));
            }

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
            Assert.IsTrue(templateDatabase.TemplateTable.Rows.Count == numberOfStandardControls);
            this.VerifyControls(templateDatabase);

            DataRow newControl = templateDatabase.AddUserDefinedControl(Constants.Control.Counter);
            Assert.IsTrue(templateDatabase.TemplateTable.Rows.Count == numberOfStandardControls + 1);
            this.VerifyControl(newControl);

            newControl = templateDatabase.AddUserDefinedControl(Constants.Control.FixedChoice);
            Assert.IsTrue(templateDatabase.TemplateTable.Rows.Count == numberOfStandardControls + 2);
            this.VerifyControl(newControl);

            newControl = templateDatabase.AddUserDefinedControl(Constants.Control.Flag);
            Assert.IsTrue(templateDatabase.TemplateTable.Rows.Count == numberOfStandardControls + 3);
            this.VerifyControl(newControl);

            newControl = templateDatabase.AddUserDefinedControl(Constants.Control.Note);
            Assert.IsTrue(templateDatabase.TemplateTable.Rows.Count == numberOfStandardControls + 4);
            this.VerifyControl(newControl);
            this.VerifyTemplateDatabase(templateDatabase, templateDatabaseBaseFileName);

            templateDatabase.RemoveUserDefinedControl(templateDatabase.TemplateTable.Rows[numberOfStandardControls + 2]);
            Assert.IsTrue(templateDatabase.TemplateTable.Rows.Count == numberOfStandardControls + 3);
            templateDatabase.RemoveUserDefinedControl(templateDatabase.TemplateTable.Rows[numberOfStandardControls + 2]);
            Assert.IsTrue(templateDatabase.TemplateTable.Rows.Count == numberOfStandardControls + 2);
            templateDatabase.RemoveUserDefinedControl(templateDatabase.TemplateTable.Rows[numberOfStandardControls + 0]);
            Assert.IsTrue(templateDatabase.TemplateTable.Rows.Count == numberOfStandardControls + 1);
            templateDatabase.RemoveUserDefinedControl(templateDatabase.TemplateTable.Rows[numberOfStandardControls + 0]);
            Assert.IsTrue(templateDatabase.TemplateTable.Rows.Count == numberOfStandardControls);
            this.VerifyTemplateDatabase(templateDatabase, templateDatabaseBaseFileName);

            int iterations = 10;
            for (int iteration = 0; iteration < iterations; ++iteration)
            {
                DataRow noteControl = templateDatabase.AddUserDefinedControl(Constants.Control.Note);
                this.VerifyControl(noteControl);
                DataRow flagControl = templateDatabase.AddUserDefinedControl(Constants.Control.Flag);
                this.VerifyControl(flagControl);
                DataRow choiceControl = templateDatabase.AddUserDefinedControl(Constants.Control.FixedChoice);
                choiceControl[Constants.Control.List] = "DefaultChoice|OtherChoice";
                templateDatabase.SyncControlToDatabase(choiceControl);
                this.VerifyControl(newControl);
                DataRow counterControl = templateDatabase.AddUserDefinedControl(Constants.Control.Counter);
                this.VerifyControl(counterControl);
            }

            // modify control and spreadsheet orders
            // control order ends up reverse order from ID, spreadsheet order is alphabetical
            Dictionary<string, long> newControlOrderByDataLabel = new Dictionary<string, long>();
            long controlOrder = templateDatabase.TemplateTable.Rows.Count;
            for (int row = 0; row < templateDatabase.TemplateTable.Rows.Count; --controlOrder, ++row)
            {
                string dataLabel = templateDatabase.TemplateTable.Rows[row].GetStringField(Constants.Control.DataLabel);
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
            templateDatabase.RemoveUserDefinedControl(templateDatabase.TemplateTable.Rows[numberOfStandardControls + 22]);
            templateDatabase.RemoveUserDefinedControl(templateDatabase.TemplateTable.Rows[numberOfStandardControls + 16]);
            this.VerifyTemplateDatabase(templateDatabase, templateDatabaseBaseFileName);

            // create a database to capture the current template into its template table
            ImageDatabase imageDatabase = this.CreateImageDatabase(templateDatabase, TestConstant.File.DefaultNewImageDatabaseFileName);

            // modify UX properties of some controls by data row manipulation
            int copyableIndex = numberOfStandardControls + (3 * iterations) - 1;
            DataRow copyableControl = templateDatabase.TemplateTable.Rows[copyableIndex];
            bool modifiedCopyable = !Boolean.Parse(copyableControl.GetStringField(Constants.Control.Copyable));
            copyableControl[Constants.Control.Copyable] = modifiedCopyable;
            templateDatabase.SyncControlToDatabase(copyableControl);
            Assert.IsTrue(templateDatabase.TemplateTable.Rows[copyableIndex].GetStringField(Constants.Control.Copyable) == modifiedCopyable.ToString());

            int defaultValueIndex = numberOfStandardControls + (3 * iterations) - 4;
            DataRow defaultValueControl = templateDatabase.TemplateTable.Rows[defaultValueIndex];
            string modifiedDefaultValue = "Default value modification roundtrip.";
            defaultValueControl[Constants.Control.DefaultValue] = modifiedDefaultValue;
            templateDatabase.SyncControlToDatabase(defaultValueControl);
            Assert.IsTrue(templateDatabase.TemplateTable.Rows[defaultValueIndex].GetStringField(Constants.Control.DefaultValue) == modifiedDefaultValue);

            int labelIndex = numberOfStandardControls + (3 * iterations) - 3;
            DataRow labelControl = templateDatabase.TemplateTable.Rows[labelIndex];
            string modifiedLabel = "Label modification roundtrip.";
            labelControl[Constants.Control.Label] = modifiedLabel;
            templateDatabase.SyncControlToDatabase(labelControl);
            Assert.IsTrue(templateDatabase.TemplateTable.Rows[labelIndex].GetStringField(Constants.Control.Label) == modifiedLabel);

            int listIndex = numberOfStandardControls + (3 * iterations) - 2;
            DataRow listControl = templateDatabase.TemplateTable.Rows[listIndex];
            string modifiedList = listControl.GetStringField(Constants.Control.List) + "|NewChoice0|NewChoice1";
            listControl[Constants.Control.List] = modifiedList;
            templateDatabase.SyncControlToDatabase(listControl);
            Assert.IsTrue(templateDatabase.TemplateTable.Rows[listIndex].GetStringField(Constants.Control.List) == modifiedList);

            int tooltipIndex = numberOfStandardControls + (3 * iterations) - 3;
            DataRow tooltipControl = templateDatabase.TemplateTable.Rows[tooltipIndex];
            string modifiedTooltip = "Tooltip modification roundtrip.";
            tooltipControl[Constants.Control.Tooltip] = modifiedTooltip;
            templateDatabase.SyncControlToDatabase(tooltipControl);
            Assert.IsTrue(templateDatabase.TemplateTable.Rows[tooltipIndex].GetStringField(Constants.Control.Tooltip) == modifiedTooltip);

            int widthIndex = numberOfStandardControls + (3 * iterations) - 2;
            DataRow widthControl = templateDatabase.TemplateTable.Rows[widthIndex];
            string modifiedWidth = "1000";
            widthControl[Constants.Control.TextBoxWidth] = modifiedWidth;
            templateDatabase.SyncControlToDatabase(widthControl);
            Assert.IsTrue(templateDatabase.TemplateTable.Rows[widthIndex].GetStringField(Constants.Control.TextBoxWidth) == modifiedWidth);

            int visibleIndex = numberOfStandardControls + (3 * iterations) - 3;
            DataRow visibleControl = templateDatabase.TemplateTable.Rows[visibleIndex];
            bool modifiedVisible = !Boolean.Parse(visibleControl.GetStringField(Constants.Control.Visible));
            visibleControl[Constants.Control.Visible] = modifiedVisible;
            templateDatabase.SyncControlToDatabase(visibleControl);
            Assert.IsTrue(templateDatabase.TemplateTable.Rows[visibleIndex].GetStringField(Constants.Control.Visible) == modifiedVisible.ToString());

            // reopen the template database and check again
            string templateDatabaseFilePath = templateDatabase.FilePath;
            templateDatabase = TemplateDatabase.CreateOrOpen(templateDatabaseFilePath);
            this.VerifyTemplateDatabase(templateDatabase, templateDatabaseFilePath);
            Assert.IsTrue(templateDatabase.TemplateTable.Rows.Count == numberOfStandardControls + (4 * iterations) - 2);
            Assert.IsTrue(templateDatabase.TemplateTable.Columns.Count == TestConstant.TemplateTableColumns.Count);
            this.VerifyControls(templateDatabase);
            Assert.IsTrue(templateDatabase.TemplateTable.Rows[copyableIndex].GetStringField(Constants.Control.Copyable) == modifiedCopyable.ToString());
            Assert.IsTrue(templateDatabase.TemplateTable.Rows[defaultValueIndex].GetStringField(Constants.Control.DefaultValue) == modifiedDefaultValue);
            Assert.IsTrue(templateDatabase.TemplateTable.Rows[labelIndex].GetStringField(Constants.Control.Label) == modifiedLabel);
            Assert.IsTrue(templateDatabase.TemplateTable.Rows[listIndex].GetStringField(Constants.Control.List) == modifiedList);
            Assert.IsTrue(templateDatabase.TemplateTable.Rows[tooltipIndex].GetStringField(Constants.Control.Tooltip) == modifiedTooltip);
            Assert.IsTrue(templateDatabase.TemplateTable.Rows[visibleIndex].GetStringField(Constants.Control.Visible) == modifiedVisible.ToString());
            Assert.IsTrue(templateDatabase.TemplateTable.Rows[widthIndex].GetStringField(Constants.Control.TextBoxWidth) == modifiedWidth);

            // reopen the image database to synchronize its template table with the modified table in the current template
            imageDatabase = ImageDatabase.CreateOrOpen(imageDatabase.FilePath, templateDatabase);
            Assert.IsTrue(imageDatabase.TemplateSynchronizationIssues.Count == 0);
            this.VerifyTemplateDatabase(imageDatabase, imageDatabase.FilePath);
            Assert.IsTrue(imageDatabase.TemplateTable.Rows.Count == numberOfStandardControls + (4 * iterations) - 2);
            Assert.IsTrue(imageDatabase.TemplateTable.Columns.Count == TestConstant.TemplateTableColumns.Count);
            this.VerifyControls(imageDatabase);
            Assert.IsTrue(imageDatabase.TemplateTable.Rows[copyableIndex].GetStringField(Constants.Control.Copyable) == modifiedCopyable.ToString());
            Assert.IsTrue(imageDatabase.TemplateTable.Rows[defaultValueIndex].GetStringField(Constants.Control.DefaultValue) == modifiedDefaultValue);
            Assert.IsTrue(imageDatabase.TemplateTable.Rows[labelIndex].GetStringField(Constants.Control.Label) == modifiedLabel);
            Assert.IsTrue(imageDatabase.TemplateTable.Rows[listIndex].GetStringField(Constants.Control.List) == modifiedList);
            Assert.IsTrue(imageDatabase.TemplateTable.Rows[tooltipIndex].GetStringField(Constants.Control.Tooltip) == modifiedTooltip);
            Assert.IsTrue(imageDatabase.TemplateTable.Rows[visibleIndex].GetStringField(Constants.Control.Visible) == modifiedVisible.ToString());
            Assert.IsTrue(imageDatabase.TemplateTable.Rows[widthIndex].GetStringField(Constants.Control.TextBoxWidth) == modifiedWidth);
        }

        [TestMethod]
        public void GenerateControls()
        {
            List<DatabaseExpectations> databaseExpectations = new List<DatabaseExpectations>()
            {
                new DatabaseExpectations()
                {
                    ImageDatabaseFileName = TestConstant.File.CarnivoreNewImageDatabaseFileName,
                    TemplateDatabaseFileName = TestConstant.File.CarnivoreTemplateDatabaseFileName,
                    ExpectedControls = 5 + 10
                },
                new DatabaseExpectations()
                {
                    ImageDatabaseFileName = Constants.File.DefaultImageDatabaseFileName,
                    TemplateDatabaseFileName = TestConstant.File.DefaultTemplateDatabaseFileName2015,
                    ExpectedControls = 6 + 12
                }
            };

            foreach (DatabaseExpectations databaseExpectation in databaseExpectations)
            {
                ImageDatabase imageDatabase = this.CreateImageDatabase(databaseExpectation.TemplateDatabaseFileName, databaseExpectation.ImageDatabaseFileName);
                ImageTableEnumerator imageEnumerator = new ImageTableEnumerator(imageDatabase);

                DataEntryControls controls = new DataEntryControls();
                controls.Generate(imageDatabase, imageEnumerator);

                Assert.IsTrue(controls.ControlsByDataLabel.Count == databaseExpectation.ExpectedControls, "Expected {0} controls to be generated but {1} were.", databaseExpectation.ExpectedControls, controls.ControlsByDataLabel.Count);
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
            Assert.IsTrue(imageDatabase.TryGetImages(ImageQualityFilter.All));

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
            martenMarkerExpectation.Verify(imageDatabase.MarkersTable.Rows[0]);

            MarkerExpectation bobcatMarkerExpectation = new MarkerExpectation();
            bobcatMarkerExpectation.ID = 2;
            bobcatMarkerExpectation.UserDefinedCountersByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Counter0, String.Empty);
            bobcatMarkerExpectation.UserDefinedCountersByDataLabel.Add(TestConstant.DefaultDatabaseColumn.CounterWithCustomDataLabel, String.Empty);
            bobcatMarkerExpectation.UserDefinedCountersByDataLabel.Add(TestConstant.DefaultDatabaseColumn.CounterNotVisible, String.Empty);
            bobcatMarkerExpectation.UserDefinedCountersByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Counter3, String.Empty);
            bobcatMarkerExpectation.Verify(imageDatabase.MarkersTable.Rows[1]);

            // verify ImageDataTable
            Assert.IsTrue(imageDatabase.ImageDataTable.Columns.Count == TestConstant.DefaultImageTableColumns.Count);
            Assert.IsTrue(imageDatabase.ImageDataTable.Rows.Count == imagesExpected);

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
            martenExpectation.Verify(imageDatabase.ImageDataTable.Rows[0]);

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
            bobcatExpectation.Verify(imageDatabase.ImageDataTable.Rows[1]);
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

            Assert.IsTrue(imageDatabase.GetImageValue(-1, Constants.DatabaseColumn.ID) == String.Empty);
            Assert.IsTrue(imageDatabase.GetImageValue(imageDatabase.CurrentlySelectedImageCount, Constants.DatabaseColumn.ID) == String.Empty);

            Assert.IsFalse(imageDatabase.IsImageCorrupt(-1));
            Assert.IsFalse(imageDatabase.IsImageCorrupt(imageDatabase.CurrentlySelectedImageCount));

            Assert.IsFalse(imageDatabase.IsImageDisplayable(-1));
            Assert.IsFalse(imageDatabase.IsImageDisplayable(imageDatabase.CurrentlySelectedImageCount));

            Assert.IsFalse(imageDatabase.IsImageRowInRange(-1));
            Assert.IsFalse(imageDatabase.IsImageRowInRange(imageDatabase.CurrentlySelectedImageCount));

            ImageProperties imageProperties = new ImageProperties(imageDatabase.ImageDataTable.Rows[0]);
            imageProperties.ID = Constants.Database.InvalidID;
            imageProperties.FileName = null;
            DataRow imageRow;
            Assert.IsFalse(imageDatabase.TryGetImage(imageProperties, out imageRow));
            Assert.IsNull(imageRow);

            // template table synchronization
            // remove choices and change a note to a choice to produce a type failure
            DataRow choiceControl = templateDatabase.GetControlFromTemplateTable(TestConstant.DefaultDatabaseColumn.Choice0);
            choiceControl[Constants.Control.List] = "Choice0|Choice1|Choice2|Choice3|Choice4|Choice5|Choice6|Choice7";
            templateDatabase.SyncControlToDatabase(choiceControl);
            DataRow noteControl = templateDatabase.GetControlFromTemplateTable(TestConstant.DefaultDatabaseColumn.Note0);
            noteControl[Constants.Control.Type] = Constants.Control.FixedChoice;
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

        private void VerifyControl(DataRow control)
        {
            foreach (string column in TestConstant.TemplateTableColumns)
            {
                object value = control[column];
                Assert.IsNotNull(value);
                Assert.IsTrue(value is long || value is string);
            }
        }

        private void VerifyControls(TemplateDatabase database)
        {
            for (int row = 0; row < database.TemplateTable.Rows.Count; ++row)
            {
                DataRow control = database.TemplateTable.Rows[row];

                // sanity check control
                this.VerifyControl(control);

                // verify controls are sorted in control order and that control order is ones based
                long controlOrder = (long)control[Constants.Control.ControlOrder];
                Assert.IsTrue(controlOrder == row + 1);
            }
        }

        private void VerifyTemplateDatabase(TemplateDatabase templateDatabase, string templateDatabaseBaseFileName)
        {
            // sanity checks
            Assert.IsNotNull(templateDatabase);
            Assert.IsNotNull(templateDatabase.FilePath);
            Assert.IsNotNull(templateDatabase.TemplateTable);
            Assert.IsNotNull(templateDatabase.TemplateTable.Columns);
            Assert.IsNotNull(templateDatabase.TemplateTable.Rows);

            // FilePath checks
            string templateDatabaseFileName = Path.GetFileName(templateDatabase.FilePath);
            Assert.IsTrue(templateDatabaseFileName.StartsWith(Path.GetFileNameWithoutExtension(templateDatabaseBaseFileName)));
            Assert.IsTrue(templateDatabaseFileName.EndsWith(Path.GetExtension(templateDatabaseBaseFileName)));
            Assert.IsTrue(File.Exists(templateDatabase.FilePath));

            // TemplateTable checks
            Assert.IsTrue(templateDatabase.TemplateTable.Columns.Count == TestConstant.TemplateTableColumns.Count);
            List<long> ids = new List<long>();
            List<long> controlOrders = new List<long>();
            List<long> spreadsheetOrders = new List<long>();
            for (int row = 0; row < templateDatabase.TemplateTable.Rows.Count; ++row)
            {
                DataRow control = templateDatabase.TemplateTable.Rows[row];
                ids.Add((long)control[Constants.DatabaseColumn.ID]);
                controlOrders.Add((long)control[Constants.Control.ControlOrder]);
                spreadsheetOrders.Add((long)control[Constants.Control.SpreadsheetOrder]);
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
            Assert.IsTrue(imageDatabase.GetImageSetFilter() == ImageQualityFilter.All);
            Assert.IsTrue(imageDatabase.GetImageSetRowIndex() == 0);
            Assert.IsTrue(imageDatabase.GetImageSetLog() == Constants.Database.ImageSetDefaultLog);
            Assert.IsTrue(imageDatabase.IsMagnifierEnabled());
        }

        private void VerifyDefaultMarkerTableContent(ImageDatabase imageDatabase, int imagesExpected)
        {
            Assert.IsTrue(imageDatabase.MarkersTable.Columns.Count == TestConstant.DefaultMarkerTableColumns.Count);
            Assert.IsTrue(imageDatabase.MarkersTable.Rows.Count == imagesExpected);
        }

        private void VerifyDefaultTemplateTableContent(TemplateDatabase templateDatabase)
        {
            Assert.IsTrue(templateDatabase.TemplateTable.Rows.Count == TestConstant.DefaultImageTableColumns.Count - 1);
            TestConstant.DefaultExpectation.File.Verify(templateDatabase.TemplateTable.Rows[0]);
            TestConstant.DefaultExpectation.RelativePath.Verify(templateDatabase.TemplateTable.Rows[1]);
            TestConstant.DefaultExpectation.Folder.Verify(templateDatabase.TemplateTable.Rows[2]);
            TestConstant.DefaultExpectation.Date.Verify(templateDatabase.TemplateTable.Rows[3]);
            TestConstant.DefaultExpectation.Time.Verify(templateDatabase.TemplateTable.Rows[4]);
            TestConstant.DefaultExpectation.ImageQuality.Verify(templateDatabase.TemplateTable.Rows[5]);
            TestConstant.DefaultExpectation.MarkForDeletion.Verify(templateDatabase.TemplateTable.Rows[6]);
            TestConstant.DefaultExpectation.Counter0.Verify(templateDatabase.TemplateTable.Rows[7]);
            TestConstant.DefaultExpectation.Choice0.Verify(templateDatabase.TemplateTable.Rows[8]);
            TestConstant.DefaultExpectation.Note0.Verify(templateDatabase.TemplateTable.Rows[9]);
            TestConstant.DefaultExpectation.Flag0.Verify(templateDatabase.TemplateTable.Rows[10]);
            TestConstant.DefaultExpectation.CounterWithCustomDataLabel.Verify(templateDatabase.TemplateTable.Rows[11]);
            TestConstant.DefaultExpectation.ChoiceWithCustomDataLabel.Verify(templateDatabase.TemplateTable.Rows[12]);
            TestConstant.DefaultExpectation.NoteWithCustomDataLabel.Verify(templateDatabase.TemplateTable.Rows[13]);
            TestConstant.DefaultExpectation.FlagWithCustomDataLabel.Verify(templateDatabase.TemplateTable.Rows[14]);
            TestConstant.DefaultExpectation.CounterNotVisible.Verify(templateDatabase.TemplateTable.Rows[15]);
            TestConstant.DefaultExpectation.ChoiceNotVisible.Verify(templateDatabase.TemplateTable.Rows[16]);
            TestConstant.DefaultExpectation.NoteNotVisible.Verify(templateDatabase.TemplateTable.Rows[17]);
            TestConstant.DefaultExpectation.FlagNotVisible.Verify(templateDatabase.TemplateTable.Rows[18]);
            TestConstant.DefaultExpectation.Counter3.Verify(templateDatabase.TemplateTable.Rows[19]);
            TestConstant.DefaultExpectation.Choice3.Verify(templateDatabase.TemplateTable.Rows[20]);
            TestConstant.DefaultExpectation.Note3.Verify(templateDatabase.TemplateTable.Rows[21]);
            TestConstant.DefaultExpectation.Flag3.Verify(templateDatabase.TemplateTable.Rows[22]);
        }
    }
}
