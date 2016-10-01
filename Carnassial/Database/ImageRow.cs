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
    /// A row in the image database representing a single image.
    /// </summary>
    public class ImageRow : DataRowBackedObject
    {
        public ImageRow(DataRow row)
            : base(row)
        {
        }

        public DateTime DateTime
        {
            get { return this.Row.GetDateTimeField(Constants.DatabaseColumn.DateTime); }
            private set { this.Row.SetField(Constants.DatabaseColumn.DateTime, value); }
        }

        public bool DeleteFlag
        {
            get { return this.Row.GetBooleanField(Constants.DatabaseColumn.DeleteFlag); }
            set { this.Row.SetField(Constants.DatabaseColumn.DeleteFlag, value); }
        }

        public string FileName
        {
            get { return this.Row.GetStringField(Constants.DatabaseColumn.File); }
            set { this.Row.SetField(Constants.DatabaseColumn.File, value); }
        }

        public ImageSelection ImageQuality
        {
            get
            {
                return this.Row.GetEnumField<ImageSelection>(Constants.DatabaseColumn.ImageQuality);
            }
            set
            {
                switch (value)
                {
                    case ImageSelection.CorruptFile:
                    case ImageSelection.Dark:
                    case ImageSelection.FileNoLongerAvailable:
                    case ImageSelection.Ok:
                        this.Row.SetField<ImageSelection>(Constants.DatabaseColumn.ImageQuality, value);
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

        public string InitialRootFolderName
        {
            get { return this.Row.GetStringField(Constants.DatabaseColumn.Folder); }
            set { this.Row.SetField(Constants.DatabaseColumn.Folder, value); }
        }

        public string RelativePath
        {
            get { return this.Row.GetStringField(Constants.DatabaseColumn.RelativePath); }
            set { this.Row.SetField(Constants.DatabaseColumn.RelativePath, value); }
        }

        public TimeSpan UtcOffset
        {
            get { return this.Row.GetUtcOffsetField(Constants.DatabaseColumn.UtcOffset); }
            private set { this.Row.SetUtcOffsetField(Constants.DatabaseColumn.UtcOffset, value); }
        }

        public override ColumnTuplesWithWhere GetColumnTuples()
        {
            ColumnTuplesWithWhere columnTuples = this.GetDateTimeColumnTuples();
            columnTuples.Columns.Add(new ColumnTuple(Constants.DatabaseColumn.File, this.FileName));
            columnTuples.Columns.Add(new ColumnTuple(Constants.DatabaseColumn.ImageQuality, this.ImageQuality.ToString()));
            columnTuples.Columns.Add(new ColumnTuple(Constants.DatabaseColumn.Folder, this.InitialRootFolderName));
            columnTuples.Columns.Add(new ColumnTuple(Constants.DatabaseColumn.RelativePath, this.RelativePath));
            return columnTuples;
        }

        public ColumnTuplesWithWhere GetDateTimeColumnTuples()
        {
            List<ColumnTuple> columnTuples = new List<ColumnTuple>(3);
            columnTuples.Add(new ColumnTuple(Constants.DatabaseColumn.DateTime, this.DateTime));
            columnTuples.Add(new ColumnTuple(Constants.DatabaseColumn.UtcOffset, this.UtcOffset));
            return new ColumnTuplesWithWhere(columnTuples, this.ID);
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
            return new FileInfo(this.GetImagePath(rootFolderPath));
        }

        public string GetImagePath(string rootFolderPath)
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
                case Constants.DatabaseColumn.DateTime:
                    return DateTimeHandler.ToDatabaseDateTimeString(this.DateTime);
                default:
                    return this.GetValueDisplayString(dataLabel);
            }
        }

        public string GetValueDisplayString(string dataLabel)
        {
            switch (dataLabel)
            {
                case Constants.DatabaseColumn.DateTime:
                    return this.GetDisplayDateTime();
                case Constants.DatabaseColumn.UtcOffset:
                    return DateTimeHandler.ToDatabaseUtcOffsetString(this.UtcOffset);
                case Constants.DatabaseColumn.ImageQuality:
                    return this.ImageQuality.ToString();
                default:
                    return this.Row.GetStringField(dataLabel);
            }
        }

        public bool IsDisplayable()
        {
            if (this.ImageQuality == ImageSelection.CorruptFile || this.ImageQuality == ImageSelection.FileNoLongerAvailable)
            {
                return false;
            }
            return true;
        }

        public BitmapSource LoadBitmap(string imageFolderPath)
        {
            return this.LoadBitmap(imageFolderPath, null);
        }

        public virtual BitmapSource LoadBitmap(string imageFolderPath, Nullable<int> desiredWidth)
        {
            string path = this.GetImagePath(imageFolderPath);
            if (!File.Exists(path))
            {
                return Constants.Images.FileNoLongerAvailable;
            }

            try
            {
                using (FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.SequentialScan))
                {
                    // All of WPF's image loading assumes, problematically, the file loaded will never need to be deleted or moved on disk until such time as
                    // as all WPF references to it have been garbage collected.  This is not the case for many applications including, in Carnassial, when the
                    // user soft deletes the current image or all images marked for deletion.  Disposing a BitmapImage's StreamSource in principle avoids the 
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
                return Constants.Images.CorruptFile;
            }
        }

        public void SetDateAndTime(DateTimeOffset dateTime)
        {
            this.DateTime = dateTime.UtcDateTime;
            this.UtcOffset = dateTime.Offset;
        }

        public void SetDateAndTimeFromFileInfo(string folderPath, TimeZoneInfo imageSetTimeZone)
        {
            // populate new image's default date and time
            // Typically the creation time is the time a file was created in the local file system and the last write time when it was
            // last modified ever in any file system.  So, for example, copying an image from a camera's SD card to a computer results
            // in the image file on the computer having a write time which is before its creation time.  Check both and take the lesser 
            // of the two to provide a best effort default.  In most cases it's desirable to see if a more accurate time can be obtained
            // from the image's EXIF metadata.
            FileInfo imageFile = this.GetFileInfo(folderPath);
            DateTime earliestTimeLocal = imageFile.CreationTime < imageFile.LastWriteTime ? imageFile.CreationTime : imageFile.LastWriteTime;
            this.SetDateAndTime(new DateTimeOffset(earliestTimeLocal));
        }

        public void SetValueFromDatabaseString(string dataLabel, string value)
        {
            switch (dataLabel)
            {
                case Constants.DatabaseColumn.DateTime:
                    this.DateTime = DateTimeHandler.ParseDatabaseDateTimeString(value);
                    break;
                case Constants.DatabaseColumn.UtcOffset:
                    this.UtcOffset = DateTimeHandler.ParseDatabaseUtcOffsetString(value);
                    break;
                case Constants.DatabaseColumn.ImageQuality:
                    this.ImageQuality = (ImageSelection)Enum.Parse(typeof(ImageSelection), value);
                    break;
                default:
                    this.Row.SetField(dataLabel, value);
                    break;
            }
        }

        /// <summary>
        /// Move the file to the deleted images folder.
        /// </summary>
        public bool TryMoveFileToDeletedImagesFolder(string folderPath)
        {
            string sourceFilePath = this.GetImagePath(folderPath);
            if (!File.Exists(sourceFilePath))
            {
                return false;  // If there is no source file, its a missing file so we can't back it up
            }

            // Create a new target folder, if necessary.
            string deletedImagesFolderPath = Path.Combine(folderPath, Constants.File.DeletedFilesFolder);
            if (!Directory.Exists(deletedImagesFolderPath))
            {
                Directory.CreateDirectory(deletedImagesFolderPath);
            }

            // Move the image file to the backup location.           
            string destinationFilePath = Path.Combine(deletedImagesFolderPath, this.FileName);
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

        public DateTimeAdjustment TryReadDateTimeOriginalFromMetadata(string folderPath, TimeZoneInfo imageSetTimeZone)
        {
            IList<MetadataDirectory> metadataDirectories = ImageMetadataReader.ReadMetadata(this.GetImagePath(folderPath));
            ExifSubIfdDirectory exifSubIfd = metadataDirectories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            if (exifSubIfd == null)
            {
                return DateTimeAdjustment.MetadataNotUsed;
            }
            DateTime dateTimeOriginal;
            if (exifSubIfd.TryGetDateTime(ExifSubIfdDirectory.TagDateTimeOriginal, out dateTimeOriginal) == false)
            {
                ReconyxMakernoteDirectory reconyxMakernote = metadataDirectories.OfType<ReconyxMakernoteDirectory>().FirstOrDefault();
                if ((reconyxMakernote == null) || (reconyxMakernote.TryGetDateTime(ReconyxMakernoteDirectory.TagDateTimeOriginal, out dateTimeOriginal) == false))
                {
                    return DateTimeAdjustment.MetadataNotUsed;
                }
            }
            DateTimeOffset exifDateTime = DateTimeHandler.CreateDateTimeOffset(dateTimeOriginal, imageSetTimeZone);

            // measure the extent to which the image file time and image taken metadata are consistent
            DateTimeOffset currentDateTime = this.GetDateTime();
            bool dateAdjusted = currentDateTime.Date != exifDateTime.Date;
            bool timeAdjusted = currentDateTime.TimeOfDay != exifDateTime.TimeOfDay;
            if (dateAdjusted || timeAdjusted)
            {
                this.SetDateAndTime(exifDateTime);
            }

            // At least with several Bushnell Trophy HD and Aggressor models (119677C, 119775C, 119777C) file times are sometimes
            // indicated an hour before the image taken time during standard time.  This is not known to occur during daylight 
            // savings time and does not occur consistently during standard time.  It is problematic in the sense time becomes
            // scrambled, meaning there's no way to detect and correct cases where an image taken time is incorrect because a
            // daylight-standard transition occurred but the camera hadn't yet been serviced to put its clock on the new time,
            // and needs to be reported separately as the change of day in images taken just after midnight is not an indicator
            // of day-month ordering ambiguity in the image taken metadata.
            bool standardTimeAdjustment = exifDateTime - currentDateTime == TimeSpan.FromHours(1);

            // snap to metadata time and return the extent of the time adjustment
            if (standardTimeAdjustment)
            {
                return DateTimeAdjustment.MetadataDateAndTimeOneHourLater;
            }
            if (dateAdjusted && timeAdjusted)
            {
                return DateTimeAdjustment.MetadataDateAndTimeUsed;
            }
            if (dateAdjusted)
            {
                return DateTimeAdjustment.MetadataDateUsed;
            }
            if (timeAdjusted)
            {
                return DateTimeAdjustment.MetadataTimeUsed;
            }
            return DateTimeAdjustment.SameFileAndMetadataTime;
        }
    }
}
