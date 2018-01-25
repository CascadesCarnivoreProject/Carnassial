#include "StdafxClr.h"
#include "Processor.h"
#include "NativeProcessor.h"

using namespace System;

namespace Carnassial
{
	namespace Native
	{
		static Processor::Processor()
		{
			NativeProcessor nativeProcessor = NativeProcessor();
			Processor::Hyperthreaded = Environment::ProcessorCount == 2 * nativeProcessor.PhysicalCores();
			Processor::PhysicalCoreCount = NativeProcessor::PhysicalCores();
		}
	}
}
