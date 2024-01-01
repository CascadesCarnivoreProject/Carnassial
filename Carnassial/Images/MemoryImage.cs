using Carnassial.Native;
using System.Runtime.Intrinsics;
using System;
using System.Runtime.Intrinsics.X86;
using System.Windows.Media.Imaging;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows;

namespace Carnassial.Images
{
    public class MemoryImage : MemoryImageCppCli
    {
        private const int DefaultDpi = 96;

        public MemoryImage(BitmapSource bitmap)
            : base(bitmap) 
        {
        }

        public MemoryImage(byte[] jpeg, Nullable<int> requestedWidth)
            : base(jpeg, requestedWidth)
        {
        }

        public MemoryImage(byte[] jpeg, int offset, int length, Nullable<int> requestedWidth)
            : base(jpeg, offset, length, requestedWidth)
        {
        }

        public MemoryImage(int width, int height, PixelFormat format)
            : base(width, height, format)
        {
        }

        private unsafe void DifferenceAvx256(MemoryImage other, byte thresholdPerChannel, MemoryImage difference)
        {
            Vector256<byte> blackOctet = Vector256.AsByte(Vector256.Create(0xff000000)); // assume BGRA; fully opaque black
            Vector256<Int16> thresholdEpi16 = Vector256.Create((Int16)(6 * thresholdPerChannel)); // two pixels * RGB = 6 * threshold
            Vector256<Int32> numeratorForAverageEpi32 = Vector256.Create(715827883, 0, 715827883, 0, 715827883, 0, 715827883, 0);
            Vector256<byte> broadcastLowPackedOctet = Vector256.Create((byte)0, 0, 0, 3, 0, 0, 0, 3, 8, 8, 8, 11, 8, 8, 8, 11, 16, 16, 16, 19, 16, 16, 16, 19, 24, 24, 24, 27, 24, 24, 24, 27); // need (byte) to disambiguate byte overload

            fixed (byte* differencePixels = &difference.Pixels[0])
            fixed (byte* otherPixels = &other.Pixels[0])
            fixed (byte* thisPixels = &this.Pixels[0])
            {
                for (int pixelOctetOffset = 0; pixelOctetOffset < this.Pixels.Length; pixelOctetOffset += sizeof(Vector256<byte>))
                {
                    Vector256<byte> thisPixelOctet = Avx.LoadVector256(thisPixels + pixelOctetOffset);
                    Vector256<byte> otherPixelOctet = Avx.LoadVector256(otherPixels + pixelOctetOffset);

                    // SumAbsoluteDifference() finds two 16 bit sums of absolute difference, one for the lower two pixels and one for the upper two
                    // These two unsigned sums are in the low 16 bits of the 64 bit halves. Interpreting them as epi16 allows the above threshold
                    // mask to propagate the alpha = 255 bytes from the black octect.
                    Vector256<Int16> sumsOfAbsoluteDifferencesEpi16 = Vector256.AsInt16(Avx2.SumAbsoluteDifferences(thisPixelOctet, otherPixelOctet));
                    Vector256<byte> aboveThresholdEpu8 = Vector256.AsByte(Avx2.CompareGreaterThan(sumsOfAbsoluteDifferencesEpi16, thresholdEpi16));

                    // no integer division is available without AVX-512VL; use integer approximation of a divide by three to find the average for the output pixel value
                    // The maximum possible 40 bits of the 64 bit intermediates are used (the maximum sum of absolute differences is 6 * 255 = 10.58 bytes)
                    // so this is equivalent to multiplying by 0.16666666674428.
                    Vector256<byte> greyscaleLowPackedOctet = Vector256.AsByte(Avx2.ShiftRightLogical(Avx2.Multiply(numeratorForAverageEpi32, Vector256.AsInt32(sumsOfAbsoluteDifferencesEpi16)), 32));
                    Vector256<byte> outputLowPackedOctet = Avx2.BlendVariable(blackOctet, greyscaleLowPackedOctet, aboveThresholdEpu8);
                    Vector256<byte> outputOctet = Avx2.Shuffle(outputLowPackedOctet, broadcastLowPackedOctet);
                    Avx.Store(differencePixels + pixelOctetOffset, outputOctet);
                }
            }
        }

