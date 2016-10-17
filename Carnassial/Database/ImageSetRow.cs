using System;
using System.Collections.Generic;
using System.Data;

namespace Carnassial.Database
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

        public override ColumnTuplesWithWhere GetColumnTuples()
        {
            List<ColumnTuple> columnTuples = new List<ColumnTuple>();
            columnTuples.Add(new ColumnTuple(Constant.DatabaseColumn.FileSelection, this.FileSelection.ToString()));
            columnTuples.Add(new ColumnTuple(Constant.DatabaseColumn.InitialFolderName, this.InitialFolderName));
            columnTuples.Add(new ColumnTuple(Constant.DatabaseColumn.Log, this.Log));
            columnTuples.Add(new ColumnTuple(Constant.DatabaseColumn.Options, this.Options.ToString()));
            columnTuples.Add(new ColumnTuple(Constant.DatabaseColumn.MostRecentFileID, this.MostRecentFileID));
            columnTuples.Add(new ColumnTuple(Constant.DatabaseColumn.TimeZone, this.TimeZone));
            return new ColumnTuplesWithWhere(columnTuples, this.ID);
        }

        public TimeZoneInfo GetTimeZone()
        {
            return TimeZoneInfo.FindSystemTimeZoneById(this.TimeZone);
        }
    }
}
