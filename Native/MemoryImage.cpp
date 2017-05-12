#include "StdafxClr.h"
#include <stdexcept>
#include "MemoryImage.h"

using namespace System::Diagnostics;
using namespace System::Threading::Tasks;
using namespace System::Windows;

namespace Carnassial
{
	namespace Native
	{
		MemoryImage::MemoryImage(BitmapSource^ bitmap)
		{
			this->nativeImage = new NativeImage(bitmap->PixelWidth, bitmap->PixelHeight, MemoryImage::GetTurboJpegPixelFormat(bitmap->Format), bitmap->Format.BitsPerPixel / 8);
			this->format = this->GetPixelFormat();
			this->decodeError = false;

			// it appears the IntPtr in bitmap->CopyPixels(Int32Rect, IntPtr, int, int) must point to managed memory; work around via managed buffer
			// Inefficient, but this code path is used only on demand by a few members of Constant.Images in Carnassial's managed assembly which aren't
			// of any great size.
			array<unsigned __int8>^ managedPixels = gcnew array<unsigned __int8>(this->nativeImage->TotalPixelBytes());
			bitmap->CopyPixels(managedPixels, nativeImage->StrideInBytes(), 0);
			pin_ptr<unsigned __int8> pinnedPixels = &managedPixels[0];
			this->nativeImage->CopyPixelsFrom(pinnedPixels);
		}

		MemoryImage::MemoryImage(array<unsigned __int8>^ jpeg, Nullable<__int32>^ requestedWidth)
		{
			__int32 requestedWidthNative = (requestedWidth != nullptr) && requestedWidth->HasValue ? requestedWidth->Value : -1;
			bool decodeErrorNative;
			pin_ptr<unsigned __int8> jpegBytes = &jpeg[0];
			try
			{
				this->nativeImage = new NativeImage(jpegBytes, jpeg->Length, requestedWidthNative, &decodeErrorNative);
			}
			catch (const std::runtime_error& turboJpegError)
			{
				throw gcnew ArgumentException(gcnew String(turboJpegError.what()), "jpeg");
			}
			this->decodeError = decodeErrorNative;
			this->format = this->GetPixelFormat();
		}

		MemoryImage::MemoryImage(__int32 width, __int32 height, PixelFormat^ format)
		{
			this->nativeImage = new NativeImage(width, height, MemoryImage::GetTurboJpegPixelFormat(format), format->BitsPerPixel / 8);
			this->format = this->GetPixelFormat();
		}

		MemoryImage::!MemoryImage()
		{
			delete(this->nativeImage);
		}

		MemoryImage::~MemoryImage()
		{
			this->!MemoryImage();
		}

		PixelFormat MemoryImage::GetPixelFormat()
		{
			if (TJPF::TJPF_BGR == this->nativeImage->Format())
			{
				return PixelFormats::Bgr24;
			}
			if (TJPF::TJPF_BGRX == this->nativeImage->Format())
			{
				return PixelFormats::Bgr32;
			}
			if (TJPF::TJPF_BGRA == this->nativeImage->Format())
			{
				return PixelFormats::Pbgra32;
			}
			if (TJPF::TJPF_RGB == this->nativeImage->Format())
			{
				return PixelFormats::Rgb24;
			}
			throw gcnew NotSupportedException(String::Format("Unhandled bitmap format {0}.", (__int32)this->nativeImage->Format()));
		}

		TJPF MemoryImage::GetTurboJpegPixelFormat(PixelFormat^ format)
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
			throw gcnew ArgumentOutOfRangeException("bitmap", String::Format("Unhandled bitmap format {0}.", format));
		}

		// checks whether the image is completely black
		// This is called on initial frames of videos so typically operates on smaller numbers of pixels than still image calculations.
		bool MemoryImage::IsBlack()
		{
			if (this->nativeImage->Format() != NativeImage::PreferredPixelFormat)
			{
				throw gcnew NotSupportedException(String::Format("Unhandled image format {0}.", this->Format));
			}
			return this->nativeImage->IsBlack();
		}

		bool MemoryImage::IsDark(unsigned __int8 darkPixelThreshold, double darkPixelRatio)
		{
			double ignored1;
			bool ignored2;
			return this->IsDark(darkPixelThreshold, darkPixelRatio, ignored1, ignored2);
		}

