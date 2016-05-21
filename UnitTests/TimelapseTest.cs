using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using Timelapse.Database;

namespace Timelapse.UnitTests
{
    public class TimelapseTest
    {
        protected ImageDatabase CreateImageDatabase(string templateFileName, string imageDatabaseFileName)
        {
            string folderPath = Environment.CurrentDirectory;
            TemplateDatabase template;
            bool result = TemplateDatabase.TryOpen(Path.Combine(folderPath, templateFileName), out template);
            Assert.IsTrue(result);

            string imageDatabaseFilePath = Path.Combine(folderPath, imageDatabaseFileName);
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

        protected void PopulateCarnivoreDatabase(ImageDatabase database)
        {
            FileInfo martenFileInfo = new FileInfo(TestConstants.File.InfraredMartenImage);
            ImageProperties image1 = new ImageProperties(database.FolderPath, martenFileInfo);
            FileInfo bobcatFileInfo = new FileInfo(TestConstants.File.DaylightBobcatImage);
            ImageProperties image2 = new ImageProperties(database.FolderPath, bobcatFileInfo);
            database.AddImages(new List<ImageProperties>() { image1, image2 }, null);
            database.CreateWhiteSpaceColumn();
            database.TrimImageAndTemplateTableWhitespace();  // Trim the white space from all the data
            database.InitializeMarkerTableFromDataTable();
            Assert.IsTrue(database.TryGetImagesAll());

            ImageTableEnumerator imageEnumerator = new ImageTableEnumerator(database);
            Assert.IsTrue(imageEnumerator.TryMoveToImage(0));
            Assert.IsTrue(imageEnumerator.MoveNext());

            // simulate data entry
            ColumnTuplesWithWhere image2Update = new ColumnTuplesWithWhere();
            image2Update.Columns.Add(new ColumnTuple("Station", "ID02 Unit Test Camera Location"));
            image2Update.Columns.Add(new ColumnTuple("TriggerSource", "critter"));
            image2Update.Columns.Add(new ColumnTuple("Identification", "American marten"));
            image2Update.Columns.Add(new ColumnTuple("Confidence", "high"));
            image2Update.Columns.Add(new ColumnTuple("GroupType", "individual"));
            image2Update.Columns.Add(new ColumnTuple("Age", "adult"));
            image2Update.Columns.Add(new ColumnTuple("Pelage", String.Empty));
            image2Update.Columns.Add(new ColumnTuple("Activity", "unknown"));
            image2Update.Columns.Add(new ColumnTuple("Comments", "escaped field due presence of ,"));
            image2Update.Columns.Add(new ColumnTuple("Survey", "Timelapse database unit tests"));
            image2Update.SetWhere(imageEnumerator.Current.ID);
            database.UpdateImages(new List<ColumnTuplesWithWhere>() { image2Update });

            // pull the image data table again so the updates are visible to .csv export
            Assert.IsTrue(database.TryGetImagesAll());
        }
    }
}
