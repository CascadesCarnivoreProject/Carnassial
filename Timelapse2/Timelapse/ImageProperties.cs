using System;
using System.IO;
using System.Windows.Media.Imaging;

namespace Timelapse
{
    /// <summary>
    /// A class which tracks progress as images are loaded
    /// </summary>
    public class ImageProperties
    {
        public DateTime DateFileCreation { get; set; }
        public string DateMetadata { get; set; }
        public int DateOrder { get; set; }
        public string FinalDate { get; set; }
        public string FinalTime { get; set; }
        public string Folder { get; set; }
        public int ID { get; set; }
        public ImageQualityFilter ImageQuality { get; set; }
        public string File { get; set; }
        public bool UseMetadata { get; set; }

        public FileInfo GetFileInfo(string rootFolderPath)
        {
            return new FileInfo(this.GetImagePath(rootFolderPath));
        }

        public string GetImagePath(string rootFolderPath)
        {
            if (this.Folder == null)
            {
                return Path.Combine(rootFolderPath, this.File);
            }
            return Path.Combine(rootFolderPath, this.Folder, this.File);
        }

        public BitmapFrame Load(string rootFolderPath)
        {
            return BitmapFrame.Create(new Uri(this.GetImagePath(rootFolderPath)), BitmapCreateOptions.None, BitmapCacheOption.None);
        }
    }
}
