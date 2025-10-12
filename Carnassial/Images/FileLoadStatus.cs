using Carnassial.Data;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Threading;

namespace Carnassial.Images
{
    internal class FileLoadStatus : FileIOComputeTransactionStatus
    {
        private CachedImage? image;

        public ImageRow? CurrentFile { get; set; }
        public int ImageRenderWidthInPixels { get; private set; }
        public UInt64 MostRecentImageUpdate { get; set; }
        public int TotalFiles { get; set; }

        public FileLoadStatus()
        {
            this.CurrentFile = null;
            this.image = null;
            this.MaybeUpdateImageRenderWidth(0);
            this.MostRecentImageUpdate = UInt64.MinValue;
            this.TotalFiles = 0;
        }

        public string GetMessage()
        {
            if (this.CurrentFileIndex == 0)
            {
                return $"File {this.CurrentFileIndex} of {this.TotalFiles}...";
            }
            Debug.Assert(this.CurrentFile != null);
            return $"Loading file {this.CurrentFileIndex} of {this.TotalFiles} ({this.CurrentFile.FileName})...";
        }

        public double GetPercentage()
        {
            return 100.0 * this.CurrentFileIndex / (double)this.TotalFiles;
        }

        public void MaybeUpdateImageRenderWidth(int possibleNewWidthInPixels)
        {
            this.ImageRenderWidthInPixels = Math.Max(Constant.Images.MinimumRenderWidthInPixels, possibleNewWidthInPixels);
        }

        public void SetImage(CachedImage imageToDisplay)
        {
            // a race condition potentially exists between calls to this function and calls to TryDetachImage()
            this.image = imageToDisplay;
        }

        public bool TryDetachImage([NotNullWhen(true)] out CachedImage? image)
        {
            if (this.image == null)
            {
                image = null;
                return false;
            }

            // see remarks in SetImage()
            image = Interlocked.Exchange<CachedImage?>(ref this.image, null);
            return image != null;
        }
    }
}
