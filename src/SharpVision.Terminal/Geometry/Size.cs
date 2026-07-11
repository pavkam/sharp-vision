namespace SharpVision.Terminal.Geometry;

/// <summary>
/// Represents non-negative width and height in documented caller units.
/// </summary>
public readonly record struct Size
{
    /// <summary>Initializes a validated size.</summary>
    /// <param name="width">The non-negative width.</param>
    /// <param name="height">The non-negative height.</param>
    /// <exception cref="ArgumentOutOfRangeException">An extent is negative.</exception>
    public Size(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        Width = width;
        Height = height;
    }

    /// <summary>Gets the horizontal extent.</summary>
    public int Width { get; }

    /// <summary>Gets the vertical extent.</summary>
    public int Height { get; }
}
