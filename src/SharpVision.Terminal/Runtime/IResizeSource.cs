namespace SharpVision.Terminal.Runtime;

/// <summary>Provides asynchronous terminal dimension changes without polling callers.</summary>
public interface IResizeSource: IAsyncDisposable
{
    /// <summary>Waits for and returns the next observed dimensions.</summary>
    /// <param name="cancellationToken">Cancels the pending wait.</param>
    /// <returns>The next immutable dimensions.</returns>
    public ValueTask<Dimensions> ReadAsync(CancellationToken cancellationToken);
}
