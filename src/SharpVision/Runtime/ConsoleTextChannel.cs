// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Runtime;

using Microsoft.Win32.SafeHandles;

/// <summary>Writes host diagnostic and status lines without ever touching <see cref="Console"/>.</summary>
/// <remarks>
/// On Unix, the first write through <see cref="Console.Out"/> or <see cref="Console.Error"/>
/// initializes the BCL's Unix console, which emits <c>smkx</c> (application keypad mode) and
/// leaves the runtime re-emitting it on every later child-process exit - including this host's
/// own restore-lease teardown. Writing through a raw, unowned <see cref="FileStream"/> over
/// standard output or standard error never triggers that initialization (see #254). Windows has
/// no equivalent side effect, so it keeps using <see cref="Console"/> directly.
/// </remarks>
internal static class ConsoleTextChannel
{
    // POSIX standard-output and standard-error file descriptors. Not read from
    // SharpVision.Terminal's RuntimeInterop: that type is internal to its own assembly, and this
    // boundary is stable enough not to need a cross-assembly seam for two well-known constants.
    private const int _standardOutputFileDescriptor = 1;
    private const int _standardErrorFileDescriptor = 2;

    /// <summary>Writes one line to standard output.</summary>
    /// <param name="text">The non-null line, without a trailing newline.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public static void WriteLine(string text) => WriteLine(_standardOutputFileDescriptor, text);

    /// <summary>Writes one line to standard error.</summary>
    /// <param name="text">The non-null line, without a trailing newline.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public static void WriteErrorLine(string text) => WriteLine(_standardErrorFileDescriptor, text);

    /// <summary>
    /// Writes one line straight to an arbitrary Unix file descriptor via a raw, unowned
    /// <see cref="FileStream"/>, bypassing the Windows/<see cref="Console"/> fallback entirely.
    /// Internal so a test can point this at a pseudoterminal descriptor and observe the exact
    /// bytes written, which the real fd 1/2 entry points cannot safely do inside a shared test
    /// process.
    /// </summary>
    /// <param name="fileDescriptor">The non-negative Unix file descriptor to write to.</param>
    /// <param name="text">The non-null line, without a trailing newline.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="PlatformNotSupportedException">The current platform is Windows.</exception>
    internal static void WriteRawLine(int fileDescriptor, string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Raw file-descriptor writes are Unix-only.");
        }

        using var handle = new SafeFileHandle(fileDescriptor, ownsHandle: false);
        using var stream = new FileStream(handle, FileAccess.Write);
        using var writer = new StreamWriter(stream) { AutoFlush = true };
        writer.WriteLine(text);
    }

    private static void WriteLine(int unixFileDescriptor, string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (OperatingSystem.IsWindows())
        {
            var consoleWriter = unixFileDescriptor == _standardErrorFileDescriptor ? Console.Error : Console.Out;
            consoleWriter.WriteLine(text);
            return;
        }

        WriteRawLine(unixFileDescriptor, text);
    }
}
