using Carnassial.Native;
using System;
using System.Threading.Tasks;

namespace Carnassial.Data
{
    public class VideoRow : ImageRow
    {
        public VideoRow(string fileName, string relativePath, FileTable table)
            : base(fileName, relativePath, table)
        {
            this.ImageQuality = FileSelection.Video;
        }

        public override bool IsVideo
        {
            get { return true; }
        }

        #pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async override Task<MemoryImage> LoadAsync(string baseFolderPath, Nullable<int> expectedDisplayWidth)
        {
            throw new NotSupportedException();
        }
        #pragma warning restore CS1998
    }
}