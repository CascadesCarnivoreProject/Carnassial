#include "StdafxClr.h"
#include <CodeAnalysis/sourceannotations.h>
#include <Windows.h>
#include "Processor.h"
#include "NativeProcessor.h"

using namespace System;
using namespace System::ComponentModel;
using namespace System::Diagnostics::CodeAnalysis;

namespace Carnassial
{
	namespace Native
	{
		[SuppressMessage("Microsoft.Design", "CA1065")]
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
