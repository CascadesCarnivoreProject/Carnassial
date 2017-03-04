using Carnassial.Database;
using Carnassial.Native;
using System;

namespace Carnassial.Images
{
    public class ImageQuality
    {
        public MemoryImage Image { get; set; }
        public double DarkPixelRatioFound { get; set; }
        public string FileName { get; set; }
        public bool IsColor { get; set; }
        public Nullable<FileSelection> NewImageQuality { get; set; }
        public FileSelection OldImageQuality { get; set; }

        public ImageQuality(ImageRow image)
        {
            this.DarkPixelRatioFound = 0;
            this.FileName = image.FileName;
            this.Image = null;
            this.IsColor = false;
            this.OldImageQuality = image.ImageQuality;
            this.NewImageQuality = null;
        }
    }
}
