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
                List<string> whereClauses = new List<string>(this.Where.Count);
                foreach (WhereClause clause in this.Where)
                {
                    // check to see if the search should match an empty string
                    // If so, nulls need also to be matched as NULL and empty are considered interchangeable.
                    if (String.IsNullOrEmpty(clause.Value) && clause.Operator == Constant.SearchTermOperator.Equal)
                    {
                        whereClauses.Add("(" + clause.Name + " IS NULL OR " + clause.Name + " = '')");
                    }
                    else
                    {
                        whereClauses.Add(clause.Name + " " + clause.Operator + " @" + clause.Name);
                        whereParameters.Add(new SQLiteParameter("@" + clause.Name, clause.Value));
                    }
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
