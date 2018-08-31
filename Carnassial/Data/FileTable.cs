using Carnassial.Database;
using Carnassial.Images;
using Carnassial.Util;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.Linq;

namespace Carnassial.Data
{
    public class FileTable : SQLiteTable<ImageRow>
    {
        public Dictionary<string, FileTableUserColumn> UserColumnsByName { get; private set; }
        public int UserCounters { get; private set; }
        public int UserFlags { get; private set; }
        public int UserNotesAndChoices { get; private set; }

        public FileTable()
        {
            this.UserColumnsByName = new Dictionary<string, FileTableUserColumn>(StringComparer.Ordinal);
        }

        public static SQLiteTableSchema CreateSchema(ControlTable controls)
        {
            SQLiteTableSchema schema = new SQLiteTableSchema(Constant.DatabaseTable.Files);
            schema.ColumnDefinitions.Add(ColumnDefinition.CreatePrimaryKey());

            // derive schema from the controls defined
            foreach (ControlRow control in controls)
            {
                schema.ColumnDefinitions.AddRange(FileTable.CreateFileTableColumnDefinitions(control));

                if (control.IndexInFileTable)
                {
                    schema.Indices.Add(SecondaryIndex.CreateFileTableIndex(control));
                }
            }

            return schema;
        }

        public ImageRow CreateAndAppendFile(string fileName, string relativePath)
        {
            ImageRow file;
            if (FileTable.IsVideo(fileName))
            {
                file = new VideoRow(fileName, relativePath, this);
            }
            else if (JpegImage.IsJpeg(fileName))
            {
                file = new ImageRow(fileName, relativePath, this);
            }
            else
            {
                throw new NotSupportedException(String.Format("Unhandled extension for file '{0}'.", fileName));
            }

            this.Rows.Add(file);
            return file;
        }

        public static IEnumerable<ColumnDefinition> CreateFileTableColumnDefinitions(ControlRow control)
        {
            if (String.Equals(control.DataLabel, Constant.FileColumn.Classification, StringComparison.Ordinal))
            {
                yield return new ColumnDefinition(control.DataLabel, Constant.SQLiteAffninity.Integer)
                {
                    DefaultValue = ((int)default(FileClassification)).ToString(),
                    NotNull = true
                };
                yield break;
            }
            if (String.Equals(control.DataLabel, Constant.FileColumn.DateTime, StringComparison.Ordinal))
            {
                yield return new ColumnDefinition(control.DataLabel, Constant.ControlDefault.DateTimeValue.DateTime);
                yield break;
            }
            if (String.Equals(control.DataLabel, Constant.FileColumn.UtcOffset, StringComparison.Ordinal))
            {
                // UTC offsets are typically represented as TimeSpans but the least awkward way to store them in SQLite is as a real column containing the offset in
                // hours.  This is because SQLite
                // - handles TIME columns as DateTime rather than TimeSpan, requiring the associated DataTable column also be of type DateTime
                // - doesn't support negative values in time formats, requiring offsets for time zones west of Greenwich be represented as positive values
                // - imposes an upper bound of 24 hours on time formats, meaning the 26 hour range of UTC offsets (UTC-12 to UTC+14) cannot be accomodated
                // - lacks support for DateTimeOffset, so whilst offset information can be written to the database it cannot be read from the database as .NET
                //   supports only DateTimes whose offset matches the current system time zone
                // Storing offsets as ticks, milliseconds, seconds, minutes, or days offers equivalent functionality.  Potential for rounding error in roundtrip 
                // calculations on offsets is similar to hours for all formats other than an INTEGER (long) column containing ticks.  Ticks are a common 
                // implementation choice but testing shows no roundoff errors at single tick precision (100 nanoseconds) when using hours.  Even with TimeSpans 
                // near the upper bound of 256M hours, well beyond the plausible range of time zone calculations.  So there does not appear to be any reason to 
                // avoid using hours for readability when working with the database directly.
                yield return new ColumnDefinition(control.DataLabel, Constant.ControlDefault.DateTimeValue.Offset);
                yield break;
            }

            ColumnDefinition column = new ColumnDefinition(control.DataLabel, Constant.SQLiteAffninity.Text);
            switch (control.Type)
            {
                case ControlType.Counter:
                case ControlType.Flag:
                    column.NotNull = true;
                    column.Type = Constant.SQLiteAffninity.Integer;
                    break;
                case ControlType.Note:
                    if (String.Equals(control.DataLabel, Constant.FileColumn.File, StringComparison.Ordinal))
                    {
                        column.NotNull = true;
                    }
                    break;
                default:
                    // nothing to do
                    break;
            }
            if (String.IsNullOrWhiteSpace(control.DefaultValue) == false)
            {
                column.DefaultValue = control.DefaultValue;
            }
            yield return column;

            if (control.Type == ControlType.Counter)
            {
                yield return new ColumnDefinition(FileTable.GetMarkerPositionColumnName(control.DataLabel), Constant.SQLiteAffninity.Blob);
            }
        }

