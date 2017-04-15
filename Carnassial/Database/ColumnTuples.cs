using System;
using System.Collections.Generic;

namespace Carnassial.Database
{
    public class ColumnTuples
    {
        protected List<string> Columns { get; private set; }
        protected List<List<object>> Values { get; private set; }

        protected ColumnTuples(params string[] columns)
            : this((IEnumerable<string>)columns)
        {
        }

        protected ColumnTuples(IEnumerable<string> columns)
        {
            this.Columns = new List<string>(columns);
            if (this.Columns.Count < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(columns), "At least one column must be specified to update.");
            }

            this.Values = new List<List<object>>();
        }

        protected ColumnTuples(IList<ColumnTuple> columnTuples)
        {
            this.Columns = new List<string>(columnTuples.Count);
            List<object> values = new List<object>(columnTuples.Count);
            foreach (ColumnTuple columnTuple in columnTuples)
            {
                this.Columns.Add(columnTuple.Name);
                values.Add(columnTuple.Value);
            }

            this.Values = new List<List<object>>() { values };
        }

        public int RowCount
        {
            get { return this.Values.Count; }
        }
    }
}
