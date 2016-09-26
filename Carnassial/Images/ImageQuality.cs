using Carnassial.Database;
using System;
using System.Windows.Media.Imaging;

namespace Carnassial.Images
{
    public class ImageQuality
    {
        public WriteableBitmap Bitmap { get; set; }
        public double DarkPixelRatioFound { get; set; }
        public string FileName { get; set; }
        public bool IsColor { get; set; }
        public Nullable<ImageSelection> NewImageQuality { get; set; }
        public ImageSelection OldImageQuality { get; set; }

        public ImageQuality(ImageRow image)
        {
            this.Bitmap = null;
            this.DarkPixelRatioFound = 0;
            this.FileName = image.FileName;
            this.IsColor = false;
            this.OldImageQuality = image.ImageQuality;
            this.NewImageQuality = null;
        }
    }
}
