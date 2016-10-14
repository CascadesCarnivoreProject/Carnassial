using Carnassial.Github;
using Carnassial.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Carnassial.UnitTests
{
    [TestClass]
    public class VersionTests
    {
        [TestMethod]
        public void CheckForUpdates()
        {
            GithubReleaseClient carnassialUpdates = new GithubReleaseClient(Constant.ApplicationName, CarnassialConfigurationSettings.GetLatestReleaseApiAddress());
            // TODO: change to IsTrue and add additional checking once a prerelease has been generated to test against
            Assert.IsFalse(carnassialUpdates.TryGetAndParseRelease(false));
        }
    }
}
