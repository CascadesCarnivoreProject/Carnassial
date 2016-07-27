using System;
using System.Data;

namespace Timelapse.Database
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
                case Constants.DatabaseColumn.Folder:
                case Constants.DatabaseColumn.RelativePath:
                case Constants.DatabaseColumn.File:
                case Constants.DatabaseColumn.Date:
                case Constants.DatabaseColumn.Time:
                case Constants.Control.Note:
                    return new ImageDataNoteColumn(control);
                case Constants.DatabaseColumn.ImageQuality:
                    return new ImageDataChoiceColumn(control);
                case Constants.Control.Counter:
                    return new ImageDataCounterColumn(control);
                case Constants.Control.DeleteFlag:
                case Constants.Control.Flag:
                    return new ImageDataFlagColumn(control);
                case Constants.Control.FixedChoice:
                    return new ImageDataChoiceColumn(control);
                default:
                    throw new NotSupportedException(String.Format("Unhandled control type {0}.", control.Type));
            }
        }
    }
}
