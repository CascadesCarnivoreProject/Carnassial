using System;
using System.Data;
using System.Windows.Media.Imaging;
using Timelapse.Database;

namespace Timelapse.Images
{
    // Because the bgw worker is asynchronous, we have to create a copy of the data at each invocation, 
    // otherwise the values may have changed on the other thread.
    public class ImageQuality
    {
        public WriteableBitmap Bitmap { get; set; }
        public double DarkPixelRatioFound { get; set; }
        public string FileName { get; set; }
        public bool IsColor { get; set; }
        public Nullable<ImageFilter> NewImageQuality { get; set; }
        public ImageFilter OldImageQuality { get; set; }

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
