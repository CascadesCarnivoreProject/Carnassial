using Carnassial.Database;
using System;
using System.Diagnostics;

namespace Carnassial.Data
{
    public class FileTableColumn
    {
        public ControlRow Control { get; private set; }
        public int DataIndex { get; set; }
        public SqlDataType DataType { get; set; }
        public string ParameterName { get; private set; }
        public string QuotedName { get; private set; }

        public FileTableColumn(ControlRow control)
            : this(control.DataLabel, control)
        {
        }

        public FileTableColumn(string dataLabel, ControlRow control)
        {
            this.Control = control;
            this.DataIndex = -1;
            this.ParameterName = SQLiteDatabase.ToParameterName(dataLabel);
            this.QuotedName = SQLiteDatabase.QuoteIdentifier(dataLabel);

            switch (control.Type)
            {
                case ControlType.Counter:
                    if (String.Equals(dataLabel, control.DataLabel))
                    {
                        this.DataType = SqlDataType.Integer;
                    }
                    else
                    {
                        Debug.Assert(dataLabel.EndsWith(Constant.FileColumn.MarkerPositionSuffix, StringComparison.Ordinal));
                        this.DataType = SqlDataType.Blob;
                    }
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