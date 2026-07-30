// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

/// <summary>Provides the Unix terminal-size native boundary.</summary>
internal static partial class Native
{
    private const nuint _linuxGetSize = 0x5413;

    /// <summary>Reads cell and pixel dimensions from one terminal file descriptor.</summary>
    /// <param name="fileDescriptor">The non-negative terminal descriptor.</param>
    /// <returns>The current dimensions.</returns>
    /// <exception cref="IOException">The descriptor cannot provide terminal dimensions.</exception>
    public static unsafe Dimensions GetDimensions(int fileDescriptor)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(fileDescriptor);
        WindowSize value = default;
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
        Size? pixels = value is { PixelWidth: > 0, PixelHeight: > 0 }
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

    /// <summary>The POSIX standard-output file descriptor, used when no more specific terminal
    /// descriptor (such as an opened /dev/tty) is available for description resolution.</summary>
    public const int StandardOutputFileDescriptor = 1;

    // Windows console-mode boundary. Bit-math is factored out so it is unit
    // testable without a real console handle.
    public const int StdInputHandle = -10;
    public const int StdOutputHandle = -11;

    public const uint EnableProcessedInput = 0x0001;
    public const uint EnableLineInput = 0x0002;
    public const uint EnableEchoInput = 0x0004;
    public const uint EnableVirtualTerminalInput = 0x0200;
    public const uint EnableProcessedOutput = 0x0001;

    /// <summary>Enables automatic cursor wrapping after output reaches the final column.</summary>
    public const uint EnableWrapAtEolOutput = 0x0002;

    public const uint EnableVirtualTerminalProcessing = 0x0004;
    public const uint DisableNewlineAutoReturn = 0x0008;

    /// <summary>Computes the raw-input console mode from the saved mode.</summary>
    /// <param name="current">The saved console input mode.</param>
    /// <param name="captureControlKeys">Whether Ctrl+C is delivered as input.</param>
    /// <returns>The mode enabling VT input without canonical line editing or echo.</returns>
    public static uint ComputeInputMode(uint current, bool captureControlKeys)
    {
        var mode = current;
        mode &= ~(EnableLineInput | EnableEchoInput);
        mode |= EnableVirtualTerminalInput;

        if (captureControlKeys)
        {
            mode &= ~EnableProcessedInput;
        }
        else
        {
            mode |= EnableProcessedInput;
        }

        return mode;
    }

    /// <summary>Computes the wrapping VT-processing console output mode from the saved mode.</summary>
    /// <param name="current">The saved console output mode.</param>
    /// <returns>The mode enabling processed output, wrapping, VT processing, and delayed newline auto-return.</returns>
    public static uint ComputeOutputMode(uint current) =>
        current |
        EnableProcessedOutput |
        EnableWrapAtEolOutput |
        EnableVirtualTerminalProcessing |
        DisableNewlineAutoReturn;

    /// <summary>Gets a standard console handle.</summary>
    /// <param name="which">The <see cref="StdInputHandle"/> or <see cref="StdOutputHandle"/> id.</param>
    /// <returns>The native handle.</returns>
    [SupportedOSPlatform("windows")]
    public static nint GetStandardHandle(int which) => GetStdHandle(which);

    /// <summary>Reads a console mode.</summary>
    /// <param name="handle">The console handle.</param>
    /// <param name="mode">Receives the current mode on success.</param>
    /// <returns>True when the mode was read.</returns>
    [SupportedOSPlatform("windows")]
    public static bool TryGetConsoleMode(nint handle, out uint mode) => GetConsoleMode(handle, out mode);

    /// <summary>Writes a console mode.</summary>
    /// <param name="handle">The console handle.</param>
    /// <param name="mode">The mode to apply.</param>
    /// <returns>True when the mode was applied.</returns>
    [SupportedOSPlatform("windows")]
    public static bool TrySetConsoleMode(nint handle, uint mode) => SetConsoleMode(handle, mode);

    [LibraryImport("kernel32", EntryPoint = "GetStdHandle", SetLastError = true)]
    private static partial nint GetStdHandle(int which);

    [LibraryImport("kernel32", EntryPoint = "GetConsoleMode", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetConsoleMode(nint handle, out uint mode);

    [LibraryImport("kernel32", EntryPoint = "SetConsoleMode", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetConsoleMode(nint handle, uint mode);
}
