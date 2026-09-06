// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

/// <summary>Reads UTF-8 bytes directly from the raw Windows console input handle.</summary>
/// <remarks>
/// <para>
/// This owns the input handle itself rather than wrapping <see cref="Console.OpenStandardInput()"/>.
/// .NET's own Windows console stream opens either a code-page-dependent <c>ReadFile</c> path or a
/// narrow <c>ReadConsoleA</c> path depending on the process's console input code page - neither of
/// which is UTF-8 aware, so non-ASCII keyboard input and pastes are dropped or mis-decoded on any
/// console that has not run <c>chcp 65001</c>. Calling <c>ReadConsoleW</c> directly and
/// transcoding its UTF-16 code units to UTF-8 (the approach Rust's standard library uses for
/// Windows stdio) bypasses the code page entirely.
/// </para>
/// <para>
/// A native <c>ReadConsoleW</c> call blocks synchronously for however long it takes for a key or
/// paste to arrive, often for the remainder of the process's lifetime, and console handles have
/// no overlapped/IOCP-based asynchronous variant the way a Unix tty's <see cref="FileStream"/>
/// does. Every <c>ReadAsync</c> call therefore runs the blocking call on a pooled
/// <see cref="ThreadPool"/> thread rather than a dedicated
/// <see cref="TaskCreationOptions.LongRunning"/> thread - the model .NET's own console stream used
/// before it grew a dedicated reader thread. A dedicated thread only pays for itself when it is
/// created once and reused for the input's entire lifetime; this stream instead issues one native
/// call per <c>ReadAsync</c>, so a <see cref="TaskCreationOptions.LongRunning"/> task would spin up
/// and tear down a whole OS thread for every keystroke, mouse report, and
/// aborted-and-retried Ctrl+C read. A pooled thread is safe here because only one native read is
/// ever outstanding at a time - callers await each <c>ReadAsync</c> before issuing the next - so at
/// most one pool worker blocks, and cancellation unblocks it almost immediately via
/// <c>CancelIoEx</c> rather than parking it for however long the next key takes to arrive.
/// </para>
/// <para>
/// Because the blocking call cannot be interrupted by cancelling a <see cref="CancellationToken"/>
/// once it has started, cancellation instead asks the OS to abort the pending native call directly
/// via <c>CancelIoEx</c>, then translates the resulting failure or short read into
/// <see cref="OperationCanceledException"/>. A probe process's pending read once hung for the
/// remainder of the test session instead of observing its own 200ms-later cancellation before this
/// was added.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
internal sealed class WindowsConsoleInputStream: Stream
{
    private readonly nint _handle;
    private readonly ConsoleUtf16ToUtf8Transcoder _transcoder = new();

    // Sized generously enough that ordinary keyboard input and pastes transcode in one native
    // call; a read larger than this simply spans more native calls and drains through the pending
    // buffer exactly as a caller-buffer-limited read would.
    private readonly char[] _nativeBuffer = new char[256];

    /// <summary>Initializes the stream over the raw console input handle.</summary>
    /// <param name="handle">The process's standard console input handle.</param>
    public WindowsConsoleInputStream(nint handle) => _handle = handle;

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => false;

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
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was cancelled before or during the read.
    /// </exception>
    /// <exception cref="IOException">The console input could not be read.</exception>
    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Bytes already transcoded from an earlier native call but not yet delivered - because
        // the caller's own buffer was smaller than the transcoded output, buffers as small as one
        // byte are used for interactive input - take priority over blocking for more input.
        if (_transcoder.HasPendingOutput)
        {
            return _transcoder.DrainPending(buffer.Span);
        }

        using var registration = cancellationToken.Register(
            static state => RuntimeInterop.TryCancelPendingIo((nint) state!),
            _handle);

        while (true)
        {
            // CancellationToken.None: cancellation is handled entirely by the registration above
            // asking the OS to abort the pending native read, not by the framework's own
            // before-the-fact check, which cannot interrupt a read already in flight. A pooled
            // Task.Run is deliberate, not an oversight - see the class remarks.
            var (succeeded, charsRead, error) = await Task.Run(
                ReadConsoleOnce,
                CancellationToken.None).ConfigureAwait(false);

            if (!succeeded)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (error == RuntimeInterop.ErrorOperationAborted)
                {
                    // Ctrl+C was consumed by ENABLE_PROCESSED_INPUT as a signal rather than
                    // delivered as an input record; the abort is real but nobody asked for
                    // cancellation, so the read is simply reissued.
                    continue;
                }

                throw new IOException(
                    "The console input could not be read.",
                    new Win32Exception(error));
            }

            // A successfully aborted native read can also surface as a zero-length read rather
            // than a thrown failure, depending on the underlying platform implementation. Zero
            // would otherwise be misread as end-of-stream, so it is only trusted when
            // cancellation was never requested.
            return charsRead == 0 && cancellationToken.IsCancellationRequested
                ? throw new OperationCanceledException(cancellationToken)
                : charsRead == 0
                    ? 0
                    : _transcoder.Transcode(_nativeBuffer.AsSpan(0, (int) charsRead), flush: false, buffer.Span);
        }
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) =>
        ReadAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();

    /// <inheritdoc/>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc/>
    [DoesNotReturn]
    public override void Flush() => throw new NotSupportedException();

    /// <inheritdoc/>
    [DoesNotReturn]
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    [DoesNotReturn]
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    [DoesNotReturn]
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <summary>Issues one blocking native read on the calling (pooled) thread.</summary>
    /// <returns>
    /// Whether the call succeeded, the number of code units read on success, and the Win32 error
    /// captured immediately on failure - captured here, rather than after crossing back onto the
    /// awaiting continuation, because the last Win32 error is thread-local and this call may
    /// resume on a different thread.
    /// </returns>
    private (bool Succeeded, uint CharsRead, int Error) ReadConsoleOnce()
    {
        unsafe
        {
            fixed (char* pointer = _nativeBuffer)
            {
                var succeeded = RuntimeInterop.TryReadConsole(_handle, pointer, (uint) _nativeBuffer.Length, out var charsRead);
                var error = succeeded ? 0 : Marshal.GetLastPInvokeError();

                return (succeeded, charsRead, error);
            }
        }
    }
}
