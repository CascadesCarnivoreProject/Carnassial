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
    public class ImageTests : CarnassialTest
    {
        [TestMethod]
        public void Cache()
        {
            ImageDatabase imageDatabase = this.CreateImageDatabase(TestConstant.File.DefaultTemplateDatabaseFileName, TestConstant.File.DefaultNewImageDatabaseFileName);
            List<ImageExpectations> imageExpectations = this.PopulateDefaultDatabase(imageDatabase);

            ImageCache cache = new ImageCache(imageDatabase);
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
            Assert.IsTrue(cache.TryMoveToImage(0, out newImageToDisplay));
            Assert.IsFalse(newImageToDisplay);
            Assert.IsTrue(cache.TryMoveToImage(0, out newImageToDisplay));
            Assert.IsFalse(newImageToDisplay);
            Assert.IsTrue(cache.TryMoveToImage(1, out newImageToDisplay));
            Assert.IsTrue(newImageToDisplay);
            Assert.IsTrue(cache.TryMoveToImage(1, out newImageToDisplay));
            Assert.IsFalse(newImageToDisplay);

            Assert.IsTrue(cache.TryInvalidate(1));
            Assert.IsTrue(cache.TryMoveToImage(0, out newImageToDisplay));
            Assert.IsTrue(newImageToDisplay);
            Assert.IsTrue(cache.TryMoveToImage(1, out newImageToDisplay));
            Assert.IsTrue(newImageToDisplay);

            Assert.IsTrue(cache.TryInvalidate(2));
            Assert.IsTrue(cache.TryMoveToImage(1, out newImageToDisplay));
            Assert.IsTrue(newImageToDisplay);
            Assert.IsTrue(cache.TryMoveToImage(1, out newImageToDisplay));
            Assert.IsFalse(newImageToDisplay);
            Assert.IsTrue(cache.TryMoveToImage(0, out newImageToDisplay));
            Assert.IsTrue(newImageToDisplay);

            Assert.IsFalse(cache.TryMoveToImage(imageExpectations.Count, out newImageToDisplay));
            Assert.IsFalse(cache.TryMoveToImage(imageExpectations.Count, out newImageToDisplay));

            Assert.IsTrue(cache.TryMoveToImage(0));
            Assert.IsTrue(cache.TryMoveToImage(1));
            Assert.IsFalse(cache.TryMoveToImage(imageExpectations.Count));

            for (int step = 0; step < 4; ++step)
            {
                cache.MoveToNextStateInCombinedDifferenceCycle();
                Assert.IsTrue((cache.CurrentDifferenceState == ImageDifference.Combined) ||
                              (cache.CurrentDifferenceState == ImageDifference.Unaltered));

                ImageDifferenceResult combinedDifferenceResult = cache.TryCalculateCombinedDifference(Constants.Images.DifferenceThresholdDefault - 2);
                this.CheckDifferenceResult(combinedDifferenceResult, cache, imageDatabase);
            }

            Assert.IsTrue(cache.TryMoveToImage(0));
            for (int step = 0; step < 7; ++step)
            {
                cache.MoveToNextStateInPreviousNextDifferenceCycle();
                Assert.IsTrue((cache.CurrentDifferenceState == ImageDifference.Next) ||
                              (cache.CurrentDifferenceState == ImageDifference.Previous) ||
                              (cache.CurrentDifferenceState == ImageDifference.Unaltered));

                ImageDifferenceResult differenceResult = cache.TryCalculateDifference();
                this.CheckDifferenceResult(differenceResult, cache, imageDatabase);
            }

            cache.Reset();
            Assert.IsNull(cache.Current);
            Assert.IsTrue(cache.CurrentDifferenceState == ImageDifference.Unaltered);
            Assert.IsTrue(cache.CurrentRow == Constants.Database.InvalidRow);
        }

        [TestMethod]
        public void ExifBushnell()
        {
            ImageDatabase imageDatabase = this.CreateImageDatabase(TestConstant.File.DefaultTemplateDatabaseFileName, Constants.File.DefaultImageDatabaseFileName);
            Dictionary<string, string> metadata = this.LoadMetadata(imageDatabase, TestConstant.ImageExpectation.InfraredMarten);

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
            ImageDatabase imageDatabase = this.CreateImageDatabase(TestConstant.File.DefaultTemplateDatabaseFileName, Constants.File.DefaultImageDatabaseFileName);
            Dictionary<string, string> metadata = this.LoadMetadata(imageDatabase, TestConstant.ImageExpectation.DaylightMartenPair);

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
            List<ImageExpectations> imageExpectations = new List<ImageExpectations>()
            {
                new ImageExpectations(TestConstant.ImageExpectation.DaylightBobcat),
                new ImageExpectations(TestConstant.ImageExpectation.DaylightCoyote),
                new ImageExpectations(TestConstant.ImageExpectation.DaylightMartenPair),
                new ImageExpectations(TestConstant.ImageExpectation.InfraredMarten)
            };

            TemplateDatabase templateDatabase = this.CreateTemplateDatabase(TestConstant.File.DefaultNewTemplateDatabaseFileName);
            ImageDatabase imageDatabase = this.CreateImageDatabase(templateDatabase, TestConstant.File.DefaultNewImageDatabaseFileName);
            foreach (ImageExpectations imageExpectation in imageExpectations)
            {
                // Load the image
                ImageRow imageProperties = imageExpectation.GetImageProperties(imageDatabase);
                BitmapSource bitmap = imageProperties.LoadBitmap(this.WorkingDirectory);

                double darkPixelFraction;
                bool isColor;
                ImageSelection imageQuality = bitmap.AsWriteable().GetImageQuality(Constants.Images.DarkPixelThresholdDefault, Constants.Images.DarkPixelRatioThresholdDefault, out darkPixelFraction, out isColor);
                Assert.IsTrue(Math.Abs(darkPixelFraction - imageExpectation.DarkPixelFraction) < TestConstant.DarkPixelFractionTolerance, "{0}: Expected dark pixel fraction to be {1}, but was {2}.", imageExpectation.FileName, imageExpectation.DarkPixelFraction, darkPixelFraction);
                Assert.IsTrue(isColor == imageExpectation.IsColor, "{0}: Expected isColor to be {1}, but it was {2}", imageExpectation.FileName, imageExpectation.IsColor, isColor);
                Assert.IsTrue(imageQuality == imageExpectation.Quality, "{0}: Expected image quality {1}, but it was {2}", imageExpectation.FileName, imageExpectation.Quality, imageQuality);
            }
        }

        private void CheckDifferenceResult(ImageDifferenceResult result, ImageCache cache, ImageDatabase imageDatabase)
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
                            expectNullBitmap = (cache.CurrentRow == 0) || (cache.CurrentRow == imageDatabase.CurrentlySelectedImageCount - 1);
                            previousNextImageRow = cache.CurrentRow - 1;
                            otherImageRowForCombined = cache.CurrentRow + 1;
                            break;
                        case ImageDifference.Next:
                            expectNullBitmap = cache.CurrentRow == imageDatabase.CurrentlySelectedImageCount - 1;
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
                    if (imageDatabase.IsImageRowInRange(previousNextImageRow))
                    {
                        WriteableBitmap unalteredBitmap = cache.Current.LoadBitmap(imageDatabase.FolderPath).AsWriteable();
                        ImageRow previousNextImage = imageDatabase.ImageDataTable[previousNextImageRow];
                        WriteableBitmap previousNextBitmap = previousNextImage.LoadBitmap(imageDatabase.FolderPath).AsWriteable();
                        bool mismatched = WriteableBitmapExtensions.BitmapsMismatched(unalteredBitmap, previousNextBitmap);

                        if (imageDatabase.IsImageRowInRange(otherImageRowForCombined))
                        {
                            ImageRow otherImageForCombined = imageDatabase.ImageDataTable[otherImageRowForCombined];
                            WriteableBitmap otherBitmapForCombined = otherImageForCombined.LoadBitmap(imageDatabase.FolderPath).AsWriteable();
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
