using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Carnassial.Database
{
    public class ControlRow : DataRowBackedObject
    {
        private static readonly char[] BarDelimiter = { '|' };

        public ControlRow(DataRow row)
            : base(row)
        {
        }

        public long ControlOrder
        {
            get { return this.Row.GetLongField(Constant.Control.ControlOrder); }
            set { this.Row.SetField(Constant.Control.ControlOrder, value); }
        }

        public bool Copyable
        {
            get { return this.Row.GetBooleanField(Constant.Control.Copyable); }
            set { this.Row.SetField(Constant.Control.Copyable, value); }
        }

        public string DataLabel
        {
            get { return this.Row.GetStringField(Constant.Control.DataLabel); }
            set { this.Row.SetField(Constant.Control.DataLabel, value); }
        }

        public string DefaultValue
        {
            get { return this.Row.GetStringField(Constant.Control.DefaultValue); }
            set { this.Row.SetField(Constant.Control.DefaultValue, value); }
        }

        public string Label
        {
            get { return this.Row.GetStringField(Constant.Control.Label); }
            set { this.Row.SetField(Constant.Control.Label, value); }
        }

        public string List
        {
            get { return this.Row.GetStringField(Constant.Control.List); }
            set { this.Row.SetField(Constant.Control.List, value); }
        }

        public long SpreadsheetOrder
        {
            get { return this.Row.GetLongField(Constant.Control.SpreadsheetOrder); }
            set { this.Row.SetField(Constant.Control.SpreadsheetOrder, value); }
        }

        public string Tooltip
        {
            get { return this.Row.GetStringField(Constant.Control.Tooltip); }
            set { this.Row.SetField(Constant.Control.Tooltip, value); }
        }

        public string Type
        {
            get { return this.Row.GetStringField(Constant.Control.Type); }
            set { this.Row.SetField(Constant.Control.Type, value); }
        }

        public bool Visible
        {
            get { return this.Row.GetBooleanField(Constant.Control.Visible); }
            set { this.Row.SetField(Constant.Control.Visible, value); }
        }

        public long Width
        {
            get { return this.Row.GetLongField(Constant.Control.Width); }
            set { this.Row.SetField(Constant.Control.Width, value); }
        }

        public List<string> GetChoices()
        {
            return this.List.Split(ControlRow.BarDelimiter).ToList();
        }

        public override ColumnTuplesWithWhere GetColumnTuples()
        {
            List<ColumnTuple> columnTuples = new List<ColumnTuple>();
            columnTuples.Add(new ColumnTuple(Constant.Control.ControlOrder, this.ControlOrder));
            columnTuples.Add(new ColumnTuple(Constant.Control.Copyable, this.Copyable));
            columnTuples.Add(new ColumnTuple(Constant.Control.DataLabel, this.DataLabel));
            columnTuples.Add(new ColumnTuple(Constant.Control.DefaultValue, this.DefaultValue));
            columnTuples.Add(new ColumnTuple(Constant.Control.Label, this.Label));
            columnTuples.Add(new ColumnTuple(Constant.Control.List, this.List));
            columnTuples.Add(new ColumnTuple(Constant.Control.SpreadsheetOrder, this.SpreadsheetOrder));
            columnTuples.Add(new ColumnTuple(Constant.Control.Width, this.Width));
            columnTuples.Add(new ColumnTuple(Constant.Control.Tooltip, this.Tooltip));
            columnTuples.Add(new ColumnTuple(Constant.Control.Type, this.Type));
            columnTuples.Add(new ColumnTuple(Constant.Control.Visible, this.Visible));
            return new ColumnTuplesWithWhere(columnTuples, this.ID);
        }

        public void SetChoices(List<string> choices)
        {
            this.List = String.Join("|", choices);
        }

        public void SetValueFromDatabaseString(string dataLabel, string value)
        {
            switch (dataLabel)
            {
                case Constant.Control.ControlOrder:
                    this.ControlOrder = Int32.Parse(value);
                    break;
                case Constant.Control.Copyable:
                    this.Copyable = Boolean.Parse(value);
                    break;
                case Constant.Control.SpreadsheetOrder:
                    this.SpreadsheetOrder = Int32.Parse(value);
                    break;
                case Constant.Control.Visible:
                    this.Visible = Boolean.Parse(value);
                    break;
                case Constant.Control.Width:
                    this.Width = Int32.Parse(value);
                    break;
                default:
                    this.Row.SetField(dataLabel, value);
                    break;
            }
        }

        public bool Synchronize(ControlRow other)
        {
            bool synchronizationMadeChanges = false;
            if (this.Copyable != other.Copyable)
            {
                this.Copyable = other.Copyable;
                synchronizationMadeChanges = true;
            }
            if (this.ControlOrder != other.ControlOrder)
            {
                this.ControlOrder = other.ControlOrder;
                synchronizationMadeChanges = true;
            }
            if (this.DefaultValue != other.DefaultValue)
            {
                this.DefaultValue = other.DefaultValue;
                synchronizationMadeChanges = true;
            }
            if (this.Label != other.Label)
            {
                this.Label = other.Label;
                synchronizationMadeChanges = true;
            }
            if (this.List != other.List)
            {
                this.List = other.List;
                synchronizationMadeChanges = true;
            }
            if (this.SpreadsheetOrder != other.SpreadsheetOrder)
            {
                this.SpreadsheetOrder = other.SpreadsheetOrder;
                synchronizationMadeChanges = true;
            }
            if (this.Tooltip != other.Tooltip)
            {
                this.Tooltip = other.Tooltip;
                synchronizationMadeChanges = true;
            }
            if (this.Visible != other.Visible)
            {
                this.Visible = other.Visible;
                synchronizationMadeChanges = true;
            }
            if (this.Width != other.Width)
            {
                this.Width = other.Width;
                synchronizationMadeChanges = true;
            }

            return synchronizationMadeChanges;
        }
    }
}
