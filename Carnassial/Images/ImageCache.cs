using Carnassial.Database;
using Carnassial.Util;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Carnassial.Images
{
    public class ImageCache : FileTableEnumerator
    {
        private Dictionary<ImageDifference, BitmapSource> differenceBitmapCache;
        private MostRecentlyUsedList<long> mostRecentlyUsedIDs;
        private ConcurrentDictionary<long, Task> prefetechesByID;
        private ConcurrentDictionary<long, BitmapSource> unalteredBitmapsByID;

        public ImageDifference CurrentDifferenceState { get; private set; }

        public ImageCache(FileDatabase fileDatabase) :
            base(fileDatabase)
        {
            this.CurrentDifferenceState = ImageDifference.Unaltered;
            this.differenceBitmapCache = new Dictionary<ImageDifference, BitmapSource>();
            this.mostRecentlyUsedIDs = new MostRecentlyUsedList<long>(Constant.Images.BitmapCacheSize);
            this.prefetechesByID = new ConcurrentDictionary<long, Task>();
            this.unalteredBitmapsByID = new ConcurrentDictionary<long, BitmapSource>();
        }

        public BitmapSource GetCurrentImage()
        {
            return this.differenceBitmapCache[this.CurrentDifferenceState];
        }

        public void MoveToNextStateInCombinedDifferenceCycle()
        {
            Debug.Assert((this.Current != null) && (this.Current.IsVideo == false), "No current file or current file is an image.");

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
            Debug.Assert((this.Current != null) && (this.Current.IsVideo == false), "No current file or current file is an image.");

            // always go to unaltered from combined difference
            if (this.CurrentDifferenceState == ImageDifference.Combined)
            {
                this.CurrentDifferenceState = ImageDifference.Unaltered;
                return;
            }

            if (!this.Current.IsDisplayable())
            {
                // can't calculate differences for files which aren't displayble
                this.CurrentDifferenceState = ImageDifference.Unaltered;
                return;
            }
            else
            {
                // move to next state in cycle, wrapping around as needed
                this.CurrentDifferenceState = (this.CurrentDifferenceState >= ImageDifference.Next) ? ImageDifference.Previous : ++this.CurrentDifferenceState;
            }

            // unaltered is always displayable; no more checks required
            if (this.CurrentDifferenceState == ImageDifference.Unaltered)
            {
                return;
            }

            // can't calculate previous or next difference for the first or last file in the image set, respectively
            // can't calculate difference if needed file isn't displayable
            if (this.CurrentDifferenceState == ImageDifference.Previous && this.CurrentRow == 0)
            {
                this.MoveToNextStateInPreviousNextDifferenceCycle();
            }
            else if (this.CurrentDifferenceState == ImageDifference.Next && this.CurrentRow == this.Database.CurrentlySelectedFileCount - 1)
            {
                this.MoveToNextStateInPreviousNextDifferenceCycle();
            }
            else if (this.CurrentDifferenceState == ImageDifference.Next && !this.Database.IsFileDisplayable(this.CurrentRow + 1))
            {
                this.MoveToNextStateInPreviousNextDifferenceCycle();
            }
            else if (this.CurrentDifferenceState == ImageDifference.Previous && !this.Database.IsFileDisplayable(this.CurrentRow - 1))
            {
                this.MoveToNextStateInPreviousNextDifferenceCycle();
            }
        }

        // reset enumerator state but don't clear caches
        public override void Reset()
        {
            base.Reset();
            this.ResetDifferenceState(null);
        }

        public async Task<ImageDifferenceResult> TryCalculateDifferenceAsync()
        {
            if (this.Current == null || this.Current.IsVideo || this.Current.IsDisplayable() == false)
            {
                this.CurrentDifferenceState = ImageDifference.Unaltered;
                return ImageDifferenceResult.CurrentImageNotAvailable;
            }

            // determine which image to use for differencing
            WriteableBitmap comparisonBitmap = null;
            if (this.CurrentDifferenceState == ImageDifference.Previous)
            {
                comparisonBitmap = await this.TryGetPreviousBitmapAsWriteableAsync();
                if (comparisonBitmap == null)
                {
                    return ImageDifferenceResult.PreviousImageNotAvailable;
                }
            }
            else if (this.CurrentDifferenceState == ImageDifference.Next)
            {
                comparisonBitmap = await this.TryGetNextBitmapAsWriteableAsync();
                if (comparisonBitmap == null)
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

            BitmapSource differenceBitmap = await Task.Run(() => { return unalteredBitmap.Difference(comparisonBitmap); });
            this.differenceBitmapCache[this.CurrentDifferenceState] = differenceBitmap;
            return differenceBitmap != null ? ImageDifferenceResult.Success : ImageDifferenceResult.NotCalculable;
        }

        public async Task<ImageDifferenceResult> TryCalculateCombinedDifferenceAsync(byte differenceThreshold)
        {
            if (this.CurrentDifferenceState != ImageDifference.Combined)
            {
                return ImageDifferenceResult.NotCalculable;
            }

            // three valid images are needed: the current one, the previous one, and the next one
            if (this.Current == null || this.Current.IsVideo || this.Current.IsDisplayable() == false)
            {
                this.CurrentDifferenceState = ImageDifference.Unaltered;
                return ImageDifferenceResult.CurrentImageNotAvailable;
            }

            WriteableBitmap previousBitmap = await this.TryGetPreviousBitmapAsWriteableAsync();
            if (previousBitmap == null)
            {
                return ImageDifferenceResult.PreviousImageNotAvailable;
            }

            WriteableBitmap nextBitmap = await this.TryGetNextBitmapAsWriteableAsync();
            if (nextBitmap == null)
            {
                return ImageDifferenceResult.NextImageNotAvailable;
            }

            BitmapSource unalteredBitmap = this.differenceBitmapCache[ImageDifference.Unaltered];
            Debug.Assert(unalteredBitmap != null, "Difference cache coherency error: unaltered bitmap is null.");
            WriteableBitmap unalteredWriteableBitmap = unalteredBitmap.AsWriteable();
            this.differenceBitmapCache[ImageDifference.Unaltered] = unalteredWriteableBitmap;

            // all three images are available, so calculate and cache difference
            BitmapSource differenceBitmap = await Task.Run(() => { return unalteredWriteableBitmap.Difference(previousBitmap, nextBitmap, differenceThreshold); });
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

            BitmapSource bitmapForID;
            this.unalteredBitmapsByID.TryRemove(id, out bitmapForID);
            lock (this.mostRecentlyUsedIDs)
            {
                return this.mostRecentlyUsedIDs.TryRemove(id);
            }
        }

        public override bool TryMoveToFile(int fileIndex)
        {
            MoveToFileResult moveToFile = this.TryMoveToFileAsync(fileIndex, false).GetAwaiter().GetResult();
            return moveToFile.Succeeded;
        }

        public async Task<MoveToFileResult> TryMoveToFileAsync(int fileIndex, bool suppressPrefetch)
        {
            long oldFileID = -1;
            if (this.Current != null)
            {
                oldFileID = this.Current.ID;
            }

            if (base.TryMoveToFile(fileIndex) == false)
            {
                return new MoveToFileResult();
            }

            bool newFileToDisplay = false;
            if (this.Current.ID != oldFileID)
            {
                // if this is an image load it from cache or disk
                BitmapSource unalteredImage = null;
                if (this.Current.IsVideo == false)
                {
                    unalteredImage = await this.TryGetBitmapAsync(this.Current, suppressPrefetch);
                }

                // all moves are to display of unaltered images and invalidate any cached differences
                // it is assumed images on disk are not altered while Carnassial is running and hence unaltered bitmaps can safely be cached by their IDs
                this.ResetDifferenceState(unalteredImage);

                newFileToDisplay = true;
            }

            return new MoveToFileResult(newFileToDisplay);
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
                            long fileIDToRemove;
                            if (this.mostRecentlyUsedIDs.TryGetLeastRecent(out fileIDToRemove))
                            {
                                BitmapSource ignored;
                                this.unalteredBitmapsByID.TryRemove(fileIDToRemove, out ignored);
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

        private async Task<WriteableBitmap> TryGetBitmapAsWriteableAsync(int fileRow)
        {
            ImageRow file;
            if (this.TryGetFile(fileRow, out file) == false || file.IsVideo)
            {
                return null;
            }

            BitmapSource bitmapSource = await this.TryGetBitmapAsync(file, false);
            if (bitmapSource == null)
            {
                return null;
            }

            WriteableBitmap bitmap = bitmapSource.AsWriteable();
            this.CacheBitmap(file.ID, bitmap);
            return bitmap;
        }

        private async Task<BitmapSource> TryGetBitmapAsync(ImageRow file, bool suppressPrefetch)
        {
            // locate the requested bitmap
            BitmapSource bitmap;
            if (this.unalteredBitmapsByID.TryGetValue(file.ID, out bitmap) == false)
            {
                Task prefetch;
                if (this.prefetechesByID.TryGetValue(file.ID, out prefetch))
                {
                    // bitmap retrieval's already in progress, so wait for it to complete
                    await prefetch;
                    bitmap = this.unalteredBitmapsByID[file.ID];
                }
                else
                {
                    // load the requested bitmap from disk as it isn't cached, doesn't have a prefetch running, and is needed right now by the caller
                    bitmap = await file.LoadBitmapAsync(this.Database.FolderPath);
                    this.CacheBitmap(file.ID, bitmap);
                }
            }

            // start prefetches of nearby bitmaps if requested
            if (suppressPrefetch == false)
            {
                // assuming a sequential forward scan order the next file is the most likely to be requested
                this.TryInitiateBitmapPrefetch(this.CurrentRow + 1);
            }
            return bitmap;
        }

        private bool TryGetFile(int fileRow, out ImageRow file)
        {
            if (fileRow == this.CurrentRow)
            {
                file = this.Current;
                return true;
            }

            if (this.Database.IsFileRowInRange(fileRow) == false)
            {
                file = null;
                return false;
            }

            file = this.Database.Files[fileRow];
            return file.IsDisplayable();
        }

        private async Task<WriteableBitmap> TryGetNextBitmapAsWriteableAsync()
        {
            return await this.TryGetBitmapAsWriteableAsync(this.CurrentRow + 1);
        }

        private async Task<WriteableBitmap> TryGetPreviousBitmapAsWriteableAsync()
        {
            return await this.TryGetBitmapAsWriteableAsync(this.CurrentRow - 1);
        }

        private bool TryInitiateBitmapPrefetch(int fileIndex)
        {
            if (this.Database.IsFileRowInRange(fileIndex) == false)
            {
                return false;
            }

            ImageRow nextFile = this.Database.Files[fileIndex];
            if (nextFile.IsVideo || this.unalteredBitmapsByID.ContainsKey(nextFile.ID) || this.prefetechesByID.ContainsKey(nextFile.ID))
            {
                return false;
            }

            Task prefetch = Task.Run(async () =>
            {
                BitmapSource nextBitmap = await nextFile.LoadBitmapAsync(this.Database.FolderPath);
                this.CacheBitmap(nextFile.ID, nextBitmap);
                Task ignored;
                this.prefetechesByID.TryRemove(nextFile.ID, out ignored);
            });
            this.prefetechesByID.AddOrUpdate(nextFile.ID, prefetch, (long id, Task newPrefetch) => { return newPrefetch; });
            return true;
        }
    }
}
