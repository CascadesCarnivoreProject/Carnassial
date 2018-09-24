#include "Stdafx.h"
#include <intrin.h>
#include <Windows.h>
#include "NativeProcessor.h"

namespace Carnassial
{
	namespace Native
	{
		const NativeProcessor::ProcessorProperties NativeProcessor::CpuInfo;

		// code adapted from Microsoft's __cpuidex() documentation
		NativeProcessor::ProcessorProperties::ProcessorProperties() :
			function1_Ecx{ 0 },
			function7_Ebx{ 0 }
		{
			// calling __cpuid with 0x0 as the function_id argument gets the number of the highest valid function ID
			__int32 cpuidBuffer[4];
			__cpuid(cpuidBuffer, 0);
			int cpuFunctionIDs = cpuidBuffer[0];

			// load bitset with flags for function 0x00000001  
			if (cpuFunctionIDs >= 1)
			{
				__cpuidex(cpuidBuffer, 1, 0);
				this->function1_Ecx = cpuidBuffer[2];
			}

			// load bitset with flags for function 0x00000007  
			if (cpuFunctionIDs >= 7)
			{
				__cpuidex(cpuidBuffer, 7, 0);
				this->function7_Ebx = cpuidBuffer[1];
			}

			// find number of physical cores
			__int32 informationFields = 16;
			PSYSTEM_LOGICAL_PROCESSOR_INFORMATION processorInformation = new SYSTEM_LOGICAL_PROCESSOR_INFORMATION[informationFields];
			DWORD length = informationFields * sizeof(SYSTEM_LOGICAL_PROCESSOR_INFORMATION);
			bool success = GetLogicalProcessorInformation(processorInformation, &length);
			if (success == false)
			{
				this->error = GetLastError();
				if (this->error == ERROR_INSUFFICIENT_BUFFER)
				{
					delete[] processorInformation;
					informationFields = length / sizeof(SYSTEM_LOGICAL_PROCESSOR_INFORMATION);
					processorInformation = new SYSTEM_LOGICAL_PROCESSOR_INFORMATION[informationFields];
					length = informationFields * sizeof(SYSTEM_LOGICAL_PROCESSOR_INFORMATION);
					success = GetLogicalProcessorInformation(processorInformation, &length);
					if (success == false)
					{
						this->error = GetLastError();
						return;
					}
				}
				else
				{
					return;
				}
			}
			informationFields = length / sizeof(SYSTEM_LOGICAL_PROCESSOR_INFORMATION);

			this->physicalCores = 0;
			for (__int32 fieldIndex = 0; fieldIndex < informationFields; ++fieldIndex)
			{
				SYSTEM_LOGICAL_PROCESSOR_INFORMATION field = processorInformation[fieldIndex];
				if (field.Relationship == LOGICAL_PROCESSOR_RELATIONSHIP::RelationProcessorCore)
				{
					++this->physicalCores;
				}
			}
			delete[] processorInformation;
		}

		bool NativeProcessor::Avx()
		{
			// masks for selecting bits, zero based numbering
			// bit  0 - 0x0001      8 - 0x0100     16 - 0x0001 0000     24 - 0x0100 0000
			//      1 - 0x0002      9 - 0x0200     17 - 0x0002 0000     25 - 0x0200 0000
			//      2 - 0x0004     10 - 0x0400     18 - 0x0004 0000     26 - 0x0400 0000
			//      3 - 0x0008     11 - 0x0800     19 - 0x0008 0000     27 - 0x0800 0000
			//      4 - 0x0010     12 - 0x1000     20 - 0x0010 0000     28 - 0x1000 0000
			//      5 - 0x0020     13 - 0x2000     21 - 0x0020 0000     29 - 0x2000 0000
			//      6 - 0x0040     14 - 0x4000     22 - 0x0040 0000     30 - 0x4000 0000
			//      7 - 0x0080     15 - 0x8000     23 - 0x0080 0000     31 - 0x8000 0000
			// bit 28
			return (CpuInfo.function1_Ecx & 0x10000000) != 0;
		}

		bool NativeProcessor::Avx2()
		{
			// bit 5
			return (CpuInfo.function7_Ebx & 0x00000020) != 0;
		}

		__int32 NativeProcessor::Error()
		{
			return CpuInfo.error;
		}

		__int32 NativeProcessor::PhysicalCores()
		{
			return CpuInfo.physicalCores;
		}

		bool NativeProcessor::Sse41()
		{
			// bit 19
			return (CpuInfo.function1_Ecx & 0x00080000) != 0;
		}
	}
}