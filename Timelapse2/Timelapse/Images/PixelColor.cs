using System;
using System.Runtime.InteropServices;

namespace Timelapse.Images
{
    // PixelColor is structure representing a single pixel.
    // Most images are composed of 4 bytes, represent the Blue, Green, Red and Alpha value
    [StructLayout(LayoutKind.Explicit)]
    public struct PixelColor
    {
        // 32 bit BGRA 
        [FieldOffset(0)]
        public UInt32 ColorBGRA;
        // 8 bit components
        [FieldOffset(0)]
        public byte Blue;
        [FieldOffset(1)]
        public byte Green;
        [FieldOffset(2)]
        public byte Red;
        [FieldOffset(3)]
        public byte Alpha;

        public override string ToString()
        {
            return string.Format("B: {0}, G: {1}, R: {2}, A: {3}", this.Blue, this.Green, this.Red, this.Alpha);
        }
    }
}
