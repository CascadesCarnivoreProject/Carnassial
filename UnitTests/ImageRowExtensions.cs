using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Timelapse.Database;

namespace Timelapse.UnitTests
{
    internal static class ImageRowExtensions
    {
        public static DateTime GetDateTime(this ImageRow image)
        {
            DateTime imageDate;
            Assert.IsTrue(image.TryGetDateTime(out imageDate));
            return imageDate;
        }
    }
}
