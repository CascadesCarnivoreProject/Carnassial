using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media.Imaging;
using Timelapse.Database;

namespace Timelapse.UnitTests
{
    public class TimelapseTest
    {
        protected TimelapseTest()
        {
            this.WorkingDirectory = Environment.CurrentDirectory;
        }

        public TestContext TestContext { get; set; }

        protected string WorkingDirectory { get; set; }

        /// <summary>
        /// Clones the specified template and image databases and opens the image database's clone.
        /// </summary>
        protected ImageDatabase CloneImageDatabase(string templateDatabaseFileName, string imageDatabaseFileName)
        {
            TemplateDatabase templateDatabase = this.CloneTemplateDatabase(templateDatabaseFileName);

            string imageDatabaseSourceFilePath = Path.Combine(this.WorkingDirectory, imageDatabaseFileName);
            string imageDatabaseCloneFilePath = this.GetUniqueFilePathForTest(imageDatabaseFileName);
            File.Copy(imageDatabaseSourceFilePath, imageDatabaseCloneFilePath, true);
            return new ImageDatabase(Path.GetDirectoryName(imageDatabaseCloneFilePath), imageDatabaseCloneFilePath, templateDatabase);
        }

        /// <summary>
        /// Clones the specified template database and opens the clone.
        /// </summary>
        protected TemplateDatabase CloneTemplateDatabase(string templateDatabaseFileName)
        {
            string templateDatabaseSourceFilePath = Path.Combine(this.WorkingDirectory, templateDatabaseFileName);
            string templateDatabaseCloneFilePath = this.GetUniqueFilePathForTest(templateDatabaseFileName);
            File.Copy(templateDatabaseSourceFilePath, templateDatabaseCloneFilePath, true);

            TemplateDatabase clone;
            bool result = TemplateDatabase.TryOpen(templateDatabaseSourceFilePath, out clone);
            Assert.IsTrue(result, "Open of template database '{0}' failed.", templateDatabaseCloneFilePath);
            return clone;
        }

        /// <summary>
        /// Clones the specified template database and creates an image database unique to the calling test.
        /// </summary>
        protected ImageDatabase CreateImageDatabase(string templateDatabaseFileName, string imageDatabaseBaseFileName)
        {
            TemplateDatabase templateDatabase = this.CloneTemplateDatabase(templateDatabaseFileName);

            string imageDatabaseFilePath = this.GetUniqueFilePathForTest(imageDatabaseBaseFileName);
            if (File.Exists(imageDatabaseFilePath))
            {
                File.Delete(imageDatabaseFilePath);
            }

            return new ImageDatabase(Path.GetDirectoryName(imageDatabaseFilePath), Path.GetFileName(imageDatabaseFilePath), templateDatabase);
        }

        /// <summary>
        /// Creates a template database unique to the calling test.
        /// </summary>
        protected TemplateDatabase CreateTemplateDatabase(string templateDatabaseBaseFileName)
        {
            // remove any previously existing database
            string templateDatabaseFilePath = this.GetUniqueFilePathForTest(templateDatabaseBaseFileName);
            if (File.Exists(templateDatabaseFilePath))
            {
                File.Delete(templateDatabaseFilePath);
            }

            // create the new database
            return new TemplateDatabase(templateDatabaseFilePath);
        }

        protected List<ImageExpectations> PopulateCarnivoreDatabase(ImageDatabase imageDatabase)
        {
            FileInfo martenFileInfo = new FileInfo(TestConstant.File.InfraredMartenImage);
            ImageProperties martenImage = new ImageProperties(imageDatabase.FolderPath, martenFileInfo);
            martenImage.TryUseImageTaken((BitmapMetadata)martenImage.LoadBitmapFrame(imageDatabase.FolderPath).Metadata);

            FileInfo bobcatFileInfo = new FileInfo(TestConstant.File.DaylightBobcatImage);
            ImageProperties bobcatImage = new ImageProperties(imageDatabase.FolderPath, bobcatFileInfo);
            bobcatImage.TryUseImageTaken((BitmapMetadata)bobcatImage.LoadBitmapFrame(imageDatabase.FolderPath).Metadata);

            imageDatabase.AddImages(new List<ImageProperties>() { martenImage, bobcatImage }, null);
            Assert.IsTrue(imageDatabase.TryGetImagesAll());

            ImageTableEnumerator imageEnumerator = new ImageTableEnumerator(imageDatabase);
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
            imageDatabase.UpdateImages(new List<ColumnTuplesWithWhere>() { image2Update });

            // pull the image data table again so the updates are visible to .csv export
            Assert.IsTrue(imageDatabase.TryGetImagesAll());

            // generate expectations
            List<ImageExpectations> imageExpectations = new List<ImageExpectations>()
            {
                new ImageExpectations(TestConstant.Expectations.InfraredMartenImage),
                new ImageExpectations(TestConstant.Expectations.DaylightBobcatImage)
            };

            string initialRootFolderName = Path.GetFileName(imageDatabase.FolderPath);
            for (int image = 0; image < imageDatabase.ImageDataTable.Rows.Count; ++image)
            {
                ImageExpectations imageExpectation = imageExpectations[image];
                imageExpectation.ID = image + 1;
                imageExpectation.InitialRootFolderName = initialRootFolderName;
            }

            return imageExpectations;
        }

        protected string GetUniqueFilePathForTest(string baseFileName)
        {
            string uniqueTestIdentifier = String.Format("{0}.{1}", this.GetType().FullName, this.TestContext.TestName);
            string uniqueFileName = String.Format("{0}.{1}{2}", Path.GetFileNameWithoutExtension(baseFileName), uniqueTestIdentifier, Path.GetExtension(baseFileName));
            return Path.Combine(this.WorkingDirectory, uniqueFileName);
        }
    }
}
