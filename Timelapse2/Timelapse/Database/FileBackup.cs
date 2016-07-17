using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
namespace Timelapse.Database
{
    /// <summary>
    /// Make a time-stamped backup of the given file in the backup folder.
    /// At the same time, limit the number of backup files, where we prune older files with the same extension as needed. 
    /// </summary>
    public class FileBackup
    {
        public static bool TryCreateBackups(string folderPath, string sourceFile)
        {
            string backupFolderPath = Path.Combine(folderPath, Constants.File.BackupFolder);   // The Backup Folder 
            string sourceFilePath = Path.Combine(folderPath, sourceFile);
            string extension = Path.GetExtension(sourceFile);
            try
            {
                // Backup the database file
                if (File.Exists(sourceFilePath))
                {
                    // Create the backup folder if needed.
                    if (!Directory.Exists(backupFolderPath))
                    {
                        Directory.CreateDirectory(backupFolderPath);  // Create the backup folder if needed
                    }
                    // create a  timestamped destination file name
                    string destinationFile = FileBackup.CreateTimeStampedFileName(sourceFile);
                    string destinationFilePath = Path.Combine(backupFolderPath, destinationFile);
                    File.Copy(sourceFilePath, destinationFilePath, true);
                } 
                PruneBackups(backupFolderPath, extension); // Remove older backup files
                return true; // backups succeeded
            }
            catch (Exception exception)
            {
                Debug.Assert(false, String.Format("Backup of {0} failed.", sourceFile), exception.ToString());
                return false; // One or more backups failed
            }
        }

        // Given a filename, create a timestamped version of it by inserting a timestamp just before the suffix extension.
        // For example, TimelapseData.ddb becomes TimelapseData.2016-02-03.13-57-28.ddb (if done at Feb 3, 2016 at time 13:57:28)
        private static string CreateTimeStampedFileName(string fileName)
        {
            string name = Path.GetFileNameWithoutExtension(fileName);
            string extension = Path.GetExtension(fileName);
            string date = DateTime.Now.ToString("yyyy-MM-dd.HH-mm-ss");
            return String.Concat(name, ".", date, extension);
        }

        // When the backup files with the same extension exceed the NumberOfBackupFilesToKeep, we remove the oldest ones
        private static void PruneBackups(string backupFolderPath, string extension)
        {
            foreach (FileInfo file in new DirectoryInfo(backupFolderPath)
                .GetFiles("*" + extension)
                .OrderByDescending(x => x.LastWriteTime)
                .Skip(Constants.File.NumberOfBackupFilesToKeep))
            {
                File.Delete(file.FullName);
            }
        }
    }
}
