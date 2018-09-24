// adapted from https://msdn.microsoft.com/en-us/library/hskdteyh.aspx
#pragma once

namespace Carnassial
{
	namespace Native
	{
		class NativeProcessor
		{
		private:
			class ProcessorProperties
			{
			public:
				ProcessorProperties();

				__int32 error;
				__int32 function1_Ecx;
				__int32 function7_Ebx;
				__int32 physicalCores;
			};

			static const ProcessorProperties CpuInfo;

		public:
			static bool Avx();
			static bool Avx2();
			static __int32 Error();
			static __int32 PhysicalCores();
			static bool Sse41();
		};
	}
}