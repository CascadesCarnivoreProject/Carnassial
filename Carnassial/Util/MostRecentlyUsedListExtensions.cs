using System.Collections.Specialized;
using System.Linq;

namespace Carnassial.Util
{
    public static class MostRecentlyUsedListExtensions
    {
        public static StringCollection ToStringCollection(this MostRecentlyUsedList<string> list)
        {
            StringCollection stringCollection = [.. list.ToArray()];
            return stringCollection;
        }
    }
}
