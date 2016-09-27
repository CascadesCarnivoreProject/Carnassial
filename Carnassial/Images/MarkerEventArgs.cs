using System;

namespace Carnassial.Images
{
    public class MarkerEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets a value indicating whether this is a new just created tag (if true), or if its been deleted (if false)
        /// </summary>
        public bool IsNew { get; set; }

        public Marker Marker { get; set; }

        public MarkerEventArgs(Marker marker, bool isNew)
        {
            this.Marker = marker;
            this.IsNew = isNew;
        }
    }
}
