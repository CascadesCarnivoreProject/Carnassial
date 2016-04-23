using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Timelapse.Images
{
    // This code was modified from samples found at 
    // http://stackoverflow.com/questions/1176910/finding-specific-pixel-colors-of-a-bitmapimage
    public class PixelBitmap
    {
        public PixelColor[] Pixels { get; protected set; }  // A flat array containing all the pixels
        public int Width { get; protected set; }            // Width/height of the bitmap
        public int Height { get; protected set; }

        // Returns the color of a pixel at a given x,y coordinate
        // Note no bounds checking is done to minimize overhead (if things were set up correctly, it shouldn't 
        // reference an out of bounds pixel)
        public PixelColor this[int x, int y]
        {
            get { return this.Pixels[x + y * this.Width]; }
        }

        private PixelBitmap()
        {
        }

        // Constructor. Takes an image given to it, and coverts it into a BGRA32 format, i.e., an Blue, Green, Red, Alpha format
        // Note: it does not check to see if its a valid image!
        public PixelBitmap(BitmapSource source)
        {
            if (source.Format != PixelFormats.Bgra32)
            {
                source = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            }
            this.Width = source.PixelWidth;
            this.Height = source.PixelHeight;

            this.Pixels = new PixelColor[this.Width * this.Height];
            // Note: this is about two thirds of CPU used for calculating combined differences.
            // If there is any way to speed this up, it would be really really nice.
            // TODO: Saul  would using WriteableBitmap.Pixels to avoid the need to clone pixels be helpful?  Buffer.BlockCopy() may also be worth a look
            this.CopyPixels(source, this.Pixels, this.Width * 4, 0);
        }

        public static bool IsDark(BitmapSource bmap, int darkPixelThreshold, double darkPixelRatio)
        {
            double ignore = 0;
            bool ignorebool;
            return IsDark(bmap, darkPixelThreshold, darkPixelRatio, out ignore, out ignorebool);
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
            for (int i = 0; i < image1.Pixels.Length; i += SKIPPIXELS)
            {
                // Check only every 20 pixels, as otherwise its a very expensive operation
                byte b = (byte)image1.Pixels[i].Blue;
                byte g = (byte)image1.Pixels[i].Green;
                byte r = (byte)image1.Pixels[i].Red;

                total_counted++;

                // The numbers below convert a particular color to its greyscale equivalent
                // Colors are not weighted equally. Since pure green is lighter than pure red and pure blue, it has a higher weight. 
                // Pure blue is the darkest of the three, so it receives the least weight.
                byte brightness = (byte)Math.Round(0.299 * r + +0.5876 * g + 0.114 * b);
                if (brightness <= darkPixelThreshold)
                {
                    dark_counted++;
                }

                // Check if the pixel is a grey scale vs. color pixel. Note the heuristic. 
                // Normally we should check r = g = b, but we allow a bit of slop as some cameras actually
                // have a bit of color in their dark shots (don't ask me why, it just happens). 
                // i.e. if the total delta is less than the color slop, then we consider it a grey level.
                if (GetRgbDelta(r, g, b) <= color_slop)
                {
                    grey_scale_counted++;
                }
            }

            // Check if its a grey scale image, i.e., at least 90% of the pixels in this image (given this slop) are grey scale.
            // If not, its a color image so judge it as not dark
            double greyScalePixelRatioFound = 1d * grey_scale_counted / total_counted;
            if (greyScalePixelRatioFound < greyScalePixelRatio)
            {
                darkPixelRatioFound = 1 - greyScalePixelRatioFound;
                isColor = true;
                return false;
            }
            darkPixelRatioFound = 1d * dark_counted / total_counted;
            isColor = false;
            return darkPixelRatioFound >= darkPixelRatio;
        }

        /// <summary>
        /// Given a pixel's rgb values, calculate the delta between all those values. This is used to determine if its a grey scale pixel.
        /// In practice a grey scale pixel's rgb are all equal (i.e., delta = 0) but we need the value as we want to see how 'close' the pixel 
        /// actually is to 0, i.e., to allow some slop in determining grey versus color pixels.
        /// </summary>
        private static int GetRgbDelta(byte r, byte g, byte b)
        {
            return
                Math.Abs(r - g) +
                Math.Abs(g - b) +
                Math.Abs(b - r);
        }

        // Given two images, return an image containing the visual difference between them
        public static PixelBitmap operator -(PixelBitmap image1, PixelBitmap image2)
        {
            // fail if image1 is not same size as image2
            if ((image1.Width != image2.Width) ||
                (image1.Height != image2.Height))
            {
                return null;
            }

            PixelColor[] pixelColor = new PixelColor[image1.Width * image1.Height];
            for (int i = 0; i < pixelColor.Length; i++)
            {
                byte b = (byte)Math.Abs(image1.Pixels[i].Blue - image2.Pixels[i].Blue);
                byte g = (byte)Math.Abs(image1.Pixels[i].Green - image2.Pixels[i].Green);
                byte r = (byte)Math.Abs(image1.Pixels[i].Red - image2.Pixels[i].Red);
                byte a = byte.MaxValue; // opaque
                byte diff = (byte)(b / 3 + g / 3 + r / 3); // Average the differences

                // Add that pixel to the image
                PixelColor pixel = new PixelColor() { Alpha = a, Red = diff, Blue = diff, Green = diff };
                pixelColor[i] = pixel;
            }
            return new PixelBitmap() { Width = image1.Width, Height = image1.Height, Pixels = pixelColor };
        }

        // Given three images, return an image that highlights the differences in common betwen the main image and the first image,
        // and the main image and a second image. 
        public static PixelBitmap CombinedDifference(BitmapSource unaltered, BitmapSource previous, BitmapSource next, byte threshold)
        {
            // fail if the images aren't all the same size
            if ((unaltered.PixelWidth != previous.PixelWidth) ||
                (unaltered.PixelHeight != previous.PixelHeight) ||
                (unaltered.PixelWidth != next.PixelWidth) ||
                (unaltered.PixelHeight != next.PixelHeight))
            {
                return null;
            }

            PixelBitmap mainImg = new PixelBitmap((BitmapSource)unaltered);
            PixelBitmap img1 = new PixelBitmap((BitmapSource)previous);
            PixelBitmap img2 = new PixelBitmap((BitmapSource)next);

            PixelColor[] pixelColor = new PixelColor[mainImg.Width * mainImg.Height]; // CHECK THIS - BOUNDS MAY NOT BE RIGHT
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
            return new PixelBitmap() { Width = mainImg.Width, Height = mainImg.Height, Pixels = pixelColor };
        }

        public static byte TempCalc(System.Byte threshold, System.Byte p, System.Byte n)
        {
            return (p > threshold && n > threshold) ? (byte)((p + n) / 2) : (byte)0;
        }

        // Copy pixels from a source into the PixelColor array
        private unsafe void CopyPixels(BitmapSource source, PixelColor[] pixels, int stride, int offset)
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

        public unsafe WriteableBitmap ToBitmap()
        {
            var wb = new WriteableBitmap(this.Width, this.Height, 96, 96, PixelFormats.Bgra32, null);
            // fixed holds the item in memory while I am working on it
            fixed (PixelColor* buffer = &this.Pixels[0])
            {
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

        public override string ToString()
        {
            return string.Format("PixelBitmap ({0}x{1})", this.Width, this.Height);
        }
    }
}
