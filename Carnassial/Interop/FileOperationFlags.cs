using System;

namespace Carnassial.Interop
{
    // updated from Stephen Toub's .NET Matters: IFileOperation in Windows Vista column, MSDN Magazine December 2007
    [Flags]
    internal enum FileOperationFlags : uint
    {
        FOF_DEFAULT = FOF_ALLOWUNDO | FOF_NOCONFIRMMKDIR,

        /// <summary>
        /// Preserve undo information, if possible. 
        /// </summary>
        /// <remarks>
        /// Prior to Windows Vista, operations could be undone only from the same process that performed the original operation.
        /// In Windows Vista and later systems, the scope of the undo is a user session. Any process running in the user session can
        /// undo another operation. The undo state is held in the Explorer.exe process, and as long as that process is running, it can
        /// coordinate the undo functions.
        /// If the source file parameter does not contain fully qualified path and file names, this flag is ignored.
        /// </remarks>
        FOF_ALLOWUNDO = 0x0040,

        /// <summary>
        /// Perform the operation only on files (not on folders) if a wildcard file name (*.*) is specified..
        /// </summary>
        FOF_FILESONLY = 0x0080,

        /// <summary>
        /// Respond with Yes to All for any dialog box that is displayed.
        /// </summary>
        FOF_NOCONFIRMATION = 0x0010,

        /// <summary>
        /// Do not confirm the creation of a new folder if the operation requires one to be created.
        /// </summary>
        FOF_NOCONFIRMMKDIR = 0x0200, // don't confirm making any needed dirs

        /// <summary>
        /// Do not move connected items as a group. Only move the specified files.
        /// </summary>
        FOF_NO_CONNECTED_ELEMENTS = 0x2000,

        /// <summary>
        /// Do not copy the security attributes of the item.
        /// </summary>
        FOF_NOCOPYSECURITYATTRIBS = 0x0800,

        /// <summary>
        /// Do not display a message to the user if an error occurs. If this flag is set without FOFX_EARLYFAILURE, any error
        /// is treated as if the user had chosen Ignore or Continue in a dialog box. It halts the current action, sets a flag
        /// to indicate that an action was aborted, and proceeds with the rest of the operation.
        /// </summary>
        FOF_NOERRORUI = 0x0400,

        /// <summary>
        /// Only operate in the local folder. Do not operate recursively into subdirectories. 
        /// </summary>
        FOF_NORECURSION = 0x1000,

        /// <summary>
        /// Give the item being operated on a new name in a move, copy, or rename operation if an item with the target name 
        /// already exists.
        /// </summary>
        FOF_RENAMEONCOLLISION = 0x0008,

        /// <summary>
        /// Do not display a progress dialog box.
        /// </summary>
        FOF_SILENT = 0x0004,

        /// <summary>
        /// Send a warning if a file or folder is being destroyed during a delete operation rather than recycled. This flag partially
        /// overrides <see cref="FOF_NOCONFIRMATION"/>.
        /// </summary>
        FOF_WANTNUKEWARNING = 0x4000,

        /// <summary>
        /// Introduced in Windows 8. The file operation was user-invoked and should be placed on the undo stack. This flag is 
        /// preferred to FOF_ALLOWUNDO.
        /// </summary>
        FOFX_ADDUNDORECORD = 0x20000000,

        /// <summary>
        /// Walk into Shell namespace junctions. By default, junctions are not entered.
        /// </summary>
        FOFX_NOSKIPJUNCTIONS = 0x00010000,

        /// <summary>
        /// If possible, create a hard link rather than a new instance of the file in the destination.
        /// </summary>
        FOFX_PREFERHARDLINK = 0x00020000,

        /// <summary>
        /// If an operation requires elevated rights and the FOF_NOERRORUI flag is set to disable error UI, display a UAC UI prompt
        /// nonetheless. 
        /// </summary>
        FOFX_SHOWELEVATIONPROMPT = 0x00040000,

        /// <summary>
        /// If FOFX_EARLYFAILURE is set together with FOF_NOERRORUI, the entire set of operations is stopped upon encountering any 
        /// error in any operation. This flag is valid only when FOF_NOERRORUI is set.
        /// </summary>
        FOFX_EARLYFAILURE = 0x00100000,

        /// <summary>
        /// Rename collisions in such a way as to preserve file name extensions. This flag is valid only when FOF_RENAMEONCOLLISION 
        /// is also set.
        /// </summary>
        FOFX_PRESERVEFILEEXTENSIONS = 0x00200000,

        /// <summary>
        /// Keep the newer file or folder, based on the Date Modified property, if a collision occurs. This is done automatically 
        /// with no prompt UI presented to the user.
        /// </summary>
        FOFX_KEEPNEWERFILE = 0x00400000,

        /// <summary>
        /// Do not use copy hooks.
        /// </summary>
        FOFX_NOCOPYHOOKS = 0x00800000,

        /// <summary>
        /// Do not allow the progress dialog to be minimized.
        /// </summary>
        FOFX_NOMINIMIZEBOX = 0x01000000,

        /// <summary>
        /// Copy the security attributes of the source item to the destination item when performing a cross-volume move operation. 
        /// Without this flag, the destination item receives the security attributes of its new folder.
        /// </summary>
        FOFX_MOVEACLSACROSSVOLUMES = 0x02000000,

        /// <summary>
        /// Do not display the path of the source item in the progress dialog.
        /// </summary>
        FOFX_DONTDISPLAYSOURCEPATH = 0x04000000,

        /// <summary>
        /// Do not display the path of the destination item in the progress dialog.
        /// </summary>
        FOFX_DONTDISPLAYDESTPATH = 0x08000000,

        /// <summary>
        /// Introduced in Windows 8. When a file is deleted, send it to the Recycle Bin rather than permanently deleting it.
        /// </summary>
        FOFX_RECYCLEONDELETE = 0x00080000,

        /// <summary>
        /// Introduced in Windows Vista SP1. The user expects a requirement for rights elevation, so do not display a dialog
        /// box asking for a confirmation of the elevation.
        /// </summary>
        FOFX_REQUIREELEVATION = 0x10000000,

        /// <summary>
        /// Introduced in Windows 7. Display a Downloading instead of Copying message in the progress dialog.
        /// </summary>
        FOFX_COPYASDOWNLOAD = 0x40000000,

        /// <summary>
        /// Introduced in Windows 7. Do not display the location line in the progress dialog.
        /// </summary>
        FOFX_DONTDISPLAYLOCATIONS = 0x80000000
    }
}