        private unsafe void DifferenceAvx256(MemoryImage previous, MemoryImage next, byte thresholdPerChannel, MemoryImage difference)
        {
            Vector256<byte> blackOctet = Vector256.AsByte(Vector256.Create(0xff000000));
            Vector256<Int16> thresholdEpi16 = Vector256.Create((Int16)(6 * thresholdPerChannel));
            Vector256<Int32> numeratorForAverageEpi32 = Vector256.Create(715827883, 0, 715827883, 0, 715827883, 0, 715827883, 0);
            Vector256<byte> broadcastLowPackedOctet = Vector256.Create((byte)0, 0, 0, 3, 0, 0, 0, 3, 8, 8, 8, 11, 8, 8, 8, 11, 16, 16, 16, 19, 16, 16, 16, 19, 24, 24, 24, 27, 24, 24, 24, 27);

            fixed (byte* previousPixels = &previous.Pixels[0])
            fixed (byte* nextPixels = &next.Pixels[0])
            fixed (byte* differencePixels = &difference.Pixels[0])
            fixed (byte* pixels = &this.Pixels[0])
            {
                for (int pixelOctetOffset = 0; pixelOctetOffset < this.Pixels.Length; pixelOctetOffset += sizeof(Vector256<byte>))
                {
                    Vector256<byte> thisPixelOctet = Avx.LoadVector256(pixels + pixelOctetOffset);
                    Vector256<byte> previousPixelOctet = Avx.LoadVector256(previousPixels + pixelOctetOffset);
                    Vector256<Int16> sumsOfPreviousDifferencesEpi16 = Vector256.AsInt16(Avx2.SumAbsoluteDifferences(thisPixelOctet, previousPixelOctet));
                    Vector256<byte> previousAboveThreshold = Vector256.AsByte(Avx2.CompareGreaterThan(sumsOfPreviousDifferencesEpi16, thresholdEpi16));

                    Vector256<byte> nextPixelOctet = Avx.LoadVector256(nextPixels + pixelOctetOffset);
                    Vector256<Int16> sumsOfNextDifferenceEpi16 = Vector256.AsInt16(Avx2.SumAbsoluteDifferences(thisPixelOctet, nextPixelOctet));
                    Vector256<byte> nextAboveThreshold = Vector256.AsByte(Avx2.CompareGreaterThan(sumsOfNextDifferenceEpi16, thresholdEpi16));

                    // no SIMD integer division is available; use integer approximation of a divide by 12 to find the average for the output pixel value
                    // The maximum possible 41 bits of the 64 bit intermediates are used (the maximum sum of absolute differences is 12 * 255 = 10.58 bytes)
                    // so this is equivalent to multiplying by 0.0833333334885538.
                    Vector256<byte> outputOctet = Vector256.AsByte(Avx2.ShiftRightLogical(Vector256.AsInt64(Avx2.Multiply(numeratorForAverageEpi32, Vector256.AsInt32(Avx2.Add(sumsOfPreviousDifferencesEpi16, sumsOfNextDifferenceEpi16)))), 32));

                    Vector256<byte> aboveThreshold = Avx2.And(previousAboveThreshold, nextAboveThreshold);
                    outputOctet = Avx2.BlendVariable(blackOctet, outputOctet, aboveThreshold);
                    outputOctet = Avx2.Shuffle(outputOctet, broadcastLowPackedOctet);
                    Avx.Store(differencePixels + pixelOctetOffset, outputOctet);
                }
            }
        }

        /// <summary>
        /// Find average luminosity and coloration of image.
        /// </summary>
        // 2.2.0.3 
        // 2.2.0.2 - low resolution timer
        // 8MP average performance (n ~= 200), milliseconds
        // dark pixel skip  C++/CLI  C++  SSE4.1  SSE4.1 VEX  AVX2
        // 10               7.1      6.6  3.9     3.7
        //  8                        13   4.1     4.0         4.2
        //  4                        18   4.4     4.3         5.2
        //  2                                     5.8         7.1
        //  1                                     7.4
        // Scalar performance is primarily instruction bound while SIMD performance is L3 bound with VEX approaching 50% superqueue bound, hence the different
        // scaling characteristics.  For pixel skips less than 16 every cache line in the image has to be fetched so memory access cost is essentially skip
        // invariant and compute increases occur primarily during load latency.  However, dark and grey estimates are weak functions of sampling density so
        // there's minimal benefit to testing more pixels.  AVX2 averages slower than VEX encoded SSE 4.1, consistent with Intel's guidance for memory (rather 
        // than compute) bound operations and loss of turbo from ymm register use, so is disabled from CPU dispatch.  AVX2's mode is faster but its average is 
        // worse over any substantial number of files due to greater likelihood of slow iterations.
        public (double luminosity, double coloration) GetLuminosityAndColoration(int bottomRowsToSkip)
        {
            // require alpha channel be set to a constant value; see remarks in NativeImage::IsDarkSse41()
            if ((this.Format != MemoryImageCppCli.PreferredPixelFormat) ||
                (this.PixelSizeInBytes != MemoryImageCppCli.CalculationPixelSizeInBytes))
            {
                throw new NotSupportedException("Unhandled image format " + this.Format + " or unsuppored pixel size of " + this.PixelSizeInBytes + " bytes.");
            }
            if (Avx2.IsSupported == false)
            {
                throw new NotSupportedException("AVX2 instructions are not available.");
            }

            return this.GetLuminosityAndColorationAvx256(bottomRowsToSkip);
        }

