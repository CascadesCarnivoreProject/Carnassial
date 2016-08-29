﻿using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Timelapse.Database;
using Timelapse.Util;

namespace Timelapse.Images
{
    public class ImageCache : ImageTableEnumerator
    {
        private Dictionary<ImageDifference, BitmapSource> differenceBitmapCache;
        private MostRecentlyUsedList<long> mostRecentlyUsedIDs;
        private ConcurrentDictionary<long, Task> prefetechesByID;
        private ConcurrentDictionary<long, BitmapSource> unalteredBitmapsByID;

        public ImageDifference CurrentDifferenceState { get; private set; }

        public ImageCache(ImageDatabase imageDatabase) :
            base(imageDatabase)
        {
            this.CurrentDifferenceState = ImageDifference.Unaltered;
            this.differenceBitmapCache = new Dictionary<ImageDifference, BitmapSource>();
            this.mostRecentlyUsedIDs = new MostRecentlyUsedList<long>(Constants.Images.BitmapCacheSize);
            this.prefetechesByID = new ConcurrentDictionary<long, Task>();
            this.unalteredBitmapsByID = new ConcurrentDictionary<long, BitmapSource>();
        }

        public BitmapSource GetCurrentImage()
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
                if (this.TryGetPreviousBitmapAsWriteable(out comparisonBitmap) == false)
                {
                    return ImageDifferenceResult.PreviousImageNotAvailable;
                }
            }
            else if (this.CurrentDifferenceState == ImageDifference.Next)
            {
                if (this.TryGetNextBitmapAsWriteable(out comparisonBitmap) == false)
                {
                    return ImageDifferenceResult.NextImageNotAvailable;
                }
            }
            else
            {
                return ImageDifferenceResult.NotCalculable;
            }

            WriteableBitmap unalteredBitmap = this.differenceBitmapCache[ImageDifference.Unaltered].AsWriteable();
            this.differenceBitmapCache[ImageDifference.Unaltered] = unalteredBitmap;

            BitmapSource differenceBitmap = unalteredBitmap.Subtract(comparisonBitmap);
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
            if (this.TryGetPreviousBitmapAsWriteable(out previousBitmap) == false)
            {
                return ImageDifferenceResult.PreviousImageNotAvailable;
            }

            WriteableBitmap nextBitmap;
            if (this.TryGetNextBitmapAsWriteable(out nextBitmap) == false)
            {
                return ImageDifferenceResult.NextImageNotAvailable;
            }

            WriteableBitmap unalteredBitmap = this.differenceBitmapCache[ImageDifference.Unaltered].AsWriteable();
            this.differenceBitmapCache[ImageDifference.Unaltered] = unalteredBitmap;

