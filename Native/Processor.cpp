#include "StdafxClr.h"
#include <Windows.h>
#include "Processor.h"
#include "NativeProcessor.h"

using namespace System;
using namespace System::ComponentModel;

namespace Carnassial
{
	namespace Native
	{
		static Processor::Processor()
		{
			NativeProcessor nativeProcessor = NativeProcessor();
			__int32 error = nativeProcessor.Error();
			if (error != ERROR_SUCCESS)
			{
				throw gcnew Win32Exception(error);
			}
			Processor::Hyperthreaded = Environment::ProcessorCount == 2 * nativeProcessor.PhysicalCores();
			Processor::PhysicalCoreCount = NativeProcessor::PhysicalCores();
		}
	}
}
