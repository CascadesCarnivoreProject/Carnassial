using System;
using System.Data;
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
        public DateTime DateFileCreation { get; set; }
        public string DateMetadata { get; set; }
        public string Date { get; set; }
        public string FileName { get; set; }
        public long ID { get; set; }
        public ImageQualityFilter ImageQuality { get; set; }
        public string RelativeFolderPath { get; set; }
        public string Time { get; set; }
        public bool UseMetadata { get; set; }

        public ImageProperties(string imageFolderPath, FileInfo imageFile)
        {
            this.FileName = imageFile.Name;
            this.RelativeFolderPath = NativeMethods.GetRelativePath(imageFolderPath, imageFile.FullName);
            this.RelativeFolderPath = Path.GetDirectoryName(this.RelativeFolderPath);
        }

        public ImageProperties(DataRow imageRow)
        {
            this.Date = (string)imageRow[Constants.DatabaseColumn.Date];
            this.FileName = (string)imageRow[Constants.DatabaseColumn.File];
            this.ID = (long)imageRow[Constants.Database.ID];
            this.ImageQuality = (ImageQualityFilter)Enum.Parse(typeof(ImageQualityFilter), (string)imageRow[Constants.DatabaseColumn.ImageQuality]);
            this.RelativeFolderPath = (string)imageRow[Constants.DatabaseColumn.Folder];
            this.Time = (string)imageRow[Constants.DatabaseColumn.Time];
        }

        public FileInfo GetFileInfo(string rootFolderPath)
        {
            return new FileInfo(this.GetImagePath(rootFolderPath));
        }

        public string GetImagePath(string rootFolderPath)
        {
            if (this.RelativeFolderPath == null)
            {
                return Path.Combine(rootFolderPath, this.FileName);
            }
            return Path.Combine(rootFolderPath, this.RelativeFolderPath, this.FileName);
        }

        public bool IsDisplayable()
        {
            if (this.ImageQuality == ImageQualityFilter.Corrupted || this.ImageQuality == ImageQualityFilter.Missing)
            {
                return false;
            }
            return true;
        }

        public BitmapFrame LoadImage(string imageFolderPath)
        {
            string path = this.GetImagePath(imageFolderPath);
            if (!File.Exists(path))
            {
                return BitmapFrame.Create(new Uri("pack://application:,,/Resources/missing.jpg"));
            }
            try
            {
                return BitmapFrame.Create(new Uri(path), BitmapCreateOptions.None, BitmapCacheOption.None);
            }
            catch
            {
                return BitmapFrame.Create(new Uri("pack://application:,,/Resources/corrupted.jpg"));
            }
        }
    }
}
