using Carnassial.Database;
using Carnassial.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;

namespace Carnassial.UnitTests
{
    public class CarnassialTest
    {
        protected CarnassialTest()
        {
            this.WorkingDirectory = Environment.CurrentDirectory;

            // Constants.Images needs to load resources from Carnassial.exe and falls back to Application.ResourceAssembly if Application.Current isn't set
            // for unit tests neither Current or ResourceAssembly gets set as Carnassial.exe is not the entry point
            if (Application.ResourceAssembly == null)
            {
                Application.ResourceAssembly = typeof(Constant.Images).Assembly;
            }
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
        protected FileDatabase CloneFileDatabase(string templateDatabaseBaseFileName, string fileDatabaseFileName)
        {
            TemplateDatabase templateDatabase = this.CloneTemplateDatabase(templateDatabaseBaseFileName);

            string fileDatabaseSourceFilePath = Path.Combine(this.WorkingDirectory, fileDatabaseFileName);
            string fileDatabaseCloneFilePath = this.GetUniqueFilePathForTest(fileDatabaseFileName);
            File.Copy(fileDatabaseSourceFilePath, fileDatabaseCloneFilePath, true);

            return FileDatabase.CreateOrOpen(fileDatabaseCloneFilePath, templateDatabase, false, CustomSelectionOperator.And);
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
        /// Creates a data row from the specified FileExpectation.  Verifies the file isn't already in the database and does not add it to the database.
        /// </summary>
        protected ImageRow CreateFile(FileDatabase fileDatabase, TimeZoneInfo imageSetTimeZone, FileExpectations fileExpectation, out DateTimeAdjustment dateTimeAdjustment)
        {
            FileInfo fileInfo = new FileInfo(Path.Combine(this.WorkingDirectory, fileExpectation.RelativePath, fileExpectation.FileName));
            ImageRow file;
            Assert.IsFalse(fileDatabase.GetOrCreateFile(fileInfo, imageSetTimeZone, out file));
            dateTimeAdjustment = file.TryReadDateTimeOriginalFromMetadata(fileDatabase.FolderPath, imageSetTimeZone);
            return file;
        }

        /// <summary>
        /// Clones the specified template database and creates a file database unique to the calling test.
        /// </summary>
        protected FileDatabase CreateFileDatabase(string templateDatabaseBaseFileName, string fileDatabaseBaseFileName)
        {
            TemplateDatabase templateDatabase = this.CloneTemplateDatabase(templateDatabaseBaseFileName);
            return this.CreateFileDatabase(templateDatabase, fileDatabaseBaseFileName);
        }

        /// <summary>
        /// Creates a file database unique to the calling test.
        /// </summary>
        protected FileDatabase CreateFileDatabase(TemplateDatabase templateDatabase, string fileDatabaseBaseFileName)
        {
            string fileDatabaseFilePath = this.GetUniqueFilePathForTest(fileDatabaseBaseFileName);
            if (File.Exists(fileDatabaseFilePath))
            {
                File.Delete(fileDatabaseFilePath);
            }

            return FileDatabase.CreateOrOpen(fileDatabaseFilePath, templateDatabase, false, CustomSelectionOperator.And);
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

            // ensure subdirectory contains default files
            List<string> defaultFiles = new List<string>()
            {
                TestConstant.FileExpectation.DaylightBobcat.FileName,
                TestConstant.FileExpectation.InfraredMarten.FileName
            };
            foreach (string fileName in defaultFiles)
            {
                FileInfo sourceFile = new FileInfo(Path.Combine(this.WorkingDirectory, fileName));
                FileInfo destinationFile = new FileInfo(Path.Combine(this.WorkingDirectory, this.TestClassSubdirectory, fileName));
                if (destinationFile.Exists == false ||
                    destinationFile.LastWriteTimeUtc < sourceFile.LastWriteTimeUtc)
                {
                    sourceFile.CopyTo(destinationFile.FullName, true);
                }
            }
        }

        protected Dictionary<string, string> LoadMetadata(FileDatabase fileDatabase, FileExpectations fileExpectation)
        {
            string filePath = Path.Combine(this.WorkingDirectory, fileExpectation.RelativePath, fileExpectation.FileName);
            Dictionary<string, string> metadata = Utilities.LoadMetadata(filePath);
            Assert.IsTrue(metadata.Count > 40, "Expected at least 40 metadata fields to be retrieved from {0}", filePath);
            // example information returned from ExifTool
            // field name                   Bushnell                                    Reconyx
            // --- fields of likely interest for image analysis ---
            // Create Date                  2016.02.24 04:59:46                         [Bushnell only]
            // Date/Time Original           2016.02.24 04:59:46                         2015.01.28 11:17:34 [but located in Makernote rather than the standard EXIF field!]
            // Modify Date                  2016.02.24 04:59:46                         [Bushnell only]
            // --- fields possibly of interest interest for image analysis ---
            // Exposure Time                0                                           1/84
            // Shutter Speed                0                                           1/84
            // Software                     BS683BWYx05209                              [Bushnell only]
            // Firmware Version             [Reconyx only]                              3.3.0
            // Trigger Mode                 [Reconyx only]                              Motion Detection
            // Sequence                     [Reconyx only]                              1 of 5
            // Ambient Temperature          [Reconyx only]                              0 C
            // Serial Number                [Reconyx only]                              H500DE01120343
            // Infrared Illuminator         [Reconyx only]                              Off
            // User Label                   [Reconyx only]                              CCPF DS02
            // --- fields of little interest (Bushnell often uses placeholder values which don't change) ---
            // ExifTool Version Number      10.14                                       10.14
            // File Name                    BushnellTrophyHD-119677C-20160224-056.JPG   Reconyx-HC500-20150128-201.JPG
            // Directory                    Carnassial/UnitTests/bin/Debug               Carnassial/UnitTests/bin/Debug/CarnivoreTestImages
            // File Size                    731 kB                                      336 kB
            // File Modification Date/Time  <file time from last build>                 <file time from last build>
            // File Access Date/Time        <file time from last build>                 <file time from last build>
            // File Creation Date/Time      <file time from last build>                 <file time from last build>
            // File Permissions             rw-rw-rw-                                   rw-rw-rw-
            // File Type                    JPEG                                        JPEG
            // File Type Extension          jpg                                         jpg
            // MIME Type                    image/jpeg                                  image/jpeg
            // Exif Byte Order              Little-endian(Intel, II)                    Little-endian(Intel, II)
            // Image Description            M2E6L0-0R350B362                            [Bushnell only]
            // Make                         [blank]                                     [Bushnell only]
            // Camera Model Name            [blank]                                     [Bushnell only]
            // Orientation                  Horizontal(normal)                          [Bushnell only]
            // X Resolution                 96                                          72
            // Y Resolution                 96                                          72
            // Resolution Unit              inches                                      inches
            // Y Cb Cr Positioning          Co-sited                                    Co-sited
            // Copyright                    Copyright 2002                              [Bushnell only]
            // F Number                     2.8                                         [Bushnell only]
            // Exposure Program             Aperture-priority AE                        [Bushnell only]
            // ISO                          100                                         50
            // Exif Version                 0210                                        0220
            // Components Configuration     Y, Cb, Cr, -                                Y, Cb, Cr, -
            // Compressed Bits Per Pixel    0.7419992711                                [Bushnell only]
            // Shutter Speed Value          1                                           [Bushnell only]
            // Aperture Value               2.6                                         [Bushnell only]
            // Exposure Compensation        +2                                          [Bushnell only]
            // Max Aperture Value           2.6                                         [Bushnell only]
            // Metering Mode                Average                                     [Bushnell only]
            // Light Source                 Daylight                                    [Bushnell only]
            // Flash                        No Flash                                    [Bushnell only]
            // Warning                      [minor]Unrecognized MakerNotes              [Bushnell only]
            // Flashpix Version             0100                                        0100
            // Color Space                  sRGB                                        sRGB
            // Exif Image Width             3264                                        2048
            // Exif Image Height            2448                                        1536
            // Related Sound File           RelatedSound                                [Bushnell only]
            // Interoperability Index       R98 - DCF basic file(sRGB)                  [Bushnell only]
            // Interoperability Version     0100                                        [Bushnell only]
            // Exposure Index               1                                           [Bushnell only]
            // Sensing Method               One-chip color area                         [Bushnell only]
            // File Source                  Digital Camera                              [Bushnell only]
            // Scene Type                   Directly photographed                       [Bushnell only]
            // Compression                  JPEG(old - style)                           [Bushnell only]
            // Thumbnail Offset             1312                                        [Bushnell only]
            // Thumbnail Length             5768                                        [Bushnell only]
            // Image Width                  3264                                        2048
            // Image Height                 2448                                        1536
            // Encoding Process             Baseline DCT, Huffman coding                Baseline DCT, Huffman coding
            // Bits Per Sample              8                                           8
            // Color Components             3                                           3
            // Y Cb Cr Sub Sampling         YCbCr4:2:2 (2 1)                            YCbCr4:2:2 (2 1)
            // Aperture                     2.8                                         [Bushnell only]
            // Image Size                   3264x2448                                   2048x1536
            // Megapixels                   8.0                                         3.1
            // Thumbnail Image              <binary data>                               [Bushnell only]
            // Ambient Temperature Fahrenheit [Reconyx only]                            31 F
            // Battery Voltage              [Reconyx only]                              8.65 V
            // Contrast                     [Reconyx only]                              142
            // Brightness                   [Reconyx only]                              238
            // Event Number                 [Reconyx only]                              39
            // Firmware Date                [Reconyx only]                              2011:01:10
            // Maker Note Version           [Reconyx only]                              0xf101
            // Moon Phase                   [Reconyx only]                              First Quarter
            // Motion Sensitivity           [Reconyx only]                              100
            // Sharpness                    [Reconyx only]                              64
            // Saturation                   [Reconyx only]                              144
            return metadata;
        }

        protected List<FileExpectations> PopulateDefaultDatabase(FileDatabase fileDatabase)
        {
            return this.PopulateDefaultDatabase(fileDatabase, false);
        }

        protected List<FileExpectations> PopulateDefaultDatabase(FileDatabase fileDatabase, bool excludeSubfolderFiles)
        {
            TimeZoneInfo imageSetTimeZone = fileDatabase.ImageSet.GetTimeZone();

            // files in same folder as .tdb and .ddb
            DateTimeAdjustment martenDateTimeAdjustment;
            ImageRow martenImage = this.CreateFile(fileDatabase, imageSetTimeZone, TestConstant.FileExpectation.InfraredMarten, out martenDateTimeAdjustment);
            Assert.IsTrue(martenDateTimeAdjustment.HasFlag(DateTimeAdjustment.MetadataDate) &&
                          martenDateTimeAdjustment.HasFlag(DateTimeAdjustment.MetadataTime));

            DateTimeAdjustment bobcatDatetimeAdjustment;
            ImageRow bobcatImage = this.CreateFile(fileDatabase, imageSetTimeZone, TestConstant.FileExpectation.DaylightBobcat, out bobcatDatetimeAdjustment);
            Assert.IsTrue(bobcatDatetimeAdjustment.HasFlag(DateTimeAdjustment.MetadataDate) &&
                          bobcatDatetimeAdjustment.HasFlag(DateTimeAdjustment.MetadataTime));

            fileDatabase.AddFiles(new List<ImageRow>() { martenImage, bobcatImage }, null);
            fileDatabase.SelectFiles(FileSelection.All);

            FileTableEnumerator fileEnumerator = new FileTableEnumerator(fileDatabase);
            Assert.IsTrue(fileEnumerator.TryMoveToFile(0));
            Assert.IsTrue(fileEnumerator.MoveNext());

            ColumnTuplesWithWhere bobcatUpdate = new ColumnTuplesWithWhere();
            bobcatUpdate.Columns.Add(new ColumnTuple(TestConstant.DefaultDatabaseColumn.Choice0, "choice b"));
            bobcatUpdate.Columns.Add(new ColumnTuple(TestConstant.DefaultDatabaseColumn.Counter0, 1.ToString()));
            bobcatUpdate.Columns.Add(new ColumnTuple(TestConstant.DefaultDatabaseColumn.FlagNotVisible, true));
            bobcatUpdate.Columns.Add(new ColumnTuple(TestConstant.DefaultDatabaseColumn.Note3, "bobcat"));
            bobcatUpdate.Columns.Add(new ColumnTuple(TestConstant.DefaultDatabaseColumn.NoteNotVisible, "adult"));
            bobcatUpdate.SetWhere(fileEnumerator.Current.ID);
            fileDatabase.UpdateFiles(new List<ColumnTuplesWithWhere>() { bobcatUpdate });

            long martenImageID = fileDatabase.Files[0].ID;
            fileDatabase.UpdateFile(martenImageID, TestConstant.DefaultDatabaseColumn.Choice0, "choice b");
            fileDatabase.UpdateFile(martenImageID, TestConstant.DefaultDatabaseColumn.Counter0, 1.ToString());
            fileDatabase.UpdateFile(martenImageID, TestConstant.DefaultDatabaseColumn.FlagNotVisible, Boolean.TrueString);
            fileDatabase.UpdateFile(martenImageID, TestConstant.DefaultDatabaseColumn.Note3, "American marten");
            fileDatabase.UpdateFile(martenImageID, TestConstant.DefaultDatabaseColumn.NoteNotVisible, "adult");

            // generate expectations
            List<FileExpectations> fileExpectations = new List<FileExpectations>()
            {
                new FileExpectations(TestConstant.FileExpectation.InfraredMarten),
                new FileExpectations(TestConstant.FileExpectation.DaylightBobcat),
            };

            // files in subfolder
            if (excludeSubfolderFiles == false)
            {
                DateTimeAdjustment martenPairDateTimeAdjustment;
                ImageRow martenPairImage = this.CreateFile(fileDatabase, imageSetTimeZone, TestConstant.FileExpectation.DaylightMartenPair, out martenPairDateTimeAdjustment);
                Assert.IsTrue(martenPairDateTimeAdjustment.HasFlag(DateTimeAdjustment.MetadataDate) &&
                              martenPairDateTimeAdjustment.HasFlag(DateTimeAdjustment.MetadataTime));

                DateTimeAdjustment coyoteDatetimeAdjustment;
                ImageRow coyoteImage = this.CreateFile(fileDatabase, imageSetTimeZone, TestConstant.FileExpectation.DaylightCoyote, out coyoteDatetimeAdjustment);
                Assert.IsTrue(coyoteDatetimeAdjustment.HasFlag(DateTimeAdjustment.MetadataDate) &&
                              coyoteDatetimeAdjustment.HasFlag(DateTimeAdjustment.MetadataTime));

                fileDatabase.AddFiles(new List<ImageRow>() { martenPairImage, coyoteImage }, null);
                fileDatabase.SelectFiles(FileSelection.All);

                ColumnTuplesWithWhere coyoteImageUpdate = new ColumnTuplesWithWhere();
                coyoteImageUpdate.Columns.Add(new ColumnTuple(TestConstant.DefaultDatabaseColumn.Note3, "coyote"));
                coyoteImageUpdate.Columns.Add(new ColumnTuple(TestConstant.DefaultDatabaseColumn.NoteNotVisible, "adult"));
                coyoteImageUpdate.Columns.Add(new ColumnTuple(TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel, String.Empty));
                coyoteImageUpdate.Columns.Add(new ColumnTuple(TestConstant.DefaultDatabaseColumn.Note0, "escaped field, because a comma is present"));
                coyoteImageUpdate.SetWhere(fileEnumerator.Current.ID);
                fileDatabase.UpdateFiles(new List<ColumnTuplesWithWhere>() { coyoteImageUpdate });

                long martenPairImageID = fileDatabase.Files[3].ID;
                fileDatabase.UpdateFile(martenPairImageID, TestConstant.DefaultDatabaseColumn.Note3, "American marten");
                fileDatabase.UpdateFile(martenPairImageID, TestConstant.DefaultDatabaseColumn.NoteNotVisible, "adult");
                fileDatabase.UpdateFile(martenPairImageID, TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel, String.Empty);
                fileDatabase.UpdateFile(martenPairImageID, TestConstant.DefaultDatabaseColumn.Note0, "escaped field due to presence of \",\"");

                fileExpectations.Add(new FileExpectations(TestConstant.FileExpectation.DaylightMartenPair));
                fileExpectations.Add(new FileExpectations(TestConstant.FileExpectation.DaylightCoyote));
            }

            // pull the file data table again so the updates are visible to .csv export
            fileDatabase.SelectFiles(FileSelection.All);

            // complete setting expectations
            for (int fileIndex = 0; fileIndex < fileDatabase.Files.RowCount; ++fileIndex)
            {
                FileExpectations fileExpectation = fileExpectations[fileIndex];
                fileExpectation.ID = fileIndex + 1;
            }
            return fileExpectations;
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
