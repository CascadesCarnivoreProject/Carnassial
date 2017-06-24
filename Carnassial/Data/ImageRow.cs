using Carnassial.Control;
using Carnassial.Database;
using Carnassial.Native;
using Carnassial.Util;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Exif.Makernotes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Directory = System.IO.Directory;
using MetadataDirectory = MetadataExtractor.Directory;

namespace Carnassial.Data
{
    /// <summary>
    /// A row in the file database representing a single image (see also <seealso cref="VideoRow"/>).
    /// </summary>
    public class ImageRow : DataRowBackedObject, INotifyPropertyChanged
    {
        public ImageRow(DataRow row)
            : base(row)
        {
        }

        public DateTime DateTime
        {
            get { return this.Row.GetDateTimeField(Constant.DatabaseColumn.DateTime); }
        }

        public DateTimeOffset DateTimeOffset
        {
            get
            {
                return DateTimeHandler.FromDatabaseDateTimeOffset(this.DateTime, this.UtcOffset);
            }
            set
            {
                this.Row.SetField(Constant.DatabaseColumn.DateTime, value.UtcDateTime);
                this.Row.SetUtcOffsetField(Constant.DatabaseColumn.UtcOffset, value.Offset);
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.DateTimeOffset)));
            }
        }

        public bool DeleteFlag
        {
            get
            {
                return this.Row.GetBooleanField(Constant.DatabaseColumn.DeleteFlag);
            }
            set
            {
                this.Row.SetField(Constant.DatabaseColumn.DeleteFlag, value);
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.DeleteFlag)));
            }
        }

        public string FileName
        {
            get
            {
                return this.Row.GetStringField(Constant.DatabaseColumn.File);
            }
            set
            {
                this.Row.SetField(Constant.DatabaseColumn.File, value);
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.FileName)));
            }
        }

        public FileSelection ImageQuality
        {
            get
            {
                return this.Row.GetEnumField<FileSelection>(Constant.DatabaseColumn.ImageQuality);
            }
            set
            {
                switch (value)
                {
                    case FileSelection.Corrupt:
                    case FileSelection.Dark:
                    case FileSelection.NoLongerAvailable:
                    case FileSelection.Ok:
                        this.Row.SetField<FileSelection>(Constant.DatabaseColumn.ImageQuality, value);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(value), String.Format("{0} is not an ImageQuality.  ImageQuality must be one of CorruptFile, Dark, FileNoLongerAvailable, or Ok.", value));
                }
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.ImageQuality)));
            }
        }

        public virtual bool IsVideo
        {
            get { return false; }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string RelativePath
        {
            get
            {
                return this.Row.GetStringField(Constant.DatabaseColumn.RelativePath);
            }
            set
            {
                this.Row.SetField(Constant.DatabaseColumn.RelativePath, value);
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
                        return this.ImageQuality;
                    case Constant.DatabaseColumn.RelativePath:
                        return this.RelativePath;
                    case Constant.DatabaseColumn.UtcOffset:
                        throw new NotSupportedException("Access UtcOffset through DateTimeOffset.");
                    default:
                        return this.Row.GetStringField(propertyName);
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
                        this.ImageQuality = (FileSelection)value;
                        break;
                    case Constant.DatabaseColumn.RelativePath:
                        this.RelativePath = (string)value;
                        break;
                    case Constant.DatabaseColumn.UtcOffset:
                        throw new NotSupportedException("UtcOffset must be set through DateTimeOffset.");
                    // user defined controls
                    default:
                        this.Row.SetField(propertyName, (string)value);
                        this.PropertyChanged?.Invoke(this, new IndexedPropertyChangedEventArgs<string>(propertyName));
                        break;
                }
            }
        }

        public TimeSpan UtcOffset
        {
            get { return this.Row.GetUtcOffsetField(Constant.DatabaseColumn.UtcOffset); }
        }

        public Dictionary<string, object> AsDictionary()
        {
            Dictionary<string, object> values = new Dictionary<string, object>();
            foreach (DataColumn column in this.Row.Table.Columns)
            {
                if (column.ColumnName == Constant.DatabaseColumn.UtcOffset)
                {
                    // UTC offset is included in the DateTimeOffset returned for DateTime
                    continue;
                }
                string propertyName = ImageRow.GetPropertyName(column.ColumnName);
                values.Add(propertyName, this[propertyName]);
            }
            return values;
        }

        public FileTuplesWithID CreateDateTimeUpdate()
        {
            return ImageRow.CreateDateTimeUpdate(new List<ImageRow>() { this });
        }

        public static FileTuplesWithID CreateDateTimeUpdate(IEnumerable<ImageRow> files)
        {
            FileTuplesWithID dateTimeTuples = new FileTuplesWithID(Constant.DatabaseColumn.DateTime, Constant.DatabaseColumn.UtcOffset);
            foreach (ImageRow file in files)
            {
                Debug.Assert(file != null, "files contains null.");
                Debug.Assert(file.ID != Constant.Database.InvalidID, "CreateDateTimeUpdate() should only be called on ImageRows which are database backed.");

                dateTimeTuples.Add(file.ID, file.DateTime, DateTimeHandler.ToDatabaseUtcOffset(file.UtcOffset));
            }

            return dateTimeTuples;
        }

        public FileTuplesWithID CreateUpdate()
        {
            List<string> columnsToUpdate = new List<string>(this.Row.Table.Columns.Count - 1);
            foreach (DataColumn column in this.Row.Table.Columns)
            {
                if (column.ColumnName == Constant.DatabaseColumn.ID)
                {
                    continue;
                }
                columnsToUpdate.Add(column.ColumnName);
            }

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
                    return DateTimeHandler.ToDatabaseDateTimeString(this.DateTime);
                case Constant.DatabaseColumn.DeleteFlag:
                    return this.DeleteFlag ? Boolean.TrueString : Boolean.FalseString;
                case nameof(this.DateTimeOffset):
                    throw new NotSupportedException(String.Format("Unexpected data label {0}.", nameof(this.DateTimeOffset)));
                case Constant.DatabaseColumn.File:
                    return this.FileName;
                case Constant.DatabaseColumn.ID:
                    return this.ID.ToString();
                case Constant.DatabaseColumn.ImageQuality:
                    return this.ImageQuality.ToString();
                case Constant.DatabaseColumn.RelativePath:
                    return this.RelativePath;
                case Constant.DatabaseColumn.UtcOffset:
                    return DateTimeHandler.ToDatabaseUtcOffsetString(this.UtcOffset);
                default:
                    return this.Row.GetStringField(dataLabel);
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
                case Constant.DatabaseColumn.File:
                    return this.FileName;
                case Constant.DatabaseColumn.ID:
                    return this.ID.ToString();
                case Constant.DatabaseColumn.ImageQuality:
                    return this.ImageQuality.ToString();
                case Constant.DatabaseColumn.RelativePath:
                    return this.RelativePath;
                case Constant.DatabaseColumn.UtcOffset:
                    return DateTimeHandler.ToDisplayUtcOffsetString(this.UtcOffset);
                default:
                    return this.Row.GetStringField(control.DataLabel);
            }
        }

        public FileInfo GetFileInfo(string rootFolderPath)
        {
            return new FileInfo(this.GetFilePath(rootFolderPath));
        }

        public string GetFilePath(string rootFolderPath)
        {
            // see RelativePath remarks in constructor
            return Path.Combine(rootFolderPath, this.GetRelativePath());
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
                    return this.DateTime;
                case nameof(this.DateTimeOffset):
                    throw new NotSupportedException(String.Format("Database values for {0} and {1} must be accessed through {2}.", nameof(this.DateTime), nameof(this.UtcOffset), nameof(this.DateTimeOffset)));
                case Constant.DatabaseColumn.DeleteFlag:
                    return this.DeleteFlag.ToString();
                case Constant.DatabaseColumn.ID:
                    return this.ID;
                case Constant.DatabaseColumn.ImageQuality:
                    return this.ImageQuality.ToString();
                case Constant.DatabaseColumn.UtcOffset:
                    return DateTimeHandler.ToDatabaseUtcOffset(this.UtcOffset);
                default:
                    return this.Row.GetStringField(dataLabel);
            }
        }

        public bool IsDisplayable()
        {
            if (this.ImageQuality == FileSelection.Corrupt || this.ImageQuality == FileSelection.NoLongerAvailable)
            {
                return false;
            }
            return true;
        }

        public async Task<MemoryImage> LoadAsync(string baseFolderPath)
        {
            return await this.LoadAsync(baseFolderPath, null);
        }

        // 8MP average performance (n ~= 200), milliseconds
        // scale factor  1.0  1/2   1/4    1/8
        //               110  76.3  55.9   46.1
        public async virtual Task<MemoryImage> LoadAsync(string baseFolderPath, Nullable<int> expectedDisplayWidth)
        {
            // Stopwatch stopwatch = new Stopwatch();
            // stopwatch.Start();
            string path = this.GetFilePath(baseFolderPath);
            if (!File.Exists(path))
            {
                return Constant.Images.FileNoLongerAvailable.Value;
            }

            byte[] jpegBuffer;
            using (FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.SequentialScan))
            {
                if (fileStream.Length < Constant.Images.SmallestValidJpegSizeInBytes)
                {
                    return Constant.Images.CorruptFile.Value;
                }
                jpegBuffer = new byte[fileStream.Length];
                await fileStream.ReadAsync(jpegBuffer, 0, jpegBuffer.Length);
            }

            try
            {
                // MemoryImage assumes the buffer is not empty
                Debug.Assert(jpegBuffer.Length >= Constant.Images.SmallestValidJpegSizeInBytes, "Unexpectedly small JPEG buffer.");
                MemoryImage image = new MemoryImage(jpegBuffer, expectedDisplayWidth);
                // stopwatch.Stop();
                // Trace.WriteLine(stopwatch.Elapsed.ToString("s\\.fffffff"));
                return image;
            }
            catch (ArgumentException)
            {
                return Constant.Images.CorruptFile.Value;
            }
        }

        public void SetDateTimeOffsetFromFileInfo(string folderPath, TimeZoneInfo imageSetTimeZone)
        {
            // populate new image's default date and time
            // Typically the creation time is the time a file was created in the local file system and the last write time when it was
            // last modified ever in any file system.  So, for example, copying an image from a camera's SD card to a computer results
            // in the image file on the computer having a write time which is before its creation time.  Check both and take the lesser 
            // of the two to provide a best effort default.  In most cases it's desirable to see if a more accurate time can be obtained
            // from the image's EXIF metadata.
            FileInfo fileInfo = this.GetFileInfo(folderPath);
            DateTime earliestTimeLocal = fileInfo.CreationTime < fileInfo.LastWriteTime ? fileInfo.CreationTime : fileInfo.LastWriteTime;
            this.DateTimeOffset = new DateTimeOffset(earliestTimeLocal);
        }

        /// <summary>
        /// Move corresponding file to the deleted files folder.
        /// </summary>
        public bool TryMoveFileToDeletedFilesFolder(string folderPath)
        {
           // create the deleted files folder if necessary
            string deletedFilesFolderPath = Path.Combine(folderPath, Constant.File.DeletedFilesFolder);
            if (!Directory.Exists(deletedFilesFolderPath))
            {
                Directory.CreateDirectory(deletedFilesFolderPath);
            }

            return this.TryMoveToFolder(folderPath, deletedFilesFolderPath, true);
        }

        public bool TryMoveToFolder(string folderPath, string destinationFolderPath, bool isSoftDelete)
        {
            string sourceFilePath = this.GetFilePath(folderPath);
            if (!File.Exists(sourceFilePath))
            {
                // nothing to do if the source file doesn't exist
                return false;
            }

            string destinationFilePath = Path.Combine(destinationFolderPath, this.FileName);
            if (String.Equals(sourceFilePath, destinationFilePath, StringComparison.OrdinalIgnoreCase))
            {
                // nothing to do if the file is already at the desired location
                return true;
            }

            if (File.Exists(destinationFilePath))
            {
                // can't move file since one with the same name already exists at the destination
                if (isSoftDelete == false)
                {
                    return false;
                }

                // delete the destination file if it already exists since File.Move() doesn't allow overwriting
                try
                {
                    File.Delete(sourceFilePath);
                    return true;
                }
                catch (IOException exception)
                {
                    Debug.Fail(exception.ToString());
                    return false;
                }
            }

            try
            {
                File.Move(sourceFilePath, destinationFilePath);
                if (isSoftDelete == false)
                {
                    string relativePath = NativeMethods.GetRelativePathFromDirectoryToDirectory(folderPath, destinationFolderPath);
                    if (relativePath == String.Empty || relativePath == ".")
                    {
                        relativePath = null;
                    }
                    this.RelativePath = relativePath;
                }
                return true;
            }
            catch (IOException exception)
            {
                Debug.Fail(exception.ToString());
                return false;
            }
        }

        public DateTimeAdjustment TryReadDateTimeFromMetadata(string folderPath, TimeZoneInfo imageSetTimeZone)
        {
            IReadOnlyList<MetadataDirectory> metadataDirectories;
            string filePath = this.GetFilePath(folderPath);
            try
            {
                metadataDirectories = ImageMetadataReader.ReadMetadata(filePath);
            }
            catch (ImageProcessingException)
            {
                // typically this indicates a corrupt file
                // Most commonly this is last file in the add as opening cameras to turn them off triggers them, resulting in a race condition between writing
                // the file and the camera being turned off.
                return DateTimeAdjustment.None;
            }
            ExifSubIfdDirectory exifSubIfd = metadataDirectories.OfType<ExifSubIfdDirectory>().FirstOrDefault();

            bool previousImageUsed = false;
            DateTime dateTimeOriginal;
            if (exifSubIfd == null)
            {
                // no EXIF information and no other plausible source of metadata
                if (this.IsVideo == false)
                {
                    return DateTimeAdjustment.None;
                }

                // if this is a video file try to fall back to an associated image file if one's available due to the camera operating in hybrid mode
                if (this.TryReadDateTimeFromPreviousJpeg(folderPath, out dateTimeOriginal) == false)
                {
                    return DateTimeAdjustment.None;
                }
                previousImageUsed = true;
            }
            else if (exifSubIfd.TryGetDateTime(ExifSubIfdDirectory.TagDateTimeOriginal, out dateTimeOriginal) == false)
            {
                // camera doesn't conform to EXIF standard
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
                    return DateTimeAdjustment.None;
                }
            }

            // measure the extent to which the file's current date time offset and image taken metadata are consistent
            DateTimeOffset currentDateTime = this.DateTimeOffset;
            DateTimeOffset exifDateTime = DateTimeHandler.CreateDateTimeOffset(dateTimeOriginal, imageSetTimeZone);
            bool dateAdjusted = currentDateTime.Date != exifDateTime.Date;
            bool timeAdjusted = currentDateTime.TimeOfDay != exifDateTime.TimeOfDay;
            bool offsetAdjusted = currentDateTime.Offset != exifDateTime.Offset;
            if (dateAdjusted || timeAdjusted || offsetAdjusted)
            {
                this.DateTimeOffset = exifDateTime;
            }

            // return extent of the time adjustment
            DateTimeAdjustment dateTimeAdjustment = DateTimeAdjustment.None;
            if (dateAdjusted)
            {
                dateTimeAdjustment |= DateTimeAdjustment.MetadataDate;
            }
            if (timeAdjusted)
            {
                dateTimeAdjustment |= DateTimeAdjustment.MetadataTime;
            }
            if (offsetAdjusted)
            {
                dateTimeAdjustment |= DateTimeAdjustment.ImageSetOffset;
            }

            if (dateAdjusted == false && timeAdjusted == false && offsetAdjusted == false)
            {
                dateTimeAdjustment |= DateTimeAdjustment.NoChange;
            }
            if (previousImageUsed)
            {
                dateTimeAdjustment |= DateTimeAdjustment.PreviousMetadata;
            }

            return dateTimeAdjustment;
        }

        /// <summary>
        /// Assuming this file's name in the format MMddnnnn, where nnnn is the image number, make a best effort to find MMddnnnn - 1.jpg and pull 
        /// its EXIF date time field if it does.  It's assumed nnnn is ones based, rolls over at 0999, and the previous file
        /// is in the same directory.  If matching across directories is needed this logic can be hoisted to have database access.
        /// </summary>
        /// <remarks>
        /// This algorithm works for file numbering on Bushnell Trophy HD cameras using hybrid video, possibly others.
        /// </remarks>
        private bool TryReadDateTimeFromPreviousJpeg(string folderPath, out DateTime dateTimeOriginal)
        {
            dateTimeOriginal = Constant.ControlDefault.DateTimeValue.UtcDateTime;

            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(this.FileName);
            string previousFileNameWithoutExtension;
            if (fileNameWithoutExtension.EndsWith("0001"))
            {
                previousFileNameWithoutExtension = fileNameWithoutExtension.Substring(0, 4) + "0999";
            }
            else
            {
                int fileNumber;
                if (Int32.TryParse(fileNameWithoutExtension, out fileNumber) == false)
                {
                    return false;
                }
                previousFileNameWithoutExtension = (fileNumber - 1).ToString("00000000");
            }
            
            string previousFilePath = Path.Combine(folderPath, this.RelativePath, previousFileNameWithoutExtension + Constant.File.JpgFileExtension);
            if (File.Exists(previousFilePath) == false)
            {
                if (fileNameWithoutExtension.EndsWith("0001") == false)
                {
                    return false;
                }

                // check to see if the day rolled as well as the
                // This requires image 999 to be producded just before midnight, which is unlikely but will happen sooner or later.
                DateTimeOffset dateTime = this.DateTimeOffset;
                DateTimeOffset maybePreviousDay = dateTime - Constant.Manufacturer.BushnellHybridVideoLag;
                if (dateTime.Date == maybePreviousDay.Date)
                {
                    return false;
                }

                // retry previous image check as day did roll over
                previousFileNameWithoutExtension = maybePreviousDay.DateTime.ToString("MMdd") + "0999";
                previousFilePath = Path.Combine(folderPath, this.RelativePath, previousFileNameWithoutExtension + Constant.File.JpgFileExtension);
                if (File.Exists(previousFilePath) == false)
                {
                    return false;
                }
            }

            IReadOnlyList<MetadataDirectory> metadataDirectories = ImageMetadataReader.ReadMetadata(previousFilePath);
            ExifSubIfdDirectory exifSubIfd = metadataDirectories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            if (exifSubIfd == null)
            {
                // previous image exists but doesn't have EXIF information
                return false;
            }

            if (exifSubIfd.TryGetDateTime(ExifSubIfdDirectory.TagDateTimeOriginal, out dateTimeOriginal) == false)
            {
                // previous image EXIF doesn't have datetime information
                return false;
            }

            // include an estimate of the trigger to video start lag if manufacturer information is available
            ExifIfd0Directory exifIfd0 = metadataDirectories.OfType<ExifIfd0Directory>().FirstOrDefault();
            if (exifIfd0 != null)
            {
                string make = exifIfd0.GetString(ExifSubIfdDirectory.TagMake);
                if (String.Equals(make, Constant.Manufacturer.Bushnell, StringComparison.OrdinalIgnoreCase))
                {
                    dateTimeOriginal += Constant.Manufacturer.BushnellHybridVideoLag;
                }
            }

            return true;
        }
    }
}
