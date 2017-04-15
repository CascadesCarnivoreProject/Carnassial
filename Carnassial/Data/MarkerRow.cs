using Carnassial.Database;
using System.Collections.Generic;
using System.Data;

namespace Carnassial.Data
{
    public class MarkerRow : DataRowBackedObject
    {
        public MarkerRow(DataRow row)
            : base(row)
        {
        }

        public string this[string dataLabel]
        {
            get { return this.Row.GetStringField(dataLabel); }
            set { this.Row.SetField(dataLabel, value); }
        }

        public IEnumerable<string> DataLabels
        {
            get
            {
                foreach (DataColumn column in this.Row.Table.Columns)
                {
                    if (column.ColumnName != Constant.DatabaseColumn.ID)
                    {
                        yield return column.ColumnName;
                    }
                }
            }
        }

        public ColumnTuplesWithID CreateUpdate()
        {
            List<ColumnTuple> columnTuples = new List<ColumnTuple>();
            foreach (string dataLabel in this.DataLabels)
            {
                columnTuples.Add(new ColumnTuple(dataLabel, this[dataLabel]));
            }
            return new ColumnTuplesWithID(Constant.DatabaseTable.Markers, columnTuples, this.ID);
        }
    }
}
