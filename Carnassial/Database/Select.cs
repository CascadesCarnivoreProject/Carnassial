using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace Carnassial.Database
{
    public class Select
    {
        public string OrderBy { get; set; }
        public string Table { get; set; }
        public List<WhereClause> Where { get; set; }
        public LogicalOperator WhereCombiningOperator { get; set; }

        public Select(string table)
        {
            this.OrderBy = null;
            this.Table = table;
            this.Where = new List<WhereClause>();
            this.WhereCombiningOperator = LogicalOperator.And;
        }

        public Select(string table, WhereClause where)
            : this(table)
        {
            this.Where.Add(where);
        }

        public SQLiteCommand CreateSelect(SQLiteConnection connection)
        {
            return this.CreateSelect(connection, "SELECT * FROM ");
        }

        private SQLiteCommand CreateSelect(SQLiteConnection connection, string selectFrom)
        {
            // unrestrected query
            string query = selectFrom + this.Table;

            // constrain with where clauses, if specified
            List<SQLiteParameter> whereParameters = new List<SQLiteParameter>();
            if (this.Where.Count > 0)
            {
                Dictionary<string, int> numberOfClausesByColumn = new Dictionary<string, int>(StringComparer.Ordinal);
                List<string> whereClauses = new List<string>(this.Where.Count);
                foreach (WhereClause clause in this.Where)
                {
                    if (numberOfClausesByColumn.TryGetValue(clause.Column, out int clausesEncounteredForThisColumn) == false)
                    {
                        clausesEncounteredForThisColumn = 0;
                    }

                    // check to see if the search should match an empty string
                    // If so, nulls need also to be matched as NULL and empty are considered interchangeable.
                    bool valueIsNullOrEmpty = clause.Value == null;
                    if (clause.Value is string)
                    {
                        valueIsNullOrEmpty = String.IsNullOrEmpty((string)clause.Value);
                    }
                    if (valueIsNullOrEmpty && clause.Operator == Constant.SearchTermOperator.Equal)
                    {
                        whereClauses.Add("(" + clause.Column + " IS NULL OR " + clause.Column + " = '')");
                    }
                    else
                    {
                        string parameterName = "@" + clause.Column + clausesEncounteredForThisColumn.ToString();
                        whereClauses.Add(clause.Column + " " + clause.Operator + " " + parameterName);
                        whereParameters.Add(new SQLiteParameter(parameterName, clause.Value));
                    }

                    numberOfClausesByColumn[clause.Column] = ++clausesEncounteredForThisColumn;
                }

                string whereCombiningTerm;
                switch (this.WhereCombiningOperator)
                {
                    case LogicalOperator.And:
                        whereCombiningTerm = " AND ";
                        break;
                    case LogicalOperator.Or:
                        whereCombiningTerm = " OR ";
                        break;
                    default:
                        throw new NotSupportedException(String.Format("Unhandled logical operator {0}.", this.WhereCombiningOperator));
                }

                query += Constant.Sql.Where + String.Join(whereCombiningTerm, whereClauses);
            }

            // add ordering, if specified
            if (String.IsNullOrEmpty(this.OrderBy) == false)
            {
                query += " ORDER BY " + this.OrderBy;
            }

            SQLiteCommand command = new SQLiteCommand(query, connection);

            // add where parameters, if specified
            foreach (SQLiteParameter whereParameter in whereParameters)
            {
                command.Parameters.Add(whereParameter);
            }

            return command;
        }

        public long Count(SQLiteConnection connection)
        {
            using (SQLiteCommand command = this.CreateSelect(connection, "SELECT Count(*) FROM "))
            {
                return (long)command.ExecuteScalar();
            }
        }
    }
}
