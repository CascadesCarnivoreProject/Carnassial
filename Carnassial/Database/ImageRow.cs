using Carnassial.Util;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Exif.Makernotes;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using Directory = System.IO.Directory;
using MetadataDirectory = MetadataExtractor.Directory;

namespace Carnassial.Database
{
    /// <summary>
    /// A row in the file database representing a single image.
    /// </summary>
    public class ImageRow : DataRowBackedObject
    {
        public ImageRow(DataRow row)
            : base(row)
        {
        }

        public DateTime DateTime
        {
            get { return this.Row.GetDateTimeField(Constant.DatabaseColumn.DateTime); }
            private set { this.Row.SetField(Constant.DatabaseColumn.DateTime, value); }
        }

        public bool DeleteFlag
        {
            get { return this.Row.GetBooleanField(Constant.DatabaseColumn.DeleteFlag); }
            set { this.Row.SetField(Constant.DatabaseColumn.DeleteFlag, value); }
        }

        public string FileName
        {
            get { return this.Row.GetStringField(Constant.DatabaseColumn.File); }
            set { this.Row.SetField(Constant.DatabaseColumn.File, value); }
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
                        throw new ArgumentOutOfRangeException("value", String.Format("{0} is not an ImageQuality.  ImageQuality must be one of CorruptFile, Dark, FileNoLongerAvailable, or Ok.", value));
                }
            }
        }

        public virtual bool IsVideo
        {
            get { return false; }
        }

        public string RelativePath
        {
            get { return this.Row.GetStringField(Constant.DatabaseColumn.RelativePath); }
            set { this.Row.SetField(Constant.DatabaseColumn.RelativePath, value); }
        }

        public TimeSpan UtcOffset
        {
            get { return this.Row.GetUtcOffsetField(Constant.DatabaseColumn.UtcOffset); }
            private set { this.Row.SetUtcOffsetField(Constant.DatabaseColumn.UtcOffset, value); }
        }

        public override ColumnTuplesWithWhere GetColumnTuples()
        {
            ColumnTuplesWithWhere columnTuples = this.GetDateTimeColumnTuples();
            columnTuples.Columns.Add(new ColumnTuple(Constant.DatabaseColumn.File, this.FileName));
            columnTuples.Columns.Add(new ColumnTuple(Constant.DatabaseColumn.ImageQuality, this.ImageQuality.ToString()));
            columnTuples.Columns.Add(new ColumnTuple(Constant.DatabaseColumn.RelativePath, this.RelativePath));
            columnTuples.Columns.Add(new ColumnTuple(Constant.DatabaseColumn.DeleteFlag, this.DeleteFlag));
            return columnTuples;
        }

        public ColumnTuplesWithWhere GetDateTimeColumnTuples()
        {
            List<ColumnTuple> columnTuples = new List<ColumnTuple>(2);
            columnTuples.Add(new ColumnTuple(Constant.DatabaseColumn.DateTime, this.DateTime));
            columnTuples.Add(new ColumnTuple(Constant.DatabaseColumn.UtcOffset, this.UtcOffset));

            long id = this.ID;
            if (id != Constant.Database.InvalidID)
            {
                return new ColumnTuplesWithWhere(columnTuples, this.ID);
            }
            return new ColumnTuplesWithWhere(columnTuples);
        }

        public DateTimeOffset GetDateTime()
        {
            return DateTimeHandler.FromDatabaseDateTimeOffset(this.DateTime, this.UtcOffset);
        }

        public string GetDisplayDateTime()
        {
            return DateTimeHandler.ToDisplayDateTimeString(this.GetDateTime());
        }

        public FileInfo GetFileInfo(string rootFolderPath)
        {
            return new FileInfo(this.GetFilePath(rootFolderPath));
        }

        public string GetFilePath(string rootFolderPath)
        {
            // see RelativePath remarks in constructor
            if (String.IsNullOrEmpty(this.RelativePath))
            {
                return Path.Combine(rootFolderPath, this.FileName);
            }
            return Path.Combine(rootFolderPath, this.RelativePath, this.FileName);
        }

        public string GetValueDatabaseString(string dataLabel)
        {
            switch (dataLabel)
            {
                case Constant.DatabaseColumn.DateTime:
                    return DateTimeHandler.ToDatabaseDateTimeString(this.DateTime);
                default:
                    return this.GetValueDisplayString(dataLabel);
            }
        }

        public string GetValueDisplayString(string dataLabel)
        {
            switch (dataLabel)
            {
                case Constant.DatabaseColumn.DateTime:
                    return this.GetDisplayDateTime();
                case Constant.DatabaseColumn.UtcOffset:
                    return DateTimeHandler.ToDatabaseUtcOffsetString(this.UtcOffset);
                case Constant.DatabaseColumn.ImageQuality:
                    return this.ImageQuality.ToString();
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

        public BitmapSource LoadBitmap(string baseFolderPath)
        {
            return this.LoadBitmap(baseFolderPath, null);
        }

        public virtual BitmapSource LoadBitmap(string baseFolderPath, Nullable<int> desiredWidth)
        {
            string path = this.GetFilePath(baseFolderPath);
            if (!File.Exists(path))
            {
                return Constant.Images.FileNoLongerAvailable;
            }

            try
            {
                using (FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.SequentialScan))
                {
                    // All of WPF's image loading assumes, problematically, the file loaded will never need to be deleted or moved on disk until such time as
                    // as all WPF references to it have been garbage collected.  This is not the case for many applications including, in Carnassial, when the
                    // user soft deletes the current file or all files marked for deletion.  Disposing a BitmapImage's StreamSource in principle avoids the 
                    // problem but either WPF or the semi-asynchronous nature of the filesystem prevents success in practice.  The simplest workaround's to give
                    // WPF only a MemoryStream and dispose the FileStream promptly so WPF never gets a file handle to hold on to and the risk of file system 
                    // races is mitigated.
                    byte[] fileContent = new byte[fileStream.Length];
                    fileStream.Read(fileContent, 0, fileContent.Length);

                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.None;
                    if (desiredWidth.HasValue)
                    {
                        bitmap.DecodePixelWidth = desiredWidth.Value;
                    }
                    bitmap.StreamSource = new MemoryStream(fileContent);
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }
            }
            catch (Exception exception)
            {
                Debug.Fail(String.Format("LoadBitmap: Loading of {0} failed.", this.FileName), exception.ToString());
                return Constant.Images.CorruptFile;
            }
        }

        public void SetDateTimeOffset(DateTimeOffset dateTime)
        {
            this.DateTime = dateTime.UtcDateTime;
            this.UtcOffset = dateTime.Offset;
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
            this.SetDateTimeOffset(new DateTimeOffset(earliestTimeLocal));
        }

        public void SetValueFromDatabaseString(string dataLabel, string value)
        {
            switch (dataLabel)
            {
                case Constant.DatabaseColumn.DateTime:
                    this.DateTime = DateTimeHandler.ParseDatabaseDateTimeString(value);
                    break;
                case Constant.DatabaseColumn.UtcOffset:
                    this.UtcOffset = DateTimeHandler.ParseDatabaseUtcOffsetString(value);
                    break;
                case Constant.DatabaseColumn.ImageQuality:
                    this.ImageQuality = (FileSelection)Enum.Parse(typeof(FileSelection), value);
                    break;
                default:
                    this.Row.SetField(dataLabel, value);
                    break;
            }
        }

        /// <summary>
        /// Move corresponding file to the deleted files folder.
        /// </summary>
        public bool TryMoveFileToDeletedFilesFolder(string folderPath)
        {
            string sourceFilePath = this.GetFilePath(folderPath);
            if (!File.Exists(sourceFilePath))
            {
                return false;  // If there is no source file, its a missing file so we can't back it up
            }

            // Create a new target folder, if necessary.
            string deletedFilesFolderPath = Path.Combine(folderPath, Constant.File.DeletedFilesFolder);
            if (!Directory.Exists(deletedFilesFolderPath))
            {
                Directory.CreateDirectory(deletedFilesFolderPath);
            }

            // Move the file to the backup location.           
            string destinationFilePath = Path.Combine(deletedFilesFolderPath, this.FileName);
            if (File.Exists(destinationFilePath))
            {
                try
                {
                    // Becaue move doesn't allow overwriting, delete the destination file if it already exists.
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
                return true;
            }
            catch (IOException exception)
            {
                Debug.Fail(exception.ToString());
                return false;
            }
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
                DateTimeOffset dateTime = this.GetDateTime();
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

            IList<MetadataDirectory> metadataDirectories = ImageMetadataReader.ReadMetadata(previousFilePath);
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

        public DateTimeAdjustment TryReadDateTimeOriginalFromMetadata(string folderPath, TimeZoneInfo imageSetTimeZone)
        {
            string filePath = this.GetFilePath(folderPath);
            IList<MetadataDirectory> metadataDirectories = ImageMetadataReader.ReadMetadata(filePath);
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
                ReconyxMakernoteDirectory reconyxMakernote = metadataDirectories.OfType<ReconyxMakernoteDirectory>().FirstOrDefault();
                if ((reconyxMakernote == null) || (reconyxMakernote.TryGetDateTime(ReconyxMakernoteDirectory.TagDateTimeOriginal, out dateTimeOriginal) == false))
                {
                    return DateTimeAdjustment.None;
                }
            }

            // measure the extent to which the file's current date time offset and image taken metadata are consistent
            DateTimeOffset currentDateTime = this.GetDateTime();
            DateTimeOffset exifDateTime = DateTimeHandler.CreateDateTimeOffset(dateTimeOriginal, imageSetTimeZone);
            bool dateAdjusted = currentDateTime.Date != exifDateTime.Date;
            bool timeAdjusted = currentDateTime.TimeOfDay != exifDateTime.TimeOfDay;
            bool offsetAdjusted = currentDateTime.Offset != exifDateTime.Offset;
            if (dateAdjusted || timeAdjusted || offsetAdjusted)
            {
                this.SetDateTimeOffset(exifDateTime);
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
    }
}
