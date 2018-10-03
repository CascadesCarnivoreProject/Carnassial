using System;

namespace Carnassial.Database
{
    public class WhereClause
    {
        public string Column { get; private set; }
        public string Operator { get; set; }
        public string ParameterName { get; private set; }
        public object Value { get; private set; }

        public WhereClause(string column, string op, object value)
        {
            if (String.IsNullOrEmpty(column))
            {
                throw new ArgumentOutOfRangeException(nameof(column));
            }

            this.Column = column;
            this.Operator = op;
            this.ParameterName = SQLiteDatabase.ToParameterName(column);
            this.Value = value;
        }
    }
}
