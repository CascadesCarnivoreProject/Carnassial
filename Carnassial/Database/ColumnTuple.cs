﻿using Carnassial.Util;
using System;

namespace Carnassial.Database
{
    /// <summary>
    /// A column name and a value to assign (or assigned) to that column.
    /// </summary>
    public class ColumnTuple
    {
        public string Name { get; private set; }
        public string Value { get; private set; }

        public ColumnTuple(string column, bool value)
            : this(column, value ? Constant.Boolean.True : Constant.Boolean.False)
        {
        }

        public ColumnTuple(string column, DateTime value)
            : this(column, DateTimeHandler.ToDatabaseDateTimeString(value))
        {
            if (value.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentOutOfRangeException("value");
            }
        }

        public ColumnTuple(string column, int value)
            : this(column, value.ToString())
        {
        }

        public ColumnTuple(string column, long value)
            : this(column, value.ToString())
        {
        }

        public ColumnTuple(string column, string value)
        {
            this.Name = column;
            this.Value = value;
        }

        public ColumnTuple(string column, TimeSpan utcOffset)
        {
            if ((utcOffset < Constant.Time.MinimumUtcOffset) ||
                (utcOffset > Constant.Time.MaximumUtcOffset))
            {
                throw new ArgumentOutOfRangeException("utcOffset", String.Format("UTC offset must be between {0} and {1}, inclusive.", DateTimeHandler.ToDatabaseUtcOffsetString(Constant.Time.MinimumUtcOffset), DateTimeHandler.ToDatabaseUtcOffsetString(Constant.Time.MinimumUtcOffset)));
            }
            if (utcOffset.Ticks % Constant.Time.UtcOffsetGranularity.Ticks != 0)
            {
                throw new ArgumentOutOfRangeException("utcOffset", String.Format("UTC offset must be an exact multiple of {0} ({1}).", DateTimeHandler.ToDatabaseUtcOffsetString(Constant.Time.UtcOffsetGranularity), DateTimeHandler.ToDisplayUtcOffsetString(Constant.Time.UtcOffsetGranularity)));
            }

            this.Name = column;
            this.Value = DateTimeHandler.ToDatabaseUtcOffsetString(utcOffset);
        }
    }
}
