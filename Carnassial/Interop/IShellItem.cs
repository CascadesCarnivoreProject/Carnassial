using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace Carnassial.Interop
{
    // from Stephen Toub's .NET Matters: IFileOperation in Windows Vista column, MSDN Magazine December 2007
    [ComImport]
    [Guid(Constant.ComGuid.IShellItem)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellItem
    {
        [return: MarshalAs(UnmanagedType.Interface)]
        object BindToHandler(IBindCtx pbc, ref Guid bhid, ref Guid riid);

        IShellItem GetParent();

        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetDisplayName(SIGDN sigdnName);

        uint GetAttributes(uint sfgaoMask);

        int Compare(IShellItem psi, uint hint);
    }
}
