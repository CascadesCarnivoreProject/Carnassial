using Carnassial.Database;
using System;

namespace Carnassial.Data
{
    public class FileTableColumn
    {
        public ControlRow Control { get; private set; }
        public int DataIndex { get; set; }
        public SqlDataType DataType { get; set; }

        public FileTableColumn(ControlRow control)
        {
            this.Control = control;
            this.DataIndex = -1;

            switch (control.Type)
            {
                case ControlType.Counter:
                    this.DataType = SqlDataType.Integer;
                    // FileDataType.ByteArray is set by FileTable when needed
                    break;
                case ControlType.FixedChoice:
                case ControlType.Note:
                    this.DataType = SqlDataType.String;
                    break;
                case ControlType.Flag:
                    this.DataType = SqlDataType.Boolean;
                    break;
                case ControlType.DateTime:
                    this.DataType = SqlDataType.DateTime;
                    break;
                case ControlType.UtcOffset:
                    this.DataType = SqlDataType.Real;
                    break;
                default:
                    throw new NotSupportedException(String.Format("Unhandled control type {0}.", control.Type));
            }
        }
    }
}