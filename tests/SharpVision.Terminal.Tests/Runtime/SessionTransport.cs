// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Runtime;

using System.Text;
using System.Threading.Channels;

using SharpVision.Terminal.Transport;

/// <summary>Provides deterministic terminal session input, output, and failures.</summary>
internal sealed class SessionTransport: ITransport
{
    private readonly Channel<byte[]> _input = Channel.CreateUnbounded<byte[]>();
    private readonly List<byte[]> _writes = [];
    private int _writeCount;

    /// <summary>Gets the one-based write call that should fail.</summary>
    internal int FailWriteAt { get; init; }

    /// <summary>Gets the exact injected write failure.</summary>
    internal IOException WriteFailure { get; } = new("write failed");

    /// <summary>Gets an optional exact read failure.</summary>
    internal IOException? ReadFailure { get; init; }

    /// <summary>Gets completion for the first read attempt.</summary>
    internal TaskCompletionSource FirstRead { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets ASCII-decoded concatenated writes.</summary>
    internal string JoinedWrites => string.Concat(_writes.Select(Encoding.ASCII.GetString));

    /// <summary>Queues one owned input chunk.</summary>
    /// <param name="value">The input bytes.</param>
    internal void Input(byte[] value) => _input.Writer.TryWrite(value);

    /// <summary>Completes input as an orderly closure.</summary>
    internal void Close() => _input.Writer.TryComplete();

    /// <inheritdoc/>
    public async ValueTask<int> ReadAsync(
        Memory<byte> destination,
        CancellationToken cancellationToken)
    {
        _ = FirstRead.TrySetResult();

        if (ReadFailure is not null)
        {
            throw ReadFailure;
        }

        try
        {
            var value = await _input.Reader.ReadAsync(cancellationToken);
            value.AsMemory().CopyTo(destination);
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
        _writeCount++;

        if (_writeCount == FailWriteAt)
        {
            return ValueTask.FromException(WriteFailure);
        }

        _writes.Add(source.ToArray());
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask FlushAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _ = _input.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}
