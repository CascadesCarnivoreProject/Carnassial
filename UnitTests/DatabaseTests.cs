using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using Timelapse.Database;

namespace Timelapse.UnitTests
{
    [TestClass]
    public class DatabaseTests
    {
        [TestMethod]
        public void GenerateControls()
        {
            string folderPath = Environment.CurrentDirectory;
            TemplateDatabase template;
            TemplateDatabase.TryOpen(Path.Combine(folderPath, Constants.File.DefaultTemplateDatabaseFileName), out template);

            ImageDatabase database = new ImageDatabase(folderPath, Constants.File.DefaultImageDatabaseFileName);
            database.TryCreateImageDatabase(template);

            Controls controls = new Controls();
            controls.GenerateControls(database);

            int expectedControls = 6 + 10;
            Assert.IsTrue(controls.ControlFromDataLabel.Count == expectedControls, "Expected {0} controls to be generated but {1} were.", expectedControls, controls.ControlFromDataLabel.Count);
        }
    }
}
