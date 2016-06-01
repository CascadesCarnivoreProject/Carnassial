using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using Timelapse.Database;

namespace Timelapse.UnitTests
{
    [TestClass]
    public class DatabaseTests : TimelapseTest
    {
        private static readonly ReadOnlyCollection<string> TemplateTableColumns = new List<string>()
            {
                Constants.Control.ControlOrder,
                Constants.Control.SpreadsheetOrder,
                Constants.Control.DefaultValue,
                Constants.Control.Label,
                Constants.Control.DataLabel,
                Constants.Control.Tooltip,
                Constants.Control.TextBoxWidth,
                Constants.Control.Copyable,
                Constants.Control.Visible,
                Constants.Control.List,
                Constants.DatabaseColumn.ID,
                Constants.DatabaseColumn.Type
            }.AsReadOnly();

        [TestMethod]
        public void AddImages()
        {
            string currentDirectoryName = Path.GetFileName(Environment.CurrentDirectory);

            ImageDatabase database = this.CreateImageDatabase(TestConstants.File.CarnivoreTemplateDatabaseFileName, TestConstants.File.CarnivoreImageDatabaseFileName);
            this.PopulateCarnivoreDatabase(database);

            // check images after initial add and again after reopen
            for (int pass = 0; pass < 2; ++pass)
            {
                ImageProperties martenImage = database.GetImage(0);
                Assert.IsTrue(martenImage.Date == "24-Feb-2016");
                Assert.IsTrue(martenImage.FileName == TestConstants.File.InfraredMartenImage);
                Assert.IsTrue(martenImage.ID == 1);
                Assert.IsTrue(martenImage.ImageQuality == ImageQualityFilter.Ok);
                Assert.IsTrue(martenImage.ImageTaken == DateTime.MinValue);
                Assert.IsTrue(martenImage.InitialRootFolderName == currentDirectoryName);
                Assert.IsTrue(martenImage.Time == "04:59:46");

                ImageProperties bobcatImage = database.GetImage(1);
                Assert.IsTrue(bobcatImage.Date == "05-Aug-2015");
                Assert.IsTrue(bobcatImage.FileName == TestConstants.File.DaylightBobcatImage);
                Assert.IsTrue(bobcatImage.ID == 2);
                Assert.IsTrue(bobcatImage.ImageQuality == ImageQualityFilter.Ok);
                Assert.IsTrue(bobcatImage.ImageTaken == DateTime.MinValue);
                Assert.IsTrue(bobcatImage.InitialRootFolderName == currentDirectoryName);
                Assert.IsTrue(bobcatImage.Time == "08:06:23");

                database = new ImageDatabase(Environment.CurrentDirectory, TestConstants.File.CarnivoreImageDatabaseFileName, null);
                Assert.IsTrue(database.TryGetImagesAll());
            }
        }

        [TestMethod]
        public void CreateUpdateReuseTemplateDatabase()
        {
            // remove any previously existing database
            string databaseFilePath = Path.Combine(Environment.CurrentDirectory, "CreateTemplateTest" + Constants.File.TemplateDatabaseFileExtension);
            if (File.Exists(databaseFilePath))
            {
                File.Delete(databaseFilePath);
            }

            // create and populate template database
            TemplateDatabase database = new TemplateDatabase(databaseFilePath);
            Assert.IsNotNull(database);
            Assert.IsTrue(database.FilePath == databaseFilePath);
            Assert.IsTrue(File.Exists(database.FilePath));
            Assert.IsNotNull(database.TemplateTable);
            Assert.IsNotNull(database.TemplateTable.Rows);
            Assert.IsTrue(database.TemplateTable.Rows.Count == 6);
            Assert.IsTrue(database.TemplateTable.Columns.Count == DatabaseTests.TemplateTableColumns.Count);
            this.VerifyControls(database);

            database.AddControl(Constants.Control.Counter);
            Assert.IsTrue(database.TemplateTable.Rows.Count == 7);
            this.VerifyControl(database.TemplateTable.Rows[database.TemplateTable.Rows.Count - 1]);

            database.AddControl(Constants.Control.FixedChoice);
            Assert.IsTrue(database.TemplateTable.Rows.Count == 8);
            this.VerifyControl(database.TemplateTable.Rows[database.TemplateTable.Rows.Count - 1]);

            database.AddControl(Constants.Control.Flag);
            Assert.IsTrue(database.TemplateTable.Rows.Count == 9);
            this.VerifyControl(database.TemplateTable.Rows[database.TemplateTable.Rows.Count - 1]);

            database.AddControl(Constants.Control.Note);
            Assert.IsTrue(database.TemplateTable.Rows.Count == 10);
            this.VerifyControl(database.TemplateTable.Rows[database.TemplateTable.Rows.Count - 1]);

            database.RemoveControl(database.TemplateTable.Rows[2]);
            Assert.IsTrue(database.TemplateTable.Rows.Count == 9);
            database.RemoveControl(database.TemplateTable.Rows[2]);
            Assert.IsTrue(database.TemplateTable.Rows.Count == 8);
            database.RemoveControl(database.TemplateTable.Rows[0]);
            Assert.IsTrue(database.TemplateTable.Rows.Count == 7);
            database.RemoveControl(database.TemplateTable.Rows[0]);
            Assert.IsTrue(database.TemplateTable.Rows.Count == 6);

            int iterations = 10;
            for (int iteration = 0; iteration < iterations; ++iteration)
            {
                database.AddControl(Constants.Control.Note);
                database.AddControl(Constants.Control.Flag);
                database.AddControl(Constants.Control.FixedChoice);
                database.AddControl(Constants.Control.Counter);
            }

            database.RemoveControl(database.TemplateTable.Rows[22]);
            database.RemoveControl(database.TemplateTable.Rows[16]);

            // reopen and modify the template database
            database = new TemplateDatabase(databaseFilePath);
            Assert.IsNotNull(database);
            Assert.IsTrue(database.FilePath == databaseFilePath);
            Assert.IsTrue(File.Exists(database.FilePath));
            Assert.IsNotNull(database.TemplateTable);
            Assert.IsNotNull(database.TemplateTable.Rows);
            Assert.IsTrue(database.TemplateTable.Rows.Count == 6 + (4 * iterations) - 2);
            Assert.IsTrue(database.TemplateTable.Columns.Count == DatabaseTests.TemplateTableColumns.Count);
            this.VerifyControls(database);

            int controlIndex = 6 + (3 * iterations) - 3;
            DataRow control = database.TemplateTable.Rows[controlIndex];
            string tooltip = "Field modification roundtrip.";
            control[Constants.Control.Tooltip] = tooltip;
            database.SyncControlToDatabase(control);
            Assert.IsTrue((string)database.TemplateTable.Rows[controlIndex][Constants.Control.Tooltip] == tooltip);
        }

