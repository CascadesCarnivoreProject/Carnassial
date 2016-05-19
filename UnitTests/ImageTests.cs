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
            this.PopulateImageDatabase(database);

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

                ImageDifferenceResult combinedDifferenceResult = cache.TryCalculateCombinedDifference(Constants.DifferenceThresholdDefault - 2);
                this.CheckDifferenceResult(combinedDifferenceResult, cache);
            }

            Assert.IsTrue(cache.TryMoveToImage(0));
            for (int step = 0; step < 7; ++step)
            {
                cache.MoveToNextStateInPreviousNextDifferenceCycle();
                Assert.IsTrue((cache.CurrentDifferenceState == ImageDifference.Next) ||
                              (cache.CurrentDifferenceState == ImageDifference.Previous) ||
                              (cache.CurrentDifferenceState == ImageDifference.Unaltered));

                ImageDifferenceResult differenceResult = cache.TryCalculateDifference();
                this.CheckDifferenceResult(differenceResult, cache);
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
            string imageFilePath = Path.Combine(folderPath, "BushnellTrophyHD-119677C-20160224-056.JPG");

            ImageDatabase database = new ImageDatabase(folderPath, Constants.File.DefaultImageDatabaseFileName);
            using (DialogPopulateFieldWithMetadata populateFieldDialog = new DialogPopulateFieldWithMetadata(database, imageFilePath))
            {
                populateFieldDialog.LoadExif();
                Dictionary<string, string> exif = (Dictionary<string, string>)populateFieldDialog.dg.ItemsSource;
                Assert.IsTrue(exif.Count > 0, "Expected at least one EXIF field to be retrieved from {0}", imageFilePath);
            }
        }

        private void CheckDifferenceResult(ImageDifferenceResult result, ImageCache cache)
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
                    // result should be NotCalculable on Unaltered
                    if (cache.CurrentDifferenceState != ImageDifference.Unaltered)
                    {
                        Assert.Fail();
                    }
                    else
                    {
                        Assert.IsNotNull(currentBitmap);
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
