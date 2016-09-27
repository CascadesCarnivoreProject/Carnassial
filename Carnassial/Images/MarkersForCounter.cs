using System.Collections.Generic;
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

        public void AddMarker(string dataLabel, Point point)
        {
            Marker marker = new Marker();
            marker.Point = point;
            marker.DataLabel = dataLabel;
            this.AddMarker(marker);
        }
    }
}
