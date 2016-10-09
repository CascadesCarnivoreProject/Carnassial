using Carnassial.Editor;
using Carnassial.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Carnassial.UnitTests
{
    [TestClass]
    public class DependencyTests
    {
        [TestMethod]
        public void RequiredBinaries()
        {
            Assert.IsTrue(Dependencies.AreRequiredBinariesPresent(Constant.ApplicationName, this.GetType().Assembly));
            Assert.IsTrue(Dependencies.AreRequiredBinariesPresent(EditorConstant.ApplicationName, this.GetType().Assembly));
        }
    }
}
