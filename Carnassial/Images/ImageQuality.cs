using Carnassial.Data;
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

        public ImageQuality(ImageRow file)
        {
            this.DarkPixelRatioFound = 0;
            this.FileName = file.FileName;
            this.Image = null;
            this.IsColor = false;
            this.OldImageQuality = file.ImageQuality;
            this.NewImageQuality = null;
        }
    }
}
