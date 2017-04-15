namespace Carnassial.Database
{
    public class WhereClause : ColumnTuple
    {
        public string Operator { get; set; }

        public WhereClause(string column, string op, string value)
            : base(column, value)
        {
            this.Operator = op;
        }
    }
}
