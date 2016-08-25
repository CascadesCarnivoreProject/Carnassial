using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows.Media.Imaging;
using Timelapse.Images;
using Timelapse.Util;

namespace Timelapse.Database
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

        public string this[string dataLabel]
        {
            get { return this.Row.GetStringField(dataLabel); }
            set { this.Row.SetField(dataLabel, value); }
        }

        public string Date  
        {
            get { return this.Row.GetStringField(Constants.DatabaseColumn.Date); }
            private set { this.Row.SetField(Constants.DatabaseColumn.Date, value); }
        }

        public string FileName
        {
            get { return this.Row.GetStringField(Constants.DatabaseColumn.File); }
            set { this.Row.SetField(Constants.DatabaseColumn.File, value); }
        }

        public ImageFilter ImageQuality
        {
            get { return this.Row.GetEnumField<ImageFilter>(Constants.DatabaseColumn.ImageQuality); }
            set { this.Row.SetField<ImageFilter>(Constants.DatabaseColumn.ImageQuality, value); }
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

        public string Time
        {
            get { return this.Row.GetStringField(Constants.DatabaseColumn.Time); }
            private set { this.Row.SetField(Constants.DatabaseColumn.Time, value); }
        }

        public override ColumnTuplesWithWhere GetColumnTuples()
        {
            List<ColumnTuple> columnTuples = new List<ColumnTuple>();
            columnTuples.Add(new ColumnTuple(Constants.DatabaseColumn.Date, this.Date));
            columnTuples.Add(new ColumnTuple(Constants.DatabaseColumn.File, this.FileName));
            columnTuples.Add(new ColumnTuple(Constants.DatabaseColumn.ImageQuality, this.ImageQuality.ToString()));
            columnTuples.Add(new ColumnTuple(Constants.DatabaseColumn.Folder, this.InitialRootFolderName));
            columnTuples.Add(new ColumnTuple(Constants.DatabaseColumn.RelativePath, this.RelativePath));
            columnTuples.Add(new ColumnTuple(Constants.DatabaseColumn.Time, this.Time));
            return new ColumnTuplesWithWhere(columnTuples, this.ID);
        }

        // Try to create a DateTime from the date/time string of the current image.
        // If we can't, create a date/time of 01-Jan-0001 00:00:00 and return false
        public bool TryGetDateTime(out DateTime dateTime)
        {
            bool result = DateTime.TryParse(this.Date + " " + this.Time, out dateTime);
            if (result == false)
            {
                dateTime = new DateTime(0);
            }
            return result;
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

        public bool IsDisplayable()
        {
            if (this.ImageQuality == ImageFilter.Corrupted || this.ImageQuality == ImageFilter.Missing)
            {
                return false;
            }
            return true;
        }

        // Load defaults to full size image, and to Persistent (as its safer)
        public BitmapSource LoadBitmap(string imageFolderPath)
        {
            return this.LoadBitmap(imageFolderPath, null, ImageDisplayIntent.Persistent);
        }

        // Load defaults to Persistent (as its safer)
        public virtual BitmapSource LoadBitmap(string imageFolderPath, Nullable<int> desiredWidth)
        {
            return this.LoadBitmap(imageFolderPath, desiredWidth, ImageDisplayIntent.Persistent);
        }

        // Load defaults to thumbnail size if we are TransientNavigating, else full size
        public virtual BitmapSource LoadBitmap(string imageFolderPath, ImageDisplayIntent imageExpectedUsage)
        {
            if (imageExpectedUsage == ImageDisplayIntent.TransientNavigating)
            { 
                // TODOSAUL: why load the image at icon size rather than, say, ThumbnailSmall?  Why is this value not in Constants?
                return this.LoadBitmap(imageFolderPath, 32, imageExpectedUsage);
            }
            else
            {
                return this.LoadBitmap(imageFolderPath, null, imageExpectedUsage);
            }
        }

        // Load full form
        public virtual BitmapSource LoadBitmap(string imageFolderPath, Nullable<int> desiredWidth, ImageDisplayIntent displayIntent)
        {
            // If its a transient image, BitmapCacheOption of None as its faster than OnLoad. 
            // TODOSAUL: why isn't the other case, ImageDisplayIntent.TransientNavigating, also treated as transient?
            BitmapCacheOption bitmapCacheOption = (displayIntent == ImageDisplayIntent.TransientLoading) ? BitmapCacheOption.None : BitmapCacheOption.OnLoad;
            string path = this.GetImagePath(imageFolderPath);
            if (!File.Exists(path))
            {
                return Constants.Images.Missing;
            }
            try
            {
                // TODO DISCRETIONARY: Look at CA1001 https://msdn.microsoft.com/en-us/library/ms182172.aspx as a different strategy
                // Scanning through images with BitmapCacheOption.None results in less than 6% CPU in BitmapFrame.Create() and
                // 90% in System.Windows.Application.Run(), suggesting little scope for optimization within Timelapse proper
                // this is significantly faster than BitmapCacheOption.Default
                // However, using BitmapCacheOption.None locks the file as it is being accessed (rather than a memory copy being created when using a cache)
                // This means we cannot do any file operations on it as it will produce an access violation.
                // For now, we use the (slower) form of BitmapCacheOption.OnLoad.
                if (desiredWidth.HasValue == false)
                {
                    BitmapFrame frame = BitmapFrame.Create(new Uri(path), BitmapCreateOptions.None, bitmapCacheOption);
                    frame.Freeze();
                    return frame;
                }

                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.DecodePixelWidth = desiredWidth.Value;
                bitmap.CacheOption = bitmapCacheOption;
                bitmap.UriSource = new Uri(path);
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch (Exception exception)
            {
                Debug.Fail(String.Format("LoadBitmap: Loading of {0} failed.", this.FileName), exception.ToString());
                return Constants.Images.Corrupt;
            }
        }

        public void SetDateAndTime(DateTime dateTime)
        {
            this.Date = DateTimeHandler.ToStandardDateString(dateTime);
            this.Time = DateTimeHandler.ToStandardTimeString(dateTime);
        }

        public void SetDateAndTimeFromFileInfo(string folderPath)
        {
            // populate new image's default date and time
            // Typically the creation time is the time a file was created in the local file system and the last write time when it was
            // last modified ever in any file system.  So, for example, copying an image from a camera's SD card to a computer results
            // in the image file on the computer having a write time which is before its creation time.  Check both and take the lesser 
            // of the two to provide a best effort default.  In most cases it's desirable to see if a more accurate time can be obtained
            // from the image's EXIF metadata.
            FileInfo imageFile = this.GetFileInfo(folderPath);
            DateTime earliestTime = imageFile.CreationTime < imageFile.LastWriteTime ? imageFile.CreationTime : imageFile.LastWriteTime;
            this.SetDateAndTime(earliestTime);
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
                // all the different formats used by cameras, including ambiguities in month/day vs day/month orders.
                DateTime dateImageTaken;
                if (DateTime.TryParse(metadata.DateTaken, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateImageTaken))
                {
                    // get the current date time
                    DateTime currentDateTime;
                    bool result = this.TryGetDateTime(out currentDateTime);
                    // Note that if its not a vaild date, that currentDateTime will now be set to 01-Jan-0001 00:00:00
                    // This will mean that the dateImageTaken date/time will be used instead of the currentDateTime

                    // measure the extent to which the image file time and image taken metadata are consistent
                    bool dateAdjusted = false;
                    if (currentDateTime.Date != dateImageTaken.Date)
                    {
                        this.Date = DateTimeHandler.ToStandardDateString(dateImageTaken);
                        dateAdjusted = true;
                    }

                    bool timeAdjusted = false;
                    if (currentDateTime.TimeOfDay != dateImageTaken.TimeOfDay)
                    {
                        this.Time = DateTimeHandler.ToStandardTimeString(dateImageTaken);
                        timeAdjusted = true;
                    }

                    // At least with several Bushnell Trophy HD and Aggressor models (119677C, 119775C, 119777C) file times are sometimes
                    // indicated an hour before the image taken time during standard time.  This is not known to occur during daylight 
                    // savings time and does not occur consistently during standard time.  It is problematic in the sense time becomes
                    // scrambled, meaning there's no way to detect and correct cases where an image taken time is incorrect because a
                    // daylight-standard transition occurred but the camera hadn't yet been serviced to put its clock on the new time,
                    // and needs to be reported separately as the change of day in images taken just after midnight is not an indicator
                    // of day-month ordering ambiguity in the image taken metadata.
                    bool standardTimeAdjustment = dateImageTaken - currentDateTime == TimeSpan.FromHours(1);

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

            return DateTimeAdjustment.MetadataNotUsed;
        }
    }
}
