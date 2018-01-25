using Carnassial.Database;
using System;
using System.Collections.Generic;
using System.Data;

namespace Carnassial.Data
{
    public class ImageSetRow : DataRowBackedObject
    {
        public ImageSetRow(DataRow row)
            : base(row)
        {
        }

        public FileSelection FileSelection
        {
            get { return this.Row.GetEnumField<FileSelection>(Constant.DatabaseColumn.FileSelection); }
            set { this.Row.SetField(Constant.DatabaseColumn.FileSelection, value); }
        }

        public string InitialFolderName
        {
            get { return this.Row.GetStringField(Constant.DatabaseColumn.InitialFolderName); }
            set { this.Row.SetField(Constant.DatabaseColumn.InitialFolderName, value); }
        }

        public long MostRecentFileID
        {
            get { return this.Row.GetLongField(Constant.DatabaseColumn.MostRecentFileID); }
            set { this.Row.SetField(Constant.DatabaseColumn.MostRecentFileID, value); }
        }

        public string Log
        {
            get { return this.Row.GetStringField(Constant.DatabaseColumn.Log); }
            set { this.Row.SetField(Constant.DatabaseColumn.Log, value); }
        }

        public ImageSetOptions Options
        {
            get { return this.Row.GetEnumField<ImageSetOptions>(Constant.DatabaseColumn.Options); }
            set { this.Row.SetField(Constant.DatabaseColumn.Options, value); }
        }

        public string TimeZone
        {
            get { return this.Row.GetStringField(Constant.DatabaseColumn.TimeZone); }
            set { this.Row.SetField(Constant.DatabaseColumn.TimeZone, value); }
        }

        public static ColumnTuplesForInsert CreateInsert(string folderPath)
        {
            return new ColumnTuplesForInsert(Constant.DatabaseTable.ImageSet,
                new List<ColumnTuple>()
                {
                    new ColumnTuple(Constant.DatabaseColumn.FileSelection, FileSelection.All.ToString()),
                    new ColumnTuple(Constant.DatabaseColumn.InitialFolderName, folderPath),
                    new ColumnTuple(Constant.DatabaseColumn.Log, Constant.Database.ImageSetDefaultLog),
                    new ColumnTuple(Constant.DatabaseColumn.MostRecentFileID, Constant.Database.DefaultFileID),
                    new ColumnTuple(Constant.DatabaseColumn.Options, ImageSetOptions.None.ToString()),
                    new ColumnTuple(Constant.DatabaseColumn.TimeZone, TimeZoneInfo.Local.Id)
                });
        }

        public ColumnTuplesWithID CreateUpdate()
        {
            return new ColumnTuplesWithID(Constant.DatabaseTable.ImageSet,
                new List<ColumnTuple>()
                {
                    new ColumnTuple(Constant.DatabaseColumn.FileSelection, this.FileSelection.ToString()),
                    new ColumnTuple(Constant.DatabaseColumn.InitialFolderName, this.InitialFolderName),
                    new ColumnTuple(Constant.DatabaseColumn.Log, this.Log),
                    new ColumnTuple(Constant.DatabaseColumn.Options, this.Options.ToString()),
                    new ColumnTuple(Constant.DatabaseColumn.MostRecentFileID, this.MostRecentFileID),
                    new ColumnTuple(Constant.DatabaseColumn.TimeZone, this.TimeZone),
                },
                this.ID);
        }

        public TimeZoneInfo GetTimeZoneInfo()
        {
            return TimeZoneInfo.FindSystemTimeZoneById(this.TimeZone);
        }
    }
}
