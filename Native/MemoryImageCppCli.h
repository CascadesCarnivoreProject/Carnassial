#pragma once
#include "turbojpeg.h"

using namespace System;
using namespace System::Runtime::InteropServices;
using namespace System::Windows::Controls;
using namespace System::Windows::Media;
using namespace System::Windows::Media::Imaging;

namespace Carnassial
{
	namespace Native
	{
		/// <summary>
		/// Shim interop class for reading JPEG images with TurboJPEG and to pick up pixel format definitions from turbojpeg.h.
		/// </summary>
		/// <remarks>
		/// <see cref="MemoryImageCppCli"/> is effectively a partial class with <see cref="MemoryImage"/> with functionality preferrentially
		/// placed in C# rather than in C++/CLI due to more effective Roslyn and IntelliSense interactions as of Visual Studio 17.8 (2022).
		/// </remarks>
		public ref class MemoryImageCppCli
		{
		private:
			bool decodeError;
			PixelFormat format;
			int pixelHeight;
			array<byte>^ pixels;
			int pixelSizeInBytes;
			int pixelWidth;

			void AllocatePixels();

			PixelFormat GetPixelFormat(TJPF turboJpegPixelFormat);
			TJPF GetTurboJpegPixelFormat(PixelFormat^ format);

		protected:
			static const int CalculationPixelSizeInBytes = 4;

			// preferred formats for WriteableBitmap are Bgr32 or Pbgra32 as these do not require conversion (see MSDN docs for .ctor)
			// Since SetSource() uses drives image display destinations in Carnassial to WriteableBitmap it's typically most efficient to load images
			// in a WriteableBitmap friendly format from the start.  The value here must be kept in sync with the value of PreferredPixelFormat along
			// with the implementations of Difference(), IsDark(), and so on.
			static const PixelFormat PreferredPixelFormat = PixelFormats::Pbgra32; // not used in C++/CLI but kept adjacent to PreferredTurboJpegPixelFormat for consistency
			static const TJPF PreferredTurboJpegPixelFormat = TJPF::TJPF_BGRA;

			MemoryImageCppCli(BitmapSource^ bitmap);
			MemoryImageCppCli(array<unsigned __int8>^ jpeg, Nullable<int>^ requestedWidth);
			MemoryImageCppCli(array<unsigned __int8>^ jpeg, int offset, int length, Nullable<int>^ requestedWidth);
			MemoryImageCppCli(int width, int height, PixelFormat format);

			property array<byte>^ Pixels
			{
				array<byte>^ get() { return this->pixels; }
			}

			property int PixelSizeInBytes
			{
				int get() { return this->pixelSizeInBytes; }
			}

			property int StrideInBytes
			{
				int get() { return this->pixelWidth * this->pixelSizeInBytes; }
			}

			property int TotalPixelBytes
			{
				int get() { return this->TotalPixels * this->pixelSizeInBytes; }
			}

		public:
			property bool DecodeError
			{
				bool get() { return this->decodeError; }
			}

			property PixelFormat Format
			{
				PixelFormat get() { return this->format; }
			}

			property int PixelHeight
			{
				int get() { return this->pixelHeight; }
			}

			property int PixelWidth
			{
				int get() { return this->pixelWidth; }
			}

			property int TotalPixels
			{
				int get() { return this->pixelWidth * this->pixelHeight; }
			}

			bool TryDecode(array<unsigned __int8>^ jpegFileBytes, int offsetInBytes, int lengthInBytes, Nullable<int>^ requestedImageWidth);
		};
	}
}