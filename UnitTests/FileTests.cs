using Carnassial.Data;
using Carnassial.Database;
using Carnassial.Images;
using Carnassial.Interop;
using Carnassial.Native;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Carnassial.UnitTests
{
    [TestClass]
    public class FileTests : CarnassialTest
    {
        [TestMethod]
        public void Backup()
        {
            DateTime utcStart = DateTime.UtcNow;
            string fileNameToBackup = TestConstant.File.DefaultFileDatabaseFileName;
            Assert.IsTrue(FileBackup.TryCreateBackup(fileNameToBackup));
            Assert.IsTrue(FileBackup.TryCreateBackup(fileNameToBackup));
            Assert.IsTrue(FileBackup.TryCreateBackup(fileNameToBackup));

            Assert.IsTrue(Directory.Exists(Constant.File.BackupFolder));
            List<string> filesInBackupFolder = Directory.EnumerateFiles(Constant.File.BackupFolder).ToList();
            Assert.IsTrue(filesInBackupFolder.Count >= 3);
            DateTime mostRecentBackupUtc = FileBackup.GetMostRecentBackup(fileNameToBackup);
            Assert.IsTrue(mostRecentBackupUtc > utcStart);
            // nontrivial to check file with most recent time is in the backup folder since the file's timestamps are likely
            // a few milliseconds later than the time used to make the file name unique; this coverage can be added if needed

            // move the three backup files created above to the Recycle Bin, along with any other files accumulated from other
            // tests
            List<FileInfo> filesToAgeOut = new List<FileInfo>(filesInBackupFolder.Select(filePath => new FileInfo(filePath)));
            using (Recycler recycler = new Recycler())
            {
                recycler.MoveToRecycleBin(filesToAgeOut);
            }

            filesInBackupFolder = Directory.EnumerateFiles(Constant.File.BackupFolder).ToList();
            Assert.IsTrue(filesInBackupFolder.Count == 0);
        }

        [TestMethod]
        public async Task Cache()
        {
            FileDatabase fileDatabase = this.CreateFileDatabase(TestConstant.File.DefaultTemplateDatabaseFileName, TestConstant.File.DefaultNewFileDatabaseFileName);
            List<FileExpectations> fileExpectations = this.PopulateDefaultDatabase(fileDatabase);

            using (ImageCache cache = new ImageCache(fileDatabase))
            {
                Assert.IsNull(cache.Current);
                Assert.IsTrue(cache.CurrentDifferenceState == ImageDifference.Unaltered);
                Assert.IsTrue(cache.CurrentRow == -1);

                Assert.IsTrue(cache.MoveNext());
                Assert.IsTrue(cache.MoveNext());
                Assert.IsTrue(cache.MovePrevious());
                Assert.IsTrue(cache.CurrentDifferenceState == ImageDifference.Unaltered);
                Assert.IsTrue(cache.CurrentRow == 0);
                this.VerifyCurrentImage(cache);

                MoveToFileResult moveToFile = await cache.TryMoveToFileAsync(0, 0);
                Assert.IsTrue(moveToFile.Succeeded);
                Assert.IsFalse(moveToFile.NewFileToDisplay);
                moveToFile = await cache.TryMoveToFileAsync(0, 1);
                Assert.IsTrue(moveToFile.Succeeded);
                Assert.IsFalse(moveToFile.NewFileToDisplay);
                moveToFile = await cache.TryMoveToFileAsync(1, 0);
                Assert.IsTrue(moveToFile.Succeeded);
                Assert.IsTrue(moveToFile.NewFileToDisplay);
                moveToFile = await cache.TryMoveToFileAsync(1, -1);
                Assert.IsTrue(moveToFile.Succeeded);
                Assert.IsFalse(moveToFile.NewFileToDisplay);
                this.VerifyCurrentImage(cache);

                Assert.IsTrue(cache.TryInvalidate(1));
                moveToFile = await cache.TryMoveToFileAsync(0, 0);
                Assert.IsTrue(moveToFile.Succeeded);
                Assert.IsTrue(moveToFile.NewFileToDisplay);
                moveToFile = await cache.TryMoveToFileAsync(1, 0);
                Assert.IsTrue(moveToFile.Succeeded);
                Assert.IsTrue(moveToFile.NewFileToDisplay);
                this.VerifyCurrentImage(cache);

                Assert.IsTrue(cache.TryInvalidate(2));
                moveToFile = await cache.TryMoveToFileAsync(1, 1);
                Assert.IsTrue(moveToFile.Succeeded);
                Assert.IsTrue(moveToFile.NewFileToDisplay);
                moveToFile = await cache.TryMoveToFileAsync(1, 0);
                Assert.IsTrue(moveToFile.Succeeded);
                Assert.IsFalse(moveToFile.NewFileToDisplay);
                moveToFile = await cache.TryMoveToFileAsync(0, -1);
                Assert.IsTrue(moveToFile.Succeeded);
                Assert.IsTrue(moveToFile.NewFileToDisplay);
                this.VerifyCurrentImage(cache);

                moveToFile = await cache.TryMoveToFileAsync(fileExpectations.Count, 0);
                Assert.IsFalse(moveToFile.Succeeded);
                moveToFile = await cache.TryMoveToFileAsync(fileExpectations.Count, 0);
                Assert.IsFalse(moveToFile.Succeeded);

                moveToFile = await cache.TryMoveToFileAsync(0, 0);
                Assert.IsTrue(moveToFile.Succeeded);
                moveToFile = await cache.TryMoveToFileAsync(1, 0);
                Assert.IsTrue(moveToFile.Succeeded);
                moveToFile = await cache.TryMoveToFileAsync(fileExpectations.Count, 0);
                Assert.IsFalse(moveToFile.Succeeded);

                for (int step = 0; step < 4; ++step)
                {
                    cache.MoveToNextStateInCombinedDifferenceCycle();
                    Assert.IsTrue((cache.CurrentDifferenceState == ImageDifference.Combined) ||
                                  (cache.CurrentDifferenceState == ImageDifference.Unaltered));

                    ImageDifferenceResult combinedDifferenceResult = await cache.TryCalculateCombinedDifferenceAsync(Constant.Images.DifferenceThresholdDefault - 2);
                    await this.CheckDifferenceResult(combinedDifferenceResult, cache, fileDatabase);

                    MemoryImage differenceImage = cache.GetCurrentImage();
                    if (combinedDifferenceResult == ImageDifferenceResult.Success)
                    {
                        Assert.IsNotNull(differenceImage);
                    }
                }

                Assert.IsTrue(cache.TryMoveToFile(0));
                for (int step = 0; step < 7; ++step)
                {
                    cache.MoveToNextStateInPreviousNextDifferenceCycle();
                    Assert.IsTrue((cache.CurrentDifferenceState == ImageDifference.Next) ||
                                  (cache.CurrentDifferenceState == ImageDifference.Previous) ||
                                  (cache.CurrentDifferenceState == ImageDifference.Unaltered));

                    ImageDifferenceResult differenceResult = await cache.TryCalculateDifferenceAsync(Constant.Images.DifferenceThresholdDefault + 2);
                    await this.CheckDifferenceResult(differenceResult, cache, fileDatabase);

                    MemoryImage differenceImage = cache.GetCurrentImage();
                    if (differenceResult == ImageDifferenceResult.Success)
                    {
                        Assert.IsNotNull(differenceImage);
                    }
                }

                cache.Reset();
                Assert.IsNull(cache.Current);
                Assert.IsTrue(cache.CurrentDifferenceState == ImageDifference.Unaltered);
                Assert.IsTrue(cache.CurrentRow == Constant.Database.InvalidRow);
            }
        }

        [TestMethod]
        public async Task CorruptFileAsync()
        {
            using (FileDatabase fileDatabase = this.CreateFileDatabase(TestConstant.File.DefaultTemplateDatabaseFileName, TestConstant.File.DefaultFileDatabaseFileName))
            {
                TimeZoneInfo imageSetTimeZone = fileDatabase.ImageSet.GetTimeZone();
                ImageRow corruptFile = this.CreateFile(fileDatabase, imageSetTimeZone, TestConstant.FileExpectation.CorruptFieldScan, out DateTimeAdjustment corruptDateTimeAdjustment);
                using (MemoryImage corruptImage = await corruptFile.LoadAsync(fileDatabase.FolderPath))
                {
                    Assert.IsTrue(corruptImage.DecodeError);
                }
            }
        }

        [TestMethod]
        public void ExifBushnell()
        {
            FileDatabase fileDatabase = this.CreateFileDatabase(TestConstant.File.DefaultTemplateDatabaseFileName, Constant.File.DefaultFileDatabaseFileName);
            Dictionary<string, string> metadata = this.LoadMetadata(fileDatabase, TestConstant.FileExpectation.InfraredMarten);

            Assert.IsTrue(DateTime.TryParseExact(metadata[TestConstant.Exif.DateTime], TestConstant.Exif.DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dateTime));
            Assert.IsTrue(DateTime.TryParseExact(metadata[TestConstant.Exif.DateTimeDigitized], TestConstant.Exif.DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dateTimeDigitized));
            Assert.IsTrue(DateTime.TryParseExact(metadata[TestConstant.Exif.DateTimeOriginal], TestConstant.Exif.DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dateTimeOriginal));
            Assert.IsFalse(String.IsNullOrWhiteSpace(metadata[TestConstant.Exif.Software]));
        }

        [TestMethod]
        public void ExifReconyxHyperfire()
        {
            FileDatabase fileDatabase = this.CreateFileDatabase(TestConstant.File.DefaultTemplateDatabaseFileName, Constant.File.DefaultFileDatabaseFileName);
            Dictionary<string, string> metadata = this.LoadMetadata(fileDatabase, TestConstant.FileExpectation.DaylightMartenPair);

            Assert.IsFalse(String.IsNullOrWhiteSpace(metadata[TestConstant.Exif.ExposureTime]));

            Assert.IsFalse(String.IsNullOrWhiteSpace(metadata[TestConstant.Exif.ReconyxHyperfire.AmbientTemperature]));
            Assert.IsFalse(String.IsNullOrWhiteSpace(metadata[TestConstant.Exif.ReconyxHyperfire.AmbientTemperatureFarenheit]));
            Assert.IsFalse(String.IsNullOrWhiteSpace(metadata[TestConstant.Exif.ReconyxHyperfire.BatteryVoltage]));
            Assert.IsFalse(String.IsNullOrWhiteSpace(metadata[TestConstant.Exif.ReconyxHyperfire.Brightness]));
            Assert.IsFalse(String.IsNullOrWhiteSpace(metadata[TestConstant.Exif.ReconyxHyperfire.Contrast]));
            Assert.IsTrue(DateTime.TryParseExact(metadata[TestConstant.Exif.ReconyxHyperfire.DateTimeOriginal], TestConstant.Exif.DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dateTimeOriginal));
            Assert.IsFalse(String.IsNullOrWhiteSpace(metadata[TestConstant.Exif.ReconyxHyperfire.EventNumber]));
            Assert.IsFalse(String.IsNullOrWhiteSpace(metadata[TestConstant.Exif.ReconyxHyperfire.FirmwareVersion]));
            Assert.IsFalse(String.IsNullOrWhiteSpace(metadata[TestConstant.Exif.ReconyxHyperfire.InfraredIlluminator]));
            Assert.IsFalse(String.IsNullOrWhiteSpace(metadata[TestConstant.Exif.ReconyxHyperfire.MakernoteVersion]));
            Assert.IsFalse(String.IsNullOrWhiteSpace(metadata[TestConstant.Exif.ReconyxHyperfire.MoonPhase]));
            Assert.IsFalse(String.IsNullOrWhiteSpace(metadata[TestConstant.Exif.ReconyxHyperfire.Saturation]));
            Assert.IsFalse(String.IsNullOrWhiteSpace(metadata[TestConstant.Exif.ReconyxHyperfire.Sequence]));
            Assert.IsFalse(String.IsNullOrWhiteSpace(metadata[TestConstant.Exif.ReconyxHyperfire.SerialNumber]));
            Assert.IsFalse(String.IsNullOrWhiteSpace(metadata[TestConstant.Exif.ReconyxHyperfire.Sharpness]));
            Assert.IsFalse(String.IsNullOrWhiteSpace(metadata[TestConstant.Exif.ReconyxHyperfire.TriggerMode]));
            Assert.IsFalse(String.IsNullOrWhiteSpace(metadata[TestConstant.Exif.ReconyxHyperfire.UserLabel]));
        }

        [TestMethod]
        public async Task ImageQuality()
        {
            List<FileExpectations> fileExpectations = new List<FileExpectations>()
            {
                new FileExpectations(TestConstant.FileExpectation.DaylightBobcat),
                new FileExpectations(TestConstant.FileExpectation.DaylightCoyote),
                new FileExpectations(TestConstant.FileExpectation.DaylightMartenPair),
                new FileExpectations(TestConstant.FileExpectation.InfraredMarten)
            };

            using (TemplateDatabase templateDatabase = this.CreateTemplateDatabase(TestConstant.File.DefaultNewTemplateDatabaseFileName))
            {
                using (FileDatabase fileDatabase = this.CreateFileDatabase(templateDatabase, TestConstant.File.DefaultNewFileDatabaseFileName))
                {
                    bool darkFractionError = false;
                    foreach (FileExpectations fileExpectation in fileExpectations)
                    {
                        // load the image
                        ImageRow file = fileExpectation.GetFileData(fileDatabase);
                        using (MemoryImage image = await file.LoadAsync(this.WorkingDirectory))
                        {
                            FileSelection imageQuality = image.IsDark(Constant.Images.DarkPixelThresholdDefault, Constant.Images.DarkPixelRatioThresholdDefault, out double darkPixelFraction, out bool isColor) ? FileSelection.Dark : FileSelection.Ok;
                            if (Math.Abs(darkPixelFraction - fileExpectation.DarkPixelFraction) > TestConstant.DarkPixelFractionTolerance)
                            {
                                this.TestContext.WriteLine("{0}: Expected dark pixel fraction to be {1}, but was {2}.", fileExpectation.FileName, fileExpectation.DarkPixelFraction, darkPixelFraction);
                            }
                            Assert.IsTrue(isColor == fileExpectation.IsColor, "{0}: Expected isColor to be {1}, but it was {2}", fileExpectation.FileName, fileExpectation.IsColor, isColor);
                            Assert.IsTrue(imageQuality == fileExpectation.Quality, "{0}: Expected image quality {1}, but it was {2}", fileExpectation.FileName, fileExpectation.Quality, imageQuality);
                        }
                    }

                    if (darkFractionError)
                    {
                        Assert.Fail("At least one dark pixel fraction had error greater than {0}.  See test log for details.", TestConstant.DarkPixelFractionTolerance);
                    }
                }
            }
        }

        private async Task CheckDifferenceResult(ImageDifferenceResult result, ImageCache cache, FileDatabase fileDatabase)
        {
            MemoryImage currentImage = cache.GetCurrentImage();
            switch (result)
            {
                case ImageDifferenceResult.CurrentImageNotAvailable:
                case ImageDifferenceResult.NextImageNotAvailable:
                case ImageDifferenceResult.PreviousImageNotAvailable:
                    if (cache.CurrentDifferenceState == ImageDifference.Unaltered)
                    {
                        Assert.IsNotNull(currentImage);
                    }
                    else
                    {
                        Assert.IsNull(currentImage);
                    }
                    break;
                case ImageDifferenceResult.NotCalculable:
                    bool expectNullImage = false;
                    int previousNextImageRow = -1;
                    int otherImageRowForCombined = -1;
                    switch (cache.CurrentDifferenceState)
                    {
                        // as a default assume images are matched and expect differences to be calculable if the necessary images are available
                        case ImageDifference.Combined:
                            expectNullImage = (cache.CurrentRow == 0) || (cache.CurrentRow == fileDatabase.CurrentlySelectedFileCount - 1);
                            previousNextImageRow = cache.CurrentRow - 1;
                            otherImageRowForCombined = cache.CurrentRow + 1;
                            break;
                        case ImageDifference.Next:
                            expectNullImage = cache.CurrentRow == fileDatabase.CurrentlySelectedFileCount - 1;
                            previousNextImageRow = cache.CurrentRow + 1;
                            break;
                        case ImageDifference.Previous:
                            expectNullImage = cache.CurrentRow == 0;
                            previousNextImageRow = cache.CurrentRow - 1;
                            break;
                        case ImageDifference.Unaltered:
                            // result should be NotCalculable on Unaltered
                            expectNullImage = true;
                            return;
                    }

                    // check if the image to diff against is matched
                    if (fileDatabase.IsFileRowInRange(previousNextImageRow))
                    {
                        MemoryImage unalteredImage = await cache.Current.LoadAsync(fileDatabase.FolderPath);
                        ImageRow previousNextFile = fileDatabase.Files[previousNextImageRow];
                        MemoryImage previousNextImage = await previousNextFile.LoadAsync(fileDatabase.FolderPath);
                        bool mismatched = unalteredImage.MismatchedOrNot32BitBgra(previousNextImage);

                        if (fileDatabase.IsFileRowInRange(otherImageRowForCombined))
                        {
                            ImageRow otherFileForCombined = fileDatabase.Files[otherImageRowForCombined];
                            MemoryImage otherImageForCombined = await otherFileForCombined.LoadAsync(fileDatabase.FolderPath);
                            mismatched |= unalteredImage.MismatchedOrNot32BitBgra(otherImageForCombined);
                        }

                        expectNullImage |= mismatched;
                    }

                    if (expectNullImage)
                    {
                        Assert.IsNull(currentImage, "Expected a null image for difference result {0} and state {1}.", result, cache.CurrentDifferenceState);
                    }
                    else
                    {
                        Assert.IsNotNull(currentImage, "Expected an image for difference result {0} and state {1}.", result, cache.CurrentDifferenceState);
                    }
                    break;
                case ImageDifferenceResult.Success:
                    Assert.IsNotNull(currentImage);
                    break;
                default:
                    throw new NotSupportedException(String.Format("Unhandled result {0}.", result));
            }
        }

        private void VerifyCurrentImage(ImageCache cache)
        {
            Assert.IsNotNull(cache.Current);

            // don't dispose current image as it's owned by the cache
            MemoryImage currentImage = cache.GetCurrentImage();
            Assert.IsNotNull(currentImage);
            Assert.IsTrue(currentImage.DecodeError == false);
            Assert.IsTrue(currentImage.Format == MemoryImage.PreferredPixelFormat);
            Assert.IsTrue((1000 < currentImage.PixelHeight) && (currentImage.PixelHeight < 10000));
            Assert.IsTrue((1000 < currentImage.PixelWidth) && (currentImage.PixelWidth < 10000));
            Assert.IsTrue(currentImage.TotalPixels > 1000 * 1000);
        }
    }
}
