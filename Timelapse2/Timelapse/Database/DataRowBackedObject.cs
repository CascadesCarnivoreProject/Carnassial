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

        public long ID
        {
            get { return this.Row.GetID(); }
        }

        public abstract ColumnTuplesWithWhere GetColumnTuples();
    }
}
