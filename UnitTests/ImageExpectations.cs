using System.IO;
using Timelapse.Database;

namespace Timelapse.UnitTests
{
    internal class ImageExpectations
    {
        public double DarkPixelFraction { get; set; }

        public string FileName { get; set; }

        public ImageQualityFilter Quality { get; set; }

        public bool IsColor { get; set; }

        public ImageProperties GetImageProperties(string folderPath)
        {
            string imageFilePath = Path.Combine(folderPath, this.FileName);
            return new ImageProperties(folderPath, new FileInfo(imageFilePath));
        }
    }
}
