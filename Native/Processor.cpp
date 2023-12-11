#include "PchClr.h"
#include <Windows.h>
#include "Processor.h"

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
			// find number of physical cores
			__int32 informationFields = 32;
			PSYSTEM_LOGICAL_PROCESSOR_INFORMATION processorInformation = new SYSTEM_LOGICAL_PROCESSOR_INFORMATION[informationFields];
			DWORD length = informationFields * sizeof(SYSTEM_LOGICAL_PROCESSOR_INFORMATION);
			bool success = GetLogicalProcessorInformation(processorInformation, &length);
			if (success == false)
			{
				DWORD error = GetLastError();
				if (error == ERROR_INSUFFICIENT_BUFFER)
				{
					delete[] processorInformation;
					informationFields = length / sizeof(SYSTEM_LOGICAL_PROCESSOR_INFORMATION);
					processorInformation = new SYSTEM_LOGICAL_PROCESSOR_INFORMATION[informationFields];
					length = informationFields * sizeof(SYSTEM_LOGICAL_PROCESSOR_INFORMATION);
					success = GetLogicalProcessorInformation(processorInformation, &length);
					if (success == false)
					{
						throw gcnew Win32Exception(GetLastError());
					}
				}
				else
				{
					throw gcnew Win32Exception(GetLastError());
				}
			}
			informationFields = length / sizeof(SYSTEM_LOGICAL_PROCESSOR_INFORMATION);

			for (__int32 fieldIndex = 0; fieldIndex < informationFields; ++fieldIndex)
			{
				SYSTEM_LOGICAL_PROCESSOR_INFORMATION field = processorInformation[fieldIndex];
				if (field.Relationship == LOGICAL_PROCESSOR_RELATIONSHIP::RelationProcessorCore)
				{
					++Processor::CoreCount;
				}
			}
			delete[] processorInformation;
		}
	}
}
