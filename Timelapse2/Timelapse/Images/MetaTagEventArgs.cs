using System;

namespace Timelapse.Images
{
    /// <summary>
    /// The MetaTag event argument contains 
    /// - a reference to the MetaTag
    /// - an indication if this is a new just created tag (if true), or if its been deleted (if false)
    /// </summary>
    public class MetaTagEventArgs : EventArgs
    {
        /// <summary>
        /// Gets or sets a value indicating whether this is a new just created tag (if true), or if its been deleted (if false)
        /// </summary>
        public bool IsNew { get; set; }

        /// <summary>
        /// Gets or sets the MetaTag
        /// </summary>
        public MetaTag MetaTag { get; set; }

        /// <summary>
        /// The MetaTag event argument contains 
        /// - a reference to the MetaTag
        /// - an indication if this is a new just created tag (if true), or if its been deleted (if false)
        /// </summary>
        public MetaTagEventArgs(MetaTag tag, bool isNew)
        {
            this.MetaTag = tag;
            this.IsNew = isNew;
        }
    }
}
