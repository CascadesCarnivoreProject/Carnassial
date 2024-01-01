#include "PchClr.h"
#include <stdexcept>
#include "MemoryImageCppCli.h"

using namespace System;
using namespace System::ComponentModel;
using namespace System::Diagnostics;
using namespace System::Runtime::InteropServices;
using namespace System::Runtime::Intrinsics;
using namespace System::Runtime::Intrinsics::X86;
using namespace System::Threading::Tasks;
using namespace System::Windows;

namespace Carnassial
{
	namespace Native
	{
		MemoryImageCppCli::MemoryImageCppCli(BitmapSource^ bitmap)
			: MemoryImageCppCli(bitmap->PixelWidth, bitmap->PixelHeight, bitmap->Format)
		{
			bitmap->CopyPixels(this->pixels, this->PitchInBytes, 0);
		}

		MemoryImageCppCli::MemoryImageCppCli(array<unsigned __int8>^ jpeg, Nullable<int>^ requestedWidth)
			: MemoryImageCppCli(jpeg, 0, jpeg->Length, requestedWidth)
		{
		}

		MemoryImageCppCli::MemoryImageCppCli(array<unsigned __int8>^ jpeg, int offset, int length, Nullable<int>^ requestedWidth)
		{
			this->TryDecode(jpeg, offset, length, requestedWidth);
		}

		MemoryImageCppCli::MemoryImageCppCli(int width, int height, PixelFormat format)
		{
			this->decompressionError = false;
			this->format = format;
			this->pixelHeight = height;
			this->pixelWidth = width;
			this->pixelSizeInBytes = format.BitsPerPixel / 8;
			this->AllocatePixels();
		}

		void MemoryImageCppCli::AllocatePixels()
		{
			// round the size of the pixel array up to the next 32 byte multiple for loop simplicity
			int totalPixelBytes = this->TotalPixelBytes;
			int bytesToAllocate = sizeof(Vector256<byte>) * (totalPixelBytes / sizeof(Vector256<byte>));
			if ((totalPixelBytes % sizeof(Vector256<byte>)) != 0)
			{
				bytesToAllocate += sizeof(Vector256<byte>);
			}

			this->pixels = gcnew array<byte>(bytesToAllocate);
		}

		PixelFormat MemoryImageCppCli::GetPixelFormat(TJPF turboJpegPixelFormat)
		{
			if (TJPF::TJPF_BGR == turboJpegPixelFormat)
			{
				return PixelFormats::Bgr24;
			}
			if (TJPF::TJPF_BGRX == turboJpegPixelFormat)
			{
				return PixelFormats::Bgr32;
			}
			if (TJPF::TJPF_BGRA == turboJpegPixelFormat)
			{
				return PixelFormats::Pbgra32;
			}
			if (TJPF::TJPF_RGB == turboJpegPixelFormat)
			{
				return PixelFormats::Rgb24;
			}
			throw gcnew NotSupportedException("Unhandled bitmap format " + ((int)turboJpegPixelFormat).ToString() + ".");
		}

		TJPF MemoryImageCppCli::GetTurboJpegPixelFormat(PixelFormat^ format)
		{
			if (PixelFormats::Bgr24 == *(format))
			{
				return TJPF::TJPF_BGR;
			}
			if (PixelFormats::Bgr32 == *(format))
			{
				return TJPF::TJPF_BGRX;
			}
			if ((PixelFormats::Bgra32 == *(format)) ||
				(PixelFormats::Pbgra32 == *(format)))
			{
				return TJPF::TJPF_BGRA;
			}
			if (PixelFormats::Rgb24 == *(format))
			{
				return TJPF::TJPF_RGB;
			}
			throw gcnew ArgumentOutOfRangeException("bitmap", "Unhandled bitmap format '" + format + "'.");
		}

		/// <returns>true if pixel decompresion was attempted, false if pixels are already allocated but requested decode size isn't compatible</returns>
		bool MemoryImageCppCli::TryDecode(array<unsigned __int8>^ jpegFileBytes, int offsetInBytes, int lengthInBytes, Nullable<int>^ requestedImageWidth)
		{
			int requestedWidthNative = (requestedImageWidth != nullptr) && requestedImageWidth->HasValue ? requestedImageWidth->Value : -1;
			tjhandle decompressor = tjInitDecompress();
			// see http://www.libjpeg-turbo.org/About/TurboJPEG for TurboJpeg API documentation
			pin_ptr<byte> jpegBytes = &jpegFileBytes[offsetInBytes];
			int result = tj3DecompressHeader(decompressor, jpegBytes, lengthInBytes);
			if (result != 0)
			{
				throw gcnew ArgumentException(gcnew String(tj3GetErrorStr(decompressor)), "jpegFileBytes");
			}

			int bitsPerSample = tj3Get(decompressor, TJPARAM_PRECISION);
			if (bitsPerSample != 8)
			{
				throw gcnew NotSupportedException("Unhandled bit depth of " + bitsPerSample + ".");
			}

			int height = tj3Get(decompressor, TJPARAM_JPEGHEIGHT);
			int width = tj3Get(decompressor, TJPARAM_JPEGWIDTH);
			if (requestedWidthNative != -1)
			{
				// if a width was specified, downsize the decode to the smallest available size which is still larger than the requested width
				// if no width was specified, default to full size decode
				// If needed, supported downsizing ratios can be checked with
				// int scalingFactorLength;
				// tjscalingfactor* scalingFactors = tjGetScalingFactors(&scalingFactorLength);
				int downsizeRatio = width / requestedWidthNative;
				if (downsizeRatio >= 8)
				{
					height /= 8;
					width /= 8;
					tjscalingfactor scalingFactor = { 1, 8 };
					tj3SetScalingFactor(decompressor, scalingFactor);
				}
				else if (downsizeRatio >= 4)
				{
					height /= 4;
					width /= 4;
					tjscalingfactor scalingFactor = { 1, 4 };
					tj3SetScalingFactor(decompressor, scalingFactor);
				}
				else if (downsizeRatio >= 3)
				{
					height *= 3;
					width *= 3;
					height /= 8;
					width /= 8;
					tjscalingfactor scalingFactor = { 3, 8 };
					tj3SetScalingFactor(decompressor, scalingFactor);
				}
				else if (downsizeRatio >= 2)
				{
					height /= 2;
					width /= 2;
					tjscalingfactor scalingFactor = { 1, 2 };
					tj3SetScalingFactor(decompressor, scalingFactor);
				}
				// decompressor default is TJUNSCALED = { 1, 1 }
			}

			if ((this->pixelHeight > 0) && (this->pixelHeight != height))
			{
				return false;
			}
			if ((this->pixelWidth > 0) && (this->pixelWidth != width))
			{
				return false;
			}

			this->format = PixelFormats::Pbgra32; // MemoryImageCppCli::PreferredTurboJpegPixelFormat;
			this->pixelHeight = height;
			this->pixelSizeInBytes = tjPixelSize[MemoryImageCppCli::PreferredTurboJpegPixelFormat];
			this->pixelWidth = width;
			this->AllocatePixels();

			pin_ptr<unsigned __int8> pinnedPixels = &this->pixels[0];
			result = tj3Decompress8(decompressor, jpegBytes, lengthInBytes, pinnedPixels, this->PitchInBytes, MemoryImageCppCli::PreferredTurboJpegPixelFormat);
			tjDestroy(decompressor);
			// most common error is an incompletely written .jpg because a trail camera triggered when it was opened and was turned off
			this->decompressionError = result != 0;

			return true;
		}
	}
}