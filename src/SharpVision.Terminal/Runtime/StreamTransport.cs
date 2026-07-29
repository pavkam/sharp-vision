// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

/// <summary>
/// Adapts readable and writable streams to serialized complete terminal writes.
/// </summary>
[PublicAPI]
public sealed class StreamTransport: ITransport
{
    private readonly Stream _input;
    private readonly Stream _output;
    private readonly bool _leaveInputOpen;
    private readonly bool _leaveOutputOpen;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private int _disposed;

    /// <summary>Initializes a validated stream transport with one shared ownership decision.</summary>
    /// <remarks>
    /// Use this overload when the caller's relationship to both streams is the same. A host that
    /// opens its own input device but borrows a process-owned output stream must instead use the
    /// per-stream overload, because a single flag would either leak the opened device or close a
    /// stream the transport never owned.
    /// </remarks>
    /// <param name="input">The readable input stream.</param>
    /// <param name="output">The writable output stream.</param>
    /// <param name="leaveOpen">Whether disposal leaves both streams open.</param>
    /// <exception cref="ArgumentNullException">A stream is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="input"/> is unreadable or <paramref name="output"/> is unwritable.
    /// </exception>
    public StreamTransport(Stream input, Stream output, bool leaveOpen = false) : this(
        input,
        output,
        leaveOpen,
        leaveOpen)
    {
    }

    /// <summary>Initializes a validated stream transport with independent per-stream ownership.</summary>
    /// <remarks>
    /// Ownership is decided separately for each stream so a transport can own the device it was
    /// handed while borrowing another. When both parameters reference the same stream instance and
    /// either flag claims ownership, disposal closes that single stream exactly once.
    /// </remarks>
    /// <param name="input">The readable input stream.</param>
    /// <param name="output">The writable output stream.</param>
    /// <param name="leaveInputOpen">Whether disposal leaves <paramref name="input"/> open.</param>
    /// <param name="leaveOutputOpen">Whether disposal leaves <paramref name="output"/> open.</param>
    /// <exception cref="ArgumentNullException">A stream is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="input"/> is unreadable or <paramref name="output"/> is unwritable.
    /// </exception>
    public StreamTransport(Stream input, Stream output, bool leaveInputOpen, bool leaveOutputOpen)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        if (!input.CanRead)
        {
            throw new ArgumentException("The input stream must be readable.", nameof(input));
        }

        if (!output.CanWrite)
        {
            throw new ArgumentException("The output stream must be writable.", nameof(output));
        }

        _input = input;
        _output = output;
        _leaveInputOpen = leaveInputOpen;
        _leaveOutputOpen = leaveOutputOpen;
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">The transport is disposed.</exception>
    public ValueTask<int> ReadAsync(
        Memory<byte> destination,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        return _input.ReadAsync(destination, cancellationToken);
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">The transport is disposed.</exception>
    public async ValueTask WriteAsync(
        ReadOnlyMemory<byte> source,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            ThrowIfDisposed();
            await _output.WriteAsync(source, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ = _writeGate.Release();
        }
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">The transport is disposed.</exception>
    public async ValueTask FlushAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            ThrowIfDisposed();
            await _output.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ = _writeGate.Release();
        }
    }

    /// <summary>Disposes owned streams after pending serialized writes leave the gate.</summary>
    /// <remarks>
    /// Each stream is released according to its own ownership flag. A borrowed stream is never
    /// closed, and a single stream supplied as both input and output is closed at most once.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await _writeGate.WaitAsync().ConfigureAwait(false);

        try
        {
            var shared = ReferenceEquals(_input, _output);

            if (!_leaveInputOpen)
            {
                await _input.DisposeAsync().ConfigureAwait(false);
            }

            // A stream passed as both input and output is one resource. Skip the second release
            // when the input branch already closed it, but still honor output ownership when only
            // the output side claims it.
            if (!_leaveOutputOpen && (!shared || _leaveInputOpen))
            {
                await _output.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            _ = _writeGate.Release();
            _writeGate.Dispose();
        }
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
}
