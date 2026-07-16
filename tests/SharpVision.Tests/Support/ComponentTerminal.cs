// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

using System.Threading.Channels;

using SharpVision.Terminal.Transport;

/// <summary>Provides deterministic terminal input, resize, and modeled output for component tests.</summary>
internal sealed class ComponentTerminal: ITransport, IResizeSource
{
    private readonly Channel<byte[]> _input = Channel.CreateUnbounded<byte[]>();
    private readonly Channel<Dimensions> _resize = Channel.CreateUnbounded<Dimensions>();
    private int _disposed;

    /// <summary>Initializes a terminal whose screen uses the positive fixed size.</summary>
    /// <param name="size">The positive screen dimensions.</param>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is not positive.</exception>
    internal ComponentTerminal(Size size) => Screen = new ComponentScreen(size);

    /// <summary>Gets the independently modeled terminal screen.</summary>
    internal ComponentScreen Screen { get; }

    /// <summary>Queues one immutable resize record.</summary>
    /// <param name="value">The terminal dimensions.</param>
    internal void QueueResize(Dimensions value) =>
        _resize.Writer.TryWrite(value).ShouldBeTrue();

    /// <inheritdoc/>
    public async ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken)
    {
        try
        {
            var value = await _input.Reader.ReadAsync(cancellationToken);
            value.AsSpan().CopyTo(destination.Span);
            return value.Length;
        }
        catch (ChannelClosedException)
        {
            return 0;
        }
    }

    /// <inheritdoc/>
    public ValueTask WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Screen.Apply(source.Span);
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
