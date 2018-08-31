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
                    this.wellKnownValues = Constant.ControlDefault.FlagWellKnownValues;
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
                if (this.analysisLabel == value)
                {
                    return;
                }
                this.HasChanges |= true;
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
                if (this.controlOrder == value)
                {
                    return;
                }
                this.HasChanges |= true;
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
                if (this.copyable == value)
                {
                    return;
                }
                this.copyable = value;
                this.HasChanges |= true;
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
                if (String.Equals(this.dataLabel, value, StringComparison.Ordinal))
                {
                    return;
                }
                this.dataLabel = value;
                this.HasChanges |= true;
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
                if (String.Equals(this.defaultValue, value, StringComparison.Ordinal))
                {
                    return;
                }
                this.defaultValue = value;
                this.HasChanges |= true;
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
                if (this.indexInFileTable == value)
                {
                    return;
                }
                this.HasChanges |= true;
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
                if (String.Equals(this.label, value, StringComparison.Ordinal))
                {
                    return;
                }
                this.HasChanges |= true;
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
                if (this.maxWidth == value)
                {
                    return;
                }
                this.HasChanges |= true;
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
                if (this.spreadSheetOrder == value)
                {
                    return;
                }
                this.HasChanges |= true;
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
                if (String.Equals(this.tooltip, value, StringComparison.Ordinal))
                {
                    return;
                }
                this.HasChanges |= true;
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
                if (this.type == value)
                {
                    return;
                }
                this.HasChanges |= true;
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
                if (this.visible == value)
                {
                    return;
                }
                this.HasChanges |= true;
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
                if (String.Equals(this.wellKnownValues, value, StringComparison.Ordinal))
                {
                    return;
                }
                this.HasChanges |= true;
                this.wellKnownValues = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.WellKnownValues)));
            }
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

        public List<string> GetWellKnownValues()
        {
            return WellKnownValueConverter.Convert(this.WellKnownValues);
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

        public void SetWellKnownValues(List<string> wellKnownValues)
        {
            this.WellKnownValues = WellKnownValueConverter.ConvertBack(wellKnownValues);
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
