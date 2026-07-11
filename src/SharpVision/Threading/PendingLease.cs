namespace SharpVision.Threading;

/// <summary>Suppresses dispatcher idle while asynchronous work remains pending.</summary>
internal sealed class PendingLease(Dispatcher owner): IDisposable
{
    private Dispatcher? _owner = owner;

    /// <summary>Releases the pending-work count exactly once.</summary>
    public void Dispose() => Interlocked.Exchange(ref _owner, null)?.ReleasePending();
}
