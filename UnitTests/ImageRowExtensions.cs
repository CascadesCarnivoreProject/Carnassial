using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Timelapse.Database;

namespace Timelapse.UnitTests
{
    internal static class ImageRowExtensions
    {
        public static DateTimeOffset GetDateTime(this ImageRow image, TimeZoneInfo imageSetTimeZone)
        {
            DateTimeOffset imageDateTime;
            Assert.IsTrue(image.TryGetDateTime(imageSetTimeZone, out imageDateTime));
            return imageDateTime;
        }
    }
}
