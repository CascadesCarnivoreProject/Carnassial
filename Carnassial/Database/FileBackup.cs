using Carnassial.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Carnassial.Database
{
    public class FileBackup
    {
        private static IEnumerable<FileInfo> GetBackupFiles(DirectoryInfo backupFolder, string sourceFilePath)
        {
            string sourceFileNameWithoutExtension = Path.GetFileNameWithoutExtension(sourceFilePath);
            string sourceFileExtension = Path.GetExtension(sourceFilePath);
            string searchPattern = sourceFileNameWithoutExtension + Constant.File.BackupFileSuffixPattern + sourceFileExtension;
            return backupFolder.GetFiles(searchPattern);
        }

        public static DateTime GetMostRecentBackup(string sourceFilePath)
        {
            DirectoryInfo backupFolder = FileBackup.GetOrCreateBackupFolder(sourceFilePath);
            DateTime mostRecentBackup = FileBackup.GetBackupFiles(backupFolder, sourceFilePath).Select(FileBackup.GetMostRecentFileTime).OrderByDescending(fileTime => fileTime).FirstOrDefault();
            return mostRecentBackup;
        }

        private static DateTime GetMostRecentFileTime(FileInfo file)
        {
            // in general, files in active use will have write times more recent than their creation time
            // However, when a file is copied to make a backup its creation time is set to the time of the copy and the
            // the last write time remains unchanged as the file content wasn't modified by the copy.  For backup purposes,
            // it's therefore most likely the creation time indicates the time of the backup.  However, the last write time
            // is considered here in case the file was updated after copying.  This is unlikely in normal backup use cases,
            // but it's possible in a number of atypical scenarios.
            if (file.CreationTimeUtc > file.LastWriteTimeUtc)
            {
                return file.CreationTimeUtc;
            }
            return file.LastWriteTimeUtc;
        }

        private static DirectoryInfo GetOrCreateBackupFolder(string sourceFilePath)
        {
            string sourceFolderPath = Path.GetDirectoryName(sourceFilePath);
            DirectoryInfo backupFolder = new DirectoryInfo(Path.Combine(sourceFolderPath, Constant.File.BackupFolder));   // The Backup Folder 
            if (backupFolder.Exists == false)
            {
                backupFolder.Create();
            }
            return backupFolder;
        }

        public static bool TryCreateBackup(string sourceFilePath)
        {
            return FileBackup.TryCreateBackup(Path.GetDirectoryName(sourceFilePath), Path.GetFileName(sourceFilePath));
        }

        /// <summary>
        /// Make a time stamped backup of the given file in the backup folder, pruning older files with the same extension as needed. 
        /// </summary>
        public static bool TryCreateBackup(string folderPath, string sourceFileName)
        {
            string sourceFilePath = Path.Combine(folderPath, sourceFileName);
            if (File.Exists(sourceFilePath) == false)
            {
                // nothing to do
                return false;
            }

            // create backup folder if needed
            DirectoryInfo backupFolder = FileBackup.GetOrCreateBackupFolder(sourceFilePath);

            // create a timestamped copy of the file
            // File names can't contain colons so use non-standard format for timestamp with dashes for hour-minute-second 
            // separation and an underscore in the UTC offset.
            // Allowing copy to overwrite probably isn't necessary as it's unlikely a file would be backed up twice in the
            // same millisecond.  However, it's included just in case.
            string sourceFileNameWithoutExtension = Path.GetFileNameWithoutExtension(sourceFileName);
            string sourceFileExtension = Path.GetExtension(sourceFileName);
            string destinationFileName = String.Concat(sourceFileNameWithoutExtension, ".", DateTime.Now.ToString(Constant.File.BackupFileSuffixFormat), sourceFileExtension);
            destinationFileName = destinationFileName.Replace(':', '_');
            string destinationFilePath = Path.Combine(backupFolder.FullName, destinationFileName);
            File.Copy(sourceFilePath, destinationFilePath, true);

            // move any backup files older than the age out limit to the Recycle Bin
            IEnumerable<FileInfo> backupFiles = FileBackup.GetBackupFiles(backupFolder, sourceFilePath).OrderByDescending(FileBackup.GetMostRecentFileTime);
            using (Recycler fileOperation = new Recycler())
            {
                fileOperation.MoveToRecycleBin(backupFiles.Skip(Constant.File.NumberOfBackupFilesToKeep).ToList());
            }

            return true;
        }
    }
}
