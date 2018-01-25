using MetadataExtractor.IO;
using Microsoft.Win32.SafeHandles;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using ClrBuffer = System.Buffer;

namespace Carnassial.Interop
{
    public class UnbufferedSequentialReader : SequentialReader, IDisposable
    {
        private int bufferPosition;
        private bool disposed;
        private SafeFileHandle file;
        private int filePosition;
        private Lazy<long> length;

        public byte[] Buffer { get; private set; }

        public UnbufferedSequentialReader(string filePath)
            : base(true)
        {
            this.Buffer = null;
            this.disposed = false;
            this.file = NativeMethods.CreateFileUnbuffered(filePath, false);
            this.filePosition = 0;
            this.length = new Lazy<long>(() => { return NativeMethods.GetFileSizeEx(this.file); });
            this.bufferPosition = 0;
        }

        public long Length
        {
            get { return this.length.Value; }
        }

        public override long Position
        {
            get { return this.bufferPosition; }
        }

        public void ExtendBuffer(int bytesToRead)
        {
            byte[] existingBuffer = this.Buffer;
            int existingBufferLength = existingBuffer == null ? 0 : existingBuffer.Length;
            this.Buffer = new byte[existingBufferLength + bytesToRead];
            if (existingBufferLength > 0)
            {
                ClrBuffer.BlockCopy(existingBuffer, 0, this.Buffer, 0, existingBufferLength);
            }

            int bytesRead = this.Read(this.Buffer, existingBufferLength, bytesToRead);
            if (bytesRead != bytesToRead)
            {
                throw new IOException(String.Format("Only {0} instead of {1} bytes were read.", bytesRead, bytesToRead));
            }
            this.filePosition += bytesRead;
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
                this.file.Dispose();
            }

            this.disposed = true;
        }

        public override byte GetByte()
        {
            if (this.Buffer == null)
            {
                throw new NotSupportedException("Call " + nameof(this.ExtendBuffer) + "() before calling " + nameof(this.GetByte) + "().");
            }
            if (this.bufferPosition >= this.Buffer.Length)
            {
                throw new NotSupportedException("Attempt to read byte beyond end of buffer.");
            }
            return this.Buffer[this.bufferPosition++];
        }

        public override byte[] GetBytes(int count)
        {
            byte[] bytes = new byte[count];
            this.GetBytes(bytes, 0, count);
            return bytes;
        }

        public override void GetBytes(byte[] buffer, int offset, int count)
        {
            if (this.Buffer == null)
            {
                throw new NotSupportedException("Call " + nameof(this.ExtendBuffer) + "() before calling " + nameof(this.GetBytes) + "().");
            }

            if ((this.bufferPosition + count) > this.Buffer.Length)
            {
                throw new ArgumentOutOfRangeException("Requested read exceeds file buffer length.");
            }
            ClrBuffer.BlockCopy(this.Buffer, this.bufferPosition, buffer, offset, count);
            this.bufferPosition += count;
        }

        private unsafe int Read(byte[] buffer, long offset, int bytesToRead)
        {
            fixed (byte* bufferPin = &buffer[offset])
            {
                int bytesRead = 0;
                int result = NativeMethods.ReadFile(file, bufferPin, bytesToRead, ref bytesRead, null);
                if (result == 0)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
                return bytesRead;
            }
        }

        public override void Skip(long bytes)
        {
            if (bytes < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bytes) + " must be zero or greater to read stream sequentially.");
            }

            int newPosition = this.bufferPosition + (int)bytes;
            if (newPosition > this.Buffer.Length)
            {
                throw new EndOfStreamException("Unable to skip past of end of file buffer.");
            }
            this.bufferPosition = newPosition;
        }

        public override bool TrySkip(long bytes)
        {
            int newPosition = this.bufferPosition + (int)bytes;
            if ((bytes < 0) || (newPosition > this.Buffer.Length))
            {
                return false;
            }

            this.bufferPosition = newPosition;
            return true;
        }

        public override SequentialReader WithByteOrder(bool isMotorolaByteOrder)
        {
            if (isMotorolaByteOrder)
            {
                return this;
            }
            throw new NotSupportedException("Little endian byte ordering is not available.");
        }
    }
}
