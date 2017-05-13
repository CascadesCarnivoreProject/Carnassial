using Carnassial.Util;
using System;

namespace Carnassial.Database
{
    public class ColumnDefinition
    {
        public bool Autoincrement { get; set; }
        public string DefaultValue { get; set; }
        public bool PrimaryKey { get; set; }
        public string Name { get; private set; }
        public bool NotNull { get; set; }
        public string Type { get; private set; }

        public ColumnDefinition(string name, DateTime defaultValue)
            : this(name, Constant.SqlColumnType.DateTime, DateTimeHandler.ToDatabaseDateTimeString(defaultValue))
        {
        }

        public ColumnDefinition(string name, string type)
            : this(name, type, null)
        {
        }

        public ColumnDefinition(string name, TimeSpan defaultValue)
            : this(name, Constant.SqlColumnType.Real, DateTimeHandler.ToDatabaseUtcOffsetString(defaultValue))
        {
        }

        public ColumnDefinition(string name, string type, string defaultValue)
        {
            if (String.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentOutOfRangeException(nameof(name));
            }
            if (String.IsNullOrWhiteSpace(type))
            {
                throw new ArgumentOutOfRangeException(nameof(type));
            }

            this.Autoincrement = false;
            this.DefaultValue = defaultValue;
            this.Name = name;
            this.NotNull = false;
            this.PrimaryKey = false;
            this.Type = type;
        }

        public ColumnDefinition(ColumnDefinition other)
        {
            this.Autoincrement = other.Autoincrement;
            this.DefaultValue = other.DefaultValue;
            this.Name = other.Name;
            this.NotNull = other.NotNull;
            this.PrimaryKey = other.PrimaryKey;
            this.Type = other.Type;
        }

        public static ColumnDefinition CreatePrimaryKey()
        {
            return new ColumnDefinition(Constant.DatabaseColumn.ID, Constant.SqlColumnType.Integer) { Autoincrement = true, PrimaryKey = true };
        }

        /// <summary>
        /// SQLite doesn't support parameters for default values when defining table schemas so escaping has to be done by its caller.
        /// </summary>
        public static string QuoteForSql(string value)
        {
            // promote null values to empty strings
            if (value == null)
            {
                return "''";
            }

            // for an input of "foo's bar" the output is "'foo''s bar'"
            return "'" + value.Replace("'", "''") + "'";
        }

        public override string ToString()
        {
            string columnDefinition = String.Format("{0} {1}", this.Name, this.Type);
            if (this.DefaultValue != null)
            {
                if ((this.Type == Constant.SqlColumnType.Integer) || (this.Type == Constant.SqlColumnType.Real))
                {
                    columnDefinition += " DEFAULT " + this.DefaultValue;
                }
                else
                {
                    columnDefinition += " DEFAULT " + ColumnDefinition.QuoteForSql(this.DefaultValue);
                }
            }
            if (this.NotNull)
            {
                columnDefinition += " NOT NULL";
            }
            if (this.PrimaryKey)
            {
                columnDefinition += " PRIMARY KEY";
                if (this.Autoincrement)
                {
                    columnDefinition += " AUTOINCREMENT";
                }
            }
            return columnDefinition;
        }
    }
}
