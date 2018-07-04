using Carnassial.Data;
using Carnassial.Images;
using Carnassial.Interop;
using Carnassial.Native;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Carnassial.Dialog
{
    internal class DarkImagesIOComputeTransaction : FileIOComputeTransactionManager<ReclassifyStatus>
    {
        public DarkImagesIOComputeTransaction(Action<ReclassifyStatus> onProgressUpdate, TimeSpan desiredProgressInterval)
            : base(onProgressUpdate, desiredProgressInterval)
        {
        }

        public async Task ReclassifyFilesAsync(FileDatabase fileDatabase, double darkLuminosityThreshold, int expectedDisplayWidth)
        {
            this.ComputeTaskBody = (int computeTaskNumber) =>
            {
                int atoms = 0;
                MemoryImage preallocatedImage = null;
                for (FileLoadAtom loadAtom = this.GetNextComputeAtom(computeTaskNumber); loadAtom != null; loadAtom = this.GetNextComputeAtom(computeTaskNumber))
                {
                    Debug.Assert(loadAtom.HasAtLeastOneFile, "Load atom unexpectedly empty.");
                    Debug.Assert(loadAtom.First.File != null, "Load atom unexpectedly missing first file.");

                    FileClassification firstClassification = loadAtom.First.File.Classification;
                    FileClassification secondClassification = loadAtom.HasSecondFile ? loadAtom.Second.File.Classification : default(FileClassification);
                    ImageProperties firstProperties = loadAtom.Classify(darkLuminosityThreshold, ref preallocatedImage);

                    // remove files from update list if their classification did not change
                    ImageRow firstFile = loadAtom.First.File;
                    if (firstClassification == loadAtom.First.File.Classification)
                    {
                        loadAtom.First.File = null;
                    }
                    if (loadAtom.HasSecondFile && (secondClassification == loadAtom.Second.File.Classification))
                    {
                        loadAtom.Second.File = null;
                    }

                    bool addFilesToTransaction = false;
                    bool updateStatus = false;
                    UInt64 tickNow = NativeMethods.GetTickCount64();
                    if ((tickNow - this.Status.MostRecentStatusUpdate) > this.DesiredStatusIntervalInMilliseconds)
                    {
                        lock (this.Status)
                        {
                            if ((tickNow - this.Status.MostRecentStatusUpdate) > this.DesiredStatusIntervalInMilliseconds)
                            {
                                addFilesToTransaction = this.ShouldAddFilesToTransaction();
                                if (this.Status.ProgressUpdateInProgress == false)
                                {
                                    this.Status.ProgressUpdateInProgress = true;
                                    updateStatus = true;
                                }
                            }
                        }
                    }

                    if (addFilesToTransaction)
                    {
                        this.AddFilesToTransaction();
                    }

                    if (updateStatus)
                    {
                        this.Status.ClassificationToDisplay = firstFile.Classification;
                        this.Status.CurrentFileIndex = this.FilesCompleted;
                        this.Status.File = firstFile;
                        if (((tickNow - this.Status.MostRecentImageUpdate) > Constant.ThrottleValues.DesiredIntervalBetweenImageUpdates.TotalMilliseconds) && (loadAtom.First.File != null) && (loadAtom.First.File.IsVideo == false))
                        {
                            if (this.Status.Image != null)
                            {
                                this.Status.Image.Dispose();
                            }
                            MemoryImage image = firstFile.LoadAsync(fileDatabase.FolderPath, expectedDisplayWidth).GetAwaiter().GetResult();
                            this.Status.Image = image;
                            this.Status.MostRecentImageUpdate = NativeMethods.GetTickCount64();
                        }
                        this.Status.Image = null;
                        this.Status.ImageProperties = firstProperties;
                        this.Progress.Report(this.Status);
                        this.Status.MostRecentStatusUpdate = NativeMethods.GetTickCount64();
                        this.Status.ProgressUpdateInProgress = false;
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
                    loadAtom.SetFiles(fileDatabase.Files);
                    loadAtom.CreateJpegs(fileDatabase.FolderPath);
                }
            };

            SortedDictionary<string, List<string>> filesToLoadByRelativePath = fileDatabase.Files.GetFileNamesByRelativePath();
            await this.RunTasksAsync(fileDatabase.CreateUpdateSingleColumnTransaction(Constant.FileColumn.Classification), filesToLoadByRelativePath, fileDatabase.CurrentlySelectedFileCount);

            this.Status.CurrentFileIndex = this.FilesCompleted;
            this.Progress.Report(this.Status);
            this.Status.MostRecentStatusUpdate = NativeMethods.GetTickCount64();
        }
    }
}
