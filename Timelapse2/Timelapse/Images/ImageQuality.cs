using System;
using System.Windows.Media.Imaging;

namespace Timelapse.Images
{
    // Because the bgw worker is asynchronous, we have to create a copy of the data at each invocation, 
    // otherwise the values may have changed on the other thread.
    public class ImageQuality
    {
        public BitmapFrame Bitmap { get; set; }
        public double DarkPixelRatioFound { get; set; }
        public string FileName { get; set; }
        public int ID { get; set; }
        public bool IsColor { get; set; }
        public string NewImageQuality { get; set; }
        public string OldImageQuality { get; set; }
        public bool Update { get; set; }

        public ImageQuality()
        {
            this.Bitmap = null;
            this.DarkPixelRatioFound = 0;
            this.FileName = String.Empty;
            this.ID = -1;
            this.IsColor = false;
            this.OldImageQuality = String.Empty;
            this.NewImageQuality = String.Empty;
            this.Update = false;
        }
    }
}
