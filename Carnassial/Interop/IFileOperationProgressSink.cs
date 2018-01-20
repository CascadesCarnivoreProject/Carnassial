using System;
using System.Runtime.InteropServices;

namespace Carnassial.Interop
{
    // from Stephen Toub's .NET Matters: IFileOperation in Windows Vista column, MSDN Magazine December 2007
    [ComImport]
    [Guid(Constant.ComGuid.IFileOperationProgressSink)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IFileOperationProgressSink
    {
        void StartOperations();
        void FinishOperations(uint hrResult);

        void PreRenameItem(uint dwFlags, IShellItem psiItem, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName);
        void PostRenameItem(uint dwFlags, IShellItem psiItem, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName, uint hrRename, IShellItem psiNewlyCreated);

        void PreMoveItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName);
        void PostMoveItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName, uint hrMove, IShellItem psiNewlyCreated);

        void PreCopyItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName);
        void PostCopyItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName, uint hrCopy, IShellItem psiNewlyCreated);

        void PreDeleteItem(uint dwFlags, IShellItem psiItem);
        void PostDeleteItem(uint dwFlags, IShellItem psiItem, uint hrDelete, IShellItem psiNewlyCreated);

        void PreNewItem(uint dwFlags, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName);
        void PostNewItem(uint dwFlags, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName, [MarshalAs(UnmanagedType.LPWStr)] string pszTemplateName, uint dwFileAttributes, uint hrNew, IShellItem psiNewItem);

        void UpdateProgress(uint iWorkTotal, uint iWorkSoFar);

        void ResetTimer();
        void PauseTimer();
        void ResumeTimer();
    }
}
