using Microsoft.VisualStudio.TestTools.UnitTesting;
using Timelapse.Editor;
using Timelapse.Util;

namespace Timelapse.UnitTests
{
    [TestClass]
    public class VersionTests
    {
        [TestMethod]
        public void CheckForUpdates()
        {
            VersionClient timelapseUpdates = new VersionClient(Constants.ApplicationName, Constants.LatestVersionAddress);
            Assert.IsTrue(timelapseUpdates.TryGetAndParseVersion(false));

            VersionClient editorUpdates = new VersionClient(EditorConstant.ApplicationName, EditorConstant.LatestVersionAddress);
            Assert.IsTrue(editorUpdates.TryGetAndParseVersion(false));
        }
    }
}
