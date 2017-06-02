using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Windows;

namespace Carnassial.Data
{
    public class MarkersForCounter : INotifyPropertyChanged
    {
        // the counter's data label
        public string DataLabel { get; private set; }

        // the markers associated with the counter
        public List<Marker> Markers { get; private set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public MarkersForCounter(string dataLabel)
        {
            this.DataLabel = dataLabel;
            this.Markers = new List<Marker>();
        }

        public void AddMarker(Marker marker)
        {
            this.Markers.Add(marker);
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.Markers)));
        }

        internal void AddMarker(Point point)
        {
            this.AddMarker(new Marker(this.DataLabel, point));
        }

        public string GetPointList()
        {
            StringBuilder pointList = new StringBuilder();
            foreach (Marker markerForCounter in this.Markers)
            {
                if (pointList.Length > 0)
                {
                    pointList.Append(Constant.Database.MarkerBar); // don't put a separator at the beginning of the point list
                }
                pointList.AppendFormat("{0:0.000},{1:0.000}", markerForCounter.Position.X, markerForCounter.Position.Y); // Add a point in the form x,y e.g., 0.500, 0.700
            }
            return pointList.ToString();
        }

        public static MarkersForCounter Parse(string dataLabel, string pointList)
        {
            MarkersForCounter markers = new MarkersForCounter(dataLabel);
            if (String.IsNullOrEmpty(pointList))
            {
                return markers;
            }

            char[] delimiterBar = { Constant.Database.MarkerBar };
            string[] pointsAsStrings = pointList.Split(delimiterBar);
            List<Point> points = new List<Point>();
            foreach (string pointAsString in pointsAsStrings)
            {
                Point point = Point.Parse(pointAsString);
                points.Add(point);
            }

            foreach (Point point in points)
            {
                markers.AddMarker(point);  // add the marker to the list
            }

            return markers;
        }

        public void RemoveMarker(Marker marker)
        {
            int index = this.Markers.IndexOf(marker);
            Debug.Assert(index != -1, "Attempt to remove marker unattached to counter.");
            this.Markers.RemoveAt(index);
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.Markers)));
        }
    }
}
