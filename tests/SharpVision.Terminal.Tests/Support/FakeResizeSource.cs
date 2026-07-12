using System.Threading.Channels;

using SharpVision.Terminal.Runtime;

namespace SharpVision.Terminal.Tests.Support;

/// <summary>Provides deterministic queued resize changes.</summary>
internal sealed class FakeResizeSource: IResizeSource
{
    private readonly Channel<Dimensions> _changes = Channel.CreateUnbounded<Dimensions>();
    private int _readCount;

    /// <summary>Gets or initializes the completed-read count that signals observation.</summary>
    internal int SignalAfterReads { get; init; } = int.MaxValue;

    /// <summary>Gets completion when the configured number of resize reads finish.</summary>
    internal TaskCompletionSource ReadsObserved { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Queues one resize observation.</summary>
    /// <param name="value">The immutable dimensions.</param>
    internal void Resize(Dimensions value) => _changes.Writer.TryWrite(value);

    /// <inheritdoc/>
    public async ValueTask<Dimensions> ReadAsync(CancellationToken cancellationToken)
    {
        var value = await _changes.Reader.ReadAsync(cancellationToken);

        if (Interlocked.Increment(ref _readCount) >= SignalAfterReads)
        {
            _ = ReadsObserved.TrySetResult();
        }

        return value;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _ = _changes.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}
