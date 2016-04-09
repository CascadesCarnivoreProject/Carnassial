using System;

namespace BBCSharp
{
    [Serializable]
    internal class ExifToolException : Exception
    {
        public ExifToolException(string msg) : base(msg)
        {
        }
    }
}
