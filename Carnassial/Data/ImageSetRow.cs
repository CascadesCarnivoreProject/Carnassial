using Carnassial.Database;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Carnassial.Data
{
    public class ImageSetRow : SQLiteRow, INotifyPropertyChanged
    {
        private FileSelection fileSelection;
        private string initialFolderName;
        private long mostRecentFileID;
        private string log;
        private ImageSetOptions options;
        private string timeZone;

        public ImageSetRow()
        {
            this.fileSelection = FileSelection.All;
            this.initialFolderName = null;
            this.mostRecentFileID = Constant.Database.InvalidID;
            this.log = null;
            this.options = ImageSetOptions.None;
            this.timeZone = null;
        }

        public FileSelection FileSelection
        {
            get
            {
                return this.fileSelection;
            }
            set
            {
                this.HasChanges |= this.fileSelection != value;
                this.fileSelection = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.FileSelection)));
            }
        }

        public string InitialFolderName
        {
            get
            {
                return this.initialFolderName;
            }
            set
            {
                this.HasChanges |= String.Equals(this.initialFolderName, value, StringComparison.Ordinal) == false;
                this.initialFolderName = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.InitialFolderName)));
            }
        }

        public long MostRecentFileID
        {
            get
            {
                return this.mostRecentFileID;
            }
            set
            {
                this.HasChanges |= this.mostRecentFileID != value;
                this.mostRecentFileID = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.MostRecentFileID)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string Log
        {
            get
            {
                return this.log;
            }
            set
            {
                this.HasChanges |= String.Equals(this.log, value, StringComparison.Ordinal) == false;
                this.log = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.MostRecentFileID)));
            }
        }

        public ImageSetOptions Options
        {
            get
            {
                return this.options;
            }
            set
            {
                this.HasChanges |= this.options != value;
                this.options = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.Options)));
            }
        }

        public string TimeZone
        {
            get
            {
                return this.timeZone;
            }
            set
            {
                this.HasChanges |= String.Equals(this.timeZone, value, StringComparison.Ordinal) == false;
                this.timeZone = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.TimeZone)));
            }
        }

        public static ColumnTuplesForInsert CreateInsert(string folderPath)
        {
            return new ColumnTuplesForInsert(Constant.DatabaseTable.ImageSet,
                new List<ColumnTuple>()
                {
                    new ColumnTuple(Constant.ImageSetColumn.FileSelection, (int)default(FileSelection)),
                    new ColumnTuple(Constant.ImageSetColumn.InitialFolderName, folderPath),
                    new ColumnTuple(Constant.ImageSetColumn.Log, Constant.Database.ImageSetDefaultLog),
                    new ColumnTuple(Constant.ImageSetColumn.MostRecentFileID, Constant.Database.DefaultFileID),
                    new ColumnTuple(Constant.ImageSetColumn.Options, (int)default(ImageSetOptions)),
                    new ColumnTuple(Constant.ImageSetColumn.TimeZone, TimeZoneInfo.Local.Id)
                });
        }

        public ColumnTuplesWithID CreateUpdate()
        {
            return new ColumnTuplesWithID(Constant.DatabaseTable.ImageSet,
                new List<ColumnTuple>()
                {
                    new ColumnTuple(Constant.ImageSetColumn.FileSelection, (int)this.FileSelection),
                    new ColumnTuple(Constant.ImageSetColumn.InitialFolderName, this.InitialFolderName),
                    new ColumnTuple(Constant.ImageSetColumn.Log, this.Log),
                    new ColumnTuple(Constant.ImageSetColumn.Options, (int)this.Options),
                    new ColumnTuple(Constant.ImageSetColumn.MostRecentFileID, this.MostRecentFileID),
                    new ColumnTuple(Constant.ImageSetColumn.TimeZone, this.TimeZone),
                },
                this.ID);
        }

        public TimeZoneInfo GetTimeZoneInfo()
        {
            return TimeZoneInfo.FindSystemTimeZoneById(this.TimeZone);
        }
    }
}
