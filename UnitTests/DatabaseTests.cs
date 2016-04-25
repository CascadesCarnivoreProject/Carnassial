using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using Timelapse.Database;

namespace Timelapse.UnitTests
{
    [TestClass]
    public class DatabaseTests : TimelapseTest
    {
        [TestMethod]
        public void GenerateControls()
        {
            ImageDatabase database = this.CreateImageDatabase();
            
            Controls controls = new Controls();
            controls.GenerateControls(database, 0);

            int expectedControls = 6 + 10;
            Assert.IsTrue(controls.ControlFromDataLabel.Count == expectedControls, "Expected {0} controls to be generated but {1} were.", expectedControls, controls.ControlFromDataLabel.Count);
        }

        [TestMethod]
        public void RoundtripCsv()
        {
            // create database, push test images into the database, and load the image data table
            ImageDatabase database = this.CreateImageDatabase();
            this.PopulateImageDatabase(database);

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
