using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media.Imaging;
using Timelapse.Database;
using Timelapse.Images;

namespace Timelapse.UnitTests
{
    [TestClass]
    public class ImageTests : TimelapseTest
    {
        [TestMethod]
        public void Cache()
        {
            ImageDatabase imageDatabase = this.CreateImageDatabase(TestConstant.File.CarnivoreTemplateDatabaseFileName, TestConstant.File.CarnivoreNewImageDatabaseFileName);
            this.PopulateCarnivoreDatabase(imageDatabase);

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

            WriteableBitmap currentBitmap = cache.GetCurrentImage();
            Assert.IsNotNull(currentBitmap);

            bool newImageToDisplay;
            Assert.IsTrue(cache.TryMoveToImage(0, out newImageToDisplay));
            Assert.IsTrue(cache.TryMoveToImage(0, out newImageToDisplay));
            Assert.IsTrue(cache.TryMoveToImage(1, out newImageToDisplay));
            Assert.IsTrue(cache.TryMoveToImage(1, out newImageToDisplay));
            Assert.IsFalse(cache.TryMoveToImage(2, out newImageToDisplay));
            Assert.IsFalse(cache.TryMoveToImage(2, out newImageToDisplay));

            Assert.IsTrue(cache.TryMoveToImage(0));
            Assert.IsTrue(cache.TryMoveToImage(1));
            Assert.IsFalse(cache.TryMoveToImage(2));

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
            Assert.IsNotNull(cache.Current);
            Assert.IsTrue(cache.CurrentDifferenceState == ImageDifference.Unaltered);
            Assert.IsTrue(cache.CurrentRow == 0);
        }

        [TestMethod]
        public void Exif()
        {
            string imageFilePath = Path.Combine(this.WorkingDirectory, TestConstant.File.InfraredMartenImage);

            ImageDatabase imageDatabase = this.CreateImageDatabase(TestConstant.File.DefaultTemplateDatabaseFileName2015, Constants.File.DefaultImageDatabaseFileName);
            using (DialogPopulateFieldWithMetadata populateFieldDialog = new DialogPopulateFieldWithMetadata(imageDatabase, imageFilePath))
            {
                populateFieldDialog.LoadExif();
                Dictionary<string, string> exif = (Dictionary<string, string>)populateFieldDialog.dg.ItemsSource;
                Assert.IsTrue(exif.Count > 0, "Expected at least one EXIF field to be retrieved from {0}", imageFilePath);
            }
        }

        [TestMethod]
        public void ImageQuality()
        {
            List<ImageExpectations> imageExpectations = new List<ImageExpectations>()
            {
                new ImageExpectations(TestConstant.Expectations.DaylightBobcatImage),
                new ImageExpectations(TestConstant.Expectations.InfraredMartenImage)
            };

            foreach (ImageExpectations imageExpectation in imageExpectations)
            {
                // Load the image
                ImageProperties imageProperties = imageExpectation.GetImageProperties(this.WorkingDirectory);
                WriteableBitmap bitmap = imageProperties.LoadWriteableBitmap(this.WorkingDirectory);

                double darkPixelFraction;
                bool isColor;
                ImageQualityFilter imageQuality = bitmap.GetImageQuality(Constants.Images.DarkPixelThresholdDefault, Constants.Images.DarkPixelRatioThresholdDefault, out darkPixelFraction, out isColor);
                Assert.IsTrue(Math.Abs(darkPixelFraction - imageExpectation.DarkPixelFraction) < TestConstant.DarkPixelFractionTolerance, "{0}: Expected dark pixel fraction to be {1}, but was {2}.", imageExpectation.FileName, imageExpectation.DarkPixelFraction, darkPixelFraction);
                Assert.IsTrue(isColor == imageExpectation.IsColor, "{0}: Expected isColor to be {1}, but it was {2}", imageExpectation.FileName, imageExpectation.IsColor,  isColor);
                Assert.IsTrue(imageQuality == imageExpectation.Quality, "{0}: Expected image quality {1}, but it was {2}", imageExpectation.FileName, imageExpectation.Quality, imageQuality);
            }
        }

        private void CheckDifferenceResult(ImageDifferenceResult result, ImageCache cache, ImageDatabase imageDatabase)
        {
            WriteableBitmap currentBitmap = cache.GetCurrentImage();
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
                        WriteableBitmap unalteredBitmap = cache.Current.LoadWriteableBitmap(imageDatabase.FolderPath);
                        ImageProperties previousNextImage = imageDatabase.GetImage(previousNextImageRow);
                        WriteableBitmap previousNextBitmap = previousNextImage.LoadWriteableBitmap(imageDatabase.FolderPath);
                        bool mismatched = WriteableBitmapExtensions.BitmapsMismatched(unalteredBitmap, previousNextBitmap);

                        if (imageDatabase.IsImageRowInRange(otherImageRowForCombined))
                        {
                            ImageProperties otherImageForCombined = imageDatabase.GetImage(otherImageRowForCombined);
                            WriteableBitmap otherBitmapForCombined = otherImageForCombined.LoadWriteableBitmap(imageDatabase.FolderPath);
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
