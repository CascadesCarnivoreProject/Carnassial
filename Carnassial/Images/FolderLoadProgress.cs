using Carnassial.Database;
using System;
using System.Windows.Media.Imaging;

namespace Carnassial.Images
{
    internal class FolderLoadProgress
    {
        public BitmapSource BitmapSource { get; set; }
        public ImageRow CurrentFile { get; set; }
        public int CurrentFileIndex { get; set; }
        public bool DatabaseInsert { get; set; }
        public bool DisplayBitmap { get; set; }
        public DateTime MostRecentStatusDispatch { get; set; }
        public int RenderWidthBestEstimate { get; set; }
        public int TotalFiles { get; private set; }

        public FolderLoadProgress(int totalFiles, int renderWidthBestEstimate)
        {
            this.BitmapSource = null;
            this.CurrentFile = null;
            this.CurrentFileIndex = 0;
            this.DatabaseInsert = false;
            this.DisplayBitmap = false;
            this.MostRecentStatusDispatch = DateTime.MinValue.ToUniversalTime();
            this.RenderWidthBestEstimate = renderWidthBestEstimate;
            this.TotalFiles = totalFiles;
        }

        public string GetMessage()
        {
            if (this.DatabaseInsert)
            {
                return String.Format("Adding file {0} of {1} ({2}) to database...", this.CurrentFileIndex, this.TotalFiles, this.CurrentFile.FileName);
            }

            if (this.CurrentFileIndex == 0)
            {
                return String.Format("File {0} of {1}...", this.CurrentFileIndex, this.TotalFiles);
            }
            return String.Format("Loading file {0} of {1} ({2})...", this.CurrentFileIndex, this.TotalFiles, this.CurrentFile.FileName);
        }

        public double GetPercentage()
        {
            return 100.0 * this.CurrentFileIndex / (double)this.TotalFiles;
        }
    }
}