        private unsafe (double luminosity, double coloration) GetLuminosityAndColorationAvx256(int bottomRowsToSkip)
        {
            // estimate human apparent brightness
            // In floating point this would be
            //   double humanPercievedLuminosity = 0.299 * pixelR + 0.5876 * pixelG + 0.114 * pixelB;
            // but an integer version is 26% faster as converting to floating point is avoided.  The coefficients are multiplied by 
            // 125 as this gives a maximum value of 31875 for a white pixel (255, 255, 255), avoiding overflow in a signed 16 bit 
            // integer (max positive value 32767).  If the coefficient scaling is changed the luminosity calculation below also 
            // needs also to be updated.
            //                                                        G0 R0 B0 A0 G1 R1 B1 A1 G2  R2 B2  A2  G3  R3  B3  A3  G4  R4  B4  A4  G5  R5  B5  A5  G6  R6  B6  A6  G7  R7  B7  A7
            Vector256<byte> bgraToGrbaShuffle = Vector256.Create((byte)1, 2, 0, 3, 5, 6, 4, 7, 9, 10, 8, 11, 13, 14, 12, 15, 17, 18, 16, 19, 21, 22, 20, 23, 25, 26, 24, 27, 29, 30, 28, 31);
            //                                                                B0  G0  R0 A0  B1  G1  R1 A1  B2  G2  R2 A2  B3  G3  R3  A3 B4  G4  R4 A4  B5  G5  R5 A5  B6  G6  R6 A6  B7  G7  R7 A7 
            Vector256<SByte> luminosityCoefficients = Vector256.Create((SByte)14, 74, 37, 0, 14, 74, 37, 0, 14, 74, 37, 0, 14, 74, 37, 0, 14, 74, 37, 0, 14, 74, 37, 0, 14, 74, 37, 0, 14, 74, 37, 0);

            Vector256<Int64> colorationTotalEpi64 = Vector256<Int64>.Zero;
            Vector256<Int32> luminosityRunningTotalEpi32 = Vector256<Int32>.Zero;
            int luminosityAccumulateIncrementInBytes = 32768 * sizeof(Vector256<Int32>);
            Int64 luminosityTotal = 0;
            int nextLuminosityAccumulateOffset = luminosityAccumulateIncrementInBytes; // see notes for second MultiplyAddAdjacent() below
            Vector256<Int16> oneEpi16 = Vector256<Int16>.One;
            fixed (byte* pixels = &this.Pixels[0])
            {
                for (int pixelOctetOffset = 0; pixelOctetOffset < this.Pixels.Length; pixelOctetOffset += sizeof(Vector256<byte>))
                {
                    // get next octet of pixels
                    Vector256<byte> pixelOctetBgra = Avx.LoadVector256(pixels + pixelOctetOffset);

                    // estimate human apparent brightness
                    // First argument of first MultiplyAddAdjacent() is epu8, second is epi8; pixels need to be first and coefficients must be 127
                    // or less to avoid the product becoming negative. Result is { WB0 + WG0, WR0, WB1 + WG1, WR1, ..., WB7 + WG7, WR7 } weighted
                    // luminosity components.
                    // Second MultiplyAddAdjacent() yields pixel luminosities { Luminosity0 = WB0 + WG0 + WR0, Luminosity1, ..., Luminosity7 } where
                    // each luminiosity is in the range [ 0, 31875 ] and an epi32 can thus accumulate ~2^16 pixels.
                    Vector256<Int16> humanPerceivedLuminosityEpi16 = Avx2.MultiplyAddAdjacent(pixelOctetBgra, luminosityCoefficients);
                    Vector256<Int32> humanPerceivedLuminosityEpi32 = Avx2.MultiplyAddAdjacent(humanPerceivedLuminosityEpi16, oneEpi16);
                    luminosityRunningTotalEpi32 = Avx2.Add(humanPerceivedLuminosityEpi32, luminosityRunningTotalEpi32);
                    if (pixelOctetOffset >= nextLuminosityAccumulateOffset)
                    {
                        // if luminosity vector accumulator is potentially approaching full, transfer running total to scalar accumulator
                        Vector128<Int32> luminosityRunningTotalEpi32x4 = Avx.Add(Avx.ExtractVector128(luminosityRunningTotalEpi32, Constant.Simd256x8.ExtractLower128), Avx.ExtractVector128(luminosityRunningTotalEpi32, Constant.Simd256x8.ExtractUpper128));
                        luminosityTotal += (Int64)Avx.Extract(luminosityRunningTotalEpi32x4, 0) + (Int64)Avx.Extract(luminosityRunningTotalEpi32x4, 1) + (Int64)Avx.Extract(luminosityRunningTotalEpi32x4, 2) + (Int64)Avx.Extract(luminosityRunningTotalEpi32x4, 3);

                        luminosityRunningTotalEpi32 = Vector256<Int32>.Zero;
                        nextLuminosityAccumulateOffset += luminosityAccumulateIncrementInBytes;
                    }

                    // calculate and accumulate coloration
                    // Since alphas are set to 255 at jpeg decode and aren't shuffled they contribute zero to the sum of absolute 
                    // differences. A check in GetLuminosityAndColoration() excludes RGB or BGR formats where the alpha's not a controlled value.
                    // Upper bound on each sum of absolute differences is 2 pixels * 2 * 255/pixel => 9 petapixels per 64 bit accumulator.
                    Vector256<byte> pixelOctetGrba = Avx2.Shuffle(pixelOctetBgra, bgraToGrbaShuffle);
                    Vector256<Int64> pixelOctetColoration = Vector256.AsInt64(Avx2.SumAbsoluteDifferences(pixelOctetBgra, pixelOctetGrba));
                    colorationTotalEpi64 = Avx2.Add(pixelOctetColoration, colorationTotalEpi64);
                }
            }

            // accumulate remaining luminosity to scalar
            Vector128<Int32> luminosityFinalTotalEpi32x4 = Avx.Add(Avx.ExtractVector128(luminosityRunningTotalEpi32, Constant.Simd256x8.ExtractLower128), Avx.ExtractVector128(luminosityRunningTotalEpi32, Constant.Simd256x8.ExtractUpper128));
            luminosityTotal += (Int64)Avx.Extract(luminosityFinalTotalEpi32x4, 0) + (Int64)Avx.Extract(luminosityFinalTotalEpi32x4, 1) + (Int64)Avx.Extract(luminosityFinalTotalEpi32x4, 2) + (Int64)Avx.Extract(luminosityFinalTotalEpi32x4, 3);

            // accumulate coloration to scalar
            Vector128<Int64> colorationTotalEpi64x2 = Avx.Add(Avx.ExtractVector128(colorationTotalEpi64, Constant.Simd256x8.ExtractLower128), Avx.ExtractVector128(colorationTotalEpi64, Constant.Simd256x8.ExtractUpper128));
            Int64 colorationTotal = Avx.X64.Extract(colorationTotalEpi64x2, 0) + Avx.X64.Extract(colorationTotalEpi64x2, 1);
            double coloration = (double)colorationTotal / (double)(2L * 255L * this.TotalPixels); // mean coloration fraction = total / (two differences of up to 255/pixel * max difference of 255 * pixel count)
            double luminosity = (double)luminosityTotal / (double)(125L * 255L * this.TotalPixels); // fractional perceived luminosity = total / ((blue weight + green weight + red weight) * max luminosity of 255 * pixel count)
            return (luminosity, coloration);
        }

