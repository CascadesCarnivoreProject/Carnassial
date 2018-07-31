﻿using Carnassial.Data;
using Carnassial.Interop;
using Carnassial.Native;
using Carnassial.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Carnassial.Images
{
    internal class AddFilesIOComputeTransactionManager : FileIOComputeTransactionManager<FileLoadStatus>
    {
        private bool disposed;
        private SortedDictionary<string, List<string>> filesToLoadByRelativeFolderPath;

        public int FilesToLoad { get; private set; }
        public List<string> FolderPaths { get; private set; }

        public AddFilesIOComputeTransactionManager(Action<FileLoadStatus> onProgressUpdate, TimeSpan desiredProgressInterval)
            : base(onProgressUpdate, desiredProgressInterval)
        {
            this.disposed = false;
            this.FilesToLoad = 0;
            this.filesToLoadByRelativeFolderPath = new SortedDictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            this.FolderPaths = new List<string>();
        }

        public async Task<int> AddFilesAsync(FileDatabase fileDatabase, CarnassialState state, int initialImageRenderWidth)
        {
            Debug.Assert(fileDatabase.ImageSet.FileSelection == FileSelection.All, "Database doesn't have all files selected.  Checking for files already added to the image set would fail.");
            this.Status.MaybeUpdateImageRenderWidth(initialImageRenderWidth);
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
            Dictionary<string, HashSet<string>> filesAlreadyInFileTableByRelativePath = fileDatabase.Files.HashFileNamesByRelativePath();
            this.ComputeTaskBody = (int computeTaskNumber) =>
            {
                return this.AddFilesCompute(fileDatabase, state, computeTaskNumber);
            };
            this.IOTaskBody = (int ioTaskNumber) =>
            {
                for (FileLoadAtom loadAtom = this.GetNextIOAtom(ioTaskNumber); loadAtom != null; loadAtom = this.GetNextIOAtom(ioTaskNumber))
                {
                    if (loadAtom.CreateAndAppendFiles(filesAlreadyInFileTableByRelativePath, fileDatabase.Files))
                    {
                        loadAtom.CreateJpegs(fileDatabase.FolderPath);
                    }
                }
            };
            await this.RunTasksAsync(fileDatabase.CreateAddFilesTransaction(), this.filesToLoadByRelativeFolderPath, this.FilesToLoad);

            return this.TransactionFileCount;
        }

        private int AddFilesCompute(FileDatabase fileDatabase, CarnassialState state, int computeTaskNumber)
        {
            int atoms = 0;
            TimeZoneInfo imageSetTimeZone = fileDatabase.ImageSet.GetTimeZoneInfo();
            UInt64 progressReportIntervalInMilliseconds = (UInt64)state.Throttles.GetDesiredProgressUpdateInterval().TotalMilliseconds;
            MemoryImage preallocatedThumbnail = null;
            for (FileLoadAtom loadAtom = this.GetNextComputeAtom(computeTaskNumber); loadAtom != null; loadAtom = this.GetNextComputeAtom(computeTaskNumber))
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
                    loadAtom.ClassifyFromThumbnails(state.DarkLuminosityThreshold, state.SkipDarkImagesCheck, ref preallocatedThumbnail);
                }

                // check if progress needs to be reported
                bool addFilesToTransaction = false;
                bool reportProgress = false;
                UInt64 tickNow = NativeMethods.GetTickCount64();
                if ((tickNow - this.Status.MostRecentStatusUpdate) > progressReportIntervalInMilliseconds)
                {
                    lock (this.Status)
                    {
                        if ((tickNow - this.Status.MostRecentStatusUpdate) > progressReportIntervalInMilliseconds)
                        {
                            addFilesToTransaction = this.ShouldAddFilesToTransaction();
                            if (loadAtom.First.MetadataReadResult != MetadataReadResult.Failed)
                            {
                                if ((addFilesToTransaction == false) && (this.Status.StatusUpdateInProgress == false))
                                {
                                    reportProgress = true;
                                    this.Status.StatusUpdateInProgress = true;
                                }
                            }
                        }
                    }
                }

                // transfer completed rows to pending database insert, if needed
                if (addFilesToTransaction)
                {
                    this.AddFilesToTransaction();
                }

                // initiate progress report, if needed
                if (reportProgress)
                {
                    this.Status.CurrentFile = loadAtom.First.File;
                    this.Status.CurrentFileIndex = this.FilesCompleted;
                    MemoryImage imageToDisplay = null;
                    if (((tickNow - this.Status.MostRecentImageUpdate) > Constant.ThrottleValues.DesiredIntervalBetweenImageUpdates.TotalMilliseconds) && (loadAtom.First.File != null) && (loadAtom.First.File.IsVideo == false))
                    {
                        imageToDisplay = loadAtom.First.File.LoadAsync(fileDatabase.FolderPath, this.Status.ImageRenderWidth).GetAwaiter().GetResult();
                        this.Status.MostRecentImageUpdate = NativeMethods.GetTickCount64();
                    }
                    this.Status.UpdateImage(imageToDisplay);
                    this.ReportStatus();
                }

                loadAtom.DisposeJpegs();
                ++atoms;
            }

            return atoms;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                this.Status.Dispose();
            }
            this.disposed = true;
        }

        public void FindFilesToLoad(string imageSetFolderPath)
        {
            this.FilesToLoad = 0;
            this.filesToLoadByRelativeFolderPath.Clear();

            // sorting keeps folders and files in alphabetical order
            // This
            // - improves user experience as progress images displayed during loading are likely in order
            // - allows AddFilesAggregator to flow files to the database in order
            // - may improve disk read speed performance
            List<string> extensions = new List<string>() { Constant.File.AviFileExtension, Constant.File.Mp4FileExtension, Constant.File.JpgFileExtension };
            foreach (string folderPath in this.FolderPaths)
            {
                DirectoryInfo folder = new DirectoryInfo(folderPath);
                IEnumerable<FileInfo> matchingFiles = folder.EnumerateFiles().Where(file => extensions.Contains(file.Extension, StringComparer.OrdinalIgnoreCase));
                List<string> filesToLoadfromFolder = matchingFiles.Select(file => file.Name).OrderBy(fileName => fileName, StringComparer.OrdinalIgnoreCase).ToList();
                string relativeFolderPath = NativeMethods.GetRelativePathFromDirectoryToDirectory(imageSetFolderPath, folderPath);
                this.FilesToLoad += filesToLoadfromFolder.Count;
                this.filesToLoadByRelativeFolderPath.Add(relativeFolderPath, filesToLoadfromFolder);
            }
            this.Status.TotalFiles = this.FilesToLoad;
        }

        public void ReportStatus()
        {
            this.Progress.Report(this.Status);
            this.Status.MostRecentStatusUpdate = NativeMethods.GetTickCount64();
            this.Status.StatusUpdateInProgress = false;
        }
    }
}
