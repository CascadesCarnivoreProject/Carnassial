using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using Timelapse.Database;

namespace Timelapse.UnitTests
{
    public class TimelapseTest
    {
        protected ImageDatabase CreateImageDatabase()
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

        protected void PopulateImageDatabase(ImageDatabase database)
        {
            FileInfo imageFileInfo = new FileInfo("BushnellTrophyHD-119677C-20160224-056.JPG");
            ImageProperties image1 = new ImageProperties(database.FolderPath, imageFileInfo);
            ImageProperties image2 = new ImageProperties(database.FolderPath, imageFileInfo);
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
