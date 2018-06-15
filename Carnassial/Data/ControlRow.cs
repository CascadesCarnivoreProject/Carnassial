using Carnassial.Database;
using Carnassial.Util;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;

namespace Carnassial.Data
{
    public class ControlRow : SQLiteRow, INotifyPropertyChanged
    {
        private bool analysisLabel;
        private long controlOrder;
        private bool copyable;
        private string dataLabel;
        private string defaultValue;
        private string label;
        private string list;
        private long maxWidth;
        private long spreadSheetOrder;
        private string tooltip;
        private ControlType type;
        private bool visible;

        public ControlRow()
        {
            this.analysisLabel = false;
            this.controlOrder = -1;
            this.copyable = false;
            this.dataLabel = null;
            this.defaultValue = null;
            this.label = null;
            this.list = null;
            this.maxWidth = -1;
            this.spreadSheetOrder = -1;
            this.tooltip = null;
            this.type = (ControlType)(-1);
            this.visible = false;
        }

        public ControlRow(ControlType controlType, string dataLabel, long controlOrder)
            : this()
        {
            this.analysisLabel = false;
            this.controlOrder = controlOrder;
            this.copyable = true;
            this.dataLabel = dataLabel;
            this.defaultValue = Constant.ControlDefault.Value;
            this.label = dataLabel;
            this.list = Constant.ControlDefault.Value;
            this.maxWidth = Constant.ControlDefault.MaxWidth;
            this.spreadSheetOrder = controlOrder;
            this.tooltip = null;
            this.type = controlType;
            this.visible = true;

            switch (controlType)
            {
                case ControlType.Counter:
                    this.DefaultValue = Constant.ControlDefault.CounterValue;
                    this.Copyable = false;
                    this.Tooltip = Constant.ControlDefault.CounterTooltip;
                    break;
                case ControlType.DateTime:
                    this.Copyable = false;
                    this.DefaultValue = DateTimeHandler.ToDatabaseDateTimeString(Constant.ControlDefault.DateTimeValue.UtcDateTime);
                    this.Tooltip = Constant.ControlDefault.DateTimeTooltip;
                    break;
                case ControlType.Note:
                    this.Tooltip = Constant.ControlDefault.NoteTooltip;
                    break;
                case ControlType.FixedChoice:
                    this.Tooltip = Constant.ControlDefault.FixedChoiceTooltip;
                    break;
                case ControlType.Flag:
                    this.DefaultValue = Constant.ControlDefault.FlagValue;
                    this.Tooltip = Constant.ControlDefault.FlagTooltip;
                    break;
                case ControlType.UtcOffset:
                    this.Copyable = false;
                    this.DefaultValue = DateTimeHandler.ToDatabaseUtcOffsetString(Constant.ControlDefault.DateTimeValue.Offset);
                    this.Tooltip = Constant.ControlDefault.UtcOffsetTooltip;
                    this.Visible = false;
                    break;
                default:
                    throw new NotSupportedException(String.Format("Unhandled control type {0}.", controlType));
            }
        }

