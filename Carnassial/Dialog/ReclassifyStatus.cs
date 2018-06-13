using Carnassial.Data;
using Carnassial.Images;
using Carnassial.Native;
using System;

namespace Carnassial.Dialog
{
    public class ReclassifyStatus : FileIOComputeTransactionStatus
    {
        private bool disposed;

        public FileClassification ClassificationToDisplay { get; set; }
        public ImageRow File { get; set; }
        public MemoryImage Image { get; set; }
        public ImageProperties ImageProperties { get; set; }
        public UInt64 MostRecentImageUpdate { get; set; }
        public UInt64 MostRecentStatusUpdate { get; set; }
        public bool ProgressUpdateInProgress { get; set; }

        public ReclassifyStatus()
        {
            this.ClassificationToDisplay = default(FileClassification);
            this.File = null;
            this.Image = null;
            this.ImageProperties = null;
            this.MostRecentImageUpdate = 0;
            this.MostRecentStatusUpdate = 0;
            this.ProgressUpdateInProgress = false;
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing && (this.Image != null))
            {
                this.Image.Dispose();
            }
            this.disposed = true;
        }
    }
}
