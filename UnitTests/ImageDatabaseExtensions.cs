using System;
using System.Collections.Generic;
using Timelapse.Database;

namespace Timelapse.UnitTests
{
    internal static class ImageDatabaseExtensions
    {
        public static IEnumerable<DateTime> GetImageTimes(this ImageDatabase imageDatabase)
        {
            foreach (ImageRow image in imageDatabase.ImageDataTable)
            {
                yield return image.GetDateTime();
            }
        }
    }
}
