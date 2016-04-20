using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using Timelapse.Database;
using Timelapse.Util;

namespace Timelapse.UnitTests
{
    [TestClass]
    public class DatabaseTests
    {
        [TestMethod]
        public void GenerateControls()
        {
            ImageDatabase database = this.CreateImageDatabase();
            
            Controls controls = new Controls();
            controls.GenerateControls(database);

            int expectedControls = 6 + 10;
            Assert.IsTrue(controls.ControlFromDataLabel.Count == expectedControls, "Expected {0} controls to be generated but {1} were.", expectedControls, controls.ControlFromDataLabel.Count);
        }

        [TestMethod]
        public void RoundtripCsv()
        {
            // create database, push test images into the database, and load the image data table
            ImageDatabase database = this.CreateImageDatabase();
            FileInfo imageFileInfo = new FileInfo("BushnellTrophyHD-119677C-20160224-056.JPG");
            ImageProperties image1 = new ImageProperties(database.FolderPath, imageFileInfo);
            image1.Date = DateTimeHandler.StandardDateString(imageFileInfo.LastWriteTimeUtc);
            image1.DateFileCreation = imageFileInfo.CreationTimeUtc;
            image1.Time = DateTimeHandler.StandardTimeString(imageFileInfo.LastWriteTimeUtc);
            ImageProperties image2 = new ImageProperties(database.FolderPath, imageFileInfo);
            database.AddImages(new List<ImageProperties>() { image1, image2 }, null);
            database.CreateWhiteSpaceColumn();
            database.TrimImageAndTemplateTableWhitespace();  // Trim the white space from all the data
            database.InitializeMarkerTableFromDataTable();
            Assert.IsTrue(database.TryGetImagesAll());
            Assert.IsTrue(database.TryMoveToFirstImage());
            Assert.IsTrue(database.TryMoveToNextImage());

            // simulate data entry
            ColumnTuplesWithWhere image2Update = new ColumnTuplesWithWhere();
            image2Update.Columns.Add(new ColumnTuple("Station", "ID02 Unit Test Camera Location"));
            image2Update.Columns.Add(new ColumnTuple("TriggerSource", "critter"));
            image2Update.Columns.Add(new ColumnTuple("Identification", "American marten"));
            image2Update.Columns.Add(new ColumnTuple("Confidence", "high"));
            image2Update.Columns.Add(new ColumnTuple("GroupType", "individual"));
            image2Update.Columns.Add(new ColumnTuple("Age", "adult"));
            image2Update.Columns.Add(new ColumnTuple("Pelage", ""));
            image2Update.Columns.Add(new ColumnTuple("Activity", "unknown"));
            image2Update.Columns.Add(new ColumnTuple("Comments", "escaped field due presence of ,"));
            image2Update.Columns.Add(new ColumnTuple("Survey", "Timelapse database unit tests"));
            image2Update.SetWhere(database.CurrentImage.ID);
            database.UpdateImages(new List<ColumnTuplesWithWhere>() { image2Update });

            // pull the image data table again so the updates are visible to .csv export
            Assert.IsTrue(database.TryGetImagesAll());

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

        private ImageDatabase CreateImageDatabase()
        {
            string folderPath = Environment.CurrentDirectory;
            TemplateDatabase template;
            bool result = TemplateDatabase.TryOpen(Path.Combine(folderPath, Constants.File.DefaultTemplateDatabaseFileName), out template);
            Assert.IsTrue(result);

            string imageDatabaseFilePath = Path.Combine(folderPath, Constants.File.DefaultImageDatabaseFileName);
            if (File.Exists(imageDatabaseFilePath))
            {
                File.Delete(imageDatabaseFilePath);
            }

            ImageDatabase imageDatabase = new ImageDatabase(Path.GetDirectoryName(imageDatabaseFilePath), Path.GetFileName(imageDatabaseFilePath));
            result = imageDatabase.TryCreateImageDatabase(template);
            Assert.IsTrue(result);
            imageDatabase.CreateTables();
            imageDatabase.CreateLookupTables();

            return imageDatabase;
        }
    }
}
