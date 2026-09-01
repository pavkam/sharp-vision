// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

using System.Buffers.Binary;

#pragma warning disable SYSLIB1054 // Runtime-bound imports keep this native boundary non-partial and explicit.

/// <summary>Provides the Unix terminal-size native boundary.</summary>
internal static class RuntimeInterop
{
    private const nuint _linuxGetSize = 0x5413;
    private const int _setAttributesFlush = 2;
    private const int _terminalNameBufferLength = 4096;

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
    [DllImport("libSystem.Native", EntryPoint = "SystemNative_GetWindowSize", ExactSpelling = true, SetLastError = true)]
    private static extern int GetWindowSize(int fileDescriptor, out WindowSize value);

    [DllImport("libc", EntryPoint = "ioctl", ExactSpelling = true, SetLastError = true)]
    private static extern int Ioctl(int fileDescriptor, nuint request, nint value);

    /// <summary>The POSIX standard-output file descriptor, used when no more specific terminal
    /// descriptor (such as an opened /dev/tty) is available for description resolution.</summary>
    public const int StandardOutputFileDescriptor = 1;

    /// <summary>The POSIX standard-error file descriptor.</summary>
    public const int StandardErrorFileDescriptor = 2;

    /// <summary>The POSIX standard-input file descriptor, the raw-mode target the caller's shell
    /// wired to this process's controlling terminal.</summary>
    public const int StandardInputFileDescriptor = 0;

    /// <summary>Determines whether two descriptors resolve to the same Unix terminal device.</summary>
    /// <param name="first">The first non-negative descriptor.</param>
    /// <param name="second">The second non-negative descriptor.</param>
    /// <returns>True only when both descriptors have the same POSIX terminal path or identify the
    /// controlling terminal of the same session.</returns>
    public static bool TerminalDevicesMatch(int first, int second)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(first);
        ArgumentOutOfRangeException.ThrowIfNegative(second);

        Span<byte> firstName = stackalloc byte[_terminalNameBufferLength];
        Span<byte> secondName = stackalloc byte[_terminalNameBufferLength];
        var firstLength = ReadTerminalName(first, firstName);
        var secondLength = ReadTerminalName(second, secondName);

