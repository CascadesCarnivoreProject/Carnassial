using Carnassial.Database;
using System;
using System.Windows.Media.Imaging;

namespace Carnassial.Images
{
    public class ImageQuality
    {
        public WriteableBitmap Bitmap { get; set; }
        public double DarkPixelRatioFound { get; set; }
        public int FileIndex { get; set; }
        public string FileName { get; set; }
        public bool IsColor { get; set; }
        public Nullable<FileSelection> NewImageQuality { get; set; }
        public FileSelection OldImageQuality { get; set; }

        public ImageQuality(ImageRow image)
        {
            this.Bitmap = null;
            this.DarkPixelRatioFound = 0;
            this.FileIndex = Constant.Database.InvalidRow;
            this.FileName = image.FileName;
            this.IsColor = false;
            this.OldImageQuality = image.ImageQuality;
            this.NewImageQuality = null;
        }
    }
}
