using System;
using System.Collections;
using System.Collections.Generic;

namespace Timelapse
{
    public class MostRecentlyUsedList<TElement> : IEnumerable<TElement>
    {
        private LinkedList<TElement> list;
        private int maximumItems;

        public MostRecentlyUsedList(int maximumItems)
        {
            this.list = new LinkedList<TElement>();

            // subtract one off the list's maximum size as SetMostRecent() checks it after 
            this.maximumItems = maximumItems;
        }

        public int Count
        {
            get { return this.list.Count; }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.list.GetEnumerator();
        }

        IEnumerator<TElement> IEnumerable<TElement>.GetEnumerator()
        {
            return this.list.GetEnumerator();
        }

        public void SetMostRecent(TElement mostRecent)
        {
            if (this.list.Remove(mostRecent) == false)
            {
                // item wasn't already in the list
                if (this.list.Count >= this.maximumItems)
                {
                    // list was full, drop the oldest item to make room for new item
                    this.list.RemoveLast();
                }
            }

            // make the item the most current in the list
            this.list.AddFirst(mostRecent);
        }

        public bool TryGetMostRecent(out TElement mostRecent)
        {
            if (this.list.Count > 0)
            {
                mostRecent = this.list.First.Value;
                return true;
            }

            mostRecent = default(TElement);
            return false;
        }

        public bool TryRemove(TElement value)
        {
            return this.list.Remove(value);
        }
    }
}
