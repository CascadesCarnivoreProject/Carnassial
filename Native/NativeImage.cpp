#include "Pch.h"
#include <algorithm>
#include <complex>
#include <immintrin.h>
#include <new>
#include <ppl.h>
#include <stdexcept>
#include "NativeImage.h"

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

		void NativeImage::CombinedDifferenceScalar(const NativeImage* previous, const NativeImage* next, unsigned __int8 threshold, NativeImage* difference)
		{
			const unsigned __int8* nextPixel = next->pixels;
			const unsigned __int8* previousPixel = previous->pixels;
			const __int16 thresholdAsInt16 = 3 * (__int16)threshold;
			const unsigned __int8* thisPixel = this->pixels;
			unsigned __int8* differencePixel = difference->pixels;
			for (const unsigned __int8* endPixel = std::min(reinterpret_cast<const unsigned __int8*>(this->pixels) + this->TotalPixelBytes(), thisPixel + this->TotalPixelBytes());
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

		void NativeImage::CopyPixelsFrom(unsigned __int8* source)
		{
			std::memcpy(this->pixels, source, this->TotalPixelBytes());
		}

		void NativeImage::CopyPixelsTo(unsigned __int8* destination) const
		{
			std::memcpy(destination, this->pixels, this->TotalPixelBytes());
		}

		void NativeImage::Difference(const NativeImage* other, unsigned __int8 threshold, NativeImage *difference)
		{
			this->DifferenceSse41Vex(other, threshold, difference);
			// this->DifferenceScalar(other, threshold, difference);
		}

		void NativeImage::Difference(const NativeImage* previous, const NativeImage* next, unsigned __int8 threshold, NativeImage* difference)
		{
			this->CombinedDifferenceSse41Vex(previous, next, threshold, difference);
			// this->CombinedDifferenceScalar(previous, next, threshold, difference);
		}

		void NativeImage::DifferenceScalar(const NativeImage* other, unsigned __int8 threshold, NativeImage* difference)
		{
			const __int16 thresholdAsInt16 = 3 * (__int16)threshold;

			unsigned __int8* differencePixel = difference->pixels;
			const unsigned __int8* otherPixel = other->pixels;
			const unsigned __int8* thisPixel = this->pixels;
			for (const unsigned __int8* endPixel = std::min(reinterpret_cast<const unsigned _int8*>(this->pixels) + this->TotalPixelBytes(), thisPixel + this->TotalPixelBytes());
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

		double NativeImage::GetLuminosityAndColoration(double* coloration, __int32 bottomRowsToSkip)
		{
			// estimate the image's properties by examining a portion of the pixels
			return this->GetLuminosityAndColorationSse41Vex(coloration, bottomRowsToSkip);
			// return this->GetLuminosityAndColorationScalar(coloration, bottomRowsToSkip);
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