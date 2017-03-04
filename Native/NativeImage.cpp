#include "Stdafx.h"
#include <algorithm>
#include <complex>
#include <immintrin.h>
#include <new>
#include <ppl.h>
#include <stdexcept>
#include "InstructionSet.h"
#include "NativeImage.h"

namespace Carnassial
{
    namespace Native
    {
        NativeImage::NativeImage(__int32 width, __int32 height, TJPF format, __int32 pixelSizeInBytes)
        {
            this->format = format;
            this->pixelHeight = height;
            this->pixelSizeInBytes = pixelSizeInBytes;
            this->pixelWidth = width;
            this->AllocatePixels();
        }

        NativeImage::NativeImage(unsigned __int8* jpeg, __int32 jpegLength, __int32 requestedWidth)
        {
            tjhandle decompressor = tjInitDecompress();
            __int32 colorspace, height, subsampling, width;
            // see http://www.libjpeg-turbo.org/About/TurboJPEG for TurboJpeg API documentation
            __int32 result = tjDecompressHeader3(decompressor, jpeg, jpegLength, &width, &height, &subsampling, &colorspace);
            if (result != 0)
            {
                throw std::invalid_argument(tjGetErrorStr());
            }

            if (requestedWidth != -1)
            {
                // if a width was specified, downsize the decode to the smallest available size which is still larger than the requested width
                // if no width was specified, default to full size decode
                // If needed, supported downsizing ratios can be checked with
                // __int32 scalingFactorLength;
                // tjscalingfactor* scalingFactors = tjGetScalingFactors(&scalingFactorLength);
                // As of libjpeg-turbo 1.5.1 the only available downsizing not supported in this function was 3/8.
                __int32 downsizeRatio = width / requestedWidth;
                if (downsizeRatio >= 8)
                {
                    height /= 8;
                    width /= 8;
                }
                else if (downsizeRatio >= 4)
                {
                    height /= 4;
                    width /= 4;
                }
                else if (downsizeRatio >= 3)
                {
                    height *= 3;
                    width *= 3;
                    height /= 8;
                    width /= 8;
                }
                else if (downsizeRatio >= 2)
                {
                    height /= 2;
                    width /= 2;
                }
            }

            this->format = NativeImage::PreferredPixelFormat;
            this->pixelHeight = height;
            this->pixelSizeInBytes = tjPixelSize[NativeImage::PreferredPixelFormat];
            this->pixelWidth = width;
            this->AllocatePixels();

            result = tjDecompress2(decompressor, jpeg, jpegLength, this->pixels, width, this->StrideInBytes(), height, NativeImage::PreferredPixelFormat, 0);
            if (result != 0)
            {
                throw std::invalid_argument(tjGetErrorStr());
            }

            tjDestroy(decompressor);
        }

        NativeImage::~NativeImage()
        {
            _aligned_free(this->pixels);
        }

        // Visual C++ 2015.3 doesn't inline std::abs() but does inline this workaround
        // This results in emission of a single instruction rather than a function call.  Makes DifferenceScalar(), for example, 17% faster.
        __int32 NativeImage::Abs(__int32 value)
        {
            if (value < 0)
            {
                return -value;
            }
            return value;
        }

        void NativeImage::AllocatePixels()
        {
            // round the size of the pixel array up to the next 32 byte multiple for loop simplicity
            __int32 bytesToAllocate = sizeof(__m256i) * this->TotalPixelBytes() / sizeof(__m256i);
            if ((this->TotalPixelBytes() % sizeof(__m256i)) != 0)
            {
                bytesToAllocate += sizeof(__m256i);
            }
            this->pixels = (unsigned __int8 *)_aligned_malloc(bytesToAllocate, alignof(__m256i));

            // zero any extra bytes at the end of the array so calculations (Difference(), IsDark(), etc.) work against known values
			if ((this->format == TJPF::TJPF_BGRA) || (this->format == TJPF::TJPF_RGBA))
			{
				__int32 pixelsAllocated = bytesToAllocate / this->PixelSizeInBytes();
				unsigned __int32* pixels = reinterpret_cast<unsigned __int32*>(this->pixels);
				for (__int32 pixel = this->TotalPixels(); pixel < pixelsAllocated; ++pixel)
				{
					pixels[pixel] = 0xff000000;
				}
			}
			else
			{
				for (__int32 byte = this->TotalPixelBytes(); byte < bytesToAllocate; ++byte)
				{
					this->pixels[byte] = 0;
				}
			}
        }

