using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace Carnassial.Interop
{
    internal class NativeMethods
    {
        private const int FILE_ATTRIBUTE_DIRECTORY = 0x10;
        private const int FILE_ATTRIBUTE_NORMAL = 0x80;

        public static ComReleaser<IShellItem> CreateShellItem(string path)
        {
            Guid shellItemGuid = new Guid(Constant.ComGuid.IShellItem);
            return new ComReleaser<IShellItem>((IShellItem)NativeMethods.SHCreateItemFromParsingName(path, null, ref shellItemGuid));
        }

        public static string GetRelativePathFromDirectoryToDirectory(string fromDirectoryPath, string toDirectoryPath)
        {
            return NativeMethods.GetRelativePathFromDirectory(fromDirectoryPath, toDirectoryPath, NativeMethods.FILE_ATTRIBUTE_DIRECTORY);
        }

        public static string GetRelativePathFromDirectoryToFile(string fromDirectoryPath, FileInfo toFile)
        {
            return NativeMethods.GetRelativePathFromDirectory(fromDirectoryPath, toFile.FullName, NativeMethods.FILE_ATTRIBUTE_NORMAL);
        }

        private static string GetRelativePathFromDirectory(string fromDirectoryPath, string toPath, int toType)
        {
            StringBuilder relativePathBuilder = new StringBuilder(260); // MAX_PATH
            if (NativeMethods.PathRelativePathTo(relativePathBuilder,
                                                 fromDirectoryPath,
                                                 NativeMethods.FILE_ATTRIBUTE_DIRECTORY,
                                                 toPath,
                                                 toType) == 0)
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

        public static void MoveToRecycleBin(string filePath)
        {
            if (String.IsNullOrEmpty(filePath))
            {
                throw new ArgumentOutOfRangeException(nameof(filePath), "Path to file is null or empty.");
            }

            // per docs, SHFileOperation() won't move files to recycle bin unless presented a full path
            filePath = Path.GetFullPath(filePath);

            SHFILEOPSTRUCT moveToBin = new SHFILEOPSTRUCT()
            {
                hwnd = IntPtr.Zero,
                wFunc = SHFileOpFunc.FO_DELETE,
                pFrom = filePath + "\\0\\0",
                pTo = null,
                fFlags = FILEOP_FLAGS.FOF_ALLOWUNDO | FILEOP_FLAGS.FOF_NOCONFIRMATION | FILEOP_FLAGS.FOF_NOERRORUI | FILEOP_FLAGS.FOF_SILENT,
                fAnyOperationsAborted = false,
                hNameMappings = IntPtr.Zero,
                lpszProgressTitle = null
            };
            int result = NativeMethods.SHFileOperation(ref moveToBin);
            if (result != 0)
            {
                throw new Win32Exception(result);
            }
            if (moveToBin.fAnyOperationsAborted)
            {
                throw new Win32Exception(String.Format("Move of '{0}' to recycle bin was aborted.", filePath));
            }
        }

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int PathRelativePathTo(StringBuilder pszPath, string pszFrom, int dwAttrFrom, string pszTo, int dwAttrTo);

        [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode, PreserveSig = false)]
        [return: MarshalAs(UnmanagedType.Interface)]
        private static extern object SHCreateItemFromParsingName([MarshalAs(UnmanagedType.LPWStr)] string pszPath, IBindCtx pbc, ref Guid riid);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHFileOperation([In] ref SHFILEOPSTRUCT lpFileOp);

        [Flags]
        public enum FILEOP_FLAGS : ushort
        {
            FOF_MULTIDESTFILES = 0x1,
            FOF_CONFIRMMOUSE = 0x2,

            /// <summary>
            /// Don't create progress/report
            /// </summary>
            FOF_SILENT = 0x4,
            FOF_RENAMEONCOLLISION = 0x8,
            
            /// <summary>
            /// Don't prompt the user.
            /// </summary>
            FOF_NOCONFIRMATION = 0x10,
            
            /// <summary>
            /// Fill in SHFILEOPSTRUCT.hNameMappings.
            /// Must be freed using SHFreeNameMappings
            /// </summary>
            FOF_WANTMAPPINGHANDLE = 0x20,
            FOF_ALLOWUNDO = 0x40,
            
            /// <summary>
            /// On *.*, do only files
            /// </summary>
            FOF_FILESONLY = 0x80,
            
            /// <summary>
            /// Don't show names of files
            /// </summary>
            FOF_SIMPLEPROGRESS = 0x100,
            
            /// <summary>
            /// Don't confirm directory creation
            /// </summary>
            FOF_NOCONFIRMMKDIR = 0x200,
            
            /// <summary>
            /// Don't put up error UI
            /// </summary>
            FOF_NOERRORUI = 0x400,
            
            /// <summary>
            /// Don't copy NT file Security Attributes
            /// </summary>
            FOF_NOCOPYSECURITYATTRIBS = 0x800,
            
            /// <summary>
            /// Don't recurse into directories.
            /// </summary>
            FOF_NORECURSION = 0x1000,
            
            /// <summary>
            /// Don't operate on connected elements.
            /// </summary>
            FOF_NO_CONNECTED_ELEMENTS = 0x2000,
            
            /// <summary>
            /// During delete operation, 
            /// warn if nuking instead of recycling (partially overrides FOF_NOCONFIRMATION)
            /// </summary>
            FOF_WANTNUKEWARNING = 0x4000,
            
            /// <summary>
            /// Treat reparse points as objects, not containers
            /// </summary>
            FOF_NORECURSEREPARSE = 0x8000
        }

        public enum SHFileOpFunc : uint
        {
            FO_MOVE = 0x1,
            FO_COPY = 0x2,
            FO_DELETE = 0x3,
            FO_RENAME = 0x4
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEOPSTRUCT
        {
            public IntPtr hwnd;
            public SHFileOpFunc wFunc;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pFrom;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pTo;
            public FILEOP_FLAGS fFlags;
            [MarshalAs(UnmanagedType.Bool)]
            public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpszProgressTitle;
        }
    }
}
