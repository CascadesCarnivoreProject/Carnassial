using Carnassial.Github;
using Carnassial.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Reflection;

namespace Carnassial.UnitTests
{
    [TestClass]
    public class VersionTests
    {
        [TestMethod]
        public void CheckForUpdates()
        {
            GithubReleaseClient carnassialUpdates = new GithubReleaseClient(Constant.ApplicationName, CarnassialConfigurationSettings.GetLatestReleaseApiAddress());
            Assert.IsTrue(carnassialUpdates.TryGetAndParseRelease(false, out Version publiclyAvailableVersion));

            Version currentVersion = typeof(CarnassialWindow).Assembly.GetName().Version;
            Assert.IsTrue(publiclyAvailableVersion <= currentVersion);
        }
    }
}
