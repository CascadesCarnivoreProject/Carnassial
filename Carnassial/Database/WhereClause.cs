namespace Carnassial.Database
{
    public class WhereClause
    {
        public string Column { get; private set; }
        public string Operator { get; set; }
        public object Value { get; private set; }

        public WhereClause(string column, string op, object value)
        {
            this.Column = column;
            this.Operator = op;
            this.Value = value;
        }
    }
}
