using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows;

namespace Carnassial.Images
{
    public class MarkersForCounter
    {
        // the counter's data label
        public string DataLabel { get; private set; }

        // the markers associated with the counter
        public List<Marker> Markers { get; private set; }

        public MarkersForCounter(string dataLabel)
        {
            this.DataLabel = dataLabel;
            this.Markers = new List<Marker>();
        }

        public void AddMarker(Marker marker)
        {
            this.Markers.Add(marker);
        }

        public void AddMarker(Point point)
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

        public void Parse(string pointList)
        {
            if (String.IsNullOrEmpty(pointList))
            {
                return;
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
                this.AddMarker(point);  // add the marker to the list
            }
        }

        public void RemoveMarker(Marker marker)
        {
            int index = this.Markers.IndexOf(marker);
            Debug.Assert(index != -1, "Expected marker to be present in list.");
            this.Markers.RemoveAt(index);
        }
    }
}
