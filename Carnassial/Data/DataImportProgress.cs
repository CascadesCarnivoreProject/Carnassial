using System;
using System.Globalization;

namespace Carnassial.Data
{
    public class DataImportProgress : DataImportExportProgress<DataImportProgress>
    {
        public DataImportProgress(Action<DataImportProgress> onProgressUpdate, TimeSpan progressUpdateInterval)
            : base(onProgressUpdate, progressUpdateInterval)
        {
        }

        public override string GetMessage()
        {
            return String.Format(CultureInfo.CurrentCulture, "Read {0} of {1} files...", this.CurrentPosition, this.EndPosition);
        }
    }
}
