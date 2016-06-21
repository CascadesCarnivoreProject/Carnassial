using System.Data;

namespace Timelapse.Database
{
    public abstract class DataRowBackedObject
    {
        protected DataRow Row { get; private set; }

        protected DataRowBackedObject(DataRow row)
        {
            this.Row = row;
        }

        public abstract ColumnTuplesWithWhere GetColumnTuples();
    }
}
