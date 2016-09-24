using System;
using System.Data;

namespace Carnassial.Database
{
    public abstract class ImageDataColumn
    {
        protected ImageDataColumn(ControlRow control)
        {
            this.ControlType = control.Type;
            this.DataLabel = control.DataLabel;
        }

        public string ControlType { get; private set; }

        public string DataLabel { get; private set; }

        public abstract bool IsContentValid(string content);

        public static ImageDataColumn Create(ControlRow control)
        {
            switch (control.Type)
            {
                case Constants.Control.Note:
                case Constants.DatabaseColumn.Date:
                case Constants.DatabaseColumn.File:
                case Constants.DatabaseColumn.Folder:
                case Constants.DatabaseColumn.RelativePath:
                case Constants.DatabaseColumn.Time:
                    return new ImageDataNoteColumn(control);
                case Constants.DatabaseColumn.ImageQuality:
                    return new ImageDataChoiceColumn(control);
                case Constants.Control.Counter:
                    return new ImageDataCounterColumn(control);
                case Constants.DatabaseColumn.DateTime:
                    return new ImageDataDateTimeColumn(control);
                case Constants.DatabaseColumn.DeleteFlag:
                case Constants.Control.Flag:
                    return new ImageDataFlagColumn(control);
                case Constants.Control.FixedChoice:
                    return new ImageDataChoiceColumn(control);
                case Constants.DatabaseColumn.UtcOffset:
                    return new ImageDataUtcOffsetColumn(control);
                default:
                    throw new NotSupportedException(String.Format("Unhandled control type {0}.", control.Type));
            }
        }
    }
}