            // all three images are available, so calculate and cache difference
            BitmapSource differenceBitmap = unalteredBitmap.CombinedDifference(previousBitmap, nextBitmap, differenceThreshold);
            this.differenceBitmapCache[ImageDifference.Combined] = differenceBitmap;
            return differenceBitmap != null ? ImageDifferenceResult.Success : ImageDifferenceResult.NotCalculable;
        }

        public bool TryInvalidate(long id)
        {
            if (this.unalteredBitmapsByID.ContainsKey(id) == false)
            {
                return false;
            }

            if (this.Current == null || this.Current.ID == id)
            {
                this.Reset();
            }

            BitmapSource unused;
            this.unalteredBitmapsByID.TryRemove(id, out unused);
            lock (this.mostRecentlyUsedIDs)
            {
                return this.mostRecentlyUsedIDs.TryRemove(id);
            }
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
                BitmapSource unalteredImage;
                this.TryGetBitmap(this.Current, out unalteredImage);

                // all moves are to display of unaltered images and invalidate any cached differences
                // it is assumed images on disk are not altered while Timelapse is running and hence unaltered bitmaps can safely be cached by their IDs
                this.ResetDifferenceState(unalteredImage);
            }

            return true;
        }

        private void CacheBitmap(long id, BitmapSource bitmap)
        {
            lock (this.mostRecentlyUsedIDs)
            {
                // cache the bitmap, replacing any existing bitmap with the one passed
                this.unalteredBitmapsByID.AddOrUpdate(id,
                    (long newID) => 
                    {
                        // if the bitmap cache is full make room for the incoming bitmap
                        if (this.mostRecentlyUsedIDs.IsFull())
                        {
                            long imageIDToRemove;
                            if (this.mostRecentlyUsedIDs.TryGetLeastRecent(out imageIDToRemove))
                            {
                                BitmapSource ignored;
                                this.unalteredBitmapsByID.TryRemove(imageIDToRemove, out ignored);
                            }
                        }

                        // indicate to add the bitmap
                        return bitmap;
                    },
                    (long existingID, BitmapSource newBitmap) => 
                    {
                        // indicate to update the bitmap
                        return newBitmap;
                    });
                this.mostRecentlyUsedIDs.SetMostRecent(id);
            }
        }

        private void ResetDifferenceState(BitmapSource unalteredImage)
        {
            this.CurrentDifferenceState = ImageDifference.Unaltered;
            this.differenceBitmapCache[ImageDifference.Unaltered] = unalteredImage;
            this.differenceBitmapCache[ImageDifference.Previous] = null;
            this.differenceBitmapCache[ImageDifference.Next] = null;
            this.differenceBitmapCache[ImageDifference.Combined] = null;
        }

        private bool TryGetBitmap(ImageRow image, out BitmapSource bitmap)
        {
            // locate the requested bitmap
            if (this.unalteredBitmapsByID.TryGetValue(image.ID, out bitmap) == false)
            {
                Task prefetch;
                if (this.prefetechesByID.TryGetValue(image.ID, out prefetch))
                {
                    // bitmap retrieval's already in progress, so wait for it to complete
                    prefetch.Wait();
                    bitmap = this.unalteredBitmapsByID[image.ID];
                }
                else
                {
                    // synchronously load the requested bitmap from disk as it isn't cached, doesn't have a prefetch running, and is needed right now by the caller
                    bitmap = image.LoadBitmap(this.Database.FolderPath);
                    this.CacheBitmap(image.ID, bitmap);
                }
            }

            // assuming a sequential forward scan order, start on the next bitmap
            this.TryInitiateBitmapPrefetch(this.CurrentRow + 1);
            return true;
        }

        private bool TryGetBitmap(int imageRow, out BitmapSource bitmap)
        {
            // get properties for the image to retrieve
            ImageRow image;
            if (this.TryGetImage(imageRow, out image) == false)
            {
                bitmap = null;
                return false;
            }

            // get the associated bitmap
            return this.TryGetBitmap(image, out bitmap);
        }

        private bool TryGetBitmapAsWriteable(int imageRow, out WriteableBitmap bitmap)
        {
            ImageRow image;
            if (this.TryGetImage(imageRow, out image) == false)
            {
                bitmap = null;
                return false;
            }

            BitmapSource bitmapSource;
            if (this.TryGetBitmap(image, out bitmapSource) == false)
            {
                bitmap = null;
                return false;
            }

            bitmap = bitmapSource.AsWriteable();
            this.CacheBitmap(image.ID, bitmap);
            return true;
        }

        private bool TryGetImage(int imageRow, out ImageRow image)
        {
            if (imageRow == this.CurrentRow)
            {
                image = this.Current;
                return true;
            }

            if (this.Database.IsImageRowInRange(imageRow) == false)
            {
                image = null;
                return false;
            }

            image = this.Database.ImageDataTable[imageRow];
            return image.IsDisplayable();
        }

        private bool TryGetNextBitmapAsWriteable(out WriteableBitmap nextBitmap)
        {
            return this.TryGetBitmapAsWriteable(this.CurrentRow + 1, out nextBitmap);
        }

        private bool TryGetPreviousBitmapAsWriteable(out WriteableBitmap previousBitmap)
        {
            return this.TryGetBitmapAsWriteable(this.CurrentRow - 1, out previousBitmap);
        }

        private bool TryInitiateBitmapPrefetch(int rowIndex)
        {
            if (this.Database.IsImageRowInRange(rowIndex) == false)
            {
                return false;
            }

            ImageRow nextImage = this.Database.ImageDataTable[rowIndex];
            if (this.unalteredBitmapsByID.ContainsKey(nextImage.ID) || this.prefetechesByID.ContainsKey(nextImage.ID))
            {
                return false;
            }

            Task prefetch = Task.Factory.StartNew(() =>
            {
                BitmapSource nextBitmap = nextImage.LoadBitmap(this.Database.FolderPath);
                this.CacheBitmap(nextImage.ID, nextBitmap);
                Task ignored;
                this.prefetechesByID.TryRemove(nextImage.ID, out ignored);
            });
            this.prefetechesByID.AddOrUpdate(nextImage.ID, prefetch, (long id, Task newPrefetch) => { return newPrefetch; });
            return true;
        }
    }
}
