using Carnassial.Database;
using System;
using System.Collections.Generic;

namespace Carnassial.UnitTests
{
    internal static class FileDatabaseExtensions
    {
        public static IEnumerable<DateTimeOffset> GetImageTimes(this FileDatabase fileDatabase)
        {
            foreach (ImageRow image in fileDatabase.Files)
            {
                yield return image.GetDateTime();
            }
        }
    }
}
