using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using Timelapse.Database;

namespace Timelapse.UnitTests
{
    [TestClass]
    public class DatabaseTests : TimelapseTest
    {
        [TestMethod]
        public void AddImages()
        {
            // create database for test
            ImageDatabase imageDatabase = this.CreateImageDatabase(TestConstant.File.CarnivoreTemplateDatabaseFileName, TestConstant.File.CarnivoreNewImageDatabaseFileName);
            List<ImageExpectations> imageExpectations = this.PopulateCarnivoreDatabase(imageDatabase);

            // check images after initial add and again after reopen
            string currentDirectoryName = Path.GetFileName(imageDatabase.FolderPath);
            for (int pass = 0; pass < 2; ++pass)
            {
                for (int image = 0; image < imageExpectations.Count; ++image)
                {
                    ImageExpectations imageExpectation = imageExpectations[image];
                    ImageProperties imageProperties = imageDatabase.GetImage(image);
                    imageExpectation.Verify(imageProperties, false);
                }

                // reopen database for test and refresh images so next iteration of the loop checks state after reload
                imageDatabase = new ImageDatabase(imageDatabase.FolderPath, imageDatabase.FileName, null);
                Assert.IsTrue(imageDatabase.TryGetImagesAll());
            }
        }

        [TestMethod]
        public void CreateUpdateReuseTemplateDatabase()
        {
            string templateDatabaseBaseFileName = TestConstant.File.DefaultNewTemplateDatabaseFileName;
            TemplateDatabase templateDatabase = this.CreateTemplateDatabase(templateDatabaseBaseFileName);

            // populate template database
            this.VerifyTemplateDatabase(templateDatabase, templateDatabaseBaseFileName);
            Assert.IsTrue(templateDatabase.TemplateTable.Rows.Count == 6);
            this.VerifyControls(templateDatabase);

            DataRow newControl = templateDatabase.AddControl(Constants.Control.Counter);
            Assert.IsTrue(templateDatabase.TemplateTable.Rows.Count == 7);
            this.VerifyControl(newControl);

            newControl = templateDatabase.AddControl(Constants.Control.FixedChoice);
            Assert.IsTrue(templateDatabase.TemplateTable.Rows.Count == 8);
            this.VerifyControl(newControl);

            newControl = templateDatabase.AddControl(Constants.Control.Flag);
            Assert.IsTrue(templateDatabase.TemplateTable.Rows.Count == 9);
            this.VerifyControl(newControl);

            newControl = templateDatabase.AddControl(Constants.Control.Note);
            Assert.IsTrue(templateDatabase.TemplateTable.Rows.Count == 10);
            this.VerifyControl(newControl);

            templateDatabase.RemoveControl(templateDatabase.TemplateTable.Rows[2]);
            Assert.IsTrue(templateDatabase.TemplateTable.Rows.Count == 9);
            templateDatabase.RemoveControl(templateDatabase.TemplateTable.Rows[2]);
            Assert.IsTrue(templateDatabase.TemplateTable.Rows.Count == 8);
            templateDatabase.RemoveControl(templateDatabase.TemplateTable.Rows[0]);
            Assert.IsTrue(templateDatabase.TemplateTable.Rows.Count == 7);
            templateDatabase.RemoveControl(templateDatabase.TemplateTable.Rows[0]);
            Assert.IsTrue(templateDatabase.TemplateTable.Rows.Count == 6);

            int iterations = 10;
            for (int iteration = 0; iteration < iterations; ++iteration)
            {
                newControl = templateDatabase.AddControl(Constants.Control.Note);
                this.VerifyControl(newControl);
                newControl = templateDatabase.AddControl(Constants.Control.Flag);
                this.VerifyControl(newControl);
                newControl = templateDatabase.AddControl(Constants.Control.FixedChoice);
                this.VerifyControl(newControl);
                newControl = templateDatabase.AddControl(Constants.Control.Counter);
                this.VerifyControl(newControl);
            }

            templateDatabase.RemoveControl(templateDatabase.TemplateTable.Rows[22]);
            templateDatabase.RemoveControl(templateDatabase.TemplateTable.Rows[16]);

            // reopen and modify the template database
            string templateDatabaseFilePath = templateDatabase.FilePath;
            templateDatabase = new TemplateDatabase(templateDatabaseFilePath);
            this.VerifyTemplateDatabase(templateDatabase, templateDatabaseFilePath);
            Assert.IsTrue(templateDatabase.TemplateTable.Rows.Count == 6 + (4 * iterations) - 2);
            Assert.IsTrue(templateDatabase.TemplateTable.Columns.Count == TestConstant.TemplateTableColumns.Count);
            this.VerifyControls(templateDatabase);

            int controlIndex = 6 + (3 * iterations) - 3;
            DataRow control = templateDatabase.TemplateTable.Rows[controlIndex];
            string tooltip = "Field modification roundtrip.";
            control[Constants.Control.Tooltip] = tooltip;
            templateDatabase.SyncControlToDatabase(control);
            Assert.IsTrue((string)templateDatabase.TemplateTable.Rows[controlIndex][Constants.Control.Tooltip] == tooltip);
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

                Assert.IsTrue(controls.ControlFromDataLabel.Count == databaseExpectation.ExpectedControls, "Expected {0} controls to be generated but {1} were.", databaseExpectation.ExpectedControls, controls.ControlFromDataLabel.Count);
            }
        }