		void NativeImage::CombinedDifferenceScalar(__int32 block, const NativeImage* previous, const NativeImage* next, unsigned __int8 threshold, NativeImage* difference)
        {
            const __int32 startByte = NativeImage::CombinedDifferenceTaskBlockInBytes * block;
            const unsigned __int8* nextPixel = next->pixels + startByte;
            const unsigned __int8* previousPixel = previous->pixels + startByte;
			const __int16 thresholdAsInt16 = 3 * (__int16)threshold;
			const unsigned __int8* thisPixel = this->pixels + startByte;
			unsigned __int8* differencePixel = difference->pixels + startByte;
			for (const unsigned __int8* endPixel = std::min(reinterpret_cast<const unsigned __int8*>(this->pixels) + this->TotalPixelBytes(), thisPixel + NativeImage::CombinedDifferenceTaskBlockInBytes);
                 thisPixel < endPixel;
                 differencePixel += NativeImage::CalculationPixelSizeInBytes, nextPixel += NativeImage::CalculationPixelSizeInBytes, previousPixel += NativeImage::CalculationPixelSizeInBytes, thisPixel += NativeImage::CalculationPixelSizeInBytes)
            {
                // check by MemoryImage::Difference() ensures pixels start with a color tuple, alpha channel is ignored
                __int16 previousB = NativeImage::Abs((__int16)*thisPixel - (__int16)*previousPixel);
                __int16 previousG = NativeImage::Abs((__int16)*(thisPixel + 1) - (__int16)*(previousPixel + 1));
                __int16 previousR = NativeImage::Abs((__int16)*(thisPixel + 2) - (__int16)*(previousPixel + 2));
				__int16 sumOfPreviousDifferences = previousB + previousG + previousR;
				bool previousAboveThreshold = sumOfPreviousDifferences > thresholdAsInt16;

                __int16 nextB = NativeImage::Abs((__int16)*thisPixel - (__int16)*nextPixel);
                __int16 nextG = NativeImage::Abs((__int16)*(thisPixel + 1) - (__int16)*(nextPixel + 1));
                __int16 nextR = NativeImage::Abs((__int16)*(thisPixel + 2) - (__int16)*(nextPixel + 2));
				__int16 sumOfNextDifferences = nextB + nextG + nextR;
                bool nextAboveThreshold = sumOfNextDifferences > thresholdAsInt16;

				__int16 sumOfAbsoluteDifferences = sumOfPreviousDifferences + sumOfNextDifferences;
                unsigned __int8 outputLevel = (previousAboveThreshold && nextAboveThreshold) ? (unsigned __int8)(sumOfAbsoluteDifferences / 6) : (unsigned __int8)0;
                *differencePixel = outputLevel;
                *(differencePixel + 1) = outputLevel;
                *(differencePixel + 2) = outputLevel;
                *(differencePixel + 3) = 0xff;
            }
        }

