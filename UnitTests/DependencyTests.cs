using Microsoft.VisualStudio.TestTools.UnitTesting;
using Timelapse.Util;
using TimelapseTemplateEditor;

namespace Timelapse.UnitTests
{
    [TestClass]
    public class DependencyTests
    {
        [TestMethod]
        public void RequiredBinaries()
        {
            Assert.IsTrue(Dependencies.AreRequiredBinariesPresent(Constants.ApplicationName, this.GetType().Assembly));
            Assert.IsTrue(Dependencies.AreRequiredBinariesPresent(EditorConstant.ApplicationName, this.GetType().Assembly));
        }
    }
}
