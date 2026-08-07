// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

using System.Threading.Channels;

/// <summary>Records host-preflight transport input, output, and disposal.</summary>
internal sealed class ConsoleApplicationTransport: ITransport
{
    private readonly Channel<byte[]> _input = Channel.CreateUnbounded<byte[]>();
    private readonly List<byte[]> _writes = [];
    private readonly List<string>? _disposalOrder;

    /// <summary>Initializes a recorder with an optional shared disposal-order log.</summary>
    /// <param name="disposalOrder">The optional shared log.</param>
    internal ConsoleApplicationTransport(List<string>? disposalOrder = null) =>
        _disposalOrder = disposalOrder;

    /// <summary>Raised after one complete write is copied.</summary>
    internal event Action<ReadOnlyMemory<byte>>? Written;

    /// <summary>Gets isolated bytes supplied to the terminal writer.</summary>
    internal IReadOnlyList<byte[]> Writes => _writes;

    /// <summary>Gets the number of disposal calls.</summary>
    internal int Disposals { get; private set; }

    /// <summary>Gets or sets the exact disposal failure raised after recording the attempt.</summary>
    internal Exception? DisposalFailure { get; set; }

    /// <summary>Queues exact terminal input bytes.</summary>
    /// <param name="value">The non-empty bytes returned by the next read.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is empty.</exception>
    internal void QueueInput(ReadOnlySpan<byte> value)
    {
        if (value.IsEmpty)
        {
            throw new ArgumentException("Terminal input cannot be empty.", nameof(value));
        }

        _input.Writer.TryWrite(value.ToArray()).ShouldBeTrue();
    }

    /// <summary>Completes terminal input as an orderly closure.</summary>
    internal void CloseInput() => _input.Writer.TryComplete().ShouldBeTrue();

    /// <inheritdoc/>
    public async ValueTask<int> ReadAsync(
        Memory<byte> destination,
        CancellationToken cancellationToken)
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
        var copy = source.ToArray();
        _writes.Add(copy);
        Written?.Invoke(copy);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        Disposals++;
        _disposalOrder?.Add("transport");
        _ = _input.Writer.TryComplete();

        return DisposalFailure is { } failure
            ? ValueTask.FromException(failure)
            : ValueTask.CompletedTask;
    }
}