		// very similar to DifferenceSse41()
		// Difference is accumulating SADs from two images requires a divide by 12 rather than 6.
		void NativeImage::CombinedDifferenceSse41(__int32 block, const NativeImage* previous, const NativeImage* next, unsigned __int8 threshold, NativeImage* difference)
		{
			const __m128i blackQuad = _mm_set_epi8((__int8)0xff, 0, 0, 0, (__int8)0xff, 0, 0, 0, (__int8)0xff, 0, 0, 0, (__int8)0xff, 0, 0, 0);
			const __m128i threshold_epi16 = _mm_set1_epi16(6 * threshold);
			const __m128i numeratorForAverage = _mm_set_epi32(0, 357913941, 0, 357913941);
			const __m128i bgrBroadcast = _mm_set_epi8(15, 8, 8, 8, 11, 8, 8, 8, 7, 0, 0, 0, 3, 0, 0, 0);

			const __int32 startPixelQuadIndex = NativeImage::CombinedDifferenceTaskBlockInBytes * block / (__int32)sizeof(__m128i);
			const __int32 endPixelQuadIndex = std::min(this->TotalPixelBytes() / (__int32)sizeof(__m128i), startPixelQuadIndex + NativeImage::CombinedDifferenceTaskBlockInBytes / (__int32)sizeof(__m128i));

			__m128i* differencePixels = reinterpret_cast<__m128i*>(difference->pixels);
			const __m128i* previousPixels = reinterpret_cast<__m128i*>(previous->pixels);
			const __m128i* nextPixels = reinterpret_cast<__m128i*>(next->pixels);
			const __m128i* pixels = reinterpret_cast<__m128i*>(this->pixels);
			for (__int32 pixelQuadIndex = startPixelQuadIndex; pixelQuadIndex < endPixelQuadIndex; ++pixelQuadIndex)
			{
				// performing thresholding on epi16 allows _mm_blendv_epi8() to be used to set all four of the output pixels' alpha channels as the red
				// and alpha channels in each pixel are set to zero and therefore below threshold; this saves a second blend instruction
				__m128i thisPixelQuad = _mm_load_si128(pixels + pixelQuadIndex);
				__m128i previousPixelQuad = _mm_load_si128(previousPixels + pixelQuadIndex);
				__m128i sumsOfPreviousDifferences = _mm_sad_epu8(thisPixelQuad, previousPixelQuad);
				__m128i previousAboveThreshold = _mm_cmpgt_epi16(sumsOfPreviousDifferences, threshold_epi16);

				__m128i nextPixelQuad = _mm_load_si128(nextPixels + pixelQuadIndex);
				__m128i sumsOfNextDifferences = _mm_sad_epu8(thisPixelQuad, nextPixelQuad);
				__m128i nextAboveThreshold = _mm_cmpgt_epi16(sumsOfNextDifferences, threshold_epi16);

				// no SIMD integer division is available; use integer approximation of a divide by 12 to find the average for the output pixel value
				// The maximum possible 41 bits of the 64 bit intermediates are used (the maximum sum of absolute differences is 12 * 255 = 10.58 bytes)
				// so this is equivalent to multiplying by 0.0833333334885538.
				__m128i outputQuad = _mm_srli_epi64(_mm_mul_epu32(numeratorForAverage, _mm_add_epi64(sumsOfPreviousDifferences, sumsOfNextDifferences)), 32);

				__m128i aboveThreshold = _mm_and_si128(previousAboveThreshold, nextAboveThreshold);
				outputQuad = _mm_blendv_epi8(blackQuad, outputQuad, aboveThreshold);
				outputQuad = _mm_shuffle_epi8(outputQuad, bgrBroadcast);
				_mm_store_si128(differencePixels + pixelQuadIndex, outputQuad);
			}
		}

		void NativeImage::CopyPixelsFrom(unsigned __int8* source)
        {
            std::memcpy(this->pixels, source, this->TotalPixelBytes());
        }

        void NativeImage::CopyPixelsTo(unsigned __int8* destination)
        {
            std::memcpy(destination, this->pixels, this->TotalPixelBytes());
        }

		// 8MP average performance (n = 30-60), release build, quad core, auto threads
		//                              C++    SSE4.1   SSE4.1 VEX  AVX2
		// measured milliseconds        43.7   13.4     13.4        12.3
		// CPU GHz                      2.3    2.2      2.2         2.2
		// GHz normalized milliseconds  42.0   12.1     12.1        11.7
		void NativeImage::Difference(const NativeImage* previous, const NativeImage* next, unsigned __int8 threshold, NativeImage* difference)
        {
            __int32 differenceBlocks = this->GetNumberOfCalculationBlocks(NativeImage::CombinedDifferenceTaskBlockInBytes);
            concurrency::parallel_for<__int32>(0, differenceBlocks, 1, [this, previous, next, threshold, difference](int block)
            {
				if (InstructionSet::Avx2())
				{
					this->CombinedDifferenceAvx2(block, previous, next, threshold, difference);
				}
				else if (InstructionSet::Avx())
				{
					this->CombinedDifferenceSse41Vex(block, previous, next, threshold, difference);
				}
				else if (InstructionSet::Sse41())
				{
					this->CombinedDifferenceSse41(block, previous, next, threshold, difference);
				}
				else
				{
					this->CombinedDifferenceScalar(block, previous, next, threshold, difference);
				}
            });
        }

