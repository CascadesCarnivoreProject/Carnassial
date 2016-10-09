using Carnassial.Database;
using System;
using System.Collections.Generic;

namespace Carnassial.UnitTests
{
    internal static class FileDatabaseExtensions
    {
        public static IEnumerable<DateTimeOffset> GetFileTimes(this FileDatabase fileDatabase)
        {
            foreach (ImageRow file in fileDatabase.Files)
            {
                yield return file.GetDateTime();
            }
        }
    }
}
