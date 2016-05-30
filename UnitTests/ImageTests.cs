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
            ImageDatabase database = this.CreateImageDatabase(TestConstants.File.CarnivoreImageDatabaseFileName, TestConstants.File.CarnivoreTemplateDatabaseFileName);
            this.PopulateCarnivoreDatabase(database);

            ImageCache cache = new ImageCache(database);
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
                this.CheckDifferenceResult(combinedDifferenceResult, cache, database);
            }

            Assert.IsTrue(cache.TryMoveToImage(0));
            for (int step = 0; step < 7; ++step)
            {
                cache.MoveToNextStateInPreviousNextDifferenceCycle();
                Assert.IsTrue((cache.CurrentDifferenceState == ImageDifference.Next) ||
                              (cache.CurrentDifferenceState == ImageDifference.Previous) ||
                              (cache.CurrentDifferenceState == ImageDifference.Unaltered));

                ImageDifferenceResult differenceResult = cache.TryCalculateDifference();
                this.CheckDifferenceResult(differenceResult, cache, database);
            }

            cache.Reset();
            Assert.IsNotNull(cache.Current);
            Assert.IsTrue(cache.CurrentDifferenceState == ImageDifference.Unaltered);
            Assert.IsTrue(cache.CurrentRow == 0);
        }

        [TestMethod]
        public void Exif()
        {
            string folderPath = Environment.CurrentDirectory;
            string imageFilePath = Path.Combine(folderPath, TestConstants.File.InfraredMartenImage);

            ImageDatabase database = new ImageDatabase(folderPath, Constants.File.DefaultImageDatabaseFileName);
            using (DialogPopulateFieldWithMetadata populateFieldDialog = new DialogPopulateFieldWithMetadata(database, imageFilePath))
            {
                populateFieldDialog.LoadExif();
                Dictionary<string, string> exif = (Dictionary<string, string>)populateFieldDialog.dg.ItemsSource;
                Assert.IsTrue(exif.Count > 0, "Expected at least one EXIF field to be retrieved from {0}", imageFilePath);
            }
        }

        [TestMethod]
        public void ImageQuality()
        {
            string folderPath = Environment.CurrentDirectory;
            List<ImageExpectations> images = new List<ImageExpectations>()
            {
                new ImageExpectations()
                {
                    FileName = TestConstants.File.DaylightBobcatImage,
                    DarkPixelFraction = 0.24222145485288338,
                    IsColor = true,
                    Quality = ImageQualityFilter.Ok
                },
                new ImageExpectations()
                {
                    FileName = TestConstants.File.InfraredMartenImage,
                    DarkPixelFraction = 0.0743353174106539,
                    IsColor = false,
                    Quality = ImageQualityFilter.Ok
                }
            };

            foreach (ImageExpectations image in images)
            {
                // Load the image
                ImageProperties imageProperties = image.GetImageProperties(folderPath);
                WriteableBitmap bitmap = imageProperties.LoadWriteableBitmap(folderPath);

                double darkPixelFraction;
                bool isColor;
                ImageQualityFilter imageQuality = bitmap.GetImageQuality(Constants.Images.DarkPixelThresholdDefault, Constants.Images.DarkPixelRatioThresholdDefault, out darkPixelFraction, out isColor);
                Assert.IsTrue(Math.Abs(darkPixelFraction - image.DarkPixelFraction) < TestConstants.DarkPixelFractionTolerance, "Expected dark pixel fraction to be {0}, but was {1}.", image.DarkPixelFraction, darkPixelFraction);
                Assert.IsTrue(isColor == image.IsColor, "Expected isColor to be {0}, but it was {1}", image.IsColor,  isColor);
                Assert.IsTrue(imageQuality == image.Quality, "Expected image quality {0}, but it was {1}", image.Quality, imageQuality);
            }
        }

        private void CheckDifferenceResult(ImageDifferenceResult result, ImageCache cache, ImageDatabase database)
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
                            expectNullBitmap = (cache.CurrentRow == 0) || (cache.CurrentRow == database.CurrentlySelectedImageCount - 1);
                            previousNextImageRow = cache.CurrentRow - 1;
                            otherImageRowForCombined = cache.CurrentRow + 1;
                            break;
                        case ImageDifference.Next:
                            expectNullBitmap = cache.CurrentRow == database.CurrentlySelectedImageCount - 1;
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
                    if (database.IsImageRowInRange(previousNextImageRow))
                    {
                        WriteableBitmap unalteredBitmap = cache.Current.LoadWriteableBitmap(database.FolderPath);
                        ImageProperties previousNextImage = database.GetImage(previousNextImageRow);
                        WriteableBitmap previousNextBitmap = previousNextImage.LoadWriteableBitmap(database.FolderPath);
                        bool mismatched = WriteableBitmapExtensions.BitmapsMismatched(unalteredBitmap, previousNextBitmap);

                        if (database.IsImageRowInRange(otherImageRowForCombined))
                        {
                            ImageProperties otherImageForCombined = database.GetImage(otherImageRowForCombined);
                            WriteableBitmap otherBitmapForCombined = otherImageForCombined.LoadWriteableBitmap(database.FolderPath);
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
