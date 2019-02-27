﻿using Carnassial.Data;
using Carnassial.Images;
using Carnassial.Interop;
using Carnassial.Native;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Carnassial.Dialog
{
    internal class ReclassifyIOComputeTransaction : FileIOComputeTransactionManager<ReclassifyStatus>
    {
        public ReclassifyIOComputeTransaction(Action<ReclassifyStatus> onProgressUpdate, TimeSpan desiredProgressInterval)
            : base(onProgressUpdate, desiredProgressInterval)
        {
        }

        public async Task ReclassifyFilesAsync(FileDatabase fileDatabase, double darkLuminosityThreshold, int expectedDisplayWidth)
        {
            Dictionary<string, Dictionary<string, ImageRow>> filesByRelativePathAndName = fileDatabase.Files.GetFilesByRelativePathAndName();
            this.ComputeTaskBody = (int computeTaskNumber) =>
            {
                int atoms = 0;
                MemoryImage preallocatedImage = null;
                for (FileLoadAtom loadAtom = this.GetNextComputeAtom(computeTaskNumber); loadAtom != null; loadAtom = this.GetNextComputeAtom(computeTaskNumber))
                {
                    ImageProperties firstProperties = loadAtom.Classify(darkLuminosityThreshold, ref preallocatedImage);

                    bool addFilesToTransaction = false;
                    bool updateStatus = false;
                    if (this.Progress.ShouldUpdateProgress())
                    {
                        lock (this.Status)
                        {
                            if (this.Progress.ShouldUpdateProgress())
                            {
                                addFilesToTransaction = this.ShouldAddFilesToTransaction();
                                updateStatus = true;
                            }
                        }
                    }

                    if (addFilesToTransaction)
                    {
                        this.AddToSequence();
                    }

                    if (updateStatus)
                    {
                        this.Status.CurrentFileIndex = this.FilesCompleted;
                        this.Status.File = loadAtom.First.File;
                        this.Status.ImageProperties = firstProperties;
                        this.Progress.QueueProgressUpdate(this.Status);

                        if ((loadAtom.First.File != null) && (loadAtom.First.File.IsVideo == false))
                        {
                            ulong timeSinceLastImageUpdate = NativeMethods.GetTickCount64() - this.Status.MostRecentImageUpdate;
                            if (timeSinceLastImageUpdate > Constant.ThrottleValues.DesiredIntervalBetweenImageUpdates.TotalMilliseconds)
                            {
                                CachedImage image = loadAtom.First.File.TryLoadImageAsync(fileDatabase.FolderPath, expectedDisplayWidth).GetAwaiter().GetResult();
                                this.Status.SetImage(image);
                                this.Status.MostRecentImageUpdate = NativeMethods.GetTickCount64();
                            }
                        }
                    }

                    loadAtom.DisposeJpegs();
                    ++atoms;
                }

                return atoms;
            };
            this.IOTaskBody = (int ioTaskNumber) =>
            {
                for (FileLoadAtom loadAtom = this.GetNextIOAtom(ioTaskNumber); loadAtom != null; loadAtom = this.GetNextIOAtom(ioTaskNumber))
                {
                    // attach ImageRows for files in load atom
                    loadAtom.SetFiles(filesByRelativePathAndName);
                    Debug.Assert(loadAtom.HasAtLeastOneFile, "Load atom unexpectedly empty.");
                    Debug.Assert(loadAtom.First.File.HasChanges == false, "First file in load atom unexpectedly has changes.");
                    if (loadAtom.HasSecondFile)
                    {
                        Debug.Assert(loadAtom.Second.File.HasChanges == false, "Second file in load atom unexpectedly has changes.");
                    }

                    // try to load images for files in load item
                    // This also performs core file classification by setting the IO component of files' classification when
                    // - a file is no longer available
                    // - an image file is sufficiently corrupt its metadata can't be loaded
                    // - a file is a video
                    // For image files which can be decoded, the compute task then refines their classification based on their pixels
                    // and commits any classifications which changed to the database.
                    loadAtom.CreateJpegs(fileDatabase.FolderPath);
                }
            };

            SortedDictionary<string, List<string>> filesToLoadByRelativePath = fileDatabase.Files.GetFileNamesByRelativePath();
            await this.RunTasksAsync(fileDatabase.CreateUpdateFileColumnTransaction(Constant.FileColumn.Classification), filesToLoadByRelativePath, fileDatabase.CurrentlySelectedFileCount);

            this.Status.CurrentFileIndex = this.FilesCompleted;
            this.Progress.QueueProgressUpdate(this.Status);
        }
    }
}
