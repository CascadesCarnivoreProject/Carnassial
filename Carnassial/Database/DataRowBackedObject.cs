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

        public bool HasChanges
        {
            get { return (this.Row.RowState != DataRowState.Unchanged) && (this.Row.RowState != DataRowState.Detached); }
        }

        public long ID
        {
            get { return this.Row.GetLongField(Constant.DatabaseColumn.ID); }
        }

        public void AcceptChanges()
        {
            this.Row.AcceptChanges();
        }

        public int GetIndex(DataTable dataTable)
        {
            return dataTable.Rows.IndexOf(this.Row);
        }
    }
}