        // internal to provide unit test access
        internal bool MismatchedOrNot32BitBgra(MemoryImage other)
        {
            if ((this.PixelWidth != other.PixelWidth) ||
                (this.PixelHeight != other.PixelHeight) ||
                (this.Format != MemoryImageCppCli.PreferredPixelFormat) ||
                (other.Format != MemoryImageCppCli.PreferredPixelFormat) ||
                (this.PixelSizeInBytes != other.PixelSizeInBytes))
            {
                return true;
            }
            return false;
        }

        // 8MP average performance (n ~= 40): 5.6ms
        // Not worth running in parallel.
        public void SetSource(Image image)
        {
            //Stopwatch^ stopwatch = gcnew Stopwatch();
            //stopwatch->Start();
            WriteableBitmap? writeableBitmap = (WriteableBitmap?)image.Source;
            if ((writeableBitmap == null) ||
                (writeableBitmap.PixelHeight != this.PixelHeight) ||
                (writeableBitmap.PixelWidth != this.PixelWidth) ||
                (writeableBitmap.Format != this.Format))
            {
                writeableBitmap = new(this.PixelWidth, this.PixelHeight, MemoryImage.DefaultDpi, MemoryImage.DefaultDpi, this.Format, null);
                image.Source = writeableBitmap;
            }

            writeableBitmap.WritePixels(new Int32Rect(0, 0, this.PixelWidth, this.PixelHeight), this.Pixels, this.StrideInBytes, 0, 0);
            //stopwatch->Stop();
            //Trace::WriteLine(stopwatch->Elapsed.ToString("s\\.fffffff", CultureInfo.CurrentCulture));
        }

