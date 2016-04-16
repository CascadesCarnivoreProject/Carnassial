using System;
using System.Collections.Generic;

namespace Timelapse.Database
{
    // A tuple where the first item is a columntuble and the second a string indicating 'where' it would apply
    public class ColumnTupleListWhere
    {
        public List<ColumnTuple> ListPair { get; set; }
        public string Where { get; set; }

        public ColumnTupleListWhere()
        {
        }

        public ColumnTupleListWhere(List<ColumnTuple> listPair, string where)
        {
            this.ListPair = listPair;
            this.Where = where;
        }
    }
}
