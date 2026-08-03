// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

using System.Buffers.Binary;

/// <summary>Provides the Unix terminal-size native boundary.</summary>
internal static partial class RuntimeInterop
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

    /// <summary>The POSIX standard-error file descriptor.</summary>
    public const int StandardErrorFileDescriptor = 2;

    /// <summary>The POSIX standard-input file descriptor, the raw-mode target the caller's shell
    /// wired to this process's controlling terminal.</summary>
    public const int StandardInputFileDescriptor = 0;

    // TCSANOW: apply attribute changes immediately (identical value on Linux and Darwin).
    private const int _setAttributesNow = 0;

    // Layout constants for struct termios. Only the ISIG bit inside c_lflag is ever inspected or
    // mutated; every other byte is captured and replayed as an opaque blob, so the full per-field
    // layout never needs modeling (see #251). Sourced from Apple's <sys/termios.h> (Darwin is
    // LP64, so tcflag_t is an 8-byte unsigned long; c_iflag, c_oflag, c_cflag, c_lflag precede
    // c_cc/c_ispeed/c_ospeed, giving c_lflag a 24-byte offset and a measured sizeof of 72) and
    // glibc's <bits/termios.h> (tcflag_t is a 4-byte unsigned int, giving c_lflag a 12-byte offset
    // and a struct size of 60 on both x86-64 and arm64). ISIG is bit 0x80 on Darwin (POSIX-set,
    // BSD-numbered) and bit 0x1 on Linux (POSIX-numbered first).
    /// <summary>
    /// Gets the exact byte length of a captured termios state on this platform. Internal so tests
    /// can build a correctly sized synthetic state without ever calling <c>tcgetattr</c>; passing
    /// an undersized buffer into the native boundary would corrupt memory rather than merely fail.
    /// </summary>
    internal static int TermiosStateLength { get; } = OperatingSystem.IsMacOS() ? 72 : 60;

    private static readonly int _localFlagsOffset = OperatingSystem.IsMacOS() ? 24 : 12;
    private static readonly int _localFlagsWidth = OperatingSystem.IsMacOS() ? 8 : 4;
    private static readonly ulong _signalsEnabledFlag = OperatingSystem.IsMacOS() ? 0x0000_0080ul : 0x0000_0001ul;

    /// <summary>Captures the current termios state of a Unix file descriptor as an opaque, platform-sized blob.</summary>
    /// <param name="fileDescriptor">The non-negative terminal descriptor.</param>
    /// <param name="state">Receives the captured state on success; undefined content on failure.</param>
    /// <returns>True when the state was captured.</returns>
    public static unsafe bool TryGetTerminalAttributes(int fileDescriptor, out byte[] state)
    {
        var buffer = new byte[TermiosStateLength];

        int result;

        fixed (byte* pointer = buffer)
        {
            result = TcGetAttr(fileDescriptor, pointer);
        }

        state = buffer;
        return result == 0;
    }

    /// <summary>Replays a previously captured termios state onto a Unix file descriptor.</summary>
    /// <param name="fileDescriptor">The non-negative terminal descriptor.</param>
    /// <param name="state">The platform-sized captured state, unmodified since capture or derivation.</param>
    /// <returns>True when the state was applied.</returns>
    public static unsafe bool TrySetTerminalAttributes(int fileDescriptor, byte[] state)
    {
        fixed (byte* pointer = state)
        {
            return TcSetAttr(fileDescriptor, _setAttributesNow, pointer) == 0;
        }
    }

    /// <summary>Derives the raw, no-echo termios state from a captured state without mutating it.</summary>
    /// <param name="captured">The previously captured termios state.</param>
    /// <param name="captureControlKeys">
    /// Whether Ctrl-key combinations should be delivered as input bytes instead of raising signals.
    /// </param>
    /// <returns>A new raw-mode termios buffer of the same platform size.</returns>
    public static unsafe byte[] ComputeRawTerminalAttributes(byte[] captured, bool captureControlKeys)
    {
        var raw = new byte[captured.Length];
        captured.CopyTo(raw.AsSpan());

        fixed (byte* pointer = raw)
        {
            CfMakeRaw(pointer);
        }

        // cfmakeraw() always clears ISIG. Restore it unless the caller wants Ctrl-key
        // combinations delivered as ordinary input bytes instead of raising signals.
        var flags = ReadLocalFlags(raw);
        flags = captureControlKeys ? flags & ~_signalsEnabledFlag : flags | _signalsEnabledFlag;
        WriteLocalFlags(raw, flags);

        return raw;
    }

    private static ulong ReadLocalFlags(byte[] termios) =>
        _localFlagsWidth == 8
            ? BinaryPrimitives.ReadUInt64LittleEndian(termios.AsSpan(_localFlagsOffset))
            : BinaryPrimitives.ReadUInt32LittleEndian(termios.AsSpan(_localFlagsOffset));

    private static void WriteLocalFlags(byte[] termios, ulong value)
    {
        if (_localFlagsWidth == 8)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(termios.AsSpan(_localFlagsOffset), value);
        }
        else
        {
            BinaryPrimitives.WriteUInt32LittleEndian(termios.AsSpan(_localFlagsOffset), (uint) value);
        }
    }

    [LibraryImport("libc", EntryPoint = "tcgetattr", SetLastError = true)]
    private static unsafe partial int TcGetAttr(int fileDescriptor, byte* termios);

    [LibraryImport("libc", EntryPoint = "tcsetattr", SetLastError = true)]
    private static unsafe partial int TcSetAttr(int fileDescriptor, int optionalActions, byte* termios);

    [LibraryImport("libc", EntryPoint = "cfmakeraw")]
    private static unsafe partial void CfMakeRaw(byte* termios);

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

    /// <summary>Aborts every pending synchronous read or write issued against a handle, by any
    /// thread, so a blocking native call on that handle returns instead of waiting indefinitely.</summary>
    /// <param name="handle">The console handle with a pending operation to abort.</param>
    /// <returns>True when the abort request was accepted.</returns>
    [SupportedOSPlatform("windows")]
    public static bool TryCancelPendingIo(nint handle) => CancelIoEx(handle, nint.Zero);

    [LibraryImport("kernel32", EntryPoint = "CancelIoEx", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CancelIoEx(nint handle, nint overlapped);
}
