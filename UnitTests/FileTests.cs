using Carnassial.Database;
using Carnassial.Images;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Media.Imaging;

namespace Carnassial.UnitTests
{
    [TestClass]
    public class FileTests : CarnassialTest
    {
        [TestMethod]
        public void Cache()
        {
            FileDatabase fileDatabase = this.CreateFileDatabase(TestConstant.File.DefaultTemplateDatabaseFileName, TestConstant.File.DefaultNewFileDatabaseFileName);
            List<FileExpectations> fileExpectations = this.PopulateDefaultDatabase(fileDatabase);

            ImageCache cache = new ImageCache(fileDatabase);
            Assert.IsNull(cache.Current);
            Assert.IsTrue(cache.CurrentDifferenceState == ImageDifference.Unaltered);
            Assert.IsTrue(cache.CurrentRow == -1);

            Assert.IsTrue(cache.MoveNext());
            Assert.IsTrue(cache.MoveNext());
            Assert.IsTrue(cache.MovePrevious());
            Assert.IsNotNull(cache.Current);
            Assert.IsTrue(cache.CurrentDifferenceState == ImageDifference.Unaltered);
            Assert.IsTrue(cache.CurrentRow == 0);

            BitmapSource currentBitmap = cache.GetCurrentImage();
            Assert.IsNotNull(currentBitmap);

            bool newImageToDisplay;
            Assert.IsTrue(cache.TryMoveToFile(0, out newImageToDisplay));
            Assert.IsFalse(newImageToDisplay);
            Assert.IsTrue(cache.TryMoveToFile(0, out newImageToDisplay));
            Assert.IsFalse(newImageToDisplay);
            Assert.IsTrue(cache.TryMoveToFile(1, out newImageToDisplay));
            Assert.IsTrue(newImageToDisplay);
            Assert.IsTrue(cache.TryMoveToFile(1, out newImageToDisplay));
            Assert.IsFalse(newImageToDisplay);

            Assert.IsTrue(cache.TryInvalidate(1));
            Assert.IsTrue(cache.TryMoveToFile(0, out newImageToDisplay));
            Assert.IsTrue(newImageToDisplay);
            Assert.IsTrue(cache.TryMoveToFile(1, out newImageToDisplay));
            Assert.IsTrue(newImageToDisplay);

            Assert.IsTrue(cache.TryInvalidate(2));
            Assert.IsTrue(cache.TryMoveToFile(1, out newImageToDisplay));
            Assert.IsTrue(newImageToDisplay);
            Assert.IsTrue(cache.TryMoveToFile(1, out newImageToDisplay));
            Assert.IsFalse(newImageToDisplay);
            Assert.IsTrue(cache.TryMoveToFile(0, out newImageToDisplay));
            Assert.IsTrue(newImageToDisplay);

            Assert.IsFalse(cache.TryMoveToFile(fileExpectations.Count, out newImageToDisplay));
            Assert.IsFalse(cache.TryMoveToFile(fileExpectations.Count, out newImageToDisplay));

            Assert.IsTrue(cache.TryMoveToFile(0));
            Assert.IsTrue(cache.TryMoveToFile(1));
            Assert.IsFalse(cache.TryMoveToFile(fileExpectations.Count));

            for (int step = 0; step < 4; ++step)
            {
                cache.MoveToNextStateInCombinedDifferenceCycle();
                Assert.IsTrue((cache.CurrentDifferenceState == ImageDifference.Combined) ||
                              (cache.CurrentDifferenceState == ImageDifference.Unaltered));

                ImageDifferenceResult combinedDifferenceResult = cache.TryCalculateCombinedDifference(Constant.Images.DifferenceThresholdDefault - 2);
                this.CheckDifferenceResult(combinedDifferenceResult, cache, fileDatabase);
            }

            Assert.IsTrue(cache.TryMoveToFile(0));
            for (int step = 0; step < 7; ++step)
            {
                cache.MoveToNextStateInPreviousNextDifferenceCycle();
                Assert.IsTrue((cache.CurrentDifferenceState == ImageDifference.Next) ||
                              (cache.CurrentDifferenceState == ImageDifference.Previous) ||
                              (cache.CurrentDifferenceState == ImageDifference.Unaltered));

                ImageDifferenceResult differenceResult = cache.TryCalculateDifference();
                this.CheckDifferenceResult(differenceResult, cache, fileDatabase);
            }

            cache.Reset();
            Assert.IsNull(cache.Current);
            Assert.IsTrue(cache.CurrentDifferenceState == ImageDifference.Unaltered);
            Assert.IsTrue(cache.CurrentRow == Constant.Database.InvalidRow);
        }

