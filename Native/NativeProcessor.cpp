#include "Stdafx.h"
#include <array>  
#include <bitset>
#include <intrin.h>
#include <vector>  
#include <Windows.h>
#include "NativeProcessor.h"

namespace Carnassial
{
	namespace Native
	{
		const NativeProcessor::ProcessorProperties NativeProcessor::CpuInfo;

#pragma warning(disable : 4793)
		NativeProcessor::ProcessorProperties::ProcessorProperties() :
			function1_Ecx{ 0 },
			function7_Ebx{ 0 }
		{
			// calling __cpuid with 0x0 as the function_id argument gets the number of the highest valid function ID
			std::array<int, 4> cpuIdentifiers;
			__cpuid(cpuIdentifiers.data(), 0);
			int identifiers = cpuIdentifiers[0];

			std::vector<std::array<int, 4>> cpuIdentifiersExtended;
			std::vector<std::array<int, 4>> extdata_;
			for (int identifier = 0; identifier <= identifiers; ++identifier)
			{
				__cpuidex(cpuIdentifiers.data(), identifier, 0);
				cpuIdentifiersExtended.push_back(cpuIdentifiers);
			}

			// load bitset with flags for function 0x00000001  
			if (identifiers >= 1)
			{
				this->function1_Ecx = cpuIdentifiersExtended[1][2];
			}

			// load bitset with flags for function 0x00000007  
			if (identifiers >= 7)
			{
				this->function7_Ebx = cpuIdentifiersExtended[7][1];
			}

			// find number of physical cores
			__int32 informationFields = 16;
			PSYSTEM_LOGICAL_PROCESSOR_INFORMATION processorInformation = new SYSTEM_LOGICAL_PROCESSOR_INFORMATION[informationFields];
			DWORD length = informationFields * sizeof(SYSTEM_LOGICAL_PROCESSOR_INFORMATION);
			bool success = GetLogicalProcessorInformation(processorInformation, &length);
			if (success == false)
			{
				int error = GetLastError();
				if (error == ERROR_INSUFFICIENT_BUFFER)
				{
					delete[] processorInformation;
					informationFields = length / sizeof(SYSTEM_LOGICAL_PROCESSOR_INFORMATION);
					processorInformation = new SYSTEM_LOGICAL_PROCESSOR_INFORMATION[informationFields];
					length = informationFields * sizeof(SYSTEM_LOGICAL_PROCESSOR_INFORMATION);
					success = GetLogicalProcessorInformation(processorInformation, &length);
					if (success == false)
					{
						throw ProcessorProperties::GetWindowsError();
					}
				}
				else
				{
					throw ProcessorProperties::GetWindowsError(error);
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

		std::runtime_error NativeProcessor::ProcessorProperties::GetWindowsError()
		{
			return ProcessorProperties::GetWindowsError(GetLastError());
		}

		std::runtime_error NativeProcessor::ProcessorProperties::GetWindowsError(unsigned long errorCode)
		{
			LPSTR messageBuffer = nullptr;
			DWORD size = FormatMessageA(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS, NULL, errorCode, MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT), (LPSTR)&messageBuffer, 0, NULL);
			std::string message(messageBuffer, size);
			LocalFree(messageBuffer);
			return std::runtime_error(message);
		}

		bool NativeProcessor::Avx()
		{
			return CpuInfo.function1_Ecx[28];
		}

		bool NativeProcessor::Avx2()
		{
			return CpuInfo.function7_Ebx[5];
		}

		__int32 NativeProcessor::PhysicalCores()
		{
			return CpuInfo.physicalCores;
		}

		bool NativeProcessor::Sse41()
		{
			return CpuInfo.function1_Ecx[19];
		}
	}
}