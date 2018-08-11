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
        private int controlOrder;
        private bool copyable;
        private string dataLabel;
        private string defaultValue;
        private bool indexInFileTable;
        private string label;
        private int maxWidth;
        private int spreadSheetOrder;
        private string tooltip;
        private ControlType type;
        private bool visible;
        private string wellKnownValues;

        public ControlRow()
        {
            this.analysisLabel = false;
            this.controlOrder = -1;
            this.copyable = false;
            this.dataLabel = null;
            this.defaultValue = null;
            this.indexInFileTable = false;
            this.label = null;
            this.wellKnownValues = null;
            this.maxWidth = -1;
            this.spreadSheetOrder = -1;
            this.tooltip = null;
            this.type = (ControlType)(-1);
            this.visible = false;
        }

        public ControlRow(ControlType controlType, string dataLabel, int controlOrder)
            : this()
        {
            this.controlOrder = controlOrder;
            this.copyable = true;
            this.dataLabel = dataLabel;
            this.defaultValue = Constant.ControlDefault.Value;
            this.label = dataLabel;
            this.wellKnownValues = Constant.ControlDefault.Value;
            this.maxWidth = Constant.ControlDefault.MaxWidth;
            this.spreadSheetOrder = controlOrder;
            this.type = controlType;
            this.visible = true;

            switch (controlType)
            {
                case ControlType.Counter:
                    this.copyable = false;
                    this.defaultValue = Constant.ControlDefault.CounterValue;
                    this.tooltip = Constant.ControlDefault.CounterTooltip;
                    break;
                case ControlType.DateTime:
                    this.copyable = false;
                    this.defaultValue = DateTimeHandler.ToDatabaseDateTimeString(Constant.ControlDefault.DateTimeValue.UtcDateTime);
                    this.indexInFileTable = true;
                    this.tooltip = Constant.ControlDefault.DateTimeTooltip;
                    break;
                case ControlType.Note:
                    this.tooltip = Constant.ControlDefault.NoteTooltip;
                    break;
                case ControlType.FixedChoice:
                    this.tooltip = Constant.ControlDefault.FixedChoiceTooltip;
                    break;
                case ControlType.Flag:
                    this.defaultValue = Constant.ControlDefault.FlagValue;
                    this.tooltip = Constant.ControlDefault.FlagTooltip;
                    break;
                case ControlType.UtcOffset:
                    this.copyable = false;
                    this.defaultValue = DateTimeHandler.ToDatabaseUtcOffsetString(Constant.ControlDefault.DateTimeValue.Offset);
                    this.tooltip = Constant.ControlDefault.UtcOffsetTooltip;
                    this.visible = false;
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

        public int ControlOrder
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
                this.HasChanges |= String.Equals(this.dataLabel, value, StringComparison.Ordinal) == false;
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
                this.HasChanges |= String.Equals(this.defaultValue, value, StringComparison.Ordinal) == false;
                this.defaultValue = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.DefaultValue)));
            }
        }

        public bool IndexInFileTable
        {
            get
            {
                return this.indexInFileTable;
            }
            set
            {
                this.HasChanges |= this.indexInFileTable != value;
                this.indexInFileTable = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.IndexInFileTable)));
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
                this.HasChanges |= String.Equals(this.label, value, StringComparison.Ordinal) == false;
                this.label = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.Label)));
            }
        }

        public int MaxWidth
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

        public int SpreadsheetOrder
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
                this.HasChanges |= String.Equals(this.tooltip, value, StringComparison.Ordinal) == false;
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

        public string WellKnownValues
        {
            get
            {
                return this.wellKnownValues;
            }
            set
            {
                this.HasChanges |= String.Equals(this.wellKnownValues, value, StringComparison.Ordinal) == false;
                this.wellKnownValues = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.WellKnownValues)));
            }
        }

        public static ColumnTuplesForInsert CreateInsert(params ControlRow[] controls)
        {
            return ControlRow.CreateInsert((IEnumerable<ControlRow>)controls);
        }

        public static ColumnTuplesForInsert CreateInsert(IEnumerable<ControlRow> controls)
        {
            ColumnTuplesForInsert controlTuples = new ColumnTuplesForInsert(Constant.DatabaseTable.Controls,
                                                                            Constant.ControlColumn.AnalysisLabel,
                                                                            Constant.ControlColumn.ControlOrder,
                                                                            Constant.ControlColumn.Copyable,
                                                                            Constant.ControlColumn.DataLabel,
                                                                            Constant.ControlColumn.DefaultValue,
                                                                            Constant.ControlColumn.IndexInFileTable,
                                                                            Constant.ControlColumn.Label,
                                                                            Constant.ControlColumn.WellKnownValues,
                                                                            Constant.ControlColumn.SpreadsheetOrder,
                                                                            Constant.ControlColumn.MaxWidth,
                                                                            Constant.ControlColumn.Tooltip,
                                                                            Constant.ControlColumn.Type,
                                                                            Constant.ControlColumn.Visible);
            foreach (ControlRow control in controls)
            {
                Debug.Assert(control != null, "controls contains null.");
                controlTuples.Add(control.AnalysisLabel,
                                  control.ControlOrder,
                                  control.Copyable,
                                  control.DataLabel,
                                  control.DefaultValue,
                                  control.IndexInFileTable,
                                  control.Label,
                                  control.WellKnownValues,
                                  control.SpreadsheetOrder,
                                  control.MaxWidth,
                                  control.Tooltip,
                                  (int)control.Type,
                                  control.Visible);
            }

            return controlTuples;
        }

        public SearchTerm CreateSearchTerm()
        {
            switch (this.Type)
            {
                case ControlType.Counter:
                    return new SearchTerm<int>(this)
                    {
                        DatabaseValue = 0,
                        Operator = Constant.SearchTermOperator.GreaterThan
                    };
                case ControlType.DateTime:
                    // before the CustomViewSelection dialog is first opened CarnassialWindow changes the default date time to the date time of the 
                    // current file
                    return new SearchTerm<DateTime>(this)
                    {
                        DatabaseValue = Constant.ControlDefault.DateTimeValue.UtcDateTime,
                        Operator = Constant.SearchTermOperator.GreaterThanOrEqual
                    };
                case ControlType.Flag:
                    return new SearchTerm<bool>(this)
                    {
                        DatabaseValue = this.DefaultValue
                    };
                case ControlType.FixedChoice:
                    if (String.Equals(this.DataLabel, Constant.FileColumn.Classification, StringComparison.Ordinal))
                    {
                        if (ImageRow.TryParseFileClassification(this.DefaultValue, out FileClassification defaultValue) == false)
                        {
                            defaultValue = FileClassification.Color;
                        }
                        SearchTerm searchTerm = new SearchTerm<FileClassification>(this)
                        {
                            DatabaseValue = defaultValue
                        };
                        return searchTerm;
                    }
                    else
                    {
                        return new SearchTerm<string>(this)
                        {
                            DatabaseValue = this.DefaultValue
                        };
                    }
                case ControlType.Note:
                    return new SearchTerm<string>(this)
                    {
                        DatabaseValue = this.DefaultValue
                    };
                case ControlType.UtcOffset:
                    // the first time it's opened CustomViewSelection dialog changes this default to the offset of the current file
                    return new SearchTerm<TimeSpan>(this)
                    {
                        DatabaseValue = Constant.ControlDefault.DateTimeValue.Offset
                    };
                default:
                    throw new NotSupportedException(String.Format("Unhandled control type {0}.", this.Type));
            }
        }

        public ColumnTuplesWithID CreateUpdate()
        {
            List<ColumnTuple> columnTuples = new List<ColumnTuple>()
            {
                new ColumnTuple(Constant.ControlColumn.AnalysisLabel, this.AnalysisLabel),
                new ColumnTuple(Constant.ControlColumn.ControlOrder, this.ControlOrder),
                new ColumnTuple(Constant.ControlColumn.Copyable, this.Copyable),
                new ColumnTuple(Constant.ControlColumn.DataLabel, this.DataLabel),
                new ColumnTuple(Constant.ControlColumn.DefaultValue, this.DefaultValue),
                new ColumnTuple(Constant.ControlColumn.IndexInFileTable, this.IndexInFileTable),
                new ColumnTuple(Constant.ControlColumn.Label, this.Label),
                new ColumnTuple(Constant.ControlColumn.WellKnownValues, this.WellKnownValues),
                new ColumnTuple(Constant.ControlColumn.SpreadsheetOrder, this.SpreadsheetOrder),
                new ColumnTuple(Constant.ControlColumn.MaxWidth, this.MaxWidth),
                new ColumnTuple(Constant.ControlColumn.Tooltip, this.Tooltip),
                new ColumnTuple(Constant.ControlColumn.Type, (int)this.Type),
                new ColumnTuple(Constant.ControlColumn.Visible, this.Visible)
            };
            return new ColumnTuplesWithID(Constant.DatabaseTable.Controls, columnTuples, this.ID);
        }

        public List<string> GetWellKnownValues()
        {
            return this.WellKnownValues.Split(Constant.Control.ListDelimiter).ToList();
        }

        public bool IsFilePathComponent()
        {
            return String.Equals(this.DataLabel, Constant.FileColumn.File, StringComparison.OrdinalIgnoreCase) || String.Equals(this.DataLabel, Constant.FileColumn.RelativePath, StringComparison.OrdinalIgnoreCase);
        }

        public bool IsUserControl()
        {
            if (String.Equals(this.DataLabel, Constant.DatabaseColumn.ID, StringComparison.Ordinal) ||
                Constant.Control.StandardControls.Contains(this.DataLabel, StringComparer.Ordinal))
            {
                return false;
            }
            return true;
        }

        public bool IsValidExcelData(string value)
        {
            switch (this.Type)
            {
                case ControlType.Counter:
                    return MarkersForCounter.IsValidExcelString(value);
                case ControlType.DateTime:
                    DateTime dateTime;
                    return DateTimeHandler.TryParseDatabaseDateTime(value, out dateTime);
                case ControlType.Flag:
                    return String.Equals(value, Boolean.FalseString, StringComparison.OrdinalIgnoreCase) ||
                           String.Equals(value, Boolean.TrueString, StringComparison.OrdinalIgnoreCase);
                case ControlType.FixedChoice:
                    // the editor doesn't currently enforce the default value is one of the well known values, so accept it as
                    // valid independently
                    if (String.Equals(value, this.DefaultValue, StringComparison.Ordinal))
                    {
                        return true;
                    }
                    return this.GetWellKnownValues().Contains(value, StringComparer.Ordinal);
                case ControlType.Note:
                    return true;
                case ControlType.UtcOffset:
                    double utcOffset;
                    return double.TryParse(value, out utcOffset);
                default:
                    throw new NotSupportedException(String.Format("Unhandled control type {0}.", this.Type));
            }
        }

        public void SetWellKnownValues(List<string> wellKnownValues)
        {
            this.WellKnownValues = String.Join(Constant.Control.ListDelimiter.ToString(), wellKnownValues);
        }

        public bool Synchronize(ControlRow other)
        {
            if (String.Equals(this.DataLabel, other.DataLabel, StringComparison.Ordinal) == false)
            {
                throw new ArgumentOutOfRangeException(nameof(other), "Can't synchronize controls with different data labels.");
            }

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
            if (String.Equals(this.DefaultValue, other.DefaultValue, StringComparison.Ordinal) == false)
            {
                this.DefaultValue = other.DefaultValue;
                synchronizationMadeChanges = true;
            }
            if (this.IndexInFileTable != other.IndexInFileTable)
            {
                this.IndexInFileTable = other.IndexInFileTable;
                synchronizationMadeChanges = true;
            }
            if (String.Equals(this.Label, other.Label, StringComparison.Ordinal) == false)
            {
                this.Label = other.Label;
                synchronizationMadeChanges = true;
            }
            if (String.Equals(this.WellKnownValues, other.WellKnownValues, StringComparison.Ordinal) == false)
            {
                this.WellKnownValues = other.WellKnownValues;
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
            if (String.Equals(this.Tooltip, other.Tooltip, StringComparison.Ordinal) == false)
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
    }
}
