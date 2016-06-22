﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Windows.Controls;

namespace Timelapse.Database
{
    public class DataTableBackedList<TRow> : IDisposable, IEnumerable<TRow> where TRow : DataRowBackedObject
    {
        private Func<DataRow, TRow> createRow;
        private bool disposed;

        protected DataTable DataTable { get; private set; }

        public DataTableBackedList(DataTable dataTable, Func<DataRow, TRow> createRow)
        {
            this.createRow = createRow;
            this.DataTable = dataTable;
            this.disposed = false;
        }

        public TRow this[int index]
        {
            get { return this.createRow(this.DataTable.Rows[index]); }
        }

        public IEnumerable<string> ColumnNames
        {
            get
            {
                foreach (DataColumn column in this.DataTable.Columns)
                {
                    yield return column.ColumnName;
                }
            }
        }

        public int RowCount
        {
            get { return this.DataTable.Rows.Count; }
        }

        public void BindDataGrid(DataGrid dataGrid, DataRowChangeEventHandler onRowChanged)
        {
            if (dataGrid != null)
            {
                dataGrid.DataContext = this.DataTable;
            }
            // refresh data grid binding
            if (onRowChanged != null)
            {
                this.DataTable.RowChanged += onRowChanged;
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        public IEnumerator<TRow> GetEnumerator()
        {
            foreach (DataRow row in this.DataTable.Rows)
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
            DataRow row = this.DataTable.Rows.Find(id);
            if (row == null)
            {
                return null;
            }
            return this.createRow(row);
        }

        public TRow NewRow()
        {
            DataRow row = this.DataTable.NewRow();
            return this.createRow(row);
        }

        public void RemoveAt(int index)
        {
            this.DataTable.Rows.RemoveAt(index);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                this.DataTable.Dispose();
            }
            this.disposed = true;
        }
    }
}
