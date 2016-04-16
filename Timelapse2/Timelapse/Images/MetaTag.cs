using System;
using System.Windows;
using System.Windows.Media;

namespace Timelapse.Images
{
    /// <summary>
    /// A MetaTag instance contains data describing a marker's appearance and associated metadata.
    /// </summary>
    public class MetaTag
    {
        /// <summary>
        /// Gets or sets the marker's location in the canvas, as a coordinate point
        /// </summary>
        public Point Point { get; set; }

        /// <summary>
        /// Gets or sets the marker's outline color
        /// </summary>
        public Brush Brush { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to visually emphasize the marker
        /// </summary>
        public bool Emphasise { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to show the label next to the metatag
        /// </summary>
        public bool Annotate { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the annotation has already been displayed (so we can turn it off)
        /// </summary>
        public bool AnnotationAlreadyShown { get; set; }

        /// <summary>
        /// Gets or sets the marker's tooltip text
        /// </summary>
        public string Label { get; set; } // The label (not datalabel) associated with this metatag. To be put in the tooltip and for highlighting

        /// <summary>
        /// Gets or sets the data label associated with this metatag
        /// </summary>
        public string DataLabel { get; set; }

        /// <summary>
        /// Gets or sets a GUID; its filled in on Metatag creation, but you can reset it
        /// </summary>
        public Guid Guid { get; set; }

        /// <summary>
        /// Initialize an instance of the metatag, which fills in some default values
        /// </summary>
        public MetaTag()
        {
            this.Annotate = false;
            this.AnnotationAlreadyShown = true;
            this.Brush = (SolidColorBrush)new BrushConverter().ConvertFromString(Constants.StandardColour);
            this.Emphasise = false;
            this.Guid = Guid.NewGuid();
            this.Label = String.Empty;
        }
    }
}
