namespace SharpVision.Terminal.Tests.Transport;

/// <summary>Provides a writable stream that rejects read capability.</summary>
internal sealed class WriteOnlyStream: MemoryStream
{
    /// <inheritdoc/>
    public override bool CanRead => false;
}
