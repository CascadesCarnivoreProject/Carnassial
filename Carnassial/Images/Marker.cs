using System;
using System.Windows;
using System.Windows.Media;

namespace Carnassial.Images
{
    public class Marker
    {
        /// <summary>
        /// Gets or sets a value indicating whether to show the label next to the marker
        /// </summary>
        public bool Annotate { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the annotation has already been displayed (so we can turn it off)
        /// </summary>
        public bool AnnotationPreviouslyShown { get; set; }

        /// <summary>
        /// Gets or sets the marker's outline color
        /// </summary>
        public Brush Brush { get; set; }

        /// <summary>
        /// Gets or sets the data label associated with this marker
        /// </summary>
        public string DataLabel { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to visually emphasize the marker
        /// </summary>
        public bool Emphasise { get; set; }

        /// <summary>
        /// Gets or sets a GUID; its filled in on marker creation, but you can reset it
        /// </summary>
        public Guid Guid { get; set; }

        /// <summary>
        /// Gets or sets the marker's normalized location in the canvas, as a coordinate point on [0, 1], [0, 1].
        /// </summary>
        public Point Position { get; set; }

        /// <summary>
        /// Gets or sets the marker's tooltip text
        /// </summary>
        public string Tooltip { get; set; } // The label (not datalabel) associated with this marker. To be put in the tooltip and for highlighting.

        public Marker(string dataLabel, Point point)
        {
            this.Annotate = false;
            this.AnnotationPreviouslyShown = true;
            this.Brush = (SolidColorBrush)new BrushConverter().ConvertFromString(Constant.StandardColour);
            this.DataLabel = dataLabel;
            this.Emphasise = false;
            this.Guid = Guid.NewGuid();
            this.Position = point;
            this.Tooltip = null;
        }

        // Calculate the point as a ratio of its position on the image, so we can locate it regardless of the actual image size
        public static Point ConvertPointToRatio(Point p, double width, double height)
        {
            Point ratioPt = new Point((double)p.X / (double)width, (double)p.Y / (double)height);
            return ratioPt;
        }

        // The inverse of the above operation
        public static Point ConvertRatioToPoint(Point p, double width, double height)
        {
            Point imagePt = new Point(p.X * width, p.Y * height);
            return imagePt;
        }
    }
}
