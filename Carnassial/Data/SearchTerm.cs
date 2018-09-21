using Carnassial.Database;
using Carnassial.Util;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Carnassial.Data
{
    /// <summary>
    /// A SearchTerm stores the search criteria for a field.
    /// </summary>
    public abstract class SearchTerm : INotifyPropertyChanged
    {
        private object databaseValue;
        private string op;
        private Lazy<ObservableCollection<object>> wellKnownValues;
        private bool useForSearching;

        public ControlType ControlType { get; private set; }
        public string DataLabel { get; private set; }
        public string Label { get; private set; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected SearchTerm(ControlRow control)
        {
            this.ControlType = control.Type;
            this.databaseValue = null;
            this.DataLabel = control.DataLabel;
            this.Label = control.Label;
            this.op = Constant.SearchTermOperator.Equal;
            this.useForSearching = false;
            this.wellKnownValues = new Lazy<ObservableCollection<object>>(() =>
            {
                List<string> wellKnownStrings = control.GetWellKnownValues();
                if (String.Equals(this.DataLabel, Constant.FileColumn.Classification, StringComparison.Ordinal))
                {
                    // remove duplicate Color entry due to Ok being included as a well known value
                    wellKnownStrings.Remove(FileClassification.Color.ToString());
                }

                ObservableCollection<object> wellKnownValues = new ObservableCollection<object>();
                foreach (string wellKnownString in wellKnownStrings)
                {
                    wellKnownValues.Add(this.ConvertWellKnownValue(wellKnownString));
                }
                return wellKnownValues;
            });
        }

        protected SearchTerm(SearchTerm other)
        {
            this.ControlType = other.ControlType;
            this.databaseValue = other.databaseValue;
            this.DataLabel = other.DataLabel;
            this.Label = other.Label;
            this.Operator = other.Operator;
            this.UseForSearching = other.UseForSearching;
            this.wellKnownValues = other.wellKnownValues;
        }

        public object DatabaseValue
        {
            get
            {
                return this.databaseValue;
            }
            set
            {
                this.databaseValue = this.ConvertDatabaseValue(value);
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.DatabaseValue)));
            }
        }

        public string Operator
        {
            get
            {
                return this.op;
            }
            set
            {
                this.op = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.Operator)));
            }
        }

        public bool UseForSearching
        {
            get
            {
                return this.useForSearching;
            }
            set
            {
                this.useForSearching = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.UseForSearching)));
            }
        }

        public ObservableCollection<object> WellKnownValues
        {
            get { return this.wellKnownValues.Value; }
        }

        public abstract SearchTerm Clone();
        protected abstract object ConvertDatabaseValue(object value);
        protected abstract object ConvertWellKnownValue(string value);

        public override bool Equals(object obj)
        {
            if (obj is SearchTerm)
            {
                return this.Equals((SearchTerm)obj);
            }
            return false;
        }

        public bool Equals(SearchTerm other)
        {
            if (other == null)
            {
                return false;
            }
            if (Object.ReferenceEquals(this, other))
            {
                return true;
            }

            if (this.ControlType != other.ControlType)
            {
                return false;
            }
            if (this.DatabaseValue != other.DatabaseValue)
            {
                return false;
            }
            if (String.Equals(this.DataLabel, other.DataLabel, StringComparison.Ordinal) == false)
            {
                return false;
            }
            if (String.Equals(this.Label, other.Label, StringComparison.Ordinal) == false)
            {
                return false;
            }
            if (String.Equals(this.Operator, other.Operator, StringComparison.Ordinal) == false)
            {
                return false;
            }
            if (this.UseForSearching != other.UseForSearching)
            {
                return false;
            }
            // this.WellKnownValues is excluded as doesn't affect the query fragment the term generates

            return true;
        }

        public override int GetHashCode()
        {
            // this.WellKnownValues is excluded as doesn't affect the query fragment the term generates
            return Utilities.CombineHashCodes(this.ControlType, this.DatabaseValue, this.DataLabel, this.Label, this.Operator, this.UseForSearching);
        }

        public WhereClause GetWhereClause()
        {
            // convert operator from unicode to SQL expression
            string sqlOperator;
            switch (this.Operator)
            {
                case Constant.SearchTermOperator.Equal:
                    sqlOperator = Constant.SqlOperator.Equal;
                    break;
                case Constant.SearchTermOperator.Glob:
                    sqlOperator = Constant.SqlOperator.Glob;
                    break;
                case Constant.SearchTermOperator.GreaterThan:
                    sqlOperator = Constant.SqlOperator.GreaterThan;
                    break;
                case Constant.SearchTermOperator.GreaterThanOrEqual:
                    sqlOperator = Constant.SqlOperator.GreaterThanOrEqual;
                    break;
                case Constant.SearchTermOperator.LessThan:
                    sqlOperator = Constant.SqlOperator.LessThan;
                    break;
                case Constant.SearchTermOperator.LessThanOrEqual:
                    sqlOperator = Constant.SqlOperator.LessThanOrEqual;
                    break;
                case Constant.SearchTermOperator.NotEqual:
                    sqlOperator = Constant.SqlOperator.NotEqual;
                    break;
                default:
                    throw new NotSupportedException(String.Format("Unhandled search term operator '{0}'.", this.Operator));
            }

            return new WhereClause(this.DataLabel, sqlOperator, this.DatabaseValue);
        }
    }

    [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:FileMayOnlyContainASingleClass", Justification = "StyleCop limitation.")]
    public class SearchTerm<TColumnType> : SearchTerm
    {
        public SearchTerm(ControlRow control)
            : base(control)
        {
        }

        protected SearchTerm(SearchTerm<TColumnType> other)
            : base(other)
        {
        }

        public override SearchTerm Clone()
        {
            return new SearchTerm<TColumnType>(this);
        }

        protected override object ConvertDatabaseValue(object value)
        {
            if (value is string)
            {
                return this.ConvertWellKnownValue((string)value);
            }

            if (typeof(TColumnType) == typeof(DateTime))
            {
                DateTime dateTime = (DateTime)value;
                if (dateTime.Kind != DateTimeKind.Utc)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "DateTime not UTC.");
                }
                return dateTime;
            }

            return (TColumnType)value;
        }

        protected override object ConvertWellKnownValue(string value)
        {
            if (typeof(TColumnType) == typeof(bool))
            {
                int valueAsInt = Int32.Parse(value, NumberStyles.None, Constant.InvariantCulture);
                if (valueAsInt == 0)
                {
                    return false;
                }
                if (valueAsInt == 1)
                {
                    return true;
                }
                throw new ArgumentOutOfRangeException(nameof(value), "Valid integer values for boolean database columns are 0 and 1.");
            }
            if (typeof(TColumnType) == typeof(DateTime))
            {
                return DateTimeHandler.ParseDatabaseDateTime(value);
            }
            if (typeof(TColumnType) == typeof(FileClassification))
            {
                if (ImageRow.TryParseFileClassification(value, out FileClassification classification))
                {
                    return classification;
                }
                throw new ArgumentOutOfRangeException(nameof(value), String.Format("Unknown file classification '{0}'.", value));
            }
            if (typeof(TColumnType) == typeof(int))
            {
                return Int32.Parse(value, NumberStyles.AllowLeadingSign, Constant.InvariantCulture);
            }
            if (typeof(TColumnType) == typeof(string))
            {
                return value;
            }
            if (typeof(TColumnType) == typeof(TimeSpan))
            {
                return DateTimeHandler.ParseDisplayUtcOffsetString(value).TotalHours;
            }

            throw new NotSupportedException(String.Format("Unhandled type {0}.", typeof(TColumnType)));
        }

        public override string ToString()
        {
            string displayValue;
            if (typeof(TColumnType) == typeof(bool))
            {
                displayValue = (bool)this.DatabaseValue ? Boolean.TrueString : Boolean.FalseString;
            }
            else if (typeof(TColumnType) == typeof(DateTime))
            {
                displayValue = DateTimeHandler.ToDisplayDateTimeString((DateTime)this.DatabaseValue);
            }
            else if ((typeof(TColumnType) == typeof(FileClassification)) || 
                     (typeof(TColumnType) == typeof(int)))
            {
                displayValue = this.DatabaseValue.ToString();
            }
            else if (typeof(TColumnType) == typeof(string))
            {
                displayValue = (string)this.DatabaseValue;
            }
            else if (typeof(TColumnType) == typeof(TimeSpan))
            {
                displayValue = DateTimeHandler.ToDisplayUtcOffsetString(DateTimeHandler.FromDatabaseUtcOffset((double)this.DatabaseValue));
            }
            else
            {
                throw new NotSupportedException(String.Format("Unhandled type {0}.", typeof(TColumnType)));
            }

            if (String.IsNullOrEmpty(displayValue))
            {
                displayValue = "\"\"";  // an empty string, display it as ""
            }

            return this.DataLabel + " " + this.Operator + " " + displayValue;
        }
    }
}
