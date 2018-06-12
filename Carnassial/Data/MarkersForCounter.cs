using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;

namespace Carnassial.Data
{
    public class MarkersForCounter : INotifyPropertyChanged
    {
        public int Count { get; private set; }
        public string DataLabel { get; private set; }
        public List<Marker> Markers { get; private set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public MarkersForCounter(string dataLabel, int count)
        {
            this.Count = count;
            this.DataLabel = dataLabel;
            this.Markers = new List<Marker>();
        }

        public void AddMarker(Marker marker)
        {
            ++this.Count;
            this.Markers.Add(marker);
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.Markers)));
        }

        internal void AddMarker(Point point)
        {
            this.AddMarker(new Marker(this.DataLabel, point));
        }

        public static MarkersForCounter Parse(string dataLabel, string databaseString)
        {
            Debug.Assert(String.IsNullOrWhiteSpace(dataLabel) == false, "Data label is null or empty.");
            if (String.IsNullOrWhiteSpace(databaseString))
            {
                throw new ArgumentOutOfRangeException(nameof(databaseString));
            }

            string[] tokens = databaseString.Split(Constant.Database.BarDelimiter);
            if ((tokens == null) || (tokens.Length < 1))
            {
                throw new ArgumentOutOfRangeException(nameof(databaseString));
            }

            int count = Int32.Parse(tokens[0]);
            MarkersForCounter markers = new MarkersForCounter(dataLabel, count);

            for (int point = 1; point < tokens.Length; ++point)
            {
                markers.Markers.Add(new Marker(dataLabel, Point.Parse(tokens[point])));
            }

            return markers;
        }

        public void RemoveMarker(Marker marker)
        {
            for (int index = 0; index < this.Markers.Count; ++index)
            {
                Marker candidate = this.Markers[index];
                if ((marker.Position == candidate.Position) &&
                    String.Equals(marker.DataLabel, candidate.DataLabel, StringComparison.Ordinal))
                {
                    this.Markers.RemoveAt(index);
                    --this.Count;
                    this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.Markers)));
                    return;
                }
            }

            Debug.Fail("Attempt to remove marker unattached to counter.");
        }

        public string ToDatabaseString()
        {
            StringBuilder pointList = new StringBuilder(this.Count.ToString());
            foreach (Marker marker in this.Markers)
            {
                pointList.Append(Constant.Database.BarDelimiter);
                pointList.AppendFormat("{0:0.000},{1:0.000}", marker.Position.X, marker.Position.Y);
            }
            return pointList.ToString();
        }
    }
}
