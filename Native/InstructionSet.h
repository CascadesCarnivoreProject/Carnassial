// adapted from https://msdn.microsoft.com/en-us/library/hskdteyh.aspx
#pragma once
#include <bitset>

namespace Carnassial
{
	namespace Native
	{
		class InstructionSet
		{
		private:
			class CpuInfo
			{
			public:
				CpuInfo();

				std::bitset<32> function1_Ecx;
				std::bitset<32> function7_Ebx;
			};

			static const CpuInfo Cpu;

		public:
			static bool Avx();
			static bool Avx2();
			static bool Sse41();
		};
	}
}