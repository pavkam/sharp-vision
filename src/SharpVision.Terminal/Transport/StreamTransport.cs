// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Transport;

/// <summary>
/// Adapts readable and writable streams to serialized complete terminal writes.
/// </summary>
public sealed class StreamTransport: ITransport
{
    private readonly Stream _input;
    private readonly Stream _output;
    private readonly bool _leaveOpen;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private int _disposed;

    /// <summary>Initializes a validated stream transport.</summary>
    /// <param name="input">The readable input stream.</param>
    /// <param name="output">The writable output stream.</param>
    /// <param name="leaveOpen">Whether disposal leaves both streams open.</param>
    /// <exception cref="ArgumentNullException">A stream is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="input"/> is unreadable or <paramref name="output"/> is unwritable.
    /// </exception>
    public StreamTransport(Stream input, Stream output, bool leaveOpen = false)
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
        _leaveOpen = leaveOpen;
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
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await _writeGate.WaitAsync().ConfigureAwait(false);

        try
        {
            if (!_leaveOpen)
            {
                await _input.DisposeAsync().ConfigureAwait(false);

                if (!ReferenceEquals(_input, _output))
                {
                    await _output.DisposeAsync().ConfigureAwait(false);
                }
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
