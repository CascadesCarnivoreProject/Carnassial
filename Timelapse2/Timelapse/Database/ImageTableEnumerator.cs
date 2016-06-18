using System;
using System.Collections;
using System.Collections.Generic;

namespace Timelapse.Database
{
    public class ImageTableEnumerator : IEnumerator<ImageProperties>
    {
        protected ImageDatabase Database { get; private set; }

        // the current image, null if its no been set or if the database is empty
        public ImageProperties Current { get; private set; }
        public int CurrentRow { get; private set; }

        public ImageTableEnumerator(ImageDatabase imageDatabase) :
            this(imageDatabase, Constants.Database.InvalidRow)
        {
        }

        public ImageTableEnumerator(ImageDatabase imageDatabase, int startingPosition)
        {
            this.CurrentRow = startingPosition;
            this.Database = imageDatabase;

            // OK if this fails as ImageTableEnumerator..ctor(ImageDatabase) passes -1 to match default enumerator behaviour
            this.TryMoveToImage(startingPosition);
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        object IEnumerator.Current
        {
            get { return this.Current; }
        }

        /// <summary>
        /// Go to the next image, returning false if we can't (e.g., if we are at the end) 
        /// </summary>
        public bool MoveNext()
        {
            return this.TryMoveToImage(this.CurrentRow + 1);
        }

        public virtual void Reset()
        {
            this.Current = null;
            this.CurrentRow = Constants.Database.InvalidRow;
        }

        /// <summary>
        /// Go to the previous image, returning true if we can otherwise false (e.g., if we are at the beginning)
        /// </summary>
        public bool MovePrevious()
        {
            return this.TryMoveToImage(this.CurrentRow - 1);
        }

        /// <summary>
        /// Attempt to go to a particular image, returning true if we can otherwise false (e.g., if the index is out of range)
        /// Remember, that we are zero based, so (for example) and index of 5 will go to the sixth image
        /// </summary>
        public virtual bool TryMoveToImage(int imageRowIndex)
        {
            if (this.Database.IsImageRowInRange(imageRowIndex))
            {
                this.CurrentRow = imageRowIndex;
                // rebuild ImageProperties regardless of whether the row changed or not as this seek may be a refresh after a database change
                this.Current = new ImageProperties(this.Database.ImageDataTable.Rows[imageRowIndex]);
                return true;
            }

            return false;
        }

        protected virtual void Dispose(bool disposing)
        {
            // nothing to do but required by IEnumerator<T>
        }
    }
}
