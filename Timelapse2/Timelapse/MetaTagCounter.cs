using System;
using System.Collections.Generic;

namespace Timelapse
{
    // A class representing counters for counting things. 
    public class MetaTagCounter
    {
        // A list of metatags
        // Each metatag represents the coordinates of an entity on the screen being counted
        public List<MetaTag> MetaTags { get; private set; }
        public String DataLabel { get; set; }  // The datalabel associated with this Metatag counter

        public MetaTagCounter()
        {
            this.MetaTags = new List<MetaTag>();
        }

        // Add a MetaTag to the list of MetaTags
        public void AddMetaTag(MetaTag mtag)
        {
            this.MetaTags.Add(mtag);
        }

        // Create a metatag with the given point and add it to the metatag list
        public MetaTag CreateMetaTag(System.Windows.Point point, string dataLabel)
        {
            MetaTag mtag = new MetaTag();

            mtag.Point = point;
            mtag.DataLabel = dataLabel;
            this.AddMetaTag(mtag);
            return mtag;
        }
    }
}
