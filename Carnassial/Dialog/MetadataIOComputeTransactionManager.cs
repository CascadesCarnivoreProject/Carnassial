using Carnassial.Data;
using Carnassial.Images;
using MetadataExtractor;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Carnassial.Dialog
{
    internal class MetadataIOComputeTransactionManager : FileIOComputeTransactionManager<ObservableStatus<MetadataFieldResult>>
    {
        public MetadataIOComputeTransactionManager(Action<ObservableStatus<MetadataFieldResult>> onProgressUpdate, ObservableArray<MetadataFieldResult> feedbackRows, TimeSpan desiredProgressInterval)
            : base(onProgressUpdate, desiredProgressInterval)
        {
            this.Status.FeedbackRows = feedbackRows;
        }

        public async Task ReadFieldAsync(FileDatabase fileDatabase, string dataLabel, Tag metadataField, bool clearIfNoMetadata)
        {
            Dictionary<string, Dictionary<string, ImageRow>> filesByRelativePathAndName = fileDatabase.Files.GetFilesByRelativePathAndName();
            this.ComputeTaskBody = (int computeTaskNumber) =>
            {
                int atoms = 0;
                for (FileLoadAtom? loadAtom = this.GetNextComputeAtom(computeTaskNumber); loadAtom != null; loadAtom = this.GetNextComputeAtom(computeTaskNumber))
                {
                    Debug.Assert(loadAtom.HasAtLeastOneFile, "Load atom unexpectedly empty.");
                    Debug.Assert(loadAtom.First.File != null, "Load atom unexpectedly missing first file.");

                    string? message;
                    if ((loadAtom.First.File.IsVideo == false) &&
                        (loadAtom.First.Jpeg != null) &&
                        loadAtom.First.Jpeg.TryGetMetadata() &&
                        loadAtom.First.Jpeg.Metadata.TryGetMetadataValue(metadataField, out string? metadataValue))
                    {
                        Debug.Assert(metadataValue != null);
                        loadAtom.First.File[dataLabel] = metadataValue;
                        message = metadataValue;
                    }
                    else
                    {
                        if (clearIfNoMetadata)
                        {
                            loadAtom.First.File[dataLabel] = String.Empty;
                            message = "No metadata found.  Field cleared.";
                        }
                        else
                        {
                            Debug.Assert(loadAtom.First.File.HasChanges == false, "First file in load atom unexpectedly has changes.");
                            message = "No metadata found.  Field remains unaltered.";
                        }
                    }
                    Debug.Assert(this.Status.FeedbackRows != null, nameof(this.Status.FeedbackRows) + " set non-null in constructor.");
                    this.Status.FeedbackRows[loadAtom.Offset] = new MetadataFieldResult(loadAtom.First.FileName, message);

                    if (loadAtom.HasSecondFile)
                    {
                        if ((loadAtom.Second.File!.IsVideo == false) &&
                            (loadAtom.Second.Jpeg != null) &&
                            loadAtom.Second.Jpeg.TryGetMetadata() &&
                            loadAtom.Second.Jpeg.Metadata.TryGetMetadataValue(metadataField, out metadataValue))
                        {
                            Debug.Assert(metadataValue != null);
                            loadAtom.Second.File[dataLabel] = metadataValue;
                            message = metadataValue;
                        }
                        else
                        {
                            if (clearIfNoMetadata)
                            {
                                loadAtom.Second.File[dataLabel] = String.Empty;
                                message = "No metadata found.  Field cleared.";
                            }
                            else
                            {
                                Debug.Assert(loadAtom.Second.File.HasChanges == false, "Second file in load atom unexpectedly has changes.");
                                message = "No metadata found.  Field remains unaltered.";
                            }
                        }
                        this.Status.FeedbackRows[loadAtom.Offset + 1] = new MetadataFieldResult(loadAtom.Second.FileName, message);
                    }

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
                        this.Progress.QueueProgressUpdate(this.Status);
                    }

                    loadAtom.DisposeJpegs();
                    ++atoms;
                }

                return atoms;
            };
            this.IOTaskBody = (int ioTaskNumber) =>
            {
                for (FileLoadAtom? loadAtom = this.GetNextIOAtom(ioTaskNumber); loadAtom != null; loadAtom = this.GetNextIOAtom(ioTaskNumber))
                {
                    loadAtom.SetFiles(filesByRelativePathAndName);
                    Debug.Assert(loadAtom.HasAtLeastOneFile, "Load atom unexpectedly empty.");
                    Debug.Assert((loadAtom.First.File == null) || (loadAtom.First.File.HasChanges == false), "First file in load atom unexpectedly has changes.");
                    if (loadAtom.HasSecondFile)
                    {
                        Debug.Assert(loadAtom.Second.File!.HasChanges == false, "Second file in load atom unexpectedly has changes.");
                    }

                    loadAtom.CreateJpegs(fileDatabase.FolderPath);
                }
            };

            SortedDictionary<string, List<string>> filesToLoadByRelativePath = fileDatabase.Files.GetFileNamesByRelativePath();
            await this.RunTasksAsync(fileDatabase.CreateUpdateFileColumnTransaction(dataLabel), filesToLoadByRelativePath, fileDatabase.CurrentlySelectedFileCount).ConfigureAwait(false);

            this.Status.CurrentFileIndex = this.FilesCompleted;
            this.Progress.QueueProgressUpdate(this.Status);
        }
    }
}