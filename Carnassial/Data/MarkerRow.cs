using Carnassial.Database;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;

namespace Carnassial.Data
{
    public class MarkerRow : DataRowBackedObject, IEnumerable<MarkersForCounter>
    {
        private Dictionary<string, MarkersForCounter> markersByDataLabel;

        public MarkerRow(DataRow row)
            : base(row)
        {
            this.markersByDataLabel = new Dictionary<string, MarkersForCounter>();
        }

        public MarkersForCounter this[string dataLabel]
        {
            get
            {
                MarkersForCounter markersForCounter;
                if (this.markersByDataLabel.TryGetValue(dataLabel, out markersForCounter) == false)
                {
                    markersForCounter = MarkersForCounter.Parse(dataLabel, this.Row.GetStringField(dataLabel));
                    markersForCounter.PropertyChanged += this.MarkersForCounter_PropertyChanged;
                    this.markersByDataLabel.Add(dataLabel, markersForCounter);
                }
                return markersForCounter;
            }
        }

        private IEnumerable<string> DataLabels
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
                columnTuples.Add(new ColumnTuple(dataLabel, this.Row.GetStringField(dataLabel)));
            }
            return new ColumnTuplesWithID(Constant.DatabaseTable.Markers, columnTuples, this.ID);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public IEnumerator<MarkersForCounter> GetEnumerator()
        {
            return new MarkerRowEnumerator(this);
        }

        private void MarkersForCounter_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            MarkersForCounter markers = (MarkersForCounter)sender;
            this.Row.SetField(markers.DataLabel, markers.GetPointList());
        }

        private class MarkerRowEnumerator : IEnumerator<MarkersForCounter>
        {
            private IEnumerator<string> dataLabelEnumerator;
            private bool disposed;
            private MarkerRow row;

            public MarkerRowEnumerator(MarkerRow row)
            {
                this.dataLabelEnumerator = row.DataLabels.GetEnumerator();
                this.row = row;
            }

            public MarkersForCounter Current
            {
                get { return this.row[this.dataLabelEnumerator.Current]; }
            }

            object IEnumerator.Current
            {
                get { return this.Current; }
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
                    this.dataLabelEnumerator.Dispose();
                }

                this.disposed = true;
            }

            public bool MoveNext()
            {
                return this.dataLabelEnumerator.MoveNext();
            }

            public void Reset()
            {
                this.dataLabelEnumerator.Reset();
            }
        }
    }
}
