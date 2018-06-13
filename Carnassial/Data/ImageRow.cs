using Carnassial.Control;
using Carnassial.Database;
using Carnassial.Images;
using Carnassial.Interop;
using Carnassial.Native;
using Carnassial.Util;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Exif.Makernotes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MetadataDirectory = MetadataExtractor.Directory;

namespace Carnassial.Data
{
    /// <summary>
    /// A row in the file database representing a single image (see also <seealso cref="VideoRow"/>).
    /// </summary>
    public class ImageRow : SQLiteRow, INotifyPropertyChanged
    {
        private FileClassification classification;
        private DateTimeOffset dateTimeOffset;
        private bool deleteFlag;
        private string fileName;
        private string relativePath;
        private FileTable table;

        public string[] UserControlValues { get; private set; }

        public ImageRow(string fileName, string relativePath, FileTable table)
        {
            this.classification = FileClassification.Color;
            this.dateTimeOffset = Constant.ControlDefault.DateTimeValue;
            this.deleteFlag = false;
            this.fileName = fileName;
            this.relativePath = relativePath;
            this.table = table;
            this.UserControlValues = new string[table.UserControlIndicesByDataLabel.Count];
        }

        public FileClassification Classification
        {
            get
            {
                return this.classification;
            }
            set
            {
                this.HasChanges |= this.classification != value;
                this.classification = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.Classification)));
            }
        }

        public DateTimeOffset DateTimeOffset
        {
            get
            {
                return this.dateTimeOffset;
            }
            set
            {
                this.HasChanges |= this.dateTimeOffset != value;
                this.dateTimeOffset = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.DateTimeOffset)));
            }
        }

        public bool DeleteFlag
        {
            get
            {
                return this.deleteFlag;
            }
            set
            {
                this.HasChanges |= this.deleteFlag != value;
                this.deleteFlag = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.DeleteFlag)));
            }
        }

        public string FileName
        {
            get
            {
                return this.fileName;
            }
            set
            {
                this.HasChanges |= this.fileName != value;
                this.fileName = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.FileName)));
            }
        }

        public virtual bool IsVideo
        {
            get
            {
                Debug.Assert(this.classification != FileClassification.Video, "Image unexpectedly classified as video.");
                return false;
            }
        }

        private void MarkersForCounter_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            MarkersForCounter markers = (MarkersForCounter)sender;
            this[markers.DataLabel] = markers.ToDatabaseString();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string RelativePath
        {
            get
            {
                return this.relativePath;
            }
            set
            {
                this.HasChanges |= this.relativePath != value;
                this.relativePath = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.RelativePath)));
            }
        }

        public object this[string propertyName]
        {
            get
            {
                switch (propertyName)
                {
                    case Constant.DatabaseColumn.DateTime:
                        throw new NotSupportedException("Access DateTime through DateTimeOffset.");
                    case nameof(this.DateTimeOffset):
                        return this.DateTimeOffset;
                    case Constant.DatabaseColumn.DeleteFlag:
                        return this.DeleteFlag;
                    case Constant.DatabaseColumn.File:
                        throw new NotSupportedException("Access FileName through FileName.");
                    case nameof(ImageRow.FileName):
                        return this.FileName;
                    case Constant.DatabaseColumn.ID:
                        return this.ID;
                    case Constant.DatabaseColumn.ImageQuality:
                        return this.Classification;
                    case Constant.DatabaseColumn.RelativePath:
                        return this.RelativePath;
                    case Constant.DatabaseColumn.UtcOffset:
                        throw new NotSupportedException("Access UtcOffset through DateTimeOffset.");
                    default:
                        return this.UserControlValues[this.table.UserControlIndicesByDataLabel[propertyName]];
                }
            }
            set
            {
                switch (propertyName)
                {
                    // standard controls
                    // Property change notification is sent from the properties called.
                    case Constant.DatabaseColumn.DateTime:
                        throw new NotSupportedException("DateTime must be set through DateTimeOffset.");
                    case nameof(this.DateTimeOffset):
                        this.DateTimeOffset = (DateTimeOffset)value;
                        break;
                    case Constant.DatabaseColumn.DeleteFlag:
                        this.DeleteFlag = (bool)value;
                        break;
                    case Constant.DatabaseColumn.File:
                        throw new NotSupportedException("FileName must be set through FileName.");
                    case nameof(this.FileName):
                        this.FileName = (string)value;
                        break;
                    case Constant.DatabaseColumn.ID:
                        throw new NotSupportedException("ID is immutable.");
                    case Constant.DatabaseColumn.ImageQuality:
                        this.Classification = (FileClassification)value;
                        break;
                    case Constant.DatabaseColumn.RelativePath:
                        this.RelativePath = (string)value;
                        break;
                    case Constant.DatabaseColumn.UtcOffset:
                        throw new NotSupportedException("UtcOffset must be set through DateTimeOffset.");
                    // user defined controls
                    default:
                        int userControlIndex = this.table.UserControlIndicesByDataLabel[propertyName];
                        string newValueAsString = (string)value;
                        this.HasChanges |= !String.Equals(this.UserControlValues[userControlIndex], newValueAsString, StringComparison.Ordinal);
                        this.UserControlValues[userControlIndex] = newValueAsString;
                        this.PropertyChanged?.Invoke(this, new IndexedPropertyChangedEventArgs<string>(propertyName));
                        break;
                }
            }
        }

        public DateTime UtcDateTime
        {
            get { return this.dateTimeOffset.UtcDateTime; }
        }

        public TimeSpan UtcOffset
        {
            get { return this.dateTimeOffset.Offset; }
        }

        public static FileTuplesWithID CreateDateTimeUpdate(IEnumerable<ImageRow> files)
        {
            FileTuplesWithID dateTimeTuples = new FileTuplesWithID(Constant.DatabaseColumn.DateTime, Constant.DatabaseColumn.UtcOffset);
            foreach (ImageRow file in files)
            {
                Debug.Assert(file != null, "files contains null.");
                Debug.Assert(file.ID != Constant.Database.InvalidID, "CreateDateTimeUpdate() should only be called on ImageRows which are database backed.");

                dateTimeTuples.Add(file.ID, file.UtcDateTime, DateTimeHandler.ToDatabaseUtcOffset(file.UtcOffset));
            }

            return dateTimeTuples;
        }

        public FileTuplesWithID CreateUpdate()
        {
            List<string> columnsToUpdate = new List<string>(Constant.Control.StandardControls.Count + this.table.UserControlIndicesByDataLabel.Count)
            {
                Constant.DatabaseColumn.DateTime,
                Constant.DatabaseColumn.DeleteFlag,
                Constant.DatabaseColumn.File,
                Constant.DatabaseColumn.ImageQuality,
                Constant.DatabaseColumn.RelativePath,
                Constant.DatabaseColumn.UtcOffset
            };
            columnsToUpdate.AddRange(this.table.UserControlIndicesByDataLabel.Keys);

            FileTuplesWithID tuples = new FileTuplesWithID(columnsToUpdate);
            List<object> values = new List<object>(columnsToUpdate.Count);
            foreach (string dataLabel in columnsToUpdate)
            {
                values.Add(this.GetValue(dataLabel));
            }
            tuples.Add(this.ID, values);
            return tuples;
        }

        public string GetDatabaseString(string dataLabel)
        {
            switch (dataLabel)
            {
                case Constant.DatabaseColumn.DateTime:
                    return DateTimeHandler.ToDatabaseDateTimeString(this.UtcDateTime);
                case Constant.DatabaseColumn.DeleteFlag:
                    return this.DeleteFlag ? Boolean.TrueString : Boolean.FalseString;
                case nameof(this.DateTimeOffset):
                    throw new NotSupportedException(String.Format("Unexpected data label {0}.", nameof(this.DateTimeOffset)));
                case Constant.DatabaseColumn.File:
                    return this.FileName;
                case Constant.DatabaseColumn.ID:
                    return this.ID.ToString();
                case Constant.DatabaseColumn.ImageQuality:
                    return this.Classification.ToString();
                case Constant.DatabaseColumn.RelativePath:
                    return this.RelativePath;
                case Constant.DatabaseColumn.UtcOffset:
                    return DateTimeHandler.ToDatabaseUtcOffsetString(this.UtcOffset);
                default:
                    return this.UserControlValues[this.table.UserControlIndicesByDataLabel[dataLabel]];
            }
        }

        [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1126:PrefixCallsCorrectly", Justification = "StyleCop bug.")]
        public static string GetDataBindingPath(string dataLabel)
        {
            Debug.Assert(dataLabel != Constant.DatabaseColumn.UtcOffset, String.Format("Display and editing of UTC offset should be integrated into {0}.", nameof(DataEntryDateTimeOffset)));

            if (dataLabel == Constant.DatabaseColumn.DateTime)
            {
                return nameof(ImageRow.DateTimeOffset);
            }
            if (dataLabel == Constant.DatabaseColumn.File)
            {
                return nameof(ImageRow.FileName);
            }
            if ((dataLabel == Constant.DatabaseColumn.ID) ||
                Constant.Control.StandardControls.Contains(dataLabel))
            {
                return dataLabel;
            }
            return "[" + dataLabel + "]";
        }

        public static string GetDataLabel(string propertyName)
        {
            if (propertyName == nameof(ImageRow.DateTimeOffset))
            {
                return Constant.DatabaseColumn.DateTime;
            }
            if (propertyName == nameof(ImageRow.FileName))
            {
                return Constant.DatabaseColumn.File;
            }

            return propertyName;
        }

        public string GetDisplayDateTime()
        {
            return DateTimeHandler.ToDisplayDateTimeString(this.DateTimeOffset);
        }

        public string GetDisplayString(DataEntryControl control)
        {
            switch (control.PropertyName)
            {
                case Constant.DatabaseColumn.DateTime:
                    throw new NotSupportedException(String.Format("Control has unexpected property name {0}.", Constant.DatabaseColumn.DateTime));
                case nameof(this.DateTimeOffset):
                    return this.GetDisplayDateTime();
                case Constant.DatabaseColumn.DeleteFlag:
                    return this.DeleteFlag ? Boolean.TrueString : Boolean.FalseString;
                case nameof(this.FileName):
                    return this.FileName;
                case Constant.DatabaseColumn.ID:
                    return this.ID.ToString();
                case Constant.DatabaseColumn.ImageQuality:
                    return this.Classification.ToString();
                case Constant.DatabaseColumn.RelativePath:
                    return this.RelativePath;
                case Constant.DatabaseColumn.UtcOffset:
                    return DateTimeHandler.ToDisplayUtcOffsetString(this.UtcOffset);
                default:
                    return this.UserControlValues[this.table.UserControlIndicesByDataLabel[control.DataLabel]];
            }
        }

        public FileInfo GetFileInfo(string imageSetFolderPath)
        {
            return new FileInfo(this.GetFilePath(imageSetFolderPath));
        }

        public string GetFilePath(string imageSetFolderPath)
        {
            // see RelativePath remarks in constructor
            return Path.Combine(imageSetFolderPath, this.GetRelativePath());
        }

        public MarkersForCounter GetMarkersForCounter(string dataLabel)
        {
            MarkersForCounter markersForCounter = MarkersForCounter.Parse(dataLabel, this.GetDatabaseString(dataLabel));
            markersForCounter.PropertyChanged += this.MarkersForCounter_PropertyChanged;
            return markersForCounter;
        }

        public static string GetPropertyName(string dataLabel)
        {
            Debug.Assert(dataLabel != Constant.DatabaseColumn.UtcOffset, String.Format("UTC offset should be accessed by {0}.", nameof(ImageRow.DateTimeOffset)));

            string propertyName = dataLabel;
            if (propertyName == Constant.DatabaseColumn.DateTime)
            {
                propertyName = nameof(ImageRow.DateTimeOffset);
            }
            if (dataLabel == Constant.DatabaseColumn.File)
            {
                return nameof(ImageRow.FileName);
            }
            return propertyName;
        }

        public string GetRelativePath()
        {
            if (String.IsNullOrWhiteSpace(this.RelativePath))
            {
                return this.FileName;
            }
            return Path.Combine(this.RelativePath, this.FileName);
        }

        public object GetValue(string dataLabel)
        {
            switch (dataLabel)
            {
                case Constant.DatabaseColumn.DateTime:
                    return this.UtcDateTime;
                case nameof(this.DateTimeOffset):
                    throw new NotSupportedException(String.Format("Database values for {0} and {1} must be accessed through {2}.", nameof(this.UtcDateTime), nameof(this.UtcOffset), nameof(this.DateTimeOffset)));
                case Constant.DatabaseColumn.DeleteFlag:
                    return this.DeleteFlag.ToString();
                case Constant.DatabaseColumn.File:
                    return this.FileName;
                case Constant.DatabaseColumn.ID:
                    return this.ID;
                case Constant.DatabaseColumn.ImageQuality:
                    return this.Classification.ToString();
                case Constant.DatabaseColumn.RelativePath:
                    return this.RelativePath;
                case Constant.DatabaseColumn.UtcOffset:
                    return DateTimeHandler.ToDatabaseUtcOffset(this.UtcOffset);
                default:
                    return this.UserControlValues[this.table.UserControlIndicesByDataLabel[dataLabel]];
            }
        }

        public Dictionary<string, object> GetValues()
        {
            // UtcDateTime and UtcOffset aren't included as they're portions of DateTimeOffset
            Dictionary<string, object> values = new Dictionary<string, object>()
            {
                { nameof(this.DateTimeOffset), this.DateTimeOffset },
                { Constant.DatabaseColumn.DeleteFlag, this.DeleteFlag },
                { nameof(this.FileName), this.FileName },
                { Constant.DatabaseColumn.ID, this.ID },
                { Constant.DatabaseColumn.ImageQuality, this.Classification },
                { Constant.DatabaseColumn.RelativePath, this.RelativePath },
            };
            foreach (KeyValuePair<string, int> userControl in this.table.UserControlIndicesByDataLabel)
            {
                values[userControl.Key] = this.UserControlValues[userControl.Value];
            }
            return values;
        }

        public bool IsDisplayable()
        {
            if ((this.Classification == FileClassification.Corrupt) || (this.Classification == FileClassification.NoLongerAvailable))
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Assuming this file's name in the format MMddnnnn, where nnnn is the image number, make a best effort to find MMddnnnn - 1.jpg
        /// and pull its EXIF date time field if it does.  It's assumed nnnn is ones based, rolls over at 0999, and the previous file
        /// is in the same directory.
        /// </summary>
        /// <remarks>
        /// This algorithm works for file numbering on Bushnell Trophy HD cameras using hybrid video, possibly others.
        /// </remarks>
        public bool IsPreviousJpegName(string fileName)
        {
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(this.FileName);
            string previousFileNameWithoutExtension;
            if (fileNameWithoutExtension.EndsWith("0001"))
            {
                previousFileNameWithoutExtension = fileNameWithoutExtension.Substring(0, 4) + "0999";
            }
            else
            {
                if (Int32.TryParse(fileNameWithoutExtension, out int fileNumber) == false)
                {
                    return false;
                }
                previousFileNameWithoutExtension = (fileNumber - 1).ToString("00000000");
            }

            string previousJpegName = previousFileNameWithoutExtension + Constant.File.JpgFileExtension;
            return String.Equals(previousJpegName, fileName, StringComparison.OrdinalIgnoreCase);
        }

        public async Task<MemoryImage> LoadAsync(string imageSetFolderPath)
        {
            return await this.LoadAsync(imageSetFolderPath, null);
        }

        public async virtual Task<MemoryImage> LoadAsync(string imageSetFolderPath, Nullable<int> expectedDisplayWidth)
        {
            string jpegPath = this.GetFilePath(imageSetFolderPath);
            if (File.Exists(jpegPath) == false)
            {
                return new MemoryImage(Constant.Images.FileNoLongerAvailable.Value);
            }

            // 8MP average performance (n ~= 200), milliseconds
            // scale factor  1.0  1/2   1/4    1/8
            //               110  76.3  55.9   46.1
            // Stopwatch stopwatch = new Stopwatch();
            // stopwatch.Start();
            using (FileStream stream = new FileStream(jpegPath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                if (stream.Length < Constant.Images.SmallestValidJpegSizeInBytes)
                {
                    return new MemoryImage(Constant.Images.CorruptFile.Value);
                }
                byte[] buffer = new byte[stream.Length];
                await stream.ReadAsync(buffer, 0, buffer.Length);
                MemoryImage image = new MemoryImage(buffer, expectedDisplayWidth);
                // stopwatch.Stop();
                // Trace.WriteLine(stopwatch.Elapsed.ToString("s\\.fffffff"));
                return image;
            }
        }

        public void SetDateTimeOffsetFromFileInfo(FileInfo fileInfo)
        {
            // populate new image's default date and time
            // Typically the creation time is the time a file was created in the local file system and the last write time when it was
            // last modified ever in any file system.  So, for example, copying an image from a camera's SD card to a computer results
            // in the image file on the computer having a write time which is before its creation time.  Check both and take the lesser 
            // of the two to provide a best effort default.  In most cases it's desirable to see if a more accurate time can be obtained
            // from the image's EXIF metadata.
            Debug.Assert(fileInfo.Exists, "Attempt to read DateTimeOffset from nonexistent file.");
            DateTime earliestTimeLocal = fileInfo.CreationTime < fileInfo.LastWriteTime ? fileInfo.CreationTime : fileInfo.LastWriteTime;
            this.DateTimeOffset = new DateTimeOffset(earliestTimeLocal);
        }

        public ImageProperties TryGetThumbnailProperties(string imageSetFolderPath)
        {
            using (JpegImage jpeg = new JpegImage(this.GetFilePath(imageSetFolderPath)))
            {
                if ((jpeg.Metadata != null) || jpeg.TryGetMetadata())
                {
                    MemoryImage preallocatedThumbnail = null;
                    return jpeg.GetThumbnailProperties(ref preallocatedThumbnail);
                }
                return new ImageProperties(MetadataReadResult.Failed);
            }
        }

        public bool TryMoveFileToFolder(string imageSetFolderPath, string destinationFolderPath)
        {
            string sourceFilePath = this.GetFilePath(imageSetFolderPath);
            if (!File.Exists(sourceFilePath))
            {
                // nothing to do if the source file doesn't exist
                return false;
            }

            string destinationFilePath = Path.Combine(destinationFolderPath, this.FileName);
            if (String.Equals(sourceFilePath, destinationFilePath, StringComparison.OrdinalIgnoreCase))
            {
                // nothing to do if the source and destination locations are the same
                return true;
            }

            if (File.Exists(destinationFilePath))
            {
                // can't move file since one with the same name already exists at the destination
                return false;
            }

            // the file can't be moved if there's an open FileStream on it, so close any such stream
            try
            {
                File.Move(sourceFilePath, destinationFilePath);
            }
            catch (IOException exception)
            {
                Debug.Fail(exception.ToString());
                return false;
            }

            string relativePath = NativeMethods.GetRelativePathFromDirectoryToDirectory(imageSetFolderPath, destinationFolderPath);
            if (relativePath == String.Empty || relativePath == ".")
            {
                relativePath = null;
            }
            this.RelativePath = relativePath;
            return true;
        }

        public MetadataReadResult TryReadDateTimeFromMetadata(IReadOnlyList<MetadataDirectory> metadataDirectories, TimeZoneInfo imageSetTimeZone)
        {
            ExifSubIfdDirectory exifSubIfd = metadataDirectories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            if (exifSubIfd == null)
            {
                // no EXIF information and no other plausible source of metadata
                return MetadataReadResult.None;
            }

            if (exifSubIfd.TryGetDateTime(ExifSubIfdDirectory.TagDateTimeOriginal, out DateTime dateTimeOriginal) == false)
            {
                // camera doesn't provide an EXIF standard DateTimeOriginal
                // check for a Reconyx makernote
                bool reconyxDateTimeOriginalFound = false;
                ReconyxHyperFireMakernoteDirectory hyperfireMakernote = metadataDirectories.OfType<ReconyxHyperFireMakernoteDirectory>().FirstOrDefault();
                if (hyperfireMakernote != null)
                {
                    reconyxDateTimeOriginalFound = hyperfireMakernote.TryGetDateTime(ReconyxHyperFireMakernoteDirectory.TagDateTimeOriginal, out dateTimeOriginal);
                }
                else
                {
                    ReconyxUltraFireMakernoteDirectory ultrafireMakernote = metadataDirectories.OfType<ReconyxUltraFireMakernoteDirectory>().FirstOrDefault();
                    if (ultrafireMakernote != null)
                    {
                        reconyxDateTimeOriginalFound = ultrafireMakernote.TryGetDateTime(ReconyxUltraFireMakernoteDirectory.TagDateTimeOriginal, out dateTimeOriginal);
                    }
                }

                if (reconyxDateTimeOriginalFound == false)
                {
                    return MetadataReadResult.None;
                }
            }

            // measure the extent to which the file's current date time offset and image taken metadata are consistent
            this.DateTimeOffset = DateTimeHandler.CreateDateTimeOffset(dateTimeOriginal, imageSetTimeZone);
            return MetadataReadResult.DateTime;
        }
    }
}
