using Carnassial.Images;

namespace Carnassial.Data
{
    // a shim for distinguishing between image files which could not be found and those which could not be loaded
    // This class borders on unnecessary. In general, a loadable image produces a MemoryImage (possibly with DecompressionError = true,
    // implying corruption) and an image which cannot be loaded can be indicated by a null pointer. However, a narrow range of cases
    // occurs where an image file exists on disk but is so corrupted or truncated no decoding is possible. MemoryImage { DecompressionError
    // = true } also applies to these cases and the special casing needed to produce an empty MemoryImage with the flag set is arguably
    // less complex than imposing the CachedImage class.
    public class CachedImage
    {
        public bool FileNoLongerAvailable { get; set; }
        public MemoryImage? Image { get; private set; }
        public bool ImageNotDecodable { get; set; }

        public CachedImage()
        {
            this.FileNoLongerAvailable = false;
            this.Image = null;
            this.ImageNotDecodable = false;
        }

        public CachedImage(MemoryImage image)
            : this()
        {
            this.Image = image;
        }
    }
}
