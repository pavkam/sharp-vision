// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

/// <summary>Writes UTF-8 bytes directly to the raw Windows console output handle.</summary>
/// <remarks>
/// This owns the output handle itself rather than wrapping <see cref="Console.OpenStandardOutput()"/>.
/// .NET's own Windows console stream opens either a code-page-dependent <c>WriteFile</c> path or a
/// narrow <c>WriteConsoleA</c> path depending on the process's console output code page - neither
/// of which is UTF-8 aware, so the renderer's raw UTF-8 bytes are garbled on any console that has
/// not run <c>chcp 65001</c>. Calling <c>WriteConsoleW</c> directly and transcoding the caller's
/// UTF-8 bytes to UTF-16 code units first (the approach Rust's standard library uses for Windows
/// stdio) bypasses the code page entirely. Unlike the input side, a blocking <c>WriteConsoleW</c>
/// call ordinarily returns promptly, so this stream does not need the input stream's dedicated
/// background thread; its async overloads simply delegate to the synchronous path.
/// </remarks>
[SupportedOSPlatform("windows")]
internal sealed class WindowsConsoleOutputStream: Stream
{
    private readonly nint _handle;
    private readonly ConsoleUtf8ToUtf16Transcoder _transcoder = new();

    /// <summary>Initializes the stream over the raw console output handle.</summary>
    /// <param name="handle">The process's standard console output handle.</param>
    public WindowsConsoleOutputStream(nint handle) => _handle = handle;

    /// <inheritdoc/>
    public override bool CanRead => false;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => true;

    /// <inheritdoc/>
    public override long Length
    {
        [DoesNotReturn]
        get => throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override long Position
    {
        [DoesNotReturn]
        get => throw new NotSupportedException();
        [DoesNotReturn]
        set => throw new NotSupportedException();
    }

    /// <inheritdoc/>
    /// <exception cref="IOException">The console output could not be written.</exception>
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (buffer.IsEmpty)
        {
            return;
        }

        // A UTF-8 sequence can straddle two writes - the renderer's own framing guarantees a
        // write carries whole terminal escape sequences and text, not whole Unicode scalar
        // values - so the decoder's carried-over state, not this call alone, is what keeps a torn
        // multi-byte character intact.
        WriteChars(_transcoder.Transcode(buffer, flush: false));
    }

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    /// <inheritdoc/>
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Write(buffer.Span);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc/>
    /// <remarks><c>WriteConsoleW</c> has no separate buffering stage to flush.</remarks>
    public override void Flush()
    {
    }

    /// <inheritdoc/>
    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    [DoesNotReturn]
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    [DoesNotReturn]
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    [DoesNotReturn]
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <summary>Loops the native write until every transcoded code unit has been accepted.</summary>
    /// <param name="chars">The code units to write, already transcoded from UTF-8.</param>
    /// <exception cref="IOException">The console output could not be written.</exception>
    private unsafe void WriteChars(ReadOnlySpan<char> chars)
    {
        if (chars.IsEmpty)
        {
            return;
        }

        fixed (char* origin = chars)
        {
            var cursor = origin;
            var remaining = chars.Length;

            // WriteConsoleW may accept fewer code units than requested in a single call, so the
            // loop keeps issuing the call against whatever remains rather than assuming one call
            // is enough.
            while (remaining > 0)
            {
                if (!RuntimeInterop.TryWriteConsole(_handle, cursor, (uint) remaining, out var written))
                {
                    throw new IOException(
                        "The console output could not be written.",
                        new Win32Exception(Marshal.GetLastPInvokeError()));
                }

                if (written == 0)
                {
                    throw new IOException("The console output stopped accepting data unexpectedly.");
                }

                cursor += written;
                remaining -= (int) written;
            }
        }
    }
}
