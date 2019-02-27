using System;

namespace Carnassial.Data
{
    public class DataImportProgress : DataImportExportStatus<DataImportProgress>
    {
        public DataImportProgress(Action<DataImportProgress> onProgressUpdate, TimeSpan progressUpdateInterval)
            : base(onProgressUpdate, progressUpdateInterval)
        {
        }

        public override string GetMessage()
        {
            return String.Format("Read {0} of {1} files...", this.CurrentPosition, this.EndPosition);
        }
    }
}
