using Carnassial.Interop;
using System;
using System.Diagnostics;

namespace Carnassial.Data
{
    public class SpreadsheetReadWriteStatus
    {
        private double currentPosition;
        private double endPosition;
        private bool isExcelSave;
        private bool isExcelLoad;
        private bool isRead;
        private bool isTransactionCommit;
        private ulong mostRecentStatusUpdate;
        private readonly IProgress<SpreadsheetReadWriteStatus> progress;
        private readonly ulong progressUpdateIntervalInMilliseconds;
        private double spreadsheetReadPositionDivisor;
        private string spreadsheetReadPositionUnit;

        public SpreadsheetReadWriteStatus(Action<SpreadsheetReadWriteStatus> onProgressUpdate, TimeSpan progressUpdateInterval)
        {
            this.ClearFlags();
            this.currentPosition = 0.0;
            this.endPosition = -1.0;
            this.mostRecentStatusUpdate = 0;
            this.progress = new Progress<SpreadsheetReadWriteStatus>(onProgressUpdate);
            this.progressUpdateIntervalInMilliseconds = (UInt64)progressUpdateInterval.TotalMilliseconds;
            this.spreadsheetReadPositionDivisor = -1.0;
            this.spreadsheetReadPositionUnit = null;
        }

        public void BeginRead(long bytesToRead)
        {
            Debug.Assert(bytesToRead >= 0, "Expected bytes to read.");

            this.ClearFlags();
            this.endPosition = (double)bytesToRead;
            this.isRead = true;
            this.spreadsheetReadPositionDivisor = 1024.0;
            this.spreadsheetReadPositionUnit = "kB";

            if (bytesToRead > 1024 * 1024)
            {
                this.spreadsheetReadPositionDivisor = 1024.0 * 1024.0;
                this.spreadsheetReadPositionUnit = "MB";
            }

            this.Report(0);
        }

        public void BeginExcelLoad(int sharedStringsToRead)
        {
            this.ClearFlags();
            this.endPosition = sharedStringsToRead;
            this.isExcelLoad = true;

            this.Report(0);
        }

        public void BeginExcelSave()
        {
            this.ClearFlags();
            this.endPosition = 1.0;
            this.isExcelSave = true;

            this.Report(0);
        }

        public void BeginTransactionCommit(int totalFilesToInsertOrUpdate)
        {
            Debug.Assert(totalFilesToInsertOrUpdate >= 0, "Expected files to transact.");

            this.ClearFlags();
            this.endPosition = (double)totalFilesToInsertOrUpdate;
            this.isTransactionCommit = true;

            this.Report(0);
        }

        public void BeginWrite(int rowsToWrite)
        {
            Debug.Assert(rowsToWrite >= 0, "Expected rows to write.");

            this.ClearFlags();
            this.endPosition = (double)rowsToWrite;

            this.Report(0);
        }

        private void ClearFlags()
        {
            this.isExcelSave = false;
            this.isExcelLoad = false;
            this.isRead = false;
            this.isTransactionCommit = false;
        }

        public void EndExcelWorkbookSave()
        {
            this.Report(1);
        }

        public string GetMessage()
        {
            if (this.isExcelSave)
            {
                return "Saving Excel file...";
            }
            if (this.isExcelLoad)
            {
                return "Loading Excel file...";
            }
            if (this.isRead)
            {
                return String.Format("Read {0:0.0} of {1:0.0}{2}...", this.currentPosition / this.spreadsheetReadPositionDivisor, this.endPosition / this.spreadsheetReadPositionDivisor, this.spreadsheetReadPositionUnit);
            }
            if (this.isTransactionCommit)
            {
                return "Updating Carnassial database...";
            }
            return String.Format("Writing row {0} of {1}...", this.currentPosition, this.endPosition);
        }

        public double GetPercentage()
        {
            Debug.Assert(this.endPosition >= 0.0, "Call a Begin() method to set the endPosition before initiating a status report.");

            if (this.endPosition <= Double.Epsilon)
            {
                // avoid divide by zero as ProgressBar.Value rejects values larger than 100%
                // Positioning is ambiguous in this case.  Since currentPosition should not be greater than endPosition and positions
                // are zero or positive, a zero end position implies a zero current position.  In such no op cases, the progress
                // value which produces the best user experience likely depends on previous and subsequent operations, information
                // which is not available in this context.  In general, it's least confusing to indicate an operation in progress so,
                // while any value from 0 to 100% is valid, report 50% as a best effort.
                return 50.0;
            }
            return 100.0 * this.currentPosition / this.endPosition;
        }

        public void Report(long currentPosition)
        {
            this.currentPosition = (double)currentPosition;
            Debug.Assert(this.currentPosition <= this.endPosition, "Current position past end position.");

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
