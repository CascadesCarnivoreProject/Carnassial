using Carnassial.Interop;

namespace Carnassial.Util
{
    internal static class Processor
    {
        public static int PhysicalCores { get; private set; }

        static Processor() 
        { 
            Processor.PhysicalCores = NativeMethods.GetPhysicalCoreCount();
        }
    }
}
