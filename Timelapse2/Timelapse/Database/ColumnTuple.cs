using System;

namespace Timelapse.Database
{
    // A tuple comprising a Column and a Value
    public class ColumnTuple
    {
        public string ColumnName { get; set; }
        public object ColumnValue { get; set; }

        public ColumnTuple(string column, object value)
        {
            this.ColumnName = column;
            this.ColumnValue = value;
        }
    }
}
