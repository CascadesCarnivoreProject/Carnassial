using Carnassial.Database;
using Carnassial.Util;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;

namespace Carnassial.Data
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

        public ControlType Type
        {
            get { return this.Row.GetEnumField<ControlType>(Constant.Control.Type); }
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

        public static ColumnTuplesForInsert CreateInsert(IEnumerable<ControlRow> controls)
        {
            ColumnTuplesForInsert controlTuples = new ColumnTuplesForInsert(Constant.DatabaseTable.Controls,
                                                                            Constant.Control.ControlOrder,
                                                                            Constant.Control.Copyable,
                                                                            Constant.Control.DataLabel,
                                                                            Constant.Control.DefaultValue,
                                                                            Constant.Control.Label,
                                                                            Constant.Control.List,
                                                                            Constant.Control.SpreadsheetOrder,
                                                                            Constant.Control.Width,
                                                                            Constant.Control.Tooltip,
                                                                            Constant.Control.Type,
                                                                            Constant.Control.Visible);
            foreach (ControlRow control in controls)
            {
                Debug.Assert(control != null, "controls contains null.");
                controlTuples.Add(control.ControlOrder,
                                  control.Copyable ? Boolean.TrueString : Boolean.FalseString,
                                  control.DataLabel,
                                  control.DefaultValue,
                                  control.Label,
                                  control.List,
                                  control.SpreadsheetOrder,
                                  control.Width,
                                  control.Tooltip,
                                  control.Type.ToString(),
                                  control.Visible ? Boolean.TrueString : Boolean.FalseString);
            }

            return controlTuples;
        }

        public ColumnTuplesWithID CreateUpdate()
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
            columnTuples.Add(new ColumnTuple(Constant.Control.Type, this.Type.ToString()));
            columnTuples.Add(new ColumnTuple(Constant.Control.Visible, this.Visible));
            return new ColumnTuplesWithID(Constant.DatabaseTable.Controls, columnTuples, this.ID);
        }

        public List<string> GetChoices()
        {
            return this.List.Split(ControlRow.BarDelimiter).ToList();
        }

        public bool IsFilePathComponent()
        {
            return String.Equals(this.DataLabel, Constant.DatabaseColumn.File, StringComparison.OrdinalIgnoreCase) || String.Equals(this.DataLabel, Constant.DatabaseColumn.RelativePath, StringComparison.OrdinalIgnoreCase);
        }

        public bool IsValidData(string value)
        {
            switch (this.Type)
            {
                case ControlType.Counter:
                    long result;
                    return Int64.TryParse(value, out result);
                case ControlType.DateTime:
                    DateTime dateTime;
                    return DateTimeHandler.TryParseDatabaseDateTime(value, out dateTime);
                case ControlType.Flag:
                    return String.Equals(value, Boolean.FalseString, StringComparison.OrdinalIgnoreCase) ||
                           String.Equals(value, Boolean.TrueString, StringComparison.OrdinalIgnoreCase);
                case ControlType.FixedChoice:
                    // the editor doesn't currently enforce the default value is one of the choices, so accept it as valid independently
                    if (value == this.DefaultValue)
                    {
                        return true;
                    }
                    return this.GetChoices().Contains(value);
                case ControlType.Note:
                    return true;
                case ControlType.UtcOffset:
                    double utcOffset;
                    return double.TryParse(value, out utcOffset);
                default:
                    throw new NotSupportedException(String.Format("Unhandled control type {0}.", this.Type));
            }
        }

        public void SetChoices(List<string> choices)
        {
            this.List = String.Join("|", choices);
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
