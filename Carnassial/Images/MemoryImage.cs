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

        private unsafe void DifferenceAvx128(MemoryImage other, byte threshold, MemoryImage difference)
        {
            Vector128<byte> blackQuad = Vector128.AsByte(Vector128.Create(0xff000000));
            Vector128<Int32> thresholdEpi32 = Vector128.Create(6 * threshold);
            Vector128<Int32> numeratorForAverageEpi32 = Vector128.Create(0, 715827883, 0, 715827883);
            Vector128<byte> bgrBroadcast = Vector128.Create((byte)15, 8, 8, 8, 11, 8, 8, 8, 7, 0, 0, 0, 3, 0, 0, 0); // need (byte) to disambiguate byte overload

            fixed (byte* differencePixels = &difference.Pixels[0])
            fixed (byte* otherPixels = &other.Pixels[0])
            fixed (byte* pixels = &this.Pixels[0])
            {
                for (int pixelQuadOffset = 0; pixelQuadOffset < this.Pixels.Length; pixelQuadOffset += sizeof(Vector128<byte>))
                {
                    Vector128<byte> thisPixelQuad = Avx.LoadVector128(pixels + pixelQuadOffset);
                    Vector128<byte> otherPixelQuad = Avx.LoadVector128(otherPixels + pixelQuadOffset);
                    Vector128<Int32> sumsOfAbsoluteDifferencesEpi32 = Vector128.AsInt32(Avx.SumAbsoluteDifferences(thisPixelQuad, otherPixelQuad));

                    // performing thresholding on epi16 allows _mm_blendv_epi8() to be used to set all four of the output pixels' alpha channels as the red
                    // and alpha channels in each pixel are set to zero and therefore below threshold; this saves a second blend instruction
                    Vector128<byte> aboveThreshold = Vector128.AsByte(Avx.CompareGreaterThan(numeratorForAverageEpi32, thresholdEpi32));

                    // no SIMD integer division is available; use integer approximation of a divide by three to find the average for the output pixel value
                    // The maximum possible 40 bits of the 64 bit intermediates are used (the maximum sum of absolute differences is 6 * 255 = 10.58 bytes)
                    // so this is equivalent to multiplying by 0.16666666674428.
                    Vector128<byte> outputQuad = Vector128.AsByte(Avx.ShiftRightLogical(Vector128.AsInt64(Avx.Multiply(numeratorForAverageEpi32, sumsOfAbsoluteDifferencesEpi32)), 32));
                    outputQuad = Avx.BlendVariable(blackQuad, outputQuad, aboveThreshold);
                    outputQuad = Avx.Shuffle(outputQuad, bgrBroadcast);
                    Avx.Store(differencePixels + pixelQuadOffset, outputQuad);
                }
            }
        }

        private unsafe void DifferenceAvx128(MemoryImage previous, MemoryImage next, byte threshold, MemoryImage difference)
        {
            Vector128<byte> blackQuad = Vector128.AsByte(Vector128.Create(0xff000000));
            Vector128<Int32> thresholdEpi32 = Vector128.Create(6 * threshold);
            Vector128<Int32> numeratorForAverageEpi32 = Vector128.Create(0, 715827883, 0, 715827883);
            Vector128<byte> bgrBroadcast = Vector128.Create((byte)15, 8, 8, 8, 11, 8, 8, 8, 7, 0, 0, 0, 3, 0, 0, 0); // need (byte) to disambiguate byte overload

            fixed (byte* previousPixels = &previous.Pixels[0])
            fixed (byte* nextPixels = &next.Pixels[0])
            fixed (byte* differencePixels = &difference.Pixels[0])
            fixed (byte* pixels = &this.Pixels[0])
            {
                for (int pixelQuadOffset = 0; pixelQuadOffset < this.Pixels.Length; pixelQuadOffset += sizeof(Vector128<byte>))
                {
                    // performing thresholding on epi16 allows _mm_blendv_epi8() to be used to set all four of the output pixels' alpha channels as the red
                    // and alpha channels in each pixel are set to zero and therefore below threshold; this saves a second blend instruction
                    Vector128<byte> thisPixelQuad = Avx.LoadVector128(pixels + pixelQuadOffset);
                    Vector128<byte> previousPixelQuad = Avx.LoadVector128(previousPixels + pixelQuadOffset);
                    Vector128<Int32> sumsOfPreviousDifferencesEpi32 = Vector128.AsInt32(Avx.SumAbsoluteDifferences(thisPixelQuad, previousPixelQuad));
                    Vector128<byte> previousAboveThreshold = Vector128.AsByte(Avx.CompareGreaterThan(sumsOfPreviousDifferencesEpi32, thresholdEpi32));

                    Vector128<byte> nextPixelQuad = Avx.LoadVector128(nextPixels + pixelQuadOffset);
                    Vector128<Int32> sumsOfNextDifferenceEpi32 = Vector128.AsInt32(Avx.SumAbsoluteDifferences(thisPixelQuad, nextPixelQuad));
                    Vector128<byte> nextAboveThreshold = Vector128.AsByte(Avx.CompareGreaterThan(sumsOfNextDifferenceEpi32, thresholdEpi32));

                    // no SIMD integer division is available; use integer approximation of a divide by 12 to find the average for the output pixel value
                    // The maximum possible 41 bits of the 64 bit intermediates are used (the maximum sum of absolute differences is 12 * 255 = 10.58 bytes)
                    // so this is equivalent to multiplying by 0.0833333334885538.
                    Vector128<byte> outputQuad = Vector128.AsByte(Avx.ShiftRightLogical(Vector128.AsInt64(Avx.Multiply(numeratorForAverageEpi32, Avx.Add(sumsOfPreviousDifferencesEpi32, sumsOfNextDifferenceEpi32))), 32));

                    Vector128<byte> aboveThreshold = Avx.And(previousAboveThreshold, nextAboveThreshold);
                    outputQuad = Avx.BlendVariable(blackQuad, outputQuad, aboveThreshold);
                    outputQuad = Avx.Shuffle(outputQuad, bgrBroadcast);
                    Avx.Store(differencePixels + pixelQuadOffset, outputQuad);
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
        public double GetLuminosityAndColoration(int bottomRowsToSkip, out double coloration)
        {
            // require alpha channel be set to a constant value; see remarks in NativeImage::IsDarkSse41()
            if ((this.Format != MemoryImageCppCli.PreferredPixelFormat) ||
                (this.PixelSizeInBytes != MemoryImageCppCli.CalculationPixelSizeInBytes))
            {
                throw new NotSupportedException("Unhandled image format " + this.Format + " or unsuppored pixel size of " + this.PixelSizeInBytes + " bytes.");
            }
            if (Avx.IsSupported == false)
            {
                throw new NotSupportedException("AVX instructions are not available.");
            }

            return this.GetLuminosityAndColorationAvx128(bottomRowsToSkip, out coloration);
        }

        private unsafe double GetLuminosityAndColorationAvx128(int bottomRowsToSkip, out double coloration)
        {
            // estimate human apparent brightness
            // In floating point this would be
            //   double humanPercievedLuminosity = 0.299 * pixelR + 0.5876 * pixelG + 0.114 * pixelB;
            // but an integer version is 26% faster as converting to floating point is avoided.  The coefficients are multiplied by 
            // 125 as this gives a maximum value of 31875 for a white pixel (255, 255, 255), avoiding overflow in a signed 16 bit 
            // integer (max positive value 32767).  If the coefficient scaling is changed the luminosity calculation below also 
            // needs also to be updated.
            //                                                        G0 R0 B0 A0 G1 R1 B1 A1 G2  R2 B2  A2  G3  R3  B3  A3 
            Vector128<byte> bgraToGrbaShuffle = Vector128.Create((byte)1, 2, 0, 3, 5, 6, 4, 7, 9, 10, 8, 11, 13, 14, 12, 15);
            //                                                                G0  R0  B0 A0  G1  R1  B1 A1  G2  R2  B2 A2  G3  R3  B3  A3 
            Vector128<SByte> luminosityCoefficients = Vector128.Create((SByte)14, 74, 37, 0, 14, 74, 37, 0, 14, 74, 37, 0, 14, 74, 37, 0);

            Vector128<Int64> colorationTotal = Vector128<Int64>.Zero;
            Vector128<Int64> luminosityTotal = Vector128<Int64>.Zero;
            fixed (byte* pixels = &this.Pixels[0])
            {
                // loop notionally uses 7 xmm registers
                //   xmm0 + luminosity coeffs + luminosity total + pixel data + shuffle + grba + coloration total
                for (int pixelQuadOffset = 0; pixelQuadOffset < this.Pixels.Length; pixelQuadOffset += sizeof(Vector128<byte>))
                {
                    // get next quad of pixels
                    Vector128<byte> pixelQuad = Avx.LoadVector128(pixels + pixelQuadOffset);

                    // estimate human apparent brightness
                    // Maximum value remains below 2^15 - 1 as in IsDarkTaskScalar() so only the low 16 bits need be retained.  Since alpha
                    // is multiplied by zero the intermediate format is [ Bscale * B0 + Gscale * G0, Rscale * R0, Bscale * B1 + Gscale * G1,
                    // Rscale * R1, ... ] and a self hadd produces [ luminosity0, luminosity1, luminosity2, luminosity3, luminosity0, 
                    // luminosity1, luminosity2, luminosity3 ].
                    // First argument of _mm_maddubs_epi16() is epu8, second is epi8; pixels need to be first and coefficients must be 127 or less to avoid the 
                    // product becoming negative.
                    Vector128<Int16> humanPerceivedLuminosityEpi16 = Avx.MultiplyAddAdjacent(pixelQuad, luminosityCoefficients);
                    humanPerceivedLuminosityEpi16 = Avx.HorizontalAddSaturate(humanPerceivedLuminosityEpi16, humanPerceivedLuminosityEpi16);
                    Vector128<Int32> humanPerceivedLuminosityEpi32 = Avx.ConvertToVector128Int32(humanPerceivedLuminosityEpi16);
                    Vector128<Int64> humanPerceivedLuminosityEpi64 = Avx.ConvertToVector128Int64(humanPerceivedLuminosityEpi32);
                    // accumulate luminosity of lower two pixels
                    luminosityTotal = Avx.Add(humanPerceivedLuminosityEpi64, luminosityTotal);
                    // accumulate luminosity of upper two pixels
                    humanPerceivedLuminosityEpi32 = Vector128.AsInt32(Avx.UnpackHigh(Vector128.AsInt64(humanPerceivedLuminosityEpi32), Vector128.AsInt64(humanPerceivedLuminosityEpi32)));
                    humanPerceivedLuminosityEpi64 = Avx.ConvertToVector128Int64(humanPerceivedLuminosityEpi32);
                    luminosityTotal = Avx.Add(humanPerceivedLuminosityEpi64, luminosityTotal);

                    // calculate and accumulate coloration
                    // Since alphas are set to 255 at jpeg decode and aren't shuffled they contribute zero to the sum of absolute 
                    // differences.  A check in IsDark() excludes RGB or BGR formats where the alpha's not a controlled 
                    // value.
                    Vector128<byte> pixelQuadGrba = Avx.Shuffle(pixelQuad, bgraToGrbaShuffle);
                    Vector128<Int64> pixelQuadColoration = Vector128.AsInt64(Avx.SumAbsoluteDifferences(pixelQuad, pixelQuadGrba));
                    colorationTotal = Avx.Add(pixelQuadColoration, colorationTotal);
                }
            }

            // _mm_cvtepi64_pd() requires AVX512VL and AVX512DQ, so convert doubles individually
            double pixelsChecked = this.TotalPixels;
            coloration = ((double)Sse41.X64.Extract(colorationTotal, 0) + (double)Sse41.X64.Extract(colorationTotal, 1)) / (2.0 * 255.0 * pixelsChecked);
            return ((double)Sse41.X64.Extract(luminosityTotal, 0) + (double)Sse41.X64.Extract(luminosityTotal, 1)) / (125.0 * 2.0 * 255.0 * pixelsChecked);
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
            if (this.MismatchedOrNot32BitBgra(other) || (Avx.IsSupported == false))
            {
                difference = null;
                return false;
            }

            difference = new MemoryImage(this.PixelWidth, this.PixelHeight, this.Format);
            this.DifferenceAvx128(other, threshold, difference);
            return true;
        }

        /// <summary>
        /// Get the sum of absolute differences between three images.
        /// </summary>
        public bool TryDifference(MemoryImage previous, MemoryImage next, byte threshold, [NotNullWhen(true)] out MemoryImage? difference)
        {
            if (this.MismatchedOrNot32BitBgra(previous) ||
                this.MismatchedOrNot32BitBgra(next) ||
                (Avx.IsSupported == false))
            {
                difference = null;
                return false;
            }

            difference = new(this.PixelWidth, this.PixelHeight, this.Format);
            this.DifferenceAvx128(previous, next, threshold, difference);
            return true;
        }
    }
}
