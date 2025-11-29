// from turbojpeg.h
using System;

namespace Carnassial.Interop
{
    internal enum LibjpegPixelFormat : Int16
    {
        TJPF_RGB,
        TJPF_BGR,
        TJPF_RGBX,
        TJPF_BGRX,
        TJPF_XBGR,
        TJPF_XRGB,
        TJPF_GRAY,
        TJPF_RGBA,
        TJPF_BGRA,
        TJPF_ABGR,
        TJPF_ARGB,
        TJPF_CMYK,
        TJPF_UNKNOWN = -1
    }
}