        [TestMethod]
        public void ExifBushnell()
        {
            FileDatabase fileDatabase = this.CreateFileDatabase(TestConstant.File.DefaultTemplateDatabaseFileName, Constant.File.DefaultFileDatabaseFileName);
            Dictionary<string, string> metadata = this.LoadMetadata(fileDatabase, TestConstant.FileExpectation.InfraredMarten);

            DateTime dateTime;
            Assert.IsTrue(DateTime.TryParseExact(metadata[TestConstant.Exif.DateTime], TestConstant.Exif.DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTime));
            DateTime dateTimeDigitized;
            Assert.IsTrue(DateTime.TryParseExact(metadata[TestConstant.Exif.DateTimeDigitized], TestConstant.Exif.DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTimeDigitized));
            DateTime dateTimeOriginal;
            Assert.IsTrue(DateTime.TryParseExact(metadata[TestConstant.Exif.DateTimeOriginal], TestConstant.Exif.DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTimeOriginal));
            Assert.IsFalse(String.IsNullOrWhiteSpace(metadata[TestConstant.Exif.Software]));
        }

        [TestMethod]
        public void ExifReconyx()
        {
            FileDatabase fileDatabase = this.CreateFileDatabase(TestConstant.File.DefaultTemplateDatabaseFileName, Constant.File.DefaultFileDatabaseFileName);
            Dictionary<string, string> metadata = this.LoadMetadata(fileDatabase, TestConstant.FileExpectation.DaylightMartenPair);

            Assert.IsFalse(String.IsNullOrWhiteSpace(metadata[TestConstant.Exif.ExposureTime]));

            Assert.IsFalse(String.IsNullOrWhiteSpace(metadata[TestConstant.Exif.Reconyx.AmbientTemperature]));
            Assert.IsFalse(String.IsNullOrWhiteSpace(metadata[TestConstant.Exif.Reconyx.AmbientTemperatureFarenheit]));
            Assert.IsFalse(String.IsNullOrWhiteSpace(metadata[TestConstant.Exif.Reconyx.BatteryVoltage]));
            Assert.IsFalse(String.IsNullOrWhiteSpace(metadata[TestConstant.Exif.Reconyx.Brightness]));
            Assert.IsFalse(String.IsNullOrWhiteSpace(metadata[TestConstant.Exif.Reconyx.Contrast]));
            DateTime dateTimeOriginal;
            Assert.IsTrue(DateTime.TryParseExact(metadata[TestConstant.Exif.Reconyx.DateTimeOriginal], TestConstant.Exif.DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTimeOriginal));
            Assert.IsFalse(String.IsNullOrWhiteSpace(metadata[TestConstant.Exif.Reconyx.FirmwareVersion]));
            Assert.IsFalse(String.IsNullOrWhiteSpace(metadata[TestConstant.Exif.Reconyx.InfraredIlluminator]));
            Assert.IsFalse(String.IsNullOrWhiteSpace(metadata[TestConstant.Exif.Reconyx.MoonPhase]));
            Assert.IsFalse(String.IsNullOrWhiteSpace(metadata[TestConstant.Exif.Reconyx.Saturation]));
            Assert.IsFalse(String.IsNullOrWhiteSpace(metadata[TestConstant.Exif.Reconyx.Sequence]));
            Assert.IsFalse(String.IsNullOrWhiteSpace(metadata[TestConstant.Exif.Reconyx.SerialNumber]));
            Assert.IsFalse(String.IsNullOrWhiteSpace(metadata[TestConstant.Exif.Reconyx.Sharpness]));
            Assert.IsFalse(String.IsNullOrWhiteSpace(metadata[TestConstant.Exif.Reconyx.TriggerMode]));
            Assert.IsFalse(String.IsNullOrWhiteSpace(metadata[TestConstant.Exif.Reconyx.UserLabel]));
        }

