using Carnassial.Data;
using Carnassial.Database;
using Carnassial.Images;
using Carnassial.Interop;
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

            Assert.IsTrue(FileDatabase.TryCreateOrOpen(fileDatabaseCloneFilePath, templateDatabase, false, LogicalOperator.And, out FileDatabase fileDatabase));
            return fileDatabase;
        }

        /// <summary>
        /// Clones the specified template database and opens the clone.
        /// </summary>
        protected TemplateDatabase CloneTemplateDatabase(string templateDatabaseFileName)
        {
            string templateDatabaseSourceFilePath = Path.Combine(this.WorkingDirectory, templateDatabaseFileName);
            string templateDatabaseCloneFilePath = this.GetUniqueFilePathForTest(templateDatabaseFileName);
            File.Copy(templateDatabaseSourceFilePath, templateDatabaseCloneFilePath, true);

            bool result = TemplateDatabase.TryCreateOrOpen(templateDatabaseCloneFilePath, out TemplateDatabase clone);
            Assert.IsTrue(result, "Open of template database '{0}' failed.", templateDatabaseCloneFilePath);
            return clone;
        }

        /// <summary>
        /// Creates a file data table row from the specified FileExpectation.  Verifies the file isn't already in the database and does not add it to the database.
        /// </summary>
        protected ImageRow CreateFile(FileDatabase fileDatabase, TimeZoneInfo imageSetTimeZone, FileExpectations fileExpectation, out MetadataReadResult metadataReadResult)
        {
            FileInfo fileInfo = new FileInfo(Path.Combine(this.WorkingDirectory, fileExpectation.RelativePath, fileExpectation.FileName));
            string relativePath = Path.GetDirectoryName(NativeMethods.GetRelativePathFromDirectoryToFile(fileDatabase.FolderPath, fileInfo));
            ImageRow file = fileDatabase.Files.CreateFile(fileInfo.Name, relativePath);
            using (FileLoadAtom loadAtom = new FileLoadAtom(file.RelativePath, new FileLoad(file), new FileLoad((string)null), 0))
            {
                loadAtom.CreateJpegs(fileDatabase.FolderPath);
                loadAtom.ReadDateTimeOffsets(fileDatabase.FolderPath, imageSetTimeZone);
                metadataReadResult = loadAtom.First.MetadataReadResult;
            }
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

            Assert.IsTrue(FileDatabase.TryCreateOrOpen(fileDatabaseFilePath, templateDatabase, false, LogicalOperator.And, out FileDatabase fileDatabase));
            return fileDatabase;
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
            Assert.IsTrue(TemplateDatabase.TryCreateOrOpen(templateDatabaseFilePath, out TemplateDatabase templateDatabase));
            return templateDatabase;
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

        protected List<FileExpectations> PopulateDefaultDatabase(FileDatabase fileDatabase)
        {
            return this.PopulateDefaultDatabase(fileDatabase, false);
        }

        protected List<FileExpectations> PopulateDefaultDatabase(FileDatabase fileDatabase, bool excludeSubfolderFiles)
        {
            TimeZoneInfo imageSetTimeZone = fileDatabase.ImageSet.GetTimeZoneInfo();

            // files in same folder as .tdb and .ddb
            ImageRow martenImage = this.CreateFile(fileDatabase, imageSetTimeZone, TestConstant.FileExpectation.InfraredMarten, out MetadataReadResult martenMetadataRead);
            Assert.IsTrue(martenMetadataRead.HasFlag(MetadataReadResult.DateTime));

            ImageRow bobcatImage = this.CreateFile(fileDatabase, imageSetTimeZone, TestConstant.FileExpectation.DaylightBobcat, out MetadataReadResult bobcatMetadataRead);
            Assert.IsTrue(bobcatMetadataRead.HasFlag(MetadataReadResult.DateTime));

            using (AddFilesTransaction addFiles = fileDatabase.CreateAddFilesTransaction())
            {
                addFiles.AddFiles(new List<FileLoad>() { new FileLoad(martenImage), new FileLoad(bobcatImage) });
                addFiles.Commit();
            }
            fileDatabase.SelectFiles(FileSelection.All);

            FileTableEnumerator fileEnumerator = new FileTableEnumerator(fileDatabase);
            Assert.IsTrue(fileEnumerator.TryMoveToFile(0));
            Assert.IsTrue(fileEnumerator.MoveNext());

            FileTuplesWithID bobcatUpdate = new FileTuplesWithID(new List<ColumnTuple>()
                {
                    new ColumnTuple(TestConstant.DefaultDatabaseColumn.Choice0, "choice b"),
                    new ColumnTuple(TestConstant.DefaultDatabaseColumn.Counter0, 1.ToString()),
                    new ColumnTuple(TestConstant.DefaultDatabaseColumn.FlagNotVisible, true),
                    new ColumnTuple(TestConstant.DefaultDatabaseColumn.Note3, "bobcat"),
                    new ColumnTuple(TestConstant.DefaultDatabaseColumn.NoteNotVisible, "adult")
                }, fileEnumerator.Current.ID);
            fileDatabase.UpdateFiles(bobcatUpdate);

            ImageRow martenFile = fileDatabase.Files[0];
            martenFile[TestConstant.DefaultDatabaseColumn.Choice0] = "choice b";
            martenFile[TestConstant.DefaultDatabaseColumn.Counter0] = 1.ToString();
            martenFile[TestConstant.DefaultDatabaseColumn.FlagNotVisible] = Boolean.TrueString;
            martenFile[TestConstant.DefaultDatabaseColumn.Note3] = "American marten";
            martenFile[TestConstant.DefaultDatabaseColumn.NoteNotVisible] = "adult";
            Assert.IsTrue(martenFile.HasChanges);
            fileDatabase.SyncFileToDatabase(martenFile);
            martenFile.AcceptChanges();
            Assert.IsFalse(martenFile.HasChanges);

            // generate expectations
            List<FileExpectations> fileExpectations = new List<FileExpectations>()
            {
                new FileExpectations(TestConstant.FileExpectation.InfraredMarten),
                new FileExpectations(TestConstant.FileExpectation.DaylightBobcat),
            };

            // files in subfolder
            if (excludeSubfolderFiles == false)
            {
                ImageRow martenPairImage = this.CreateFile(fileDatabase, imageSetTimeZone, TestConstant.FileExpectation.DaylightMartenPair, out MetadataReadResult martenPairMetadataRead);
                Assert.IsTrue(martenPairMetadataRead.HasFlag(MetadataReadResult.DateTime));

                ImageRow coyoteImage = this.CreateFile(fileDatabase, imageSetTimeZone, TestConstant.FileExpectation.DaylightCoyote, out MetadataReadResult coyoteMetadataRead);
                Assert.IsTrue(coyoteMetadataRead.HasFlag(MetadataReadResult.DateTime));

                using (AddFilesTransaction addFiles = fileDatabase.CreateAddFilesTransaction())
                {
                    addFiles.AddFiles(new List<FileLoad>() { new FileLoad(martenPairImage), new FileLoad(coyoteImage) });
                    addFiles.Commit();
                }
                fileDatabase.SelectFiles(FileSelection.All);

                coyoteImage = fileEnumerator.Current;
                coyoteImage[TestConstant.DefaultDatabaseColumn.Note3] = "coyote";
                coyoteImage[TestConstant.DefaultDatabaseColumn.NoteNotVisible] = "adult";
                coyoteImage[TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel] = String.Empty;
                coyoteImage[TestConstant.DefaultDatabaseColumn.Note0] = "escaped field, because a comma is present";
                fileDatabase.SyncFileToDatabase(coyoteImage);
                coyoteImage.AcceptChanges();

                FileTuplesWithID martenPairImageUpdate = new FileTuplesWithID(new List<ColumnTuple>()
                    {
                        new ColumnTuple(TestConstant.DefaultDatabaseColumn.Note3, "American marten"),
                        new ColumnTuple(TestConstant.DefaultDatabaseColumn.NoteNotVisible, "adult"),
                        new ColumnTuple(TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel, String.Empty),
                        new ColumnTuple(TestConstant.DefaultDatabaseColumn.Note0, "escaped field due to presence of \",\"")
                    },
                    fileDatabase.Files[3].ID);
                fileDatabase.UpdateFiles(martenPairImageUpdate);

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
