using System;
using System.IO;

namespace Timelapse.Database
{
    /// <summary>
    /// Make a time-stamped backup of the database and csv files, if they exist 
    /// </summary>
    public class FileBackup
    {
        public static bool TryCreateBackups(string folderPath, string databaseFileName)
        {
            string backupFolderPath = Path.Combine(folderPath, Constants.File.BackupFolder);   // The Backup Folder 
            string databaseFilePath = Path.Combine(folderPath, databaseFileName);
            try
            {
                // Backup the database file
                if (File.Exists(databaseFilePath))
                {
                    // While this file almost certainly exists, may as well check for it.
                    if (!Directory.Exists(backupFolderPath))
                    {
                        Directory.CreateDirectory(backupFolderPath);  // Create the backup folder if needed
                    }

                    string databaseBackupFileName = FileBackup.CreateTimeStampedFileName(databaseFileName);
                    string databaseBackupFilePath = Path.Combine(backupFolderPath, databaseBackupFileName);
                    File.Copy(databaseFilePath, databaseBackupFilePath, true);
                }

                // Backup the CSV file if it exists
                // there won't be one for template databases and one may never be never be exported from an image database
                string csvFileName = Path.GetFileNameWithoutExtension(databaseFileName) + Constants.File.CsvFileExtension;
                string csvFilePath = Path.Combine(folderPath, csvFileName);
                if (File.Exists(csvFilePath))
                {
                    string csvBackupFileName = FileBackup.CreateTimeStampedFileName(csvFileName);
                    string csvBackupFilePath = Path.Combine(folderPath, Constants.File.BackupFolder, csvBackupFileName);
                    File.Copy(csvFilePath, csvBackupFilePath, true);
                }
                return true; // backups succeeded
            }
            catch
            {
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
    }
}
