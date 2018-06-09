using Carnassial.Data;
using Carnassial.Native;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Carnassial.Images
{
    public class FileLoadAtom : IDisposable
    {
        private bool disposed;

        public FileLoad First { get; private set; }
        public int Offset { get; private set; }
        public string RelativePath { get; private set; }
        public FileLoad Second { get; private set; }

        public FileLoadAtom(string relativePath, FileLoad first, FileLoad second, int offset)
        {
            Debug.Assert(relativePath != null, "Relative path unexpectedly null.");
            Debug.Assert(first != null, "First file unexpectedly null.");
            Debug.Assert(second != null, "Second file unexpectedly null.");
            Debug.Assert(offset >= 0, "Offset unexpectedly negative.");

            this.disposed = false;
            this.First = first;
            this.Offset = offset;
            this.RelativePath = relativePath;
            this.Second = second;
        }

        public bool HasAtLeastOneFile
        {
            get { return (this.First.File != null) || (this.Second.File != null);  }
        }

        public bool HasSecondFile
        {
            get { return this.Second.File != null; }
        }

        public ImageProperties Classify(double darkLuminosityThreshold, ref MemoryImage preallocatedImage)
        {
            Debug.Assert(this.First.File != null, "First file unexpectedly null.");

            ImageProperties firstProperties = null;
            if (this.First.File.IsVideo)
            {
                this.First.File.ImageQuality = FileSelection.Video;
            }
            else
            {
                Debug.Assert(this.First.Jpeg != null, "First jpeg unexpectedly null.");
                if (this.First.Jpeg.Metadata == null)
                {
                    this.First.Jpeg.TryGetMetadata();
                }
                if (this.First.Jpeg.Metadata != null)
                {
                    firstProperties = this.First.Jpeg.GetThumbnailProperties(ref preallocatedImage);
                    this.First.MetadataReadResult |= MetadataReadResult.Thumbnail;
                }
                if ((firstProperties == null) || (firstProperties.CanClassify == false))
                {
                    firstProperties = this.First.Jpeg.GetProperties(Constant.Images.NoThumbnailClassificationRequestedWidthInPixels, ref preallocatedImage);
                }
                if (firstProperties.CanClassify)
                {
                    this.First.File.ImageQuality = firstProperties.EvaluateNewClassification(darkLuminosityThreshold);
                    this.First.MetadataReadResult |= MetadataReadResult.Classification;
                }
            }

            if (this.HasSecondFile)
            {
                if (this.Second.File.IsVideo)
                {
                    this.Second.File.ImageQuality = FileSelection.Video;
                }
                else
                {
                    Debug.Assert(this.Second.Jpeg != null, "Second jpeg unexpectedly null.");
                    if (this.Second.Jpeg.Metadata == null)
                    {
                        this.Second.Jpeg.TryGetMetadata();
                    }
                    if (this.Second.Jpeg.Metadata != null)
                    {
                        ImageProperties thumbnailProperties = this.Second.Jpeg.GetThumbnailProperties(ref preallocatedImage);
                        this.Second.MetadataReadResult |= MetadataReadResult.Thumbnail;
                        if (thumbnailProperties.CanClassify)
                        {
                            this.Second.File.ImageQuality = thumbnailProperties.EvaluateNewClassification(darkLuminosityThreshold);
                            this.Second.MetadataReadResult |= MetadataReadResult.Classification;
                        }
                    }
                }
            }

            return firstProperties;
        }

        public void ClassifyFromThumbnails(double darkLuminosityThreshold, bool skipDarkCheck, ref MemoryImage preallocatedThumbnail)
        {
            Debug.Assert(this.First.File != null, "First file unexpectedly null.");
            if (this.First.File.IsVideo)
            {
                this.First.File.ImageQuality = FileSelection.Video;
            }
            else if (skipDarkCheck)
            {
                this.First.File.ImageQuality = FileSelection.Ok;
            }
            else
            {
                Debug.Assert(this.First.Jpeg != null, "First jpeg unexpectedly null.");
                if (this.First.Jpeg.Metadata == null)
                {
                    this.First.Jpeg.TryGetMetadata();
                }
                if (this.First.Jpeg.Metadata != null)
                {
                    ImageProperties thumbnailProperties = this.First.Jpeg.GetThumbnailProperties(ref preallocatedThumbnail);
                    this.First.MetadataReadResult |= MetadataReadResult.Thumbnail;
                    if (thumbnailProperties.CanClassify)
                    {
                        this.First.File.ImageQuality = thumbnailProperties.EvaluateNewClassification(darkLuminosityThreshold);
                        this.First.MetadataReadResult |= MetadataReadResult.Classification;
                    }
                }
            }

            if (this.HasSecondFile)
            {
                if (this.Second.File.IsVideo)
                {
                    this.Second.File.ImageQuality = FileSelection.Video;
                }
                else if (skipDarkCheck)
                {
                    this.Second.File.ImageQuality = FileSelection.Ok;
                }
                else
                {
                    Debug.Assert(this.Second.Jpeg != null, "Second jpeg unexpectedly null.");
                    if (this.Second.Jpeg.Metadata == null)
                    {
                        this.Second.Jpeg.TryGetMetadata();
                    }
                    if (this.Second.Jpeg.Metadata != null)
                    {
                        ImageProperties thumbnailProperties = this.Second.Jpeg.GetThumbnailProperties(ref preallocatedThumbnail);
                        this.Second.MetadataReadResult |= MetadataReadResult.Thumbnail;
                        if (thumbnailProperties.CanClassify)
                        {
                            this.Second.File.ImageQuality = thumbnailProperties.EvaluateNewClassification(darkLuminosityThreshold);
                            this.Second.MetadataReadResult |= MetadataReadResult.Classification;
                        }
                    }
                }
            }
        }

        public bool CreateAndAppendFiles(Dictionary<string, HashSet<string>> fileNamesByRelativePath, FileTable files)
        {
            bool databaseHasFilesInFolder = fileNamesByRelativePath.TryGetValue(this.RelativePath, out HashSet<string> filesInFolder);
            if ((databaseHasFilesInFolder == false) || (filesInFolder.Contains(this.First.FileName) == false))
            {
                this.First.File = files.CreateAndAppendFile(this.First.FileName, this.RelativePath);
            }

            if (this.Second.FileName != null)
            {
                if ((databaseHasFilesInFolder == false) || (filesInFolder.Contains(this.Second.FileName) == false))
                {
                    this.Second.File = files.CreateAndAppendFile(this.Second.FileName, this.RelativePath);
                }
            }

            return this.HasAtLeastOneFile;
        }

        public void CreateJpegs(string imageSetFolderPath)
        {
            if ((this.First.File != null) && (this.First.File.IsVideo == false))
            {
                this.First.Jpeg = new JpegImage(this.First.File.GetFilePath(imageSetFolderPath));
            }
            if (this.HasSecondFile && (this.Second.File.IsVideo == false))
            {
                this.Second.Jpeg = new JpegImage(this.Second.File.GetFilePath(imageSetFolderPath));
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                if (this.First != null)
                {
                    this.First.Dispose();
                }
                if (this.Second != null)
                {
                    this.Second.Dispose();
                }
            }
            this.disposed = true;
        }

        public void DisposeJpegs()
        {
            if ((this.First != null) && (this.First.Jpeg != null))
            {
                this.First.Jpeg.Dispose();
            }
            if ((this.Second != null) && (this.Second.Jpeg != null))
            {
                this.Second.Jpeg.Dispose();
            }
        }

        public void ReadDateTimeOffsets(string imageSetFolderPath, TimeZoneInfo imageSetTimeZone)
        {
            Debug.Assert(this.First.File != null, "First file unexpectedly null.");
            if (this.First.File.IsVideo == false)
            {
                Debug.Assert(this.First.Jpeg != null, "First jpeg unexpectedly null.");
                if (this.First.Jpeg.TryGetMetadata())
                {
                    this.First.MetadataReadResult = this.First.File.TryReadDateTimeFromMetadata(this.First.Jpeg.Metadata, imageSetTimeZone);
                }
            }
            if (this.First.MetadataReadResult.HasFlag(MetadataReadResult.DateTime) == false)
            {
                this.First.File.SetDateTimeOffsetFromFileInfo(this.First.File.GetFileInfo(imageSetFolderPath));
            }

            if (this.HasSecondFile)
            {
                if (this.Second.File.IsVideo)
                {
                    if (this.Second.File.IsPreviousJpegName(this.First.FileName))
                    {
                        this.Second.File.DateTimeOffset = this.First.File.DateTimeOffset + Constant.Images.DefaultHybridVideoLag;
                        this.Second.MetadataReadResult = MetadataReadResult.DateTimeInferredFromPrevious;
                    }
                    else
                    {
                        this.Second.File.SetDateTimeOffsetFromFileInfo(this.Second.File.GetFileInfo(imageSetFolderPath));
                    }
                }
                else
                {
                    Debug.Assert(this.Second.Jpeg != null, "Second jpeg unexpectedly null.");
                    if (this.Second.Jpeg.TryGetMetadata())
                    {
                        this.Second.MetadataReadResult = this.Second.File.TryReadDateTimeFromMetadata(this.Second.Jpeg.Metadata, imageSetTimeZone);
                    }
                    if (this.Second.MetadataReadResult.HasFlag(MetadataReadResult.DateTime) == false)
                    {
                        this.Second.File.SetDateTimeOffsetFromFileInfo(this.Second.File.GetFileInfo(imageSetFolderPath));
                    }
                }
            }
        }

        public void SetFiles(FileTable fileTable)
        {
            this.First.File = fileTable[this.Offset];
            if (this.Second.FileName != null)
            {
                this.Second.File = fileTable[this.Offset + 1];
            }
        }
    }
}
