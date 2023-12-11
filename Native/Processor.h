#pragma once

namespace Carnassial
{
	namespace Native
	{
		/// <summary>
		/// Small wrapper over GetLogicalProcessorInformation() to obtain the current processor's number of physical cores.
		/// </summary>
		/// <remarks>
		/// This class could easily be implemented from C# with P/Invoke but, given <see cref="MemoryImageCppCli"/> motivates the inclusion 
		/// of a C++/CLI assembly, there's no particular reason not to implement <see cref="Processor"/> in C++/CLI.
		/// </remarks>
		public ref class Processor
		{
		private:
			static __int32 CoreCount;

			static Processor();

		public:
			static property __int32 PhysicalCores
			{
				__int32 get() { return Processor::CoreCount; }
			}
		};
	}
}

