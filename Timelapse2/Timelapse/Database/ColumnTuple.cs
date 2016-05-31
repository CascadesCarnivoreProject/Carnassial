using System;

namespace Timelapse.Database
{
    // A tuple comprising a Column and a Value
    public class ColumnTuple
    {
        public string Name { get; private set; }
        public string Value { get; private set; }

        public ColumnTuple(string column, int value)
            : this(column, value.ToString())
        {
        }

        public ColumnTuple(string column, string value)
        {
            this.Name = column;
            this.Value = value;
        }
    }
}
