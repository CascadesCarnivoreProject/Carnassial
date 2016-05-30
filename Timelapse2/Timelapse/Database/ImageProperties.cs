using System;
using System.Data;
using System.Globalization;
using System.IO;
using System.Windows.Media.Imaging;
using Timelapse.Util;

namespace Timelapse.Database
{
    /// <summary>
    /// A row in the image database representing a single image.
    /// </summary>
    public class ImageProperties
    {
        public DateTime ImageTaken { get; set; }
        public string Date { get; set; }
        public string FileName { get; set; }
        public long ID { get; set; }
        public ImageQualityFilter ImageQuality { get; set; }
        public string InitialRootFolderName { get; set; }
        public string Time { get; set; }

        public ImageProperties(string imageFolderPath, FileInfo imageFile)
        {
            this.FileName = imageFile.Name;
            this.ImageQuality = ImageQualityFilter.Ok;
            this.InitialRootFolderName = Path.GetFileName(imageFolderPath);
            // TODOTODD: restore support for this
            // GetRelativePath() includes the image's file name; remove that from the relative path as it's stored separately
            // this.RelativeFolderPath = NativeMethods.GetRelativePath(imageFolderPath, imageFile.FullName);
            // this.RelativeFolderPath = Path.GetDirectoryName(this.RelativeFolderPath);

            this.PopulateDateAndTimeFields(imageFile);
        }

        public ImageProperties(DataRow imageRow)
        {
            this.Date = (string)imageRow[Constants.DatabaseColumn.Date];
            this.FileName = (string)imageRow[Constants.DatabaseColumn.File];
            this.ID = (long)imageRow[Constants.Database.ID];
            this.ImageQuality = (ImageQualityFilter)Enum.Parse(typeof(ImageQualityFilter), (string)imageRow[Constants.DatabaseColumn.ImageQuality]);
            this.InitialRootFolderName = (string)imageRow[Constants.DatabaseColumn.Folder];
            this.Time = (string)imageRow[Constants.DatabaseColumn.Time];
        }

        public FileInfo GetFileInfo(string rootFolderPath)
        {
            return new FileInfo(this.GetImagePath(rootFolderPath));
        }

        public string GetImagePath(string rootFolderPath)
        {
            // TODOTODD: restore support for this
            // if (this.RelativeFolderPath == null)
            // {
                return Path.Combine(rootFolderPath, this.FileName);
            // }
            // return Path.Combine(rootFolderPath, this.RelativeFolderPath, this.FileName);
        }

        public bool IsDisplayable()
        {
            if (this.ImageQuality == ImageQualityFilter.Corrupted || this.ImageQuality == ImageQualityFilter.Missing)
            {
                return false;
            }
            return true;
        }

        public BitmapFrame LoadBitmapFrame(string imageFolderPath)
        {
            string path = this.GetImagePath(imageFolderPath);
            if (!File.Exists(path))
            {
                return Constants.Images.Missing;
            }
            try
            {
                // scanning through images with BitmapCacheOption.None results in less than 6% CPU in BitmapFrame.Create() and
                // 90% in System.Windows.Application.Run(), suggesting little scope for optimization within Timelapse proper
                // this is significantly faster than BitmapCacheOption.Default
                return BitmapFrame.Create(new Uri(path), BitmapCreateOptions.None, BitmapCacheOption.None);
            }
            catch
            {
                return Constants.Images.Corrupt;
            }
        }

        public WriteableBitmap LoadWriteableBitmap(string imageFolderPath)
        {
            return new WriteableBitmap(this.LoadBitmapFrame(imageFolderPath));
        }

        private void PopulateDateAndTimeFields(FileInfo fileInfo)
        {
            // Typically the creation time is the time a file was created in the local file system and the last write time when it was
            // last modified ever in any file system.  So, for example, copying an image from a camera's SD card to a computer results
            // in the image file on the computer having a write time which is before its creation time.  Check both and take the lesser 
            // of the two.
            DateTime earliestTime = fileInfo.CreationTime < fileInfo.LastWriteTime ? fileInfo.CreationTime : fileInfo.LastWriteTime;
            this.ImageTaken = earliestTime;
            this.Date = DateTimeHandler.StandardDateString(this.ImageTaken);
            this.Time = DateTimeHandler.StandardTimeString(this.ImageTaken);
        }

        public DateTimeAdjustment TryUseImageTaken(BitmapMetadata metadata)
        {
            if (metadata == null)
            {
                return DateTimeAdjustment.MetadataNotUsed;
            }

            if (String.IsNullOrWhiteSpace(metadata.DateTaken) == false)
            {
                // try to get the date from the metadata
                DateTime dateImageTaken;
                // all the different formats used by cameras, including ambiguities in month/day vs day/month orders.
                if (DateTime.TryParse(metadata.DateTaken, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateImageTaken))
                {
                    // measure the extent to which the image file time and image taken metadata are consistent
                    bool dateAdjusted = false;
                    if (this.ImageTaken.Date != dateImageTaken.Date)
                    {
                        this.Date = DateTimeHandler.StandardDateString(dateImageTaken);
                        dateAdjusted = true;
                    }

                    bool timeAdjusted = false;
                    if (this.ImageTaken.TimeOfDay != dateImageTaken.TimeOfDay)
                    {
                        this.Time = DateTimeHandler.StandardTimeString(dateImageTaken);
                        timeAdjusted = true;
                    }

                    // At least with several Bushnell Trophy HD and Aggressor models (119677C, 119775C, 119777C) file times are sometimes
                    // indicated an hour before the image taken time during standard time.  This is not known to occur during daylight 
                    // savings time and does not occur consistently during standard time.  It is problematic in the sense time becomes
                    // scrambled, meaning there's no way to detect and correct cases where an image taken time is incorrect because a
                    // daylight-standard transition occurred but the camera hadn't yet been serviced to put its clock on the new time,
                    // and needs to be reported separately as the change of day in images taken just after midnight is not an indicator
                    // of day-month ordering ambiguity in the image taken metadata.
                    bool standardTimeAdjustment = dateImageTaken - this.ImageTaken == TimeSpan.FromHours(1);

                    // snap to metadata time and return the extent of the time adjustment
                    this.ImageTaken = dateImageTaken;
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

            return DateTimeAdjustment.MetadataNotUsed;
        }
    }
}
