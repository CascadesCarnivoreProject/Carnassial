using Carnassial.Interop;
using System;

namespace Carnassial.Data
{
    public class SpreadsheetReadWriteStatus
    {
        private double csvReadPositionDivisor;
        private string csvReadPositionUnit;
        private double currentPosition;
        private double endPosition;
        private bool isCsvRead;
        private bool isExcelRead;
        private ulong mostRecentStatusUpdate;
        private IProgress<SpreadsheetReadWriteStatus> progress;
        private ulong progressUpdateIntervalInMilliseconds;

        public SpreadsheetReadWriteStatus(Action<SpreadsheetReadWriteStatus> onProgressUpdate, TimeSpan progressUpdateInterval)
        {
            this.csvReadPositionDivisor = -1.0;
            this.csvReadPositionUnit = null;
            this.currentPosition = 0.0;
            this.endPosition = -1.0;
            this.isCsvRead = false;
            this.isExcelRead = false;
            this.mostRecentStatusUpdate = 0;
            this.progress = new Progress<SpreadsheetReadWriteStatus>(onProgressUpdate);
            this.progressUpdateIntervalInMilliseconds = (UInt64)progressUpdateInterval.TotalMilliseconds;
        }

        public void BeginCsvRead(long bytesToRead)
        {
            this.endPosition = (double)bytesToRead;
            this.isCsvRead = true;
            this.isExcelRead = false;
            this.csvReadPositionDivisor = 1024.0;
            this.csvReadPositionUnit = "kB";

            if (bytesToRead > 1024 * 1024)
            {
                this.csvReadPositionDivisor = 1024.0 * 1024.0;
                this.csvReadPositionUnit = "MB";
            }

            this.Report(0);
        }

        public void BeginExcelRead(int rowsToRead)
        {
            this.endPosition = (double)rowsToRead;
            this.isCsvRead = false;
            this.isExcelRead = true;

            this.Report(0);
        }

        public void BeginWrite(int rowsToWrite)
        {
            this.endPosition = (double)rowsToWrite;
            this.isCsvRead = false;
            this.isExcelRead = false;

            this.Report(0);
        }

        public string GetMessage()
        {
            if (this.isCsvRead)
            {
                return String.Format("Read {0:0.0} of {1:0.0}{2}...", this.currentPosition / this.csvReadPositionDivisor, this.endPosition / this.csvReadPositionDivisor, this.csvReadPositionUnit);
            }
            if (this.isExcelRead)
            {
                return String.Format("Read {0:0} of {1:0} rows...", this.currentPosition, this.endPosition);
            }
            return String.Format("Writing row {0} of {1}...", this.currentPosition, this.endPosition);
        }

        public double GetPercentage()
        {
            return 100.0 * this.currentPosition / this.endPosition;
        }

        public void Report(long currentPosition)
        {
            this.currentPosition = (double)(currentPosition + 1);
            this.progress.Report(this);
            this.mostRecentStatusUpdate = NativeMethods.GetTickCount64();
        }

        public bool ShouldReport()
        {
            UInt64 tickNow = NativeMethods.GetTickCount64();
            return (tickNow - this.mostRecentStatusUpdate) > this.progressUpdateIntervalInMilliseconds;
        }
    }
}
