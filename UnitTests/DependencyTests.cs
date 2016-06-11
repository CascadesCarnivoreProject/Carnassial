using Microsoft.VisualStudio.TestTools.UnitTesting;
using Timelapse.Util;

namespace Timelapse.UnitTests
{
    [TestClass]
    public class DependencyTests
    {
        [TestMethod]
        public void RequiredBinaries()
        {
            Assert.IsTrue(Dependencies.AreRequiredBinariesPresent(this.GetType().Assembly));
        }
    }
}
