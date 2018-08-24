using Carnassial.Data;
using Carnassial.Images;
using Carnassial.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Carnassial.Dialog
{
    internal class DateTimeRereadIOComputeTransactionManager : FileIOComputeTransactionManager<ObservableStatus<DateTimeRereadResult>>
    {
        private UInt64 mostRecentStatusUpdate;

        public DateTimeRereadIOComputeTransactionManager(Action<ObservableStatus<DateTimeRereadResult>> onProgressUpdate, ObservableArray<DateTimeRereadResult> feedbackRows, TimeSpan desiredProgressInterval)
            : base(onProgressUpdate, desiredProgressInterval)
        {
            this.mostRecentStatusUpdate = 0;
            this.Status.FeedbackRows = feedbackRows;
        }

        public async Task RereadDateTimesAsync(FileDatabase fileDatabase)
        {
            Dictionary<string, Dictionary<string, ImageRow>> filesByRelativePathAndName = fileDatabase.Files.GetFilesByRelativePathAndName();
            this.ComputeTaskBody = (int computeTaskNumber) =>
            {
                int atoms = 0;
                TimeZoneInfo imageSetTimeZone = fileDatabase.ImageSet.GetTimeZoneInfo();
                for (FileLoadAtom loadAtom = this.GetNextComputeAtom(computeTaskNumber); loadAtom != null; loadAtom = this.GetNextComputeAtom(computeTaskNumber))
                {
                    DateTimeOffset originalDateTimeFirst = loadAtom.First.File.DateTimeOffset;
                    DateTimeOffset originalDateTimeSecond = loadAtom.Second.File == null ? Constant.ControlDefault.DateTimeValue : loadAtom.Second.File.DateTimeOffset;
                    loadAtom.ReadDateTimeOffsets(fileDatabase.FolderPath, imageSetTimeZone);

                    this.Status.FeedbackRows[loadAtom.Offset] = new DateTimeRereadResult(loadAtom.First, originalDateTimeFirst);
                    if (loadAtom.HasSecondFile)
                    {
                        this.Status.FeedbackRows[loadAtom.Offset + 1] = new DateTimeRereadResult(loadAtom.Second, originalDateTimeSecond);
                    }

                    UInt64 tickNow = NativeMethods.GetTickCount64();
                    bool addFilesToTransaction = false;
                    bool updateStatus = false;
                    if ((tickNow - this.mostRecentStatusUpdate) > this.DesiredStatusIntervalInMilliseconds)
                    {
                        lock (this.Status)
                        {
                            if ((tickNow - this.mostRecentStatusUpdate) > this.DesiredStatusIntervalInMilliseconds)
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
                        this.Progress.Report(this.Status);
                        this.mostRecentStatusUpdate = NativeMethods.GetTickCount64();
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
                    loadAtom.SetFiles(filesByRelativePathAndName);
                    Debug.Assert(loadAtom.HasAtLeastOneFile, "Load atom unexpectedly empty.");
                    Debug.Assert(loadAtom.First.File.HasChanges == false, "First file in load atom unexpectedly has changes.");
                    if (loadAtom.HasSecondFile)
                    {
                        Debug.Assert(loadAtom.Second.File.HasChanges == false, "Second file in load atom unexpectedly has changes.");
                    }

                    loadAtom.CreateJpegs(fileDatabase.FolderPath);
                }
            };

            SortedDictionary<string, List<string>> filesToLoadByRelativePath = fileDatabase.Files.GetFileNamesByRelativePath();
            await this.RunTasksAsync(fileDatabase.CreateUpdateFileDateTimeTransaction(), filesToLoadByRelativePath, fileDatabase.CurrentlySelectedFileCount);

            this.Status.CurrentFileIndex = this.FilesCompleted;
            this.Progress.Report(this.Status);
            this.mostRecentStatusUpdate = NativeMethods.GetTickCount64();
        }
    }
}