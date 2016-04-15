using Microsoft.Win32;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Timelapse
{
    internal class NativeMethods
    {
        public static string GetRelativePath(string fromPath, string toPath)
        {
            int fromAttr = NativeMethods.GetPathAttribute(fromPath);
            int toAttr = NativeMethods.GetPathAttribute(toPath);
            StringBuilder relativePathBuilder = new StringBuilder(260); // MAX_PATH
            if (NativeMethods.PathRelativePathTo(relativePathBuilder,
                                                 fromPath,
                                                 fromAttr,
                                                 toPath,
                                                 toAttr) == 0)
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

        private static int GetPathAttribute(string path)
        {
            DirectoryInfo di = new DirectoryInfo(path);
            if (di.Exists)
            {
                return NativeMethods.FILE_ATTRIBUTE_DIRECTORY;
            }

            FileInfo fi = new FileInfo(path);
            if (fi.Exists)
            {
                return NativeMethods.FILE_ATTRIBUTE_NORMAL;
            }

            throw new FileNotFoundException(path);
        }

        private const int FILE_ATTRIBUTE_DIRECTORY = 0x10;
        private const int FILE_ATTRIBUTE_NORMAL = 0x80;

        [DllImport("shlwapi.dll", SetLastError = true)]
        private static extern int PathRelativePathTo(StringBuilder pszPath, string pszFrom, int dwAttrFrom, string pszTo, int dwAttrTo);
    }
}
