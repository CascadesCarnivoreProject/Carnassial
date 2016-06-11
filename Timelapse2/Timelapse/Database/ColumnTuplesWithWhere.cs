using System;
using System.Collections.Generic;
using Timelapse.Util;

namespace Timelapse.Database
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

        public ColumnTuplesWithWhere(List<ColumnTuple> columns, string where)
            : this(columns)
        {
            this.Where = where;
        }

        public void SetWhere(long id)
        {
            this.Where = Constants.Database.ID + " = " + id.ToString();
        }

        public void SetWhere(string folder, string file)
        {
            this.Where = String.Format("{0} = {1}", Constants.DatabaseColumn.File, Utilities.QuoteForSql(file));
            if (String.IsNullOrEmpty(folder) == false)
            {
                this.Where += String.Format(" AND {0} = {1}", Constants.DatabaseColumn.Folder, Utilities.QuoteForSql(folder));
            }
        }
    }
}
