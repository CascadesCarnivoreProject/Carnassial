using System.Collections.Generic;
using System.Windows.Media.Imaging;
using Timelapse.Database;
using Timelapse.Util;

namespace Timelapse.Images
{
    public class ImageCache : ImageTableEnumerator
    {
        private Dictionary<ImageDifference, WriteableBitmap> differenceBitmapCache;
        private MostRecentlyUsedList<long> mostRecentlyUsedIDs;
        private Dictionary<long, WriteableBitmap> unalteredBitmapsByID;

        public ImageDifference CurrentDifferenceState { get; private set; }

        public ImageCache(ImageDatabase imageDatabase) :
            base(imageDatabase)
        {
            this.CurrentDifferenceState = ImageDifference.Unaltered;
            this.differenceBitmapCache = new Dictionary<ImageDifference, WriteableBitmap>();
            this.mostRecentlyUsedIDs = new MostRecentlyUsedList<long>(Constants.Images.BitmapCacheSize);
            this.unalteredBitmapsByID = new Dictionary<long, WriteableBitmap>();
        }

        public WriteableBitmap GetCurrentImage()
        {
            return this.differenceBitmapCache[this.CurrentDifferenceState];
        }

        public void MoveToNextStateInCombinedDifferenceCycle()
        {
            // if this method and MoveToNextStateInPreviousNextDifferenceCycle() returned bool they'd be consistent MoveNext() and MovePrevious()
            // however, there's no way for them to fail and there's not value in always returning true
            if (this.CurrentDifferenceState == ImageDifference.Next ||
                this.CurrentDifferenceState == ImageDifference.Previous ||
                this.CurrentDifferenceState == ImageDifference.Combined)
            {
                this.CurrentDifferenceState = ImageDifference.Unaltered;
            }
            else
            {
                this.CurrentDifferenceState = ImageDifference.Combined;
            }
        }

        public void MoveToNextStateInPreviousNextDifferenceCycle()
        {
            // If we are looking at the combined differenced image, then always go to the unaltered image.
            if (this.CurrentDifferenceState == ImageDifference.Combined)
            {
                this.CurrentDifferenceState = ImageDifference.Unaltered;
                return;
            }

            // If the current image is marked as corrupted, we will only show the original (replacement) image
            if (!this.Current.IsDisplayable())
            {
                this.CurrentDifferenceState = ImageDifference.Unaltered;
                return;
            }
            else
            {
                // We are going around in a cycle, so go back to the beginning if we are at the end of it.
                this.CurrentDifferenceState = (this.CurrentDifferenceState >= ImageDifference.Next) ? ImageDifference.Previous : ++this.CurrentDifferenceState;
            }

            // Because we can always display the unaltered image, we don't have to do any more tests if that is the current one in the cyle
            if (this.CurrentDifferenceState == ImageDifference.Unaltered)
            {
                return;
            }

            // We can't actually show the previous or next image differencing if we are on the first or last image in the set respectively
            // Nor can we do it if the next image in the sequence is a corrupted one.
            // If that is the case, skip to the next one in the sequence
            if (this.CurrentDifferenceState == ImageDifference.Previous && this.CurrentRow == 0)
            {
                // Already at the beginning
                this.MoveToNextStateInPreviousNextDifferenceCycle();
            }
            else if (this.CurrentDifferenceState == ImageDifference.Next && this.CurrentRow == this.Database.CurrentlySelectedImageCount - 1)
            {
                // Already at the end
                this.MoveToNextStateInPreviousNextDifferenceCycle();
            }
            else if (this.CurrentDifferenceState == ImageDifference.Next && !this.Database.IsImageDisplayable(this.CurrentRow + 1))
            {
                // Can't use the next image as its corrupted
                this.MoveToNextStateInPreviousNextDifferenceCycle();
            }
            else if (this.CurrentDifferenceState == ImageDifference.Previous && !this.Database.IsImageDisplayable(this.CurrentRow - 1))
            {
                // Can't use the previous image as its corrupted
                this.MoveToNextStateInPreviousNextDifferenceCycle();
            }
        }

        // reset enumerator state but don't clear caches
        public override void Reset()
        {
            base.Reset();
            this.ResetDifferenceState(null);
        }

        public ImageDifferenceResult TryCalculateDifference()
        {
            if (this.Current == null || this.Current.IsDisplayable() == false)
            {
                this.CurrentDifferenceState = ImageDifference.Unaltered;
                return ImageDifferenceResult.CurrentImageNotAvailable;
            }

            // determine which image to use for differencing
            WriteableBitmap comparisonBitmap = null;
            if (this.CurrentDifferenceState == ImageDifference.Previous)
            {
                if (this.TryGetPreviousBitmap(out comparisonBitmap) == false)
                {
                    return ImageDifferenceResult.PreviousImageNotAvailable;
                }
            }
            else if (this.CurrentDifferenceState == ImageDifference.Next)
            {
                if (this.TryGetNextBitmap(out comparisonBitmap) == false)
                {
                    return ImageDifferenceResult.NextImageNotAvailable;
                }
            }
            else
            {
                return ImageDifferenceResult.NotCalculable;
            }

            WriteableBitmap unalteredBitmap = this.differenceBitmapCache[ImageDifference.Unaltered];
            WriteableBitmap differenceBitmap = unalteredBitmap.Subtract(comparisonBitmap);
            this.differenceBitmapCache[this.CurrentDifferenceState] = differenceBitmap;
            return differenceBitmap != null ? ImageDifferenceResult.Success : ImageDifferenceResult.NotCalculable;
        }

