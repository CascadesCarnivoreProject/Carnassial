#pragma once
#include "turbojpeg.h"

namespace Carnassial
{
	namespace Native
	{
		class NativeImage
		{
		private:
			// a truly greyscale pixel has r = g = b but allow for some color cast
			static const __int32 GreyscalePixelThreshold = 40;

			TJPF format;
			__int32 pixelHeight;
			unsigned __int8* pixels;
			__int32 pixelSizeInBytes;
			__int32 pixelWidth;

			__int32 Abs(__int32 value);
			void AllocatePixels();

			void CombinedDifferenceAvx2(__int32 block, const NativeImage* previous, const NativeImage* next, unsigned __int8 threshold, NativeImage* difference);
			void CombinedDifferenceScalar(__int32 block, const NativeImage* previous, const NativeImage* next, unsigned __int8 threshold, NativeImage* difference);
			void CombinedDifferenceSse41(__int32 block, const NativeImage* previous, const NativeImage* next, unsigned __int8 threshold, NativeImage* difference);
			void CombinedDifferenceSse41Vex(__int32 block, const NativeImage* previous, const NativeImage* next, unsigned __int8 threshold, NativeImage* difference);
			void DifferenceAvx2(__int32 block, const NativeImage* other, unsigned __int8 threshold, NativeImage* difference);
			void DifferenceScalar(__int32 block, const NativeImage* other, unsigned __int8 threshold, NativeImage* difference);
			void DifferenceSse41(__int32 block, const NativeImage* other, unsigned __int8 threshold, NativeImage* difference);
			void DifferenceSse41Vex(__int32 block, const NativeImage* other, unsigned __int8 threshold, NativeImage* difference);

			__int32 GetNumberOfCalculationBlocks(__int32 blockSizeInBytes);

			bool IsBlackAvx2();
			bool IsBlackScalar();
			bool IsBlackSse41();
			bool IsBlackSse41Vex();

			__int32 IsDarkAvx2(unsigned __int8 darkPixelThreshold, __int32* uncoloredPixels, __int32* totalPixelsChecked);
			__int32 IsDarkScalar(unsigned __int8 darkPixelThreshold, __int32* uncoloredPixels, __int32* totalPixelsChecked);
			__int32 IsDarkSse41(unsigned __int8 darkPixelThreshold, __int32* uncoloredPixels, __int32* totalPixelsChecked);
			__int32 IsDarkSse41Vex(unsigned __int8 darkPixelThreshold, __int32* uncoloredPixels, __int32* totalPixelsChecked);

		public:
			static const __int32 CalculationPixelSizeInBytes = 4;
			// must be aligned to CalculationPixelSizeInBytes, sizeof(__m128i), sizeof(__m256i) and the granularity used by AllocatePixels()
			// Optimum seems to be about 12k (evaluated on Haswell quad core).
			static const __int32 CombinedDifferenceTaskBlockInBytes = 12 * 1024;
			// see remarks in MemoryImage::IsDark()
			static const __int32 DarkPixelSkipScalar = 10;
			static const __int32 DarkPixelSkipSimd = 8;
			// must be aligned as CombinedDifferenceTaskBlockInBytes
			// Optimum seems to be about 16k (evaluated on Haswell quad core).
			static const __int32 DifferenceTaskBlockInBytes = 16 * 1024;
			// a greyscale image (given the threshold tolerance) will typically have about 90% of its pixels as grey scale
			static constexpr double GreyScaleImageThreshold = 0.9;
			// preferred formats for WriteableBitmap are Bgr32 or Pbgra32 as these do not require conversion (see MSDN docs for .ctor)
			// Since SetSource() uses drives image display destinations in Carnassial to WriteableBitmap it's typically most efficient to load images
			// in a WriteableBitmap friendly format from the start.  The value here must be kept in sync with the value of PreferredPixelFormat along
			// with the implementations of Difference(), IsDark(), and so on.
			static const TJPF PreferredPixelFormat = TJPF_BGRA;

			NativeImage(__int32 width, __int32 height, TJPF format, __int32 pixelSizeInBytes);
			NativeImage(unsigned __int8* jpeg, __int32 jpegLength, __int32 requestedWidth);
			~NativeImage();

			TJPF Format()
			{
				return this->format;
			}

			__int32 PixelHeight()
			{
				return this->pixelHeight;
			}

			__int32 PixelSizeInBytes()
			{
				return this->pixelSizeInBytes;
			}

			__int32 PixelWidth()
			{
				return this->pixelWidth;
			}

			__int32 StrideInBytes()
			{
				return this->pixelWidth * this->pixelSizeInBytes;
			}

			__int32 TotalPixelBytes()
			{
				return this->TotalPixels() * this->pixelSizeInBytes;
			}

			__int32 TotalPixels()
			{
				return this->pixelHeight * this->pixelWidth;
			}

			void CopyPixelsFrom(unsigned __int8* source);
			void CopyPixelsTo(unsigned __int8* destination);
			void Difference(const NativeImage* other, unsigned __int8 threshold, NativeImage* difference);
			void Difference(const NativeImage* previous, const NativeImage* next, unsigned __int8 threshold, NativeImage* difference);
			bool IsBlack();
			bool IsDark(unsigned __int8 darkPixelThreshold, double darkPixelRatioThreshold, double* darkPixelFraction, bool* isColor);
		};
	}
}