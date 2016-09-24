using Carnassial.Editor;
using Carnassial.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Carnassial.UnitTests
{
    [TestClass]
    public class VersionTests
    {
        // [TestMethod] disabled as there's not currently a version server for Carnassial
        public void CheckForUpdates()
        {
            VersionClient carnassialUpdates = new VersionClient(Constants.ApplicationName, CarnassialConfigurationSettings.GetLatestVersionAddress());
            Assert.IsTrue(carnassialUpdates.TryGetAndParseVersion(false));

            VersionClient editorUpdates = new VersionClient(EditorConstant.ApplicationName, CarnassialConfigurationSettings.GetLatestVersionAddress());
            Assert.IsTrue(editorUpdates.TryGetAndParseVersion(false));
        }
    }
}