        /// <summary>
        /// Get the sum of absolute differences between two images.
        /// </summary>
        // 2.2.0.3
        // 8MP average performance (n = 20+ @ 10x), release build, i5-4200U, 256k blocks, Visual Studio 2017, Windows 10 Fall Creators Update + Meltdown and Spectre
        //              standalone average ms, mains               standalone average ms, battery
        // threads      1     2     4 (default)                    1     2     4
        // C++ scalar   48.1  40.1  36.1    2.1  2.1  2.1 GHz
        // SSE4.1       20.8  22.4  26.1    2.1  2.1  2.1 GHz      31.8               1.5 GHz
        // SSE4.1 VEX   19.7  20.4  22.0    1.9  2.0  2.0 GHz      32.0               1.5
        // AVX2         26.5  21.2  22.3    1.9  1.9  2.1 GHz      36.7  33.8  34.0   1.3  1.3  1.5 GHz
        // 
        // 2.2.0.2
        // 8MP average performance (n = 30-60), release build, i5-4200U, default threads (4), 16k blocks, Visual Studio 2015, Windows 10
        //                                                  C++    SSE4.1   SSE4.1 VEX  AVX2  VS tracing, mains
        // measured milliseconds                            43.7   13.4     13.4        12.3
        // CPU GHz                                          2.3    2.2      2.2         2.2
        // GHz normalized milliseconds                      42.0   12.1     12.1        11.7
        //                              C# unsafe  C++/CLI  C++    SSE4.1   SSE4.1 VEX  AVX2  VS tracing, battery
        // measured milliseconds        58.7       46.1     28.6   ~15      ~15         ~18
        // CPU GHz                      2.45       2.45     2.0    ~1.7     ~1.5        ~1.0
        // GHz normalized milliseconds  58.7       46.1     23      10       8.8         7.2
        //
        // The more modern the SIMD used the more times vary depending on how much processor upclocks in response to the load.  At a 
        // given core frequency newer is faster but AVX2 upclocking is less aggressive than with SSE4.  The result might be less power 
        // expenditure but certainly more variable and higher average latency.  On Haswell the limiting factor is more the ability of 
        // the rendering system to deliver the difference image as this runs single threaded and imposes more time than the differencing 
        // and overall latencies remain low enough for response to be quite prompt subjectively.  Additionally, the on core speed of 
        // differencing is memory bound but the systems measured don't push much beyond 11GB/s and frequently run below 7.5GB/s.
        public bool TryDifference(MemoryImage other, byte threshold, [NotNullWhen(true)] out MemoryImage? difference)
        {
            if (this.MismatchedOrNot32BitBgra(other) || (Avx2.IsSupported == false))
            {
                difference = null;
                return false;
            }

            difference = new MemoryImage(this.PixelWidth, this.PixelHeight, this.Format);
            this.DifferenceAvx256(other, threshold, difference);
            return true;
        }

        /// <summary>
        /// Get the sum of absolute differences between three images.
        /// </summary>
        public bool TryDifference(MemoryImage previous, MemoryImage next, byte threshold, [NotNullWhen(true)] out MemoryImage? difference)
        {
            if (this.MismatchedOrNot32BitBgra(previous) ||
                this.MismatchedOrNot32BitBgra(next) ||
                (Avx2.IsSupported == false))
            {
                difference = null;
                return false;
            }

            difference = new(this.PixelWidth, this.PixelHeight, this.Format);
            this.DifferenceAvx256(previous, next, threshold, difference);
            return true;
        }
    }
}