        [TestMethod]
        public void GenerateControls()
        {
            List<DatabaseExpectations> databaseExpectations = new List<DatabaseExpectations>()
            {
                new DatabaseExpectations()
                {
                    ImageDatabaseFileName = TestConstants.File.CarnivoreImageDatabaseFileName,
                    TemplateDatabaseFileName = TestConstants.File.CarnivoreTemplateDatabaseFileName,
                    ExpectedControls = 5 + 10
                },
                new DatabaseExpectations()
                {
                    ImageDatabaseFileName = Constants.File.DefaultImageDatabaseFileName,
                    TemplateDatabaseFileName = Constants.File.DefaultTemplateDatabaseFileName,
                    ExpectedControls = 6 + 12
                }
            };

            foreach (DatabaseExpectations databaseExpectation in databaseExpectations)
            {
                ImageDatabase database = this.CreateImageDatabase(databaseExpectation.TemplateDatabaseFileName, databaseExpectation.ImageDatabaseFileName);
                ImageTableEnumerator imageEnumerator = new ImageTableEnumerator(database);

                Controls controls = new Controls();
                controls.GenerateControls(database, imageEnumerator);

                Assert.IsTrue(controls.ControlFromDataLabel.Count == databaseExpectation.ExpectedControls, "Expected {0} controls to be generated but {1} were.", databaseExpectation.ExpectedControls, controls.ControlFromDataLabel.Count);
            }
        }

        [TestMethod]
        public void RoundtripCsv()
        {
            // create database, push test images into the database, and load the image data table
            ImageDatabase database = this.CreateImageDatabase(TestConstants.File.CarnivoreTemplateDatabaseFileName, TestConstants.File.CarnivoreImageDatabaseFileName);
            this.PopulateCarnivoreDatabase(database);

            // roundtrip data through .csv
            CsvReaderWriter csvReaderWriter = new CsvReaderWriter();
            string initialCsvFilePath = Path.Combine(Environment.CurrentDirectory, Path.GetFileNameWithoutExtension(Constants.File.DefaultImageDatabaseFileName) + ".csv");
            csvReaderWriter.ExportToCsv(database, initialCsvFilePath);
            csvReaderWriter.ImportFromCsv(database, initialCsvFilePath);

            string roundtripCsvFilePath = Path.Combine(Environment.CurrentDirectory, Path.GetFileNameWithoutExtension(Constants.File.DefaultImageDatabaseFileName) + "-roundtrip.csv");
            csvReaderWriter.ExportToCsv(database, roundtripCsvFilePath);

            string initialCsv = File.ReadAllText(initialCsvFilePath);
            string roundtripCsv = File.ReadAllText(roundtripCsvFilePath);
            Assert.IsTrue(initialCsv == roundtripCsv, "Initial and roundtrip .csv files don't match.");
        }

        private void VerifyControl(DataRow control)
        {
            foreach (string column in DatabaseTests.TemplateTableColumns)
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
    }
}
