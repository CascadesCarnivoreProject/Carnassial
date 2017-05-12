#pragma once
#include "turbojpeg.h"
#include "NativeImage.h"

using namespace System;
using namespace System::Runtime::InteropServices;
using namespace System::Windows::Controls;
using namespace System::Windows::Media;
using namespace System::Windows::Media::Imaging;

namespace Carnassial
{
	namespace Native
	{
		// a managed wrapper and error checking interface for NativeImage
		public ref class MemoryImage
		{
		private:
			static const __int32 DefaultDpi = 96;

			bool decodeError;
			PixelFormat format;
			NativeImage* nativeImage;

			MemoryImage(__int32 width, __int32 height, PixelFormat^ format);

			PixelFormat GetPixelFormat();
			TJPF GetTurboJpegPixelFormat(PixelFormat^ format);

		public:
			// see remarks for NativeImage::PreferredPixelFormat
			static const PixelFormat PreferredPixelFormat = PixelFormats::Pbgra32;

			MemoryImage(BitmapSource^ bitmap);
			MemoryImage(array<unsigned __int8>^ jpeg, Nullable<__int32>^ requestedWidth);
			!MemoryImage();
			~MemoryImage();

			property bool DecodeError
			{
				bool get() { return this->decodeError; }
			}

			property PixelFormat Format
			{
				PixelFormat get() { return this->format; }
			}

			property __int32 PixelHeight
			{
				__int32 get() { return this->nativeImage->PixelHeight(); }
			}

			property __int32 PixelWidth
			{
				__int32 get() { return this->nativeImage->PixelWidth(); }
			}

			property __int32 TotalPixels
			{
				__int32 get() { return this->nativeImage->TotalPixels(); }
			}

			bool IsBlack();
			bool IsDark(unsigned __int8 darkPixelThreshold, double darkPixelRatio);
			bool IsDark(unsigned __int8 darkPixelThreshold, double darkPixelRatio, [Out] double% darkPixelFraction, [Out] bool% isColor);
			bool MismatchedOrNot32BitBgra(MemoryImage^ other);
			void SetSource(Image^ image);
			bool TryDifference(MemoryImage^ other, unsigned __int8 threshold, [Out] MemoryImage^% difference);
			bool TryDifference(MemoryImage^ previous, MemoryImage^ next, unsigned __int8 threshold, [Out] MemoryImage^% difference);
		};
	}
}