#pragma once

namespace Carnassial
{
	namespace Native
	{
		public ref class Processor
		{
		private:
			static bool Hyperthreaded;
			static __int32 PhysicalCoreCount;

			static Processor();

		public:
			static property bool IsHyperthreaded
			{
				bool get() { return Processor::Hyperthreaded; }
			}

			static property __int32 PhysicalCores
			{
				__int32 get() { return Processor::PhysicalCoreCount; }
			}
		};
	}
}