        [TestMethod]
        public void ImageQuality()
        {
            List<FileExpectations> fileExpectations = new List<FileExpectations>()
            {
                new FileExpectations(TestConstant.FileExpectation.DaylightBobcat),
                new FileExpectations(TestConstant.FileExpectation.DaylightCoyote),
                new FileExpectations(TestConstant.FileExpectation.DaylightMartenPair),
                new FileExpectations(TestConstant.FileExpectation.InfraredMarten)
            };

            TemplateDatabase templateDatabase = this.CreateTemplateDatabase(TestConstant.File.DefaultNewTemplateDatabaseFileName);
            FileDatabase fileDatabase = this.CreateFileDatabase(templateDatabase, TestConstant.File.DefaultNewFileDatabaseFileName);
            foreach (FileExpectations fileExpectation in fileExpectations)
            {
                // Load the image
                ImageRow file = fileExpectation.GetFileData(fileDatabase);
                BitmapSource bitmap = file.LoadBitmap(this.WorkingDirectory);

                double darkPixelFraction;
                bool isColor;
                FileSelection imageQuality = bitmap.AsWriteable().IsDark(Constant.Images.DarkPixelThresholdDefault, Constant.Images.DarkPixelRatioThresholdDefault, out darkPixelFraction, out isColor);
                Assert.IsTrue(Math.Abs(darkPixelFraction - fileExpectation.DarkPixelFraction) < TestConstant.DarkPixelFractionTolerance, "{0}: Expected dark pixel fraction to be {1}, but was {2}.", fileExpectation.FileName, fileExpectation.DarkPixelFraction, darkPixelFraction);
                Assert.IsTrue(isColor == fileExpectation.IsColor, "{0}: Expected isColor to be {1}, but it was {2}", fileExpectation.FileName, fileExpectation.IsColor, isColor);
                Assert.IsTrue(imageQuality == fileExpectation.Quality, "{0}: Expected image quality {1}, but it was {2}", fileExpectation.FileName, fileExpectation.Quality, imageQuality);
            }
        }

        private void CheckDifferenceResult(ImageDifferenceResult result, ImageCache cache, FileDatabase fileDatabase)
        {
            BitmapSource currentBitmap = cache.GetCurrentImage();
            switch (result)
            {
                case ImageDifferenceResult.CurrentImageNotAvailable:
                case ImageDifferenceResult.NextImageNotAvailable:
                case ImageDifferenceResult.PreviousImageNotAvailable:
                    if (cache.CurrentDifferenceState == ImageDifference.Unaltered)
                    {
                        Assert.IsNotNull(currentBitmap);
                    }
                    else
                    {
                        Assert.IsNull(currentBitmap);
                    }
                    break;
                case ImageDifferenceResult.NotCalculable:
                    bool expectNullBitmap = false;
                    int previousNextImageRow = -1;
                    int otherImageRowForCombined = -1;
                    switch (cache.CurrentDifferenceState)
                    {
                        // as a default assume images are matched and expect differences to be calculable if the necessary images are available
                        case ImageDifference.Combined:
                            expectNullBitmap = (cache.CurrentRow == 0) || (cache.CurrentRow == fileDatabase.CurrentlySelectedFileCount - 1);
                            previousNextImageRow = cache.CurrentRow - 1;
                            otherImageRowForCombined = cache.CurrentRow + 1;
                            break;
                        case ImageDifference.Next:
                            expectNullBitmap = cache.CurrentRow == fileDatabase.CurrentlySelectedFileCount - 1;
                            previousNextImageRow = cache.CurrentRow + 1;
                            break;
                        case ImageDifference.Previous:
                            expectNullBitmap = cache.CurrentRow == 0;
                            previousNextImageRow = cache.CurrentRow - 1;
                            break;
                        case ImageDifference.Unaltered:
                            // result should be NotCalculable on Unaltered
                            expectNullBitmap = true;
                            return;
                    }

                    // check if the image to diff against is matched
                    if (fileDatabase.IsFileRowInRange(previousNextImageRow))
                    {
                        WriteableBitmap unalteredBitmap = cache.Current.LoadBitmap(fileDatabase.FolderPath).AsWriteable();
                        ImageRow previousNextImage = fileDatabase.Files[previousNextImageRow];
                        WriteableBitmap previousNextBitmap = previousNextImage.LoadBitmap(fileDatabase.FolderPath).AsWriteable();
                        bool mismatched = WriteableBitmapExtensions.BitmapsMismatched(unalteredBitmap, previousNextBitmap);

                        if (fileDatabase.IsFileRowInRange(otherImageRowForCombined))
                        {
                            ImageRow otherImageForCombined = fileDatabase.Files[otherImageRowForCombined];
                            WriteableBitmap otherBitmapForCombined = otherImageForCombined.LoadBitmap(fileDatabase.FolderPath).AsWriteable();
                            mismatched |= WriteableBitmapExtensions.BitmapsMismatched(unalteredBitmap, otherBitmapForCombined);
                        }

                        expectNullBitmap |= mismatched;
                    }

                    if (expectNullBitmap)
                    {
                        Assert.IsNull(currentBitmap, "Expected a null bitmap for difference result {0} and state {1}.", result, cache.CurrentDifferenceState);
                    }
                    else
                    {
                        Assert.IsNotNull(currentBitmap, "Expected a bitmap for difference result {0} and state {1}.", result, cache.CurrentDifferenceState);
                    }
                    break;
                case ImageDifferenceResult.Success:
                    Assert.IsNotNull(currentBitmap);
                    break;
                default:
                    throw new NotSupportedException(String.Format("Unhandled result {0}.", result));
            }
        }
    }
}
