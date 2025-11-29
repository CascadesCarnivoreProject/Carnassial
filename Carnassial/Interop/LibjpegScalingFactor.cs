// turbojpeg.h
using System.Runtime.InteropServices;

namespace Carnassial.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct LibjpegScalingFactor
    {
        public int num;
        public int denom;
    }
}
