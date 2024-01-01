using Carnassial.Database;
using System;
using System.Diagnostics;
using System.Globalization;

namespace Carnassial.Data
{
    public class FileTableColumn
    {
        public ControlRow Control { get; private init; }
        public int DataIndex { get; set; }
        public SqlDataType DataType { get; set; }
        public string ParameterName { get; private init; }
        public string QuotedName { get; private init; }

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

            switch (control.ControlType)
            {
                case ControlType.Counter:
                    if (String.Equals(dataLabel, control.DataLabel, StringComparison.Ordinal))
                    {
                        this.DataType = SqlDataType.Integer;
                    }
                    else
                    {
                        Debug.Assert(dataLabel.EndsWith(Constant.FileColumn.MarkerPositionSuffix, StringComparison.Ordinal), "Since data label and control data label don't match the data label is expected to indicate a marker position column.");
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
                    throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled control type {0}.", control.ControlType));
            }
        }
    }
}