		// 8MP average performance (n = 30-60), release build, quad core, auto threads
		//                              C# unsafe  C++/CLI  C++    SSE4.1   SSE4.1 VEX  AVX2
		// measured milliseconds        58.7       46.1     28.6   ~15      ~15         ~18
		// CPU GHz                      2.45       2.45     2.0    ~1.7     ~1.5        ~1.0
		// GHz normalized milliseconds  58.7       46.1     23      10       8.8         7.2
		// The more modern the SIMD used the more times vary depending on how much processor upclocks in response to the load.  At a given core
		// frequency newer is faster but AVX2 upclocking is less aggressive than with SSE4.  The result is less power expenditure but more variable 
		// and higher average latency.  On Haswell the limiting factor is more the ability of the rendering system to deliver the difference image
		// as this runs single threaded and imposes more time than the differencing and overall latencies remain low enough for response to be
		// quite prompt subjectively.  Additionally, the on core speed of differencing is memory bound but the systems measured don't push much beyond 
		// 11GB/s and frequently run below 7.5GB/s.
		void NativeImage::Difference(const NativeImage* other, unsigned __int8 threshold, NativeImage *difference)
		{
			// performance varies with image stride interactions with the cache but, in general, the cache locality advantage of small task blocks 
			// substantially exceeds task dispatch overhead, meaning best performance occurs at single row granularity
			__int32 differenceBlocks = this->GetNumberOfCalculationBlocks(NativeImage::DifferenceTaskBlockInBytes);
			concurrency::parallel_for<__int32>(0, differenceBlocks, 1, [this, other, threshold, difference](int block)
			{
				// check by MemoryImage::Difference() ensures pixels start with a color tuple, alpha channel cancels since it's consistently set to 0xff
				if (InstructionSet::Avx2())
				{
					this->DifferenceAvx2(block, other, threshold, difference);
				}
				else if (InstructionSet::Avx())
				{
					this->DifferenceSse41Vex(block, other, threshold, difference);
				}
				else if (InstructionSet::Sse41())
				{
					this->DifferenceSse41(block, other, threshold, difference);
				}
				else
				{
					this->DifferenceScalar(block, other, threshold, difference);
				}
			});
		}

		void NativeImage::DifferenceScalar(__int32 block, const NativeImage* other, unsigned __int8 threshold, NativeImage* difference)
        {
            const __int32 startByte = NativeImage::DifferenceTaskBlockInBytes * block;
			const __int16 thresholdAsInt16 = 3 * (__int16)threshold;
			
			unsigned __int8* differencePixel = difference->pixels + startByte;
            const unsigned __int8* otherPixel = other->pixels + startByte;
            const unsigned __int8* thisPixel = this->pixels + startByte;
            for (const unsigned __int8* endPixel = std::min(reinterpret_cast<const unsigned _int8*>(this->pixels) + this->TotalPixelBytes(), thisPixel + NativeImage::DifferenceTaskBlockInBytes);
                 thisPixel < endPixel;
                 differencePixel += NativeImage::CalculationPixelSizeInBytes, otherPixel += NativeImage::CalculationPixelSizeInBytes, thisPixel += NativeImage::CalculationPixelSizeInBytes)
            {
                __int16 absoluteDifferenceB = NativeImage::Abs((__int16)*thisPixel - (__int16)*otherPixel);
                __int16 absoluteDifferenceG = NativeImage::Abs((__int16)*(thisPixel + 1) - (__int16)*(otherPixel + 1));
                __int16 absoluteDifferenceR = NativeImage::Abs((__int16)*(thisPixel + 2) - (__int16)*(otherPixel + 2));
                __int16 sumOfAbsoluteDifferences = absoluteDifferenceB + absoluteDifferenceG + absoluteDifferenceR;
                if (sumOfAbsoluteDifferences > thresholdAsInt16)
                {
					// the four moves below are about 10% faster than
					//   *reinterpret_cast<unsigned __int32*>(differencePixel) = 0xff000000 | (outputLevel << 16) | (outputLevel << 8) | outputLevel;
					unsigned __int32 outputLevel = sumOfAbsoluteDifferences / 3;
					*(differencePixel) = outputLevel;
					*(differencePixel + 1) = outputLevel;
					*(differencePixel + 2) = outputLevel;
					*(differencePixel + 3) = 0xff;
                }
                else
                {
                    *reinterpret_cast<unsigned __int32*>(differencePixel) = 0xff000000;
                }
            }
        }

