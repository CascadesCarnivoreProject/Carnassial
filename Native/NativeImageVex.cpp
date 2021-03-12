#include "Pch.h"
#include <algorithm>
#include <immintrin.h>
#include "NativeImage.h"

namespace Carnassial
{
	namespace Native
	{
		// copy/paste of CombinedDifferenceSse41()
		void NativeImage::CombinedDifferenceSse41Vex(__int32 block, const NativeImage* previous, const NativeImage* next, unsigned __int8 threshold, NativeImage* difference)
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

		// copy/paste of DifferenceSse41()
		void NativeImage::DifferenceSse41Vex(__int32 block, const NativeImage* other, unsigned __int8 threshold, NativeImage* difference)
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

				// no SIMD integer division is available; use integer approximation of a divide by three to find the average for the output pixel value
				// The maximum possible 40 bits of the 64 bit intermediates are used (the maximum sum of absolute differences is 6 * 255 = 10.58 bytes)
				// so this is equivalent to multiplying by 0.16666666674428.
				__m128i outputQuad = _mm_srli_epi64(_mm_mul_epu32(numeratorForAverage, sumsOfAbsoluteDifferences), 32);
				outputQuad = _mm_blendv_epi8(blackQuad, outputQuad, aboveThreshold);
				outputQuad = _mm_shuffle_epi8(outputQuad, bgrBroadcast);
				_mm_store_si128(differencePixels + pixelQuadIndex, outputQuad);
			}
		}

		// copy/paste of GetLuminosityAndColorationSse41()
		double NativeImage::GetLuminosityAndColorationSse41Vex(double* coloration, __int32 bottomRowsToSkip)
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
	}
}