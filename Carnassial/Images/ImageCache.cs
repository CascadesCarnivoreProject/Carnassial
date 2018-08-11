using Carnassial.Data;
using Carnassial.Native;
using Carnassial.Util;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Carnassial.Images
{
    public class ImageCache : FileTableEnumerator
    {
        private int combinedDifferencesCalculated;
        private TimeSpan combinedDifferenceTime;
        private Dictionary<ImageDifference, CachedImage> differenceCache;
        private int differencesCalculated;
        private TimeSpan differenceTime;
        private bool disposed;
        private MostRecentlyUsedList<long> mostRecentlyUsedIDs;
        private ConcurrentDictionary<long, Task> prefetechesByID;
        private ConcurrentDictionary<long, CachedImage> unalteredImagesByID;

        public ImageDifference CurrentDifferenceState { get; private set; }

        public ImageCache(FileDatabase fileDatabase) :
            base(fileDatabase)
        {
            this.CurrentDifferenceState = ImageDifference.Unaltered;
            this.differenceCache = new Dictionary<ImageDifference, CachedImage>(4);
            foreach (ImageDifference differenceState in Enum.GetValues(typeof(ImageDifference)))
            {
                this.differenceCache.Add(differenceState, null);
            }
            this.differencesCalculated = 0;
            this.differenceTime = TimeSpan.Zero;
            this.disposed = false;
            this.mostRecentlyUsedIDs = new MostRecentlyUsedList<long>(Constant.Images.ImageCacheSize);
            this.prefetechesByID = new ConcurrentDictionary<long, Task>();
            this.unalteredImagesByID = new ConcurrentDictionary<long, CachedImage>();
        }

        public double AverageCombinedDifferenceTimeInSeconds
        {
            get { return this.combinedDifferencesCalculated == 0 ? 0.0 : this.combinedDifferenceTime.TotalSeconds / this.combinedDifferencesCalculated; }
        }

        public double AverageDifferenceTimeInSeconds
        {
            get { return this.differencesCalculated == 0 ? 0.0 : this.differenceTime.TotalSeconds / this.differencesCalculated; }
        }

        protected override void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                this.ResetDifferenceState(null);
                foreach (CachedImage image in this.unalteredImagesByID.Values)
                {
                    image.Dispose();
                }
            }

            base.Dispose(disposing);
            this.disposed = true;
        }

        public CachedImage GetCurrentImage()
        {
            return this.differenceCache[this.CurrentDifferenceState];
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
            else if (this.CurrentDifferenceState == ImageDifference.Next && this.CurrentRow == this.FileDatabase.CurrentlySelectedFileCount - 1)
            {
                this.MoveToNextStateInPreviousNextDifferenceCycle();
            }
            else if (this.CurrentDifferenceState == ImageDifference.Next && !this.FileDatabase.IsFileDisplayable(this.CurrentRow + 1))
            {
                this.MoveToNextStateInPreviousNextDifferenceCycle();
            }
            else if (this.CurrentDifferenceState == ImageDifference.Previous && !this.FileDatabase.IsFileDisplayable(this.CurrentRow - 1))
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

        public async Task<ImageDifferenceResult> TryCalculateDifferenceAsync(byte differenceThreshold)
        {
            if (this.Current == null || this.Current.IsVideo || this.Current.IsDisplayable() == false)
            {
                this.CurrentDifferenceState = ImageDifference.Unaltered;
                return ImageDifferenceResult.CurrentImageNotAvailable;
            }

            CachedImage unaltered = this.differenceCache[ImageDifference.Unaltered];
            if (unaltered.Image == null)
            {
                this.CurrentDifferenceState = ImageDifference.Unaltered;
                return ImageDifferenceResult.CurrentImageNotAvailable;
            }

            // determine which image to use for differencing
            CachedImage comparisonImage = null;
            if (this.CurrentDifferenceState == ImageDifference.Previous)
            {
                comparisonImage = await this.TryGetPreviousImageAsync();
                if (comparisonImage.Image == null)
                {
                    return ImageDifferenceResult.PreviousImageNotAvailable;
                }
            }
            else if (this.CurrentDifferenceState == ImageDifference.Next)
            {
                comparisonImage = await this.TryGetNextImageAsync();
                if (comparisonImage.Image == null)
                {
                    return ImageDifferenceResult.NextImageNotAvailable;
                }
            }
            else
            {
                return ImageDifferenceResult.NotCalculable;
            }

            MemoryImage difference = null;
            bool differenceComputed = await Task.Run(() =>
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                bool success = unaltered.Image.TryDifference(comparisonImage.Image, differenceThreshold, out difference);
                if (success)
                {
                    ++this.differencesCalculated;
                    this.differenceTime += stopwatch.Elapsed;
                }
                return success;
            });
            this.differenceCache[this.CurrentDifferenceState] = new CachedImage(difference);
            return differenceComputed ? ImageDifferenceResult.Success : ImageDifferenceResult.NotCalculable;
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

            CachedImage unaltered = this.differenceCache[ImageDifference.Unaltered];
            if (unaltered.Image == null)
            {
                this.CurrentDifferenceState = ImageDifference.Unaltered;
                return ImageDifferenceResult.CurrentImageNotAvailable;
            }

            CachedImage previous = await this.TryGetPreviousImageAsync();
            if (previous.Image == null)
            {
                return ImageDifferenceResult.PreviousImageNotAvailable;
            }

            CachedImage next = await this.TryGetNextImageAsync();
            if (next.Image == null)
            {
                return ImageDifferenceResult.NextImageNotAvailable;
            }

            // all three images are available, so calculate and cache difference
            MemoryImage difference = null;
            bool differenceComputed = await Task.Run((Func<bool>)(() => 
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                bool success = unaltered.Image.TryDifference(previous.Image, next.Image, differenceThreshold, out difference);
                stopwatch.Stop();
                if (success)
                {
                    ++this.combinedDifferencesCalculated;
                    this.combinedDifferenceTime += stopwatch.Elapsed;
                }
                return success;
            }));
            this.differenceCache[ImageDifference.Combined] = new CachedImage(difference);
            return difference != null ? ImageDifferenceResult.Success : ImageDifferenceResult.NotCalculable;
        }

        public bool TryInvalidate(long id)
        {
            if (this.unalteredImagesByID.ContainsKey(id) == false)
            {
                return false;
            }

            if (this.Current == null || this.Current.ID == id)
            {
                this.Reset();
            }

            if (this.unalteredImagesByID.TryRemove(id, out CachedImage imageForID))
            {
                imageForID.Dispose();
            }
            lock (this.mostRecentlyUsedIDs)
            {
                return this.mostRecentlyUsedIDs.TryRemove(id);
            }
        }

        public override bool TryMoveToFile(int fileIndex)
        {
            MoveToFileResult moveToFile = this.TryMoveToFileAsync(fileIndex, 0).GetAwaiter().GetResult();
            return moveToFile.Succeeded;
        }

        public async Task<MoveToFileResult> TryMoveToFileAsync(int fileIndex, int prefetchStride)
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
                CachedImage unalteredImage = null;
                if (this.Current.IsVideo == false)
                {
                    unalteredImage = await this.TryGetImageAsync(this.Current, prefetchStride);
                }

                // all moves are to display of unaltered images and invalidate any cached differences
                // it is assumed images on disk are not altered while Carnassial is running and hence unaltered images can safely be cached by their IDs
                this.ResetDifferenceState(unalteredImage);

                newFileToDisplay = true;
            }

            return new MoveToFileResult(newFileToDisplay);
        }

        private void CacheImage(long id, CachedImage image)
        {
            lock (this.mostRecentlyUsedIDs)
            {
                // cache the image, replacing any existing image with the one passed
                this.unalteredImagesByID.AddOrUpdate(id,
                    (long newID) => 
                    {
                        // if the image cache is full make room for the incoming image
                        if (this.mostRecentlyUsedIDs.IsFull())
                        {
                            if (this.mostRecentlyUsedIDs.TryGetLeastRecent(out long fileIDToRemove))
                            {
                                if (this.unalteredImagesByID.TryRemove(fileIDToRemove, out CachedImage imageForID))
                                {
                                    imageForID.Dispose();
                                }
                            }
                        }

                        // indicate to add the image
                        return image;
                    },
                    (long existingID, CachedImage newImage) => 
                    {
                        // indicate to update the image
                        return newImage;
                    });
                this.mostRecentlyUsedIDs.SetMostRecent(id);
            }
        }

        private void ResetDifferenceState(CachedImage unaltered)
        {
            this.CurrentDifferenceState = ImageDifference.Unaltered;
            this.differenceCache[ImageDifference.Unaltered] = unaltered;

            foreach (ImageDifference difference in new ImageDifference[] { ImageDifference.Previous, ImageDifference.Next, ImageDifference.Combined })
            {
                CachedImage differenceImage = this.differenceCache[difference];
                if (differenceImage != null)
                {
                    differenceImage.Dispose();
                    this.differenceCache[difference] = null;
                }
            }
        }

        private async Task<CachedImage> TryGetImageAsync(int fileRow)
        {
            if (this.TryGetFile(fileRow, out ImageRow file) == false || file.IsVideo)
            {
                return null;
            }

            return await this.TryGetImageAsync(file, 0);
        }

        private async Task<CachedImage> TryGetImageAsync(ImageRow file, int prefetchStride)
        {
            // locate the requested image
            if (this.unalteredImagesByID.TryGetValue(file.ID, out CachedImage image) == false)
            {
                if (this.prefetechesByID.TryGetValue(file.ID, out Task prefetch))
                {
                    // image retrieval's already in progress, so wait for it to complete
                    await prefetch;
                    image = this.unalteredImagesByID[file.ID];
                }
                else
                {
                    // load the requested image from disk as it isn't cached, doesn't have a prefetch running, and is needed right now 
                    // by the caller
                    image = await file.TryLoadImageAsync(this.FileDatabase.FolderPath);
                    this.CacheImage(file.ID, image);
                }
            }

            // start prefetches of nearby images if requested
            if (prefetchStride != 0)
            {
                this.TryInitiatePrefetch(this.CurrentRow + prefetchStride);
            }
            return image;
        }

        private bool TryGetFile(int fileRow, out ImageRow file)
        {
            if (fileRow == this.CurrentRow)
            {
                file = this.Current;
                return true;
            }

            if (this.FileDatabase.IsFileRowInRange(fileRow) == false)
            {
                file = null;
                return false;
            }

            file = this.FileDatabase.Files[fileRow];
            return file.IsDisplayable();
        }

        private async Task<CachedImage> TryGetNextImageAsync()
        {
            return await this.TryGetImageAsync(this.CurrentRow + 1);
        }

        private async Task<CachedImage> TryGetPreviousImageAsync()
        {
            return await this.TryGetImageAsync(this.CurrentRow - 1);
        }

        private bool TryInitiatePrefetch(int fileIndex)
        {
            if (this.FileDatabase.IsFileRowInRange(fileIndex) == false)
            {
                return false;
            }

            ImageRow nextFile = this.FileDatabase.Files[fileIndex];
            if (nextFile.IsVideo || this.unalteredImagesByID.ContainsKey(nextFile.ID) || this.prefetechesByID.ContainsKey(nextFile.ID))
            {
                return false;
            }

            Task prefetch = Task.Run((Func<Task>)(async () =>
            {
                CachedImage nextImage = await nextFile.TryLoadImageAsync((string)this.FileDatabase.FolderPath);
                this.CacheImage(nextFile.ID, nextImage);
                this.prefetechesByID.TryRemove(nextFile.ID, out Task ignored);
            }));
            this.prefetechesByID.AddOrUpdate(nextFile.ID, prefetch, (long id, Task newPrefetch) => { return newPrefetch; });
            return true;
        }
    }
}
