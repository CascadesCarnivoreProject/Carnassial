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
            return $"Read {this.CurrentPosition} of {this.EndPosition} files...";
        }
    }
}
