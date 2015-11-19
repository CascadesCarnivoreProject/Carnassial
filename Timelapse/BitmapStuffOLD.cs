using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using System.Runtime.InteropServices;


namespace Timelapse
{
    //
    // Various classes for manipulating bitmaps
    //


    // An extention function to let us write into bitmaps
    public static class WriteableBitmapExtensions
    {
        public static void WritePixels(this WriteableBitmap bitmap, PixelColor[,] pixels, int x, int y)
        {
            int width = pixels.GetLength(0);
            int height = pixels.GetLength(1);
            bitmap.WritePixels(new System.Windows.Int32Rect(0, 0, width, height), pixels, width * 4 /* bpp of incoming pixels */, x, y);
        }
    }


    #region PixelBitmap
    // This code was modified from samples found at 
    // http://stackoverflow.com/questions/1176910/finding-specific-pixel-colors-of-a-bitmapimage
    public class PixelBitmap
    {
        public PixelColor[] Pixels { get; protected set; }
        public int Width { get; protected set; }
        public int Height { get; protected set; }

        // Returns the color of a pixel at a given x,y coordinate
        // Note no bounds checking is done
        public PixelColor this[int x, int y]
        {
            get
            {
                return this.Pixels[x + y * this.Width];
            }
        }

        // Constructor. Takes an image given to it, and coverts it into a BGRA32 format, i.e., an Blue, Green, Red, Alpha format
        public PixelBitmap(BitmapSource source)
        {
            if (source.Format != PixelFormats.Bgra32)
            {
                source = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            }
            this.Width = source.PixelWidth;
            this.Height = source.PixelHeight;
            var bpp = source.Format.BitsPerPixel;

            this.Pixels = new PixelColor[this.Width * this.Height];

            this.copyPixels(source, this.Pixels, this.Width * 4, 0);
        }

        // Copy pixels form a source into the PixelColor array
        unsafe void copyPixels(BitmapSource source, PixelColor[] pixels, int stride, int offset)
        {
            // fixed holds the item in memory while I am working on it
            fixed (PixelColor* buffer = &pixels[0])
            {
                source.CopyPixels(
                  new System.Windows.Int32Rect(0, 0, source.PixelWidth, source.PixelHeight),
                  (IntPtr)(buffer + offset),
                  pixels.Length * sizeof(PixelColor),
                  stride);
            }
        }
    }
    #endregion


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
    }

    // This extends BitmapSource (the 'this Bitmap source' does this), but it can only be done in a static class
    public static class BitmapSourceHelper
    {

        // Copy Pixels from the bitmap source int a PixelColor array, i.e., flattens out those pixels into one huge array
        public unsafe static void CopyPixelsEx(this BitmapSource source, PixelColor[] pixels, int stride, int offset)
        {
            fixed (PixelColor* buffer = &pixels[0])
                source.CopyPixels(
                  new System.Windows.Int32Rect(0, 0, source.PixelWidth, source.PixelHeight),
                  (IntPtr)(buffer + offset),
                  pixels.Length * sizeof(PixelColor),
                  stride);
        }
    }
}
