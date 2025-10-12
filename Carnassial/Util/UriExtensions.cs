using System;

namespace Carnassial.Util
{
    public static class UriExtensions
    {
        public static string ToEmailAddress(this Uri uri)
        {
            return $"{uri.UserInfo}@{uri.Host}";
        }
    }
}