		/// <summary>
		/// Find percentage of pixels whose brightness is below threshold and classify image accordingly.
		/// </summary>
		// 8MP average performance (n ~= 200), milliseconds
		// dark pixel skip  C++/CLI  C++  SSE4.1  SSE4.1 VEX  AVX2
		// 10               7.1      6.6  3.9     3.7
		//  8                        13   4.1     4.0         4.2
		//  4                        18   4.4     4.3         5.2
		//  2                                     5.8         7.1
		//  1                                     7.4
		// Scalar performance is primarily instruction bound while SIMD performance is L3 bound with VEX approaching 50% superqueue bound, hence the different
		// scaling characteristics.  For pixel skips less than 16 every cache line in the image has to be fetched so memory access cost is essentially skip
		// invariant and compute increases occur primarily during load latency.  However, dark and grey estimates are weak functions of sampling density so
		// there's minimal benefit to testing more pixels.  AVX2 averages slower than VEX encoded SSE 4.1, consistent with Intel's guidance for memory (rather 
		// than compute) bound operations and loss of turbo from ymm register use, so is disabled from CPU dispatch.  AVX2's mode is faster but its average is 
		// worse over any substantial number of files due to greater likelihood of slow iterations.
		bool MemoryImage::IsDark(unsigned __int8 darkPixelThreshold, double darkPixelRatioThreshold, [Out] double% darkPixelFraction, [Out] bool% isColor)
		{
			if ((darkPixelRatioThreshold < 0.0) || (darkPixelRatioThreshold > 1.0))
			{
				throw gcnew ArgumentOutOfRangeException("darkPixelRatioThreshold", String::Format("Ratio threshold for declaring image dark {0:0.###} is not between 0 and 1, inclusive.", darkPixelRatioThreshold));
			}
			// require alpha channel be set to a constant value; see remarks in NativeImage::IsDarkSse41()
			if ((this->nativeImage->Format() != NativeImage::PreferredPixelFormat) ||
				(this->nativeImage->PixelSizeInBytes() != NativeImage::CalculationPixelSizeInBytes))
			{
				throw gcnew NotSupportedException(String::Format("Unhandled image format {0} or pixel size {1} bytes.", this->Format, this->nativeImage->PixelSizeInBytes()));
			}

			double darkPixelFractionNative;
			bool isColorNative;
			bool isDark = this->nativeImage->IsDark(darkPixelThreshold, darkPixelRatioThreshold, &darkPixelFractionNative, &isColorNative);
			darkPixelFraction = darkPixelFractionNative;
			isColor = isColorNative;
			return isDark;
		}

		bool MemoryImage::MismatchedOrNot32BitBgra(MemoryImage^ other)
		{
			if ((this->PixelWidth != other->PixelWidth) ||
				(this->PixelHeight != other->PixelHeight) ||
				(this->nativeImage->Format() != NativeImage::PreferredPixelFormat) ||
				(other->nativeImage->Format() != NativeImage::PreferredPixelFormat) ||
				(this->nativeImage->PixelSizeInBytes() != other->nativeImage->PixelSizeInBytes()))
			{
				return true;
			}
			return false;
		}

		// 8MP average performance (n ~= 40): 5.6ms
		// Not worth running in parallel.
		void MemoryImage::SetSource(Image^ image)
		{
			//Stopwatch^ stopwatch = gcnew Stopwatch();
			//stopwatch->Start();
			WriteableBitmap^ writeableBitmap = dynamic_cast<WriteableBitmap^>(image->Source);
			if ((writeableBitmap == nullptr) ||
				(writeableBitmap->PixelHeight != this->PixelHeight) ||
				(writeableBitmap->PixelWidth != this->PixelWidth) ||
				(writeableBitmap->Format != this->Format))
			{
				writeableBitmap = gcnew WriteableBitmap(this->PixelWidth, this->PixelHeight, MemoryImage::DefaultDpi, MemoryImage::DefaultDpi, this->Format, nullptr);
				image->Source = writeableBitmap;
			}

			// it appears the IntPtr in writeableBitmap->WritePixels(Int32Rect, IntPtr, int, int) must point to managed memory; work around via back buffer
			writeableBitmap->Lock();
			unsigned __int8* backBuffer = reinterpret_cast<unsigned __int8*>(writeableBitmap->BackBuffer.ToPointer());
			this->nativeImage->CopyPixelsTo(backBuffer);
			writeableBitmap->AddDirtyRect(Int32Rect(0, 0, this->PixelWidth, this->PixelHeight));
			writeableBitmap->Unlock();
			//stopwatch->Stop();
			//Debug::WriteLine(stopwatch->Elapsed.ToString("s\\.fffffff"));
		}

		/// <summary>
		/// Get the sum of absolute differences between two images.
		/// </summary>
		bool MemoryImage::TryDifference(MemoryImage^ other, unsigned __int8 threshold, [Out] MemoryImage^% difference)
		{
			if (this->MismatchedOrNot32BitBgra(other))
			{
				difference = nullptr;
				return false;
			}

			//Stopwatch^ stopwatch = gcnew Stopwatch();
			//stopwatch->Start();
			difference = gcnew MemoryImage(this->PixelWidth, this->PixelHeight, this->Format);
			this->nativeImage->Difference(other->nativeImage, threshold, difference->nativeImage);
			//stopwatch->Stop();
			//Debug::WriteLine(stopwatch->Elapsed.ToString("s\\.fffffff"));
			return true;
		}

		/// <summary>
		/// Get the sum of absolute differences between three images.
		/// </summary>
		bool MemoryImage::TryDifference(MemoryImage^ previous, MemoryImage^ next, unsigned __int8 threshold, [Out] MemoryImage^% difference)
		{
			if (this->MismatchedOrNot32BitBgra(previous) ||
				this->MismatchedOrNot32BitBgra(next))
			{
				difference = nullptr;
				return false;
			}

			//Stopwatch^ stopwatch = gcnew Stopwatch();
			//stopwatch->Start();
			difference = gcnew MemoryImage(this->PixelWidth, this->PixelHeight, MemoryImage::PreferredPixelFormat);
			this->nativeImage->Difference(previous->nativeImage, next->nativeImage, threshold, difference->nativeImage);
			//stopwatch->Stop();
			//Debug::WriteLine(stopwatch->Elapsed.ToString("s\\.fffffff"));
			return true;
		}
	}
}