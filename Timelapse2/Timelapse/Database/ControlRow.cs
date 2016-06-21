using System.Collections.Generic;
using System.Data;

namespace Timelapse.Database
{
    public class ControlRow : DataRowBackedObject
    {
        public ControlRow(DataRow row)
            : base(row)
        {
        }

        public long ControlOrder
        {
            get { return this.Row.GetLongField(Constants.Control.ControlOrder); }
            set { this.Row.SetField(Constants.Control.ControlOrder, value); }
        }

        public bool Copyable
        {
            get { return this.Row.GetBooleanField(Constants.Control.Copyable); }
            set { this.Row.SetField(Constants.Control.Copyable, value); }
        }

        public string DataLabel
        {
            get { return this.Row.GetStringField(Constants.Control.DataLabel); }
            set { this.Row.SetField(Constants.Control.DataLabel, value); }
        }

        public string DefaultValue
        {
            get { return this.Row.GetStringField(Constants.Control.DefaultValue); }
            set { this.Row.SetField(Constants.Control.DefaultValue, value); }
        }

        public string Label
        {
            get { return this.Row.GetStringField(Constants.Control.Label); }
            set { this.Row.SetField(Constants.Control.Label, value); }
        }

        public string List
        {
            get { return this.Row.GetStringField(Constants.Control.List); }
            set { this.Row.SetField(Constants.Control.List, value); }
        }

        public long SpreadsheetOrder
        {
            get { return this.Row.GetLongField(Constants.Control.SpreadsheetOrder); }
            set { this.Row.SetField(Constants.Control.SpreadsheetOrder, value); }
        }

        public int TextBoxWidth
        {
            get { return this.Row.GetIntegerField(Constants.Control.TextBoxWidth); }
            set { this.Row.SetField(Constants.Control.TextBoxWidth, value); }
        }

        public string Tooltip
        {
            get { return this.Row.GetStringField(Constants.Control.Tooltip); }
            set { this.Row.SetField(Constants.Control.Tooltip, value); }
        }

        public string Type
        {
            get { return this.Row.GetStringField(Constants.Control.Type); }
            set { this.Row.SetField(Constants.Control.Type, value); }
        }

        public bool Visible
        {
            get { return this.Row.GetBooleanField(Constants.Control.Visible); }
            set { this.Row.SetField(Constants.Control.Visible, value); }
        }

        public override ColumnTuplesWithWhere GetColumnTuples()
        {
            List<ColumnTuple> columnTuples = new List<ColumnTuple>();
            columnTuples.Add(new ColumnTuple(Constants.Control.ControlOrder, this.ControlOrder));
            columnTuples.Add(new ColumnTuple(Constants.Control.Copyable, this.Copyable));
            columnTuples.Add(new ColumnTuple(Constants.Control.DataLabel, this.DataLabel));
            columnTuples.Add(new ColumnTuple(Constants.Control.DefaultValue, this.DefaultValue));
            columnTuples.Add(new ColumnTuple(Constants.Control.Label, this.Label));
            columnTuples.Add(new ColumnTuple(Constants.Control.List, this.List));
            columnTuples.Add(new ColumnTuple(Constants.Control.SpreadsheetOrder, this.SpreadsheetOrder));
            columnTuples.Add(new ColumnTuple(Constants.Control.TextBoxWidth, this.TextBoxWidth));
            columnTuples.Add(new ColumnTuple(Constants.Control.Tooltip, this.Tooltip));
            columnTuples.Add(new ColumnTuple(Constants.Control.Type, this.Type));
            columnTuples.Add(new ColumnTuple(Constants.Control.Visible, this.Visible));
            return new ColumnTuplesWithWhere(columnTuples, this.ID);
        }
    }
}
