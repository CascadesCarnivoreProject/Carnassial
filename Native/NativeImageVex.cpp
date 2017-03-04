#include "Stdafx.h"
#include <algorithm>
#include <immintrin.h>
#include "NativeImage.h"

namespace Carnassial
{
	namespace Native
	{
		void NativeImage::CombinedDifferenceAvx2(__int32 block, const NativeImage* previous, const NativeImage* next, unsigned __int8 threshold, NativeImage* difference)
		{
			const __m256i blackOctet = _mm256_set_epi8((__int8)0xff, 0, 0, 0, (__int8)0xff, 0, 0, 0, (__int8)0xff, 0, 0, 0, (__int8)0xff, 0, 0, 0, (__int8)0xff, 0, 0, 0, (__int8)0xff, 0, 0, 0, (__int8)0xff, 0, 0, 0, (__int8)0xff, 0, 0, 0);
			const __m256i threshold_epi16 = _mm256_set1_epi16(6 * threshold);
			const __m256i numeratorForAverage = _mm256_set_epi32(0, 357913941, 0, 357913941, 0, 357913941, 0, 357913941);
			const __m256i bgrBroadcast = _mm256_set_epi8(15, 8, 8, 8, 11, 8, 8, 8, 7, 0, 0, 0, 3, 0, 0, 0, 15, 8, 8, 8, 11, 8, 8, 8, 7, 0, 0, 0, 3, 0, 0, 0);

			const __int32 startPixelOctetIndex = NativeImage::CombinedDifferenceTaskBlockInBytes * block / (__int32)sizeof(__m256i);
			const __int32 endPixelOctetIndex = std::min(this->TotalPixelBytes() / (__int32)sizeof(__m256i), startPixelOctetIndex + NativeImage::CombinedDifferenceTaskBlockInBytes / (__int32)sizeof(__m256i));

			__m256i* differencePixels = reinterpret_cast<__m256i*>(difference->pixels);
			const __m256i* previousPixels = reinterpret_cast<__m256i*>(previous->pixels);
			const __m256i* nextPixels = reinterpret_cast<__m256i*>(next->pixels);
			const __m256i* pixels = reinterpret_cast<__m256i*>(this->pixels);
			for (__int32 pixelOctetIndex = startPixelOctetIndex; pixelOctetIndex < endPixelOctetIndex; ++pixelOctetIndex)
			{
				// performing thresholding on epi16 allows _mm256_blendv_epi8() to be used to set all four of the output pixels' alpha channels as the red
				// and alpha channels in each pixel are set to zero and therefore below threshold; this saves a second blend instruction
				__m256i thisPixelOctet = _mm256_load_si256(pixels + pixelOctetIndex);
				__m256i previousPixelOctet = _mm256_load_si256(previousPixels + pixelOctetIndex);
				__m256i sumsOfPreviousDifferences = _mm256_sad_epu8(thisPixelOctet, previousPixelOctet);
				__m256i previousAboveThreshold = _mm256_cmpgt_epi16(sumsOfPreviousDifferences, threshold_epi16);

				__m256i nextPixelOctet = _mm256_load_si256(nextPixels + pixelOctetIndex);
				__m256i sumsOfNextDifferences = _mm256_sad_epu8(thisPixelOctet, nextPixelOctet);
				__m256i nextAboveThreshold = _mm256_cmpgt_epi16(sumsOfNextDifferences, threshold_epi16);

				// no SIMD integer division is available; use integer approximation of a divide by 12 to find the average for the output pixel value
				// The maximum possible 41 bits of the 64 bit intermediates are used (the maximum sum of absolute differences is 12 * 255 = 10.58 bytes)
				// so this is equivalent to multiplying by 0.0833333334885538.
				__m256i outputOctet = _mm256_srli_epi64(_mm256_mul_epu32(numeratorForAverage, _mm256_add_epi64(sumsOfPreviousDifferences, sumsOfNextDifferences)), 32);

				__m256i aboveThreshold = _mm256_and_si256(previousAboveThreshold, nextAboveThreshold);
				outputOctet = _mm256_blendv_epi8(blackOctet, outputOctet, aboveThreshold);
				outputOctet = _mm256_shuffle_epi8(outputOctet, bgrBroadcast);
				_mm256_store_si256(differencePixels + pixelOctetIndex, outputOctet);
			}

			_mm256_zeroupper();
		}

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