        void NativeImage::DifferenceSse41(__int32 block, const NativeImage* other, unsigned __int8 threshold, NativeImage* difference)
        {
            const __m128i blackQuad = _mm_set_epi8((__int8)0xff, 0, 0, 0, (__int8)0xff, 0, 0, 0, (__int8)0xff, 0, 0, 0, (__int8)0xff, 0, 0, 0);
            const __m128i threshold_epi16 = _mm_set1_epi16(6 * threshold);
			const __m128i numeratorForAverage = _mm_set_epi32(0, 715827883, 0, 715827883);
			const __m128i bgrBroadcast = _mm_set_epi8(15, 8, 8, 8, 11, 8, 8, 8, 7, 0, 0, 0, 3, 0, 0, 0);
	
			const __int32 startPixelQuadIndex = NativeImage::DifferenceTaskBlockInBytes * block / (__int32)sizeof(__m128i);
            const __int32 endPixelQuadIndex = std::min(this->TotalPixelBytes() / (__int32)sizeof(__m128i), startPixelQuadIndex + NativeImage::DifferenceTaskBlockInBytes / (__int32)sizeof(__m128i));

            __m128i* differencePixels = reinterpret_cast<__m128i*>(difference->pixels);
            const __m128i* otherPixels = reinterpret_cast<__m128i*>(other->pixels);
            const __m128i* pixels = reinterpret_cast<__m128i*>(this->pixels);
            for (__int32 pixelQuadIndex = startPixelQuadIndex; pixelQuadIndex < endPixelQuadIndex; ++pixelQuadIndex)
            {
                __m128i thisPixelQuad = _mm_load_si128(pixels + pixelQuadIndex);
                __m128i otherPixelQuad = _mm_load_si128(otherPixels + pixelQuadIndex);
                __m128i sumsOfAbsoluteDifferences = _mm_sad_epu8(thisPixelQuad, otherPixelQuad);

				// performing thresholding on epi16 allows _mm_blendv_epi8() to be used to set all four of the output pixels' alpha channels as the red
				// and alpha channels in each pixel are set to zero and therefore below threshold; this saves a second blend instruction
				__m128i aboveThreshold = _mm_cmpgt_epi16(sumsOfAbsoluteDifferences, threshold_epi16);

				// no SIMD integer division is available; use integer approximation of a divide by six to find the average for the output pixel value
				// The maximum possible 40 bits of the 64 bit intermediates are used (the maximum sum of absolute differences is 6 * 255 = 10.58 bytes)
				// so this is equivalent to multiplying by 0.16666666674428.
				__m128i outputQuad = _mm_srli_epi64(_mm_mul_epu32(numeratorForAverage, sumsOfAbsoluteDifferences), 32);
						outputQuad = _mm_blendv_epi8(blackQuad, outputQuad, aboveThreshold);
						outputQuad = _mm_shuffle_epi8(outputQuad, bgrBroadcast);
				_mm_store_si128(differencePixels + pixelQuadIndex, outputQuad);
			}
        }

        __int32 NativeImage::GetNumberOfCalculationBlocks(__int32 blockSizeInBytes)
        {
            __int32 blocks = this->TotalPixelBytes() / blockSizeInBytes;
            if (this->TotalPixelBytes() % blockSizeInBytes != 0)
            {
                ++blocks;
            }
            return blocks;
        }

