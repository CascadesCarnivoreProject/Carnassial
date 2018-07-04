﻿using System.Collections.Generic;

namespace Carnassial.Database
{
    public class FileTuplesWithID : ColumnTuplesWithID
    {
        public FileTuplesWithID(params string[] columns)
            : this((IEnumerable<string>)columns)
        {
        }

        public FileTuplesWithID(IEnumerable<string> columns)
            : base(Constant.DatabaseTable.Files, columns)
        {
        }

        public FileTuplesWithID(IList<ColumnTuple> columnTuples, long id)
            : base(Constant.DatabaseTable.Files, columnTuples, id)
        {
        }
    }
}
