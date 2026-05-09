using Carnassial.Data;
using Carnassial.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Carnassial.Images
{
    internal class AddFilesIOComputeTransactionManager : FileIOComputeTransactionManager<FileLoadStatus>
    {
        private readonly SortedDictionary<string, List<string>> filesToLoadByRelativeFolderPath;

        public int FilesToLoad { get; private set; }
        public List<string> FolderPaths { get; private init; }

        public AddFilesIOComputeTransactionManager(Action<FileLoadStatus> onProgressUpdate, TimeSpan desiredProgressInterval)
            : base(onProgressUpdate, desiredProgressInterval)
        {
            this.FilesToLoad = 0;
            this.filesToLoadByRelativeFolderPath = new(StringComparer.Create(CultureInfo.CurrentUICulture, CompareOptions.NumericOrdering));
            this.FolderPaths = [];
        }

        public async Task<int> AddFilesAsync(FileDatabase fileDatabase, int initialImageRenderWidthInPixels)
        {
            Debug.Assert(fileDatabase.ImageSet.FileSelection == FileSelection.All, "Database doesn't have all files selected.  Checking for files already added to the image set would fail.");
            this.Status.MaybeUpdateImageRenderWidth(initialImageRenderWidthInPixels);
            this.Status.MostRecentImageUpdate = NativeMethods.GetTickCount64();

            // load all files found
            // First, examine files to extract their basic properties and build a list of files not already in the database.
            // Performance is primarily a function of bytes read from disk and jpeg decoding effort.  For 8MP Busnell loads 
            // of 1000+ files on an i5-4200U with Evo 850 SSD with image classification enabled:
            // - Carnassial 2.2.0.2
            //   two threads: 21.3 files/s, 66% CPU, 48MB/s disk, image display every ~200ms
            //   read full files, decode to main window size, subsample pixels in classification
            //   (with dark checking disabled: limited by SQL inserts flushing to disk every 100 files)
            // - Carnassial 2.2.0.3
            //   four pinned threads: ~2000 files/s typical, ~85% CPU, ~23MB/s disk, image display every ~5s
            //   decode thumbnail and classify all pixels in thumbnail
            //
            //             two files/atom                                         one file/atom
            //   threads   8k reads   8k + datetime   8k + datetime + thumbnail   8k + datetime + thumbnail
            //             files/s    files/s         files/s                     files/s
            //   1         1750                       1100
            //   2         2900       2900            1850                        1800
            //   3         3250       3250            1900
            //   4         3900                       2200
            //
            // i5-4200U with PNY Elite Performance (95 MB/s) SD card with image classification enabled:
            // - Carnassial 2.2.0.3, two files/atom, 8k reads
            //   threads   files/s
            //   4         ~450
            //
            // - Carnassial 2.2.0.5
            //   7200 files/s on 5650U using one compute thread per core and hyperthreaded IO. See FileIOComputeTransactionManager..ctor().
            Dictionary<string, HashSet<string>> filesAlreadyInFileTableByRelativePath = fileDatabase.Files.HashFileNamesByRelativePath();
            this.ComputeTaskBody = (int computeTaskNumber) =>
            {
                return this.AddFilesCompute(fileDatabase, computeTaskNumber);
            };

            using (ReaderWriterLockSlim fileCreateAndAppendLock = new())
            {
                this.IOTaskBody = (int ioTaskNumber) =>
                {
                    for (FileLoadAtom? loadAtom = this.GetNextIOAtom(ioTaskNumber); loadAtom != null; loadAtom = this.GetNextIOAtom(ioTaskNumber))
                    {
                        // CreateAndAppendFiles() ultimately calls List.Add(), which is not thread safe for more than one IO task
                        // Considerig core counts in target hardware, assume at least two IO tasks will be running.
                        bool filesCreated = false;
                        fileCreateAndAppendLock.EnterWriteLock();
                        try
                        {
                            filesCreated = loadAtom.CreateAndAppendFiles(filesAlreadyInFileTableByRelativePath, fileDatabase.Files);
                        }
                        finally
                        {
                            fileCreateAndAppendLock.ExitWriteLock();
                        }
                        if (filesCreated)
                        {
                            loadAtom.CreateJpegs(fileDatabase.FolderPath, false);
                        }
                    }
                };
                await this.RunTasksAsync(fileDatabase.CreateAddFilesTransaction(), this.filesToLoadByRelativeFolderPath, this.FilesToLoad).ConfigureAwait(true);
            }
            return this.TransactionFileCount;
        }

        private int AddFilesCompute(FileDatabase fileDatabase, int computeTaskNumber)
        {
            int atoms = 0;
            TimeZoneInfo imageSetTimeZone = fileDatabase.ImageSet.GetTimeZoneInfo();
            MemoryImage? preallocatedThumbnail = null;
            for (FileLoadAtom? loadAtom = this.GetNextComputeAtom(computeTaskNumber); loadAtom != null; loadAtom = this.GetNextComputeAtom(computeTaskNumber))
            {
                // try to read file metadata
                // For files containing metadata (including hybrid video pairs) wait for metadata to be loaded from disk 
                // and, in principle, process it while any second fetch completes.  Additional bookkeeping is required 
                // to set date times on files
                // - atoms with a single video
                // - atoms with two videos
                // - atoms with a jpeg and a video which don't form a hybrid video pair
                if (loadAtom.HasAtLeastOneFile)
                {
                    loadAtom.ReadDateTimeOffsets(fileDatabase.FolderPath, imageSetTimeZone);
                    loadAtom.ClassifyFromThumbnails(ref preallocatedThumbnail);
                }

                // check if progress needs to be reported
                bool addFilesToTransaction = false;
                bool reportProgress = false;
                if (this.Progress.ShouldUpdateProgress())
                {
                    lock (this.Status)
                    {
                        if (this.Progress.ShouldUpdateProgress())
                        {
                            addFilesToTransaction = this.ShouldAddFilesToTransaction();
                            if (addFilesToTransaction == false)
                            {
                                reportProgress = loadAtom.First.MetadataReadResult != MetadataReadResults.Failed;
                            }
                        }
                    }
                }

                // transfer completed rows to pending database insert, if needed
                if (addFilesToTransaction)
                {
                    this.AddToSequence();
                }

                // queue progress report and update display image, if needed
                if (reportProgress)
                {
                    this.Status.CurrentFile = loadAtom.First.File;
                    this.Status.CurrentFileIndex = this.FilesCompleted;
                    this.QueueProgressUpdate();

                    if ((loadAtom.First.File != null) && (loadAtom.First.File.IsVideo == false))
                    {
                        ulong timeSinceLastImageUpdate = NativeMethods.GetTickCount64() - this.Status.MostRecentImageUpdate;
                        if (timeSinceLastImageUpdate > Constant.ThrottleValues.DesiredIntervalBetweenImageUpdates.TotalMilliseconds)
                        {
                            CachedImage imageToDisplay = loadAtom.First.File.TryLoadImageAsync(fileDatabase.FolderPath, this.Status.ImageRenderWidthInPixels).GetAwaiter().GetResult();
                            this.Status.SetImage(imageToDisplay);
                            this.Status.MostRecentImageUpdate = NativeMethods.GetTickCount64();
                        }
                    }
                }

                loadAtom.DisposeJpegs();
                ++atoms;
            }

            return atoms;
        }

        /// <summary>
        /// Gather sorted lists of files to load from each folder in <see cref="this.FolderPaths"/> and count total files to load.
        /// </summary>
        /// <remarks>
        /// Files are sorted alphabetically by folder name and then by UTC creation time within each folder. In general, cameras lay down
        /// images in sequentially numbered folders and file copies preserve creation time, so this has the effect of adding files to the 
        /// image set in chronological order.
        /// </remarks>
        public void FindFilesToLoad(string imageSetFolderPath)
        {
            this.FilesToLoad = 0;
            this.filesToLoadByRelativeFolderPath.Clear();

            List<string> extensions = [ Constant.File.AviFileExtension, Constant.File.Mp4FileExtension, Constant.File.JpgFileExtension ];
            EnumerationOptions folderEnumerationOptions = new()
            {
                BufferSize = Constant.File.DefaultBufferSizeInBytes // overkill but fine (docs describe 16 kB as large)
            };
            foreach (string folderPath in this.FolderPaths)
            {
                // regex not supported so have to list all files and filter
                DirectoryInfo folder = new(folderPath);
                IEnumerable<FileInfo> folderFiles = folder.EnumerateFiles("*.*", folderEnumerationOptions).Where(fileInfo => extensions.Contains(fileInfo.Extension, StringComparer.OrdinalIgnoreCase));

                List<string> filesToLoadfromFolder = [.. folderFiles.OrderBy(fileInfo => fileInfo.LastWriteTimeUtc).Select(fileInfo => fileInfo.Name)];
                string relativeFolderPath = NativeMethods.GetRelativePathFromDirectoryToDirectory(imageSetFolderPath, folderPath);
                this.FilesToLoad += filesToLoadfromFolder.Count;

                this.filesToLoadByRelativeFolderPath.Add(relativeFolderPath, filesToLoadfromFolder);
            }
            this.Status.TotalFiles = this.FilesToLoad;
        }

        public void QueueProgressUpdate()
        {
            this.Progress.QueueProgressUpdate(this.Status);
        }
    }
}
