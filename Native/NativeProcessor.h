// adapted from https://msdn.microsoft.com/en-us/library/hskdteyh.aspx
#pragma once
#include <bitset>

namespace Carnassial
{
	namespace Native
	{
		class NativeProcessor
		{
		private:
			class ProcessorProperties
			{
			private:
				std::runtime_error GetWindowsError();
				std::runtime_error GetWindowsError(unsigned long errorCode);

			public:
				ProcessorProperties();

				std::bitset<32> function1_Ecx;
				std::bitset<32> function7_Ebx;
				__int32 physicalCores;
			};

			static const ProcessorProperties CpuInfo;

		public:
			static bool Avx();
			static bool Avx2();
			static __int32 PhysicalCores();
			static bool Sse41();
		};
	}
}