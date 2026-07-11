using System.ComponentModel;
using System.Runtime.InteropServices;

using SharpVision.Terminal.Geometry;

namespace SharpVision.Terminal.Runtime;

/// <summary>Provides the Unix terminal-size native boundary.</summary>
internal static partial class Native
{
    private const nuint _linuxGetSize = 0x5413;

    /// <summary>Reads cell and pixel dimensions from one terminal file descriptor.</summary>
    /// <param name="fileDescriptor">The non-negative terminal descriptor.</param>
    /// <returns>The current dimensions.</returns>
    /// <exception cref="IOException">The descriptor cannot provide terminal dimensions.</exception>
    internal static unsafe Dimensions GetDimensions(int fileDescriptor)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(fileDescriptor);
        var value = default(WindowSize);
        var result = OperatingSystem.IsMacOS()
            ? GetWindowSize(fileDescriptor, out value)
            : OperatingSystem.IsLinux()
                ? Ioctl(fileDescriptor, _linuxGetSize, (nint) (&value))
                : throw new PlatformNotSupportedException(
                    "Unix terminal resize is supported only on Linux and macOS.");

        if (result != 0)
        {
            throw new IOException(
                "The terminal dimensions could not be read.",
                new Win32Exception(Marshal.GetLastPInvokeError()));
        }

        var cells = new Size(value.Columns, value.Rows);
        Size? pixels = value.PixelWidth > 0 && value.PixelHeight > 0
            ? new Size(value.PixelWidth, value.PixelHeight)
            : null;
        return new Dimensions(cells, pixels);
    }

    // Darwin ARM64 gives variadic arguments a different ABI, so raw ioctl
    // cannot be declared as a fixed managed signature. The .NET runtime's
    // fixed native shim is the safe boundary on macOS.
    [LibraryImport("libSystem.Native", EntryPoint = "SystemNative_GetWindowSize", SetLastError = true)]
    private static partial int GetWindowSize(int fileDescriptor, out WindowSize value);

    [LibraryImport("libc", EntryPoint = "ioctl", SetLastError = true)]
    private static partial int Ioctl(int fileDescriptor, nuint request, nint value);

}
