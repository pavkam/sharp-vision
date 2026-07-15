// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Consumer.Tests;

/// <summary>Provides deterministic terminal input, output, and resize boundaries for consumer tests.</summary>
internal sealed class ConsumerTerminal: ITransport, IResizeSource
{
    private readonly Channel<byte[]> _input = Channel.CreateUnbounded<byte[]>();
    private readonly Channel<Dimensions> _resize = Channel.CreateUnbounded<Dimensions>();
    private int _disposed;

    /// <summary>Queues one immutable terminal resize record.</summary>
    /// <param name="value">The resize record to publish.</param>
    /// <exception cref="InvalidOperationException">The terminal is already closed.</exception>
    internal void QueueResize(Dimensions value)
    {
        if (!_resize.Writer.TryWrite(value))
        {
            throw new InvalidOperationException("The consumer terminal is closed.");
        }
    }

    /// <inheritdoc/>
    public async ValueTask<int> ReadAsync(
        Memory<byte> destination,
        CancellationToken cancellationToken)
    {
        try
        {
            var value = await _input.Reader.ReadAsync(cancellationToken);

            if (value.Length > destination.Length)
            {
                throw new InvalidOperationException("The queued terminal input exceeds the destination buffer.");
            }

            value.AsSpan().CopyTo(destination.Span);
            return value.Length;
        }
        catch (ChannelClosedException)
        {
            return 0;
        }
    }

    /// <inheritdoc/>
    public ValueTask WriteAsync(
        ReadOnlyMemory<byte> source,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = source.Length;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask FlushAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public async ValueTask<Dimensions> ReadAsync(CancellationToken cancellationToken) =>
        await _resize.Reader.ReadAsync(cancellationToken);

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _ = _input.Writer.TryComplete();
            _ = _resize.Writer.TryComplete();
        }

        return ValueTask.CompletedTask;
    }
}
