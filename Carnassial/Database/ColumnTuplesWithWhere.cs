using Carnassial.Util;
using System;
using System.Collections.Generic;

namespace Carnassial.Database
{
    // A tuple with a list of ColumnTuples and a string indicating where to apply the updates contained in the tuples
    public class ColumnTuplesWithWhere
    {
        public List<ColumnTuple> Columns { get; private set; }
        public string Where { get; private set; }

        public ColumnTuplesWithWhere()
        {
            this.Columns = new List<ColumnTuple>();
        }

        public ColumnTuplesWithWhere(List<ColumnTuple> columns)
        {
            this.Columns = columns;
        }

        public ColumnTuplesWithWhere(List<ColumnTuple> columns, long id)
            : this(columns)
        {
            this.SetWhere(id);
        }

        public void SetWhere(long id)
        {
            this.Where = Constant.DatabaseColumn.ID + " = " + id.ToString();
        }

        public void SetWhere(string relativePath, string file)
        {
            this.Where = String.Format("{0} = {1}", Constant.DatabaseColumn.File, Utilities.QuoteForSql(file));
            if (String.IsNullOrEmpty(relativePath) == false)
            {
                this.Where += String.Format(" AND {0} = {1}", Constant.DatabaseColumn.RelativePath, Utilities.QuoteForSql(relativePath));
            }
        }
    }
}
