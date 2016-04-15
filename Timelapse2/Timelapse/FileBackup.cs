using System;
using System.IO;

namespace Timelapse
{
    /// <summary>
    /// Make a time-stamped backup of the database and csv files, if they exist 
    /// </summary>
    class FileBackup
    {
        public static bool CreateBackups(string folderPath, string dbFilename)
        {
            string BackupFolder = System.IO.Path.Combine(folderPath, Constants.File.BackupFolder);   // The Backup Folder 
            string dbFile = System.IO.Path.Combine(folderPath, dbFilename);
            string dbBackupFilename = FileBackup.CreateTimeStampedFileName(dbFilename);
            string dbBackupFile = System.IO.Path.Combine(BackupFolder, dbBackupFilename);

            try
            {
                // Backup the database file
                if (File.Exists(dbFile))    // While this file almost certainly exists, may as well check for it.
                {
                    if (!Directory.Exists(BackupFolder))  Directory.CreateDirectory(BackupFolder);  // Create the backup folder if needed
                    File.Copy(dbFile, dbBackupFile, true);
                }

                // Backup the CSV file
                string csvFilename = (System.IO.Path.GetFileNameWithoutExtension(dbFilename) + Constants.File.CsvFileExtension);
                string csvFile = System.IO.Path.Combine(folderPath, csvFilename);
                string csvBackupFilename = CreateTimeStampedFileName(csvFilename);
                string csvBackupFile = System.IO.Path.Combine(folderPath, Constants.File.BackupFolder, csvBackupFilename);
                if (File.Exists(csvFile)) // The CVS file doesn't always exist.
                {
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
            string prefix = System.IO.Path.GetFileNameWithoutExtension(fname);
            string suffix = System.IO.Path.GetExtension(fname);
            string date = DateTime.Now.ToString("yyyy-MM-dd.HH-mm-ss");
            return (string.Concat(prefix, ".", date, suffix));
        }
    }
}
