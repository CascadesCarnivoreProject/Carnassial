#pragma once
#include "turbojpeg.h"

namespace Carnassial
{
	namespace Native
	{
		class NativeImage
		{
		private:
			TJPF format;
			__int32 pixelHeight;
			unsigned __int8* pixels;
			__int32 pixelSizeInBytes;
			__int32 pixelWidth;

			__int32 Abs(__int32 value);
			void AllocatePixels();

			void CombinedDifferenceScalar(const NativeImage* previous, const NativeImage* next, unsigned __int8 threshold, NativeImage* difference);
			void CombinedDifferenceSse41Vex(const NativeImage* previous, const NativeImage* next, unsigned __int8 threshold, NativeImage* difference);
			void DifferenceScalar(const NativeImage* other, unsigned __int8 threshold, NativeImage* difference);
			void DifferenceSse41Vex(const NativeImage* other, unsigned __int8 threshold, NativeImage* difference);

			double GetLuminosityAndColorationScalar(double* coloration, __int32 bottomRowsToSkip);
			double GetLuminosityAndColorationSse41Vex(double* coloration, __int32 bottomRowsToSkip);

		public:
			static const __int32 CalculationPixelSizeInBytes = 4;
			// preferred formats for WriteableBitmap are Bgr32 or Pbgra32 as these do not require conversion (see MSDN docs for .ctor)
			// Since SetSource() uses drives image display destinations in Carnassial to WriteableBitmap it's typically most efficient to load images
			// in a WriteableBitmap friendly format from the start.  The value here must be kept in sync with the value of PreferredPixelFormat along
			// with the implementations of Difference(), IsDark(), and so on.
			static const TJPF PreferredPixelFormat = TJPF::TJPF_BGRA;

			NativeImage(__int32 width, __int32 height, TJPF format, __int32 pixelSizeInBytes);
			NativeImage(unsigned __int8* jpeg, __int32 jpegLength, __int32 requestedWidth, bool* decodeError);
			~NativeImage();

			TJPF Format() const
			{
				return this->format;
			}

			__int32 GetPixelAreaSizeInBytes(__int32 rowsToSkip) const
			{
				return this->pixelWidth * (this->pixelHeight - rowsToSkip) * this->pixelSizeInBytes;
			}

			__int32 PixelHeight() const
			{
				return this->pixelHeight;
			}

			__int32 PixelSizeInBytes() const
			{
				return this->pixelSizeInBytes;
			}

			__int32 PixelWidth() const
			{
				return this->pixelWidth;
			}

			__int32 StrideInBytes() const
			{
				return this->pixelWidth * this->pixelSizeInBytes;
			}

			__int32 TotalPixelBytes() const
			{
				return this->TotalPixels() * this->pixelSizeInBytes;
			}

			__int32 TotalPixels() const
			{
				return this->pixelHeight * this->pixelWidth;
			}

			void CopyPixelsFrom(unsigned __int8* source);
			void CopyPixelsTo(unsigned __int8* destination) const;
			void Difference(const NativeImage* other, unsigned __int8 threshold, NativeImage* difference);
			void Difference(const NativeImage* previous, const NativeImage* next, unsigned __int8 threshold, NativeImage* difference);
			double GetLuminosityAndColoration(double* coloration, __int32 bottomRowsToSkip);
			bool TryDecode(unsigned __int8* jpeg, __int32 jpegLength, __int32 requestedWidth, bool* decodeError);
		};
	}
}