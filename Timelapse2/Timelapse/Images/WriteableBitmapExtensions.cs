using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Timelapse.Images
{
    /// <summary>
    /// Extends <see cref="WriteableBitmap"/> with differencing calculations.
    /// </summary>
    /// <remarks>This class consumes WriteableBitmap.BackBuffer in unsafe operations for speed.  Other Windows Presentation Foundation bitmap classes do not
    /// expose in memory content of bitmaps, requiring somewhat expensive double buffering for image calculations, and using Marshal to obtain pixels from
    /// the backing buffer is substantially slower than direct access.</remarks>
    public static class WriteableBitmapExtensions
    {
        // Given three images, return an image that highlights the differences in common betwen the main image and the first image
        // and the main image and a second image.
        public static unsafe WriteableBitmap CombinedDifference(this WriteableBitmap unaltered, WriteableBitmap previous, WriteableBitmap next, byte threshold)
        {
            if (WriteableBitmapExtensions.BitmapsMismatched(unaltered, previous) ||
                WriteableBitmapExtensions.BitmapsMismatched(unaltered, next))
            {
                return null;
            }

            int blueOffset;
            int greenOffset;
            int redOffset;
            WriteableBitmapExtensions.GetColorOffsets(unaltered, out blueOffset, out greenOffset, out redOffset);

            int totalPixels = unaltered.PixelWidth * unaltered.PixelHeight;
            int pixelSizeInBytes = unaltered.Format.BitsPerPixel / 8;
            byte* unalteredIndex = (byte*)unaltered.BackBuffer.ToPointer();
            byte* previousIndex = (byte*)previous.BackBuffer.ToPointer();
            byte* nextIndex = (byte*)next.BackBuffer.ToPointer();
            byte[] differencePixels = new byte[totalPixels * pixelSizeInBytes];
            int differenceIndex = 0;
            for (int pixel = 0; pixel < totalPixels; ++pixel)
            {
                byte b1 = (byte)Math.Abs(*(unalteredIndex + blueOffset) - *(previousIndex + blueOffset));
                byte g1 = (byte)Math.Abs(*(unalteredIndex + greenOffset) - *(previousIndex + greenOffset));
                byte r1 = (byte)Math.Abs(*(unalteredIndex + redOffset) - *(previousIndex + redOffset));

                byte b2 = (byte)Math.Abs(*(unalteredIndex + blueOffset) - *(nextIndex + blueOffset));
                byte g2 = (byte)Math.Abs(*(unalteredIndex + greenOffset) - *(nextIndex + greenOffset));
                byte r2 = (byte)Math.Abs(*(unalteredIndex + redOffset) - *(nextIndex + redOffset));

                byte b = WriteableBitmapExtensions.DifferenceIfAboveThreshold(threshold, b1, b2);
                byte g = WriteableBitmapExtensions.DifferenceIfAboveThreshold(threshold, g1, g2);
                byte r = WriteableBitmapExtensions.DifferenceIfAboveThreshold(threshold, r1, r2);

                byte averageDifference = (byte)((b + g + r) / 3);
                differencePixels[differenceIndex + blueOffset] = averageDifference;
                differencePixels[differenceIndex + greenOffset] = averageDifference;
                differencePixels[differenceIndex + redOffset] = averageDifference;

                unalteredIndex += pixelSizeInBytes;
                previousIndex += pixelSizeInBytes;
                nextIndex += pixelSizeInBytes;
                differenceIndex += pixelSizeInBytes;
            }

            WriteableBitmap difference = new WriteableBitmap(BitmapSource.Create(unaltered.PixelWidth, unaltered.PixelHeight, unaltered.DpiX, unaltered.DpiY, unaltered.Format, unaltered.Palette, differencePixels, unaltered.BackBufferStride));
            return difference;
        }

        public static bool IsDark(this WriteableBitmap image, int darkPixelThreshold, double darkPixelRatio)
        {
            double dummy1;
            bool dummy2;
            return image.IsDark(darkPixelThreshold, darkPixelRatio, out dummy1, out dummy2);
        }

        // Return whether the image is mostly dark. This is done by counting the number of pixels that are
        // below a certain tolerance (i.e., mostly black), and seeing if the % of mostly dark pixels are
        // at or higher than the given darkPercent.
        // We also check to see if the image is predominantly color. We do this by checking to see if pixels are grey scale
        // (where r=g=b, with a bit of slop added) and then check that against a threshold.
        public static unsafe bool IsDark(this WriteableBitmap image, int darkPixelThreshold, double darkPixelRatio, out double darkPixelFraction, out bool isColor)
        {
            const int ColorThreshold = 40; // A grey scale pixel has r = g = b. But we will allow some slop in here just in case a bit of color creeps in
            const double GreyScalePixelThreshold = .9; // A greyscale image (given the above slop) will typically have about 90% of its pixels as grey scale
            const int PixelStride = 20;

            int blueOffset;
            int greenOffset;
            int redOffset;
            WriteableBitmapExtensions.GetColorOffsets(image, out blueOffset, out greenOffset, out redOffset);

            byte* imageIndex = (byte*)image.BackBuffer.ToPointer();
            int darkPixels = 0;
            int totalPixels = image.PixelHeight * image.PixelWidth;
            int uncoloredPixels = 0;
            for (int pixel = 0; pixel < totalPixels; pixel += PixelStride)
            {
                // Check only every 20 pixels, as otherwise its a very expensive operation
                byte b = *(imageIndex + blueOffset);
                byte g = *(imageIndex + greenOffset);
                byte r = *(imageIndex + redOffset);

                // The numbers below convert a particular color to its greyscale equivalent
                // Colors are not weighted equally. Since pure green is lighter than pure red and pure blue, it has a higher weight. 
                // Pure blue is the darkest of the three, so it receives the least weight.
                byte brightness = (byte)Math.Round(0.299 * r + +0.5876 * g + 0.114 * b);
                if (brightness <= darkPixelThreshold)
                {
                    ++darkPixels;
                }

                // Check if the pixel is a grey scale vs. color pixel. Note the heuristic. 
                // Normally we should check r = g = b, but we allow a bit of slop as some cameras actually
                // have a bit of color in their dark shots (don't ask me why, it just happens). 
                // i.e. if the total delta is less than the color slop, then we consider it a grey level.
                // Given a pixel's rgb values, calculate the delta between all those values. This is used to determine if its a grey scale pixel.
                // In practice a grey scale pixel's rgb are all equal (i.e., delta = 0) but we need the value as we want to see how 'close' the pixel 
                // actually is to 0, i.e., to allow some slop in determining grey versus color pixels.
                int rgbDelta = Math.Abs(r - g) + Math.Abs(g - b) + Math.Abs(b - r);
                if (rgbDelta <= ColorThreshold)
                {
                    ++uncoloredPixels;
                }
            }

            // Check if its a grey scale image, i.e., at least 90% of the pixels in this image (given this slop) are grey scale.
            // If not, its a color image so judge it as not dark
            double uncoloredPixelFraction = 1d * uncoloredPixels / totalPixels;
            if (uncoloredPixelFraction < GreyScalePixelThreshold)
            {
                darkPixelFraction = 1 - uncoloredPixelFraction;
                isColor = true;
                return false;
            }
            darkPixelFraction = 1d * darkPixels / totalPixels;
            isColor = false;
            return darkPixelFraction >= darkPixelRatio;
        }

        // Given two images, return an image containing the visual difference between them
        public static unsafe WriteableBitmap Subtract(this WriteableBitmap image1, WriteableBitmap image2)
        {
            if (WriteableBitmapExtensions.BitmapsMismatched(image1, image2))
            {
                return null;
            }

            int blueOffset;
            int greenOffset;
            int redOffset;
            WriteableBitmapExtensions.GetColorOffsets(image1, out blueOffset, out greenOffset, out redOffset);

            int totalPixels = image1.PixelWidth * image1.PixelHeight;
            int pixelSizeInBytes = image1.Format.BitsPerPixel / 8;
            byte* image1Index = (byte*)image1.BackBuffer.ToPointer();
            byte* image2Index = (byte*)image2.BackBuffer.ToPointer();
            byte[] differencePixels = new byte[totalPixels * pixelSizeInBytes];
            int differenceIndex = 0;
            for (int pixel = 0; pixel < totalPixels; ++pixel)
            {
                byte b = (byte)Math.Abs(*(image1Index + blueOffset) - *(image2Index + blueOffset));
                byte g = (byte)Math.Abs(*(image1Index + greenOffset) - *(image2Index + greenOffset));
                byte r = (byte)Math.Abs(*(image1Index + redOffset) - *(image2Index + redOffset));

                byte averageDifference = (byte)((b + g + r) / 3);
                differencePixels[differenceIndex + blueOffset] = averageDifference;
                differencePixels[differenceIndex + greenOffset] = averageDifference;
                differencePixels[differenceIndex + redOffset] = averageDifference;

                image1Index += pixelSizeInBytes;
                image2Index += pixelSizeInBytes;
                differenceIndex += pixelSizeInBytes;
            }

            WriteableBitmap difference = new WriteableBitmap(BitmapSource.Create(image1.PixelWidth, image1.PixelHeight, image1.DpiX, image1.DpiY, image1.Format, image1.Palette, differencePixels, image1.BackBufferStride));
            return difference;
        }

        private static bool BitmapsMismatched(WriteableBitmap image1, WriteableBitmap image2)
        {
            return (image1.PixelWidth != image2.PixelWidth) ||
                   (image1.PixelHeight != image2.PixelHeight) ||
                   (image1.Format != image2.Format);
        }

        private static byte DifferenceIfAboveThreshold(byte threshold, byte p, byte n)
        {
            return p > threshold && n > threshold ? (byte)((p + n) / 2) : Byte.MinValue;
        }

        private static void GetColorOffsets(WriteableBitmap image, out int blueOffset, out int greenOffset, out int redOffset)
        {
            if (image.Format == PixelFormats.Bgr24)
            {
                blueOffset = 0;
                greenOffset = 1;
                redOffset = 2;
            }
            else if (image.Format == PixelFormats.Rgb24)
            {
                redOffset = 0;
                greenOffset = 1;
                blueOffset = 2;
            }
            else
            {
                throw new NotSupportedException(String.Format("Unhandled image format {0}.", image.Format));
            }
        }
    }
}
