using System;

namespace Carnassial.Database
{
    public abstract class FileTableColumn
    {
        protected FileTableColumn(ControlRow control)
        {
            this.ControlType = control.Type;
            this.DataLabel = control.DataLabel;
        }

        public string ControlType { get; private set; }

        public string DataLabel { get; private set; }

        public abstract bool IsContentValid(string content);

        public static FileTableColumn Create(ControlRow control)
        {
            switch (control.Type)
            {
                case Constants.Control.Note:
                case Constants.DatabaseColumn.File:
                case Constants.DatabaseColumn.Folder:
                case Constants.DatabaseColumn.RelativePath:
                    return new FileTableNoteColumn(control);
                case Constants.DatabaseColumn.ImageQuality:
                    return new FileTableChoiceColumn(control);
                case Constants.Control.Counter:
                    return new FileTableCounterColumn(control);
                case Constants.DatabaseColumn.DateTime:
                    return new FileTableDateTimeColumn(control);
                case Constants.DatabaseColumn.DeleteFlag:
                case Constants.Control.Flag:
                    return new FileTableFlagColumn(control);
                case Constants.Control.FixedChoice:
                    return new FileTableChoiceColumn(control);
                case Constants.DatabaseColumn.UtcOffset:
                    return new FileTableUtcOffsetColumn(control);
                default:
                    throw new NotSupportedException(String.Format("Unhandled control type {0}.", control.Type));
            }
        }
    }
}
