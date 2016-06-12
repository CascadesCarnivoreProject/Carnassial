namespace Timelapse.Database
{
    /// <summary>
    /// A column name and a value to assign (or assigned) to that column.
    /// </summary>
    public class ColumnTuple
    {
        public string Name { get; private set; }
        public string Value { get; private set; }

        public ColumnTuple(string column, bool value)
            : this(column, value ? Constants.Boolean.True : Constants.Boolean.False)
        {
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
    }
}
