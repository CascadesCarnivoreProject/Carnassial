#include "StdafxClr.h"
#include <stdexcept>
#include "MemoryImage.h"
#include "NativeProcessor.h"

using namespace System;
using namespace System::ComponentModel;
using namespace System::Diagnostics;
using namespace System::Threading::Tasks;
using namespace System::Windows;

namespace Carnassial
{
	namespace Native
	{
		static MemoryImage::MemoryImage()
		{
			// as a default, restrict Carnassial's use of the concurrency runtime to the number of physical cores
			// As of Carnassial 2.2.0.3 this is desirable for a number of reasons:
			// - unlike the C# TPL's Parallel class, the C++ concurrency API typically lacks the ability to set per operation policy
			// - Carnassial's C++ and C++/CLI layers are dedicated to image processing and constrained by memory bandwidth
			//   - The memory bottleneck means single threaded operation is most effective for SIMD code paths.  Within the 2.2.0.3
			//     algorithm set, a single thread suffices to reach memory bus capacity and further threads slightly degrade throughput
			//     due to contention.  Such overparalleling results in ineffective active processor cores and often slightly higher
			//     clock rates, expending more power to take longer to get the same results.
			//   - Integer SIMD operations from hyperthread siblings run on the same gates, so dispatching to all logical processors  
			//     somewhat degrades performance as the two threads experience net contention for the same resources on the physical core.
			//   - A corollary of the above is (as of 2.2.0.3) the only parallel operations initiated from Carnasial.Native.dll are in
			//     NativeImage's scalar fallback paths, which are rarely executed as nearly all x64 processors remaining in use have
			//     at least SSE4.1.  These profile slightly better hyperthreaded on the oldest test hardware available, which is 
			//     substantially newer than Penryn.  It appears likely, however, that in the older systems where the scalar paths are
			//     likely to be reached attempting to schedule beyond physical processors would also degrade performance due to memory
			//     bandwidth overpressure.
			// - concurrency's default policy allows practically unbounded threads.  Carnassial has no flows where overprovisioning 
			//   is beneficial so a more safe policy default is to allow, at most, concurrency up to the number of logical processors.
			//   Defaulting to the number of physical cores is a more conservative variant of this.
			NativeImage::SetDefaultConcurrencyPolicy(NativeProcessor::PhysicalCores());
		}

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
			: MemoryImage(jpeg, 0, jpeg->Length, requestedWidth)
		{
		}

