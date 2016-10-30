﻿using System;
using System.Windows;
using System.Windows.Media;

namespace Carnassial.Images
{
    public class Marker
    {
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
        /// Gets or sets a value indicating whether the label has already been displayed.
        /// </summary>
        public bool LabelShownPreviously { get; set; }

        /// <summary>
        /// Gets or sets the marker's normalized location in the canvas, as a coordinate point on [0, 1], [0, 1].
        /// </summary>
        public Point Position { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to show the label next to the marker
        /// </summary>
        public bool ShowLabel { get; set; }

        /// <summary>
        /// Gets or sets the marker's tooltip text
        /// </summary>
        public string Tooltip { get; set; } // The label (not datalabel) associated with this marker. To be put in the tooltip and for highlighting.

        public Marker(string dataLabel, Point point)
        {
            this.ShowLabel = false;
            this.LabelShownPreviously = true;
            this.Brush = (SolidColorBrush)new BrushConverter().ConvertFromString(Constant.StandardColour);
            this.DataLabel = dataLabel;
            this.Emphasise = false;
            this.Position = point;
            this.Tooltip = null;
        }

        // find a point's relative location so it's size invariant
        public static Point ConvertPointToRatio(Point point, double width, double height)
        {
            Point ratio = new Point((double)point.X / (double)width, (double)point.Y / (double)height);
            return ratio;
        }

        // convert a relative location to a specific screen location
        public static Point ConvertRatioToPoint(Point ratio, double width, double height)
        {
            Point point = new Point(ratio.X * width, ratio.Y * height);
            return point;
        }
    }
}
