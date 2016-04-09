﻿using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using System.Runtime.InteropServices;

namespace Timelapse
{
    //
    // Various classes for manipulating bitmaps
    //

    #region Class WriteableBitmapExtensions - An extention function to let us write into bitmaps
    public static class WriteableBitmapExtensions
    {
        public static void WritePixels(this WriteableBitmap bitmap, PixelColor[,] pixels, int x, int y)
        {
            int width = pixels.GetLength(0);
            int height = pixels.GetLength(1);
            bitmap.WritePixels(new System.Windows.Int32Rect(0, 0, width, height), pixels, width * 4 /* bpp of incoming pixels */, x, y);
        }
    }
    #endregion

    #region PixelBitmap
    // This code was modified from samples found at 
    // http://stackoverflow.com/questions/1176910/finding-specific-pixel-colors-of-a-bitmapimage
    public class PixelBitmap
    {
        public PixelColor[] Pixels { get; protected set; }  //A flat array containing all the pixels
        public int Width { get; protected set; }            // Width/height of the bitmap
        public int Height { get; protected set; }

        // Returns the color of a pixel at a given x,y coordinate
        // Note no bounds checking is done to minimize overhead (if things were set up correctly, it shouldn't 
        // reference an out of bounds pixel)
        public PixelColor this[int x, int y] {get { return this.Pixels[x + y * this.Width]; } }

        // Constructor. Takes an image given to it, and coverts it into a BGRA32 format, i.e., an Blue, Green, Red, Alpha format
        // Note: it does not check to see if its a valid image!
        // Note: this is slow. If there is any way to speed this up, it would be really really nice.
        private PixelBitmap() { }
        public  PixelBitmap(BitmapSource source)
        {
            if (source.Format != PixelFormats.Bgra32)
            {
                source = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            }
            this.Width = source.PixelWidth;
            this.Height = source.PixelHeight;

            this.Pixels = new PixelColor[this.Width * this.Height];
            this.copyPixels(source, this.Pixels, this.Width * 4, 0);
        }

        public static bool IsDark(BitmapSource bmap, int darkPixelThreshold, double darkPixelRatio)
        {
            double ignore = 0;
            bool ignorebool;
            return (IsDark(bmap, darkPixelThreshold, darkPixelRatio, out ignore, out ignorebool));
        }

        // Return whether the image is mostly dark. This is done by counting the number of pixels that are
        // below a certain tolerance (i.e., mostly black), and seeing if the % of mostly dark pixels are
        // at or higher than the given darkPercent.
        // We also check to see if the image is predominantly color. We do this by checking to see if pixels are grey scale
        // (where r=g=b, with a bit of slop added) and then check that against a threshold.
        public static bool IsDark(BitmapSource bmap, int darkPixelThreshold, double darkPixelRatio, out double darkPixelRatioFound, out bool isColor)
        {
            PixelBitmap image1 = new PixelBitmap(bmap);
            int dark_counted = 0;
            int total_counted = 0;

            int color_slop = 40; // A grey scale pixel has r = g = b. But we will allow some slop in here just in case a bit of color creeps in
            int grey_scale_counted = 0;
            double greyScalePixelRatio = .9; // A greyscale image (given the above slop) will typically have about 90% of its pixels as grey scale

            const int SKIPPIXELS = 20;
            for (int i = 0; i < image1.Pixels.Length; i += SKIPPIXELS) // Check only every 20 pixels, as otherwise its a very expensive operation
            {
                byte b = (byte)image1.Pixels[i].Blue;
                byte g = (byte)image1.Pixels[i].Green;
                byte r = (byte)image1.Pixels[i].Red;

                total_counted++;

                // The numbers below convert a particular color to its greyscale equivalent
                // Colors are not weighted equally. Since pure green is lighter than pure red and pure blue, it has a higher weight. 
                // Pure blue is the darkest of the three, so it receives the least weight.
                byte brightness = (byte)Math.Round((0.299 * r + +0.5876 * g + 0.114 * b));
                if (brightness <= darkPixelThreshold) dark_counted++;


                // Check if the pixel is a grey scale vs. color pixel. Note the heuristic. 
                // Normally we should check r = g = b, but we allow a bit of slop as some cameras actually
                // have a bit of color in their dark shots (don't ask me why, it just happens). 
                // i.e. if the total delta is less than the color slop, then we consider it a grey level.
                if (GetRgbDelta(r, g, b) <= color_slop) grey_scale_counted++;
            }

            // Check if its a grey scale image, i.e., at least 90% of the pixels in this image (given this slop) are grey scale.
            // If not, its a color image so judge it as not dark
            double greyScalePixelRatioFound = (1d * grey_scale_counted / total_counted);
            if (greyScalePixelRatioFound < greyScalePixelRatio)
            {
                darkPixelRatioFound = 1 - greyScalePixelRatioFound;
                isColor = true;
                return false;
            }
            darkPixelRatioFound = (1d * dark_counted / total_counted);
            isColor = false;
            return (darkPixelRatioFound >= darkPixelRatio);
        }
        /// <summary>
        /// Given a pixel's rgb values, calculate the delta between all those values. This is used to determine if its a grey scale pixel.
        /// In practice a grey scale pixel's rgb are all equal (i.e., delta = 0) but we need the value as we want to see how 'close' the pixel 
        /// actually is to 0, i.e., to allow some slop in determining grey vs. color pixels.
        /// </summary>
        /// <param name="r"></param>
        /// <param name="g"></param>
        /// <param name="b"></param>
        /// <returns>delta</returns>
        private static int GetRgbDelta(byte r, byte g, byte b)
        {
            return
                Math.Abs(r - g) +
                Math.Abs(g - b) +
                Math.Abs(b - r);
        }

