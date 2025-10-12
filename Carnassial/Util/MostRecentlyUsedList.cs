using System;
using System.Collections;
using System.Collections.Generic;

namespace Carnassial.Util
{
    public class MostRecentlyUsedList<TElement> : IEnumerable<TElement>
    {
        private readonly LinkedList<TElement> list;
        private readonly int maximumElements;

        public MostRecentlyUsedList(int maximumElements)
        {
            this.list = new();
            this.maximumElements = maximumElements;
        }

        public MostRecentlyUsedList(IList? elements, int maximumElements)
            : this(maximumElements)
        {
            if (elements == null)
            {
                return;
            }
            if (elements.Count > maximumElements)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumElements), $"{nameof(elements)} has {elements.Count} items but a maximum of {maximumElements} items is allowed in the {this.GetType().Name}.");
            }

            for (int index = 0; index < elements.Count; ++index)
            {
                TElement? element = (TElement?)elements[index];
                if (element == null)
                {
                    throw new ArgumentOutOfRangeException(nameof(elements), $"Element {index} of {nameof(elements)} is null.");
                }
                this.list.AddLast(element);
            }
        }

        public int Count
        {
            get { return this.list.Count; }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.list.GetEnumerator();
        }

        public IEnumerator<TElement> GetEnumerator()
        {
            return this.list.GetEnumerator();
        }

        public bool IsFull()
        {
            return this.list.Count == this.maximumElements;
        }

        public void SetMostRecent(TElement mostRecent)
        {
            if (this.list.Remove(mostRecent) == false)
            {
                // item wasn't already in the list
                if (this.list.Count >= this.maximumElements)
                {
                    // list was full, drop the oldest item to make room for new item
                    this.list.RemoveLast();
                }
            }

            // make the item the most current in the list
            this.list.AddFirst(mostRecent);
        }

        public bool TryGetMostRecent(out TElement? mostRecent)
        {
            if (this.list.Count > 0)
            {
                mostRecent = this.list.First!.Value;
                return true;
            }

            mostRecent = default;
            return false;
        }

        public bool TryGetLeastRecent(out TElement? leastRecent)
        {
            if (this.list.Count > 0)
            {
                leastRecent = this.list.Last!.Value;
                return true;
            }

            leastRecent = default;
            return false;
        }

        public bool TryRemove(TElement value)
        {
            return this.list.Remove(value);
        }
    }
}
