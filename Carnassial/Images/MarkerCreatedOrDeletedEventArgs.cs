using Carnassial.Data;
using System;

namespace Carnassial.Images
{
    public class MarkerCreatedOrDeletedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets a value indicating whether this marker was just created (true) or if it's just been deleted (false).
        /// </summary>
        public bool IsCreation { get; set; }

        public Marker Marker { get; set; }

        public MarkerCreatedOrDeletedEventArgs(Marker marker, bool isCreation)
        {
            this.IsCreation = isCreation;
            this.Marker = marker;
        }
    }
}
