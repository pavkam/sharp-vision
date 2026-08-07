// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Support;

/// <summary>
/// Provides deterministic writes, failures, and backpressure for terminal tests.
/// </summary>
internal sealed class FakeTransport: ITransport
{
    private readonly Queue<Failure> _failures = [];
    private readonly Queue<Exception> _flushFailures = [];
    private TaskCompletionSource? _block;

    /// <summary>Gets copied write calls in observed order.</summary>
    internal List<byte[]> Writes { get; } = [];

    /// <summary>Gets the number of flush calls.</summary>
    internal int Flushes { get; private set; }

    /// <summary>Gets the number of disposal calls.</summary>
    internal int DisposeCount { get; private set; }

    /// <summary>Gets a task completed when a blocked write enters the transport.</summary>
    internal Task WriteStarted { get; private set; } = Task.CompletedTask;

    /// <summary>Gets or sets the exception raised by the next flush.</summary>
    internal Exception? FlushFailure { get; set; }

    /// <summary>Queues an exact flush failure after earlier queued flush outcomes.</summary>
    /// <param name="exception">The exact exception to throw.</param>
    internal void QueueFlushFailure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        _flushFailures.Enqueue(exception);
    }

    /// <summary>Queues a write failure after optionally retaining a prefix.</summary>
    /// <param name="exception">The exact exception to throw.</param>
    /// <param name="prefixBytes">The non-negative retained prefix byte count.</param>
    internal void QueueFailure(Exception exception, int prefixBytes = 0)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentOutOfRangeException.ThrowIfNegative(prefixBytes);
        _failures.Enqueue(new Failure(exception, prefixBytes));
    }

    /// <summary>Blocks subsequent writes until <see cref="Release"/>.</summary>
    internal void Block()
    {
        _block = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        WriteStarted = Task.CompletedTask;
    }

    /// <summary>Releases blocked writes.</summary>
    internal void Release() => _block?.TrySetResult();

    /// <inheritdoc/>
    public ValueTask<int> ReadAsync(
        Memory<byte> destination,
        CancellationToken cancellationToken) => ValueTask.FromResult(0);

    /// <inheritdoc/>
    public ValueTask WriteAsync(
        ReadOnlyMemory<byte> source,
        CancellationToken cancellationToken)
    {
        if (_block is null)
        {
            WriteCore(source.Span);
            return ValueTask.CompletedTask;
        }

        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        WriteStarted = started.Task;
        return new ValueTask(WriteBlockedAsync(source, started, cancellationToken));
    }

    /// <inheritdoc/>
    public ValueTask FlushAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Flushes++;

        if (_flushFailures.TryDequeue(out var queued))
        {
            return ValueTask.FromException(queued);
        }

        if (FlushFailure is { } exception)
        {
            FlushFailure = null;
            return ValueTask.FromException(exception);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        DisposeCount++;
        return ValueTask.CompletedTask;
    }

    private async Task WriteBlockedAsync(
        ReadOnlyMemory<byte> source,
        TaskCompletionSource started,
        CancellationToken cancellationToken)
    {
        started.SetResult();
        await _block!.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        _block = null;
        WriteCore(source.Span);
    }

    private void WriteCore(ReadOnlySpan<byte> source)
    {
        if (_failures.TryDequeue(out var failure))
        {
            var count = Math.Min(source.Length, failure.PrefixBytes);

            if (count > 0)
            {
                Writes.Add(source[..count].ToArray());
            }

            throw failure.Exception;
        }

        Writes.Add(source.ToArray());
    }
}