		void NativeImage::DifferenceAvx2(__int32 block, const NativeImage* other, unsigned __int8 threshold, NativeImage* difference)
		{
			const __m256i blackOctet = _mm256_set_epi8((__int8)0xff, 0, 0, 0, (__int8)0xff, 0, 0, 0, (__int8)0xff, 0, 0, 0, (__int8)0xff, 0, 0, 0, (__int8)0xff, 0, 0, 0, (__int8)0xff, 0, 0, 0, (__int8)0xff, 0, 0, 0, (__int8)0xff, 0, 0, 0);
			const __m256i threshold_epi16 = _mm256_set1_epi16(6 * threshold);
			const __m256i numeratorForAverage = _mm256_set_epi32(0, 715827883, 0, 715827883, 0, 715827883, 0, 715827883);
			const __m256i bgrBroadcast = _mm256_set_epi8(15, 8, 8, 8, 11, 8, 8, 8, 7, 0, 0, 0, 3, 0, 0, 0, 15, 8, 8, 8, 11, 8, 8, 8, 7, 0, 0, 0, 3, 0, 0, 0);

			const __int32 startPixelOctetIndex = NativeImage::DifferenceTaskBlockInBytes * block / (__int32)sizeof(__m256i);
			const __int32 endPixelOctetIndex = std::min(this->TotalPixelBytes() / (__int32)sizeof(__m256i), startPixelOctetIndex + NativeImage::DifferenceTaskBlockInBytes / (__int32)sizeof(__m256i));

			__m256i* differencePixels = reinterpret_cast<__m256i*>(difference->pixels);
			const __m256i* otherPixels = reinterpret_cast<__m256i*>(other->pixels);
			const __m256i* pixels = reinterpret_cast<__m256i*>(this->pixels);
			for (__int32 pixelOctetIndex = startPixelOctetIndex; pixelOctetIndex < endPixelOctetIndex; ++pixelOctetIndex)
			{
				__m256i thisPixelOctet = _mm256_load_si256(pixels + pixelOctetIndex);
				__m256i otherPixelOctet = _mm256_load_si256(otherPixels + pixelOctetIndex);
				__m256i sumsOfAbsoluteDifferences = _mm256_sad_epu8(thisPixelOctet, otherPixelOctet);

				// performing thresholding on epi16 allows _mm256_blendv_epi8() to be used to set all four of the output pixels' alpha channels as the red
				// and alpha channels in each pixel are set to zero and therefore below threshold; this saves a second blend instruction
				__m256i aboveThreshold = _mm256_cmpgt_epi16(sumsOfAbsoluteDifferences, threshold_epi16);

				// no SIMD integer division is available; use integer approximation of a divide by three to find the average for the output pixel value
				// The maximum possible 40 bits of the 64 bit intermediates are used (the maximum sum of absolute differences is 6 * 255 = 10.58 bytes)
				// so this is equivalent to multiplying by 0.16666666674428.
				__m256i outputOctet = _mm256_srli_epi64(_mm256_mul_epu32(numeratorForAverage, sumsOfAbsoluteDifferences), 32);
						outputOctet = _mm256_blendv_epi8(blackOctet, outputOctet, aboveThreshold);
						outputOctet = _mm256_shuffle_epi8(outputOctet, bgrBroadcast);
				_mm256_store_si256(differencePixels + pixelOctetIndex, outputOctet);
			}

			_mm256_zeroupper();
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

		// simple widening of IsBlackSse4()
		bool NativeImage::IsBlackAvx2()
		{
			__int32 totalPixelOctets = this->TotalPixelBytes() / sizeof(__m256i);
			const __m256i* pixelOctets = reinterpret_cast<const __m256i*>(this->pixels);
			const __m256i alphaMask = _mm256_set1_epi32(0x00ffffff);
			for (__int32 pixelOctetIndex = totalPixelOctets - 1; pixelOctetIndex >= 0; --pixelOctetIndex)
			{
				__m256i pixelOctet = _mm256_load_si256(pixelOctets + pixelOctetIndex);
				if (_mm256_test_all_zeros(alphaMask, pixelOctet) == 0)
				{
					return false;
				}
			}

			_mm256_zeroupper();
			return true;
		}

		// copy/paste of IsBlackSse4()
		bool NativeImage::IsBlackSse41Vex()
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

		// Mostly simple widening of IsDarkSse41().  There is not a _mm256_cmplt_epi32() so _mm256_cmpgt_epi32() is used instead, resulting in slight threshold
		// changes and need to subtract from total pixels counted to find dark and greyscale pixels rather than counting them directly.
		__int32 NativeImage::IsDarkAvx2(unsigned __int8 darkPixelThreshold, __int32* greyscalePixels, __int32* totalPixelsChecked)
		{
			// same scaling approach as in IsDarkTaskScalar()
			__m256i lightPixels = _mm256_setzero_si256();
			__m256i colorSumsOfAbsoluteDifferences = _mm256_setzero_si256();
			//                                                A7, B7  G7  R7  A6 B6  G6 R6 A5 B5 G5 R5 A4 B4 R4 G4  A3  B3  R3  G3  A2 B2  R2 G2 A1 B1 R1 G1 A0 B0 R0 G0
			const __m256i bgraToGrbaShuffle = _mm256_set_epi8(15, 12, 14, 13, 11, 8, 10, 9, 7, 4, 6, 5, 3, 0, 2, 1, 15, 12, 14, 13, 11, 8, 10, 9, 7, 4, 6, 5, 3, 0, 2, 1);
			// there is no _mm256_cmple_epi32() so 1 needs to be added to both the grey and dark pixel thresholds for parity with IsDarkScalar()
			const __m256i greyscalePixelThreshold = _mm256_set1_epi64x(2 * NativeImage::GreyscalePixelThreshold);
			//                                                     A7 R7  G7  B7  A7 R6  G6  B6  A6 R5  G5  B5  A4 R4  G4  B4 A3 R3  G3  B3  A2 R2  G2  B2  A1 R1  G1  B1  A0 R0  G0  B0
			const __m256i luminosityCoefficients = _mm256_set_epi8(0, 37, 74, 14, 0, 37, 74, 14, 0, 37, 74, 14, 0, 37, 74, 14, 0, 37, 74, 14, 0, 37, 74, 14, 0, 37, 74, 14, 0, 37, 74, 14);
			const __int32 maxPixel = this->TotalPixels() - 8 * NativeImage::DarkPixelSkipSimd;
			const __m256i scaledDarkPixelThreshold = _mm256_set1_epi32(125 * darkPixelThreshold);
			const __int32* pixels = reinterpret_cast<__int32*>(this->pixels);
			const __m256i gatherOffsets = _mm256_set_epi32(7 * NativeImage::DarkPixelSkipSimd, 6 * NativeImage::DarkPixelSkipSimd,
				5 * NativeImage::DarkPixelSkipSimd, 4 * NativeImage::DarkPixelSkipSimd,
				3 * NativeImage::DarkPixelSkipSimd, 2 * NativeImage::DarkPixelSkipSimd,
				NativeImage::DarkPixelSkipSimd, 0);
			for (__int32 pixel = 0; pixel < maxPixel; pixel += 8 * NativeImage::DarkPixelSkipSimd)
			{
				// get next octet of pixels
				__m256i pixelOctet = _mm256_i32gather_epi32(pixels + pixel, gatherOffsets, 1);

				// estimate human apparent brightness and flag as dark if below threshold
				// Maximum value remains below 2^15 - 1 as in IsDarkTaskScalar() so only the low 16 bits need be retained.  Since alpha is multiplied by zero
				// the intermediate format is [ Bscale * B0 + Gscale * G0, Rscale * R0, Bscale * B1 + Gscale * G1, Rscale * R1, ... ] and a self hadd produces
				// [ luminosity0, luminosity1, luminosity2, luminosity3, luminosity0, luminosity1, luminosity2, luminosity3 ].  If the compare was performed in
				// epi16 only 2^15 * 4 = 128k pixels can be tested before an integer overflow might occur.  Promoting to epi32 by discarding the redundant upper 
				// half avoids this.
				// First argument of _mm256_maddubs_epi16() is epu8, second is epi8; pixels need to be first and coefficients must be 127 or less to avoid the 
				// product becoming negative.
				__m256i humanPerceivedLuminosity = _mm256_maddubs_epi16(pixelOctet, luminosityCoefficients);
						humanPerceivedLuminosity = _mm256_hadds_epi16(humanPerceivedLuminosity, humanPerceivedLuminosity);
						humanPerceivedLuminosity = _mm256_cvtepi16_epi32(_mm256_castsi256_si128(humanPerceivedLuminosity));
				__m256i isLight = _mm256_cmpgt_epi32(humanPerceivedLuminosity, scaledDarkPixelThreshold);
				lightPixels = _mm256_add_epi32(lightPixels, isLight);

				// check if the pixel is a grey scale vs. color pixel, using a heuristic
				// In greyscale r = g = b but some cameras have a bit of color cast in infrared images, so allow a tolerance.
				// Since alphas are set to 255 at jpeg decode and aren't shuffled they contribute zero to the sum of absolute differences.  A check in
				// MemoryImage::IsDark() excludes RGB or BGR formats where the alpha's not a controlled value.
				__m256i pixelOctetGrba = _mm256_shuffle_epi8(pixelOctet, bgraToGrbaShuffle);
				__m256i totalColoration = _mm256_sad_epu8(pixelOctet, pixelOctetGrba);
				__m256i isColor = _mm256_cmpgt_epi32(totalColoration, greyscalePixelThreshold);
				colorSumsOfAbsoluteDifferences = _mm256_add_epi32(colorSumsOfAbsoluteDifferences, isColor);
			}

			// both color and dark pixels are counted by decrementing so a negation is needed to to return a positive count
			// For greyscale pixels the color pixel count is of _mm256_sad_epu8() outputs greater than threshold, each of which represents two pixels.
			*(totalPixelsChecked) = this->TotalPixels() / NativeImage::DarkPixelSkipSimd;
			*(greyscalePixels) = *(totalPixelsChecked) + 2 * (colorSumsOfAbsoluteDifferences.m256i_i32[0] + colorSumsOfAbsoluteDifferences.m256i_i32[1] +
				colorSumsOfAbsoluteDifferences.m256i_i32[2] + colorSumsOfAbsoluteDifferences.m256i_i32[3] +
				colorSumsOfAbsoluteDifferences.m256i_i32[4] + colorSumsOfAbsoluteDifferences.m256i_i32[5] +
				colorSumsOfAbsoluteDifferences.m256i_i32[6] + colorSumsOfAbsoluteDifferences.m256i_i32[7]);
			__int32 darkPixels = *(totalPixelsChecked) + lightPixels.m256i_i32[0] + lightPixels.m256i_i32[1] + lightPixels.m256i_i32[2] + lightPixels.m256i_i32[3] +
				lightPixels.m256i_i32[4] + lightPixels.m256i_i32[5] + lightPixels.m256i_i32[6] + lightPixels.m256i_i32[7];
			_mm256_zeroupper();
			return darkPixels;
		}

		// Copy/paste of IsDarkSse41()
		__int32 NativeImage::IsDarkSse41Vex(unsigned __int8 darkPixelThreshold, __int32* greyscalePixels, __int32* totalPixelsChecked)
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
			for (__int32 pixel = 0; pixel < maxPixel; pixel += 4 * NativeImage::DarkPixelSkipSimd)
			{
				// get next quad of pixels
				__m128i pixelQuad = _mm_set_epi32(*(pixels + pixel), *(pixels + pixel + NativeImage::DarkPixelSkipSimd),
					*(pixels + pixel + 2 * NativeImage::DarkPixelSkipSimd), *(pixels + pixel + 3 * NativeImage::DarkPixelSkipSimd));

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
			*(greyscalePixels) = -2 * (greySumsOfAbsoluteDifferences.m128i_i32[0] + greySumsOfAbsoluteDifferences.m128i_i32[1] +
				greySumsOfAbsoluteDifferences.m128i_i32[2] + greySumsOfAbsoluteDifferences.m128i_i32[3]);
			*(totalPixelsChecked) = this->TotalPixels() / NativeImage::DarkPixelSkipSimd;
			return -(darkPixels.m128i_i32[0] + darkPixels.m128i_i32[1] + darkPixels.m128i_i32[2] + darkPixels.m128i_i32[3]);
		}
	}
}