        public Dictionary<string, Dictionary<string, ImageRow>> GetFilesByRelativePathAndName()
        {
            Dictionary<string, Dictionary<string, ImageRow>> filesByRelativePathAndName = new Dictionary<string, Dictionary<string, ImageRow>>(StringComparer.OrdinalIgnoreCase);
            foreach (ImageRow file in this.Rows)
            {
                if (filesByRelativePathAndName.TryGetValue(file.RelativePath, out Dictionary<string, ImageRow> filesInFolderByName) == false)
                {
                    filesInFolderByName = new Dictionary<string, ImageRow>(StringComparer.OrdinalIgnoreCase);
                    filesByRelativePathAndName.Add(file.RelativePath, filesInFolderByName);
                }
                filesInFolderByName.Add(file.FileName, file);
            }
            return filesByRelativePathAndName;
        }

        public SortedDictionary<string, List<string>> GetFileNamesByRelativePath()
        {
            SortedDictionary<string, List<string>> filesByRelativePath = new SortedDictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (ImageRow file in this.Rows)
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

        public static string GetMarkerPositionColumnName(string counterDataLabel)
        {
            return counterDataLabel + Constant.FileColumn.MarkerPositionSuffix;
        }

        public Dictionary<string, HashSet<string>> HashFileNamesByRelativePath()
        {
            // for now, the primary purpose of this function is allow the caller to quickly check if a file is in the database
            // Therefore, assemble a Dictionary<, HashSet<>> as both these collection types have O(1) Contains().
            Dictionary<string, HashSet<string>> filesByRelativePath = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (ImageRow file in this.Rows)
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

        public FileTableSpreadsheetMap IndexSpreadsheetColumns(List<string> columnsFromSpreadsheet)
        {
            FileTableSpreadsheetMap spreadsheetMap = new FileTableSpreadsheetMap();
            for (int columnIndex = 0; columnIndex < columnsFromSpreadsheet.Count; ++columnIndex)
            {
                string column = columnsFromSpreadsheet[columnIndex];
                if (String.Equals(column, Constant.FileColumn.Classification, StringComparison.Ordinal))
                {
                    spreadsheetMap.ClassificationIndex = columnIndex;
                }
                else if (String.Equals(column, Constant.FileColumn.DateTime, StringComparison.Ordinal))
                {
                    spreadsheetMap.DateTimeIndex = columnIndex;
                }
                else if (String.Equals(column, Constant.FileColumn.DeleteFlag, StringComparison.Ordinal))
                {
                    spreadsheetMap.DeleteFlagIndex = columnIndex;
                }
                else if (String.Equals(column, Constant.FileColumn.File, StringComparison.Ordinal))
                {
                    spreadsheetMap.FileNameIndex = columnIndex;
                }
                else if (String.Equals(column, Constant.FileColumn.RelativePath, StringComparison.Ordinal))
                {
                    spreadsheetMap.RelativePathIndex = columnIndex;
                }
                else if (String.Equals(column, Constant.FileColumn.UtcOffset, StringComparison.Ordinal))
                {
                    spreadsheetMap.UtcOffsetIndex = columnIndex;
                }
                else
                {
                    FileTableUserColumn userColumn = this.UserColumnsByName[column];
                    switch (userColumn.Control.Type)
                    {
                        case ControlType.Counter:
                            if (userColumn.DataType == FileDataType.Integer)
                            {
                                spreadsheetMap.UserCounterIndices.Add(columnIndex);
                            }
                            else if (userColumn.DataType == FileDataType.ByteArray)
                            {
                                spreadsheetMap.UserMarkerIndices.Add(columnIndex);
                            }
                            else
                            {
                                throw new NotSupportedException(String.Format("Unhandled data type {0} for column {1}.", userColumn.DataType, userColumn.Control.DataLabel));
                            }
                            spreadsheetMap.UserCounters.Add(userColumn);
                            break;
                        case ControlType.FixedChoice:
                            spreadsheetMap.UserChoiceDataIndices.Add(userColumn.DataIndex);
                            spreadsheetMap.UserChoiceIndices.Add(columnIndex);
                            spreadsheetMap.UserChoices.Add(userColumn);

                            List<string> choiceValues = userColumn.Control.GetWellKnownValues();
                            if (choiceValues.Contains(userColumn.Control.DefaultValue, StringComparer.Ordinal) == false)
                            {
                                // back compat: prior to Carnassial 2.2.0.3 the editor didn't require a choice's default value also
                                // be a well known value, so include the default as an acceptable value if it's not a well known value
                                choiceValues.Add(userColumn.Control.DefaultValue);
                            }
                            spreadsheetMap.UserChoiceValues.Add(choiceValues);
                            break;
                        case ControlType.Flag:
                            spreadsheetMap.UserFlagIndices.Add(columnIndex);
                            spreadsheetMap.UserFlags.Add(userColumn);
                            break;
                        case ControlType.Note:
                            spreadsheetMap.UserNoteDataIndices.Add(userColumn.DataIndex);
                            spreadsheetMap.UserNoteIndices.Add(columnIndex);
                            spreadsheetMap.UserNotes.Add(userColumn);
                            break;
                        default:
                            throw new NotSupportedException(String.Format("Unhandled control type {0} for column {1}.", userColumn.Control.Type, userColumn.Control.DataLabel));
                    }
                }
            }

            return spreadsheetMap;
        }

        public static bool IsVideo(string fileName)
        {
            return fileName.EndsWith(Constant.File.AviFileExtension, StringComparison.OrdinalIgnoreCase) ||
                   fileName.EndsWith(Constant.File.Mp4FileExtension, StringComparison.OrdinalIgnoreCase);
        }

        public override void Load(SQLiteDataReader reader)
        {
            if (reader.FieldCount < 1)
            {
                throw new SQLiteException(SQLiteErrorCode.Schema, "Table has no columns.");
            }

            // locate columns in schema
            // Standard columns usually appear first but, depending on the table's evolution, this may not always be the case.
            int dateTimeIndex = -1;
            int deleteFlagIndex = -1;
            int fileNameIndex = -1;
            int idIndex = -1;
            int classificationIndex = -1;
            int relativePathIndex = -1;
            int utcOffsetIndex = -1;

            int userCounter = -1;
            int[] userCounterSqlIndices = new int[this.UserCounters];
            int userFlag = -1;
            int[] userFlagSqlIndices = new int[this.UserFlags];
            int userMarkerPosition = -1;
            int[] userMarkerPositionSqlIndices = new int[this.UserCounters];
            int userNoteOrChoice = -1;
            int[] userNoteAndChoiceSqlIndices = new int[this.UserNotesAndChoices];

            for (int columnIndex = 0; columnIndex < reader.FieldCount; ++columnIndex)
            {
                string column = reader.GetName(columnIndex);
                switch (column)
                {
                    case Constant.FileColumn.DateTime:
                        dateTimeIndex = columnIndex;
                        break;
                    case Constant.FileColumn.DeleteFlag:
                        deleteFlagIndex = columnIndex;
                        break;
                    case Constant.FileColumn.File:
                        fileNameIndex = columnIndex;
                        break;
                    case Constant.DatabaseColumn.ID:
                        idIndex = columnIndex;
                        break;
                    case Constant.FileColumn.Classification:
                        classificationIndex = columnIndex;
                        break;
                    case Constant.FileColumn.RelativePath:
                        relativePathIndex = columnIndex;
                        break;
                    case Constant.FileColumn.UtcOffset:
                        utcOffsetIndex = columnIndex;
                        break;
                    default:
                        FileTableUserColumn userColumn = this.UserColumnsByName[column];
                        int dataIndex;
                        switch (userColumn.DataType)
                        {
                            case FileDataType.Boolean:
                                dataIndex = ++userFlag;
                                userFlagSqlIndices[dataIndex] = columnIndex;
                                break;
                            case FileDataType.ByteArray:
                                dataIndex = ++userMarkerPosition;
                                userMarkerPositionSqlIndices[dataIndex] = columnIndex;
                                break;
                            case FileDataType.Integer:
                                dataIndex = ++userCounter;
                                userCounterSqlIndices[dataIndex] = columnIndex;
                                break;
                            case FileDataType.String:
                                dataIndex = ++userNoteOrChoice;
                                userNoteAndChoiceSqlIndices[dataIndex] = columnIndex;
                                break;
                            default:
                                throw new NotSupportedException(String.Format("Unhandled column data type {0}.", userColumn.DataIndex));
                        }
                        userColumn.DataIndex = dataIndex;
                        break;
                }
            }

            bool allStandardColumnsPresent = (dateTimeIndex != -1) &&
                                             (deleteFlagIndex != -1) &&
                                             (fileNameIndex != -1) &&
                                             (idIndex != -1) &&
                                             (classificationIndex != -1) &&
                                             (relativePathIndex != -1) &&
                                             (utcOffsetIndex != -1);
            if (allStandardColumnsPresent == false)
            {
                throw new SQLiteException(SQLiteErrorCode.Schema, "At least one standard column is missing from table " + reader.GetTableName(0));
            }

            this.Rows.Clear();

            while (reader.Read())
            {
                string fileName = reader.GetString(fileNameIndex);
                string relativePath = reader.GetString(relativePathIndex);
                ImageRow file = this.CreateAndAppendFile(fileName, relativePath);
                
                // read file values
                // Carnassial versions prior to 2.2.0.3 had a bug where UTC offsets where written as TimeSpans rather than doubles
                // which, combined with column type real, produces an odd situation where IDataRecord.GetValue() returns a correct
                // double but GetDouble() throws on System.Data.SQLite 1.0.107.  As a workaround for backwards compatibility, 
                // GetValue() is called and cast to double.
                double utcOffset = reader.GetDouble(utcOffsetIndex);
                file.Classification = (FileClassification)reader.GetInt32(classificationIndex);
                file.DateTimeOffset = DateTimeHandler.FromDatabaseDateTimeOffset(reader.GetDateTime(dateTimeIndex), DateTimeHandler.FromDatabaseUtcOffset(utcOffset));
                file.DeleteFlag = reader.GetBoolean(deleteFlagIndex);
                file.ID = reader.GetInt64(idIndex);
                foreach (FileTableUserColumn userColumn in this.UserColumnsByName.Values)
                {
                    switch (userColumn.DataType)
                    {
                        case FileDataType.Boolean:
                            int sqlIndex = userFlagSqlIndices[userColumn.DataIndex];
                            file.UserFlags[userColumn.DataIndex] = reader.GetBoolean(sqlIndex);
                            break;
                        case FileDataType.ByteArray:
                            sqlIndex = userMarkerPositionSqlIndices[userColumn.DataIndex];
                            byte[] value;
                            if (reader.IsDBNull(sqlIndex))
                            {
                                value = null;
                            }
                            else
                            {
                                byte[] packedFloats = (byte[])reader.GetValue(sqlIndex);
                                if (packedFloats.Length > 0)
                                {
                                    value = packedFloats;
                                }
                                else
                                {
                                    value = null;
                                }
                            }
                            file.UserMarkerPositions[userColumn.DataIndex] = value;
                            break;
                        case FileDataType.Integer:
                            sqlIndex = userCounterSqlIndices[userColumn.DataIndex];
                            file.UserCounters[userColumn.DataIndex] = reader.GetInt32(sqlIndex);
                            break;
                        case FileDataType.String:
                            sqlIndex = userNoteAndChoiceSqlIndices[userColumn.DataIndex];
                            file.UserNotesAndChoices[userColumn.DataIndex] = reader.GetString(sqlIndex);
                            break;
                        default:
                            throw new NotSupportedException(String.Format("Unhandled column data type {0}.", userColumn.DataIndex));
                    }
                }
                file.AcceptChanges();
            }
        }

        public void SetUserControls(ControlTable controls)
        {
            this.UserColumnsByName.Clear();
            this.UserCounters = 0;
            this.UserFlags = 0;
            this.UserNotesAndChoices = 0;

            foreach (ControlRow control in controls)
            {
                if (control.IsUserControl())
                {
                    this.UserColumnsByName.Add(control.DataLabel, new FileTableUserColumn(control));
                    switch (control.Type)
                    {
                        case ControlType.Counter:
                            string markerColumnName = FileTable.GetMarkerPositionColumnName(control.DataLabel);
                            FileTableUserColumn markerColumn = new FileTableUserColumn(control)
                            {
                                DataType = FileDataType.ByteArray
                            };
                            this.UserColumnsByName.Add(markerColumnName, markerColumn);
                            ++this.UserCounters;
                            break;
                        case ControlType.FixedChoice:
                        case ControlType.Note:
                            ++this.UserNotesAndChoices;
                            break;
                        case ControlType.Flag:
                            ++this.UserFlags;
                            break;
                        case ControlType.DateTime:
                        case ControlType.UtcOffset:
                        default:
                            throw new NotSupportedException(String.Format("Unhandled control type {0}.", control.Type));
                    }
                }
            }
        }

        public bool TryGetPreviousFile(int fileIndex, out ImageRow previousFile)
        {
            if (fileIndex > 1)
            {
                previousFile = this[fileIndex - 1];
                return true;
            }

            previousFile = null;
            return false;
        }

        public bool TryFind(long id, out ImageRow file, out int fileIndex)
        {
            for (fileIndex = 0; fileIndex < this.Rows.Count; ++fileIndex)
            {
                file = this.Rows[fileIndex];
                if (file.ID == id)
                {
                    return true;
                }
            }
            file = null;
            return false;
        }

        private class UserControlIndices
        {
            public int SqlColumnIndex { get; private set; }
            public int UserControlArrayIndex { get; private set; }

            public UserControlIndices(int sqlColumnIndex, int userControlArrayIndex)
            {
                this.SqlColumnIndex = sqlColumnIndex;
                this.UserControlArrayIndex = userControlArrayIndex;
            }
        }
    }
}