        public ImageDifferenceResult TryCalculateCombinedDifference(byte differenceThreshold)
        {
            if (this.CurrentDifferenceState != ImageDifference.Combined)
            {
                return ImageDifferenceResult.NotCalculable;
            }

            // We need three valid images: the current one, the previous one, and the next one.
            if (this.Current == null || this.Current.IsDisplayable() == false)
            {
                this.CurrentDifferenceState = ImageDifference.Unaltered;
                return ImageDifferenceResult.CurrentImageNotAvailable;
            }

            WriteableBitmap previousBitmap;
            if (this.TryGetPreviousBitmap(out previousBitmap) == false)
            {
                return ImageDifferenceResult.PreviousImageNotAvailable;
            }

            WriteableBitmap nextBitmap;
            if (this.TryGetNextBitmap(out nextBitmap) == false)
            {
                return ImageDifferenceResult.NextImageNotAvailable;
            }

            // all three images are available, so calculate and cache difference
            WriteableBitmap unalteredBitmap = this.differenceBitmapCache[ImageDifference.Unaltered];
            WriteableBitmap differenceBitmap = unalteredBitmap.CombinedDifference(previousBitmap, nextBitmap, differenceThreshold);
            this.differenceBitmapCache[ImageDifference.Combined] = differenceBitmap;
            return differenceBitmap != null ? ImageDifferenceResult.Success : ImageDifferenceResult.NotCalculable;
        }

        public bool TryInvalidate(long id)
        {
            if (this.unalteredBitmapsByID.ContainsKey(id) == false)
            {
                return false;
            }

            if (this.Current.ID == id)
            {
                this.Reset();
            }

            this.unalteredBitmapsByID.Remove(id);
            return this.mostRecentlyUsedIDs.TryRemove(id);
        }

        public override bool TryMoveToImage(int imageRowIndex)
        {
            bool ignored;
            return this.TryMoveToImage(imageRowIndex, out ignored);
        }

        public bool TryMoveToImage(int imageRowIndex, out bool newImageToDisplay)
        {
            long oldImageID = -1;
            if (this.Current != null)
            {
                oldImageID = this.Current.ID;
            }

            if (base.TryMoveToImage(imageRowIndex) == false)
            {
                newImageToDisplay = false;
                return false;
            }

            newImageToDisplay = this.Current.ID != oldImageID;
            if (newImageToDisplay)
            {
                // get the image data from cache or disk
                WriteableBitmap unalteredImage;
                this.TryGetBitmap(this.Current, out unalteredImage);

                // all moves are to display of unaltered images and invalidate any cached differences
                // it is assumed images on disk are not altered while Timelapse is running and hence unaltered bitmaps can safely be cached by their IDs
                this.ResetDifferenceState(unalteredImage);
            }

            return true;
        }

        private void ResetDifferenceState(WriteableBitmap unalteredImage)
        {
            this.CurrentDifferenceState = ImageDifference.Unaltered;
            this.differenceBitmapCache[ImageDifference.Unaltered] = unalteredImage;
            this.differenceBitmapCache[ImageDifference.Previous] = null;
            this.differenceBitmapCache[ImageDifference.Next] = null;
            this.differenceBitmapCache[ImageDifference.Combined] = null;
        }

        private bool TryGetBitmap(ImageProperties image, out WriteableBitmap bitmap)
        {
            if (this.unalteredBitmapsByID.TryGetValue(image.ID, out bitmap) == false)
            {
                // load the requested bitmap from disk as it isn't cached
                bitmap = image.LoadWriteableBitmap(this.Database.FolderPath);

                // if the bitmap cache is full make room for the newly loaded bitmap
                if (this.mostRecentlyUsedIDs.IsFull())
                {
                    long imageIDToRemove;
                    if (this.mostRecentlyUsedIDs.TryGetLeastRecent(out imageIDToRemove))
                    {
                        this.unalteredBitmapsByID.Remove(imageIDToRemove);
                    }
                }

                // cache the bitmap
                this.mostRecentlyUsedIDs.SetMostRecent(image.ID);
                this.unalteredBitmapsByID.Add(image.ID, bitmap);
            }
            return true;
        }

        private bool TryGetBitmap(int imageRow, out WriteableBitmap bitmap)
        {
            // get properties for the image to retrieve
            ImageProperties imageProperties;
            if (imageRow == this.CurrentRow)
            {
                imageProperties = this.Current;
            }
            else
            {
                bitmap = null;
                if (this.Database.IsImageRowInRange(imageRow) == false)
                {
                    return false;
                }

                imageProperties = this.Database.GetImageByRow(imageRow);
                if (imageProperties.IsDisplayable() == false)
                {
                    return false;
                }
            }

            // get the associated bitmap
            return this.TryGetBitmap(imageProperties, out bitmap);
        }

        private bool TryGetNextBitmap(out WriteableBitmap nextBitmap)
        {
            return this.TryGetBitmap(this.CurrentRow + 1, out nextBitmap);
        }

        private bool TryGetPreviousBitmap(out WriteableBitmap previousBitmap)
        {
            return this.TryGetBitmap(this.CurrentRow - 1, out previousBitmap);
        }
    }
}
