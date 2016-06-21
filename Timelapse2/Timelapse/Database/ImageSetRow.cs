using System.Collections.Generic;
using System.Data;

namespace Timelapse.Database
{
    public class ImageSetRow : DataRowBackedObject
    {
        public ImageSetRow(DataRow row)
            : base(row)
        {
        }

        public ImageQualityFilter ImageQualityFilter
        {
            get { return (ImageQualityFilter)this.Row.GetIntegerField(Constants.DatabaseColumn.Filter); }
            set { this.Row.SetField(Constants.DatabaseColumn.Filter, (int)value); }
        }

        public int ImageRowIndex
        {
            get { return this.Row.GetIntegerField(Constants.DatabaseColumn.Row); }
            set { this.Row.SetField(Constants.DatabaseColumn.Row, value); }
        }

        public string Log
        {
            get { return this.Row.GetStringField(Constants.DatabaseColumn.Log); }
            set { this.Row.SetField(Constants.DatabaseColumn.Log, value); }
        }

        public bool MagnifierEnabled
        {
            get { return this.Row.GetBooleanField(Constants.DatabaseColumn.Magnifier); }
            set { this.Row.SetField(Constants.DatabaseColumn.Magnifier, value); }
        }

        // can't safely be implemented until issue #34 is addressed as the WhiteSpaceTrimmed is not consistently present
        // public bool WhitespaceTrimmed
        // {
        //    get { return this.Row.GetBooleanField(Constants.DatabaseColumn.WhiteSpaceTrimmed); }
        //    set { this.Row.SetField(Constants.DatabaseColumn.WhiteSpaceTrimmed, value); }
        // }

        public override ColumnTuplesWithWhere GetColumnTuples()
        {
            List<ColumnTuple> columnTuples = new List<ColumnTuple>();
            columnTuples.Add(new ColumnTuple(Constants.DatabaseColumn.Filter, (int)this.ImageQualityFilter));
            columnTuples.Add(new ColumnTuple(Constants.DatabaseColumn.Log, this.Log));
            columnTuples.Add(new ColumnTuple(Constants.DatabaseColumn.Magnifier, this.MagnifierEnabled));
            columnTuples.Add(new ColumnTuple(Constants.DatabaseColumn.Row, this.ImageRowIndex));
            // columnTuples.Add(new ColumnTuple(Constants.DatabaseColumn.WhiteSpaceTrimmed, this.WhitespaceTrimmed));
            return new ColumnTuplesWithWhere(columnTuples, this.ID);
        }
    }
}
