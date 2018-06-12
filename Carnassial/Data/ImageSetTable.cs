using Carnassial.Database;
using System;
using System.Data;
using System.Data.SQLite;

namespace Carnassial.Data
{
    internal class ImageSetTable : SQLiteTable<ImageSetRow>
    {
        public override void Load(SQLiteDataReader reader)
        {
            this.Rows.Clear();

            int fileSelectionIndex = -1;
            int idIndex = -1;
            int initialFolderIndex = -1;
            int logIndex = -1;
            int mostRecentFileIndex = -1;
            int optionsIndex = -1;
            int timeZoneIndex = -1;
            for (int column = 0; column < reader.FieldCount; ++column)
            {
                string columnName = reader.GetName(column);
                switch (columnName)
                {
                    case Constant.DatabaseColumn.FileSelection:
                        fileSelectionIndex = column;
                        break;
                    case Constant.DatabaseColumn.ID:
                        idIndex = column;
                        break;
                    case Constant.DatabaseColumn.InitialFolderName:
                        initialFolderIndex = column;
                        break;
                    case Constant.DatabaseColumn.Log:
                        logIndex = column;
                        break;
                    case Constant.DatabaseColumn.MostRecentFileID:
                        mostRecentFileIndex = column;
                        break;
                    case Constant.DatabaseColumn.Options:
                        optionsIndex = column;
                        break;
                    case Constant.DatabaseColumn.TimeZone:
                        timeZoneIndex = column;
                        break;
                    default:
                        throw new NotSupportedException(String.Format("Unhandled column '{0}' in {1} table schema.", columnName, reader.GetTableName(0)));
                }
            }

            bool allStandardColumnsPresent = (fileSelectionIndex != -1) &&
                                             (idIndex != -1) &&
                                             (initialFolderIndex != -1) &&
                                             (logIndex != -1) &&
                                             (mostRecentFileIndex != -1) &&
                                             (optionsIndex != -1) &&
                                             (timeZoneIndex != -1);
            if (allStandardColumnsPresent == false)
            {
                throw new SQLiteException(SQLiteErrorCode.Schema, "At least one standard column is missing from table " + reader.GetTableName(0));
            }

            while (reader.Read())
            {
                // read file values
                IDataRecord row = (IDataRecord)reader;
                ImageSetRow imageSet = new ImageSetRow()
                {
                    FileSelection = (FileSelection)Enum.Parse(typeof(FileSelection), row.GetString(fileSelectionIndex)),
                    ID = row.GetInt64(idIndex),
                    InitialFolderName = row.GetString(initialFolderIndex),
                    Log = row.GetString(logIndex),
                    MostRecentFileID = row.GetInt64(mostRecentFileIndex),
                    Options = (ImageSetOptions)Enum.Parse(typeof(ImageSetOptions), row.GetString(optionsIndex)),
                    TimeZone = row.GetString(timeZoneIndex)
                };
                imageSet.AcceptChanges();

                this.Rows.Add(imageSet);
            }
        }
    }
}
