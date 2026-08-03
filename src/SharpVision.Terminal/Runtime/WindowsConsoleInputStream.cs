// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

/// <summary>Wraps the raw Windows console input stream so a pending read can actually be cancelled.</summary>
/// <remarks>
/// <see cref="Console.OpenStandardInput()"/>'s stream reads by blocking synchronously inside the
/// native <c>ReadConsole</c> call, and the framework's default asynchronous read wraps that
/// blocking call on a background thread -- the supplied <see cref="CancellationToken"/> is only
/// checked before that blocking call starts. Once the background thread is inside the native
/// call, cancelling the token has no effect and the read only returns when real input arrives
/// (see #256, where a probe process's pending read hung for the remainder of the test session
/// instead of observing its own 200ms-later cancellation). This wrapper instead asks the OS to
/// abort the pending native call directly via <c>CancelIoEx</c> when the token is cancelled, then
/// translates the resulting failure or short read into <see cref="OperationCanceledException"/>.
/// </remarks>
[SupportedOSPlatform("windows")]
internal sealed class WindowsConsoleInputStream: Stream
{
    private readonly Stream _inner;
    private readonly nint _handle;

    /// <summary>Initializes the wrapper over the raw console input stream and its native handle.</summary>
    /// <param name="inner">The stream returned by <see cref="Console.OpenStandardInput()"/>.</param>
    /// <param name="handle">The same stream's underlying native console input handle.</param>
    public WindowsConsoleInputStream(Stream inner, nint handle)
    {
        _inner = inner;
        _handle = handle;
    }

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc/>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was cancelled before or during the read.
    /// </exception>
    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var registration = cancellationToken.Register(
            static state => RuntimeInterop.TryCancelPendingIo((nint) state!),
            _handle);

        int read;

        try
        {
            // CancellationToken.None: cancellation is handled entirely by the registration above
            // asking the OS to abort the pending native read, not by the framework's own
            // before-the-fact check, which cannot interrupt a read already in flight.
            read = await _inner.ReadAsync(buffer, CancellationToken.None).ConfigureAwait(false);
        }
        catch (IOException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        // A successfully aborted native read can also surface as a zero-length read rather than a
        // thrown exception, depending on the underlying platform implementation. Zero would
        // otherwise be misread as end-of-stream, so it is only trusted when cancellation was never
        // requested.
        return read == 0 && cancellationToken.IsCancellationRequested
            ? throw new OperationCanceledException(cancellationToken)
            : read;
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

    /// <inheritdoc/>
    public override int Read(Span<byte> buffer) => _inner.Read(buffer);

    /// <inheritdoc/>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc/>
    public override void Flush() => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
        }

        base.Dispose(disposing);
    }
}