        return firstLength >= 0 &&
               secondLength >= 0 &&
               TerminalIdentitiesMatch(
            firstName[..firstLength],
            GetTerminalSessionId(first),
            secondName[..secondLength],
            GetTerminalSessionId(second));
    }

    /// <summary>Determines whether terminal paths identify the same device directly or as aliases
    /// for the controlling terminal of one POSIX session. This seam proves that <c>/dev/tty</c>
    /// remains equivalent when <c>ttyname_r</c> reports its literal alias.</summary>
    /// <param name="firstPath">The first terminal path without a null terminator.</param>
    /// <param name="firstSessionId">The first terminal's controlling-session identifier, or a
    /// negative value when the descriptor is not a controlling terminal.</param>
    /// <param name="secondPath">The second terminal path without a null terminator.</param>
    /// <param name="secondSessionId">The second terminal's controlling-session identifier, or a
    /// negative value when the descriptor is not a controlling terminal.</param>
    /// <returns>True when the paths match or both terminals belong to the same controlling
    /// session.</returns>
    internal static bool TerminalIdentitiesMatch(
        ReadOnlySpan<byte> firstPath,
        int firstSessionId,
        ReadOnlySpan<byte> secondPath,
        int secondSessionId) =>
        firstPath.SequenceEqual(secondPath) ||
        (firstSessionId >= 0 && firstSessionId == secondSessionId);

    private static unsafe int ReadTerminalName(int fileDescriptor, Span<byte> destination)
    {
        fixed (byte* pointer = destination)
        {
            if (TtyName(fileDescriptor, pointer, (nuint) destination.Length) != 0)
            {
                return -1;
            }
        }

        return destination.IndexOf((byte) 0);
    }

    [DllImport("libc", EntryPoint = "ttyname_r", ExactSpelling = true)]
    private static extern unsafe int TtyName(int fileDescriptor, byte* destination, nuint length);

    [DllImport("libc", EntryPoint = "tcgetsid", ExactSpelling = true)]
    private static extern int GetTerminalSessionId(int fileDescriptor);

    // TCSANOW: apply attribute changes immediately (identical value on Linux and Darwin).
    private const int _setAttributesNow = 0;

    // Layout constants for struct termios. Only the ISIG bit inside c_lflag is ever inspected or
    // mutated; every other byte is captured and replayed as an opaque blob, so the full per-field
    // layout never needs modeling. Sourced from Apple's <sys/termios.h> (Darwin is
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

    /// <summary>
    /// Restores captured termios state after output drains and discards input received but not
    /// read, preventing an incomplete terminal report from reaching the resumed shell.
    /// </summary>
    /// <param name="fileDescriptor">The non-negative terminal descriptor.</param>
    /// <param name="state">The platform-sized captured state, unmodified since capture.</param>
    /// <returns>True when the pending input was flushed and the state was applied.</returns>
    public static unsafe bool TryRestoreTerminalAttributes(int fileDescriptor, byte[] state)
    {
        fixed (byte* pointer = state)
        {
            return TcSetAttr(fileDescriptor, _setAttributesFlush, pointer) == 0;
        }
    }

    /// <summary>Derives the raw, no-echo termios state from a captured state without mutating it.</summary>
    /// <param name="captured">The previously captured termios state.</param>
    /// <param name="captureControlKeys">
    /// Whether Ctrl-key combinations should be delivered as input bytes instead of raising signals.
    /// </param>
    /// <returns>A new raw-mode termios buffer of the same platform size.</returns>
    [Pure]
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

    [DllImport("libc", EntryPoint = "tcgetattr", ExactSpelling = true, SetLastError = true)]
    private static extern unsafe int TcGetAttr(int fileDescriptor, byte* termios);

    [DllImport("libc", EntryPoint = "tcsetattr", ExactSpelling = true, SetLastError = true)]
    private static extern unsafe int TcSetAttr(int fileDescriptor, int optionalActions, byte* termios);

    [DllImport("libc", EntryPoint = "cfmakeraw", ExactSpelling = true)]
    private static extern unsafe void CfMakeRaw(byte* termios);

    // Windows console-mode boundary. Bit-math is factored out so it is unit
    // testable without a real console handle.
    public const int StdInputHandle = -10;
    public const int StdOutputHandle = -11;

    public const uint EnableProcessedInput = 0x0001;
    public const uint EnableLineInput = 0x0002;
    public const uint EnableMouseInput = 0x0010;
    public const uint EnableQuickEditMode = 0x0040;
    public const uint EnableExtendedFlags = 0x0080;
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
    /// <param name="enableMouseInput">Whether mouse tracking is negotiated for this run.</param>
    /// <returns>
    /// The mode enabling VT input without canonical line editing, echo, or classic QuickEdit
    /// selection, with mouse input enabled when requested.
    /// </returns>
    [Pure]
    public static uint ComputeInputMode(uint current, bool captureControlKeys, bool enableMouseInput)
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

        // QuickEdit selection freezes the console on a stray click or drag, which the process
        // cannot detect or recover from; ENABLE_EXTENDED_FLAGS must be set in the same call for a
        // QuickEdit change to take effect at all (Win32 otherwise ignores the QuickEdit bit).
        mode &= ~EnableQuickEditMode;
        mode |= EnableExtendedFlags;

        if (enableMouseInput)
        {
            mode |= EnableMouseInput;
        }

        return mode;
    }

    /// <summary>Computes the wrapping VT-processing console output mode from the saved mode.</summary>
    /// <param name="current">The saved console output mode.</param>
    /// <returns>The mode enabling processed output, wrapping, VT processing, and delayed newline auto-return.</returns>
    [Pure]
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

    [DllImport("kernel32", EntryPoint = "GetStdHandle", ExactSpelling = true, SetLastError = true)]
    private static extern nint GetStdHandle(int which);

    [DllImport("kernel32", EntryPoint = "GetConsoleMode", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetConsoleMode(nint handle, out uint mode);

    [DllImport("kernel32", EntryPoint = "SetConsoleMode", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetConsoleMode(nint handle, uint mode);

    /// <summary>Aborts every pending synchronous read or write issued against a handle, by any
    /// thread, so a blocking native call on that handle returns instead of waiting indefinitely.</summary>
    /// <param name="handle">The console handle with a pending operation to abort.</param>
    /// <returns>True when the abort request was accepted.</returns>
    [SupportedOSPlatform("windows")]
    public static bool TryCancelPendingIo(nint handle) => CancelIoEx(handle, nint.Zero);

    [DllImport("kernel32", EntryPoint = "CancelIoEx", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CancelIoEx(nint handle, nint overlapped);
}

#pragma warning restore SYSLIB1054
