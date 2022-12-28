using Carnassial.Data;
using System;

namespace Carnassial.Images
{
    public class FileLoad : IDisposable
    {
        private bool disposed;

        public static readonly FileLoad NoLoad = new((string?)null);

        public ImageRow? File { get; set; }
        public string? FileName { get; private set; }
        public JpegImage? Jpeg { get; set; }
        public MetadataReadResults MetadataReadResult { get; set; }

        public FileLoad(ImageRow file)
        {
            this.disposed = false;
            this.File = file;
            this.FileName = file.FileName;
            this.Jpeg = null;
            this.MetadataReadResult = MetadataReadResults.None;
        }

        public FileLoad(string? fileName)
        {
            this.disposed = false;
            this.File = null;
            this.FileName = fileName;
            this.Jpeg = null;
            this.MetadataReadResult = MetadataReadResults.None;
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

            if (disposing)
            {
                this.Jpeg?.Dispose();
            }
            this.disposed = true;
        }
    }
}
