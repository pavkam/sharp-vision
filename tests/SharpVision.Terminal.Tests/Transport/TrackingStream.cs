namespace SharpVision.Terminal.Tests.Transport;

/// <summary>Counts synchronous disposal for stream-ownership tests.</summary>
internal sealed class TrackingStream: MemoryStream
{
    /// <summary>Gets the number of disposal calls.</summary>
    internal int DisposeCount { get; private set; }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        DisposeCount++;
        base.Dispose(disposing);
    }
}
