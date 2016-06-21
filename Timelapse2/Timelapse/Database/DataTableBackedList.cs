using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Windows.Controls;

namespace Timelapse.Database
{
    public class DataTableBackedList<TRow> : IDisposable, IEnumerable<TRow> where TRow : DataRowBackedObject
    {
        private Func<DataRow, TRow> createRow;
        private DataTable dataTable;
        private bool disposed;

        public DataTableBackedList(DataTable dataTable, Func<DataRow, TRow> createRow)
        {
            this.createRow = createRow;
            this.dataTable = dataTable;
            this.disposed = false;
        }

        public TRow this[int index]
        {
            get { return this.createRow(this.dataTable.Rows[index]); }
        }

        public IEnumerable<string> ColumnNames
        {
            get
            {
                foreach (DataColumn column in this.dataTable.Columns)
                {
                    yield return column.ColumnName;
                }
            }
        }

        public int RowCount
        {
            get { return this.dataTable.Rows.Count; }
        }

        public void BindDataGrid(DataGrid dataGrid, DataRowChangeEventHandler onRowChanged)
        {
            if (dataGrid != null)
            {
                dataGrid.DataContext = this.dataTable;
            }
            // refresh data grid binding
            if (onRowChanged != null)
            {
                this.dataTable.RowChanged += onRowChanged;
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        public IEnumerator<TRow> GetEnumerator()
        {
            foreach (DataRow row in this.dataTable.Rows)
            {
                yield return this.createRow(row);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public TRow Find(long id)
        {
            DataRow row = this.dataTable.Rows.Find(id);
            if (row == null)
            {
                return null;
            }
            return this.createRow(row);
        }

        public TRow NewRow()
        {
            DataRow row = this.dataTable.NewRow();
            return this.createRow(row);
        }

        public void RemoveAt(int index)
        {
            this.dataTable.Rows.RemoveAt(index);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                this.dataTable.Dispose();
            }
            this.disposed = true;
        }
    }
}