        [TestMethod]
        public void ImageDatabase2023()
        {
            // load database
            string imageDatabaseBaseFileName = TestConstant.File.DefaultImageDatabaseFileName2023;
            ImageDatabase imageDatabase = this.CloneImageDatabase(TestConstant.File.DefaultTemplateDatabaseFileName2015, imageDatabaseBaseFileName);
            Assert.IsTrue(imageDatabase.TryGetImagesAll());

            // verify template portion
            this.VerifyTemplateDatabase(imageDatabase, imageDatabaseBaseFileName);
            this.VerifyTemplateTable(imageDatabase);

            // verify image set table
            Assert.IsTrue(imageDatabase.GetImageSetFilter() == ImageQualityFilter.All);
            Assert.IsTrue(imageDatabase.GetImageSetRowIndex() == 0);
            Assert.IsTrue(imageDatabase.GetImageSetLog() == Constants.Database.ImageSetDefaultLog);
            Assert.IsTrue(imageDatabase.IsMagnifierEnabled());

            // verify markers table
            int imagesExpected = 2;
            Assert.IsTrue(imageDatabase.MarkersTable.Columns.Count == TestConstant.DefaultMarkerTableColumns.Count);
            Assert.IsTrue(imageDatabase.MarkersTable.Rows.Count == imagesExpected);

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
            ImageExpectations martenExpectation = new ImageExpectations(TestConstant.Expectations.InfraredMartenImage);
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
            martenExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.CounterNotVisible, TestConstant.Expectations.CounterNotVisible.DefaultValue);
            martenExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.ChoiceNotVisible, Constants.ControlDefault.FixedChoiceValue);
            martenExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.NoteNotVisible, Constants.ControlDefault.NoteValue);
            martenExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.FlagNotVisible, Constants.ControlDefault.FlagValue);
            martenExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Counter3, "1");
            martenExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Choice3, Constants.ControlDefault.FixedChoiceValue);
            martenExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Note3, "note");
            martenExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Flag3, Constants.Boolean.True);
            martenExpectation.Verify(imageDatabase.ImageDataTable.Rows[0]);

            ImageExpectations bobcatExpectation = new ImageExpectations(TestConstant.Expectations.DaylightBobcatImage);
            bobcatExpectation.ID = 2;
            bobcatExpectation.InitialRootFolderName = initialRootFolderName;
            bobcatExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Counter0, TestConstant.Expectations.Counter0.DefaultValue);
            bobcatExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Choice0, "choice a");
            bobcatExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Note0, "1");
            bobcatExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Flag0, Constants.Boolean.True);
            bobcatExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.CounterWithCustomDataLabel, "3");
            bobcatExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.ChoiceWithCustomDataLabel, "with , comma");
            bobcatExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel, Constants.ControlDefault.NoteValue);
            bobcatExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.FlagWithCustomDataLabel, Constants.Boolean.True);
            bobcatExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.CounterNotVisible, TestConstant.Expectations.CounterNotVisible.DefaultValue);
            bobcatExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.ChoiceNotVisible, Constants.ControlDefault.FixedChoiceValue);
            bobcatExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.NoteNotVisible, Constants.ControlDefault.NoteValue);
            bobcatExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.FlagNotVisible, Constants.ControlDefault.FlagValue);
            bobcatExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Counter3, TestConstant.Expectations.Counter3.DefaultValue);
            bobcatExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Choice3, Constants.ControlDefault.FixedChoiceValue);
            bobcatExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Note3, Constants.ControlDefault.NoteValue);
            bobcatExpectation.UserDefinedColumnsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Flag3, Constants.Boolean.True);
            bobcatExpectation.Verify(imageDatabase.ImageDataTable.Rows[1]);
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
            csvReaderWriter.ImportFromCsv(imageDatabase, initialCsvFilePath);

            string roundtripCsvFilePath = Path.Combine(Path.GetDirectoryName(initialCsvFilePath), Path.GetFileNameWithoutExtension(initialCsvFilePath) + "-roundtrip" + Constants.File.CsvFileExtension);
            csvReaderWriter.ExportToCsv(imageDatabase, roundtripCsvFilePath);

            string initialCsv = File.ReadAllText(initialCsvFilePath);
            string roundtripCsv = File.ReadAllText(roundtripCsvFilePath);
            Assert.IsTrue(initialCsv == roundtripCsv, "Initial and roundtrip .csv files don't match.");
        }

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
        }

        private void VerifyTemplateTable(TemplateDatabase templateDatabase)
        {
            Assert.IsTrue(templateDatabase.TemplateTable.Rows.Count == TestConstant.DefaultImageTableColumns.Count - 1);
            TestConstant.Expectations.File.Verify(templateDatabase.TemplateTable.Rows[0]);
            TestConstant.Expectations.Folder.Verify(templateDatabase.TemplateTable.Rows[1]);
            TestConstant.Expectations.Date.Verify(templateDatabase.TemplateTable.Rows[2]);
            TestConstant.Expectations.Time.Verify(templateDatabase.TemplateTable.Rows[3]);
            TestConstant.Expectations.ImageQuality.Verify(templateDatabase.TemplateTable.Rows[4]);
            TestConstant.Expectations.MarkForDeletion.Verify(templateDatabase.TemplateTable.Rows[5]);
            TestConstant.Expectations.Counter0.Verify(templateDatabase.TemplateTable.Rows[6]);
            TestConstant.Expectations.Choice0.Verify(templateDatabase.TemplateTable.Rows[7]);
            TestConstant.Expectations.Note0.Verify(templateDatabase.TemplateTable.Rows[8]);
            TestConstant.Expectations.Flag0.Verify(templateDatabase.TemplateTable.Rows[9]);
            TestConstant.Expectations.CounterWithCustomDataLabel.Verify(templateDatabase.TemplateTable.Rows[10]);
            TestConstant.Expectations.ChoiceWithCustomDataLabel.Verify(templateDatabase.TemplateTable.Rows[11]);
            TestConstant.Expectations.NoteWithCustomDataLabel.Verify(templateDatabase.TemplateTable.Rows[12]);
            TestConstant.Expectations.FlagWithCustomDataLabel.Verify(templateDatabase.TemplateTable.Rows[13]);
            TestConstant.Expectations.CounterNotVisible.Verify(templateDatabase.TemplateTable.Rows[14]);
            TestConstant.Expectations.ChoiceNotVisible.Verify(templateDatabase.TemplateTable.Rows[15]);
            TestConstant.Expectations.NoteNotVisible.Verify(templateDatabase.TemplateTable.Rows[16]);
            TestConstant.Expectations.FlagNotVisible.Verify(templateDatabase.TemplateTable.Rows[17]);
            TestConstant.Expectations.Counter3.Verify(templateDatabase.TemplateTable.Rows[18]);
            TestConstant.Expectations.Choice3.Verify(templateDatabase.TemplateTable.Rows[19]);
            TestConstant.Expectations.Note3.Verify(templateDatabase.TemplateTable.Rows[20]);
            TestConstant.Expectations.Flag3.Verify(templateDatabase.TemplateTable.Rows[21]);
        }
    }
}
