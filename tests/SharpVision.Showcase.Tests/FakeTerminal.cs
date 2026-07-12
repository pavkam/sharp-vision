using System.Threading.Channels;

using SharpVision.Terminal.Runtime;
using SharpVision.Terminal.Transport;

namespace SharpVision.Showcase.Tests;

/// <summary>Provides deterministic owned transport and resize streams for the running showcase.</summary>
internal sealed class FakeTerminal: ITransport, IResizeSource
{
    private readonly Channel<byte[]> _input = Channel.CreateUnbounded<byte[]>();
    private readonly Channel<Dimensions> _resize = Channel.CreateUnbounded<Dimensions>();
    private readonly Lock _gate = new();
    private readonly List<byte[]> _writes = [];
    private int _disposed;

    /// <summary>Raised synchronously after one complete write is copied.</summary>
    internal event Action<ReadOnlyMemory<byte>>? Written;

    /// <summary>Initializes empty deterministic input, resize, and captured-output streams.</summary>
    internal FakeTerminal()
    {
    }

    /// <summary>Gets isolated copies of every terminal write.</summary>
    internal IReadOnlyList<byte[]> Writes
    {
        get
        {
            lock (_gate)
            {
                return _writes.Select(static value => value.ToArray()).ToArray();
            }
        }
    }

    /// <summary>Queues exact terminal input bytes.</summary>
    /// <param name="value">The bytes copied into the input stream.</param>
    /// <exception cref="InvalidOperationException">The terminal input stream is closed.</exception>
    internal void QueueInput(ReadOnlySpan<byte> value)
    {
        if (!_input.Writer.TryWrite(value.ToArray()))
        {
            throw new InvalidOperationException("The terminal input stream is closed.");
        }
    }

    /// <summary>Queues one immutable terminal resize record.</summary>
    /// <param name="value">The cell and optional pixel dimensions.</param>
    /// <exception cref="InvalidOperationException">The resize stream is closed.</exception>
    internal void QueueResize(Dimensions value)
    {
        if (!_resize.Writer.TryWrite(value))
        {
            throw new InvalidOperationException("The terminal resize stream is closed.");
        }
    }

    /// <inheritdoc/>
    public async ValueTask<int> ReadAsync(
        Memory<byte> destination,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfZero(destination.Length);
        try
        {
            var value = await _input.Reader.ReadAsync(cancellationToken);

            if (value.Length > destination.Length)
            {
                throw new InvalidOperationException("Queued input exceeds the runtime read buffer.");
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
        var copy = source.ToArray();

        lock (_gate)
        {
            _writes.Add(copy);
        }

        Written?.Invoke(copy);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask FlushAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public async ValueTask<Dimensions> ReadAsync(CancellationToken cancellationToken)
    {
        var value = await _resize.Reader.ReadAsync(cancellationToken);
        return value;
    }

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
