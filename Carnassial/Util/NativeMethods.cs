using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Carnassial.Util
{
    internal class NativeMethods
    {
        public static string GetRelativePathFromDirectoryToDirectory(string fromDirectoryPath, string toFilePath)
        {
            return NativeMethods.GetRelativePathFromDirectory(fromDirectoryPath, toFilePath, true);
        }

        public static string GetRelativePathFromDirectoryToFile(string fromDirectoryPath, string toFilePath)
        {
            return NativeMethods.GetRelativePathFromDirectory(fromDirectoryPath, toFilePath, false);
        }

        private static string GetRelativePathFromDirectory(string fromDirectoryPath, string toPath, bool toIsDirectory)
        {
            StringBuilder relativePathBuilder = new StringBuilder(260); // MAX_PATH
            if (NativeMethods.PathRelativePathTo(relativePathBuilder,
                                                 fromDirectoryPath,
                                                 NativeMethods.FILE_ATTRIBUTE_DIRECTORY,
                                                 toPath,
                                                 toIsDirectory ? NativeMethods.FILE_ATTRIBUTE_DIRECTORY : NativeMethods.FILE_ATTRIBUTE_NORMAL) == 0)
            {
                throw new ArgumentException("Paths must have a common prefix");
            }

            string relativePath = relativePathBuilder.ToString();
            if (relativePath.StartsWith(".\\"))
            {
                relativePath = relativePath.Substring(2);
            }
            return relativePath;
        }

        private const int FILE_ATTRIBUTE_DIRECTORY = 0x10;
        private const int FILE_ATTRIBUTE_NORMAL = 0x80;

        [StructLayout(LayoutKind.Sequential)]
        internal struct Win32Point
        {
            public Int32 X;
            public Int32 Y;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(ref Win32Point pt);

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int PathRelativePathTo(StringBuilder pszPath, string pszFrom, int dwAttrFrom, string pszTo, int dwAttrTo);
    }
}
