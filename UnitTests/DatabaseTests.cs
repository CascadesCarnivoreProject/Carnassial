using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
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
            string currentDirectoryName = Path.GetFileName(Environment.CurrentDirectory);

            ImageDatabase database = this.CreateImageDatabase(TestConstants.File.CarnivoreTemplateDatabaseFileName, TestConstants.File.CarnivoreImageDatabaseFileName);
            this.PopulateCarnivoreDatabase(database);

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
                ImageTableEnumerator ImageEnumerator = new ImageTableEnumerator(database);

                Controls controls = new Controls();
                controls.GenerateControls(database, ImageEnumerator);

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
    }
}
