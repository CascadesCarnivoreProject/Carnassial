using System;

namespace Carnassial.Database
{
    public class WhereClause
    {
        public string Column { get; private init; }
        public string Operator { get; private init; }
        public string ParameterName { get; private init; }
        public object? Value { get; private init; }

        public WhereClause(string column, string op, object? value)
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
