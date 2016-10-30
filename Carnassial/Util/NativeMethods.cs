﻿using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace Carnassial.Util
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