        public bool AnalysisLabel
        {
            get
            {
                return this.analysisLabel;
            }
            set
            {
                this.HasChanges |= this.analysisLabel != value;
                this.analysisLabel = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.AnalysisLabel)));
            }
        }

        public long ControlOrder
        {
            get
            {
                return this.controlOrder;
            }
            set
            {
                this.HasChanges |= this.controlOrder != value;
                this.controlOrder = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.ControlOrder)));
            }
        }

        public bool Copyable
        {
            get
            {
                return this.copyable;
            }
            set
            {
                this.HasChanges |= this.copyable != value;
                this.copyable = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.Copyable)));
            }
        }

        public string DataLabel
        {
            get
            {
                return this.dataLabel;
            }
            set
            {
                this.HasChanges |= this.dataLabel != value;
                this.dataLabel = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.DataLabel)));
            }
        }

        public string DefaultValue
        {
            get
            {
                return this.defaultValue;
            }
            set
            {
                this.HasChanges |= this.defaultValue != value;
                this.defaultValue = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.DefaultValue)));
            }
        }

        public string Label
        {
            get
            {
                return this.label;
            }
            set
            {
                this.HasChanges |= this.label != value;
                this.label = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.Label)));
            }
        }

        public string List
        {
            get
            {
                return this.list;
            }
            set
            {
                this.HasChanges |= this.list != value;
                this.list = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.List)));
            }
        }

        public long MaxWidth
        {
            get
            {
                return this.maxWidth;
            }
            set
            {
                this.HasChanges |= this.maxWidth != value;
                this.maxWidth = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.MaxWidth)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public long SpreadsheetOrder
        {
            get
            {
                return this.spreadSheetOrder;
            }
            set
            {
                this.HasChanges |= this.spreadSheetOrder != value;
                this.spreadSheetOrder = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.SpreadsheetOrder)));
            }
        }

        public string Tooltip
        {
            get
            {
                return this.tooltip;
            }
            set
            {
                this.HasChanges |= this.tooltip != value;
                this.tooltip = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.Tooltip)));
            }
        }

        public ControlType Type
        {
            get
            {
                return this.type;
            }
            set
            {
                this.HasChanges |= this.type != value;
                this.type = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.Type)));
            }
        }

        public bool Visible
        {
            get
            {
                return this.visible;
            }
            set
            {
                this.HasChanges |= this.visible != value;
                this.visible = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.Visible)));
            }
        }

        public static ColumnTuplesForInsert CreateInsert(params ControlRow[] controls)
        {
            return ControlRow.CreateInsert((IEnumerable<ControlRow>)controls);
        }

        public static ColumnTuplesForInsert CreateInsert(IEnumerable<ControlRow> controls)
        {
            ColumnTuplesForInsert controlTuples = new ColumnTuplesForInsert(Constant.DatabaseTable.Controls,
                                                                            Constant.Control.AnalysisLabel,
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
                controlTuples.Add(control.AnalysisLabel ? 1.ToString() : 0.ToString(),
                                  control.ControlOrder,
                                  control.Copyable ? Boolean.TrueString : Boolean.FalseString,
                                  control.DataLabel,
                                  control.DefaultValue,
                                  control.Label,
                                  control.List,
                                  control.SpreadsheetOrder,
                                  control.MaxWidth,
                                  control.Tooltip,
                                  ControlRow.TypeToString(control.Type),
                                  control.Visible ? Boolean.TrueString : Boolean.FalseString);
            }

            return controlTuples;
        }

        public ColumnTuplesWithID CreateUpdate()
        {
            List<ColumnTuple> columnTuples = new List<ColumnTuple>()
            {
                new ColumnTuple(Constant.Control.AnalysisLabel, this.AnalysisLabel ? 1 : 0),
                new ColumnTuple(Constant.Control.ControlOrder, this.ControlOrder),
                new ColumnTuple(Constant.Control.Copyable, this.Copyable),
                new ColumnTuple(Constant.Control.DataLabel, this.DataLabel),
                new ColumnTuple(Constant.Control.DefaultValue, this.DefaultValue),
                new ColumnTuple(Constant.Control.Label, this.Label),
                new ColumnTuple(Constant.Control.List, this.List),
                new ColumnTuple(Constant.Control.SpreadsheetOrder, this.SpreadsheetOrder),
                new ColumnTuple(Constant.Control.Width, this.MaxWidth),
                new ColumnTuple(Constant.Control.Tooltip, this.Tooltip),
                new ColumnTuple(Constant.Control.Type, ControlRow.TypeToString(this.Type)),
                new ColumnTuple(Constant.Control.Visible, this.Visible)
            };
            return new ColumnTuplesWithID(Constant.DatabaseTable.Controls, columnTuples, this.ID);
        }

        public List<string> GetChoices()
        {
            return this.List.Split(Constant.Database.BarDelimiter).ToList();
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
            if (this.AnalysisLabel != other.AnalysisLabel)
            {
                this.AnalysisLabel = other.AnalysisLabel;
                synchronizationMadeChanges = true;
            }
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
            if (this.MaxWidth != other.MaxWidth)
            {
                this.MaxWidth = other.MaxWidth;
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

            return synchronizationMadeChanges;
        }

        private static string TypeToString(ControlType type)
        {
            // Enum.ToString() is indeterminate as to which string is returned for equivalent values
            switch (type)
            {
                case ControlType.Counter:
                    return "Counter";
                case ControlType.DateTime:
                    return "DateTime";
                case ControlType.FixedChoice:
                    return "FixedChoice";
                case ControlType.Flag:
                    return "Flag";
                case ControlType.Note:
                    return "Note";
                case ControlType.UtcOffset:
                    return "UtcOffset";
                default:
                    throw new NotSupportedException(String.Format("Unhandled control type {0}.", type));
            }
        }
    }
}
