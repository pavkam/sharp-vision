using System.Threading.Channels;

using SharpVision.Terminal.Runtime;

namespace SharpVision.Terminal.Tests.Support;

/// <summary>Provides deterministic queued resize changes.</summary>
internal sealed class FakeResizeSource: IResizeSource
{
    private readonly Channel<Dimensions> _changes = Channel.CreateUnbounded<Dimensions>();

    /// <summary>Queues one resize observation.</summary>
    /// <param name="value">The immutable dimensions.</param>
    internal void Resize(Dimensions value) => _changes.Writer.TryWrite(value);

    /// <inheritdoc/>
    public ValueTask<Dimensions> ReadAsync(CancellationToken cancellationToken) =>
        _changes.Reader.ReadAsync(cancellationToken);

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _ = _changes.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}
