#include "Stdafx.h"
#include <algorithm>
#include <complex>
#include <immintrin.h>
#include <new>
#include <ppl.h>
#include <stdexcept>
#include "NativeImage.h"
#include "NativeProcessor.h"

// NativeProcessor.h includes Windows.h, which #defines min and conflicts with use of std::min below
#undef min

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

		NativeImage::NativeImage(unsigned __int8* jpeg, __int32 jpegLength, __int32 requestedWidth, bool* decodeError)
		{
			this->format = NativeImage::PreferredPixelFormat;
			this->pixelHeight = -1;
			this->pixelSizeInBytes = -1;
			this->pixelWidth = -1;
			this->TryDecode(jpeg, jpegLength, requestedWidth, decodeError);
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

		void NativeImage::Difference(const NativeImage* other, unsigned __int8 threshold, NativeImage *difference)
		{
			// check in MemoryImage::Difference() ensures pixels start with a color tuple, alpha channel cancels since it's consistently set to 0xff
			__int32 differenceBlocks = this->GetNumberOfCalculationBlocks(NativeImage::DifferenceTaskBlockInBytes);

			// SIMD paths run single threaded as this is more performant than parallel execution due to memory bandwidth limitations.
			// See profiling results in the comments for MemoryImage::TryDifference().  Use of block processing is, however, retained
			// across CPU dispatching.  The overhead is negligible and code modifications for concurrency exploration are simplified.
			// AVX2 support was removed in 2.2.0.3 as profiling showed it simply added to the already sufficient memory bandwidth
			// pressure available with SSE4.1, using more power to produce results slightly more slowly.
			if (NativeProcessor::Avx())
			{
				for (int block = 0; block < differenceBlocks; ++block)
				{
					this->DifferenceSse41Vex(block, other, threshold, difference);
				}
			}
			else if (NativeProcessor::Sse41())
			{
				for (int block = 0; block < differenceBlocks; ++block)
				{
					this->DifferenceSse41(block, other, threshold, difference);
				}
			}
			else
			{
				if (this->TotalPixels() >= NativeImage::MultithreadedThresholdInPixels)
				{
					// performance varies with image stride interactions with the cache but, in general, it appears the cache locality 
					// advantage of smaller blocks substantially exceeds task dispatch overhead
					concurrency::parallel_for<__int32>(0, differenceBlocks, 1, [this, other, threshold, difference](int block)
					{
						this->DifferenceScalar(block, other, threshold, difference);
					});
				}
				else
				{
					for (int block = 0; block < differenceBlocks; ++block)
					{
						this->DifferenceScalar(block, other, threshold, difference);
					}
				}
			}
		}

		void NativeImage::Difference(const NativeImage* previous, const NativeImage* next, unsigned __int8 threshold, NativeImage* difference)
		{
			// see remarks in other overload of Difference()
			__int32 differenceBlocks = this->GetNumberOfCalculationBlocks(NativeImage::CombinedDifferenceTaskBlockInBytes);
			//if (NativeProcessor::Avx2())
			//{
			//	for (int block = 0; block < differenceBlocks; ++block)
			//	{
			//		this->CombinedDifferenceAvx2(block, previous, next, threshold, difference);
			//	}
			//}
			if (NativeProcessor::Avx())
			{
				for (int block = 0; block < differenceBlocks; ++block)
				{
					this->CombinedDifferenceSse41Vex(block, previous, next, threshold, difference);
				}
			}
			else if (NativeProcessor::Sse41())
			{
				for (int block = 0; block < differenceBlocks; ++block)
				{
					this->CombinedDifferenceSse41(block, previous, next, threshold, difference);
				}
			}
			else
			{
				if (this->TotalPixels() >= NativeImage::MultithreadedThresholdInPixels)
				{
					concurrency::parallel_for<__int32>(0, differenceBlocks, 1, [this, previous, next, threshold, difference](int block)
					{
						this->CombinedDifferenceScalar(block, previous, next, threshold, difference);
					});
				}
				else
				{
					for (int block = 0; block < differenceBlocks; ++block)
					{
						this->CombinedDifferenceScalar(block, previous, next, threshold, difference);
					}
				}
			}
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

		double NativeImage::GetLuminosityAndColoration(double* coloration, __int32 bottomRowsToSkip)
		{
			// estimate the image's properties by examining a portion of the pixels
			// As of Carnassial 2.2.0.3, this is called on thumbnails or maximally downscaled images with concurrency considerably
			// higher in the call graph.  So parallel execution isn't supported as it's deterimental rather than beneficial.
			if (NativeProcessor::Avx())
			{
				return this->GetLuminosityAndColorationSse41Vex(coloration, bottomRowsToSkip);
			}
			if (NativeProcessor::Sse41())
			{
				return this->GetLuminosityAndColorationSse41(coloration, bottomRowsToSkip);
			}
			return this->GetLuminosityAndColorationScalar(coloration, bottomRowsToSkip);
		}

		double NativeImage::GetLuminosityAndColorationScalar(double* coloration, __int32 bottomRowsToSkip)
		{
			__int64 colorationTotal = 0;
			__int64 luminosityTotal = 0;
			const unsigned __int8* thisPixel = this->pixels;
			const unsigned __int8* endPixel = thisPixel + this->GetPixelAreaSizeInBytes(bottomRowsToSkip);
			for (; thisPixel < endPixel; thisPixel += NativeImage::CalculationPixelSizeInBytes)
			{
				// get bytes in pixel
				__int16 pixelB = *thisPixel;
				__int16 pixelG = *(thisPixel + 1);
				__int16 pixelR = *(thisPixel + 2);

				// estimate human apparent brightness
				// In floating point this would be
				//   double humanPercievedLuminosity = 0.299 * pixelR + 0.5876 * pixelG + 0.114 * pixelB;
				// but an integer version is 26% faster as converting to floating point is avoided.  The coefficients are multiplied by 
				// 125 as this gives a maximum value of 31875 for a white pixel (255, 255, 255), avoiding overflow in a signed 16 bit 
				// integer (max positive value 32767).  If the coefficient scaling is changed the luminosity calculation below also 
				// needs also to be updated.
				__int16 humanPercievedLuminosity = 37 * pixelR + 74 * pixelG + 14 * pixelB;
				luminosityTotal += humanPercievedLuminosity;

				// calculate and accumulate coloration
				__int16 coloration = NativeImage::Abs(pixelR - pixelG) + NativeImage::Abs(pixelG - pixelB) + NativeImage::Abs(pixelB - pixelR);
				colorationTotal += coloration;
			}

			double pixelsChecked = (double)(this->TotalPixels());
			*(coloration) = colorationTotal / (255.0 * pixelsChecked);
			return (double)luminosityTotal / (125.0 * pixelsChecked);
		}

		// functionally identical to GetLuminosityAndColorationScalar()
		double NativeImage::GetLuminosityAndColorationSse41(double* coloration, __int32 bottomRowsToSkip)
		{
			// same scaling approach as in GetLuminosityAndColorationScalar()
			//                                             A3  B3  R3  G3  A2 B2  R2 G2 A1 B1 R1 G1 A0 B0 R0 G0
			const __m128i bgraToGrbaShuffle = _mm_set_epi8(15, 12, 14, 13, 11, 8, 10, 9, 7, 4, 6, 5, 3, 0, 2, 1);
			//                                                  A3 R3  G3  B3  A2 R2  G2  B2  A1 R1  G1  B1  A0 R0  G0  B0
			const __m128i luminosityCoefficients = _mm_set_epi8(0, 37, 74, 14, 0, 37, 74, 14, 0, 37, 74, 14, 0, 37, 74, 14);
			const __int32 maxPixelQuadIndex = this->GetPixelAreaSizeInBytes(bottomRowsToSkip) / (__int32)sizeof(__m128i);
			const __m128i* pixels = reinterpret_cast<__m128i*>(this->pixels);
			// loop notionally uses 7 xmm registers
			//   xmm0 + luminosity coeffs + luminosity total + pixel data + shuffle + grba + coloration total
			__m128i colorationTotal = _mm_set_epi64x(0, 0);
			__m128i luminosityTotal = _mm_set_epi64x(0, 0);
			for (__int32 pixelQuadIndex = 0; pixelQuadIndex < maxPixelQuadIndex; ++pixelQuadIndex)
			{
				// get next quad of pixels
				__m128i pixelQuad = _mm_load_si128(pixels + pixelQuadIndex);

				// estimate human apparent brightness
				// Maximum value remains below 2^15 - 1 as in IsDarkTaskScalar() so only the low 16 bits need be retained.  Since alpha
				// is multiplied by zero the intermediate format is [ Bscale * B0 + Gscale * G0, Rscale * R0, Bscale * B1 + Gscale * G1,
				// Rscale * R1, ... ] and a self hadd produces [ luminosity0, luminosity1, luminosity2, luminosity3, luminosity0, 
				// luminosity1, luminosity2, luminosity3 ].
				// First argument of _mm_maddubs_epi16() is epu8, second is epi8; pixels need to be first and coefficients must be 127 or less to avoid the 
				// product becoming negative.
				__m128i humanPerceivedLuminosity = _mm_maddubs_epi16(pixelQuad, luminosityCoefficients);
				humanPerceivedLuminosity = _mm_hadds_epi16(humanPerceivedLuminosity, humanPerceivedLuminosity);
				humanPerceivedLuminosity = _mm_cvtepi16_epi32(humanPerceivedLuminosity);
				// accumulate luminosity of lower two pixels
				luminosityTotal = _mm_add_epi64(_mm_cvtepi32_epi64(humanPerceivedLuminosity), luminosityTotal);
				// accumulate luminosity of upper two pixels
				humanPerceivedLuminosity = _mm_unpackhi_epi64(humanPerceivedLuminosity, humanPerceivedLuminosity);
				luminosityTotal = _mm_add_epi64(_mm_cvtepi32_epi64(humanPerceivedLuminosity), luminosityTotal);

				// calculate and accumulate coloration
				// Since alphas are set to 255 at jpeg decode and aren't shuffled they contribute zero to the sum of absolute 
				// differences.  A check in MemoryImage::IsDark() excludes RGB or BGR formats where the alpha's not a controlled 
				// value.
				__m128i pixelQuadGrba = _mm_shuffle_epi8(pixelQuad, bgraToGrbaShuffle);
				__m128i coloration = _mm_sad_epu8(pixelQuad, pixelQuadGrba);
				colorationTotal = _mm_add_epi64(coloration, colorationTotal);
			}

			// _mm_cvtepi64_pd() requires AVX512VL and AVX512DQ, so convert doubles individually
			double pixelsChecked = (double)this->TotalPixels();
			*(coloration) = ((double)colorationTotal.m128i_i64[0] + (double)colorationTotal.m128i_i64[1]) / (2.0 * 255.0 * pixelsChecked);
			return ((double)luminosityTotal.m128i_i64[0] + (double)luminosityTotal.m128i_i64[1]) / (125.0 * 2.0 * 255.0 * pixelsChecked);
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

		void NativeImage::SetDefaultConcurrencyPolicy(__int32 maximumConcurrency)
		{
			concurrency::Scheduler::SetDefaultSchedulerPolicy(concurrency::SchedulerPolicy(2, concurrency::MinConcurrency, 1, concurrency::MaxConcurrency, maximumConcurrency));
		}

		bool NativeImage::TryDecode(unsigned __int8* jpeg, __int32 jpegLength, __int32 requestedWidth, bool* decodeError)
		{
			tjhandle decompressor = tjInitDecompress();
			__int32 colorspace, height, subsampling, width;
			// see http://www.libjpeg-turbo.org/About/TurboJPEG for TurboJpeg API documentation
			__int32 result = tjDecompressHeader3(decompressor, jpeg, jpegLength, &width, &height, &subsampling, &colorspace);
			if (result != 0)
			{
				throw std::runtime_error(tjGetErrorStr2(decompressor));
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

			if ((this->pixelHeight >= 0) && (this->pixelHeight != height))
			{
				return false;
			}
			if ((this->pixelWidth >= 0) && (this->pixelWidth != width))
			{
				return false;
			}

			this->format = NativeImage::PreferredPixelFormat;
			this->pixelHeight = height;
			this->pixelSizeInBytes = tjPixelSize[NativeImage::PreferredPixelFormat];
			this->pixelWidth = width;
			this->AllocatePixels();

			result = tjDecompress2(decompressor, jpeg, jpegLength, this->pixels, width, this->StrideInBytes(), height, NativeImage::PreferredPixelFormat, 0);
			tjDestroy(decompressor);
			*(decodeError) = result != 0;
			return true;
		}
	}
}