        bool NativeImage::IsBlack()
        {
			// Check pixels from last to first as most cameras put a non-black status bar or at least non-black text at the bottom of the frame,
			// so reverse order may be faster in cases of night time images with black skies.
			// If the number of pixels in the image isn't a multiple of the algorithm width the last few pixels are skipped.  This is acceptable.
			// Alpha channels are ignored; MemoryImage::PreferredTurboJpegPixelFormat sets them to 255 for images coming from libjpeg-turbo but their value
			// is zero for when video first frames render black.
			if (InstructionSet::Avx2())
			{
				return this->IsBlackAvx2();
			}
			else if (InstructionSet::Avx())
			{
				return this->IsBlackSse41Vex();
			}
			else if (InstructionSet::Sse41())
			{
				return this->IsBlackSse41();
			}
			return this->IsBlackScalar();
        }

		bool NativeImage::IsBlackScalar()
		{
			const unsigned __int64* endPixel = reinterpret_cast<unsigned __int64*>(this->pixels);
			for (const unsigned __int64* thisPixel = endPixel + (__int32)(0.5 * this->TotalPixels()) - 1; thisPixel >= endPixel; --thisPixel)
			{
				unsigned __int64 bgr0bgr1 = *thisPixel & 0x00ffffff00ffffff;
				if (bgr0bgr1 != 0)
				{
					return false;
				}
			}

			return true;
		}

		bool NativeImage::IsBlackSse41()
		{
			__int32 totalPixelQuads = this->TotalPixelBytes() / sizeof(__m128i);
			const __m128i* pixelQuads = reinterpret_cast<const __m128i*>(this->pixels);
			const __m128i alphaMask = _mm_set1_epi32(0x00ffffff);
			for (__int32 pixelQuadIndex = totalPixelQuads - 1; pixelQuadIndex >= 0; --pixelQuadIndex)
			{
				__m128i pixelQuad = _mm_load_si128(pixelQuads + pixelQuadIndex);
				if (_mm_test_all_zeros(alphaMask, pixelQuad) == 0)
				{
					return false;
				}
			}

			return true;
		}

		bool NativeImage::IsDark(unsigned __int8 darkPixelThreshold, double darkPixelRatioThreshold, double* darkPixelFraction, bool* isColor)
        {
            // estimate the image's properties by examining a portion of the pixels
            // Since pixels in images are broadly similar to each other skipping reduces computation and speeds adding files to image sets while introducing
            // typically substantially less than 1% error in the fractions.
            //Stopwatch^ stopwatch = gcnew Stopwatch();
            //stopwatch->Start();
            __int32 darkPixels, greyscalePixels, totalPixelsChecked;
            if (InstructionSet::Avx2())
            {
                darkPixels = this->IsDarkAvx2(darkPixelThreshold, &greyscalePixels, &totalPixelsChecked);
            }
            else if (InstructionSet::Avx())
            {
                darkPixels = this->IsDarkSse41Vex(darkPixelThreshold, &greyscalePixels, &totalPixelsChecked);
            }
            else if (InstructionSet::Sse41())
            {
                darkPixels = this->IsDarkSse41(darkPixelThreshold, &greyscalePixels, &totalPixelsChecked);
            }
            else
            {
                darkPixels = this->IsDarkScalar(darkPixelThreshold, &greyscalePixels, &totalPixelsChecked);
            }
            //stopwatch->Stop();
            //Debug::WriteLine(stopwatch->Elapsed.ToString("s\\.fffffff"));

            // color images are never considered to be dark
            double greyscalePixelFraction = (double)greyscalePixels / totalPixelsChecked;
            // Debug::WriteLine("dark: {0}, grey: {1}, total: {2} -> {3}% grey", darkPixels, greyscalePixels, totalPixelsChecked, 100 * greyscalePixelFraction);
            if (greyscalePixelFraction < NativeImage::GreyScaleImageThreshold)
            {
                *(darkPixelFraction) = 1.0 - greyscalePixelFraction;
                *(isColor) = true;
                return false;
            }

            // if the fraction of pixels in grey scale (uncolored) images is higher than the threshold the image is dark
            *(darkPixelFraction) = (double)darkPixels / totalPixelsChecked;
            *(isColor) = false;
            return *(darkPixelFraction) >= darkPixelRatioThreshold;
        }

