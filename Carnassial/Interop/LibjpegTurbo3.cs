// P/Invoke signatures for Carnassial relevant parts of turbojpeg.h v3 API
using System;
using System.Runtime.InteropServices;

namespace Carnassial.Interop
{
    // see http://www.libjpeg-turbo.org/About/TurboJPEG for TurboJpeg API documentation
    internal partial class LibjpegTurbo3
    {
        public static readonly int[] PixelSize = [ 3, 3, 4, 4, 4, 4, 1, 4, 4, 4, 4, 4 ]; // tjPixelSize

        public static unsafe int Decompress8(IntPtr decompressor, byte* jpegBytes, int lengthInBytes, byte* pinnedPixels, int pitchInBytes, LibjpegPixelFormat pixelFormat)
        {
            return LibjpegTurbo3.tj3Decompress8(decompressor, jpegBytes, lengthInBytes, pinnedPixels, pitchInBytes, pixelFormat);
        }

        public static unsafe int DecompressHeader(IntPtr decompressor, byte* jpegBytes, int lengthInBytes)
        {
            return LibjpegTurbo3.tj3DecompressHeader(decompressor, jpegBytes, lengthInBytes);
        }

        public static void Destroy(IntPtr decompressor)
        {
            LibjpegTurbo3.tjDestroy(decompressor);
        }

        public static IntPtr InitDecompress()
        {
            return LibjpegTurbo3.tjInitDecompress();
        }

        public static int Get(IntPtr handle, LibjpegParameter parameter)
        {
            return LibjpegTurbo3.tj3Get(handle, (int)parameter);
        }

        public static string GetErrorStr(IntPtr handle)
        {
            return LibjpegTurbo3.tj3GetErrorStr(handle);
        }

        public static int SetScalingFactor(IntPtr handle, LibjpegScalingFactor scalingFactor)
        {
            return LibjpegTurbo3.tj3SetScalingFactor(handle, scalingFactor);
        }

        [LibraryImport(Constant.Assembly.TurboJpeg)]
        [return: MarshalAs(UnmanagedType.I4)]
        private static unsafe partial int tj3Decompress8(IntPtr handle, byte* jpegBuf, int jpegSize, byte* dstBuf, int pitch, LibjpegPixelFormat pixelFormat);

        [LibraryImport(Constant.Assembly.TurboJpeg)]
        [return: MarshalAs(UnmanagedType.I4)]
        private static unsafe partial int tj3DecompressHeader(IntPtr handle, byte* jpegBuf, int jpegSize);

        [LibraryImport(Constant.Assembly.TurboJpeg)]
        private static partial void tjDestroy(IntPtr handle);

        [LibraryImport(Constant.Assembly.TurboJpeg)]
        [return: MarshalAs(UnmanagedType.I4)]
        private static partial int tj3Get(IntPtr handle, int param);

        [LibraryImport(Constant.Assembly.TurboJpeg)]
        [return: MarshalAs(UnmanagedType.LPTStr)]
        private static partial string tj3GetErrorStr(IntPtr handle);

        [LibraryImport(Constant.Assembly.TurboJpeg)]
        private static partial IntPtr tjInitDecompress();

        [LibraryImport(Constant.Assembly.TurboJpeg)]
        [return: MarshalAs(UnmanagedType.I4)]
        private static partial int tj3SetScalingFactor(IntPtr handle, LibjpegScalingFactor scalingFactor);
    }
}
