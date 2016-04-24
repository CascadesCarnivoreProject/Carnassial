using System;
using System.Data;
using System.Windows.Media.Imaging;
using Timelapse.Database;

namespace Timelapse.Images
{
    // Because the bgw worker is asynchronous, we have to create a copy of the data at each invocation, 
    // otherwise the values may have changed on the other thread.
    public class ImageQuality : ImageProperties
    {
        public WriteableBitmap Bitmap { get; set; }
        public double DarkPixelRatioFound { get; set; }
        public bool IsColor { get; set; }
        public string NewImageQuality { get; set; }
        public string OldImageQuality { get; set; }
        public bool Update { get; set; }

        public ImageQuality(DataRow imageRow)
            : base(imageRow)
        {
            this.Bitmap = null;
            this.DarkPixelRatioFound = 0;
            this.IsColor = false;
            this.OldImageQuality = (string)imageRow[Constants.DatabaseColumn.ImageQuality];
            this.NewImageQuality = String.Empty;
            this.Update = false;
        }
    }
}
