using System;
using System.IO;

namespace Timelapse.Database
{
    /// <summary>
    /// Make a time-stamped backup of the database and csv files, if they exist 
    /// </summary>
    internal class FileBackup
    {
        public static bool CreateBackups(string folderPath, string databaseFileName)
        {
            string backupFolderPath = Path.Combine(folderPath, Constants.File.BackupFolder);   // The Backup Folder 
            string databaseFilePath = Path.Combine(folderPath, databaseFileName);
            string databaseBackupFileName = FileBackup.CreateTimeStampedFileName(databaseFileName);
            string databaseBackupFilePath = Path.Combine(backupFolderPath, databaseBackupFileName);

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
                    File.Copy(databaseFilePath, databaseBackupFilePath, true);
                }

                // Backup the CSV file
                string csvFilename = Path.GetFileNameWithoutExtension(databaseFileName) + Constants.File.CsvFileExtension;
                string csvFile = Path.Combine(folderPath, csvFilename);
                string csvBackupFilename = CreateTimeStampedFileName(csvFilename);
                string csvBackupFile = Path.Combine(folderPath, Constants.File.BackupFolder, csvBackupFilename);
                if (File.Exists(csvFile))
                {
                    // The csv file doesn't always exist.
                    File.Copy(csvFile, csvBackupFile, true);
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
        private static string CreateTimeStampedFileName(string fname)
        {
            string prefix = Path.GetFileNameWithoutExtension(fname);
            string suffix = Path.GetExtension(fname);
            string date = DateTime.Now.ToString("yyyy-MM-dd.HH-mm-ss");
            return String.Concat(prefix, ".", date, suffix);
        }
    }
}
