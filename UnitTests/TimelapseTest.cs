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

        /// <summary>
        /// Gets the name of an optional subdirectory under the working directory from which all tests in the current class access database and image files.
        /// Classes wishing to use this mechanism should call EnsureTestClassSubdirectory() in their constructor to set up the subdirectory.
        /// </summary>
        protected string TestClassSubdirectory { get; private set; }

        /// <summary>
        /// Gets the path to the root folder under which all tests execute.
        /// </summary>
        protected string WorkingDirectory { get; private set; }

        /// <summary>
        /// Clones the specified template and image databases and opens the image database's clone.
        /// </summary>
        protected ImageDatabase CloneImageDatabase(string templateDatabaseBaseFileName, string imageDatabaseFileName)
        {
            TemplateDatabase templateDatabase = this.CloneTemplateDatabase(templateDatabaseBaseFileName);

            string imageDatabaseSourceFilePath = Path.Combine(this.WorkingDirectory, imageDatabaseFileName);
            string imageDatabaseCloneFilePath = this.GetUniqueFilePathForTest(imageDatabaseFileName);
            File.Copy(imageDatabaseSourceFilePath, imageDatabaseCloneFilePath, true);

            return ImageDatabase.CreateOrOpen(imageDatabaseCloneFilePath, templateDatabase);
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
            bool result = TemplateDatabase.TryCreateOrOpen(templateDatabaseCloneFilePath, out clone);
            Assert.IsTrue(result, "Open of template database '{0}' failed.", templateDatabaseCloneFilePath);
            return clone;
        }

        /// <summary>
        /// Clones the specified template database and creates an image database unique to the calling test.
        /// </summary>
        protected ImageDatabase CreateImageDatabase(string templateDatabaseBaseFileName, string imageDatabaseBaseFileName)
        {
            TemplateDatabase templateDatabase = this.CloneTemplateDatabase(templateDatabaseBaseFileName);
            return this.CreateImageDatabase(templateDatabase, imageDatabaseBaseFileName);
        }

        /// <summary>
        /// Creates an image database unique to the calling test.
        /// </summary>
        protected ImageDatabase CreateImageDatabase(TemplateDatabase templateDatabase, string imageDatabaseBaseFileName)
        {
            string imageDatabaseFilePath = this.GetUniqueFilePathForTest(imageDatabaseBaseFileName);
            if (File.Exists(imageDatabaseFilePath))
            {
                File.Delete(imageDatabaseFilePath);
            }

            return ImageDatabase.CreateOrOpen(imageDatabaseFilePath, templateDatabase);
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
            return TemplateDatabase.CreateOrOpen(templateDatabaseFilePath);
        }

        protected void EnsureTestClassSubdirectory()
        {
            // ensure subdirectory exists
            this.TestClassSubdirectory = this.GetType().Name;
            string subdirectoryPath = Path.Combine(this.WorkingDirectory, this.TestClassSubdirectory);
            if (Directory.Exists(subdirectoryPath) == false)
            {
                Directory.CreateDirectory(subdirectoryPath);
            }

            // ensure subdirectory contains default images
            List<string> defaultImages = new List<string>()
            {
                TestConstant.DefaultExpectation.DaylightBobcatImage.FileName,
                TestConstant.DefaultExpectation.InfraredMartenImage.FileName
            };
            foreach (string imageFileName in defaultImages)
            {
                FileInfo sourceImageFile = new FileInfo(Path.Combine(this.WorkingDirectory, imageFileName));
                FileInfo destinationImageFile = new FileInfo(Path.Combine(this.WorkingDirectory, this.TestClassSubdirectory, imageFileName));
                if (destinationImageFile.Exists == false ||
                    destinationImageFile.LastWriteTimeUtc < sourceImageFile.LastWriteTimeUtc)
                {
                    sourceImageFile.CopyTo(destinationImageFile.FullName, true);
                }
            }
        }

        protected List<ImageExpectations> PopulateCarnivoreDatabase(ImageDatabase imageDatabase)
        {
            FileInfo martenFileInfo = new FileInfo(Path.Combine(this.WorkingDirectory, TestConstant.File.CarnivoreDirectoryName, TestConstant.File.DaylightMartenPairImage));
            ImageRow martenImage;
            Assert.IsFalse(imageDatabase.GetOrCreateImage(martenFileInfo, out martenImage));
            Assert.IsTrue(martenImage.TryUseImageTaken((BitmapMetadata)martenImage.LoadBitmapFrame(imageDatabase.FolderPath).Metadata) == DateTimeAdjustment.MetadataNotUsed);

            FileInfo coyoteFileInfo = new FileInfo(Path.Combine(this.WorkingDirectory, TestConstant.File.CarnivoreDirectoryName, TestConstant.File.DaylightCoyoteImage));
            ImageRow coyoteImage;
            imageDatabase.GetOrCreateImage(coyoteFileInfo, out coyoteImage);
            Assert.IsTrue(coyoteImage.TryUseImageTaken((BitmapMetadata)coyoteImage.LoadBitmapFrame(imageDatabase.FolderPath).Metadata) == DateTimeAdjustment.MetadataDateAndTimeUsed);

            imageDatabase.AddImages(new List<ImageRow>() { martenImage, coyoteImage }, null);
            imageDatabase.SelectDataTableImagesAll();

            ImageTableEnumerator imageEnumerator = new ImageTableEnumerator(imageDatabase);
            Assert.IsTrue(imageEnumerator.TryMoveToImage(0));
            Assert.IsTrue(imageEnumerator.MoveNext());

            // cover CSV or image XML import path
            ColumnTuplesWithWhere coyoteImageUpdate = new ColumnTuplesWithWhere();
            coyoteImageUpdate.Columns.Add(new ColumnTuple("Station", "DS02"));
            coyoteImageUpdate.Columns.Add(new ColumnTuple("TriggerSource", "critter"));
            coyoteImageUpdate.Columns.Add(new ColumnTuple("Identification", "coyote"));
            coyoteImageUpdate.Columns.Add(new ColumnTuple("Confidence", "high"));
            coyoteImageUpdate.Columns.Add(new ColumnTuple("GroupType", "single"));
            coyoteImageUpdate.Columns.Add(new ColumnTuple("Age", "adult"));
            coyoteImageUpdate.Columns.Add(new ColumnTuple("Pelage", String.Empty));
            coyoteImageUpdate.Columns.Add(new ColumnTuple("Activity", "unknown"));
            coyoteImageUpdate.Columns.Add(new ColumnTuple("Comments", "escaped field due presence of '"));
            coyoteImageUpdate.Columns.Add(new ColumnTuple("Survey", "Timelapse carnivore database unit tests"));
            coyoteImageUpdate.SetWhere(imageEnumerator.Current.ID);
            imageDatabase.UpdateImages(new List<ColumnTuplesWithWhere>() { coyoteImageUpdate });

            // simulate data entry
            long martenImageID = imageDatabase.ImageDataTable[0].ID;
            imageDatabase.UpdateImage(martenImageID, "Station", "DS02");
            imageDatabase.UpdateImage(martenImageID, "TriggerSource", "critter");
            imageDatabase.UpdateImage(martenImageID, "Identification", "American marten");
            imageDatabase.UpdateImage(martenImageID, "Confidence", "high");
            imageDatabase.UpdateImage(martenImageID, "GroupType", "pair");
            imageDatabase.UpdateImage(martenImageID, "Age", "adult");
            imageDatabase.UpdateImage(martenImageID, "Pelage", String.Empty);
            imageDatabase.UpdateImage(martenImageID, "Activity", "unknown");
            imageDatabase.UpdateImage(martenImageID, "Comments", "escaped field due presence of ,");
            imageDatabase.UpdateImage(martenImageID, "Survey", "Timelapse carnivore database unit tests");

            // pull the image data table again so the updates are visible to .csv export
            imageDatabase.SelectDataTableImagesAll();

            // generate expectations
            List<ImageExpectations> imageExpectations = new List<ImageExpectations>()
            {
                new ImageExpectations(TestConstant.DefaultExpectation.DaylightMartenPairImage),
                new ImageExpectations(TestConstant.DefaultExpectation.DaylightCoyoteImage)
            };

            string initialRootFolderName = Path.GetFileName(imageDatabase.FolderPath);
            for (int image = 0; image < imageDatabase.ImageDataTable.RowCount; ++image)
            {
                ImageExpectations imageExpectation = imageExpectations[image];
                imageExpectation.ID = image + 1;
                imageExpectation.InitialRootFolderName = initialRootFolderName;
            }

            return imageExpectations;
        }

        protected List<ImageExpectations> PopulateDefaultDatabase(ImageDatabase imageDatabase)
        {
            FileInfo martenFileInfo = new FileInfo(Path.Combine(this.WorkingDirectory, TestConstant.File.InfraredMartenImage));
            ImageRow martenImage;
            imageDatabase.GetOrCreateImage(martenFileInfo, out martenImage);
            DateTimeAdjustment martenTimeAdjustment = martenImage.TryUseImageTaken((BitmapMetadata)martenImage.LoadBitmapFrame(imageDatabase.FolderPath).Metadata);
            Assert.IsTrue(martenTimeAdjustment == DateTimeAdjustment.MetadataDateAndTimeOneHourLater ||
                          martenTimeAdjustment == DateTimeAdjustment.MetadataDateAndTimeUsed);

            FileInfo bobcatFileInfo = new FileInfo(Path.Combine(this.WorkingDirectory, TestConstant.File.DaylightBobcatImage));
            ImageRow bobcatImage;
            imageDatabase.GetOrCreateImage(bobcatFileInfo, out bobcatImage);
            Assert.IsTrue(bobcatImage.TryUseImageTaken((BitmapMetadata)bobcatImage.LoadBitmapFrame(imageDatabase.FolderPath).Metadata) == DateTimeAdjustment.MetadataDateAndTimeUsed);

            imageDatabase.AddImages(new List<ImageRow>() { martenImage, bobcatImage }, null);
            imageDatabase.SelectDataTableImagesAll();

            ImageTableEnumerator imageEnumerator = new ImageTableEnumerator(imageDatabase);
            Assert.IsTrue(imageEnumerator.TryMoveToImage(0));
            Assert.IsTrue(imageEnumerator.MoveNext());

            // cover CSV or image XML import path
            ColumnTuplesWithWhere bobcatUpdate = new ColumnTuplesWithWhere();
            bobcatUpdate.Columns.Add(new ColumnTuple(TestConstant.DefaultDatabaseColumn.Choice0, "choice b"));
            bobcatUpdate.Columns.Add(new ColumnTuple(TestConstant.DefaultDatabaseColumn.Counter0, 1));
            bobcatUpdate.Columns.Add(new ColumnTuple(TestConstant.DefaultDatabaseColumn.FlagNotVisible, true));
            bobcatUpdate.Columns.Add(new ColumnTuple(TestConstant.DefaultDatabaseColumn.Note3, "bobcat"));
            bobcatUpdate.Columns.Add(new ColumnTuple(TestConstant.DefaultDatabaseColumn.NoteNotVisible, "adult"));
            bobcatUpdate.SetWhere(imageEnumerator.Current.ID);
            imageDatabase.UpdateImages(new List<ColumnTuplesWithWhere>() { bobcatUpdate });

            // simulate data entry
            long martenImageID = imageDatabase.ImageDataTable[0].ID;
            imageDatabase.UpdateImage(martenImageID, TestConstant.DefaultDatabaseColumn.Choice0, "choice b");
            imageDatabase.UpdateImage(martenImageID, TestConstant.DefaultDatabaseColumn.Counter0, 1.ToString());
            imageDatabase.UpdateImage(martenImageID, TestConstant.DefaultDatabaseColumn.FlagNotVisible, Constants.Boolean.True);
            imageDatabase.UpdateImage(martenImageID, TestConstant.DefaultDatabaseColumn.Note3, "American marten");
            imageDatabase.UpdateImage(martenImageID, TestConstant.DefaultDatabaseColumn.NoteNotVisible, "adult");

            // pull the image data table again so the updates are visible to .csv export
            imageDatabase.SelectDataTableImagesAll();

            // generate expectations
            List<ImageExpectations> imageExpectations = new List<ImageExpectations>()
            {
                new ImageExpectations(TestConstant.DefaultExpectation.InfraredMartenImage),
                new ImageExpectations(TestConstant.DefaultExpectation.DaylightBobcatImage)
            };

            string initialRootFolderName = Path.GetFileName(imageDatabase.FolderPath);
            for (int image = 0; image < imageDatabase.ImageDataTable.RowCount; ++image)
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

            if (String.IsNullOrWhiteSpace(this.TestClassSubdirectory))
            {
                return Path.Combine(this.WorkingDirectory, uniqueFileName);
            }
            return Path.Combine(this.WorkingDirectory, this.TestClassSubdirectory, uniqueFileName);
        }
    }
}