        // Given two images, return an image containing the visual difference between them
        public static PixelBitmap operator -(PixelBitmap image1, PixelBitmap image2) {
            // if image1 is not same size as image2, use the smaller of their dimensions 
            int width = Math.Min (image1.Width, image2.Width) ;
            int height = Math.Min (image1.Height, image2.Height);

            //PixelColor[] pixelColor = new PixelColor[image1.Pixels.Length];
            PixelColor[] pixelColor = new PixelColor[width * height];
            for (int i = 0; i < pixelColor.Length; i++) 
            {
                byte b = (byte) Math.Abs(image1.Pixels[i].Blue - image2.Pixels[i].Blue);
                byte g = (byte)Math.Abs(image1.Pixels[i].Green - image2.Pixels[i].Green);
                byte r = (byte)Math.Abs(image1.Pixels[i].Red - image2.Pixels[i].Red);
                byte a = byte.MaxValue; // opaque
                byte diff = (byte)(b / 3 + g / 3 + r / 3); // Average the differences

                // Add that pixel to the image
                PixelColor pixel = new PixelColor() { Alpha = a, Red = diff, Blue = diff, Green = diff };
                pixelColor[i] = pixel;
            }
            return new PixelBitmap() { Width = width, Height = height, Pixels = pixelColor };
        }

        // Given three images, return an image that highlights the differences in common betwen the main image and the first image,
        // and the main image and a second image. 
        //public static PixelBitmap Difference(PixelBitmap mainImg, PixelBitmap img1, PixelBitmap img2, byte threshold)
        public static PixelBitmap Difference(BitmapSource mainImgBM, BitmapSource img1BM, BitmapSource img2BM, byte threshold)
        {
            PixelBitmap mainImg = new PixelBitmap((BitmapSource)mainImgBM);
            PixelBitmap img1 = new PixelBitmap((BitmapSource)img1BM);
            PixelBitmap img2 = new PixelBitmap((BitmapSource)img2BM);
            // if images are not same size , use the smaller of their dimensions 
            int width = Math.Min(mainImg.Width, img1.Width);
            width = Math.Min(width, img2.Width);
            int height = Math.Min(mainImg.Height, img1.Height);
            height = Math.Min(height, img2.Height);

            PixelColor[] pixelColor = new PixelColor[width * height]; //CHECK THIS - BOUNDS MAY NOT BE RIGHT
            for (int i = 0; i < pixelColor.Length; i++)
            {
                byte b1 = (byte)Math.Abs(mainImg.Pixels[i].Blue - img1.Pixels[i].Blue);
                byte b2 = (byte)Math.Abs(mainImg.Pixels[i].Blue - img2.Pixels[i].Blue);
                byte b = PixelBitmap.TempCalc(threshold, b1, b2);

                byte g1 = (byte)Math.Abs(mainImg.Pixels[i].Green - img1.Pixels[i].Green);
                byte g2 = (byte)Math.Abs(mainImg.Pixels[i].Green - img2.Pixels[i].Green);
                byte g = PixelBitmap.TempCalc(threshold, g1, g2);

                byte r1 = (byte)Math.Abs(mainImg.Pixels[i].Red - img1.Pixels[i].Red);
                byte r2 = (byte)Math.Abs(mainImg.Pixels[i].Red - img2.Pixels[i].Red);
                byte r = PixelBitmap.TempCalc(threshold, r1, r2);

                byte a = byte.MaxValue; // opaque
                var diff = (byte)(b / 3 + g / 3 + r / 3);
                var pixel = new PixelColor() { Alpha = a, Red = diff, Blue = diff, Green = diff };
                pixelColor[i] = pixel;
            }
            return new PixelBitmap() { Width = width, Height = height, Pixels = pixelColor };
        }

        public static byte TempCalc (System.Byte threshold, System.Byte p, System.Byte n)
        {
            return ((p > threshold && n > threshold) ? (byte) ((p + n) / 2 ): (byte)0);
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

        unsafe public WriteableBitmap ToBitmap() {
            var wb = new WriteableBitmap(this.Width, this.Height, 96, 96, PixelFormats.Bgra32, null);
            // fixed holds the item in memory while I am working on it
            fixed (PixelColor* buffer = &this.Pixels[0]) {
                var offset = 0;
                var stride = this.Width * 4;
                wb.WritePixels(
                  new System.Windows.Int32Rect(0, 0, this.Width, this.Height),
                  (IntPtr)(buffer + offset),
                  this.Pixels.Length * sizeof(PixelColor),
                  stride);
            }

            return wb;
        }

        public override string ToString() {
            return string.Format("PixelBitmap ({0}x{1})", this.Width, this.Height);
        }
    }
    #endregion

    #region Class Pixel Color - represents a single RGBA pixel
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

        public override string ToString() {
            return string.Format("B: {0}, G: {1}, R: {2}, A: {3}", this.Blue, this.Green, this.Red, this.Alpha);
        }
    }
    #endregion

    #region Class BitmapSourceHelper - copies pixels from a source bitmap into a single flat array

    // This extends BitmapSource (the 'this Bitmap source' does this), but it can only be done in a static class
    public static class BitmapSourceHelper
    {
        // Copy Pixels from the bitmap source into a PixelColor array, i.e., flattens out those pixels into one huge array
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
    #endregion 
}