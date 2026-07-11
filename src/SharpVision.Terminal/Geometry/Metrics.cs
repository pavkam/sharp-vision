namespace SharpVision.Terminal.Geometry;

/// <summary>
/// Describes positive pixel dimensions for one terminal cell.
/// </summary>
public readonly record struct Metrics
{
    /// <summary>Initializes validated cell pixel dimensions.</summary>
    /// <param name="width">The positive cell width in pixels.</param>
    /// <param name="height">The positive cell height in pixels.</param>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is not positive.</exception>
    public Metrics(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        Width = width;
        Height = height;
    }

    /// <summary>Gets one cell's width in pixels.</summary>
    public int Width { get; }

    /// <summary>Gets one cell's height in pixels.</summary>
    public int Height { get; }
}
