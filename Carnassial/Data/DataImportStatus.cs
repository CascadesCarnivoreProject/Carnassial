using System;

namespace Carnassial.Data
{
    public class DataImportStatus : DataImportExportStatus<DataImportStatus>
    {
        public DataImportStatus(Action<DataImportStatus> onProgressUpdate, TimeSpan progressUpdateInterval)
            : base(onProgressUpdate, progressUpdateInterval)
        {
        }

        public override string GetMessage()
        {
            return String.Format("Read {0} of {1} files...", this.CurrentPosition, this.EndPosition);
        }

        protected override void ReportProgress()
        {
            this.Progress.Report(this);
        }
    }
}
