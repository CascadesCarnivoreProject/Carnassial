using System.Collections.Generic;

namespace Carnassial.Data
{
    public class FileImportResult
    {
        public List<string> Errors { get; private set; }
        public int FilesAdded { get; set; }
        public int FilesUpdated { get; set; }

        public FileImportResult()
        {
            this.Errors = new List<string>();
            this.FilesAdded = 0;
            this.FilesUpdated = 0;
        }
    }
}
