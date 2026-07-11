namespace SharpVision.Terminal.Clipboard;

/// <summary>Owns completed Kitty clipboard MIME data until disposal.</summary>
public sealed class KittyResult: IDisposable
{
    private bool _disposed;

    /// <summary>Initializes a result from transferred MIME data.</summary>
    /// <param name="items">The owned result items.</param>
    internal KittyResult(KittyMimeData[] items) => Items = items;

    /// <summary>Gets MIME values in terminal delivery order.</summary>
    public IReadOnlyList<KittyMimeData> Items { get; }

    /// <summary>Clears every owned data buffer. Disposal is idempotent.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var item in Items)
        {
            item.Clear();
        }

        _disposed = true;
    }
}
