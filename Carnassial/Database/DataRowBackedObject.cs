using System.Data;

namespace Carnassial.Database
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
            get { return this.Row.GetLongField(Constant.DatabaseColumn.ID); }
        }

        public int GetIndex(DataTable dataTable)
        {
            return dataTable.Rows.IndexOf(this.Row);
        }
    }
}
