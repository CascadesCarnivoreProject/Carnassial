using Carnassial.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace Carnassial.UnitTests
{
    internal class MarkerExpectation
    {
        public MarkerExpectation()
        {
            this.UserDefinedCountersByDataLabel = new Dictionary<string, string>();
        }

        public long ID { get; set; }

        public Dictionary<string, string> UserDefinedCountersByDataLabel { get; private set; }

        public void Verify(MarkerRow markersForFile)
        {
            Assert.IsTrue(markersForFile.ID == this.ID, "{0}: Expected ID '{1}' but found '{2}'.", this.ID, this.ID, markersForFile.ID);

            foreach (KeyValuePair<string, string> userCounterExpectation in this.UserDefinedCountersByDataLabel)
            {
                MarkersForCounter markersForCounter = markersForFile[userCounterExpectation.Key];
                string pointList = markersForCounter.GetPointList();
                Assert.IsTrue(pointList == userCounterExpectation.Value, "{0}: Expected {1} to be '{2}' but found '{3}'.", this.ID, userCounterExpectation.Key, userCounterExpectation.Value, pointList);
            }
        }
    }
}
