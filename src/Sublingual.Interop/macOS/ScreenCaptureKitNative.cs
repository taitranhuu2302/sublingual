using System.Reflection;
using System.Runtime.InteropServices;

namespace Sublingual.Interop.macOS;

public static class ScreenCaptureKitNative
{
    private const string LibraryName = "ScreenCaptureKitBridge";
    private static IntPtr _libraryHandle;
    private static bool _resolverConfigured;

    public static void ConfigureLibraryPath(string libraryPath)
    {
        if (string.IsNullOrWhiteSpace(libraryPath))
        {
            throw new ArgumentException("Library path must not be empty.", nameof(libraryPath));
        }

        if (!_resolverConfigured)
        {
            NativeLibrary.SetDllImportResolver(typeof(ScreenCaptureKitNative).Assembly, ResolveLibrary);
            _resolverConfigured = true;
        }

        if (_libraryHandle == IntPtr.Zero)
        {
            _libraryHandle = NativeLibrary.Load(libraryPath);
        }
    }

    [DllImport(LibraryName, EntryPoint = "sc_create_session", CallingConvention = CallingConvention.Cdecl)]
    public static extern int CreateSession(AudioBufferCallback callback, IntPtr context);

    [DllImport(LibraryName, EntryPoint = "sc_start_capture", CallingConvention = CallingConvention.Cdecl)]
    public static extern int StartCapture();

    [DllImport(LibraryName, EntryPoint = "sc_stop_capture", CallingConvention = CallingConvention.Cdecl)]
    public static extern int StopCapture();

    [DllImport(LibraryName, EntryPoint = "sc_destroy_session", CallingConvention = CallingConvention.Cdecl)]
    public static extern int DestroySession();

    [DllImport(LibraryName, EntryPoint = "sc_get_last_error_message", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr GetLastErrorMessageNative();

    public static string GetLastErrorMessage()
    {
        var pointer = GetLastErrorMessageNative();
        return pointer == IntPtr.Zero
            ? "Unknown native ScreenCaptureKit error."
            : Marshal.PtrToStringAnsi(pointer) ?? "Unknown native ScreenCaptureKit error.";
    }

    private static IntPtr ResolveLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!string.Equals(libraryName, LibraryName, StringComparison.Ordinal) || _libraryHandle == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        return _libraryHandle;
    }
}
