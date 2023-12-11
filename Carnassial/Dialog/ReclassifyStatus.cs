using Carnassial.Data;
using Carnassial.Images;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace Carnassial.Dialog
{
    public class ReclassifyStatus : FileIOComputeTransactionStatus
    {
        private CachedImage? image;

        public ImageRow? File { get; set; }
        public ImageProperties? ImageProperties { get; set; }
        public UInt64 MostRecentImageUpdate { get; set; }

        public ReclassifyStatus()
        {
            this.File = null;
            this.image = null;
            this.ImageProperties = null;
            this.MostRecentImageUpdate = 0;
        }

        public void SetImage(CachedImage imageToDisplay)
        {
            this.image = imageToDisplay;
        }

        public bool TryDetachImage([NotNullWhen(true)] out CachedImage? image)
        {
            if (this.image == null)
            {
                image = null;
                return false;
            }

            // see remarks in FileLoadStatus.TryDetachImage()
            image = Interlocked.Exchange<CachedImage?>(ref this.image, null);
            return image != null;
        }
    }
}
