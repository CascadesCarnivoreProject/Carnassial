using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Carnassial.Interop
{
    // reduced from Stephen Toub's .NET Matters: IFileOperation in Windows Vista column, MSDN Magazine December 2007
    internal class Recycler : IDisposable
    {
        private static readonly Type ShellFileOperationType = Type.GetTypeFromCLSID(Constant.ComGuid.IFileOperationClsid);

        private bool disposed;
        private readonly IFileOperation shellFileOperation;

        public Recycler()
        {
            this.disposed = false;

            // move to recycle bin using IFileOperation on Windows 8 RTM and newer as FOFX_RECYCLEONDELETE is available
            if (Environment.OSVersion.Version >= Constant.Windows8MinimumVersion)
            {
                this.shellFileOperation = (IFileOperation)Activator.CreateInstance(Recycler.ShellFileOperationType);
                this.shellFileOperation.SetOperationFlags(FileOperationFlags.FOFX_RECYCLEONDELETE | FileOperationFlags.FOF_SILENT | FileOperationFlags.FOF_NOERRORUI);
            }
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
                if (this.shellFileOperation != null)
                {
                    Marshal.FinalReleaseComObject(this.shellFileOperation);
                }
            }

            this.disposed = true;
        }

        public void MoveToRecycleBin(string filePath)
        {
            this.ThrowIfDisposed();

            // legacy Windows 7 support
            if (this.shellFileOperation == null)
            {
                NativeMethods.MoveToRecycleBin(filePath);
                return;
            }

            using (ComReleaser<IShellItem> file = NativeMethods.CreateShellItem(filePath))
            {
                this.shellFileOperation.DeleteItem(file.Item, null);
            }
            this.shellFileOperation.PerformOperations();
        }

        public void MoveToRecycleBin(IList<FileInfo> files)
        {
            this.ThrowIfDisposed();
            if (files.Count < 1)
            {
                return;
            }

            // legacy Windows 7 support
            if (this.shellFileOperation == null)
            {
                foreach (FileInfo file in files)
                {
                    NativeMethods.MoveToRecycleBin(file.FullName);
                }
                return;
            }

            foreach (FileInfo file in files)
            {
                if (file == null)
                {
                    continue;
                }

                using (ComReleaser<IShellItem> shellFile = NativeMethods.CreateShellItem(file.FullName))
                {
                    this.shellFileOperation.DeleteItem(shellFile.Item, null);
                }
            }
            this.shellFileOperation.PerformOperations();
        }

        private void ThrowIfDisposed()
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException(nameof(Recycler));
            }
        }
    }
}
