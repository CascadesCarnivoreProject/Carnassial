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
            string searchPattern = sourceFileNameWithoutExtension + "*" + sourceFileExtension;
            return backupFolder.GetFiles(searchPattern);
        }

        public static DateTime GetMostRecentBackup(string sourceFilePath)
        {
            DirectoryInfo backupFolder = FileBackup.GetOrCreateBackupFolder(sourceFilePath);
            FileInfo mostRecentBackupFile = FileBackup.GetBackupFiles(backupFolder, sourceFilePath).OrderByDescending(file => file.LastWriteTimeUtc).FirstOrDefault();
            if (mostRecentBackupFile != null)
            {
                return mostRecentBackupFile.LastWriteTimeUtc;
            }
            return DateTime.MinValue.ToUniversalTime();
        }

        public static DirectoryInfo GetOrCreateBackupFolder(string sourceFilePath)
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
            // file names can't contain colons so use non-standard format for timestamp with dashes for hour-minute-second separation and an underscore in 
            // the UTC offset
            string sourceFileNameWithoutExtension = Path.GetFileNameWithoutExtension(sourceFileName);
            string sourceFileExtension = Path.GetExtension(sourceFileName);
            string destinationFileName = String.Concat(sourceFileNameWithoutExtension, ".", DateTime.Now.ToString("yyyy-MM-ddTHH-mm-ss.fffK"), sourceFileExtension);
            destinationFileName = destinationFileName.Replace(':', '_');
            string destinationFilePath = Path.Combine(backupFolder.FullName, destinationFileName);
            File.Copy(sourceFilePath, destinationFilePath, true);

            // age out older backup files
            IEnumerable<FileInfo> backupFiles = FileBackup.GetBackupFiles(backupFolder, sourceFilePath).OrderByDescending(file => file.LastWriteTimeUtc);
            foreach (FileInfo file in backupFiles.Skip(Constant.File.NumberOfBackupFilesToKeep))
            {
                File.Delete(file.FullName);
            }

            return true;
        }
    }
}
