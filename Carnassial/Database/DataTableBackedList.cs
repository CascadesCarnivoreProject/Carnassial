﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Windows.Controls;

namespace Carnassial.Database
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
                dataGrid.ItemsSource = this.DataTable.DefaultView;
            }
            // refresh data grid binding
            if (onRowChanged != null)
            {
                this.DataTable.RowChanged += onRowChanged;
            }
        }

        public TRow CreateRow()
        {
            DataRow row = this.DataTable.NewRow();
            this.DataTable.Rows.Add(row);
            return this.createRow(row);
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
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

        public IEnumerator<TRow> GetEnumerator()
        {
            // use a row index rather than a foreach loop as, if the caller modifies the DataRow, the DataRowCollection enumerator under the foreach may lose its place
            // Manipulation of data in a DataTable from within a foreach is common practice, suggesting whatever framework issue which invalidates the enumerator 
            // manifests only infrequently, but MSDN is ambiguous as to the level of support.  Enumerators returning the same row multiple times has been observed,
            // skipping of rows has not been.
            for (int rowIndex = 0; rowIndex < this.DataTable.Rows.Count; ++rowIndex)
            {
                yield return this.createRow(this.DataTable.Rows[rowIndex]);
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

        public int IndexOf(TRow row)
        {
            return row.GetIndex(this.DataTable);
        }
    }
}
