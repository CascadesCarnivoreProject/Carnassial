using Carnassial.Database;
using Carnassial.Images;
using Carnassial.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Carnassial.Data
{
    public class FileTable : IEnumerable<ImageRow>, ISQLiteTable
    {
        private List<ImageRow> files;

        public FileTable()
        {
            this.files = new List<ImageRow>();
        }

        public int RowCount
        {
            get { return this.files.Count; }
        }

        public ImageRow this[int index]
        {
            get { return this.files[index]; }
        }

        public ImageRow CreateFile(string fileName, string relativePath)
        {
            if (FileTable.IsVideo(fileName))
            {
                return new VideoRow(fileName, relativePath);
            }
            if (JpegImage.IsJpeg(fileName))
            {
                return new ImageRow(fileName, relativePath);
            }
            throw new NotSupportedException(String.Format("Unhandled extension for file '{0}'.", fileName));
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public IEnumerator<ImageRow> GetEnumerator()
        {
            return this.files.GetEnumerator();
        }

        public SortedDictionary<string, List<string>> GetFileNamesByRelativePath()
        {
            SortedDictionary<string, List<string>> filesByRelativePath = new SortedDictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (ImageRow file in this.files)
            {
                if (filesByRelativePath.TryGetValue(file.RelativePath, out List<string> filesInFolder) == false)
                {
                    filesInFolder = new List<string>();
                    filesByRelativePath.Add(file.RelativePath, filesInFolder);
                }
                filesInFolder.Add(file.FileName);
            }
            return filesByRelativePath;
        }

        public Dictionary<string, HashSet<string>> HashFileNamesByRelativePath()
        {
            // for now, the primary purpose of this function is allow the caller to quickly check if a file is in the database
            // Therefore, assemble a Dictionary<, HashSet<>> as both these collection types have O(1) Contains().
            Dictionary<string, HashSet<string>> filesByRelativePath = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (ImageRow file in this.files)
            {
                if (filesByRelativePath.TryGetValue(file.RelativePath, out HashSet<string> filesInFolder) == false)
                {
                    filesInFolder = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    filesByRelativePath.Add(file.RelativePath, filesInFolder);
                }
                filesInFolder.Add(file.FileName);
            }
            return filesByRelativePath;
        }

        public static bool IsVideo(string fileName)
        {
            return fileName.EndsWith(Constant.File.AviFileExtension, StringComparison.OrdinalIgnoreCase) ||
                   fileName.EndsWith(Constant.File.Mp4FileExtension, StringComparison.OrdinalIgnoreCase);
        }

        public void Load(SQLiteDataReader reader)
        {
            if (reader.FieldCount < 1)
            {
                throw new SQLiteException(SQLiteErrorCode.Schema, "Table has no columns.");
            }
            int dateTimeIndex = -1;
            int deleteFlagIndex = -1;
            int fileNameIndex = -1;
            int idIndex = -1;
            int imageQualityIndex = -1;
            int relativePathIndex = -1;
            int utcOffsetIndex = -1;
            List<KeyValuePair<int, string>> userControls = new List<KeyValuePair<int, string>>();
            for (int column = 0; column < reader.FieldCount; ++column)
            {
                string columnName = reader.GetName(column);
                switch (columnName)
                {
                    case Constant.DatabaseColumn.DateTime:
                        dateTimeIndex = column;
                        break;
                    case Constant.DatabaseColumn.DeleteFlag:
                        deleteFlagIndex = column;
                        break;
                    case Constant.DatabaseColumn.File:
                        fileNameIndex = column;
                        break;
                    case Constant.DatabaseColumn.ID:
                        idIndex = column;
                        break;
                    case Constant.DatabaseColumn.ImageQuality:
                        imageQualityIndex = column;
                        break;
                    case Constant.DatabaseColumn.RelativePath:
                        relativePathIndex = column;
                        break;
                    case Constant.DatabaseColumn.UtcOffset:
                        utcOffsetIndex = column;
                        break;
                    default:
                        userControls.Add(new KeyValuePair<int, string>(column, columnName));
                        break;
                }
            }
            bool standardColumnsPresent = (dateTimeIndex != -1) &&
                                          (deleteFlagIndex != -1) &&
                                          (fileNameIndex != -1) &&
                                          (idIndex != -1) &&
                                          (imageQualityIndex != -1) &&
                                          (relativePathIndex != -1) &&
                                          (utcOffsetIndex != -1);
            if (standardColumnsPresent == false)
            {
                throw new SQLiteException(SQLiteErrorCode.Schema, "At least one standard column is missing from table " + reader.GetTableName(0));
            }

            while (reader.Read())
            {
                IDataRecord row = (IDataRecord)reader;
                string fileName = row.GetString(fileNameIndex);
                string relativePath = row.GetString(relativePathIndex);
                ImageRow file = this.CreateFile(fileName, relativePath);

                // read file values
                // Carnassial versions prior to 2.2.0.3 had a bug where UTC offsets where written as TimeSpans rather than doubles
                // which, combined with column type real, produces an odd situation where IDataRecord.GetValue() returns a correct
                // double but GetDouble() throws.  As a workaround for backwards compatibility, GetValue() is called and cast to double.
                double utcOffset = (double)row.GetValue(utcOffsetIndex);
                file.DateTimeOffset = DateTimeHandler.FromDatabaseDateTimeOffset(row.GetDateTime(dateTimeIndex), DateTimeHandler.FromDatabaseUtcOffset(utcOffset));
                file.DeleteFlag = Boolean.Parse(row.GetString(deleteFlagIndex));
                file.ID = row.GetInt64(idIndex);
                file.ImageQuality = (FileSelection)Enum.Parse(typeof(FileSelection), row.GetString(imageQualityIndex));
                foreach (KeyValuePair<int, string> userControl in userControls)
                {
                    file[userControl.Value] = row.GetString(userControl.Key);
                }
                file.AcceptChanges();

                this.files.Add(file);
            }
        }

        public ImageRow Single(string fileName, string relativePath)
        {
            return this.files.Single(file =>
            {
                return (String.Compare(file.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase) == 0) &&
                       (String.Compare(file.FileName, fileName, StringComparison.OrdinalIgnoreCase) == 0);
            });
        }

        public bool TryFind(long id, out ImageRow file, out int fileIndex)
        {
            for (fileIndex = 0; fileIndex < this.files.Count; ++fileIndex)
            {
                file = this.files[fileIndex];
                if (file.ID == id)
                {
                    return true;
                }
            }
            file = null;
            return false;
        }
    }
}
