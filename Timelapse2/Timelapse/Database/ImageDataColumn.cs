using System;
using System.Data;

namespace Timelapse.Database
{
    public abstract class ImageDataColumn
    {
        protected ImageDataColumn(DataRow templateTableRow)
        {
            this.ControlType = templateTableRow.GetStringField(Constants.Control.Type);
            this.DataLabel = templateTableRow.GetStringField(Constants.Control.DataLabel);
        }

        public string ControlType { get; private set; }

        public string DataLabel { get; private set; }

        public abstract bool IsContentValid(string content);

        public static ImageDataColumn Create(DataRow templateTableRow)
        {
            string controlType = templateTableRow.GetStringField(Constants.Control.Type);
            switch (controlType)
            {
                case Constants.DatabaseColumn.Folder:
                case Constants.DatabaseColumn.RelativePath:
                case Constants.DatabaseColumn.File:
                case Constants.DatabaseColumn.Date:
                case Constants.DatabaseColumn.Time:
                case Constants.Control.Note:
                    return new ImageDataNoteColumn(templateTableRow);
                case Constants.DatabaseColumn.ImageQuality:
                    return new ImageDataChoiceColumn(templateTableRow);
                case Constants.Control.Counter:
                    return new ImageDataCounterColumn(templateTableRow);
                case Constants.Control.DeleteFlag:
                case Constants.Control.Flag:
                    return new ImageDataFlagColumn(templateTableRow);
                case Constants.Control.FixedChoice:
                    return new ImageDataChoiceColumn(templateTableRow);
                default:
                    throw new NotSupportedException(String.Format("Unhandled control type {0}.", controlType));
            }
        }
    }
}
