namespace SharpVision.Terminal.Tests.Transport;

/// <summary>Provides a readable stream that rejects write capability.</summary>
internal sealed class ReadOnlyStream: MemoryStream
{
    /// <inheritdoc/>
    public override bool CanWrite => false;
}
