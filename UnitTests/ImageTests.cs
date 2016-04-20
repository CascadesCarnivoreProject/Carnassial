using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using Timelapse;
using Timelapse.Database;

namespace UnitTests
{
    [TestClass]
    public class ImageTests
    {
        [TestMethod]
        public void Exif()
        {
            string folderPath = Environment.CurrentDirectory;
            string imageFile = "BushnellTrophyHD-119677C-20160224-056.JPG";

            ImageDatabase database = new ImageDatabase(folderPath, Constants.File.DefaultImageDatabaseFileName);
            DialogPopulateFieldWithMetadata populateFieldDialog = new DialogPopulateFieldWithMetadata(database, imageFile, folderPath);
            populateFieldDialog.LoadExif();
            Dictionary<string, string> exif = (Dictionary<string, string>)populateFieldDialog.dg.ItemsSource;
            Assert.IsTrue(exif.Count > 0, "Expected at least one EXIF field to be retrieved from {0}", Path.Combine(folderPath, imageFile));
        }
    }
}