        __int32 NativeImage::IsDarkScalar(unsigned __int8 darkPixelThreshold, __int32* greyscalePixels, __int32* totalPixelsChecked)
        {
            __int32 darkPixels = 0;
            *(greyscalePixels) = 0;
            const unsigned __int8* thisPixel = this->pixels;
            const unsigned __int8* endPixel = thisPixel + this->TotalPixelBytes();
            const __int32 scaledDarkPixelThreshold = 125 * darkPixelThreshold;
            for (; thisPixel < endPixel; thisPixel += NativeImage::CalculationPixelSizeInBytes * NativeImage::DarkPixelSkipScalar)
            {
                // get bytes in pixel
                __int16 pixelB = *thisPixel;
                __int16 pixelG = *(thisPixel + 1);
                __int16 pixelR = *(thisPixel + 2);

                // estimate human apparent brightness and flag as dark if below threshold
                // In floating point this would be
                //   double humanPercievedLuminosity = 0.299 * pixelR + 0.5876 * pixelG + 0.114 * pixelB;
                // but an integer version is 26% faster as converting to floating point is avoided.  The coefficients are multiplied by 125 as this gives a
                // maximum value of 31875 for a white pixel (255, 255, 255), avoiding overflow in a signed 16 bit integer (max positive value 32767).  If the
                // coefficient scaling is changed the scaled threshold needs also to be updated.
                __int16 humanPercievedLuminosity = 37 * pixelR + 74 * pixelG + 14 * pixelB;
                if (humanPercievedLuminosity <= scaledDarkPixelThreshold)
                {
                    ++darkPixels;
                }

                // Check if the pixel is a grey scale vs. color pixel, using a heuristic. 
                // In greyscale r = g = b but some cameras have a bit of color cast in infrared images, so allow a tolerance.
                __int16 totalColoration = NativeImage::Abs(pixelR - pixelG) + NativeImage::Abs(pixelG - pixelB) + NativeImage::Abs(pixelB - pixelR);
                if (totalColoration <= NativeImage::GreyscalePixelThreshold)
                {
                    ++*(greyscalePixels);
                }
            }

            *(totalPixelsChecked) = this->TotalPixels() / NativeImage::DarkPixelSkipScalar;
            return darkPixels;
        }

