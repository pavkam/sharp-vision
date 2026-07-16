// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

using System.Threading.Channels;

using SharpVision.Terminal.Transport;

/// <summary>Provides deterministic terminal input, resize, and modeled output for component tests.</summary>
internal sealed class ComponentTerminal: ITransport, IResizeSource
{
    private readonly Channel<(byte[] Bytes, TaskCompletionSource Consumed)> _input =
        Channel.CreateUnbounded<(byte[] Bytes, TaskCompletionSource Consumed)>();
    private readonly Channel<(Dimensions Value, TaskCompletionSource Consumed)> _resize =
        Channel.CreateUnbounded<(Dimensions Value, TaskCompletionSource Consumed)>();
    private ComponentScreen _screen;
    private int _disposed;

    /// <summary>Initializes a terminal whose screen uses the positive fixed size.</summary>
    /// <param name="size">The positive screen dimensions.</param>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is not positive.</exception>
    internal ComponentTerminal(Size size) => _screen = new ComponentScreen(size);

    /// <summary>Gets the independently modeled terminal screen.</summary>
    internal ComponentScreen Screen => Volatile.Read(ref _screen);

    /// <summary>Queues immutable terminal input and returns a signal completed when transport reads it.</summary>
    /// <param name="value">The non-empty terminal input bytes.</param>
    /// <returns>A task completed after the bytes are copied into the session read buffer.</returns>
    /// <exception cref="ArgumentException"><paramref name="value"/> is empty.</exception>
    /// <exception cref="ObjectDisposedException">The terminal is disposed.</exception>
    internal Task QueueInput(ReadOnlySpan<byte> value)
    {
        if (value.IsEmpty)
        {
            throw new ArgumentException("Terminal input cannot be empty.", nameof(value));
        }

        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        var consumed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _input.Writer.TryWrite((value.ToArray(), consumed)).ShouldBeTrue();
        return consumed.Task;
    }

    /// <summary>Queues one immutable resize record.</summary>
    /// <param name="value">The positive terminal dimensions.</param>
    /// <returns>A task completed when the session consumes the immutable resize record.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A cell dimension is not positive.</exception>
    /// <exception cref="ObjectDisposedException">The terminal is disposed.</exception>
    internal Task QueueResize(Dimensions value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value.Cells.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value.Cells.Height);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        var consumed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = Interlocked.Exchange(ref _screen, new ComponentScreen(value.Cells));
        _resize.Writer.TryWrite((value, consumed)).ShouldBeTrue();
        return consumed.Task;
    }

    /// <inheritdoc/>
    public async ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken)
    {
        try
        {
            var (bytes, consumed) = await _input.Reader.ReadAsync(cancellationToken);

            try
            {
                bytes.AsSpan().CopyTo(destination.Span);
                _ = consumed.TrySetResult();
                return bytes.Length;
            }
            catch (Exception exception)
            {
                _ = consumed.TrySetException(exception);
                throw;
            }
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
    public async ValueTask<Dimensions> ReadAsync(CancellationToken cancellationToken)
    {
        var (value, consumed) = await _resize.Reader.ReadAsync(cancellationToken);
        _ = consumed.TrySetResult();
        return value;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _ = _input.Writer.TryComplete();
            _ = _resize.Writer.TryComplete();

            while (_input.Reader.TryRead(out var value))
            {
                _ = value.Consumed.TrySetCanceled();
            }

            while (_resize.Reader.TryRead(out var value))
            {
                _ = value.Consumed.TrySetCanceled();
            }
        }

        return ValueTask.CompletedTask;
    }
}
