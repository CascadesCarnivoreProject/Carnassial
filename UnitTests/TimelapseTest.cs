﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
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
        protected ImageDatabase CloneImageDatabase(string templateDatabaseBaseFileName, string imageDatabaseFileName)
        {
            TemplateDatabase templateDatabase = this.CloneTemplateDatabase(templateDatabaseBaseFileName);

            string imageDatabaseSourceFilePath = Path.Combine(this.WorkingDirectory, imageDatabaseFileName);
            string imageDatabaseCloneFilePath = this.GetUniqueFilePathForTest(imageDatabaseFileName);
            File.Copy(imageDatabaseSourceFilePath, imageDatabaseCloneFilePath, true);

            return new ImageDatabase(imageDatabaseCloneFilePath, templateDatabase);
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
            bool result = TemplateDatabase.TryOpen(templateDatabaseCloneFilePath, out clone);
            Assert.IsTrue(result, "Open of template database '{0}' failed.", templateDatabaseCloneFilePath);
            return clone;
        }

        /// <summary>
        /// Clones the specified template database and creates an image database unique to the calling test.
        /// </summary>
        protected ImageDatabase CreateImageDatabase(string templateDatabaseBaseFileName, string imageDatabaseBaseFileName)
        {
            TemplateDatabase templateDatabase = this.CloneTemplateDatabase(templateDatabaseBaseFileName);

            string imageDatabaseFilePath = this.GetUniqueFilePathForTest(imageDatabaseBaseFileName);
            if (File.Exists(imageDatabaseFilePath))
            {
                File.Delete(imageDatabaseFilePath);
            }

            return new ImageDatabase(imageDatabaseFilePath, templateDatabase);
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
            FileInfo martenFileInfo = new FileInfo(Path.Combine(this.WorkingDirectory, TestConstant.File.CarnivoreDirectoryName, TestConstant.File.DaylightMartenPairImage));
            ImageProperties martenImage = new ImageProperties(imageDatabase.FolderPath, martenFileInfo);
            Assert.IsTrue(martenImage.TryUseImageTaken((BitmapMetadata)martenImage.LoadBitmapFrame(imageDatabase.FolderPath).Metadata) == DateTimeAdjustment.MetadataNotUsed);

            FileInfo coyoteFileInfo = new FileInfo(Path.Combine(this.WorkingDirectory, TestConstant.File.CarnivoreDirectoryName, TestConstant.File.DaylightCoyoteImage));
            ImageProperties coyoteImage = new ImageProperties(imageDatabase.FolderPath, coyoteFileInfo);
            Assert.IsTrue(coyoteImage.TryUseImageTaken((BitmapMetadata)coyoteImage.LoadBitmapFrame(imageDatabase.FolderPath).Metadata) == DateTimeAdjustment.MetadataDateAndTimeUsed);

            imageDatabase.AddImages(new List<ImageProperties>() { martenImage, coyoteImage }, null);
            Assert.IsTrue(imageDatabase.TryGetImages(ImageQualityFilter.All));

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
            long martenImageID = imageDatabase.GetImageID(0);
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
            Assert.IsTrue(imageDatabase.TryGetImages(ImageQualityFilter.All));

            // generate expectations
            List<ImageExpectations> imageExpectations = new List<ImageExpectations>()
            {
                new ImageExpectations(TestConstant.Expectations.DaylightMartenPairImage),
                new ImageExpectations(TestConstant.Expectations.DaylightCoyoteImage)
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

        protected List<ImageExpectations> PopulateDefaultDatabase(ImageDatabase imageDatabase)
        {
            FileInfo martenFileInfo = new FileInfo(Path.Combine(this.WorkingDirectory, TestConstant.File.InfraredMartenImage));
            ImageProperties martenImage = new ImageProperties(imageDatabase.FolderPath, martenFileInfo);
            DateTimeAdjustment martenTimeAdjustment = martenImage.TryUseImageTaken((BitmapMetadata)martenImage.LoadBitmapFrame(imageDatabase.FolderPath).Metadata);
            Assert.IsTrue(martenTimeAdjustment == DateTimeAdjustment.MetadataDateAndTimeOneHourLater ||
                          martenTimeAdjustment == DateTimeAdjustment.MetadataDateAndTimeUsed);

            FileInfo coyoteFileInfo = new FileInfo(Path.Combine(this.WorkingDirectory, TestConstant.File.DaylightBobcatImage));
            ImageProperties bobcatImage = new ImageProperties(imageDatabase.FolderPath, coyoteFileInfo);
            Assert.IsTrue(bobcatImage.TryUseImageTaken((BitmapMetadata)bobcatImage.LoadBitmapFrame(imageDatabase.FolderPath).Metadata) == DateTimeAdjustment.MetadataDateAndTimeUsed);

            imageDatabase.AddImages(new List<ImageProperties>() { martenImage, bobcatImage }, null);
            Assert.IsTrue(imageDatabase.TryGetImages(ImageQualityFilter.All));

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
            long martenImageID = imageDatabase.GetImageID(0);
            imageDatabase.UpdateImage(martenImageID, TestConstant.DefaultDatabaseColumn.Choice0, "choice b");
            imageDatabase.UpdateImage(martenImageID, TestConstant.DefaultDatabaseColumn.Counter0, 1.ToString());
            imageDatabase.UpdateImage(martenImageID, TestConstant.DefaultDatabaseColumn.FlagNotVisible, Constants.Boolean.True);
            imageDatabase.UpdateImage(martenImageID, TestConstant.DefaultDatabaseColumn.Note3, "American marten");
            imageDatabase.UpdateImage(martenImageID, TestConstant.DefaultDatabaseColumn.NoteNotVisible, "adult");

            // pull the image data table again so the updates are visible to .csv export
            Assert.IsTrue(imageDatabase.TryGetImages(ImageQualityFilter.All));

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