        #pragma warning(disable:4793)
        // This method is functionally identical to IsDarkScalar() for dark pixel counts up to maybe the last 1-3 pixels tested.  It is not identical for 
        // greyscale counts as, instead of testing pixels individually, they're tested in pairs.  This may cause a marginal image to cross the color 
        // threshold relative to IsDarkScalar() as, on test images, testing pairs reduces greyscale counts typically by 0.1-1.0%.  It's debatable whether 
        // noise rejection from such averaging is a feature or bug but it's unlikely to have much effect in practice.  The implementations can be matched 
        // if it proves desirable.
        __int32 NativeImage::IsDarkSse41(unsigned __int8 darkPixelThreshold, __int32* greyscalePixels, __int32* totalPixelsChecked)
        {
            // same scaling approach as in IsDarkTaskScalar()
            __m128i darkPixels = _mm_setzero_si128();
            __m128i greySumsOfAbsoluteDifferences = _mm_setzero_si128();
            //                                             A3  B3  R3  G3  A2 B2  R2 G2 A1 B1 R1 G1 A0 B0 R0 G0
            const __m128i bgraToGrbaShuffle = _mm_set_epi8(15, 12, 14, 13, 11, 8, 10, 9, 7, 4, 6, 5, 3, 0, 2, 1);
            // there is no _mm_cmple_epi32() so 1 needs to be added to both the grey and dark pixel thresholds for parity with IsDarkScalar()
            const __m128i greyscalePixelThreshold = _mm_set1_epi64x(2 * NativeImage::GreyscalePixelThreshold + 1);
            //                                                  A3 R3  G3  B3  A2 R2  G2  B2  A1 R1  G1  B1  A0 R0  G0  B0
            const __m128i luminosityCoefficients = _mm_set_epi8(0, 37, 74, 14, 0, 37, 74, 14, 0, 37, 74, 14, 0, 37, 74, 14);
            const __int32 maxPixel = this->TotalPixels() - 4 * NativeImage::DarkPixelSkipSimd;
            const __m128i scaledDarkPixelThreshold = _mm_set1_epi32(125 * darkPixelThreshold + 1);
            const __int32* pixels = reinterpret_cast<__int32*>(this->pixels);
            // Loop notionally uses 10 xmm registers
            //   xmm0 + dark count + luminosity coeffs + dark threshold + pixel data + luminosity/is dark/coloration/is grey
            //   shuffle + grba + grey threshold + grey counts
            // but VC 2015.3 is able to fit it eight so spilling does not occur.
            for (__int32 pixel = 0; pixel < maxPixel; pixel += 4 * NativeImage::DarkPixelSkipSimd)
            {
                // get next quad of pixels
                __m128i pixelQuad = _mm_set_epi32(*(pixels + pixel), *(pixels + pixel + NativeImage::DarkPixelSkipSimd), *(pixels + pixel + 2 * NativeImage::DarkPixelSkipSimd), *(pixels + pixel + 3 * NativeImage::DarkPixelSkipSimd));

                // estimate human apparent brightness and flag as dark if below threshold
                // Maximum value remains below 2^15 - 1 as in IsDarkTaskScalar() so only the low 16 bits need be retained.  Since alpha is multiplied by zero
                // the intermediate format is [ Bscale * B0 + Gscale * G0, Rscale * R0, Bscale * B1 + Gscale * G1, Rscale * R1, ... ] and a self hadd produces
                // [ luminosity0, luminosity1, luminosity2, luminosity3, luminosity0, luminosity1, luminosity2, luminosity3 ].  If the compare was performed in
                // epi16 only 2^15 * 4 = 128k pixels can be tested before an integer overflow might occur.  Promoting to epi32 by discarding the redundant upper 
                // half avoids this.
                // First argument of _mm_maddubs_epi16() is epu8, second is epi8; pixels need to be first and coefficients must be 127 or less to avoid the 
                // product becoming negative.
                __m128i humanPerceivedLuminosity = _mm_maddubs_epi16(pixelQuad, luminosityCoefficients);
                        humanPerceivedLuminosity = _mm_hadds_epi16(humanPerceivedLuminosity, humanPerceivedLuminosity);
                        humanPerceivedLuminosity = _mm_cvtepi16_epi32(humanPerceivedLuminosity);
                __m128i isDark = _mm_cmplt_epi32(humanPerceivedLuminosity, scaledDarkPixelThreshold);
                darkPixels = _mm_add_epi32(darkPixels, isDark);

                // check if the pixel is a grey scale vs. color pixel, using a heuristic
                // In greyscale r = g = b but some cameras have a bit of color cast in infrared images, so allow a tolerance.
                // Since alphas are set to 255 at jpeg decode and aren't shuffled they contribute zero to the sum of absolute differences.  A check in
                // MemoryImage::IsDark() excludes RGB or BGR formats where the alpha's not a controlled value.
                __m128i pixelQuadGrba = _mm_shuffle_epi8(pixelQuad, bgraToGrbaShuffle);
                __m128i totalColoration = _mm_sad_epu8(pixelQuad, pixelQuadGrba);
                __m128i isGrey = _mm_cmplt_epi32(totalColoration, greyscalePixelThreshold);
                greySumsOfAbsoluteDifferences = _mm_add_epi32(greySumsOfAbsoluteDifferences, isGrey);
            }

            // both grey and dark pixels are counted by decrementing so a negation is needed to to return a positive count
            // For greyscale pixels the count is of _mm_sad_epu8() outputs less than threshold, each of which represents two pixels.
            *(greyscalePixels) = -2 * (greySumsOfAbsoluteDifferences.m128i_i32[0] + greySumsOfAbsoluteDifferences.m128i_i32[1] + greySumsOfAbsoluteDifferences.m128i_i32[2] + greySumsOfAbsoluteDifferences.m128i_i32[3]);
            *(totalPixelsChecked) = this->TotalPixels() / NativeImage::DarkPixelSkipSimd;
            return -(darkPixels.m128i_i32[0] + darkPixels.m128i_i32[1] + darkPixels.m128i_i32[2] + darkPixels.m128i_i32[3]);
        }
    }
}