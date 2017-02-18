using Carnassial.Database;
using System;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Carnassial.Images
{
    /// <summary>
    /// Extends <see cref="WriteableBitmap"/> with differencing and other calculations.
    /// </summary>
    /// <remarks>This class uses WriteableBitmap.BackBuffer in place for performance.  Other Windows Presentation Foundation bitmap classes do not directly expose 
    /// pixels and require memory copies.</remarks>
    public static class WriteableBitmapExtensions
    {
        /// <summary>
        /// Get sum of absolute difference between two images as a greyscale image.
        /// </summary>
        // 8MP average performance (n ~= 30), milliseconds, release build
        // threads  scalar   _mm_sad_epu8   _mm256_sad_epu8
        // auto     55
        public static unsafe WriteableBitmap Difference(this WriteableBitmap image1, WriteableBitmap image2)
        {
            if (WriteableBitmapExtensions.BitmapsMismatchedOrNot24BitRgb(image1, image2))
            {
                return null;
            }

            // DateTime start = DateTime.UtcNow;
            int pixelSizeInBytes = image1.Format.BitsPerPixel / 8;
            byte* backBuffer1 = (byte*)image1.BackBuffer.ToPointer();
            byte* backBuffer2 = (byte*)image2.BackBuffer.ToPointer();
            WriteableBitmap difference = new WriteableBitmap(image1.PixelWidth, image1.PixelHeight, image1.DpiX, image1.DpiY, PixelFormats.Gray8, null);
            difference.Lock();
            byte* differenceBackBuffer = (byte*)difference.BackBuffer.ToPointer();
            Parallel.For(0, image1.PixelHeight, (int row) =>
            {
                int startPixel = image1.PixelWidth * row;
                int endPixel = startPixel + image1.PixelWidth;
                byte* image1Pixel = backBuffer1 + startPixel * pixelSizeInBytes;
                byte* image2Pixel = backBuffer2 + startPixel * pixelSizeInBytes;
                for (int pixel = startPixel; pixel < endPixel; ++pixel, image1Pixel += pixelSizeInBytes, image2Pixel += pixelSizeInBytes)
                {
                    // check above ensures pixels start with BRG or RGB
                    int difference0 = Math.Abs(*image1Pixel - *image2Pixel);
                    int difference1 = Math.Abs(*(image1Pixel + 1) - *(image2Pixel + 1));
                    int difference2 = Math.Abs(*(image1Pixel + 2) - *(image2Pixel + 2));
                    *(differenceBackBuffer + pixel) = (byte)((difference0 + difference1 + difference2) / 3);
                }
            });

            difference.Unlock();
            difference.Freeze();
            // Console.WriteLine((DateTime.UtcNow - start).ToString(@"s\.fff"));
            return difference;
        }

        /// <summary>
        /// Get an image of the differences in common between the unaltered and previous image and the unaltered and next image.
        /// </summary>
        /// <remarks>Implementation is color insensitive so it's simply assumed the first three values in input pixels are RGB in some order.</remarks>
        // 8MP average performance (n ~= 30), milliseconds
        //          release
        //          scalar
        // threads  greyscale
        // auto     102
        public static unsafe WriteableBitmap Difference(this WriteableBitmap unaltered, WriteableBitmap previous, WriteableBitmap next, byte threshold)
        {
            if (WriteableBitmapExtensions.BitmapsMismatchedOrNot24BitRgb(unaltered, previous) ||
                WriteableBitmapExtensions.BitmapsMismatchedOrNot24BitRgb(unaltered, next))
            {
                return null;
            }

            // DateTime start = DateTime.UtcNow;
            int pixelSizeInBytes = unaltered.Format.BitsPerPixel / 8;
            byte* unalteredBackBuffer = (byte*)unaltered.BackBuffer.ToPointer();
            byte* previousBackBuffer = (byte*)previous.BackBuffer.ToPointer();
            byte* nextBackBuffer = (byte*)next.BackBuffer.ToPointer();
            int thresholdAsInt = (int)threshold;
            WriteableBitmap difference = new WriteableBitmap(unaltered.PixelWidth, unaltered.PixelHeight, unaltered.DpiX, unaltered.DpiY, PixelFormats.Gray8, null);
            difference.Lock();
            byte* differenceBackBuffer = (byte*)difference.BackBuffer.ToPointer();
            Parallel.For(0, unaltered.PixelHeight, (int row) =>
            {
                int startPixel = unaltered.PixelWidth * row;
                int endPixel = startPixel + unaltered.PixelWidth;
                byte* unalteredPixel = unalteredBackBuffer + startPixel * pixelSizeInBytes;
                byte* previousPixel = previousBackBuffer + startPixel * pixelSizeInBytes;
                byte* nextPixel = nextBackBuffer + startPixel * pixelSizeInBytes;
                for (int pixel = startPixel; pixel < endPixel; ++pixel, unalteredPixel += pixelSizeInBytes, previousPixel += pixelSizeInBytes, nextPixel += pixelSizeInBytes)
                {
                    int previous0 = Math.Abs(*unalteredPixel - *previousPixel);
                    int previous1 = Math.Abs(*(unalteredPixel + 1) - *(previousPixel + 1));
                    int previous2 = Math.Abs(*(unalteredPixel + 2) - *(previousPixel + 2));

                    int next0 = Math.Abs(*unalteredPixel - *nextPixel);
                    int next1 = Math.Abs(*(unalteredPixel + 1) - *(nextPixel + 1));
                    int next2 = Math.Abs(*(unalteredPixel + 2) - *(nextPixel + 2));

                    int difference0 = WriteableBitmapExtensions.DifferenceIfAboveThreshold(thresholdAsInt, previous0, next0);
                    int difference1 = WriteableBitmapExtensions.DifferenceIfAboveThreshold(thresholdAsInt, previous1, next1);
                    int difference2 = WriteableBitmapExtensions.DifferenceIfAboveThreshold(thresholdAsInt, previous2, next2);
                    *(differenceBackBuffer + pixel) = (byte)((difference0 + difference1 + difference2) / 3);
                }
            });

            difference.Unlock();
            difference.Freeze();
            // Console.WriteLine((DateTime.UtcNow - start).ToString(@"s\.fff"));
            return difference;
        }

        public static FileSelection IsDark(this WriteableBitmap image, int darkPixelThreshold, double darkPixelRatio)
        {
            double ignored1;
            bool ignored2;
            return image.IsDark(darkPixelThreshold, darkPixelRatio, out ignored1, out ignored2);
        }

        /// <summary>
        /// Find the percentage of pixels whose brightness is below the threshold and classify image accordingly.
        /// </summary>
        /// <returns>Dark if the specified ratio is exceeded, Ok otherwise.</returns>
        [HandleProcessCorruptedStateExceptions]
        public static unsafe FileSelection IsDark(this WriteableBitmap image, int darkPixelThreshold, double darkPixelRatio, out double darkPixelFraction, out bool isColor)
        {
            bool bgrPixels;
            if (image.Format == PixelFormats.Bgr24 ||
                image.Format == PixelFormats.Bgr32 ||
                image.Format == PixelFormats.Bgra32 ||
                image.Format == PixelFormats.Pbgra32)
            {
                bgrPixels = true;
            }
            else if (image.Format == PixelFormats.Rgb24)
            {
                bgrPixels = false;
            }
            else
            {
                throw new NotSupportedException(String.Format("Unhandled image format {0}.", image.Format));
            }

            // examine a fraction of the pixels to reduce computation
            byte* currentPixel = (byte*)image.BackBuffer.ToPointer();
            int pixelSizeInBytes = image.Format.BitsPerPixel / 8;
            int pixelStride = Constant.Images.DarkPixelSampleStrideDefault;
            int totalPixels = image.PixelHeight * image.PixelWidth;
            int countedPixels = 0;
            int darkPixels = 0;
            int uncoloredPixels = 0;
            try
            {
                for (int pixel = 0; pixel < totalPixels; pixel += pixelStride, currentPixel += pixelSizeInBytes * pixelStride)
                {
                    // get pixel
                    byte pixel0 = *currentPixel;
                    byte pixel1 = *(currentPixel + 1);
                    byte pixel2 = *(currentPixel + 2);

                    // The numbers below convert a particular color to its greyscale equivalent, and then checked against the darkPixelThreshold
                    // Colors are not weighted equally. Since pure green is lighter than pure red and pure blue, it has a higher weight. 
                    // Pure blue is the darkest of the three, so it receives the least weight.
                    int humanPercievedLuminosity;
                    if (bgrPixels)
                    {
                        ////                                     red               green            blue
                        humanPercievedLuminosity = (int)(0.299 * pixel2 + 0.5876 * pixel1 + 0.114 * pixel0 + 0.5);
                    }
                    else
                    {
                        ////                                     red               green            blue
                        humanPercievedLuminosity = (int)(0.299 * pixel0 + 0.5876 * pixel1 + 0.114 * pixel2 + 0.5);
                    }
                    if (humanPercievedLuminosity <= darkPixelThreshold)
                    {
                        ++darkPixels;
                    }

                    // Check if the pixel is a grey scale vs. color pixel, using a heuristic. 
                    // In greyscale r = g = b but some cameras have a bit of color in night time images, so allow a tolerance.
                    int totalColoration = Math.Abs(pixel2 - pixel1) + Math.Abs(pixel1 - pixel0) + Math.Abs(pixel0 - pixel2);
                    if (totalColoration <= Constant.Images.GreyScalePixelThreshold)
                    {
                        ++uncoloredPixels;
                    }

                    // update other loop variables
                    ++countedPixels;
                }

                // images with sufficient color are never considered to be dark
                double uncoloredPixelFraction = 1.0 * uncoloredPixels / countedPixels;
                if (uncoloredPixelFraction < Constant.Images.GreyScaleImageThreshold)
                {
                    darkPixelFraction = 1.0 - uncoloredPixelFraction;
                    isColor = true;
                    return FileSelection.Ok;
                }

                // if a sufficient fraction of pixels in grey scale (uncolored) images are higher than the threshold they're dark
                darkPixelFraction = 1.0 * darkPixels / countedPixels;
                isColor = false;
                if (darkPixelFraction >= darkPixelRatio)
                {
                    return FileSelection.Dark;
                }
                return FileSelection.Ok;
            }
            catch (AccessViolationException av)
            {
                throw new SystemException(String.Format("Fatal fault in dark calcuation at pixel 0x{0:x8} from 0x{1:x8} on {2}x{3}@{4} ({5} counted, {6} dark, {7} uncolored).", (long)currentPixel, (long)image.BackBuffer.ToPointer(), image.PixelHeight, image.PixelWidth, image.Format.BitsPerPixel, countedPixels, darkPixels, uncoloredPixels), av);
            }
        }

        // checks whether the image is completely black
        public static unsafe bool IsBlack(this WriteableBitmap image)
        {
            // examine a fraction of the pixels to reduce computation
            // Check pixels from last to first as most cameras put a non-black status bar or at least non-black text at the bottom of the frame,
            // so reverse order may be faster in cases of night time images with black skies.  Depending on pixel format and alignment this may
            // also be acceleratable by checking ints or longs.
            int pixelSizeInBytes = image.Format.BitsPerPixel / 8;
            int pixelStride = Constant.Images.DarkPixelSampleStrideDefault;
            int totalBytes = image.PixelHeight * image.PixelWidth;
            byte* currentPixel = (byte*)image.BackBuffer.ToPointer() + pixelSizeInBytes * (totalBytes - 1);
            for (int pixel = totalBytes - 1; pixel > 0; pixel -= pixelStride, currentPixel -= pixelSizeInBytes * pixelStride)
            {
                if (*currentPixel != 0 || *(currentPixel + 1) != 0 || *(currentPixel + 2) != 0)
                {
                    return false;
                }
            }
            return true;
        }

        internal static bool BitmapsMismatchedOrNot24BitRgb(WriteableBitmap image1, WriteableBitmap image2)
        {
            if ((image1.PixelWidth != image2.PixelWidth) || (image1.PixelHeight != image2.PixelHeight) || (image1.Format != image2.Format))
            {
                return true;
            }
            if ((image1.Format == PixelFormats.Bgr24) ||
                (image1.Format == PixelFormats.Bgr32) ||
                (image1.Format == PixelFormats.Bgra32) ||
                (image1.Format == PixelFormats.Pbgra32) ||
                (image1.Format == PixelFormats.Rgb24))
            {
                return false;
            }
            return true;
        }

        private static int DifferenceIfAboveThreshold(int threshold, int previousDifference, int nextDifference)
        {
            return (previousDifference > threshold) && (nextDifference > threshold) ? (previousDifference + nextDifference) / 2 : 0;
        }
    }
}