		MemoryImage::MemoryImage(array<unsigned __int8>^ jpeg, __int32 offset, __int32 length, Nullable<__int32>^ requestedWidth)
		{
			__int32 requestedWidthNative = (requestedWidth != nullptr) && requestedWidth->HasValue ? requestedWidth->Value : -1;
			bool decodeErrorNative;
			pin_ptr<unsigned __int8> jpegBytes = &jpeg[offset];
			try
			{
				this->nativeImage = new NativeImage(jpegBytes, length, requestedWidthNative, &decodeErrorNative);
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

		/// <summary>
		/// Find average luminosity and coloration of image.
		/// </summary>
		// 2.2.0.3 
		// 2.2.0.2 - low resolution timer
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
		double MemoryImage::GetLuminosityAndColoration(__int32 bottomRowsToSkip, [Out] double% coloration)
		{
			// require alpha channel be set to a constant value; see remarks in NativeImage::IsDarkSse41()
			if ((this->nativeImage->Format() != NativeImage::PreferredPixelFormat) ||
				(this->nativeImage->PixelSizeInBytes() != NativeImage::CalculationPixelSizeInBytes))
			{
				throw gcnew NotSupportedException(String::Format("Unhandled image format {0} or pixel size {1} bytes.", this->Format, this->nativeImage->PixelSizeInBytes()));
			}

			double colorationNative;
			double luminosity = this->nativeImage->GetLuminosityAndColoration(&colorationNative, bottomRowsToSkip);

			coloration = colorationNative;
			return luminosity;
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
			//Trace::WriteLine(stopwatch->Elapsed.ToString("s\\.fffffff", CultureInfo.CurrentCulture));
		}

		bool MemoryImage::TryDecode(array<unsigned __int8>^ jpeg, __int32 offset, __int32 length, Nullable<__int32>^ requestedWidth)
		{
			__int32 requestedWidthNative = (requestedWidth != nullptr) && requestedWidth->HasValue ? requestedWidth->Value : -1;
			bool decodeErrorNative;
			bool success;
			pin_ptr<unsigned __int8> jpegBytes = &jpeg[offset];
			try
			{
				 success = this->nativeImage->TryDecode(jpegBytes, length, requestedWidthNative, &decodeErrorNative);
			}
			catch (const std::runtime_error& turboJpegError)
			{
				throw gcnew ArgumentException(gcnew String(turboJpegError.what()), "jpeg");
			}
			this->decodeError = decodeErrorNative;
			this->format = this->GetPixelFormat();
			return success;
		}

		/// <summary>
		/// Get the sum of absolute differences between two images.
		/// </summary>
		// 2.2.0.3
		// 8MP average performance (n = 20+ @ 10x), release build, i5-4200U, 256k blocks, Visual Studio 2017, Windows 10 Fall Creators Update + Meltdown and Spectre
		//              standalone average ms, mains              standalone average ms, battery
		// threads      1     2     4 (default)                   1     2     4
		// C++ scalar   48.1  40.1  36.1    2.1  2.1  2.1GHz
		// SSE4.1       20.8  22.4  26.1    2.1  2.1  2.1GHz      31.8               1.5GHz
		// SSE4.1 VEX   19.7  20.4  22.0    1.9  2.0  2.0GHz      32.0               1.5
		// AVX2         26.5  21.2  22.3    1.9  1.9  2.1GHz      36.7  33.8  34.0   1.3  1.3  1.5GHz
		// 
		// 2.2.0.2
		// 8MP average performance (n = 30-60), release build, i5-4200U, default threads (4), 16k blocks, Visual Studio 2015, Windows 10
		//                                                  C++    SSE4.1   SSE4.1 VEX  AVX2  VS tracing, mains
		// measured milliseconds                            43.7   13.4     13.4        12.3
		// CPU GHz                                          2.3    2.2      2.2         2.2
		// GHz normalized milliseconds                      42.0   12.1     12.1        11.7
		//                              C# unsafe  C++/CLI  C++    SSE4.1   SSE4.1 VEX  AVX2  VS tracing, battery
		// measured milliseconds        58.7       46.1     28.6   ~15      ~15         ~18
		// CPU GHz                      2.45       2.45     2.0    ~1.7     ~1.5        ~1.0
		// GHz normalized milliseconds  58.7       46.1     23      10       8.8         7.2
		//
		// The more modern the SIMD used the more times vary depending on how much processor upclocks in response to the load.  At a 
		// given core frequency newer is faster but AVX2 upclocking is less aggressive than with SSE4.  The result might be less power 
		// expenditure but certainly more variable and higher average latency.  On Haswell the limiting factor is more the ability of 
		// the rendering system to deliver the difference image as this runs single threaded and imposes more time than the differencing 
		// and overall latencies remain low enough for response to be quite prompt subjectively.  Additionally, the on core speed of 
		// differencing is memory bound but the systems measured don't push much beyond 11GB/s and frequently run below 7.5GB/s.
		bool MemoryImage::TryDifference(MemoryImage^ other, unsigned __int8 threshold, [Out] MemoryImage^% difference)
		{
			if (this->MismatchedOrNot32BitBgra(other))
			{
				difference = nullptr;
				return false;
			}

			difference = gcnew MemoryImage(this->PixelWidth, this->PixelHeight, this->Format);
			this->nativeImage->Difference(other->nativeImage, threshold, difference->nativeImage);
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

			difference = gcnew MemoryImage(this->PixelWidth, this->PixelHeight, MemoryImage::PreferredPixelFormat);
			this->nativeImage->Difference(previous->nativeImage, next->nativeImage, threshold, difference->nativeImage);
			return true;
		}
	}
}