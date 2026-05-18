using System.Runtime.InteropServices;

namespace Sublingual.Interop.macOS;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void AudioBufferCallback(
    IntPtr samples,
    int frameCount,
    int channels,
    double timestamp,
    IntPtr context
);
