using System;

namespace Timelapse.Images
{
    [Serializable]
    internal class ExifToolException : Exception
    {
        public ExifToolException(string msg) : base(msg)
        {
        }
    }
}
