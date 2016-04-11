using System;

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
        public string Name { get; set; }
        public bool UseMetadata { get; set; }

        public ImageProperties()
        {
        }
    }
}
