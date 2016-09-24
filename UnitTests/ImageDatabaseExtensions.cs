using Carnassial.Database;
using System;
using System.Collections.Generic;

namespace Carnassial.UnitTests
{
    internal static class ImageDatabaseExtensions
    {
        public static IEnumerable<DateTimeOffset> GetImageTimes(this ImageDatabase imageDatabase)
        {
            TimeZoneInfo imageSetTimeZone = imageDatabase.ImageSet.GetTimeZone();
            foreach (ImageRow image in imageDatabase.ImageDataTable)
            {
                yield return image.GetDateTime(imageSetTimeZone);
            }
        }
    }
}
